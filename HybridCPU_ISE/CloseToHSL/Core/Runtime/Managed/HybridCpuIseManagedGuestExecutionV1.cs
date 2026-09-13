using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using System.Security.Cryptography;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Registers;
using HybridCPU_ISE.NonRTL.Runtime;

namespace HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;

/// <summary>
/// Immutable startup facts extracted by the HCEXE inspector. HCEXE parsing deliberately
/// remains outside ISE so this execution owner has no production compiler dependency.
/// </summary>
public sealed record HybridCpuIseManagedGuestExecutionRequestV1(
    ulong ImageBase, ulong EntryAddress, ulong ReturnSentinel,
    int StackPointerRegister, int FramePointerRegister, int ThreadPointerRegister,
    int ReturnAddressRegister, int ReturnValueRegister, int GlobalPointerRegister,
    ulong StackPointer, ulong FramePointer, ulong ThreadPointer, ulong ReturnAddress, ulong GlobalPointer,
    HybridCpuIseManagedImageLoadResultV1 LoadResult, int VirtualThreadId, int MaximumPipelineCycles,
    bool RequireGcSafepointEvidence = false,
    IReadOnlyList<HybridCpuIseGuestRegisterSeedV1>? AdditionalRegisterSeeds = null,
    bool RunPendingInitializers = true,
    bool TreatReturnSentinelAsProcessExit = true,
    Func<OwnedCoreObservationBinding, IOwnedCoreObservationConsumer>? ObservationAttach = null,
    TestOnlyInitializerTrace? TestOnlyInitializerTrace = null);

public sealed record HybridCpuIseGuestRegisterSeedV1(int Register, ulong Value);

public enum HybridCpuIseManagedGuestExecutionStatusV1 : byte
{
    Completed = 0, InvalidRequest = 1, CycleBudgetExceeded = 2, KernelRejected = 3, ExecutionFault = 4
}

public sealed record HybridCpuIseManagedGuestExecutionResultV1(
    HybridCpuIseManagedGuestExecutionStatusV1 Status, string Reason, int RetiredPipelineCycles,
    int? ProcessExitCode, ulong FinalProgramCounter, int GcSafepointsObserved = 0,
    IReadOnlyList<string>? GcResultDigests = null, ulong ProcessExitEcallsObserved = 0,
    ulong LastRetiredBundlePc = 0, ulong LastRetireSequence = 0)
{
    public bool IsSuccess => Status == HybridCpuIseManagedGuestExecutionStatusV1.Completed;
    public OwnedCoreObservationDiagnostics? ObservationDiagnostics { get; init; }
}

/// <summary>
/// Explicit, bounded managed-guest execution authority. It starts the real ISE pipeline,
/// observes only retired state, and maps only the exact inspected return sentinel to the
/// RuntimeKernel process-exit boundary. Host exceptions never become guest success.
/// </summary>
public sealed class HybridCpuIseManagedGuestExecutionRunnerV1
{
    private const ulong BundleBytes = 256;

