using System.Diagnostics;
using System.Text.Json;
using System.Windows;

namespace RocoModStudio;

public partial class MainWindow
{
    private async void BlenderPrepare_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("建立 Blender 工程", async () =>
        {
            var input = RequireFile(ModelSourceBox.Text, "输入模型");
            EnsureBlenderDefaults();
            await RunBlenderBridgeAsync("prepare", input, BlendFileBox.Text, BlenderExportDirBox.Text, GetBlenderExportFormat());
            AppendLog($"Blender 工程已建立：{BlendFileBox.Text}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private void BlenderOpenInteractive_Click(object sender, RoutedEventArgs e)
    {
        EnsureBlenderDefaults();
        var blender = _toolLocator.FindBlender(_settings) ?? throw new InvalidOperationException("未找到 Blender，请在设置中配置。");
        if (!File.Exists(BlendFileBox.Text)) throw new FileNotFoundException("Blender 工程不存在，请先建立工程。", BlendFileBox.Text);
        Process.Start(new ProcessStartInfo { FileName = blender, Arguments = $"\"{BlendFileBox.Text}\"", WorkingDirectory = Path.GetDirectoryName(blender)!, UseShellExecute = true });
        AppendLog("已打开 Blender。保存 .blend 后回到本程序执行导出。", System.Windows.Media.Brushes.LightGreen);
    }

    private async void BlenderExportEdited_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("导出 Blender 编辑结果", async () =>
        {
            EnsureBlenderDefaults();
            if (!File.Exists(BlendFileBox.Text)) throw new FileNotFoundException("Blender 工程不存在。", BlendFileBox.Text);
            await RunBlenderBridgeAsync("export", ModelSourceBox.Text, BlendFileBox.Text, BlenderExportDirBox.Text, GetBlenderExportFormat());
            AppendLog($"模型已导出到：{BlenderExportDirBox.Text}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async Task RunBlenderBridgeAsync(string mode, string input, string blend, string outputDirectory, string format)
    {
        var blender = _toolLocator.FindBlender(_settings) ?? throw new InvalidOperationException("未找到 Blender，请在设置中配置。");
        var script = Path.Combine(AppContext.BaseDirectory, "Tools", "Bridges", "RocoBlenderBridge.py");
        if (!File.Exists(script)) throw new FileNotFoundException("缺少 Blender 桥接脚本。", script);
        Directory.CreateDirectory(outputDirectory);
        var arguments = new List<string>
        {
            "--background", "--python", script, "--", mode,
            "--input", input,
            "--blend", blend,
            "--output-dir", outputDirectory,
            "--name", string.IsNullOrWhiteSpace(ModelNameBox.Text) ? "RocoModModel" : ModelNameBox.Text.Trim(),
            "--format", format
        };
        var result = await _processRunner.RunAsync(blender, arguments, Path.GetDirectoryName(blender)!, _operationCancellation?.Token ?? default);
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"Blender 返回代码 {result.ExitCode}" : result.StandardError.Trim());
    }

    private async void UnrealImport_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("导入模型到 Unreal 项目", async () =>
        {
            var project = RequireFile(UeProjectBox.Text, "Unreal .uproject");
            var editor = FirstExisting(EmptyToNull(UeEditorBox.Text), _toolLocator.FindUnrealEditor(_settings)) ?? throw new InvalidOperationException("未找到 UnrealEditor-Cmd.exe。");
            var source = RequireFile(ResolveBlenderOutputModel(), "Blender 导出模型");
            var bridge = Path.Combine(AppContext.BaseDirectory, "Tools", "Bridges", "RocoUnrealImportBridge.py");
            if (!File.Exists(bridge)) throw new FileNotFoundException("缺少 Unreal Python 桥接脚本。", bridge);
            var configPath = Path.Combine(_project?.WorkingDirectory ?? Path.GetDirectoryName(project)!, "unreal-import.json");
            var config = new
            {
                source,
                destinationPath = UeDestinationPathBox.Text.Trim(),
                assetName = string.IsNullOrWhiteSpace(ModelNameBox.Text) ? Path.GetFileNameWithoutExtension(source) : ModelNameBox.Text.Trim(),
                skeletonPath = UeSkeletonPathBox.Text.Trim()
            };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var arguments = new List<string> { project, "-run=pythonscript", $"-script={bridge}", "--", configPath, "-unattended", "-nosplash", "-NullRHI" };
            var result = await _processRunner.RunAsync(editor, arguments, Path.GetDirectoryName(editor)!, _operationCancellation?.Token ?? default);
            if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"Unreal 返回代码 {result.ExitCode}" : result.StandardError.Trim());
            UeCookedContentBox.Text = GetCookedContentDirectory(project, GetSelectedCookPlatform());
            AppendLog($"Unreal 导入完成：{UeDestinationPathBox.Text}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void UnrealCook_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("Cook Unreal Mod 资源", async () =>
        {
            var project = RequireFile(UeProjectBox.Text, "Unreal .uproject");
            var runUat = FirstExisting(EmptyToNull(UeRunUatBox.Text), _toolLocator.FindRunUat(_settings)) ?? throw new InvalidOperationException("未找到 RunUAT.bat。");
            var platform = GetSelectedCookPlatform();
            var archive = _project is not null ? Path.Combine(_project.WorkingDirectory, "unreal-cook") : Path.Combine(Path.GetDirectoryName(project)!, "RocoModCook");
            Directory.CreateDirectory(archive);
            var arguments = new List<string>
            {
                "BuildCookRun", $"-project={project}", "-noP4", $"-platform={platform}", "-clientconfig=Development",
                "-cook", "-build", "-stage", "-pak", "-archive", $"-archivedirectory={archive}"
            };
            var result = await _processRunner.RunAsync(runUat, arguments, Path.GetDirectoryName(runUat)!, _operationCancellation?.Token ?? default);
            if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"RunUAT 返回代码 {result.ExitCode}" : result.StandardError.Trim());
            UeCookedContentBox.Text = FindCookedContent(archive) ?? GetCookedContentDirectory(project, platform);
            AppendLog($"Cook 完成：{UeCookedContentBox.Text}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void CopyCookedToPack_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("复制 Cooked 到 pack-root", async () =>
        {
            var cooked = RequireDirectory(UeCookedContentBox.Text, "Cooked Content 目录");
            var packRoot = RequireDirectory(UePackRootBox.Text, "pack-root 目录");
            var projectName = _project?.Name ?? new DirectoryInfo(packRoot).Name;
            var destination = Path.Combine(packRoot, projectName, "Content");
            var count = await Task.Run(() => CopyDirectoryRecursive(cooked, destination));
            AppendLog($"已复制 {count:N0} 个 Cooked 文件到：{destination}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private void EnsureBlenderDefaults()
    {
        var blender = _toolLocator.FindBlender(_settings);
        if (blender is not null && string.IsNullOrWhiteSpace(SetBlenderBox.Text)) SetBlenderBox.Text = blender;
        var root = _project is not null ? Path.Combine(_project.WorkingDirectory, "blender") : Path.Combine(Path.GetTempPath(), "RocoModStudio", "blender");
        if (string.IsNullOrWhiteSpace(BlendFileBox.Text)) BlendFileBox.Text = Path.Combine(root, $"{GetModelName()}.blend");
        if (string.IsNullOrWhiteSpace(BlenderExportDirBox.Text)) BlenderExportDirBox.Text = Path.Combine(root, "exports");
        if (string.IsNullOrWhiteSpace(UePackRootBox.Text) && _project is not null) UePackRootBox.Text = _project.PackRoot;
    }

    private string GetModelName() => string.IsNullOrWhiteSpace(ModelNameBox.Text) ? "RocoModModel" : ModelNameBox.Text.Trim();

    private string GetBlenderExportFormat() => BlenderExportFormatCombo.SelectedIndex switch { 1 => "glb", 2 => "gltf", _ => "fbx" };

    private string ResolveBlenderOutputModel()
    {
        var extension = GetBlenderExportFormat();
        var candidate = Path.Combine(BlenderExportDirBox.Text, $"{GetModelName()}.{extension}");
        return File.Exists(candidate) ? candidate : throw new FileNotFoundException("找不到 Blender 导出的模型，请先执行导出。", candidate);
    }

    private static string? FirstExisting(params string?[] paths) => paths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

    private static string GetCookedContentDirectory(string uproject, string platform) => Path.Combine(Path.GetDirectoryName(uproject)!, "Saved", "Cooked", platform, Path.GetFileNameWithoutExtension(uproject), "Content");
    private string GetSelectedCookPlatform() => (UeCookPlatformCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Android_ASTC";

    private static string? FindCookedContent(string root)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root, "Content", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static int CopyDirectoryRecursive(string source, string destination)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
            count++;
        }
        return count;
    }
}



