using System.Text.Json;
using System.Windows;
using RocoModStudio.Models;

namespace RocoModStudio;

public partial class MainWindow
{
    private async void TextureCheck_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("检查贴图资源版本", async () =>
        {
            var asset = RequireFile(TextureAssetBox.Text, "纹理 uasset");
            var arguments = BuildDdsArguments(asset, null, "check", "dds");
            arguments.Add("--save_detected_version");
            var result = await RunDdsToolAsync(arguments);
            AppendProcessOutput(result.StandardOutput);
            AppendProcessOutput(result.StandardError);
        });
    }

    private async void TextureRun_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("执行贴图操作", async () =>
        {
            var asset = RequireFile(TextureAssetBox.Text, "纹理 uasset");
            if (string.IsNullOrWhiteSpace(TextureOutputBox.Text))
            {
                TextureOutputBox.Text = _project is not null
                    ? Path.Combine(_project.WorkingDirectory, "textures")
                    : Path.Combine(Path.GetDirectoryName(asset)!, "texture-output");
            }
            Directory.CreateDirectory(TextureOutputBox.Text);

            var modes = new[] { "inject", "export", "check", "convert", "parse" };
            var mode = modes[Math.Clamp(TextureModeCombo.SelectedIndex, 0, modes.Length - 1)];
            var textureFile = string.IsNullOrWhiteSpace(TextureImageBox.Text) ? null : RequireFile(TextureImageBox.Text, "输入图像");
            if (mode is "inject" or "convert" && textureFile is null)
            {
                throw new InvalidOperationException("该模式需要输入图像或贴图文件。");
            }

            var format = (TextureFormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant() ?? "dds";
            var arguments = BuildDdsArguments(asset, textureFile, mode, format);
            var result = await RunDdsToolAsync(arguments);
            AppendProcessOutput(result.StandardOutput);
            AppendProcessOutput(result.StandardError);
            AppendLog($"贴图操作输出目录：{TextureOutputBox.Text}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private List<string> BuildDdsArguments(string assetPath, string? texturePath, string mode, string format)
    {
        var script = _toolLocator.FindDdsScript(_settings) ?? throw new InvalidOperationException("未找到 UE4-DDS-Tools main.py。");
        var arguments = new List<string> { script, assetPath };
        if (!string.IsNullOrWhiteSpace(texturePath)) arguments.Add(texturePath);
        arguments.Add("--save_folder");
        arguments.Add(string.IsNullOrWhiteSpace(TextureOutputBox.Text) ? Path.Combine(Path.GetDirectoryName(assetPath)!, "texture-output") : TextureOutputBox.Text);
        arguments.Add("--mode");
        arguments.Add(mode);
        arguments.Add("--version");
        arguments.Add("4.26");
        if (mode == "export")
        {
            arguments.Add("--export_as");
            arguments.Add(format);
        }
        if (mode == "convert")
        {
            arguments.Add("--convert_to");
            arguments.Add(format);
        }
        if (TextureNoMipmapsCheck.IsChecked == true) arguments.Add("--no_mipmaps");
        if (TextureForceUncompressedCheck.IsChecked == true) arguments.Add("--force_uncompressed");
        return arguments;
    }

    private async Task<ProcessResult> RunDdsToolAsync(IEnumerable<string> arguments)
    {
        var script = _toolLocator.FindDdsScript(_settings) ?? throw new InvalidOperationException("未找到 UE4-DDS-Tools main.py。");
        var python = _toolLocator.FindDdsPython(_settings, script) ?? throw new InvalidOperationException("未找到 UE4-DDS-Tools 的 Python 运行时。");
        var result = await _processRunner.RunAsync(python, arguments, Path.GetDirectoryName(script), _operationCancellation?.Token ?? default);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"贴图工具返回代码 {result.ExitCode}" : result.StandardError.Trim());
        }
        return result;
    }

    private async void NrcRun_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("NRC 结构转换", async () =>
        {
            var input = RequireDirectory(NrcInputBox.Text, "Cooked 资源目录");
            if (string.IsNullOrWhiteSpace(NrcOutputBox.Text))
            {
                NrcOutputBox.Text = _project is not null
                    ? Path.Combine(_project.WorkingDirectory, "nrc-output")
                    : Path.Combine(input, "Converted");
            }
            var output = NrcOutputBox.Text.Trim();
            var types = new[] { "All", "Texture", "StaticMesh", "SkeletalMesh", "AnimSequence", "Material" };
            var type = types[Math.Clamp(NrcAssetTypeCombo.SelectedIndex, 0, types.Length - 1)];

            var result = await Task.Run(() => _assetEngine.ConvertToNrc(input, output, type, "VER_UE4_26", message => Dispatcher.Invoke(() => AppendLog(message))));
            var report = new
            {
                generatedAt = DateTimeOffset.Now,
                input,
                output,
                assetType = type,
                result.Total,
                result.Converted,
                result.Skipped,
                warnings = result.Messages
            };
            Directory.CreateDirectory(output);
            var reportPath = Path.Combine(output, "nrc-conversion-report.json");
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            AppendLog($"转换完成：{result.Converted} 已转换，{result.Skipped} 跳过。报告：{reportPath}", System.Windows.Media.Brushes.LightGreen);
        });
    }
}