    public HybridCpuIseManagedGuestExecutionResultV1 Execute(
        HybridCpuIseManagedGuestExecutionRequestV1 request,
        Processor.MainMemoryArea memory,
        IHybridCpuRuntimeKernelV1 kernel)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = request.ObservationAttach is null ? null : new OwnedCoreObservationLedger();
        var result = ExecuteCore(request, memory, kernel, diagnostics, "Main");
        request.TestOnlyInitializerTrace?.Record("Terminal", null, request.ImageBase, null, null, false, false, result);
        return diagnostics is null ? result : result with { ObservationDiagnostics = diagnostics.Snapshot() };
    }

    private HybridCpuIseManagedGuestExecutionResultV1 ExecuteCore(
        HybridCpuIseManagedGuestExecutionRequestV1 request,
        Processor.MainMemoryArea memory,
        IHybridCpuRuntimeKernelV1 kernel,
        OwnedCoreObservationLedger? diagnostics, string segmentKind)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(kernel);

        HybridCpuIseManagedImageLoadResultV1 load = request.LoadResult;
        if (!load.IsSuccess || load.Context is null || request.ImageBase == 0 || request.EntryAddress == 0 ||
            request.ReturnSentinel == 0 || request.VirtualThreadId is < 0 or >= Processor.CPU_Core.SmtWays ||
            request.MaximumPipelineCycles is <= 0 || !ValidAdditionalSeeds(request))
            return Failure(HybridCpuIseManagedGuestExecutionStatusV1.InvalidRequest,
                "Execution requires one successfully inspected and loaded HCEXE, a live kernel context, a valid VT and a finite cycle budget.");

        if (request.EntryAddress < request.ImageBase || request.EntryAddress % BundleBytes != 0 ||
            !ValidRegister(request.StackPointerRegister) || !ValidRegister(request.FramePointerRegister) ||
            !ValidRegister(request.ThreadPointerRegister) || !ValidRegister(request.ReturnAddressRegister) ||
            !ValidRegister(request.ReturnValueRegister) || !ValidRegister(request.GlobalPointerRegister) ||
            load.Context.VirtualThreadCarrier != request.VirtualThreadId)
            return Failure(HybridCpuIseManagedGuestExecutionStatusV1.InvalidRequest,
                "HCEXE entry, register state or RuntimeKernel virtual-thread binding is invalid for guest execution.");

        if (request.RunPendingInitializers && load.PendingInitializers is { Count: > 0 } pending)
        {
            foreach (HybridCpuIseManagedInitializerBindingV1 initializer in pending.OrderBy(static row => row.Order))
            {
                var trace = request.TestOnlyInitializerTrace;
                string? beginState = trace is not null && initializer.TypeId is ulong diagnosticTypeId
                    ? load.TypeSystem!.InitializationState(diagnosticTypeId).ToString() : null;
                bool begun = false, completed = false;
                HybridCpuIseManagedGuestExecutionResultV1? initialized = null;
                try
                {
                var managedBridge = load.EcallBridge as HybridCpuIseManagedEcallBridgeV1;
                if (initializer.TypeId is ulong typeId && !load.TypeSystem!.TryBeginInitialization(typeId))
                    return Failure(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        $"Initializer '{initializer.InitializerSymbol}' type-state transition to Running was rejected " +
                        $"from {load.TypeSystem.InitializationState(typeId)} for TypeId 0x{typeId:x16}.");
                if (initializer.TypeHandle is ulong typeHandle &&
                    (managedBridge is null || !managedBridge.TryBeginCpuInitializer(typeHandle)))
                    return Failure(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        $"Initializer '{initializer.InitializerSymbol}' exact CPU-context TypeHandle activation was rejected.");
                begun = true;
                trace?.Record("Begin", initializer, request.ImageBase, beginState,
                    initializer.TypeId is ulong begunTypeId ? load.TypeSystem!.InitializationState(begunTypeId).ToString() : null,
                    true, false);
                initialized = ExecuteCore(request with
                {
                    EntryAddress = initializer.EntryAddress,
                    RequireGcSafepointEvidence = false,
                    RunPendingInitializers = false,
                    TreatReturnSentinelAsProcessExit = false
                }, memory, kernel, diagnostics, "Initializer");
                bool succeeded = initialized.IsSuccess;
                if (initializer.TypeHandle is ulong completedHandle &&
                    !managedBridge!.TryCompleteCpuInitializer(completedHandle))
                    return Failure(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        $"Initializer '{initializer.InitializerSymbol}' exact CPU-context TypeHandle completion was rejected.");
                if (initializer.TypeId is ulong completedTypeId &&
                    !load.TypeSystem!.TryCompleteInitialization(completedTypeId, succeeded))
                    return Failure(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        $"Initializer '{initializer.InitializerSymbol}' type-state completion was rejected.");
                completed = true;
                if (!succeeded)
                    return initialized with { Reason = $"Initializer '{initializer.InitializerSymbol}' failed on the ISE CPU: {initialized.Reason}" };
                }
                finally
                {
                    trace?.Record(begun ? "Finalizer" : "BeginRejected", initializer, request.ImageBase, beginState,
                        initializer.TypeId is ulong finalTypeId ? load.TypeSystem!.InitializationState(finalTypeId).ToString() : null,
                        begun, completed, initialized);
                }
            }
        }

        Processor.CPU_Core? core = null;
        ulong lastRetiredBundlePc = 0;
        ulong lastRetireSequence = 0;
        string priorRetiredLaneDiagnostic = "unavailable";
        string lastRetiredLaneDiagnostic = "unavailable";
        var x17RetireTrace = new List<string>();
        var x18RetireTrace = new Queue<string>();
        var x1RetireTrace = new Queue<string>();
        var x20RetireTrace = new Queue<string>();
        var x10RetireTrace = new Queue<string>();
        var x24RetireTrace = new Queue<string>();
        const int diagnosticControlRetireTraceCapacity = 300_000;
        var controlRetireTrace = new Queue<string>();
        ulong firstNonEmptyDecodedPc = 0;
        ulong lastNonEmptyDecodedPc = 0;
        byte observedDecodedValidMask = 0;
        var entryBundle = new byte[checked((int)BundleBytes)];
        if (!memory.TryReadPhysicalRange(request.EntryAddress, entryBundle))
            return Failure(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                "ISE memory rejected a read-back of the exact HCEXE entry bundle.");
        string entryBundleDigest = Convert.ToHexString(SHA256.HashData(entryBundle)).ToLowerInvariant();
        string entryInstructionPrefix = Convert.ToHexString(entryBundle.AsSpan(0, 32)).ToLowerInvariant();
        int safepoints = 0;
        var gcDigests = new List<string>();
        OwnedCoreObservationSession? observationSession = null;
        try
        {
            core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(
                memory, ProcessorMode.Compiler, ecallBridge: load.EcallBridge));
            core.InitializePipeline();
            core.PrepareExecutionStart(request.EntryAddress, request.VirtualThreadId);
            core.SetExecutionPrivilegeForStartup(request.VirtualThreadId, PrivilegeLevel.User);
            SeedStartupState(core, request);
            if (request.AdditionalRegisterSeeds is not null)
                foreach (HybridCpuIseGuestRegisterSeedV1 seed in request.AdditionalRegisterSeeds)
                    core.WriteCommittedArch(request.VirtualThreadId, seed.Register, seed.Value);
            HybridCpuVmRangeV1? executableImage = load.Context.VmMappings.SingleOrDefault(mapping =>
                (mapping.Protection & HybridCpuVmProtectionV1.Execute) != 0 &&
                mapping.Address <= request.EntryAddress &&
                request.EntryAddress < checked(mapping.Address + mapping.Size));
            if (executableImage is null)
                return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                    "RuntimeKernel did not publish one exact executable image mapping for the HCEXE entry.",
                    0, null, request.EntryAddress);
            HybridCpuVmRangeV1 executableImageRange = executableImage.Value;
            ulong executableImageEnd = checked(executableImageRange.Address + executableImageRange.Size);
            ulong observedRetireSequence = 0;
            var gcRetireTrace = new Queue<string>();
            var gcStack = new HybridCpuManagedGcStackBoundaryV1(request.ImageBase, request.StackPointer, request.ReturnAddress);
            observationSession = request.ObservationAttach is null ? null :
                new OwnedCoreObservationSession(core, request.EntryAddress, request.ObservationAttach, segmentKind);

            for (int cycle = 0; cycle < request.MaximumPipelineCycles; cycle++)
            {
                observationSession?.OnBoundary();
                core.ExecutePipelineCycle();
                Processor.CPU_Core.PipelineObservationSnapshot cycleObservation =
                    core.GetPipelineObservationSnapshot();
                if (cycleObservation.DecodedBundleValidMask != 0)
                {
                    if (firstNonEmptyDecodedPc == 0)
                        firstNonEmptyDecodedPc = cycleObservation.DecodedBundlePc;
                    lastNonEmptyDecodedPc = cycleObservation.DecodedBundlePc;
                    observedDecodedValidMask |= cycleObservation.DecodedBundleValidMask;
                }
                if (core.TryReadLastRetiredBundle(out ulong retiredBundlePc, out ulong retireSequence) &&
                    retireSequence != observedRetireSequence)
                {
                    observedRetireSequence = retireSequence;
                    lastRetiredBundlePc = retiredBundlePc;
                    lastRetireSequence = retireSequence;
                    priorRetiredLaneDiagnostic = lastRetiredLaneDiagnostic;
                    if (core.TryReadLastRetiredLaneDiagnostic(
                        out byte retiredLaneIndex,
                        out uint retiredOpcode,
                        out bool retiredWritesRegister,
                        out ushort retiredDestinationRegister,
                        out ulong retiredResultValue,
                        out string retiredMicroOpType,
                        out string retiredMicroOpDescription))
                    {
                        lastRetiredLaneDiagnostic =
                            $"lane={retiredLaneIndex},opcode=0x{retiredOpcode:x},writes={retiredWritesRegister}," +
                            $"dest=x{retiredDestinationRegister},value=0x{retiredResultValue:x16},uop={retiredMicroOpType}";
                        if (retiredWritesRegister && retiredDestinationRegister == 17)
                        {
                            x17RetireTrace.Add(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                        if (retiredWritesRegister && retiredDestinationRegister == 18)
                        {
                            if (x18RetireTrace.Count == 128) x18RetireTrace.Dequeue();
                            x18RetireTrace.Enqueue(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                        if (retiredWritesRegister && retiredDestinationRegister == 20)
                        {
                            if (x20RetireTrace.Count == 512) x20RetireTrace.Dequeue();
                            x20RetireTrace.Enqueue(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                        if (retiredWritesRegister && retiredDestinationRegister is 10 or 24)
                        {
                            Queue<string> trace = retiredDestinationRegister == 10 ? x10RetireTrace : x24RetireTrace;
                            if (trace.Count == 512) trace.Dequeue();
                            trace.Enqueue(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                        if (retiredWritesRegister && retiredDestinationRegister == 1)
                        {
                            if (x1RetireTrace.Count == 128) x1RetireTrace.Dequeue();
                            x1RetireTrace.Enqueue(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                        if (retiredWritesRegister && retiredDestinationRegister == 11 ||
                            retiredOpcode is 0xb0 or 0xb1 or 0xb2 or 0xb3 or 0xb4 or 0xb5)
                        {
                            if (controlRetireTrace.Count == diagnosticControlRetireTraceCapacity)
                                controlRetireTrace.Dequeue();
                            controlRetireTrace.Enqueue(
                                $"pc=0x{retiredBundlePc:x},op=0x{retiredOpcode:x},writes={retiredWritesRegister}," +
                                $"dest=x{retiredDestinationRegister},value=0x{retiredResultValue:x},desc={retiredMicroOpDescription}");
                        }
                    }
                    if (request.RequireGcSafepointEvidence)
                    {
                        HybridCpuManagedRetiredSafepointResultV1 safepoint = CollectSafepoint(
                            request, memory, core, retiredBundlePc, gcStack);
                        if (safepoint.Status == HybridCpuManagedRetiredSafepointStatusV1.Rejected)
                            return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                                "Retired GC safepoint was rejected: " + safepoint.Reason +
                                " Prior collections: " + string.Join(" | ", gcRetireTrace) +
                                $" x10-retire-trace=[{string.Join(';', x10RetireTrace)}]," +
                                $" x18-retire-trace=[{string.Join(';', x18RetireTrace)}]," +
                                $" x24-retire-trace=[{string.Join(';', x24RetireTrace)}]," +
                                $" control-retire-trace=[{string.Join(';', controlRetireTrace)}].",
                                cycle + 1, null, core.ReadCommittedPc(request.VirtualThreadId), safepoints, gcDigests,
                                LastRetiredBundlePc: lastRetiredBundlePc, LastRetireSequence: lastRetireSequence);
                        if (safepoint.IsSuccess)
                        {
                            safepoints++;
                            gcDigests.Add(safepoint.Collection!.ResultDigest);
                            if (gcRetireTrace.Count == 32) gcRetireTrace.Dequeue();
                            gcRetireTrace.Enqueue($"pc=0x{retiredBundlePc:x},sp=0x{core.ReadArch(request.VirtualThreadId, request.StackPointerRegister):x}," +
                                $"roots={safepoint.Collection.Roots.Count}[{string.Join(',', safepoint.Collection.Roots.Take(64).Select(value => $"{value:x}"))}]," +
                                $"reclaimed={safepoint.Collection.ReclaimedObjects.Count}[{string.Join(',', safepoint.Collection.ReclaimedObjects.Take(64).Select(value => $"{value:x}"))}]");
                        }
                    }
                }
                if (load.EcallBridge is HybridCpuIseManagedEcallBridgeV1 managedBridge &&
                    managedBridge.LastProcessExitCode is int ecallExitCode)
                {
                    if (!request.TreatReturnSentinelAsProcessExit)
                    {
                        ulong diagnosticX10 = core.ReadArch(request.VirtualThreadId, 10);
                        ulong diagnosticX11 = core.ReadArch(request.VirtualThreadId, 11);
                        ulong diagnosticX12 = core.ReadArch(request.VirtualThreadId, 12);
                        ulong diagnosticX17 = core.ReadArch(request.VirtualThreadId, 17);
                        ulong diagnosticX1 = core.ReadArch(request.VirtualThreadId, 1);
                        ulong diagnosticX2 = core.ReadArch(request.VirtualThreadId, 2);
                        int[] diagnosticCalleeSavedRegisters = [8, 9, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27];
                        string diagnosticCalleeSaved = string.Join(',',
                            diagnosticCalleeSavedRegisters.Select(register =>
                                $"x{register}=0x{core.ReadArch(request.VirtualThreadId, register):x}"));
                        return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                            $"Image initializer invoked ProcessExit({ecallExitCode}) instead of returning to its exact sentinel; " +
                            $"last-managed-operation=0x{managedBridge.LastManagedOperation:x}, " +
                            $"last-managed-status={managedBridge.LastManagedStatus?.ToString() ?? "none"}, " +
                            $"last-managed-reason='{managedBridge.LastManagedReason}', " +
                            $"last-managed-external-status={managedBridge.LastManagedExternalStatus?.ToString() ?? "none"}, " +
                            $"last-managed-external-error={managedBridge.LastManagedExternalError?.ToString() ?? "none"}, " +
                            $"last-managed-external-value=0x{managedBridge.LastManagedExternalValue:x}, " +
                            $"last-managed-receiver=0x{managedBridge.LastManagedReceiver:x}, " +
                            $"last-managed-argument1=0x{managedBridge.LastManagedArgument1:x}, " +
                            $"last-managed-argument2=0x{managedBridge.LastManagedArgument2:x}, " +
                            $"x1=0x{diagnosticX1:x},x2=0x{diagnosticX2:x},x10=0x{diagnosticX10:x}," +
                            $"x11=0x{diagnosticX11:x},x12=0x{diagnosticX12:x},x17=0x{diagnosticX17:x}," +
                            $"callee-saved=[{diagnosticCalleeSaved}], " +
                            $"last-retired-lane=({lastRetiredLaneDiagnostic}), x18-retire-trace=[{string.Join(';', x18RetireTrace)}], " +
                            $"x10-retire-trace=[{string.Join(';', x10RetireTrace)}], " +
                            $"x24-retire-trace=[{string.Join(';', x24RetireTrace)}], " +
                            $"x17-retire-trace=[{string.Join(';', x17RetireTrace)}], " +
                            $"control-retire-trace=[{string.Join(';', controlRetireTrace)}].",
                            cycle + 1, ecallExitCode, core.ReadCommittedPc(request.VirtualThreadId), safepoints, gcDigests,
                            managedBridge.ProcessExitRetireCount, lastRetiredBundlePc, lastRetireSequence);
                    }
                    if (request.RequireGcSafepointEvidence && safepoints == 0)
                        return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                            "Guest reached ECALL process exit without one exact retired GC safepoint.",
                            cycle + 1, null, core.ReadCommittedPc(request.VirtualThreadId), 0, gcDigests,
                            managedBridge.ProcessExitRetireCount);
                    return new(HybridCpuIseManagedGuestExecutionStatusV1.Completed, string.Empty, cycle + 1,
                        ecallExitCode, core.ReadCommittedPc(request.VirtualThreadId), safepoints, gcDigests,
                        managedBridge.ProcessExitRetireCount);
                }
                ulong retiredPc = core.ReadCommittedPc(request.VirtualThreadId);
                if (retiredPc != request.ReturnSentinel)
                {
                    ulong activePc = cycleObservation.ActiveLivePc;
                    if (activePc != request.ReturnSentinel &&
                        (activePc < executableImageRange.Address || activePc >= executableImageEnd))
                    {
                        ulong x3 = core.ReadArch(request.VirtualThreadId, 3);
                        ulong x5 = core.ReadArch(request.VirtualThreadId, 5);
                        ulong x10 = core.ReadArch(request.VirtualThreadId, 10);
                        ulong x17 = core.ReadArch(request.VirtualThreadId, 17);
                        ulong x28 = core.ReadArch(request.VirtualThreadId, 28);
                        ulong x5Vt0 = core.ReadArch(0, 5);
                        ulong x5Vt1 = core.ReadArch(1, 5);
                        ulong x5Vt2 = core.ReadArch(2, 5);
                        ulong x5Vt3 = core.ReadArch(3, 5);
                        Span<byte> pointedBytes = stackalloc byte[8];
                        ulong pointedValue = memory.TryReadPhysicalRange(x28, pointedBytes)
                            ? BitConverter.ToUInt64(pointedBytes)
                            : 0;
                        byte[] priorRetiredBundleBytes = new byte[32];
                        const ulong bundleSizeBytes = 256;
                        ulong priorRetiredBundlePc = lastRetiredBundlePc >= bundleSizeBytes
                            ? lastRetiredBundlePc - bundleSizeBytes
                            : 0;
                        string priorRetiredInstruction = priorRetiredBundlePc != 0 &&
                            memory.TryReadPhysicalRange(priorRetiredBundlePc, priorRetiredBundleBytes)
                                ? Convert.ToHexString(priorRetiredBundleBytes).ToLowerInvariant()
                                : "unavailable";
                        string ecallRejection = load.EcallBridge is HybridCpuIseManagedEcallBridgeV1 rejectedBridge
                            ? rejectedBridge.LastRejectedReason
                            : "unavailable";
                        return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                            $"Guest control flow escaped its exact RuntimeKernel executable image mapping: " +
                            $"active-pc=0x{activePc:x16}, mapping=[0x{executableImageRange.Address:x16},0x{executableImageEnd:x16}), " +
                            $"x3=0x{x3:x16}, x5=0x{x5:x16}, x10=0x{x10:x16}, x17=0x{x17:x16}, x28=0x{x28:x16}, " +
                            $"x5-by-vt=[0x{x5Vt0:x16},0x{x5Vt1:x16},0x{x5Vt2:x16},0x{x5Vt3:x16}], " +
                            $"memory[x28]=0x{pointedValue:x16}, prior-retired-pc=0x{priorRetiredBundlePc:x16}, " +
                            $"prior-retired-instruction={priorRetiredInstruction}, " +
                            $"prior-retired-lane=({priorRetiredLaneDiagnostic}), " +
                            $"last-retired-lane=({lastRetiredLaneDiagnostic}), " +
                            $"x17-retire-trace=[{string.Join(';', x17RetireTrace)}], " +
                            $"ecall-rejection=({ecallRejection}).", cycle + 1, null, retiredPc,
                            safepoints, gcDigests, LastRetiredBundlePc: lastRetiredBundlePc,
                            LastRetireSequence: lastRetireSequence);
                    }
                    continue;
                }

                if (request.RequireGcSafepointEvidence && safepoints == 0)
                    return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        "Guest reached process exit without one exact retired GC safepoint.",
                        cycle + 1, null, retiredPc, 0, gcDigests);

                ulong exitRegister = core.ReadArch(request.VirtualThreadId, request.ReturnValueRegister);
                if (exitRegister != unchecked((ulong)(uint)exitRegister))
                    return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                        "Guest process-exit value is not a canonical 32-bit exit code.", cycle + 1, null, retiredPc,
                        safepoints, gcDigests);

                int exitCode = unchecked((int)(uint)exitRegister);
                if (!request.TreatReturnSentinelAsProcessExit)
                    return new(HybridCpuIseManagedGuestExecutionStatusV1.Completed, string.Empty, cycle + 1,
                        null, retiredPc, safepoints, gcDigests, LastRetiredBundlePc: lastRetiredBundlePc,
                        LastRetireSequence: lastRetireSequence);
                HybridCpuKernelResultV1 exited = kernel.ProcessExit(exitCode);
                return exited.IsSuccess
                    ? new(HybridCpuIseManagedGuestExecutionStatusV1.Completed, string.Empty, cycle + 1, exitCode,
                        retiredPc, safepoints, gcDigests,
                        LastRetiredBundlePc: lastRetiredBundlePc, LastRetireSequence: lastRetireSequence)
                    : new(HybridCpuIseManagedGuestExecutionStatusV1.KernelRejected, exited.Reason, cycle + 1, null,
                        retiredPc, safepoints, gcDigests,
                        LastRetiredBundlePc: lastRetiredBundlePc, LastRetireSequence: lastRetireSequence);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            return new(HybridCpuIseManagedGuestExecutionStatusV1.ExecutionFault,
                "ISE pipeline rejected the guest image: " + DescribeExceptionChain(exception), 0, null,
                core?.ReadCommittedPc(request.VirtualThreadId) ?? 0,
                LastRetiredBundlePc: lastRetiredBundlePc, LastRetireSequence: lastRetireSequence);
        }

        finally
        {
            observationSession?.Dispose();
            if (observationSession is not null) diagnostics!.Record(observationSession.Report);
        }

        Processor.CPU_Core.PipelineObservationSnapshot observation = core!.GetPipelineObservationSnapshot();
        Processor.CPU_Core.PipelineControl control = observation.PipelineControl;
        int[] boundedDiagnosticRegisters = [1, 2, 5, 8, 9, 10, 11, 12, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27];
        string boundedRegisterState = string.Join(',', boundedDiagnosticRegisters.Select(register =>
            $"x{register}=0x{core.ReadArch(request.VirtualThreadId, register):x}"));
        return new(HybridCpuIseManagedGuestExecutionStatusV1.CycleBudgetExceeded,
            $"Guest did not reach the exact restricted process-exit sentinel within its bounded pipeline-cycle budget; " +
            $"committed-pc=0x{core.ReadCommittedPc(request.VirtualThreadId):x16}, " +
            $"active-live-pc=0x{observation.ActiveLivePc:x16}, vt-state={observation.CurrentVirtualThreadPipelineState}, " +
            $"stalled={control.Stalled}, stall-kind={control.StallReason}, stall-cycles={control.StallCycles}, " +
            $"decoded-pc=0x{observation.DecodedBundlePc:x16}, valid-mask=0x{observation.DecodedBundleValidMask:x2}, " +
            $"nop-mask=0x{observation.DecodedBundleNopMask:x2}, decode-fault={observation.DecodedBundleHasDecodeFault}, " +
            $"first-nonempty-decoded-pc=0x{firstNonEmptyDecodedPc:x16}, " +
            $"last-nonempty-decoded-pc=0x{lastNonEmptyDecodedPc:x16}, observed-valid-mask=0x{observedDecodedValidMask:x2}, " +
            $"instructions-retired={control.InstructionsRetired}, " +
            $"bounded-register-state=[{boundedRegisterState}], " +
            $"x1-retire-trace=[{string.Join(';', x1RetireTrace)}], " +
            $"x20-retire-trace=[{string.Join(';', x20RetireTrace)}], " +
            $"x10-retire-trace=[{string.Join(';', x10RetireTrace)}], " +
            $"x24-retire-trace=[{string.Join(';', x24RetireTrace)}], " +
            $"control-retire-trace=[{string.Join(';', controlRetireTrace)}], " +
            $"entry-bundle-sha256={entryBundleDigest}, entry-instruction={entryInstructionPrefix}, " +
            $"last-retired-bundle=0x{lastRetiredBundlePc:x16}, retire-sequence={lastRetireSequence}.",
            request.MaximumPipelineCycles, null, core.ReadCommittedPc(request.VirtualThreadId), safepoints, gcDigests,
            LastRetiredBundlePc: lastRetiredBundlePc, LastRetireSequence: lastRetireSequence);
    }

    private static string DescribeExceptionChain(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            messages.Add($"{current.GetType().Name}: {current.Message}");
        return string.Join(" -> ", messages);
    }

    private static bool ValidRegister(int register) => register is >= 0 and <= 31;

    private static bool ValidAdditionalSeeds(HybridCpuIseManagedGuestExecutionRequestV1 request)
    {
        IReadOnlyList<HybridCpuIseGuestRegisterSeedV1>? seeds = request.AdditionalRegisterSeeds;
        if (seeds is null) return true;
        int[] startupRegisters = [request.StackPointerRegister, request.FramePointerRegister,
            request.ThreadPointerRegister, request.ReturnAddressRegister, request.ReturnValueRegister,
            request.GlobalPointerRegister];
        return seeds.Count <= 31 &&
            seeds.All(static seed => seed.Register is > 0 and <= 31) &&
            seeds.All(seed => !startupRegisters.Contains(seed.Register)) &&
            seeds.Select(static seed => seed.Register).Distinct().Count() == seeds.Count;
    }

    private static HybridCpuManagedRetiredSafepointResultV1 CollectSafepoint(
        HybridCpuIseManagedGuestExecutionRequestV1 request,
        Processor.MainMemoryArea memory,
        Processor.CPU_Core core,
        ulong retiredBundlePc,
        HybridCpuManagedGcStackBoundaryV1 gcStack)
    {
        HybridCpuIseManagedImageLoadResultV1 load = request.LoadResult;
        if (load.Gc is null || load.StackMaps is null)
            return new(HybridCpuManagedRetiredSafepointStatusV1.Rejected,
                "Loader did not publish image-owned GC registrations.", null);
        int relativePc;
        try { relativePc = checked((int)(retiredBundlePc - request.ImageBase)); }
        catch (OverflowException)
        {
            return new(HybridCpuManagedRetiredSafepointStatusV1.Rejected,
                "Retired bundle PC is outside the inspected image coordinate space.", null);
        }
        var registers = new ulong[64];
        for (int register = 0; register < 32; register++)
            registers[register] = core.ReadArch(request.VirtualThreadId, register);
        ulong stackPointer = registers[request.StackPointerRegister];
        return load.Gc.CollectRetiredSafepoint(load.StackMaps, relativePc, registers, stackPointer,
            (address, count) =>
            {
                byte[] bytes = new byte[count];
                return memory.TryReadPhysicalRange(address, bytes) ? bytes : null;
            }, load.ProcessRoots ?? [], load.Strings,
            gcStack);
    }

    private static void SeedStartupState(Processor.CPU_Core core, HybridCpuIseManagedGuestExecutionRequestV1 request)
    {
        core.WriteCommittedPc(request.VirtualThreadId, request.EntryAddress);
        core.WriteCommittedArch(request.VirtualThreadId, request.StackPointerRegister, request.StackPointer);
        core.WriteCommittedArch(request.VirtualThreadId, request.FramePointerRegister, request.FramePointer);
        core.WriteCommittedArch(request.VirtualThreadId, request.ThreadPointerRegister, request.ThreadPointer);
        core.WriteCommittedArch(request.VirtualThreadId, request.ReturnAddressRegister, request.ReturnAddress);
        core.WriteCommittedArch(request.VirtualThreadId, request.GlobalPointerRegister, request.GlobalPointer);
    }

    private static HybridCpuIseManagedGuestExecutionResultV1 Failure(HybridCpuIseManagedGuestExecutionStatusV1 status, string reason) =>
        new(status, reason, 0, null, 0);
}
