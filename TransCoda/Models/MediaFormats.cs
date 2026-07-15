namespace TransCoda.Models;

public enum OutputFormat
{
    Mp4, Mkv, Mov, Webm,
    M4a, Mp3, Flac, Opus, Wav,
}

public enum VideoCodec
{
    H264, Hevc, Av1, Vp9,
}

public enum AudioCodec
{
    Aac, Opus, Mp3, Flac, Pcm,
}

public static class MediaFormats
{
    public static readonly OutputFormat[] VideoFormats =
    {
        OutputFormat.Mp4, OutputFormat.Mkv, OutputFormat.Mov, OutputFormat.Webm,
    };

    public static readonly OutputFormat[] AudioFormats =
    {
        OutputFormat.M4a, OutputFormat.Mp3, OutputFormat.Flac, OutputFormat.Opus, OutputFormat.Wav,
    };

    public static bool IsAudioOnly(this OutputFormat format) =>
        Array.IndexOf(AudioFormats, format) >= 0;

    public static string FileExtension(this OutputFormat format) =>
        format.ToString().ToLowerInvariant();

    public static string DisplayName(this OutputFormat format) => format switch
    {
        OutputFormat.M4a => "M4A (AAC)",
        _ => format.ToString().ToUpperInvariant(),
    };

    public static string DisplayName(this VideoCodec codec) => codec switch
    {
        VideoCodec.H264 => "H.264 / AVC",
        VideoCodec.Hevc => "H.265 / HEVC",
        VideoCodec.Av1 => "AV1",
        VideoCodec.Vp9 => "VP9",
        _ => codec.ToString(),
    };

    public static string DisplayName(this AudioCodec codec) => codec switch
    {
        AudioCodec.Aac => "AAC",
        AudioCodec.Opus => "Opus",
        AudioCodec.Mp3 => "MP3",
        AudioCodec.Flac => "FLAC",
        AudioCodec.Pcm => "PCM (uncompressed)",
        _ => codec.ToString(),
    };

    public static VideoCodec[] SupportedVideoCodecs(this OutputFormat format) => format switch
    {
        OutputFormat.Mp4 => new[] { VideoCodec.H264, VideoCodec.Hevc, VideoCodec.Av1 },
        OutputFormat.Mkv => new[] { VideoCodec.H264, VideoCodec.Hevc, VideoCodec.Av1, VideoCodec.Vp9 },
        OutputFormat.Mov => new[] { VideoCodec.H264, VideoCodec.Hevc },
        OutputFormat.Webm => new[] { VideoCodec.Vp9, VideoCodec.Av1 },
        _ => Array.Empty<VideoCodec>(),
    };

    public static AudioCodec[] SupportedAudioCodecs(this OutputFormat format) => format switch
    {
        OutputFormat.Mp4 or OutputFormat.Mov or OutputFormat.M4a => new[] { AudioCodec.Aac },
        OutputFormat.Mkv => new[] { AudioCodec.Aac, AudioCodec.Opus, AudioCodec.Mp3, AudioCodec.Flac },
        OutputFormat.Webm or OutputFormat.Opus => new[] { AudioCodec.Opus },
        OutputFormat.Mp3 => new[] { AudioCodec.Mp3 },
        OutputFormat.Flac => new[] { AudioCodec.Flac },
        OutputFormat.Wav => new[] { AudioCodec.Pcm },
        _ => Array.Empty<AudioCodec>(),
    };

    public static bool SupportsBitrate(this AudioCodec codec) =>
        codec is AudioCodec.Aac or AudioCodec.Opus or AudioCodec.Mp3;
}
