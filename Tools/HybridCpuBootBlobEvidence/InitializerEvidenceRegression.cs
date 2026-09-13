using System.Text.Json;
using DoomSharp.HybridCpu.Windows;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.NonRTL.Runtime;

internal static class InitializerEvidenceRegression
{
    public static int Run(string directory)
    {
        int checks = 0;
        void Check(bool value, string reason)
        { if (!value) throw new InvalidDataException(reason); checks++; Console.WriteLine("PASS " + reason); }
        void Reject(Action action, string reason)
        {
            bool rejected = false;
            try { action(); } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { rejected = true; }
            Check(rejected, reason);
        }
        string path = GcEvidenceModeRegression.CreateObservationFixture(Path.Combine(directory, "fixture"), 32, true);
        var image = new HybridCpuRestrictedImageBuilderV1().Inspect(File.ReadAllBytes(path));
        var untouched = new TestOnlyInitializerTrace();
        var baseline = new DoomGuestExecutionService().Execute(path, 4096, true);
        Check(baseline.ExitCode == 0 && baseline.Execution!.ObservationDiagnostics is null &&
            untouched.Snapshot().Count == 0, "default path publishes no test-only events");
        string manifest = Path.Combine(directory, "manifest.json");
        using var host = new ObservationHostTransport(manifest);
        string evidence = Path.Combine(directory, "evidence");
        using var observed = new InitializerEvidence(evidence, manifest, host, image, 4096);
        var result = new DoomGuestExecutionService {
            TestOnlyInitializerTrace = observed.Trace, ObservationAttach = observed.Attach
        }.Execute(path, 4096, true);
        observed.Complete(result);
        Check(JsonSerializer.Serialize(result.Execution! with { ObservationDiagnostics = null }) ==
            JsonSerializer.Serialize(baseline.Execution), "opt-in execution/GC/retired identity equals baseline");
        var events = observed.Trace.Snapshot();
        Check(events.Select(e => e.Kind).SequenceEqual(new[] { "Begin", "Finalizer", "Terminal" }),
            "begin/finalizer/terminal order is exact");
        var final = events[1];
        Check(events[0].BeginState == "Uninitialized" && events[0].CompletionState == "Running" &&
            final.CompletionState == "Initialized" && final.Success == true && final.CompletionAccepted &&
            final.Initializer!.TypeId.HasValue && final.Initializer.TypeHandle == 1, "exact TypeId/handle state transitions");
        var segment = result.Execution!.ObservationDiagnostics!.Segments.Single(s => s.SegmentKind == "Initializer");
        Check(final.PipelineCycleDelta > 0 && final.PipelineCycleDelta == segment.BoundariesObserved &&
            final.RetireSequence > 0 && final.LastRetiredPc != 0, "initializer cycle delta equals actual owner cycle boundaries");
        Check(final.GcSafepointsObserved == 0 && final.ProcessExitCount == 0, "initializer counters retained without GC claims");
        var doomPlan = InitializerEvidence.CreatePlan(26, 100_000_000);
        Check(doomPlan.PeriodicCycles == 641_026 && doomPlan.MaximumPeriodicPerSegment == 156 &&
            doomPlan.MaximumSnapshots == 4_082 && doomPlan.MaximumSnapshots <= InitializerEvidence.MaximumCheckpoints,
            "checkpoint plan covers all 26 segments at 100M cycles within the hard capacity");
        Reject(() => InitializerEvidence.CreatePlan(InitializerEvidence.MaximumCheckpoints, 100_000_000),
            "checkpoint plan rejects cardinality that cannot preserve final captures");
        var boundedTrace = new TestOnlyInitializerTrace();
        var bounded = new DoomGuestExecutionService { TestOnlyInitializerTrace = boundedTrace }.Execute(path, 10, true);
        var budgetFinal = boundedTrace.Snapshot().Single(e => e.Kind == "Finalizer");
        Check(bounded.Execution!.Status == HybridCpuIseManagedGuestExecutionStatusV1.CycleBudgetExceeded &&
            budgetFinal.PipelineCycleDelta == 10 && budgetFinal.CompletionState == "Failed" &&
            budgetFinal.CompletionAccepted && budgetFinal.Success == false, "budget finalizer preserves exact cycles and failed type state");

        // Real loader, then deliberately duplicate its exact pending binding in this negative fixture.
        var memory = new HybridCpuIseSparseMainMemoryAreaV1();
        var kernel = new DeterministicRuntimeKernelV1();
        var helpers = image.RuntimeBootstrap!.RuntimeHelpers.ToDictionary(r => r.Symbol,
            _ => (HybridCpuRuntimeHelperEntryV1)(_ => true), StringComparer.Ordinal);
        var load = new HybridCpuIseManagedImageLoaderV1().Load(new(image.ImageBytes, image.ImageBase,
            image.EntryAddress, image.PackageSha256, image.RuntimeBootstrap,
            HybridCpuManagedHeapOptionsV1.Create(0x28000000, 0x100000, 65536, -3),
            HybridCpuManagedAbiFamilyV1.Default.TargetContractDigest, HybridCpuManagedAbiFamilyV1.Default.NativeAbiDigest,
            HybridCpuManagedAbiFamilyV1.RuntimePackRevision), memory, kernel, helpers);
        Check(load.IsSuccess && load.PendingInitializers?.Count == 1, "negative fixture uses successfully loaded exact initializer");
        var pending = load.PendingInitializers!.Single();
        load = load with { PendingInitializers = new[] { pending, pending with { Order = pending.Order + 1 } } };
        var registers = image.InitialRegisters!;
        var duplicateTrace = new TestOnlyInitializerTrace();
        var duplicate = new HybridCpuIseManagedGuestExecutionRunnerV1().Execute(new(image.ImageBase, image.EntryAddress,
            HybridCpuRestrictedStartupOptionsV1.Production.ReturnSentinel,
            registers.StackPointerRegister, registers.FramePointerRegister, registers.ThreadPointerRegister,
            registers.ReturnAddressRegister, registers.ReturnValueRegister, registers.GlobalPointerRegister,
            registers.StackPointer, registers.FramePointer, registers.ThreadPointer, registers.ReturnAddress, registers.GlobalPointer,
            load, 0, 4096, TestOnlyInitializerTrace: duplicateTrace), memory, kernel);
        Check(duplicate.Status == HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault &&
            duplicateTrace.Snapshot().Select(e => e.Kind).SequenceEqual(new[] { "Begin", "Finalizer", "BeginRejected", "Terminal" }) &&
            duplicateTrace.Snapshot()[2].BeginState == "Initialized", "second begin of same TypeId rejected before CPU execution");

        InitializerEvidence.Validate(evidence, manifest);
        Check(true, "complete manifest/terminal/checkpoint digest chain accepted");
        Reject(() => InitializerEvidence.Validate(evidence, manifest + ".missing"), "missing required manifest rejected");
        Reject(() => InitializerEvidence.Validate(Path.Combine(directory, "missing-terminal"), manifest), "missing terminal rejected");
        string missingDigest = Path.Combine(directory, "missing-digest");
        Directory.CreateDirectory(missingDigest);
        File.Copy(Path.Combine(evidence, "terminal.json"), Path.Combine(missingDigest, "terminal.json"));
        Reject(() => InitializerEvidence.Validate(missingDigest, manifest), "missing terminal digest rejected");
        string corruptDigest = Path.Combine(directory, "corrupt-digest");
        Directory.CreateDirectory(corruptDigest);
        File.Copy(Path.Combine(evidence, "terminal.json"), Path.Combine(corruptDigest, "terminal.json"));
        File.WriteAllText(Path.Combine(corruptDigest, "terminal.json.sha256"), new string('0', 64));
        Reject(() => InitializerEvidence.Validate(corruptDigest, manifest), "wrong terminal digest rejected");
        InitializerEvidence.AtomicJson(Path.Combine(directory, "regression.json"),
            new { Checks = checks, Baseline = baseline.Execution, Observed = result.Execution,
                Events = events, Budget = boundedTrace.Snapshot(), Duplicate = duplicateTrace.Snapshot(), Qualification = "no" });
        host.Dispose(); host.Completion.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Console.WriteLine($"PASS initializer evidence regression: {checks} checks; qualification no");
        return 0;
    }
}
