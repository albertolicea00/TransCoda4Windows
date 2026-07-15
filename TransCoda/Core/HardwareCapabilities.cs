using TransCoda.Models;

namespace TransCoda.Core;

/// <summary>
/// Hardware encoders exposed by the local FFmpeg build: NVIDIA NVENC,
/// Intel Quick Sync (QSV), and AMD AMF. Hardware encoding keeps the CPU
/// nearly idle and is dramatically faster than software encoding.
/// </summary>
public sealed class HardwareCapabilities
{
    public static readonly HardwareCapabilities None = new(new HashSet<string>());

    private readonly HashSet<string> _encoderNames;

    private HardwareCapabilities(HashSet<string> encoderNames) => _encoderNames = encoderNames;

    public IEnumerable<string> Encoders => _encoderNames.OrderBy(name => name, StringComparer.Ordinal);

    /// <summary>Best available hardware encoder for a codec, in NVENC → QSV → AMF order.</summary>
    public string? Encoder(VideoCodec codec)
    {
        var candidates = codec switch
        {
            VideoCodec.H264 => new[] { "h264_nvenc", "h264_qsv", "h264_amf" },
            VideoCodec.Hevc => new[] { "hevc_nvenc", "hevc_qsv", "hevc_amf" },
            VideoCodec.Av1 => new[] { "av1_nvenc", "av1_qsv", "av1_amf" },
            _ => Array.Empty<string>(),
        };
        return candidates.FirstOrDefault(_encoderNames.Contains);
    }

    public static async Task<HardwareCapabilities> DetectAsync(string ffmpegPath)
    {
        try
        {
            var output = await ProcessRunner.RunAsync(ffmpegPath, new[] { "-hide_banner", "-encoders" });
            if (output.ExitCode != 0)
            {
                return None;
            }

            // Each encoder line looks like " V....D h264_nvenc  NVIDIA NVENC H.264 encoder".
            var names = new HashSet<string>();
            foreach (var line in output.StandardOutput.Split('\n'))
            {
                var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2)
                {
                    continue;
                }

                var name = columns[1];
                if (name.EndsWith("_nvenc", StringComparison.Ordinal)
                    || name.EndsWith("_qsv", StringComparison.Ordinal)
                    || name.EndsWith("_amf", StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }

            return new HardwareCapabilities(names);
        }
        catch
        {
            return None;
        }
    }
}
