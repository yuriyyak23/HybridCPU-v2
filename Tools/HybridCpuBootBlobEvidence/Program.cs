using System.Security.Cryptography;
using System.Text.Json;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.NonRTL.Runtime;

if (args.Length == 2 && args[0] == "test-initializer-evidence") return InitializerEvidenceRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "test-launcher-create-image")
{
    Console.WriteLine(GcEvidenceModeRegression.CreateObservationFixture(args[1]));
    return 0;
}
if (args.Length == 3 && args[0] == "test-launcher-loader")
    return ObservationLoaderPipeRegression.Run(args[1], args[2]);
if (args.Length == 1 && args[0] == "test-observation") return OwnedCoreObservationRegression.Run();
if (args.Length == 2 && args[0] == "test-observation-event-pressure") return ObservationEventPressureRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "test-observation-event-pressure-client") return ObservationEventPressureRegression.Client(args[1]).GetAwaiter().GetResult();
if (args.Length == 2 && args[0] == "test-observation-late-cancel") return ObservationLateCancelRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "test-observation-late-cancel-client") return ObservationLateCancelRegression.Client(args[1]).GetAwaiter().GetResult();
if (args.Length == 2 && args[0] == "test-observation-read-once") return ObservationReadOnceRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "observe-once") return ObservationReadOnce.Run(args[1]).GetAwaiter().GetResult();
if (args.Length == 3 && args[0] == "observe-once") return ObservationReadOnce.Run(args[1], args[2]).GetAwaiter().GetResult();
if (args.Length == 3 && args[0] == "compare-observations") return ObservationProgressComparison.Run(args[1], args[2]);
if (args.Length == 4 && args[0] == "test-frame-loader" && int.TryParse(args[3], out int frameBudget) && frameBudget > 0)
    return FrameLoaderHostRegression.Run(args[1], args[2], frameBudget);
if (args.Length == 2 && args[0] == "test-observation-pending") return ObservationPendingRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "test-observation-pending-client") return ObservationPendingRegression.Client(args[1]).GetAwaiter().GetResult();
if (args.Length == 2 && args[0] == "test-observation-production-loader") return ObservationProductionLoaderRegression.Run(args[1]);
if (args.Length == 2 && args[0] == "test-observation-production-client") return ObservationProductionLoaderRegression.Client(args[1]).GetAwaiter().GetResult();
if (args.Length == 2 && args[0] == "test-observation-manifest") return ObservationManifestRegression.Run(args[1]).GetAwaiter().GetResult();
if (args.Length == 2 && args[0] == "test-observation-manifest-client") return ObservationManifestRegression.Client(args[1]).GetAwaiter().GetResult();
if (args.Length == 1 && args[0] == "test-observation-transport") return ObservationTransportRegression.Run();
if (args.Length == 2 && args[0] == "test-observation-loader-pipe") return ObservationLoaderPipeRegression.Run(args[1]);
if (args.Length == 3 && args[0] == "test-observation-loader-client") return ObservationLoaderPipeRegression.ClientAsync(args[1], args[2]).GetAwaiter().GetResult();
if (args.Length == 1 && args[0] == "test-observation-external-pipe") return ObservationExternalPipeRegression.RunAsync().GetAwaiter().GetResult();
if (args.Length == 3 && args[0] == "test-observation-pipe-client") return ObservationExternalPipeRegression.ClientAsync(args[1], args[2]).GetAwaiter().GetResult();
if (args.Length == 1 && args[0] == "test-observation-pipe") return ObservationPipeRegression.RunAsync().GetAwaiter().GetResult();
if (args.Length == 1 && args[0] == "test-observation-handshake") return ObservationHandshakeRegression.Run();
if (args.Length == 1 && args[0] == "test-observation-queue") return ObservationQueueRegression.Run();
if (args.Length == 1 && args[0] == "test-observation-frames") return ObservationFrameRegression.RunAsync().GetAwaiter().GetResult();

