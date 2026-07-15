namespace TransCoda.Core;

public sealed record FfmpegInstallation(string FfmpegPath, string? FfprobePath);

public static class FfmpegLocator
{
    public static FfmpegInstallation? Locate()
    {
        var ffmpeg = Find("ffmpeg.exe");
        return ffmpeg is null ? null : new FfmpegInstallation(ffmpeg, Find("ffprobe.exe"));
    }

    private static string? Find(string fileName)
    {
        // 1. Binaries shipped next to the app.
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, fileName),
            Path.Combine(baseDirectory, "ffmpeg", fileName),
            Path.Combine(baseDirectory, "ffmpeg", "bin", fileName),
            Path.Combine(@"C:\ffmpeg\bin", fileName),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 2. Anything on PATH (covers winget/choco/scoop installs).
        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry; skip it.
            }
        }

        return null;
    }
}
