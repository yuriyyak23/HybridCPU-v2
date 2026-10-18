using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using CpuInterfaceBridge.Diagnostics;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU_ISE.NonRTL.Runtime;

// Test-only host instrumentation. Sampling never drives the guest or reads arbitrary RAM.
internal sealed class InitializerEvidence : IDisposable
{
    internal const int MaximumCheckpoints = 4096;
    internal sealed record CheckpointPlan(int SegmentCount, int MaximumCyclesPerSegment,
        int PeriodicCycles, int MaximumPeriodicPerSegment, int MaximumSnapshots);
    private readonly string directory, manifestPath;
    private readonly ObservationHostTransport transport;
    private readonly HybridCpuRestrictedImageV1 image;
    private readonly Channel<object> queue = Channel.CreateBounded<object>(new BoundedChannelOptions(64)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly Task writer;
    private readonly List<object> checkpoints = new();
    private readonly CheckpointPlan plan;
    private string? failure;
    private int samples;
    public TestOnlyInitializerTrace Trace { get; } = new();

    public InitializerEvidence(string directory, string manifestPath, ObservationHostTransport transport,
        HybridCpuRestrictedImageV1 image, int maximumCyclesPerSegment)
    {
        this.directory = Path.GetFullPath(directory);
        this.manifestPath = manifestPath;
        this.transport = transport;
        this.image = image;
        plan = CreatePlan(image.RuntimeBootstrap!.ModuleInitializers.Count + 1, maximumCyclesPerSegment);
        if (Directory.Exists(directory)) throw new IOException("Fresh initializer evidence directory required.");
        ObservationTransportProtocol.ValidateManifest(
            ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(manifestPath)));
        Directory.CreateDirectory(directory);
        AtomicJson(Path.Combine(directory, "started.json"), new {
            Schema = "hybridcpu.test-initializer-started/v1", Trace.RunId, Utc = DateTimeOffset.UtcNow,
            transport.Manifest.Host, transport.Manifest.EndpointGeneration, ImageSha256 = image.PackageSha256,
            CommandLine = Environment.CommandLine, ManifestPath = manifestPath, ManifestSha256 = Hash(manifestPath),
            CheckpointPlan = plan
        });
        writer = Task.Run(WriteAsync);
    }

    public IOwnedCoreObservationConsumer Attach(OwnedCoreObservationBinding binding)
        => new Consumer(this, binding, transport.Attach(binding));

    private sealed class Consumer : IOwnedCoreObservationConsumer
    {
        private readonly InitializerEvidence owner;
        private readonly OwnedCoreObservationBinding binding;
        private readonly IOwnedCoreObservationConsumer transportConsumer;
        private readonly HostObservationEndpoint endpoint;
        private readonly HostObservationConnection connection;
        private long boundaries;
        public Consumer(InitializerEvidence owner, OwnedCoreObservationBinding binding,
            IOwnedCoreObservationConsumer transportConsumer)
        {
            this.owner = owner; this.binding = binding; this.transportConsumer = transportConsumer;
            endpoint = IseHostObservationAdapter.Create("test-only-initializer-checkpoints", binding.Observer);
            connection = endpoint.Connect(endpoint.Descriptor.Identity);
        }
        private void Capture(string kind)
        {
            try
            {
                var capture = connection.CaptureOnce(0);
                if (capture.Status != HostCaptureStatus.Observed || capture.Frame is null)
                    throw new InvalidDataException("Checkpoint capture unavailable: " + capture.Status);
                var snapshot = capture.Frame.Snapshot;
                var active = owner.Trace.Active;
                var payload = new {
                    Schema = "hybridcpu.test-initializer-checkpoint/v1", Utc = DateTimeOffset.UtcNow, Kind = kind,
                    owner.transport.Manifest.Host, RunId = owner.Trace.RunId, binding.SegmentId,
                    Generation = owner.transport.CurrentBinding?.Generation,
                    owner.transport.Manifest.EndpointGeneration, binding.SegmentKind,
                    ActiveInitializer = active, InitializerEntryAddress = active?.EntryAddress,
                    binding.EntryAddress, owner.image.ImageBase, ImageSha256 = owner.image.PackageSha256,
                    binding.RunIdentity, OwnerBoundaries = boundaries,
                    snapshot.CycleCount, snapshot.PipelineInstructionsRetired, snapshot.LiveInstructionPointer,
                    RetirePc = snapshot.RetireVisibilityCertificate.Pc,
                    RetirePublication = snapshot.RetireVisibilityCertificate,
                    snapshot.IsStalled, snapshot.HasExceptions, snapshot.PipelineIPC, snapshot.PipelineEfficiency,
                    snapshot.PipelineStallCycles, snapshot.PipelineDataHazards, snapshot.PipelineMemoryStalls,
                    LiveLocation = owner.Locate(snapshot.LiveInstructionPointer),
                    RetireLocation = owner.Locate(snapshot.RetireVisibilityCertificate.Pc),
                    Capture = capture
                };
                if (++owner.samples > MaximumCheckpoints || !owner.queue.Writer.TryWrite(payload))
                    owner.failure ??= "Bounded checkpoint queue/capture limit exceeded; evidence incomplete.";
            }
            catch (Exception error) { owner.failure ??= error.ToString(); }
        }
        public void OnBoundary()
        {
            transportConsumer.OnBoundary();
            if (boundaries % owner.plan.PeriodicCycles == 0) Capture("Periodic");
            boundaries++;
        }
        public void Dispose()
        {
            try { Capture("SegmentFinal"); }
            finally { connection.Dispose(); endpoint.Dispose(); transportConsumer.Dispose(); }
        }
    }