string? initializerEvidenceDirectory = null;
if (args.Length >= 6 && args[0] == "run-gc" && args[^2] == "--test-initializer-evidence")
{
    initializerEvidenceDirectory = Path.GetFullPath(args[^1]);
    args = args[..^2];
}
string? observationManifest = null;
string? frameDirectory = null;
if (args.Length == 8 && args[0] == "run-gc" && args[6] == "--frame-directory")
{
    frameDirectory = Path.GetFullPath(args[7]);
    if (Directory.Exists(frameDirectory)) throw new IOException("A new frame evidence directory is required.");
    Directory.CreateDirectory(frameDirectory);
    args = args[..6];
}
if (args.Length == 6 && args[0] == "run-gc" && args[4] == "--observation-manifest")
{
    observationManifest = Path.GetFullPath(args[5]);
    args = args[..4];
}

if (args.Length != 4 || args[0] is not ("test" or "test-bridge" or "test-clock" or "test-concat" or "test-console" or "test-gc" or "test-gc-callers" or "test-gc-cache" or "test-gc-scan" or "test-empty" or "run" or "run-gc" or "inspect" or "inspect-gc" or "benchmark-gc" or "benchmark-gc-collect") || !int.TryParse(args[3], out int budget) || budget <= 0)
    throw new ArgumentException("Expected test|run|run-gc image.hcexe wad-path positive-cycle-budget.");
if (args[0] == "test-gc") return GcEvidenceModeRegression.Run(args[2]);
var image = new HybridCpuRestrictedImageBuilderV1().Inspect(File.ReadAllBytes(args[1]));
if (image.Status != HybridCpuStartupStatusV1.Success || image.RuntimeBootstrap is null)
    throw new InvalidDataException("Image inspection failed.");
