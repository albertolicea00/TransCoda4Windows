using TransCoda.Models;

namespace TransCoda.Core;

/// <summary>
/// Turns conversion settings into an FFmpeg argument list. Arguments are
/// always passed via ProcessStartInfo.ArgumentList — no shell is ever
/// involved, so paths need no quoting or escaping.
/// </summary>
public sealed class FfmpegCommandBuilder
{
    private readonly string _inputPath;
    private readonly string _outputPath;
    private readonly ConversionSettings _settings;
    private readonly HardwareCapabilities _hardware;

    public FfmpegCommandBuilder(string inputPath, string outputPath, ConversionSettings settings, HardwareCapabilities hardware)
    {
        _inputPath = inputPath;
        _outputPath = outputPath;
        _settings = settings;
        _hardware = hardware;
    }

    public IReadOnlyList<string> Arguments()
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", _inputPath };

        if (_settings.Format.IsAudioOnly())
        {
            args.Add("-vn");
        }
        else
        {
            AddVideoArguments(args);
        }

        AddAudioArguments(args);
        AddContainerArguments(args);
        args.AddRange(new[] { "-progress", "pipe:1", "-nostats", _outputPath });
        return args;
    }

    private void AddVideoArguments(List<string> args)
    {
        var quality = _settings.Quality.ToString();
        var hardwareEncoder = _settings.UseHardwareAcceleration
            ? _hardware.Encoder(_settings.VideoCodec)
            : null;

        if (hardwareEncoder is not null)
        {
            args.AddRange(new[] { "-c:v", hardwareEncoder });

            // None of the hardware encoders take -crf; each has its own
            // constant-quality knob.
            if (hardwareEncoder.EndsWith("_nvenc", StringComparison.Ordinal))
            {
                args.AddRange(new[] { "-rc", "vbr", "-cq", quality, "-b:v", "0" });
            }
            else if (hardwareEncoder.EndsWith("_qsv", StringComparison.Ordinal))
            {
                args.AddRange(new[] { "-global_quality", quality });
            }
            else // AMF
            {
                args.AddRange(new[] { "-rc", "cqp", "-qp_i", quality, "-qp_p", quality });
            }
        }
        else
        {
            switch (_settings.VideoCodec)
            {
                case VideoCodec.H264:
                    args.AddRange(new[] { "-c:v", "libx264", "-preset", "medium", "-crf", quality });
                    break;
                case VideoCodec.Hevc:
                    args.AddRange(new[] { "-c:v", "libx265", "-preset", "medium", "-crf", quality });
                    break;
                case VideoCodec.Av1:
                    args.AddRange(new[] { "-c:v", "libsvtav1", "-preset", "8", "-crf", quality });
                    break;
                case VideoCodec.Vp9:
                    args.AddRange(new[] { "-c:v", "libvpx-vp9", "-b:v", "0", "-crf", quality, "-row-mt", "1" });
                    break;
            }
        }

        if (_settings.MaxHeight is int maxHeight)
        {
            // -2 keeps the width even, which most encoders require; min()
            // avoids upscaling sources smaller than the limit.
            args.AddRange(new[] { "-vf", $"scale=-2:min(ih\\,{maxHeight})" });
        }

        if (_settings.VideoCodec == VideoCodec.Hevc
            && _settings.Format is OutputFormat.Mp4 or OutputFormat.Mov)
        {
            // hvc1 tag so QuickTime and Apple devices recognize the track.
            args.AddRange(new[] { "-tag:v", "hvc1" });
        }
    }

    private void AddAudioArguments(List<string> args)
    {
        args.AddRange(_settings.AudioCodec switch
        {
            AudioCodec.Aac => new[] { "-c:a", "aac" },
            AudioCodec.Opus => new[] { "-c:a", "libopus" },
            AudioCodec.Mp3 => new[] { "-c:a", "libmp3lame" },
            AudioCodec.Flac => new[] { "-c:a", "flac" },
            _ => new[] { "-c:a", "pcm_s16le" },
        });

        if (_settings.AudioCodec.SupportsBitrate())
        {
            args.AddRange(new[] { "-b:a", $"{_settings.AudioBitrateKbps}k" });
        }
    }

    private void AddContainerArguments(List<string> args)
    {
        if (_settings.Format is OutputFormat.Mp4 or OutputFormat.Mov or OutputFormat.M4a)
        {
            // Move the moov atom up front so files start playing immediately
            // when streamed.
            args.AddRange(new[] { "-movflags", "+faststart" });
        }
    }
}
