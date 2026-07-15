using System.ComponentModel;

namespace TransCoda.Models;

public enum JobStatus
{
    Waiting, Probing, Running, Completed, Failed, Cancelled,
}

/// <summary>
/// One queued conversion. Property changes must happen on the UI thread —
/// the queue guarantees this by marshaling engine progress through
/// <see cref="Progress{T}"/>.
/// </summary>
public sealed class ConversionJob : INotifyPropertyChanged
{
    public ConversionJob(string sourcePath, ConversionSettings settings)
    {
        SourcePath = sourcePath;
        Settings = settings;
    }

    public string SourcePath { get; }
    public ConversionSettings Settings { get; set; }
    public string? OutputPath { get; set; }
    public double? DurationSeconds { get; set; }
    public string? ErrorMessage { get; private set; }

    public string FileName => Path.GetFileName(SourcePath);

    private JobStatus _status = JobStatus.Waiting;
    public JobStatus Status => _status;

    private double _progress;
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            Raise(nameof(Progress));
            Raise(nameof(StatusText));
        }
    }

    public void SetStatus(JobStatus status, string? error = null)
    {
        _status = status;
        ErrorMessage = error;
        Raise(nameof(Status));
        Raise(nameof(StatusText));
        Raise(nameof(IsRunning));
        Raise(nameof(ShowCancel));
        Raise(nameof(ShowReveal));
    }

    public bool IsRunning => _status == JobStatus.Running;

    public bool IsFinished =>
        _status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled;

    public bool ShowCancel =>
        _status is JobStatus.Waiting or JobStatus.Probing or JobStatus.Running;

    public bool ShowReveal => _status == JobStatus.Completed && OutputPath is not null;

    public string StatusText => _status switch
    {
        JobStatus.Waiting => "Waiting",
        JobStatus.Probing => "Preparing…",
        JobStatus.Running => $"{_progress:P0}",
        JobStatus.Completed => "Done",
        JobStatus.Failed => ErrorMessage ?? "Failed",
        JobStatus.Cancelled => "Cancelled",
        _ => string.Empty,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