if (args[0] == "inspect")
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        image.ImageBase, image.EntryAddress, image.PackageSha256,
        Types = image.RuntimeBootstrap.ManagedTypes!.Where(row => row.StableIdentity.Contains(args[2], StringComparison.Ordinal)),
        Methods = image.RuntimeBootstrap.CodeManagerRecords.Where(row => row.MethodIdentity.Contains(args[2], StringComparison.Ordinal)),
        image.RuntimeBootstrap.ModuleInitializers
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (args[0] is "run" or "run-gc")
{
    bool requireGcSafepointEvidence = args[0] == "run-gc";
    using var transport = observationManifest is null ? null : new ObservationHostTransport(observationManifest);
    if (initializerEvidenceDirectory is not null && transport is null)
        throw new InvalidDataException("Test initializer evidence requires a fresh host observation manifest.");
    using var initializerEvidence = initializerEvidenceDirectory is null ? null :
        new InitializerEvidence(initializerEvidenceDirectory, observationManifest!, transport!, image, budget);
    DoomGuestExecutionReport report;
    string? transportCloseFailure = null;
    try
    {
        report = new DoomGuestExecutionService {
            TestOnlyInitializerTrace = initializerEvidence?.Trace,
            ObservationAttach = initializerEvidence is not null ? initializerEvidence.Attach : transport is null ? null : transport.Attach,
            FramePresented = frameDirectory is null ? null : frame =>
            {
                // Bounded explicit evidence: first 16 presented frames, never arbitrary RAM.
                if (frame.Sequence > 16) return;
                string path = Path.Combine(frameDirectory, $"frame-{frame.Sequence:D4}.bmp");
                DoomFramebufferExport.SaveBmp(frame, path);
                using var output = new FileStream(path + ".json", FileMode.CreateNew);
                JsonSerializer.Serialize(output, new { frame.ContextId, frame.Sequence, frame.Width, frame.Height,
                    frame.PixelSha256, frame.PaletteSha256, frame.CapturedAtUtc,
                    BmpSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), Qualified = false });
                output.Flush(true);
            } }
            .Execute(args[1], args[2], budget, requireGcSafepointEvidence);
    }
    finally
    {
        if (transport is not null)
        {
            transport.Dispose();
            try { transport.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
            catch (Exception error) { transportCloseFailure = error.Message; }
        }
    }
    var execution = report.Execution;
    initializerEvidence?.Complete(report);
    var bridge = report.Load.EcallBridge as HybridCpuIseManagedEcallBridgeV1;
    var allocations = report.Load.Heap?.ActiveAllocations().ToDictionary(row => row.ObjectAddress);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Image = Path.GetFullPath(args[1]), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))),
        Wad = ImageAdmission.InspectWad(args[2]), Qualified = false, Budget = budget,
        RequireGcSafepointEvidence = requireGcSafepointEvidence,
        ObservationManifest = observationManifest, ObservationTransport = transport?.Report,
        ObservationTransportCloseFailure = transportCloseFailure,
        report.ExitCode, report.StartedCpu, LoadStatus = report.Load.Status.ToString(), report.Load.ProcessRoots,
        Status = execution?.Status.ToString(), Execution = execution,
        report.ConsoleOutput,
        GuestStaticRoots = report.Load.TypeSystem?.Descriptors
            .Where(type => type.StableIdentity.StartsWith("DoomSharp.HybridCpu.Guest.", StringComparison.Ordinal))
            .SelectMany(type => type.StaticLayout.ObjectReferenceOffsets.Select(offset =>
            {
                ulong reference = BitConverter.ToUInt64(report.Load.TypeSystem.StaticStorage(type.TypeId)!, offset);
                return new { Owner = type.StableIdentity, Offset = offset, Reference = reference,
                    Type = allocations!.TryGetValue(reference, out var allocation)
                        ? report.Load.TypeSystem.Resolve(allocation.TypeId)!.StableIdentity : null };
            })),
        ContainingCode = image.RuntimeBootstrap.CodeManagerRecords.Where(row => execution is not null &&
            execution.FinalProgramCounter >= image.ImageBase + (ulong)row.CodeStartOffsetBytes &&
            execution.FinalProgramCounter < image.ImageBase + (ulong)row.CodeStartOffsetBytes + (ulong)row.CodeSizeBytes),
        ManagedDiagnostics = bridge is null ? null : new
        {
            bridge.LastRejectedReason, bridge.LastManagedOperation, bridge.LastManagedReason,
            bridge.LastBootBlobObservation,
            bridge.LastClockReadObservation,
            bridge.LastConsoleObservation,
            bridge.LastManagedStatus, bridge.LastManagedExternalStatus, bridge.LastManagedExternalError,
            bridge.LastManagedReceiver, bridge.LastManagedArgument1, bridge.LastManagedArgument2,
            bridge.ManagedObservations,
            AllocationTypes = bridge.ManagedObservations.SelectMany(row => new[] { row.Receiver, row.Value })
                .Distinct().Select(reference => allocations!.TryGetValue(reference, out var allocation)
                    ? new { Reference = reference, allocation!.TypeId,
                        Type = report.Load.TypeSystem!.Resolve(allocation.TypeId)!.StableIdentity }
                    : null).Where(row => row is not null)
        },
        report.Detail
    }, new JsonSerializerOptions { WriteIndented = true }));
    return report.ExitCode;
}

int checks = 0;
void Check(bool condition, string detail)
{ if (!condition) throw new Exception(detail); checks++; Console.WriteLine("PASS " + detail); }
var provider = new HybridCpuIseBootBlobBindingV1();
var memory = new HybridCpuIseSparseMainMemoryAreaV1();
var console = new HybridCpuIseConsoleProviderV1((address, count) =>
{
    byte[] bytes = new byte[count];
    return memory.TryReadPhysicalRange(address, bytes) ? bytes : null;
});
var kernel = new DeterministicRuntimeKernelV1(hostServiceProvider: console, managedBootBlobProvider: provider);
var helpers = image.RuntimeBootstrap.RuntimeHelpers.ToDictionary(row => row.Symbol,
    _ => (HybridCpuRuntimeHelperEntryV1)(_ => true), StringComparer.Ordinal);
