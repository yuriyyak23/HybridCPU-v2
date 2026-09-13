using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target;
using HybridCPU.Compiler.Core.Target.Link;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Object;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;

// Synthetic HCEXEs verify Windows service wiring through the real ISE pipeline.
// These are not Doom execution or publication qualification evidence.
public static class GcEvidenceModeRegression
{
    private static int checks;
    private sealed class Observer(HybridCPU_ISE.NonRTL.Runtime.OwnedCoreObservationBinding binding)
        : HybridCPU_ISE.NonRTL.Runtime.IOwnedCoreObservationConsumer
    {
        public int Captures;
        public bool Closed;
        public bool FailCapture;
        public bool FailClose;
        public HybridCPU_ISE.NonRTL.Runtime.OwnedCoreObservationBinding Binding => binding;
        public void OnBoundary()
        {
            _ = binding.Observer.GetCoreState(0); Captures++;
            if (FailCapture) throw new InvalidOperationException("fixture observer failure");
        }
        public void Dispose() { Closed = true; if (FailClose) throw new InvalidOperationException("fixture close failure"); }
    }
    public static int Run(string outputDirectory)
    {
        checks = 0;
        try
        {
            MethodInfo? strict = typeof(DoomGuestExecutionService).GetMethod("Execute",
                [typeof(string), typeof(int), typeof(bool)]);
            Check(strict is not null, "Windows execution service cannot request retired GC evidence.");
            Directory.CreateDirectory(outputDirectory);
            string withGc = Save(outputDirectory, "with-gc.hcexe", Build(true));
            string withoutGc = Save(outputDirectory, "without-gc.hcexe", Build(false));
            var service = new DoomGuestExecutionService();
            DoomGuestExecutionReport Execute(string path, bool required) =>
                (DoomGuestExecutionReport)strict!.Invoke(service, [path, 4096, required])!;
            var positive = Execute(withGc, true);
            Check(positive.ExitCode == 0, positive.Detail);
            Check(positive.Execution!.GcSafepointsObserved > 0, "Strict mode did not collect a retired safepoint.");
            Check(positive.Execution.GcResultDigests is { Count: > 0 }, "Missing collection digest.");
            Check(positive.Execution.ProcessExitCode == 0, "Incorrect fixture exit.");
            Check(positive.Execution.LastRetireSequence > 0 && positive.Execution.LastRetiredBundlePc != 0,
                "Successful sentinel result lost its retired identity.");
            Observer? observer = null;
            var observedService = new DoomGuestExecutionService
            {
                ObservationAttach = binding => observer = new Observer(binding)
            };
            var observed = observedService.Execute(withGc, 4096, true);
            Check(observer is { Captures: > 0, Closed: true }, "Runner observation lifecycle missing.");
            Check(JsonSerializer.Serialize(observed.Execution! with { ObservationDiagnostics = null }) == JsonSerializer.Serialize(positive.Execution),
                "Observation changed loader-backed execution outcome or GC evidence.");
            var bounded = (DoomGuestExecutionReport)strict!.Invoke(service,
                [withGc, checked((int)positive.Execution.RetiredPipelineCycles - 1), true])!;
            Check(bounded.Execution!.Status.ToString() == "CycleBudgetExceeded", "Fixture did not exhaust its shorter CPU budget.");
            Check(bounded.Execution.GcSafepointsObserved > 0 &&
                bounded.Execution.GcResultDigests?.Count == bounded.Execution.GcSafepointsObserved,
                "CycleBudgetExceeded dropped already collected GC safepoints/digests.");
            var observedBudget = observedService.Execute(withGc, positive.Execution.RetiredPipelineCycles - 1, true);
            Check(observer is { Captures: > 0, Closed: true }, "Budget exit left observer open.");
            Check(JsonSerializer.Serialize(observedBudget.Execution! with { ObservationDiagnostics = null }) == JsonSerializer.Serialize(bounded.Execution),
                "Observation changed budget outcome.");
            bool staleRejected = false;
            try { observer!.Binding.Observer.GetCoreState(0); }
            catch (ObjectDisposedException) { staleRejected = true; }
            Check(staleRejected, "Budget exit left stale core observable.");
            Observer? faultedObserver = null;
            var faultedService = new DoomGuestExecutionService
            {
                ObservationAttach = binding => faultedObserver = new Observer(binding) { FailCapture = true }
            };
            var withObserverFault = faultedService.Execute(withGc, 4096, true);
            Check(faultedObserver is { Captures: 1, Closed: true }, "Faulted observer was not detached exactly once.");
            Check(JsonSerializer.Serialize(withObserverFault.Execution! with { ObservationDiagnostics = null }) == JsonSerializer.Serialize(positive.Execution),
                "Diagnostic failure changed guest outcome.");
            using (var terminal = JsonDocument.Parse(JsonSerializer.Serialize(withObserverFault.Execution)))
                Check(terminal.RootElement.TryGetProperty("ObservationDiagnostics", out var diagnostics) &&
                    diagnostics.ToString().Contains("fixture observer failure", StringComparison.Ordinal),
                    "Terminal runner evidence lost the contained observer failure.");
            Check(withObserverFault.Execution!.ObservationDiagnostics is { FailedSegments: 1, DroppedSegments: 0 } &&
                withObserverFault.Execution.ObservationDiagnostics.Segments.Single() is
                    { Closed: true, BoundariesObserved: 1, Failure: "fixture observer failure" },
                "Fault summary has incorrect lifecycle/counts.");
            var attachFault = new DoomGuestExecutionService
            {
                ObservationAttach = _ => throw new InvalidOperationException("fixture attach failure")
            }.Execute(withGc, 4096, true);
            Check(attachFault.Execution!.ObservationDiagnostics!.Segments.Single() is
                { Failure: "fixture attach failure", Closed: true, BoundariesObserved: 0 }, "Attach failure not reported.");
            var closeFault = new DoomGuestExecutionService
            {
                ObservationAttach = binding => new Observer(binding) { FailClose = true }
            }.Execute(withGc, 4096, true);
            Check(closeFault.Execution!.ObservationDiagnostics!.Segments.Single() is
                { Failure: "fixture close failure", Closed: true, BoundariesObserved: > 0 }, "Dispose failure not reported.");
            Check(JsonSerializer.Serialize(attachFault.Execution with { ObservationDiagnostics = null }) == JsonSerializer.Serialize(positive.Execution) &&
                JsonSerializer.Serialize(closeFault.Execution with { ObservationDiagnostics = null }) == JsonSerializer.Serialize(positive.Execution),
                "Attach/dispose diagnostic failure changed execution.");
            string initializedImage = Save(outputDirectory, "initializers.hcexe", Build(true, 2));
            var initializers = new List<Observer>();
            var initializedBaseline = service.Execute(initializedImage, 4096, true);
            var initialized = new DoomGuestExecutionService
            {
                ObservationAttach = binding =>
                {
                    foreach (var prior in initializers)
                    {
                        bool rejected = false;
                        try { prior.Binding.Observer.GetCoreState(0); }
                        catch (ObjectDisposedException) { rejected = true; }
                        Check(rejected, "Previous initializer source remains available at next segment attach.");
                    }
                    var next = new Observer(binding) { FailCapture = initializers.Count == 0 };
                    initializers.Add(next);
                    return next;
                }
            }.Execute(initializedImage, 4096, true);
            var segments = initialized.Execution!.ObservationDiagnostics!;
            Check(initializedBaseline.ExitCode == 0 && initialized.ExitCode == 0 &&
                JsonSerializer.Serialize(initialized.Execution with { ObservationDiagnostics = null }) ==
                JsonSerializer.Serialize(initializedBaseline.Execution), "Initializer observation changed execution.");
            Check(segments.SegmentsObserved == 3 && segments.FailedSegments == 1 && segments.DroppedSegments == 0 &&
                segments.Segments.Select(row => row.SegmentKind).SequenceEqual(new[] { "Initializer", "Initializer", "Main" }) &&
                segments.Segments.Select(row => row.SegmentId).Distinct().Count() == 3 &&
                segments.Segments.Select(row => row.EntryAddress).Distinct().Count() == 3 &&
                segments.Segments.All(row => row.Closed), "Initializer/main identity or diagnostic aggregation lost.");
            Check(initializers.Select(row => row.Binding.SegmentId).SequenceEqual(segments.Segments.Select(row => row.SegmentId)),
                "Binding and terminal segment identities differ.");
            string overflowImage = Save(outputDirectory, "ledger-overflow.hcexe", Build(true, 256));
            var overflow = new DoomGuestExecutionService
            {
                ObservationAttach = _ => throw new InvalidOperationException(new string('x', 3000))
            }.Execute(overflowImage, 4096, true);
            Check(overflow.ExitCode == 0 && overflow.Execution!.ObservationDiagnostics is
                { SegmentsObserved: 257, FailedSegments: 257, DroppedSegments: 1, Segments.Count: 256 },
                "Diagnostic ledger bounds/loss counts not preserved across initializer segments.");
            Check(overflow.Execution!.ObservationDiagnostics!.Segments.All(row =>
                row.FailureTruncated && row.Failure!.Length == 2048 && row.Closed), "Failure text bounds not explicit.");
            using (var reportFile = new FileStream(Path.Combine(outputDirectory, "terminal-evidence.json"), FileMode.CreateNew))
                JsonSerializer.Serialize(reportFile, new { positive.Execution, Observed = observed.Execution,
                    Budget = observedBudget.Execution, BoundaryFault = withObserverFault.Execution,
                    AttachFault = attachFault.Execution, CloseFault = closeFault.Execution,
                    Initializers = initialized.Execution, Overflow = overflow.Execution }, new JsonSerializerOptions { WriteIndented = true });
            var negative = Execute(withoutGc, true);
            Check(negative.ExitCode == 6 && negative.Execution is { IsSuccess: false },
                "Strict mode admitted exit with no safepoint.");
            Check(negative.Execution!.Reason.Contains("without one exact retired GC safepoint", StringComparison.Ordinal),
                negative.Detail);
            var diagnostic = Execute(withGc, false);
            Check(diagnostic.ExitCode == 0 && diagnostic.Execution!.GcSafepointsObserved == 0,
                "Diagnostic mode behavior changed.");
            Console.WriteLine($"PASS GC evidence service regression: {checks} checks; normal/budget/attach/boundary/close-fault equivalence, initializer/main identities, bounded terminal ledger/loss; synthetic loader-backed CPU fixtures, not Doom qualification.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.GetBaseException().Message);
            return 1;
        }
    }

    private static string Save(string directory, string name, HybridCpuRestrictedImageV1 image)
    {
        string path = Path.Combine(directory, name);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        stream.Write(image.PackageBytes);
        return path;
    }

    internal static string CreateObservationFixture(string directory, int bodyBundles = 1024, bool typedInitializer = false)
    {
        Directory.CreateDirectory(directory);
        return Save(directory, "observation-lifecycle.hcexe", Build(true, typedInitializer ? 1 : 2, bodyBundles, typedInitializer));
    }

    private static HybridCpuRestrictedImageV1 Build(bool safepoint, int initializerCount = 0, int bodyBundles = 1, bool typedInitializer = false)
    {
        const string entry = "gc_service_fixture";
        var types = new HybridCpuManagedTypeSystemBuilderV1().Build(
            [new("Gc.ServiceFixture", HybridCpuManagedTypeKindV1.Class, null, [], [])]);
        Check(types.IsSuccess, types.Reason);
        var type = types.TypeSystem!.Descriptors.Single();
        var metadata = new HybridCpuManagedTypeMetadataEncoderV1().Encode([type]);
        var gc = HybridCpuManagedAbiEncodingV1.EncodeGcInfo(safepoint
            ? [new(0, HybridCpuSafepointCategoryV1.CallSite, [])] : []);
        Check(gc.Status == HybridCpuPlatformFactStatus.Supported, "GC fixture encoding failed.");
        byte[] unwind = HybridCpuManagedUnwindCodecV2.Encode(new(
            HybridCpuManagedFrameKindV1.Managed, HybridCpuManagedCfaBaseV1.StackPointer, 0,
            HybridCpuNativeAbiContractV2.ReturnAddressRegister, null, []));
        HybridCpuInstructionWord Word(HybridCpuOpcode opcode, byte destination, byte source) => new()
        {
            OpCode = (uint)opcode, DataTypeValue = HybridCpuDataType.INT64, PredicateMask = byte.MaxValue,
            Word1 = HybridCpuInstructionWord.PackArchRegs(destination, source, HybridCpuInstructionWord.NoArchReg),
            VirtualThreadId = 0
        };
        var first = new HybridCpuInstructionBundle();
        first.SetInstruction(0, Word(HybridCpuOpcode.ADDI, 10, 0));
        var second = new HybridCpuInstructionBundle();
        second.SetInstruction(0, Word(HybridCpuOpcode.JALR, 0, (byte)HybridCpuNativeAbiContractV2.ReturnAddressRegister));
        byte[] code = new HybridCpuBundleSerializer().SerializeProgram([
            .. Enumerable.Repeat(first, bodyBundles), second]);
        byte[] text = Enumerable.Range(0, initializerCount + 1).SelectMany(_ => code).ToArray();
        var initializerSymbols = Enumerable.Range(0, initializerCount).Select(index => new HybridCpuObjectSymbolV1(
            "__hybridcpu_static_init_fixture_" + index, HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Default,
            ".text", (ulong)((index + 1) * code.Length), (ulong)code.Length, true)).ToArray();
        var obj = new HybridCpuObjectWriterV1().Write(new(
            [new(".text", HybridCpuObjectSectionKind.Code, HybridCpuBundleSerializer.BundleSizeBytes, text, (ulong)text.Length),
             new(".hcgc", HybridCpuObjectSectionKind.ReadOnlyData, 8, gc.Bytes, (ulong)gc.Bytes.Length),
             new(".hcunwind", HybridCpuObjectSectionKind.Unwind, 8, unwind, (ulong)unwind.Length),
             new(".hctypes", HybridCpuObjectSectionKind.ReadOnlyData, 8, metadata.Bytes, (ulong)metadata.Bytes.Length)],
            [new(entry, HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Default, ".text", 0, (ulong)code.Length, true),
             new("gc", HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Hidden, ".hcgc", 0, (ulong)gc.Bytes.Length, true),
             new("unwind", HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Hidden, ".hcunwind", 0, (ulong)unwind.Length, true),
             new("types", HybridCpuSymbolBinding.Global, HybridCpuSymbolVisibility.Hidden, ".hctypes", 0, (ulong)metadata.Bytes.Length, true),
             .. initializerSymbols],
            [], HybridCpuTargetPlatformContractV1.Default.ContractDigest, HybridCpuManagedAbiFamilyV1.Default.ContractDigest));
        Check(obj.Status == HybridCpuObjectStatusV1.Success, "Fixture object rejected.");
        var link = new HybridCpuStaticLinkerV1().Link([new("gc-service-fixture", obj.Bytes)]);
        Check(link.Status == HybridCpuLinkStatusV1.Success, "Fixture link rejected.");
        var codeSymbol = link.Symbols.Single(row => row.Name == entry);
        var typeSymbol = link.Symbols.Single(row => row.Name == "types");
        string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var record = new HybridCpuCodeManagerRegistrationV1(entry,
            checked((int)(codeSymbol.Address - link.ImageBase)), checked((int)codeSymbol.Size), Hash(gc.Bytes), Hash(unwind));
        var bootstrap = HybridCpuImageRuntimeBootstrapContractV1.Create(
            HybridCpuManagedAbiFamilyV1.Default.ContractDigest, entry, entry, [], [record,
                .. initializerSymbols.Select(symbol => new HybridCpuCodeManagerRegistrationV1(symbol.Name,
                    checked((int)(link.Symbols.Single(row => row.Name == symbol.Name).Address - link.ImageBase)),
                    code.Length, Hash(gc.Bytes), Hash(unwind)))],
            moduleInitializers: initializerSymbols.Select((symbol, index) =>
                new HybridCpuModuleInitializerRegistrationV1("fixture_module_" + index, symbol.Name, index,
                    typedInitializer ? type.TypeId : null)),
            managedTypes: [new(type.TypeId, type.StableIdentity, type.DescriptorDigest,
                checked((int)(typeSymbol.Address - link.ImageBase)), checked((int)typeSymbol.Size), null, TypeHandle: 1)]);
        var image = new HybridCpuRestrictedImageBuilderV1().Build(new(link, entry, RuntimeBootstrap: bootstrap));
        Check(image.Status == HybridCpuStartupStatusV1.Success, "Fixture image rejected: " + JsonSerializer.Serialize(image.Diagnostics));
        return image;
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
        checks++;
    }
}
