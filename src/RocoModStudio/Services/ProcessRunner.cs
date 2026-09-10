using System.Diagnostics;
using System.Text;
using RocoModStudio.Models;

namespace RocoModStudio.Services;

public sealed class ProcessRunner
{
    public event Action<string>? Output;

    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            standardOutput.AppendLine(args.Data);
            Output?.Invoke(args.Data);
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            standardError.AppendLine(args.Data);
            Output?.Invoke(args.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"无法启动进程: {executable}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may already have exited.
            }

            throw;
        }

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }
}

