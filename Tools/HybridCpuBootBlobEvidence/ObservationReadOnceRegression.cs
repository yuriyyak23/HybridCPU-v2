using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DoomSharp.HybridCpu.Windows;

internal static class ObservationReadOnceRegression
{
    public static int Run(string directory)
    {
        string image = GcEvidenceModeRegression.CreateObservationFixture(directory, 8192);
        var baseline = new DoomGuestExecutionService().Execute(image, 20000, true);
        if (baseline.ExitCode != 0) throw new InvalidOperationException(baseline.Detail);
        string manifest = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(manifest);
        Process? client = null;
        Task<string>? stdout = null, stderr = null;
        try
        {
            var result = new DoomGuestExecutionService
            {
                ObservationAttach = binding =>
                {
                    var consumer = host.Attach(binding);
                    if (binding.SegmentKind == "Main")
                    {
                        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardOutput = true, RedirectStandardError = true };
                        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                        start.ArgumentList.Add("observe-once"); start.ArgumentList.Add(manifest); start.ArgumentList.Add(image);
                        client = Process.Start(start)!;
                        stdout = client.StandardOutput.ReadToEndAsync(); stderr = client.StandardError.ReadToEndAsync();
                    }
                    return consumer;
                }
            }.Execute(image, 20000, true);
            if (client is null) throw new InvalidOperationException("Main client was not launched.");
            client.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
            string text = stdout!.GetAwaiter().GetResult();
            using (var output = new StreamWriter(new FileStream(Path.Combine(directory, "client.json"), FileMode.CreateNew))) output.Write(text);
            using (var output = new StreamWriter(new FileStream(Path.Combine(directory, "client.stderr.log"), FileMode.CreateNew))) output.Write(stderr!.GetAwaiter().GetResult());
            if (client.ExitCode != 0) throw new InvalidOperationException("observe-once exit " + client.ExitCode);
            using var observed = JsonDocument.Parse(text);
            var envelope = observed.RootElement.GetProperty("Observation");
            var progress = observed.RootElement.GetProperty("Progress");
            if (progress.GetProperty("CycleCount").GetUInt64() == 0 ||
                progress.GetProperty("InstructionCounter").GetString() != "Available" ||
                progress.GetProperty("PipelineInstructionsRetired").GetUInt64() == 0 ||
                progress.GetProperty("Location").GetProperty("Methods").GetArrayLength() != 1)
                throw new InvalidDataException("Progress must preserve cycles and exact image method mapping without inventing instruction counts.");
            if (envelope.GetProperty("Status").GetString() != "Observed" || envelope.GetProperty("Binding").GetProperty("Kind").GetString() != "Main" ||
                observed.RootElement.GetProperty("Disconnect").GetString() != "Disconnected") throw new InvalidDataException("Generic CLI capture/lifecycle mismatch.");
            if (JsonSerializer.Serialize(result.Execution! with { ObservationDiagnostics = null }) != JsonSerializer.Serialize(baseline.Execution))
                throw new InvalidDataException("Generic CLI changed guest outcome.");
            using (var output = new FileStream(Path.Combine(directory, "outcomes.json"), FileMode.CreateNew))
                JsonSerializer.Serialize(output, new { Baseline = baseline.Execution, Observed = result.Execution, ClientExit = client.ExitCode, Transport = host.Report });
            Console.WriteLine("PASS generic observe-once: independent client exit 0, production-loader Main capture and disconnect; unchanged guest outcome. Not Doom qualification.");
            return 0;
        }
        finally { host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); client?.Dispose(); }
    }
}