var load = new HybridCpuIseManagedImageLoaderV1().Load(new(image.ImageBytes, image.ImageBase,
    image.EntryAddress, image.PackageSha256, image.RuntimeBootstrap,
    HybridCpuManagedHeapOptionsV1.Create(0x28000000, 128UL * 1024 * 1024,
        HybridCpuBootBlobServiceContractV1.MaximumBlobBytes + 4096, -3),
    HybridCpuManagedAbiFamilyV1.Default.TargetContractDigest, HybridCpuManagedAbiFamilyV1.Default.NativeAbiDigest,
    HybridCpuManagedAbiFamilyV1.RuntimePackRevision), memory, kernel, helpers);
Check(load.IsSuccess, "production loader accepts inspected image bytes: " + load.Reason);
if (args[0] == "benchmark-gc")
{
    var bank = new ulong[64];
    var timer = System.Diagnostics.Stopwatch.StartNew();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    for (int iteration = 0; iteration < Math.Min(budget, 10000); iteration++)
        if (load.Gc!.CollectRetiredSafepoint(load.StackMaps!, int.MaxValue, bank, 0,
                (_, _) => null, [], load.Strings).Status != HybridCpuManagedRetiredSafepointStatusV1.NotSafepoint)
            throw new InvalidOperationException("Non-safepoint probe unexpectedly admitted or rejected metadata.");
    Console.WriteLine(JsonSerializer.Serialize(new { Iterations = Math.Min(budget, 10000),
        Methods = load.StackMaps!.Count, Milliseconds = timer.Elapsed.TotalMilliseconds,
        AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
        CpuExecution = false, Qualified = false }));
    return 0;
}
if (args[0] == "inspect-gc")
{
    Console.WriteLine(JsonSerializer.Serialize(load.StackMaps!.Where(row => row.MethodIdentity.Contains(args[2], StringComparison.Ordinal))
        .Select(row => new { row.MethodIdentity, GcInfo = Convert.ToHexString(row.GcInfo),
            CodeManagerMetadata = Convert.ToHexString(row.CodeManagerMetadata),
            Unwind = row.UnwindInfo is null ? null : HybridCpuManagedUnwindCodecV2.Decode(row.UnwindInfo) }),
        new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
HybridCpuExternalServiceResultV1 Lookup(int id)
{
    var draft = new HybridCpuExternalServiceRequestV1(load.Context!.ContextId, HybridCpuHostServiceV1.File,
        HybridCpuBootBlobServiceContractV1.GetOperation, HybridCpuPrivilegeModeV1.User, 0, 0,
        HybridCpuHostBufferAccessV1.None, [(ulong)id], string.Empty);
    return kernel.ExternalServiceTransition(draft with { TransitionDigest = HybridCpuManagedInteropContractV1.ComputeTransitionDigest(draft) });
}
Check(Lookup(1).Status == HybridCpuExternalServiceStatusV1.ProviderFailure, "unbound BLOB lookup fails closed (pre-patch gap)");
int originalCount = load.Heap!.ActiveAllocations().Count;
Check(!new HybridCpuIseBootBlobBindingV1().TryBind(load, 0, [1], out _, out _), "invalid blob id rejected");
Check(!new HybridCpuIseBootBlobBindingV1().TryBind(load, 1, [], out _, out _), "empty blob rejected");
Check(!new HybridCpuIseBootBlobBindingV1().TryBind(load with { TypeSystem = null }, 1, [1], out _, out _), "missing runtime type authority rejected");
Check(load.Heap.ActiveAllocations().Count == originalCount, "rejected preconditions allocate nothing");
byte[] wad = File.ReadAllBytes(args[2]);
Check(provider.TryBind(load, 1, wad, out var bound, out string reason), "materialize real WAD: " + reason);
var blob = provider.Resolve(1);
Check(blob.Success && blob.Length == wad.Length && blob.ManagedReference >= load.Heap.BaseAddress, "provider returns guest heap reference and exact length");
var allocation = load.Heap.ActiveAllocations().Single(row => row.ObjectAddress == blob.ManagedReference);
var type = load.TypeSystem!.Resolve(allocation.TypeId)!;
Check(type.StableIdentity == "System.Byte[]", "exact image byte-array descriptor, no hardcoded handle");
byte[] copied = new byte[wad.Length];
Check(memory.TryReadPhysicalRange(blob.ManagedReference + (ulong)type.ArrayShape!.DataOffsetBytes, copied) && copied.SequenceEqual(wad), "physical ISE heap contains exact WAD bytes");
wad[0] ^= 255;
Check(load.Heap.TryReadObjectBytes(blob.ManagedReference, type.ArrayShape.DataOffsetBytes, copied) && copied[0] != wad[0], "host buffer mutation does not alias guest array");
Check(bound.ProcessRoots is { Count: 1 } && bound.ProcessRoots[0].ObjectReference == blob.ManagedReference &&
    bound.ProcessRoots[0].Source == HybridCpuManagedGcRootSourceV1.Handle, "process-lifetime handle root accompanies load result");
Check(Lookup(1).Status == HybridCpuExternalServiceStatusV1.Success, "existing kernel BLOB transition resolves materialized array (component, not CPU ECALL)");
if (args[0] == "benchmark-gc-collect")
{
    var timer = System.Diagnostics.Stopwatch.StartNew();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    for (int iteration = 0; iteration < Math.Min(budget, 100); iteration++)
    {
        var result = load.Gc!.Collect(new([], [], bound.ProcessRoots!, load.Strings),
            HybridCpuManagedNonMovingGcOptionsV1.Qualification);
        if (!result.IsSuccess || !result.ReachableObjects.Contains(blob.ManagedReference))
            throw new InvalidOperationException(result.Reason);
    }
    Console.WriteLine(JsonSerializer.Serialize(new { Iterations = Math.Min(budget, 100), WadBytes = blob.Length,
        Milliseconds = timer.Elapsed.TotalMilliseconds, AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated,
        CpuExecution = false, Qualified = false }));
    return 0;
}
if (args[0] == "test-bridge") BootBlobBridgeRegression.Verify(bound, blob.ManagedReference, Check);
if (args[0] == "test-clock") ClockReadBridgeRegression.Verify(bound, kernel, Check);
if (args[0] == "test-concat") StringConcatLoaderRegression.Verify(bound, blob.ManagedReference, Check);
if (args[0] == "test-console") ConsoleBridgeRegression.Verify(bound, kernel, console, Check);
if (args[0] == "test-empty") StringEmptyLoaderRegression.Verify(bound, Check);
if (args[0] == "test-gc-callers") GcCallerRootRegression.Verify(bound, blob.ManagedReference, Check);
if (args[0] == "test-gc-cache") GcMetadataCacheRegression.Verify(bound, Check);
if (args[0] == "test-gc-scan") GcPointerlessScanRegression.Verify(bound, blob.ManagedReference, Check);
Check(!provider.TryBind(bound, 1, [1], out _, out _) && provider.Resolve(1) == blob, "rebind rejected without changing published reference");
Check(!provider.Resolve(2).Success, "unknown blob remains fail-closed");
for (int i = 0; i < 2; i++)
{
    var garbage = new HybridCpuManagedArrayRuntimeV1(load.TypeSystem, load.Heap).NewArray(load.TypeSystem.TypeHandle(type.TypeId)!.Value, 512);
    var gc = load.Gc!.Collect(new([], [], bound.ProcessRoots!, load.Strings), HybridCpuManagedNonMovingGcOptionsV1.Qualification);
    Check(gc.IsSuccess && gc.ReachableObjects.Contains(blob.ManagedReference) && gc.ReclaimedObjects.Contains(garbage.ObjectReference), "explicit-root collection retains WAD and reclaims garbage, pass " + i);
}
var omitted = load.Gc!.Collect(new([], [], [], load.Strings), HybridCpuManagedNonMovingGcOptionsV1.Qualification);
Check(omitted.IsSuccess && omitted.ReclaimedObjects.Contains(blob.ManagedReference), "negative control: omitted process roots reproduces WAD reclamation");
Console.WriteLine($"PASS {checks} checks; loader/GC/kernel component evidence only, no guest CPU execution.");
return 0;
