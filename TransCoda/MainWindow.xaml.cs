using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using TransCoda.Core;
using TransCoda.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TransCoda;

public sealed partial class MainWindow : Window
{
    private sealed record Choice(string Label, object? Value)
    {
        public override string ToString() => Label;
    }

    public JobQueue Queue => App.Queue;

    private bool _updatingControls;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarArea);
        SystemBackdrop = new MicaBackdrop();

        PopulateFormatChoices();
        PopulateResolutionChoices();
        PopulateAudioBitrateChoices();
        RefreshCodecChoices();

        Queue.StateChanged += RefreshCommandState;
        Queue.Jobs.CollectionChanged += (_, _) => RefreshCommandState();

        _ = InitializeQueueAsync();
    }

    private async Task InitializeQueueAsync()
    {
        await Queue.InitializeAsync();
        FfmpegInfoBar.IsOpen = Queue.Installation is null;
        RefreshCommandState();
    }

    // MARK: Command bar

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        foreach (var extension in JobQueue.SupportedExtensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

        var files = await picker.PickMultipleFilesAsync();
        Queue.Add(files.Select(file => file.Path));
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _ = Queue.StartAsync();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Queue.ClearFinished();
    }

    private void RefreshCommandState()
    {
        StartButton.IsEnabled =
            !Queue.IsProcessing
            && Queue.Installation is not null
            && Queue.Jobs.Any(job => job.Status == JobStatus.Waiting);
        ClearButton.IsEnabled = Queue.Jobs.Any(job => job.IsFinished);
        EmptyState.Visibility = Queue.Jobs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // MARK: Job row actions

    private void CancelJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ConversionJob job)
        {
            Queue.Cancel(job);
        }
    }

    private void RevealJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ConversionJob { OutputPath: { } outputPath })
        {
            Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
        }
    }

    // MARK: Drag & drop

    private void Queue_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void Queue_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        Queue.Add(items.OfType<StorageFile>().Select(file => file.Path));
    }

    // MARK: Output settings

    private void PopulateFormatChoices()
    {
        _updatingControls = true;
        FormatBox.ItemsSource = MediaFormats.VideoFormats
            .Concat(MediaFormats.AudioFormats)
            .Select(format => new Choice(format.DisplayName(), format))
            .ToList();
        FormatBox.SelectedIndex = 0;
        _updatingControls = false;
    }

    private void PopulateResolutionChoices()
    {
        _updatingControls = true;
        var items = new List<Choice> { new("Original", null) };
        items.AddRange(new[] { 2160, 1440, 1080, 720, 480 }.Select(height => new Choice($"{height}p", height)));
        ResolutionBox.ItemsSource = items;
        ResolutionBox.SelectedIndex = 0;
        _updatingControls = false;
    }

    private void PopulateAudioBitrateChoices()
    {
        _updatingControls = true;
        var bitrates = new[] { 96, 128, 160, 192, 256, 320 };
        AudioBitrateBox.ItemsSource = bitrates.Select(kbps => new Choice($"{kbps} kbps", kbps)).ToList();
        AudioBitrateBox.SelectedIndex = Array.IndexOf(bitrates, Queue.Settings.AudioBitrateKbps);
        _updatingControls = false;
    }

    /// <summary>
    /// Rebuilds the codec combo boxes after a container change, clamping the
    /// current selection to what the container supports.
    /// </summary>
    private void RefreshCodecChoices()
    {
        _updatingControls = true;
        try
        {
            var format = Queue.Settings.Format;
            VideoSection.Visibility = format.IsAudioOnly() ? Visibility.Collapsed : Visibility.Visible;

            if (!format.IsAudioOnly())
            {
                var videoCodecs = format.SupportedVideoCodecs();
                if (Array.IndexOf(videoCodecs, Queue.Settings.VideoCodec) < 0)
                {
                    Queue.Settings.VideoCodec = videoCodecs[0];
                }
                VideoCodecBox.ItemsSource = videoCodecs
                    .Select(codec => new Choice(codec.DisplayName(), codec))
                    .ToList();
                VideoCodecBox.SelectedIndex = Array.IndexOf(videoCodecs, Queue.Settings.VideoCodec);
            }

            var audioCodecs = format.SupportedAudioCodecs();
            if (Array.IndexOf(audioCodecs, Queue.Settings.AudioCodec) < 0)
            {
                Queue.Settings.AudioCodec = audioCodecs[0];
            }
            AudioCodecBox.ItemsSource = audioCodecs
                .Select(codec => new Choice(codec.DisplayName(), codec))
                .ToList();
            AudioCodecBox.SelectedIndex = Array.IndexOf(audioCodecs, Queue.Settings.AudioCodec);
            AudioBitrateBox.Visibility = Queue.Settings.AudioCodec.SupportsBitrate()
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _updatingControls = false;
        }
    }

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || FormatBox.SelectedItem is not Choice choice)
        {
            return;
        }
        Queue.Settings.Format = (OutputFormat)choice.Value!;
        RefreshCodecChoices();
    }

    private void VideoCodec_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || VideoCodecBox.SelectedItem is not Choice choice)
        {
            return;
        }
        Queue.Settings.VideoCodec = (VideoCodec)choice.Value!;
    }

    private void AudioCodec_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || AudioCodecBox.SelectedItem is not Choice choice)
        {
            return;
        }
        Queue.Settings.AudioCodec = (AudioCodec)choice.Value!;
        AudioBitrateBox.Visibility = Queue.Settings.AudioCodec.SupportsBitrate()
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void AudioBitrate_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || AudioBitrateBox.SelectedItem is not Choice choice)
        {
            return;
        }
        Queue.Settings.AudioBitrateKbps = (int)choice.Value!;
    }

    private void Resolution_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingControls || ResolutionBox.SelectedItem is not Choice choice)
        {
            return;
        }
        Queue.Settings.MaxHeight = (int?)choice.Value;
    }

    private void Quality_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }
        var quality = (int)e.NewValue;
        Queue.Settings.Quality = quality;
        QualitySlider.Header = $"Quality (CRF {quality})";
    }

    private void Hardware_Toggled(object sender, RoutedEventArgs e)
    {
        if (_updatingControls)
        {
            return;
        }
        Queue.Settings.UseHardwareAcceleration = HardwareToggle.IsOn;
    }

    // MARK: Destination

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        Queue.OutputDirectory = folder.Path;
        DestinationLabel.Text = folder.Path;
        ResetFolderButton.Visibility = Visibility.Visible;
    }

    private void ResetFolder_Click(object sender, RoutedEventArgs e)
    {
        Queue.OutputDirectory = null;
        DestinationLabel.Text = "Same folder as source";
        ResetFolderButton.Visibility = Visibility.Collapsed;
    }
}
