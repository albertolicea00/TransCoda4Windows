using System.Diagnostics;
using System.Globalization;

namespace TransCoda.Core;

public sealed class TranscodeException : Exception
{
    public TranscodeException(int exitCode, string log)
        : base(string.IsNullOrWhiteSpace(log) ? $"FFmpeg exited with code {exitCode}." : log.Trim())
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

/// <summary>
/// Runs a single FFmpeg conversion and reports progress parsed from FFmpeg's
/// machine-readable -progress stream on stdout.
/// </summary>
public sealed class TranscodeEngine
{
    private readonly object _gate = new();
    private Process? _process;
    private bool _cancelled;

    public void Cancel()
    {
        lock (_gate)
        {
            _cancelled = true;
            try
            {
                if (_process is { HasExited: false })
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the check and the kill.
            }
        }
    }

    public async Task RunAsync(
        string ffmpegPath,
        IReadOnlyList<string> arguments,
        double? durationSeconds,
        IProgress<double> progress)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        lock (_gate)
        {
            if (_cancelled)
            {
                throw new OperationCanceledException();
            }
            _process = process;
        }

        process.Start();

        // Drain stderr concurrently so a chatty FFmpeg cannot fill the pipe
        // buffer and stall; only the tail is kept for error reporting.
        var stderrTask = ReadTailAsync(process.StandardError);

        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            var seconds = ParseOutTimeSeconds(line);
            if (seconds is not null && durationSeconds is > 0)
            {
                progress.Report(Math.Min(seconds.Value / durationSeconds.Value, 0.999));
            }
        }

        await process.WaitForExitAsync();
        var log = await stderrTask;

        bool wasCancelled;
        lock (_gate)
        {
            wasCancelled = _cancelled;
            _process = null;
        }

        if (wasCancelled)
        {
            throw new OperationCanceledException();
        }
        if (process.ExitCode != 0)
        {
            throw new TranscodeException(process.ExitCode, log);
        }
        progress.Report(1);
    }

    private static async Task<string> ReadTailAsync(StreamReader reader)
    {
        var text = await reader.ReadToEndAsync();
        return text.Length <= 4000 ? text : text[^4000..];
    }

    /// <summary>
    /// FFmpeg emits out_time_us=… on its progress stream, and historically
    /// out_time_ms=… — which, despite the name, is also microseconds.
    /// </summary>
    internal static double? ParseOutTimeSeconds(string line)
    {
        foreach (var key in new[] { "out_time_us=", "out_time_ms=" })
        {
            if (!line.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            return double.TryParse(
                line.AsSpan(key.Length),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var microseconds) && microseconds >= 0
                ? microseconds / 1_000_000
                : null;
        }
        return null;
    }
}
