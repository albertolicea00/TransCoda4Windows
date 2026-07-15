namespace TransCoda.Models;

public sealed class ConversionSettings
{
    public OutputFormat Format { get; set; } = OutputFormat.Mp4;
    public VideoCodec VideoCodec { get; set; } = VideoCodec.H264;
    public AudioCodec AudioCodec { get; set; } = AudioCodec.Aac;

    /// <summary>CRF-style quality: lower is better. Sensible range is 18–30.</summary>
    public int Quality { get; set; } = 23;

    public int AudioBitrateKbps { get; set; } = 192;

    /// <summary>Maximum output height in pixels; null keeps the source resolution.</summary>
    public int? MaxHeight { get; set; }

    public bool UseHardwareAcceleration { get; set; } = true;

    /// <summary>Clamps codec choices to what the selected container supports.</summary>
    public ConversionSettings Normalized()
    {
        var copy = (ConversionSettings)MemberwiseClone();

        if (!copy.Format.IsAudioOnly())
        {
            var videoCodecs = copy.Format.SupportedVideoCodecs();
            if (Array.IndexOf(videoCodecs, copy.VideoCodec) < 0)
            {
                copy.VideoCodec = videoCodecs[0];
            }
        }

        var audioCodecs = copy.Format.SupportedAudioCodecs();
        if (Array.IndexOf(audioCodecs, copy.AudioCodec) < 0)
        {
            copy.AudioCodec = audioCodecs[0];
        }

        copy.Quality = Math.Clamp(copy.Quality, 0, 51);
        return copy;
    }
}
