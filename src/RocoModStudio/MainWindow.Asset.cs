using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RocoModStudio;

public partial class MainWindow
{
    private List<string> _assetBrowserPaths = new();
    private async void AssetInspect_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("检查 UAsset", async () =>
        {
            var assetPath = RequireFile(AssetPathBox.Text, "uasset 文件");
            var inspection = await Task.Run(() => _assetEngine.Inspect(assetPath, GetEngineVersion(AssetEngineCombo), EmptyToNull(MappingsPathBox.Text)));
            AssetSummaryText.Text = $"Exports: {inspection.Exports} · Imports: {inspection.Imports} · Names: {inspection.Names} · 类型: {string.Join(", ", inspection.Classes)}";
            AppendLog(AssetSummaryText.Text);
        });
    }

    private async void AssetExport_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("导出资源 JSON", async () =>
        {
            var assetPath = RequireFile(AssetPathBox.Text, "uasset 文件");
            var jsonPath = string.IsNullOrWhiteSpace(AssetJsonPathBox.Text)
                ? Path.ChangeExtension(assetPath, ".json")
                : AssetJsonPathBox.Text.Trim();
            await Task.Run(() => _assetEngine.ExportJson(assetPath, jsonPath, GetEngineVersion(AssetEngineCombo), EmptyToNull(MappingsPathBox.Text)));
            AssetJsonPathBox.Text = jsonPath;
            AssetJsonEditor.Text = await File.ReadAllTextAsync(jsonPath);
            AppendLog($"JSON 已导出：{jsonPath}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void AssetImport_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("写回 UAsset", async () =>
        {
            var sourceAsset = RequireFile(AssetPathBox.Text, "uasset 文件");
            if (string.IsNullOrWhiteSpace(AssetJsonEditor.Text)) throw new InvalidOperationException("JSON 编辑器为空。请先导出资源。");
            JToken.Parse(AssetJsonEditor.Text);

            var outputPath = BuildAssetOutputPath(sourceAsset);
            var temporaryJson = Path.Combine(Path.GetTempPath(), $"roco-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(temporaryJson, AssetJsonEditor.Text);
            try
            {
                await Task.Run(() => _assetEngine.ImportJson(temporaryJson, outputPath, EmptyToNull(MappingsPathBox.Text)));
            }
            finally
            {
                try { File.Delete(temporaryJson); } catch { }
            }

            CopyAssetSidecar(sourceAsset, outputPath, ".uexp");
            CopyAssetSidecar(sourceAsset, outputPath, ".ubulk");
            AppendLog($"修改后的资源已写入：{outputPath}", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private async void AssetRoundTrip_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("UAsset JSON 往返校验", async () =>
        {
            var assetPath = RequireFile(AssetPathBox.Text, "uasset 文件");
            var engine = GetEngineVersion(AssetEngineCombo);
            var mappings = EmptyToNull(MappingsPathBox.Text);
            var tempRoot = Path.Combine(Path.GetTempPath(), $"roco-roundtrip-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempRoot);
            var firstJson = Path.Combine(tempRoot, "first.json");
            var roundTripAsset = Path.Combine(tempRoot, Path.GetFileName(assetPath));
            var secondJson = Path.Combine(tempRoot, "second.json");

            try
            {
                await Task.Run(() =>
                {
                    _assetEngine.ExportJson(assetPath, firstJson, engine, mappings);
                    _assetEngine.ImportJson(firstJson, roundTripAsset, mappings);
                    _assetEngine.ExportJson(roundTripAsset, secondJson, engine, mappings);
                });

                var first = JToken.Parse(await File.ReadAllTextAsync(firstJson));
                var second = JToken.Parse(await File.ReadAllTextAsync(secondJson));
                if (!JToken.DeepEquals(first, second)) throw new InvalidDataException("二轮 JSON 不一致，不能视为稳定往返。");
                AssetJsonEditor.Text = await File.ReadAllTextAsync(firstJson);
                AssetJsonPathBox.Text = firstJson;
                AppendLog("资源通过 JSON → uasset → JSON 二轮一致性检查。", System.Windows.Media.Brushes.LightGreen);
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        });
    }

    private void JsonFormat_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AssetJsonEditor.Text = JToken.Parse(AssetJsonEditor.Text).ToString(Formatting.Indented);
            AppendLog("JSON 已格式化。");
        }
        catch (Exception ex)
        {
            ShowError("JSON 格式错误", ex);
        }
    }

    private void JsonFind_Click(object sender, RoutedEventArgs e)
    {
        var query = ShowInputDialog("查找 JSON", "输入要查找的名称或值：");
        if (string.IsNullOrWhiteSpace(query)) return;
        var index = AssetJsonEditor.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            AppendLog($"未找到：{query}", System.Windows.Media.Brushes.Orange);
            return;
        }
        AssetJsonEditor.Focus();
        AssetJsonEditor.Select(index, query.Length);
        AssetJsonEditor.ScrollToLine(AssetJsonEditor.GetLineIndexFromCharacterIndex(index));
    }

    private string BuildAssetOutputPath(string sourceAsset)
    {
        if (_project is not null)
        {
            var outputDirectory = Path.Combine(_project.WorkingDirectory, "asset-edits");
            Directory.CreateDirectory(outputDirectory);
            return Path.Combine(outputDirectory, Path.GetFileName(sourceAsset));
        }

        return Path.Combine(Path.GetDirectoryName(sourceAsset)!, Path.GetFileNameWithoutExtension(sourceAsset) + ".modded.uasset");
    }

    private static void CopyAssetSidecar(string sourceAsset, string outputAsset, string extension)
    {
        var source = Path.ChangeExtension(sourceAsset, extension);
        if (!File.Exists(source)) return;
        File.Copy(source, Path.ChangeExtension(outputAsset, extension), overwrite: true);
    }

    private string? ShowInputDialog(string title, string prompt)
    {
        var window = new Window
        {
            Title = title,
            Owner = this,
            Width = 440,
            Height = 165,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (System.Windows.Media.Brush)FindResource("AppBackgroundBrush")
        };
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
        var label = new System.Windows.Controls.TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 7) };
        var box = new System.Windows.Controls.TextBox();
        var ok = new System.Windows.Controls.Button { Content = "确定", Width = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Style = (Style)FindResource("PrimaryButtonStyle") };
        System.Windows.Controls.Grid.SetRow(box, 1);
        System.Windows.Controls.Grid.SetRow(ok, 2);
        ok.Click += (_, _) => { window.DialogResult = true; };
        grid.Children.Add(label);
        grid.Children.Add(box);
        grid.Children.Add(ok);
        window.Content = grid;
        window.Loaded += (_, _) => box.Focus();
        return window.ShowDialog() == true ? box.Text : null;
    }

    private async void AssetScanFolder_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("扫描资源浏览器", async () =>
        {
            var root = RequireDirectory(GameUnpackOutputBox.Text, "解包输出目录");
            _assetBrowserPaths = await Task.Run(() => Directory.EnumerateFiles(root, "*.uasset", SearchOption.AllDirectories).OrderBy(path => path).ToList());
            ApplyAssetBrowserFilter();
            AppendLog($"资源浏览器已载入 {_assetBrowserPaths.Count:N0} 个 UAsset。", System.Windows.Media.Brushes.LightGreen);
        });
    }

    private void AssetSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyAssetBrowserFilter();
    private void AssetTypeFilterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApplyAssetBrowserFilter();

    private void AssetBrowserList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => SelectAssetFromBrowser();

    private void AssetBrowserOpen_Click(object sender, RoutedEventArgs e) => SelectAssetFromBrowser();

    private void SelectAssetFromBrowser()
    {
        if (AssetBrowserList.SelectedItem is not string path) return;
        AssetPathBox.Text = path;
        AssetJsonPathBox.Text = Path.ChangeExtension(path, ".json");
        AssetSummaryText.Text = $"已选择：{path}";
        AppendLog($"资源浏览器选择：{path}");
    }

    private void ApplyAssetBrowserFilter()
    {
        if (AssetBrowserList is null) return;
        var search = AssetSearchBox?.Text?.Trim() ?? string.Empty;
        var typeIndex = AssetTypeFilterCombo?.SelectedIndex ?? 0;
        var filtered = _assetBrowserPaths.Where(path =>
            (string.IsNullOrWhiteSpace(search) || path.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            AssetMatchesType(path, typeIndex)).Take(2000).ToList();
        AssetBrowserList.ItemsSource = filtered;
        if (filtered.Count > 0) AssetBrowserList.SelectedIndex = 0;
    }

    private static bool AssetMatchesType(string path, int typeIndex)
    {
        var name = Path.GetFileName(path);
        return typeIndex switch
        {
            1 => name.StartsWith("BP_", StringComparison.OrdinalIgnoreCase),
            2 => name.StartsWith("SKM", StringComparison.OrdinalIgnoreCase),
            3 => name.StartsWith("SM_", StringComparison.OrdinalIgnoreCase),
            4 => path.Contains("AnimSequence", StringComparison.OrdinalIgnoreCase),
            5 => name.StartsWith("T_", StringComparison.OrdinalIgnoreCase) || path.Contains("Texture", StringComparison.OrdinalIgnoreCase),
            6 => path.Contains("Material", StringComparison.OrdinalIgnoreCase),
            7 => path.Contains("Effects", StringComparison.OrdinalIgnoreCase) || path.Contains("Particle", StringComparison.OrdinalIgnoreCase) || path.Contains("Niagara", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }}

