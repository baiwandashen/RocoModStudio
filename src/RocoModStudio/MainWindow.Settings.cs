namespace RocoModStudio;

public partial class MainWindow
{
    private void OpenToolsFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Directory.CreateDirectory(_toolLocator.ToolsRoot);
        OpenPath(_toolLocator.ToolsRoot);
    }
}
