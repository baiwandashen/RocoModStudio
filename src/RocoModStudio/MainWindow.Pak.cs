using System.Windows;
using RocoModStudio.Models;

namespace RocoModStudio;

public partial class MainWindow
{
    private async void PakInfo_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("读取 PAK 信息", async () =>
        {
            var pak = RequireFile(PakFileBox.Text, "PAK 文件");
            var result = await RunRepakAsync("info", pak);
            AppendProcessOutput(result.StandardOutput);
            _settings.LastPakPath = pak;
            _settingsService.Save(_settings);
        });
    }

    private async void PakList_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("列出 PAK 文件", async () =>
        {
            var pak = RequireFile(PakFileBox.Text, "PAK 文件");
            var result = await RunRepakAsync("list", pak);
            PakEntriesList.Items.Clear();
            foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                PakEntriesList.Items.Add(line);
            }
            AppendLog($"共 {PakEntriesList.Items.Count} 个条目。", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void PakHash_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("生成 PAK 哈希清单", async () =>
        {
            var pak = RequireFile(PakFileBox.Text, "PAK 文件");
            var result = await RunRepakAsync("hash-list", pak);
            var report = Path.ChangeExtension(pak, ".sha256.txt");
            File.WriteAllText(report, result.StandardOutput);
            PakEntriesList.Items.Clear();
            foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(500))
            {
                PakEntriesList.Items.Add(line);
            }
            AppendLog($"哈希清单已保存：{report}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void PakUnpack_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("解包 PAK", async () =>
        {
            var pak = RequireFile(PakFileBox.Text, "PAK 文件");
            var output = string.IsNullOrWhiteSpace(PakUnpackOutputBox.Text)
                ? Path.Combine(Path.GetDirectoryName(pak)!, Path.GetFileNameWithoutExtension(pak))
                : PakUnpackOutputBox.Text.Trim();
            Directory.CreateDirectory(output);
            await RunRepakAsync("unpack", pak, "--output", output, "--force");
            PakUnpackOutputBox.Text = output;
            AppendLog($"已解包到：{output}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void PakPack_Click(object sender, RoutedEventArgs e)
    {
        await PackDirectoryAsync(PackSourceBox.Text, PackOutputBox.Text, PackMountPointBox.Text, GetPakVersion(PakVersionCombo));
    }

    private async void ModPack_Click(object sender, RoutedEventArgs e)
    {
        await PackDirectoryAsync(PackModFolderBox.Text, PackModOutputBox.Text, PackModMountBox.Text, GetPakVersion(PackModVersionCombo));
    }

    private async void ModVerify_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("回读检查发布 PAK", async () =>
        {
            var pak = RequireFile(PackModOutputBox.Text, "输出 PAK");
            await RunRepakAsync("info", pak);
            var result = await RunRepakAsync("list", pak);
            var expected = Directory.EnumerateFiles(PackModFolderBox.Text, "*", SearchOption.AllDirectories).Count();
            var actual = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            AppendLog($"目录文件 {expected}，PAK 条目 {actual}。", expected == actual ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Orange);
        });
    }

    private async Task PackDirectoryAsync(string source, string output, string mountPoint, string version)
    {
        await RunOperationAsync("打包 PAK", async () =>
        {
            source = RequireDirectory(source, "pack-root 源目录");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("请选择输出 PAK 路径。");
            if (!output.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) output += ".pak";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            await RunRepakAsync("pack", source, output, "--mount-point", string.IsNullOrWhiteSpace(mountPoint) ? "../../../" : mountPoint, "--version", version);
            PackOutputBox.Text = output;
            AppendLog($"PAK 已生成：{output}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async Task<ProcessResult> RunRepakAsync(params string[] commandArguments)
    {
        var repak = _toolLocator.FindRepak(_settings) ?? throw new InvalidOperationException("未找到 repak.exe，请在“工具与设置”中配置。");
        var arguments = new List<string>();
        var aesKey = string.IsNullOrWhiteSpace(PakAesKeyBox.Text) ? null : PakAesKeyBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(aesKey))
        {
            arguments.Add("--aes-key");
            arguments.Add(aesKey);
        }
        arguments.AddRange(commandArguments);
        var result = await _processRunner.RunAsync(repak, arguments, cancellationToken: _operationCancellation?.Token ?? default);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"repak 返回代码 {result.ExitCode}" : result.StandardError.Trim());
        }
        return result;
    }

    private void AppendProcessOutput(string output)
    {
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            AppendLog(line);
        }
    }

    private static string RequireDirectory(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !Directory.Exists(value)) throw new DirectoryNotFoundException($"请选择有效的{label}。");
        return Path.GetFullPath(value);
    }

    private static string RequireFile(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value)) throw new FileNotFoundException($"请选择有效的{label}。", value);
        return Path.GetFullPath(value);
    }
}
