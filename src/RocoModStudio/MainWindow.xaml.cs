using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using RocoModStudio.Models;
using RocoModStudio.Services;

namespace RocoModStudio;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly ToolLocator _toolLocator;
    private readonly ProcessRunner _processRunner = new();
    private readonly AssetEngineService _assetEngine = new();
    private readonly ProjectService _projectService = new();

    private StudioSettings _settings;
    private ModProject? _project;
    private CancellationTokenSource? _operationCancellation;
    private bool _toolRefreshRunning;
    private readonly Dictionary<string, (Grid Page, string Title, string Subtitle)> _pages;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _toolLocator = new ToolLocator();
        _pages = new Dictionary<string, (Grid, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = (HomePage, "开始与项目", "创建 Mod 项目，并按工作流完成提取、编辑、转换与发布"),
            ["Pak"] = (PakPage, "PAK 提取 / 打包", "解包现有内容，或把整理好的 pack-root 发布为可挂载 PAK"),
            ["Asset"] = (AssetPage, "蓝图与资源编辑", "检查 export/import/name map，并通过 JSON 安全修改 cooked 资源"),
            ["FModel"] = (FModelPage, "游戏包解包 / FModel", "用最新 FModel 与 CUE4Parse 核心挂载、解包并识别 Roco cooked 资源"),
            ["Blender"] = (BlenderPage, "Blender / Cook 模型", "编辑骨骼模型，并通过 UE4.26 重新导入、Cook 和回写 pack-root"),
            ["Texture"] = (TexturePage, "贴图工作台", "从 UE4.26 纹理资产导出、注入和转换贴图"),
            ["Nrc"] = (NrcPage, "NRC 魔改转换", "按上游已确认的规则转换资源，未实现类型只报告不破坏"),
            ["Pet"] = (PetPage, "宠物外观替换", "将供体宠物模型、动画、碰撞体与语音移植到目标宠物"),
            ["Pack"] = (PackPage, "Mod 发布打包", "验证目录结构并生成发布用 PAK"),
            ["Settings"] = (SettingsPage, "工具与设置", "配置便携工具路径并检查工具链可用性")
        };

        _processRunner.Output += message => Dispatcher.Invoke(() => AppendLog(message));
        NavList.SelectedIndex = 0;
        LoadSettingsIntoUi();
        Loaded += async (_, _) => await RefreshToolStatusAsync();
        Closed += (_, _) => _operationCancellation?.Cancel();
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListBoxItem item || item.Tag is not string tag || !_pages.TryGetValue(tag, out var page))
        {
            return;
        }

        foreach (var entry in _pages.Values)
        {
            entry.Page.Visibility = ReferenceEquals(entry.Page, page.Page) ? Visibility.Visible : Visibility.Collapsed;
        }

        PageTitleText.Text = page.Title;
        PageSubtitleText.Text = page.Subtitle;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string targetName) return;
            if (FindName(targetName) is not TextBox box) return;

            var dialog = new OpenFolderDialog { Multiselect = false, Title = "选择目录" };
            if (!string.IsNullOrWhiteSpace(box.Text) && Directory.Exists(box.Text)) dialog.InitialDirectory = box.Text;
            if (dialog.ShowDialog(this) == true)
            {
                box.Text = dialog.FolderName;
                box.CaretIndex = (box.Text ?? string.Empty).Length;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"打开目录选择器失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
            ShowError("目录选择器错误", ex);
        }
    }

    private void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string spec) return;
            var parts = spec.Split('|', 3);
            if (parts.Length < 2 || FindName(parts[0]) is not TextBox box) return;

            var description = parts[1];
            var pattern = parts.Length == 3 ? parts[2] : "*.*";
            var dialog = new OpenFileDialog
            {
                Filter = BuildDialogFilter(description, pattern),
                CheckFileExists = true,
                Multiselect = false,
                ValidateNames = false
            };
            SetDialogInitialDirectory(dialog, box.Text);
            if (dialog.ShowDialog(this) == true)
            {
                box.Text = dialog.FileName;
                box.CaretIndex = (box.Text ?? string.Empty).Length;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"打开文件选择器失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
            ShowError("文件选择器错误", ex);
        }
    }

    private void BrowseSaveFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string spec) return;
            var parts = spec.Split('|', 3);
            if (parts.Length < 2 || FindName(parts[0]) is not TextBox box) return;

            var description = parts[1];
            var pattern = parts.Length == 3 ? parts[2] : "*.*";
            var dialog = new SaveFileDialog
            {
                Filter = BuildDialogFilter(description, pattern),
                AddExtension = true,
                OverwritePrompt = true,
                ValidateNames = false,
                FileName = string.IsNullOrWhiteSpace(box.Text) ? null : Path.GetFileName(box.Text)
            };
            SetDialogInitialDirectory(dialog, box.Text);
            if (dialog.ShowDialog(this) == true)
            {
                box.Text = dialog.FileName;
                box.CaretIndex = (box.Text ?? string.Empty).Length;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"打开保存对话框失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
            ShowError("保存对话框错误", ex);
        }
    }

    private void OpenFolderFromBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string targetName) return;
        if (FindName(targetName) is not TextBox box || string.IsNullOrWhiteSpace(box.Text)) return;

        var path = box.Text;
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) OpenPath(directory);
    }

    private void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = ProjectRootBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RocoModStudio");
            }

            _project = _projectService.Create(root, ProjectNameBox.Text);
            _settings.LastProjectRoot = _project.Root;
            _settingsService.Save(_settings);
            SidebarProjectText.Text = _project.Name;
            ProjectRootBox.Text = root;
            PackSourceBox.Text = _project.PackRoot;
            PackModFolderBox.Text = _project.PackRoot;
            PackOutputBox.Text = Path.Combine(_project.OutputDirectory, $"{_project.Name}.pak");
            PackModOutputBox.Text = PackOutputBox.Text;
            AppendLog($"已创建项目：{_project.Root}", Brushes.LightGreen);
            Navigate("Pak");
        }
        catch (Exception ex)
        {
            ShowError("创建项目失败", ex);
        }
    }

    private void QuickPak_Click(object sender, RoutedEventArgs e) => Navigate("Pak");
    private void QuickPet_Click(object sender, RoutedEventArgs e) => Navigate("Pet");
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => Navigate("Settings");

    private void Navigate(string page)
    {
        foreach (ListBoxItem item in NavList.Items)
        {
            if (string.Equals(item.Tag as string, page, StringComparison.OrdinalIgnoreCase))
            {
                NavList.SelectedItem = item;
                return;
            }
        }
    }

    private void LoadSettingsIntoUi()
    {
        SetRepakBox.Text = _settings.RepakPath ?? string.Empty;
        SetDdsScriptBox.Text = _settings.Ue4DdsScriptPath ?? string.Empty;
        SetDdsPythonBox.Text = _settings.Ue4DdsPythonPath ?? string.Empty;
        SetNodeBox.Text = _settings.NodePath ?? string.Empty;
        SetPetScriptBox.Text = _settings.PetSwapScriptPath ?? string.Empty;
        SetFModelBox.Text = _settings.FModelPath ?? string.Empty;
        SetCue4ParseBox.Text = _settings.Cue4ParseCliPath ?? string.Empty;
        SetBlenderBox.Text = _settings.BlenderPath ?? string.Empty;
        SetUnrealEditorBox.Text = _settings.UnrealEditorCmdPath ?? string.Empty;
        SetRunUatBox.Text = _settings.UnrealRunUatPath ?? string.Empty;
        GamePaksBox.Text = _settings.LastGamePaksPath ?? string.Empty;
        GameUnpackOutputBox.Text = _settings.LastUnpackedPath ?? string.Empty;
        ModelSourceBox.Text = _settings.LastBlenderModelPath ?? string.Empty;
        UeProjectBox.Text = _settings.UnrealProjectPath ?? string.Empty;
        PakFileBox.Text = _settings.LastPakPath ?? string.Empty;
        AssetPathBox.Text = _settings.LastAssetPath ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_settings.LastProjectRoot) && Directory.Exists(_settings.LastProjectRoot))
        {
            _project = _projectService.Load(_settings.LastProjectRoot);
            if (_project is null)
            {
                var directory = new DirectoryInfo(_settings.LastProjectRoot);
                _project = new ModProject(directory.Name, directory.FullName, directory.CreationTime);
            }

            SidebarProjectText.Text = _project.Name;
            PackSourceBox.Text = _project.PackRoot;
            PackModFolderBox.Text = _project.PackRoot;
            PackOutputBox.Text = Path.Combine(_project.OutputDirectory, $"{_project.Name}.pak");
            PackModOutputBox.Text = PackOutputBox.Text;
        }
        else
        {
            ProjectRootBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RocoModStudio");
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        CaptureSettings();
        _settingsService.Save(_settings);
        await RefreshToolStatusAsync();
        AppendLog("工具设置已保存。", Brushes.LightGreen);
    }

    private async void DetectTools_Click(object sender, RoutedEventArgs e) => await RefreshToolStatusAsync();

    private void CaptureSettings()
    {
        _settings.RepakPath = EmptyToNull(SetRepakBox.Text);
        _settings.Ue4DdsScriptPath = EmptyToNull(SetDdsScriptBox.Text);
        _settings.Ue4DdsPythonPath = EmptyToNull(SetDdsPythonBox.Text);
        _settings.NodePath = EmptyToNull(SetNodeBox.Text);
        _settings.PetSwapScriptPath = EmptyToNull(SetPetScriptBox.Text);
        _settings.FModelPath = EmptyToNull(SetFModelBox.Text);
        _settings.Cue4ParseCliPath = EmptyToNull(SetCue4ParseBox.Text);
        _settings.BlenderPath = EmptyToNull(SetBlenderBox.Text);
        _settings.UnrealEditorCmdPath = EmptyToNull(SetUnrealEditorBox.Text);
        _settings.UnrealRunUatPath = EmptyToNull(SetRunUatBox.Text);
        _settings.UnrealProjectPath = EmptyToNull(UeProjectBox.Text);
        _settings.LastGamePaksPath = EmptyToNull(GamePaksBox.Text);
        _settings.LastUnpackedPath = EmptyToNull(GameUnpackOutputBox.Text);
        _settings.LastBlenderModelPath = EmptyToNull(ModelSourceBox.Text);
        _settings.LastPakPath = EmptyToNull(PakFileBox.Text);
        _settings.LastAssetPath = EmptyToNull(AssetPathBox.Text);
    }

    private async Task RefreshToolStatusAsync()
    {
        if (_toolRefreshRunning) return;
        _toolRefreshRunning = true;
        try
        {
            CaptureSettings();
            BusyText.Text = "检测工具...";
            var statuses = await Task.Run(() => _toolLocator.Detect(_settings));
            HomeToolStatusStack.Children.Clear();
            SettingsToolStatusStack.Children.Clear();
            foreach (var status in statuses)
            {
                HomeToolStatusStack.Children.Add(BuildToolStatus(status, compact: true));
                SettingsToolStatusStack.Children.Add(BuildToolStatus(status, compact: false));
            }

            var available = statuses.Count(status => status.Available);
            HomeHealthBar.Maximum = statuses.Count;
            HomeHealthBar.Value = available;
            HomeHealthSummary.Text = $"{available}/{statuses.Count} 项工具链就绪";
            HomeHealthSummary.Text += available < statuses.Count
                ? "\n缺少项可在“工具与设置”中手动指定。"
                : "\n可以开始完整工作流。";
            BusyText.Text = "就绪";
        }
        catch (Exception ex)
        {
            AppendLog($"工具检测失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
        }
        finally
        {
            _toolRefreshRunning = false;
        }
    }
    private UIElement BuildToolStatus(ToolStatus status, bool compact)
    {
        var color = status.Available ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("WarningBrush");
        var panel = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        panel.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = color, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) });
        var textPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        textPanel.Children.Add(new TextBlock { Text = status.Name, FontWeight = FontWeights.SemiBold, FontSize = 12 });
        if (!compact) textPanel.Children.Add(new TextBlock { Text = status.Purpose, Foreground = (Brush)FindResource("TextSecondaryBrush"), FontSize = 11 });
        textPanel.Children.Add(new TextBlock { Text = status.Detail, Foreground = (Brush)FindResource("TextSecondaryBrush"), FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(textPanel, 1);
        panel.Children.Add(textPanel);

        var badge = new Border { Style = (Style)FindResource("PillStyle"), Background = status.Available ? (Brush)FindResource("AccentSoftBrush") : new SolidColorBrush(Color.FromRgb(255, 244, 222)) };
        badge.Child = new TextBlock { Text = status.Badge, Foreground = color, FontSize = 10, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(badge, 2);
        panel.Children.Add(badge);
        return panel;
    }

    private async Task RunOperationAsync(string label, Func<Task> action)
    {
        if (_operationCancellation is not null) return;
        _operationCancellation = new CancellationTokenSource();
        SetBusy(true, label);
        AppendLog($"▶ {label}", (Brush)FindResource("AccentBrush"));
        try
        {
            await action();
            AppendLog($"✓ {label} 完成", (Brush)FindResource("SuccessBrush"));
        }
        catch (OperationCanceledException)
        {
            AppendLog("操作已取消。", (Brush)FindResource("WarningBrush"));
        }
        catch (Exception ex)
        {
            AppendLog($"✗ {label} 失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
            ShowError(label, ex);
        }
        finally
        {
            SetBusy(false, "就绪");
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    private void SetBusy(bool busy, string text)
    {
        BusyProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CancelOperationButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        BusyDot.Fill = busy ? (Brush)FindResource("WarningBrush") : (Brush)FindResource("SuccessBrush");
        BusyText.Text = text;
        LogStatusText.Text = text;
    }

    private void AppendLog(string message, Brush? color = null)
    {
        var paragraph = new Paragraph(new Run($"[{DateTime.Now:HH:mm:ss}] {message}"))
        {
            Foreground = color ?? new SolidColorBrush(Color.FromRgb(205, 224, 225)),
            Margin = new Thickness(0, 0, 0, 2),
            FontFamily = new FontFamily("Cascadia Mono, Consolas")
        };
        LogBox.Document.Blocks.Add(paragraph);
        while (LogBox.Document.Blocks.Count > 500) LogBox.Document.Blocks.Remove(LogBox.Document.Blocks.FirstBlock);
        LogBox.ScrollToEnd();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Document.Blocks.Clear();

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var range = new TextRange(LogBox.Document.ContentStart, LogBox.Document.ContentEnd);
        Clipboard.SetText(range.Text);
    }

    private void AssetPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AssetJsonPathBox is null || string.IsNullOrWhiteSpace(AssetJsonPathBox.Text) || !File.Exists(AssetPathBox.Text)) return;
        if (File.Exists(AssetPathBox.Text))
        {
            AssetJsonPathBox.Text = Path.ChangeExtension(AssetPathBox.Text, ".json");
        }
    }

    private string GetEngineVersion(ComboBox combo) => combo.SelectedIndex switch
    {
        1 => "VER_UE4_27",
        2 => "VER_UE5_0",
        3 => "VER_UE5_1",
        _ => "VER_UE4_26"
    };

    private string GetPakVersion(ComboBox combo) => combo.SelectedItem is ComboBoxItem item && item.Tag is string tag
        ? tag
        : combo.SelectedIndex switch { 1 => "V8B", 2 => "V5", _ => "V11" };

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"打开路径失败：{ex.Message}", (Brush)FindResource("DangerBrush"));
            ShowError("打开路径失败", ex);
        }
    }

    private static string BuildDialogFilter(string description, string pattern)
    {
        var safeDescription = string.IsNullOrWhiteSpace(description) ? "文件" : description.Trim();
        var safePattern = string.IsNullOrWhiteSpace(pattern) ? "*.*" : pattern.Trim();
        if (safePattern.Contains('|')) return safePattern;
        return $"{safeDescription} ({safePattern})|{safePattern}|所有文件 (*.*)|*.*";
    }

    private static void SetDialogInitialDirectory(FileDialog dialog, string? current)
    {
        if (string.IsNullOrWhiteSpace(current)) return;
        try
        {
            var directory = File.Exists(current) ? Path.GetDirectoryName(current) : current;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                dialog.InitialDirectory = directory;
        }
        catch
        {
            // Ignore malformed or inaccessible initial paths.
        }
    }
    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(this, exception.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
    private void CancelOperation_Click(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel();
        AppendLog("正在取消当前操作...", (Brush)FindResource("WarningBrush"));
    }
}












