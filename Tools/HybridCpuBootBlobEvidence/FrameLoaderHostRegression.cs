using System.Text.Json;
using DoomSharp.HybridCpu.Windows;

internal static class FrameLoaderHostRegression
{
    public static int Run(string image, string wad, int budget)
    {
        int frames = 0;
        string? firstFrameSha = null;
        DoomGuestExecutionReport report = new DoomGuestExecutionService
        {
            FramePresented = frame => { frames++; firstFrameSha ??= frame.PixelSha256; }
        }.Execute(image, wad, budget, requireGcSafepointEvidence: true);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            LoaderBacked = true, report.ExitCode, report.Detail, Frames = frames,
            FirstFrameSha256 = firstFrameSha, Execution = report.Execution
        }, new JsonSerializerOptions { WriteIndented = true }));
        return report.ExitCode;
    }
}
