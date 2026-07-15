using System.Collections.ObjectModel;
using TransCoda.Models;

namespace TransCoda.Core;

/// <summary>
/// Ordered conversion queue. Jobs run one at a time — a single FFmpeg encode
/// already saturates the GPU encoder or CPU, and serial processing keeps
/// memory usage flat regardless of queue size.
///
/// All members must be called from the UI thread; background work marshals
/// back through async continuations and <see cref="Progress{T}"/>.
/// </summary>
public sealed class JobQueue
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".wmv", ".flv",
        ".ts", ".mts", ".m2ts", ".3gp", ".mpg", ".mpeg", ".ogv",
        ".mp3", ".m4a", ".aac", ".flac", ".wav", ".ogg", ".oga", ".opus", ".wma", ".aiff",
    };

    public ObservableCollection<ConversionJob> Jobs { get; } = new();
    public ConversionSettings Settings { get; } = new();

    /// <summary>Destination folder; null saves next to each source file.</summary>
    public string? OutputDirectory { get; set; }

    public FfmpegInstallation? Installation { get; private set; }
    public HardwareCapabilities Hardware { get; private set; } = HardwareCapabilities.None;
    public bool IsProcessing { get; private set; }

    /// <summary>Raised whenever queue-level state changes (not per-job progress).</summary>
    public event Action? StateChanged;

    private TranscodeEngine? _activeEngine;
    private ConversionJob? _activeJob;

    public async Task InitializeAsync()
    {
        Installation = FfmpegLocator.Locate();
        if (Installation is not null)
        {
            Hardware = await HardwareCapabilities.DetectAsync(Installation.FfmpegPath);
        }
        StateChanged?.Invoke();
    }

    public void Add(IEnumerable<string> paths)
    {
        var pending = new HashSet<string>(
            Jobs.Where(job => !job.IsFinished).Select(job => job.SourcePath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }
            if (!pending.Add(path))
            {
                continue;
            }
            Jobs.Add(new ConversionJob(path, Settings));
        }
        StateChanged?.Invoke();
    }

    public void Cancel(ConversionJob job)
    {
        if (job.Status == JobStatus.Waiting)
        {
            job.SetStatus(JobStatus.Cancelled);
        }
        else if (ReferenceEquals(job, _activeJob))
        {
            _activeEngine?.Cancel();
        }
        StateChanged?.Invoke();
    }

    public void ClearFinished()
    {
        foreach (var job in Jobs.Where(job => job.IsFinished).ToArray())
        {
            Jobs.Remove(job);
        }
        StateChanged?.Invoke();
    }

    public async Task StartAsync()
    {
        if (IsProcessing || Installation is null)
        {
            return;
        }

        IsProcessing = true;
        StateChanged?.Invoke();

        while (Jobs.FirstOrDefault(job => job.Status == JobStatus.Waiting) is { } job)
        {
            await ProcessAsync(job);
        }

        IsProcessing = false;
        StateChanged?.Invoke();
    }

    private async Task ProcessAsync(ConversionJob job)
    {
        var installation = Installation!;

        // Settings are captured when the job starts, so tweaks made while
        // earlier jobs run still apply to the rest of the queue.
        job.Settings = Settings.Normalized();
        job.SetStatus(JobStatus.Probing);

        if (installation.FfprobePath is not null)
        {
            job.DurationSeconds = await Ffprobe.GetDurationSecondsAsync(installation.FfprobePath, job.SourcePath);
        }

        var outputPath = AvailableOutputPath(job);
        job.OutputPath = outputPath;

        var builder = new FfmpegCommandBuilder(job.SourcePath, outputPath, job.Settings, Hardware);
        var engine = new TranscodeEngine();
        _activeEngine = engine;
        _activeJob = job;
        job.SetStatus(JobStatus.Running);

        try
        {
            await engine.RunAsync(
                installation.FfmpegPath,
                builder.Arguments(),
                job.DurationSeconds,
                new Progress<double>(value => job.Progress = value));
            job.Progress = 1;
            job.SetStatus(JobStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            job.SetStatus(JobStatus.Cancelled);
            TryDelete(outputPath);
        }
        catch (Exception exception)
        {
            job.SetStatus(JobStatus.Failed, exception.Message);
            TryDelete(outputPath);
        }
        finally
        {
            _activeEngine = null;
            _activeJob = null;
        }
    }

    private string AvailableOutputPath(ConversionJob job)
    {
        var directory = OutputDirectory
            ?? Path.GetDirectoryName(job.SourcePath)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var baseName = Path.GetFileNameWithoutExtension(job.SourcePath);
        var extension = job.Settings.Format.FileExtension();

        var candidate = Path.Combine(directory, $"{baseName}.{extension}");
        var suffix = 1;
        while (File.Exists(candidate))
        {
            suffix++;
            candidate = Path.Combine(directory, $"{baseName} {suffix}.{extension}");
        }
        return candidate;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best effort; FFmpeg may still be releasing the handle.
        }
    }
}
