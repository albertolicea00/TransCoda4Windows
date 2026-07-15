using System.Diagnostics;

namespace TransCoda.Core;

/// <summary>
/// Runs short-lived commands to completion and captures their output.
/// Suitable for probes and capability checks with modest output sizes.
/// </summary>
public static class ProcessRunner
{
    public sealed record Output(int ExitCode, string StandardOutput, string StandardError);

    public static async Task<Output> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new Output(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
