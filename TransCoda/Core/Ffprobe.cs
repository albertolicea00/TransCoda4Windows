using System.Globalization;

namespace TransCoda.Core;

public static class Ffprobe
{
    /// <summary>
    /// Returns the media duration in seconds, or null when it cannot be
    /// determined. A missing duration only degrades progress reporting;
    /// the conversion itself still runs.
    /// </summary>
    public static async Task<double?> GetDurationSecondsAsync(string ffprobePath, string mediaPath)
    {
        try
        {
            var output = await ProcessRunner.RunAsync(ffprobePath, new[]
            {
                "-v", "error",
                "-show_entries", "format=duration",
                "-of", "default=noprint_wrappers=1:nokey=1",
                mediaPath,
            });

            if (output.ExitCode != 0)
            {
                return null;
            }

            return double.TryParse(
                output.StandardOutput.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds) ? seconds : null;
        }
        catch
        {
            return null;
        }
    }
}