    internal object[] Locate(ulong pc) => image.RuntimeBootstrap!.CodeManagerRecords
        .Where(row => pc >= image.ImageBase + (ulong)row.CodeStartOffsetBytes &&
            pc - image.ImageBase - (ulong)row.CodeStartOffsetBytes < (ulong)row.CodeSizeBytes)
        .Select(row => (object)new { row.MethodIdentity, NativeOffset = pc - image.ImageBase - (ulong)row.CodeStartOffsetBytes })
        .ToArray();

    private async Task WriteAsync()
    {
        try
        {
            using var telemetryFile = new FileStream(Path.Combine(directory, "telemetry.log"), FileMode.CreateNew);
            using var telemetry = new StreamWriter(telemetryFile);
            await foreach (object payload in queue.Reader.ReadAllAsync())
            {
                string path = Path.Combine(directory, $"checkpoint-{checkpoints.Count + 1:D4}.json");
                string sha = AtomicJson(path, payload);
                checkpoints.Add(new { Path = path, Sha256 = sha });
                var json = JsonSerializer.SerializeToElement(payload);
                string line = JsonSerializer.Serialize(new {
                    Utc = json.GetProperty("Utc"), Host = json.GetProperty("Host"), RunId = Trace.RunId,
                    SegmentId = json.GetProperty("SegmentId"), Cycles = json.GetProperty("CycleCount"),
                    Retired = json.GetProperty("PipelineInstructionsRetired"), IPC = json.GetProperty("PipelineIPC"),
                    Efficiency = json.GetProperty("PipelineEfficiency"), Method = json.GetProperty("LiveLocation"),
                    SnapshotPath = path, SnapshotSha256 = sha
                });
                telemetry.WriteLine(line); telemetry.Flush(); telemetryFile.Flush(true);
            }
        }
        catch (Exception error) { failure ??= error.ToString(); }
    }

