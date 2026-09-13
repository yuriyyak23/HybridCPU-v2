using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.Arch;
using Microsoft.Win32;

namespace DoomSharp.HybridCpu.Windows;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _inspection;
    private bool _running;
    private readonly string? observationManifest;
    private string? generatedObservationManifest;

    public MainWindow(string? observationManifest = null)
    {
        this.observationManifest = observationManifest;
        InitializeComponent();
        EnvironmentInfo.Text = $"ISE: {typeof(VLIW_Bundle).Assembly.GetName().Name} {typeof(VLIW_Bundle).Assembly.GetName().Version}\n"
            + $"Runtime: {typeof(HybridCpuManagedBootstrapRuntimeV1).Assembly.GetName().Name}\n"
            + $"Kernel: {typeof(DeterministicRuntimeKernelV1).Assembly.GetName().Name}\n"
            + $"Image: {HybridCpuRestrictedStartupOptionsV1.Production.SchemaId}\n"
            + ".NET 10 · x64 · production DLLs\nTESTING hooks не используются";
        Log.Text = "HCDOOMGUI1100: loader / ISE ECALL service подключён; read-only inspection не устанавливает Doom qualification.\n"
            + "Выберите .hcexe и WAD для read-only проверки. Приложение не публикует и не запускает Doom через CoreCLR.";
    }

    public void SetRunInputs(string image, string wad)
    {
        ImagePath.Text = image;
        WadPath.Text = wad;
        RunButton.IsEnabled = File.Exists(image) && File.Exists(wad);
    }

    public void StartCommandLineRun() => RunGuest(this, new RoutedEventArgs());

    private string? ResolveObservationManifest()
    {
        if (!string.IsNullOrWhiteSpace(observationManifest)) return observationManifest;
        if (!string.IsNullOrWhiteSpace(generatedObservationManifest)) return generatedObservationManifest;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string tempEnv = Path.Combine(directory.FullName, "TempEnv");
            if (Directory.Exists(tempEnv))
            {
                string? continuation = Directory.EnumerateDirectories(tempEnv, "doom-continuation-*")
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                string runName = "doom-windows-gui-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff", CultureInfo.InvariantCulture)
                    + "-" + Guid.NewGuid().ToString("N");
                return generatedObservationManifest = Path.Combine(continuation ?? tempEnv, runName, "observation-manifest.json");
            }
            directory = directory.Parent;
        }
        return null;
    }

    private void ChooseImage(object sender, RoutedEventArgs e) => SelectFile(ImagePath, "HybridCPU executable|*.hcexe");
    private void ChooseWad(object sender, RoutedEventArgs e) => SelectFile(WadPath, "Doom resources|*.wad;*.WAD");
    private void SelectFile(System.Windows.Controls.TextBox target, string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FileName;
            RunButton.IsEnabled = File.Exists(ImagePath.Text) && File.Exists(WadPath.Text);
            Status.Text = "Выбор изменён — выполните проверку или bounded ISE run.";
            Log.Text = "Предыдущий результат сброшен. Нажмите «Проверить запуск».";
        }
    }

    private async void InspectLaunch(object sender, RoutedEventArgs e)
    {
        if (_inspection is not null) return;
        using var cancellation = new CancellationTokenSource();
        _inspection = cancellation;
        InspectButton.IsEnabled = ImageBrowse.IsEnabled = WadBrowse.IsEnabled = false;
        CancelButton.IsEnabled = true;
        string imagePath = ImagePath.Text, wadPath = WadPath.Text;
        Status.Text = "Проверка файлов и контрактов…";
        try
        {
            AdmissionReport report = await Task.Run(() => ImageAdmission.Inspect(imagePath, wadPath, cancellation.Token));
            var text = new StringBuilder();
            if (report.Image is { Status: HybridCpuStartupStatusV1.Success } image)
            {
                text.AppendLine($"IMAGE VALID (не execution authority): {image.PackageSha256}");
                text.AppendLine($"Entry: 0x{image.EntryAddress:x}; base: 0x{image.ImageBase:x}; payload: {image.ImageBytes.Length:N0} bytes");
                // Decode with the actual ISE carrier; never inject a bundle or mutate CPU state.
                ulong delta = image.EntryAddress - image.ImageBase;
                if (image.EntryAddress >= image.ImageBase && delta <= (ulong)image.ImageBytes.Length
                    && (ulong)image.ImageBytes.Length - delta >= HybridCpuBundleSerializer.BundleSizeBytes)
                {
                    var bundle = new VLIW_Bundle();
                    if (bundle.TryReadBytes(image.ImageBytes, checked((int)delta)))
                        text.AppendLine("ISE entry slots (opcode IDs): " + string.Join(", ", Enumerable.Range(0, 8).Select(i => bundle.GetInstruction(i).OpCode)));
                }
            }
            if (report.Wad is { } wad)
                text.AppendLine($"WAD DIRECTORY VALID: {wad.Kind}, {wad.LumpCount} lumps, {wad.Bytes:N0} bytes\nSHA-256: {wad.Sha256}\nЭто проверка файла, не guest materialization.");
            foreach (var issue in report.Issues) text.AppendLine($"{issue.Code}: {issue.Detail}");
            Log.Text = text.ToString();
            RunButton.IsEnabled = report.CanExecute;
            Status.Text = report.CanExecute
                ? "Файлы допущены к явному bounded ISE run; qualification отсутствует."
                : $"Запуск заблокирован: {report.Issues.FirstOrDefault()?.Code ?? "HCDOOMGUI1100"}";
        }
        catch (OperationCanceledException) { Status.Text = "Проверка отменена. CPU не запущен."; }
        catch (Exception exception)
        {
            Status.Text = "Ошибка проверки — запуск запрещён";
            Log.Text = $"HCDOOMGUI1999: {exception.Message}";
        }
        finally
        {
            _inspection = null;
            InspectButton.IsEnabled = ImageBrowse.IsEnabled = WadBrowse.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private void CancelInspection(object sender, RoutedEventArgs e) => _inspection?.Cancel();

    private async void RunGuest(object sender, RoutedEventArgs e)
    {
        if (_running || !File.Exists(ImagePath.Text) || !File.Exists(WadPath.Text)) return;
        _running = true;
        RunButton.IsEnabled = InspectButton.IsEnabled = ImageBrowse.IsEnabled = WadBrowse.IsEnabled = false;
        Status.Text = "ISE guest выполняется с bounded cycle budget…";
        try
        {
            string image = ImagePath.Text, wad = WadPath.Text;
            string? manifest = ResolveObservationManifest();
            if (manifest is not null)
            {
                string? manifestDirectory = Path.GetDirectoryName(manifest);
                if (!string.IsNullOrWhiteSpace(manifestDirectory))
                    Directory.CreateDirectory(manifestDirectory);
            }
            using var transport = manifest is null ? null :
                new global::ObservationHostTransport(manifest);
            DoomGuestExecutionReport report = await Task.Run(() =>
                new DoomGuestExecutionService
                {
                    FramePresented = PresentFrame,
                    ObservationAttach = transport is null ? null : transport.Attach
                }.Execute(image, wad, 100_000_000, requireGcSafepointEvidence: true));
            Status.Text = $"ISE завершён: exit {report.ExitCode}; {report.Detail}";
            Log.Text = report.Detail + Environment.NewLine +
                string.Join(Environment.NewLine, report.ConsoleOutput?.Select(row => row.Text) ?? []) +
                (transport is null ? string.Empty :
                    Environment.NewLine + "Observation transport: " +
                    JsonSerializer.Serialize(transport.Report));
        }
        catch (Exception exception)
        {
            Status.Text = "ISE execution fault — результат не квалифицирован";
            string detail = $"HCDOOMGUI1998: {exception}\n";
            Log.Text = detail;
            try
            {
                string? manifest = ResolveObservationManifest();
                string? directory = manifest is null ? null : Path.GetDirectoryName(manifest);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, "execution-fault.txt"), detail);
                }
            }
            catch { /* diagnostics must not mask the original execution fault */ }
        }
        finally
        {
            _running = false;
            RunButton.IsEnabled = File.Exists(ImagePath.Text) && File.Exists(WadPath.Text);
            InspectButton.IsEnabled = ImageBrowse.IsEnabled = WadBrowse.IsEnabled = true;
        }
    }

    private async void CaptureSnapshot(object sender, RoutedEventArgs e)
    {
        string? manifest = ResolveObservationManifest();
        if (string.IsNullOrWhiteSpace(manifest))
        {
            SnapshotText.Text = "Observation manifest не найден: запустите GUI из дерева репозитория с доступным TempEnv.";
            return;
        }

        string runDirectory = Path.GetDirectoryName(manifest)!;
        string continuationDirectory = Directory.GetParent(runDirectory)!.FullName;
        string runner = Path.Combine(continuationDirectory, "doom-windows-frame-v3", "loader-host", "bin",
            "HybridCpuBootBlobEvidence", "release", "HybridCpuBootBlobEvidence.dll");
        if (!File.Exists(runner) || !File.Exists(manifest))
        {
            SnapshotText.Text = !File.Exists(runner)
                ? $"Snapshot недоступен: observation runner не найден.\nRunner: {runner}"
                : $"Snapshot недоступен: активный ISE run не опубликовал observation manifest.\n"
                    + "Сначала нажмите «Запуск ISE» и дождитесь статуса выполнения, затем повторите snapshot.\n"
                    + $"Manifest: {manifest}";
            return;
        }

        SnapshotButton.IsEnabled = false;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(runner);
            process.StartInfo.ArgumentList.Add("observe-once");
            process.StartInfo.ArgumentList.Add(manifest);
            process.Start();
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            SnapshotText.Text = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
            if (process.ExitCode != 0)
                SnapshotText.Text = $"observe-once exit {process.ExitCode}\n" + SnapshotText.Text;
        }
        catch (Exception exception)
        {
            SnapshotText.Text = $"HCDOOMGUI1401: snapshot failed: {exception.Message}";
        }
        finally { SnapshotButton.IsEnabled = true; }
    }

    public void PresentFrame(DoomFramebufferFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Dispatcher.BeginInvoke(() =>
        {
            byte[] bgr = new byte[checked(frame.Width * frame.Height * 3)];
            for (int i = 0; i < frame.Pixels.Length; i++)
            {
                int palette = frame.Pixels[i] * 3;
                int pixel = i * 3;
                bgr[pixel] = frame.PaletteRgb24[palette + 2];
                bgr[pixel + 1] = frame.PaletteRgb24[palette + 1];
                bgr[pixel + 2] = frame.PaletteRgb24[palette];
            }
            GameFrame.Source = BitmapSource.Create(frame.Width, frame.Height, 96, 96,
                PixelFormats.Bgr24, null, bgr, frame.Width * 3);
            NoFramePanel.Visibility = Visibility.Collapsed;
            Status.Text = $"Frame {frame.Sequence}: {frame.Width}×{frame.Height}";
        }, System.Windows.Threading.DispatcherPriority.Render);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_running)
        {
            MessageBoxResult result = MessageBox.Show(this,
                "ISE guest сейчас выполняется. Закрыть GUI и завершить host-процесс?",
                "Подтверждение выхода", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        _inspection?.Cancel();
        base.OnClosing(e);
    }
}
