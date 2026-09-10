using System.Text;
using System.Windows;
using System.Windows.Threading;
using RocoModStudio.Services;

namespace RocoModStudio;

public partial class App : Application
{
    private static readonly string CrashDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RocoModStudio",
        "Logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteCrash("AppDomain", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrash("Task", args.Exception);
            args.SetObserved();
        };

        if (e.Args.Length > 0)
        {
            var exitCode = CliRunner.Run(e.Args, Console.Out, Console.Error);
            Shutdown(exitCode);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var log = WriteCrash("UI", e.Exception);
        MessageBox.Show(
            $"界面发生异常，但程序已阻止直接退出。\n\n{e.Exception.Message}\n\n诊断日志：\n{log}",
            "Roco Mod Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string WriteCrash(string source, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(CrashDirectory);
            var path = Path.Combine(CrashDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            var builder = new StringBuilder();
            builder.AppendLine($"Source: {source}");
            builder.AppendLine($"Time: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Version: {typeof(App).Assembly.GetName().Version}");
            builder.AppendLine(new string('-', 72));
            builder.AppendLine(exception?.ToString() ?? "Unknown exception");
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return "(无法写入崩溃日志)";
        }
    }
}
