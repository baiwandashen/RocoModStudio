using System.Diagnostics;
using System.Windows;
using RocoModStudio.Models;
using RocoModStudio.Services;

namespace RocoModStudio;

public partial class MainWindow
{
    private readonly AssetScanService _assetScanner = new();
    private UnpackedAssetSummary? _lastScan;

    private async void CueList_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("FModel/CUE4Parse 挂载并列出文件", async () =>
        {
            var paks = RequireDirectory(GamePaksBox.Text, "游戏 Paks 目录");
            EnsureUnpackOutput(paks);
            var limit = int.TryParse(GameListLimitBox.Text, out var parsed) && parsed > 0 ? parsed : 300;
            var arguments = BuildCueArguments(paks);
            arguments.Add("--list");
            arguments.Add("--limit");
            arguments.Add(limit.ToString());
            var result = await RunCueAsync(arguments);
            GameEntriesList.Items.Clear();
            foreach (var line in result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Take(limit))
                GameEntriesList.Items.Add(line);
            AppendLog($"已列出 {GameEntriesList.Items.Count} 个匹配条目。", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void CueUnpack_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("FModel/CUE4Parse 解包", async () =>
        {
            var paks = RequireDirectory(GamePaksBox.Text, "游戏 Paks 目录");
            var output = EnsureUnpackOutput(paks);
            Directory.CreateDirectory(output);
            var arguments = BuildCueArguments(paks);
            arguments.Add("--unpack");
            arguments.Add(output);
            await RunCueAsync(arguments);
            GameUnpackOutputBox.Text = output;
            _settings.LastUnpackedPath = output;
            _settingsService.Save(_settings);
            AppendLog($"解包完成：{output}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void CueObject_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("提取对象 JSON", async () =>
        {
            var paks = RequireDirectory(GamePaksBox.Text, "游戏 Paks 目录");
            if (string.IsNullOrWhiteSpace(GameObjectPathBox.Text)) throw new InvalidOperationException("请输入对象路径。");
            var output = EnsureUnpackOutput(paks);
            var arguments = BuildCueArguments(paks);
            arguments.Add("--object");
            arguments.Add(GameObjectPathBox.Text.Trim());
            var result = await RunCueAsync(arguments);
            var report = Path.Combine(output, "object-extract.json");
            await File.WriteAllTextAsync(report, result.StandardOutput);
            AppendLog($"对象 JSON 已保存：{report}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private void OpenFModel_Click(object sender, RoutedEventArgs e)
    {
        var fmodel = _toolLocator.FindFModel(_settings) ?? throw new InvalidOperationException("未找到 FModel.exe。");
        Process.Start(new ProcessStartInfo { FileName = fmodel, WorkingDirectory = Path.GetDirectoryName(fmodel)!, UseShellExecute = true });
        AppendLog($"已启动 FModel：{fmodel}", System.Windows.Media.Brushes.LightGreen);
    }

    private async void ScanUnpacked_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("扫描解包资源", async () =>
        {
            var root = RequireDirectory(GameUnpackOutputBox.Text, "解包输出目录");
            var scan = await Task.Run(() => _assetScanner.Scan(root));
            _lastScan = scan;
            GameScanSummaryText.Text = $"总文件 {scan.TotalFiles:N0} · UAsset {scan.UAssetCount:N0} · 贴图 {scan.TextureCount:N0} · 骨骼网格 {scan.SkeletalMeshCount:N0} · 动画 {scan.AnimationCount:N0} · 特效 {scan.EffectCount:N0} · BP {scan.BlueprintCount:N0} · 语音 {scan.VoiceBankCount:N0}\n识别宠物 {scan.PetNames.Count}: {string.Join(", ", scan.PetNames.Take(30))}";
            GameEntriesList.Items.Clear();
            foreach (var sample in scan.Samples) GameEntriesList.Items.Add(Path.GetRelativePath(root, sample));
            GamePetTargetCombo.ItemsSource = scan.PetNames;
            GamePetDonorCombo.ItemsSource = scan.PetNames;
            if (scan.PetNames.Count > 0) GamePetTargetCombo.SelectedIndex = 0;
            if (scan.PetNames.Count > 1) GamePetDonorCombo.SelectedIndex = 1;
            AppendLog($"扫描完成：{scan.TotalFiles:N0} 个文件。", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void ImportFilteredAssets_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("导入识别到的资源到 Mod 项目", async () =>
        {
            if (_project is null) throw new InvalidOperationException("请先在“开始与项目”创建 Mod 项目。");
            var root = RequireDirectory(GameUnpackOutputBox.Text, "解包输出目录");
            var destination = Path.Combine(_project.WorkingDirectory, "source");
            var count = await Task.Run(() => _assetScanner.CopyAssets(root, destination, GameFilterBox.Text));
            AppendLog($"已复制 {count:N0} 个资源文件到：{destination}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private void FillPetWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (_lastScan is null) throw new InvalidOperationException("请先扫描资源。");
        var target = GamePetTargetCombo.SelectedItem as string;
        var donor = GamePetDonorCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(donor)) throw new InvalidOperationException("请选择目标宠物与供体宠物。");
        PetTargetBpBox.Text = FindPetDirectory(_lastScan.Root, "BP", "Pets", target) ?? string.Empty;
        PetDonorBpBox.Text = FindPetDirectory(_lastScan.Root, "BP", "Pets", donor) ?? string.Empty;
        PetTargetAnimBox.Text = FindPetDirectory(_lastScan.Root, "AnimSequence", "Pets", target) ?? string.Empty;
        PetDonorAnimBox.Text = FindPetDirectory(_lastScan.Root, "AnimSequence", "Pets", donor) ?? string.Empty;
        PetTargetBankBox.Text = FindBank(_lastScan.Root, target) ?? string.Empty;
        PetDonorBankBox.Text = FindBank(_lastScan.Root, donor) ?? string.Empty;
        if (_project is not null) PetOutputBox.Text = Path.Combine(_project.WorkingDirectory, "pet-swap", $"{target}-from-{donor}");
        Navigate("Pet");
        AppendLog("已根据自动识别结果填充宠物替换工作流。", System.Windows.Media.Brushes.LightGreen);
    }

    private List<string> BuildCueArguments(string paks)
    {
        var arguments = new List<string> { paks };
        if (!string.IsNullOrWhiteSpace(GameAesKeyBox.Text)) { arguments.Add("--aes"); arguments.Add(GameAesKeyBox.Text.Trim()); }
        if (!string.IsNullOrWhiteSpace(GameOodleBox.Text)) { arguments.Add("--oodle"); arguments.Add(GameOodleBox.Text.Trim()); }
        if (!string.IsNullOrWhiteSpace(GameUsmapBox.Text)) { arguments.Add("--usmap"); arguments.Add(GameUsmapBox.Text.Trim()); }
        arguments.Add("--game");
        arguments.Add(GameNameCombo.SelectedIndex == 0 ? "GAME_RocoKingdomWorld" : "GAME_UE5_LATEST");
        return arguments;
    }

    private async Task<ProcessResult> RunCueAsync(List<string> arguments)
    {
        var cli = _toolLocator.FindCue4ParseCli(_settings) ?? throw new InvalidOperationException("未找到 cue4parse-cli.exe。");
        var result = await _processRunner.RunAsync(cli, arguments, Path.GetDirectoryName(cli), _operationCancellation?.Token ?? default);
        if (result.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? $"CUE4Parse CLI 返回代码 {result.ExitCode}" : result.StandardError.Trim());
        return result;
    }

    private string EnsureUnpackOutput(string paks)
    {
        if (!string.IsNullOrWhiteSpace(GameUnpackOutputBox.Text)) return Path.GetFullPath(GameUnpackOutputBox.Text.Trim());
        var output = _project is not null ? Path.Combine(_project.WorkingDirectory, "unpacked") : Path.Combine(paks, "Unpacked");
        GameUnpackOutputBox.Text = output;
        return output;
    }

    private static string? FindPetDirectory(string root, string category, string leaf, string pet)
    {
        foreach (var petsDir in Directory.EnumerateDirectories(root, leaf, SearchOption.AllDirectories))
        {
            if (!petsDir.Contains(category, StringComparison.OrdinalIgnoreCase)) continue;
            var candidate = Path.Combine(petsDir, pet);
            if (Directory.Exists(candidate)) return candidate;
            candidate = Directory.EnumerateDirectories(petsDir).FirstOrDefault(path => Path.GetFileName(path).Contains(pet, StringComparison.OrdinalIgnoreCase));
            if (candidate is not null) return candidate;
        }
        return null;
    }

    private static string? FindBank(string root, string pet)
    {
        return Directory.EnumerateFiles(root, "*.bnk", SearchOption.AllDirectories).FirstOrDefault(path => Path.GetFileName(path).Contains(pet, StringComparison.OrdinalIgnoreCase));
    }
    private async void BuildOverridePak_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("生成直接覆盖 PAK", async () =>
        {
            if (_project is null) throw new InvalidOperationException("请先创建 Mod 项目。");
            var root = RequireDirectory(GameUnpackOutputBox.Text, "解包输出目录");
            var count = await Task.Run(() => _assetScanner.CopyAssets(root, _project.PackRoot, GameFilterBox.Text));
            if (count == 0) throw new InvalidOperationException("过滤条件没有匹配到可覆盖的 cooked 资源。");
            var pak = Path.Combine(_project.OutputDirectory, $"{_project.Name}-override.pak");
            Directory.CreateDirectory(_project.OutputDirectory);
            await RunRepakAsync("pack", _project.PackRoot, pak, "--mount-point", "../../../", "--version", "V11");
            AppendLog($"覆盖包已生成：{pak}", System.Windows.Media.Brushes.LightGreen);
        });
    }
    private async void OneClickPipeline_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("一键解包、解析并建立 Mod 项目", async () =>
        {
            var paks = RequireDirectory(OneClickPaksBox.Text, "游戏 Paks 目录");
            if (string.IsNullOrWhiteSpace(OneClickAesBox.Text)) throw new InvalidOperationException("请输入 AES Key。");
            if (_project is null)
            {
                var projectRoot = string.IsNullOrWhiteSpace(ProjectRootBox.Text)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RocoModStudio")
                    : ProjectRootBox.Text.Trim();
                _project = _projectService.Create(projectRoot, ProjectNameBox.Text);
                _settings.LastProjectRoot = _project.Root;
                SidebarProjectText.Text = _project.Name;
                ProjectRootBox.Text = projectRoot;
            }

            GamePaksBox.Text = paks;
            GameAesKeyBox.Text = OneClickAesBox.Text.Trim();
            GameNameCombo.SelectedIndex = 0;
            GameUnpackOutputBox.Text = Path.Combine(_project.WorkingDirectory, "unpacked");
            var output = EnsureUnpackOutput(paks);
            Directory.CreateDirectory(output);

            var arguments = BuildCueArguments(paks);
            arguments.Add("--unpack");
            arguments.Add(output);
            AppendLog("步骤 1/3：挂载全部 PAK 并解包...");
            await RunCueAsync(arguments);

            AppendLog("步骤 2/3：扫描并识别 cooked 资源...");
            var scan = await Task.Run(() => _assetScanner.Scan(output));
            _lastScan = scan;
            GameScanSummaryText.Text = $"总文件 {scan.TotalFiles:N0} · UAsset {scan.UAssetCount:N0} · 贴图 {scan.TextureCount:N0} · 骨骼网格 {scan.SkeletalMeshCount:N0} · 动画 {scan.AnimationCount:N0} · 特效 {scan.EffectCount:N0} · BP {scan.BlueprintCount:N0} · 语音 {scan.VoiceBankCount:N0}\n识别宠物 {scan.PetNames.Count}";
            GamePetTargetCombo.ItemsSource = scan.PetNames;
            GamePetDonorCombo.ItemsSource = scan.PetNames;
            if (scan.PetNames.Count > 0) GamePetTargetCombo.SelectedIndex = 0;
            if (scan.PetNames.Count > 1) GamePetDonorCombo.SelectedIndex = 1;

            PackSourceBox.Text = _project.PackRoot;
            PackModFolderBox.Text = _project.PackRoot;
            PackOutputBox.Text = Path.Combine(_project.OutputDirectory, $"{_project.Name}.pak");
            PackModOutputBox.Text = PackOutputBox.Text;
            UePackRootBox.Text = _project.PackRoot;
            _settings.LastGamePaksPath = paks;
            _settings.LastUnpackedPath = output;
            _settingsService.Save(_settings);

            AppendLog("步骤 3/3：Mod 编辑工作台已就绪。", System.Windows.Media.Brushes.LightGreen);
            Navigate("FModel");
            MessageBox.Show(this, $"解包与资源识别完成。\n\n文件：{scan.TotalFiles:N0}\n宠物候选：{scan.PetNames.Count}\n输出：{output}", "Roco Mod Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}







