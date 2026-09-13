using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DoomSharp.HybridCpu.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? observationManifest = null;
        int manifestIndex = Array.IndexOf(e.Args, "--observation-manifest");
        if (manifestIndex >= 0 && manifestIndex + 1 < e.Args.Length)
            observationManifest = Path.GetFullPath(e.Args[manifestIndex + 1]);
        var window = new MainWindow(observationManifest);
        MainWindow = window;
        if (e.Args.Length >= 3 && e.Args[0] == "--run")
        {
            window.SetRunInputs(Path.GetFullPath(e.Args[1]), Path.GetFullPath(e.Args[2]));
            window.Show();
            window.Dispatcher.BeginInvoke(new Action(window.StartCommandLineRun),
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }
        if (e.Args is ["--ui-smoke", var output])
        {
            // Offscreen real WPF rendering; never invokes compilation or CPU execution.
            window.ShowInTaskbar = false;
            window.Left = -10000;
            window.Top = -10000;
            window.Show();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                try
                {
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(output);
                    encoder.Save(file);
                    Shutdown(0);
                }
                catch (Exception exception) { Console.Error.WriteLine(exception); Shutdown(1); }
            }));
            return;
        }
        window.Show();
    }
}
