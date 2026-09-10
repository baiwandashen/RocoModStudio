using System.Text;
using System.Text.Json;
using System.Windows;

namespace RocoModStudio;

public partial class MainWindow
{
    private async void RunDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("一键排查", async () =>
        {
            var report = new List<string>();
            var warnings = 0;
            var errors = 0;

            void Add(string level, string message)
            {
                report.Add($"[{level}] {message}");
                if (level == "WARN") warnings++;
                if (level == "ERROR") errors++;
            }

            try
            {
                var statuses = await Task.Run(() => _toolLocator.Detect(_settings));
                foreach (var status in statuses)
                    Add(status.Available ? "OK" : "WARN", $"{status.Name}: {(status.Available ? status.Detail : "未找到或未配置")}");
            }
            catch (Exception ex)
            {
                Add("ERROR", $"工具检测失败: {ex.Message}");
            }

            var paks = !string.IsNullOrWhiteSpace(OneClickPaksBox.Text) ? OneClickPaksBox.Text.Trim() : GamePaksBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(paks) || !Directory.Exists(paks)) Add("ERROR", "Paks 目录不存在或未选择。");
            else
            {
                try
                {
                    var pakCount = Directory.EnumerateFiles(paks, "*.pak", SearchOption.AllDirectories).Count();
                    Add(pakCount > 0 ? "OK" : "WARN", $"Paks 目录可访问，发现 {pakCount} 个 .pak。");
                }
                catch (Exception ex) { Add("ERROR", $"Paks 目录不可读: {ex.Message}"); }
            }

            var aes = !string.IsNullOrWhiteSpace(OneClickAesBox.Text) ? OneClickAesBox.Text.Trim() : GameAesKeyBox.Text.Trim();
            var normalizedAes = aes.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? aes[2..] : aes;
            Add(normalizedAes.Length == 64 && normalizedAes.All(Uri.IsHexDigit) ? "OK" : "WARN", "AES Key 应为 64 位十六进制，可带 0x 前缀。");

            var output = !string.IsNullOrWhiteSpace(GameUnpackOutputBox.Text)
                ? GameUnpackOutputBox.Text.Trim()
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RocoModStudio", "unpacked");
            CheckWritableDirectory(output, "解包输出目录", Add);

            var projectRoot = ProjectRootBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(projectRoot)) CheckWritableDirectory(projectRoot, "项目目录", Add);
            else Add("WARN", "尚未设置项目根目录。");

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(output))!);
                Add(drive.AvailableFreeSpace > 5L * 1024 * 1024 * 1024 ? "OK" : "WARN", $"目标磁盘剩余 {drive.AvailableFreeSpace / 1024d / 1024d / 1024d:F1} GB。大型解包建议至少 5 GB。");
            }
            catch (Exception ex) { Add("WARN", $"无法读取磁盘空间: {ex.Message}"); }

            await ProbeToolAsync("CUE4Parse CLI", _toolLocator.FindCue4ParseCli(_settings), ["--help"], report, Add);
            await ProbeToolAsync("repak", _toolLocator.FindRepak(_settings), ["--help"], report, Add);
            await ProbeToolAsync("Node", _toolLocator.FindNode(_settings), ["--version"], report, Add);

            var diagnosticsRoot = _project is not null ? Path.Combine(_project.Root, "reports") : output;
            Directory.CreateDirectory(diagnosticsRoot);
            var reportPath = Path.Combine(diagnosticsRoot, $"diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var payload = new
            {
                generatedAt = DateTimeOffset.Now,
                errors,
                warnings,
                report
            };
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var line in report) AppendLog(line, line.StartsWith("[ERROR]") ? System.Windows.Media.Brushes.OrangeRed : line.StartsWith("[WARN]") ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.LightGreen);
            AppendLog($"排查报告：{reportPath}", System.Windows.Media.Brushes.LightGreen);

            var summary = new StringBuilder();
            summary.AppendLine($"错误：{errors}    警告：{warnings}");
            summary.AppendLine();
            foreach (var line in report.Take(18)) summary.AppendLine(line);
            if (report.Count > 18) summary.AppendLine($"... 共 {report.Count} 项");
            MessageBox.Show(this, summary.ToString(), "一键排查结果", MessageBoxButton.OK, errors > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        });
    }

    private static void CheckWritableDirectory(string path, string label, Action<string, string> add)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".roco-write-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            add("OK", $"{label}可写: {path}");
        }
        catch (Exception ex)
        {
            add("ERROR", $"{label}不可写: {ex.Message}");
        }
    }

    private async Task ProbeToolAsync(string label, string? executable, string[] args, List<string> report, Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            add("WARN", $"{label}: 未配置");
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var result = await _processRunner.RunAsync(executable, args, Path.GetDirectoryName(executable), timeout.Token);
            add(result.ExitCode == 0 ? "OK" : "WARN", $"{label}: 进程返回 {result.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            add("WARN", $"{label}: 8 秒内未响应");
        }
        catch (Exception ex)
        {
            add("ERROR", $"{label}: {ex.Message}");
        }
    }
}