    public void Complete(DoomGuestExecutionReport report)
    {
        queue.Writer.TryComplete();
        writer.GetAwaiter().GetResult();
        if (failure is not null || Trace.Dropped != 0 || report.Execution is null ||
            !report.Load.IsSuccess || checkpoints.Count == 0 ||
            report.Execution.ObservationDiagnostics is not { FailedSegments: 0, DroppedSegments: 0 } ||
            Trace.Snapshot().LastOrDefault()?.Kind != "Terminal")
            throw new InvalidDataException("Test-only evidence unavailable: " + (failure ?? "missing loader/terminal/checkpoints or diagnostic loss"));
        AtomicJson(Path.Combine(directory, "terminal.json"), new {
            Schema = "hybridcpu.test-initializer-terminal/v1", Trace.RunId, Utc = DateTimeOffset.UtcNow,
            Manifest = transport.Manifest, ManifestPath = manifestPath, ManifestSha256 = Hash(manifestPath),
            image.ImageBase, ImageSha256 = image.PackageSha256,
            LoadStatus = report.Load.Status.ToString(), report.ExitCode, report.StartedCpu,
            Transitions = Trace.Snapshot(), Trace.Dropped, Checkpoints = checkpoints,
            CheckpointPlan = plan,
            Execution = report.Execution,
            FinalLocation = Locate(report.Execution.FinalProgramCounter),
            LastRetiredLocation = Locate(report.Execution.LastRetiredBundlePc),
            PerMethodCount = (int?)null,
            PerMethodCountAvailability = "Unavailable: last-retire publication and bounded samples are not an exhaustive call profiler.",
            CycleMeaning = "Per-core pipeline cycles; each pending initializer has its own budget. Not retired instructions or a global total.",
            InitializerGcMeaning = "RequireGcSafepointEvidence=false in existing initializer path; zero is not proof of absent safepoints.",
            Qualification = "no"
        });
        Validate(directory, manifestPath);
    }

    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static CheckpointPlan CreatePlan(int segmentCount, int maximumCyclesPerSegment)
    {
        if (segmentCount is <= 0 or >= MaximumCheckpoints || maximumCyclesPerSegment <= 0)
            throw new InvalidDataException("Checkpoint coverage requires bounded positive segments/cycles within capacity.");
        int periodicSlots = MaximumCheckpoints - segmentCount;
        int perSegment = periodicSlots / segmentCount;
        if (perSegment <= 0) throw new InvalidDataException("No periodic checkpoint capacity remains.");
        int interval = checked((int)Math.Max(1L,
            ((long)maximumCyclesPerSegment + perSegment - 1L) / perSegment));
        int maximumPeriodic = checked((int)(((long)maximumCyclesPerSegment + interval - 1L) / interval));
        int maximumSnapshots = checked(segmentCount * (maximumPeriodic + 1));
        if (maximumSnapshots > MaximumCheckpoints)
            throw new InvalidDataException("Computed checkpoint plan exceeds its hard capacity.");
        return new(segmentCount, maximumCyclesPerSegment, interval, maximumPeriodic, maximumSnapshots);
    }
    internal static string AtomicJson(string path, object value)
    {
        using (var stream = new FileStream(path + ".pending", FileMode.CreateNew))
        { JsonSerializer.Serialize(stream, value); stream.Flush(true); }
        File.Move(path + ".pending", path);
        string hash = Hash(path);
        using (var stream = new FileStream(path + ".sha256", FileMode.CreateNew))
        using (var writer = new StreamWriter(stream)) { writer.Write(hash); writer.Flush(); stream.Flush(true); }
        return hash;
    }
    internal static void Validate(string directory, string manifestPath)
    {
        ObservationTransportProtocol.ValidateManifest(
            ObservationTransportProtocol.Decode<ObservationTransportManifest>(ObservationLocalAccess.ReadManifest(manifestPath)));
        string terminal = Path.Combine(directory, "terminal.json");
        if (File.ReadAllText(terminal + ".sha256") != Hash(terminal))
            throw new InvalidDataException("Terminal digest mismatch.");
        using var doc = JsonDocument.Parse(File.ReadAllBytes(terminal));
        var root = doc.RootElement;
        if (root.GetProperty("Schema").GetString() != "hybridcpu.test-initializer-terminal/v1" ||
            root.GetProperty("Qualification").GetString() != "no" ||
            root.GetProperty("LoadStatus").GetString() != "Success" ||
            !root.GetProperty("StartedCpu").GetBoolean() ||
            root.GetProperty("Dropped").GetInt64() != 0 ||
            root.GetProperty("Checkpoints").GetArrayLength() == 0 ||
            root.GetProperty("Transitions").GetArrayLength() == 0 ||
            root.GetProperty("Transitions").EnumerateArray().Last().GetProperty("Kind").GetString() != "Terminal")
            throw new InvalidDataException("Missing required terminal lifecycle.");
        using var started = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "started.json")));
        if (started.RootElement.GetProperty("RunId").GetGuid() != root.GetProperty("RunId").GetGuid())
            throw new InvalidDataException("Started/terminal RunId mismatch.");
        if (doc.RootElement.GetProperty("ManifestSha256").GetString() != Hash(manifestPath))
            throw new InvalidDataException("Manifest digest mismatch.");
        foreach (var checkpoint in doc.RootElement.GetProperty("Checkpoints").EnumerateArray())
        {
            string path = checkpoint.GetProperty("Path").GetString()!;
            if (checkpoint.GetProperty("Sha256").GetString() != Hash(path) ||
                File.ReadAllText(path + ".sha256") != Hash(path))
                throw new InvalidDataException("Checkpoint digest mismatch.");
        }
    }
    public void Dispose() { queue.Writer.TryComplete(); writer.GetAwaiter().GetResult(); }
}
