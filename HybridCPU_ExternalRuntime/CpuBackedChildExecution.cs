using HybridCPU.Compiler.Core.Target.Managed;
using HybridCPU.Compiler.Core.Target.Runtime;
using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.NonRTL.Runtime;
using YAKSys_Hybrid_CPU;

namespace HybridCPU.ExternalRuntime;

internal sealed class CpuBackedChildExecution
{
    private readonly object sync = new();
    private HybridCpuIseManagedGuestExecutionRequestV1 request;
    private OwnedExecutableDataMemory memory;
    private IHybridCpuRuntimeKernelV1 kernel;
    private Task<HybridCpuIseManagedGuestExecutionResultV1>? run;
    private bool firstBoundary;
    private bool parkRequested;
    private bool parked;
    private bool materialized;
    private bool closed;
    private OwnedCoreObservationBinding? originalObservationBinding;
    private OwnedCoreRetireObservation? originalRetire;
    private bool observationFaulted;
    private bool terminalRetireCaptured;
    private ulong[] admittedInitializerEntries = [];
    private readonly List<InitializerObservation> initializerObservations = [];
    private sealed class InitializerObservation(OwnedCoreObservationBinding binding)
    {
        internal readonly OwnedCoreObservationBinding Binding = binding;
        internal OwnedCoreRetireObservation? Retire;
        internal bool Terminal;
    }

    private CpuBackedChildExecution(HybridCpuIseManagedGuestExecutionRequestV1 request,
        OwnedExecutableDataMemory memory, IHybridCpuRuntimeKernelV1 kernel, string packageSha256)
    {
        this.request = request;
        this.memory = memory;
        this.kernel = kernel;
        PackageSha256 = packageSha256;
        materialized = request is not null;
    }

    internal string PackageSha256 { get; }
    internal ulong ImageBase => request.ImageBase;

    internal bool ExecuteToTerminal(out HybridCpuIseManagedGuestExecutionResultV1? result, out string reason)
    {
        Task<HybridCpuIseManagedGuestExecutionResultV1> pending;
        lock (sync)
        {
            if (!materialized || closed || run is not null)
            {
                result = null;
                reason = "Execution was already started.";
                return false;
            }
            parkRequested = false;
            run = Task.Factory.StartNew(
                () => new HybridCpuIseManagedGuestExecutionRunnerV1().Execute(request, memory, kernel),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            pending = run;
        }
        try { result = pending.GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            result = null;
            reason = $"CPU-backed runner terminated with an unreceipted failure: {exception.GetType().Name}.";
            return false;
        }
        lock (sync)
        {
            // A failed admitted initializer can end the exact run before Main.
            // Require its independently returned retire tuple, not absence of Main.
            var failedInitializer = !result.IsSuccess && originalObservationBinding is null &&
                initializerObservations.Count > 0 && result.LastRetireSequence > 0
                ? initializerObservations[^1] : null;
            var terminalBinding = failedInitializer?.Binding ?? originalObservationBinding;
            var terminalRaw = failedInitializer?.Retire ?? originalRetire;
            if (observationFaulted || failedInitializer is null && initializerObservations.Count != admittedInitializerEntries.Length ||
                initializerObservations.Any(row => !row.Terminal || row.Retire is null) ||
                failedInitializer is null && !terminalRetireCaptured || terminalRaw is not { Sequence: > 0 } retired ||
                terminalBinding is not { } binding ||
                retired.SegmentId != binding.SegmentId || retired.EntryAddress != binding.EntryAddress ||
                retired.BundlePc < request.ImageBase ||
                result.LastRetireSequence != 0 && (result.LastRetireSequence != retired.Sequence || result.LastRetiredBundlePc != retired.BundlePc))
            {
                reason = "Original owned-core retire source is missing, inconsistent or faulted.";
                return false;
            }
            // The managed ECALL exit projection omits these fields. Preserve its
            // typed result while supplying exact facts captured from the same core
            // before that source is disposed; never infer them from cycle counts.
            result = result with { LastRetiredBundlePc = retired.BundlePc, LastRetireSequence = retired.Sequence };
        }
        if (result.RetiredPipelineCycles <= 0 || result.LastRetireSequence == 0 ||
            result.LastRetiredBundlePc < request.ImageBase)
        {
            reason = "The terminal ISE result did not independently prove retired guest work.";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    internal static bool TryCreate(byte[] packageBytes, int maximumPipelineCycles,
        out CpuBackedChildExecution? execution, out string reason)
        => TryCreate(packageBytes, maximumPipelineCycles, out execution, out reason, out _);

    internal static bool TryCreate(byte[] packageBytes, int maximumPipelineCycles,
        out CpuBackedChildExecution? execution, out string reason, out bool noEffect)
        => TryCreate(packageBytes, maximumPipelineCycles, out execution, out reason, out noEffect, null);

    internal static bool TryCreate(byte[] packageBytes, int maximumPipelineCycles,
        out CpuBackedChildExecution? execution, out string reason, out bool noEffect,
        Action<CpuBackedChildExecution>? retainBacking, ulong maximumPrivateDataBytes = 0x3000_0000)
    {
        noEffect = false; // Exceptions and partial materialization are unknown.
        execution = null;
        if (packageBytes is null || packageBytes.Length == 0 || maximumPipelineCycles <= 0)
        {
            reason = "A non-empty HCEXE package and positive finite cycle budget are required.";
            noEffect = true;
            return false;
        }
        HybridCpuRestrictedImageV1 image = new HybridCpuRestrictedImageBuilderV1().Inspect(packageBytes);
        if (image.Status != HybridCpuStartupStatusV1.Success ||
            image.InitialRegisters is not HybridCpuStartupRegisterStateV1 registers)
        {
            reason = $"HCEXE inspection rejected the executable package (status={image.Status}, bytes={packageBytes.Length}): " +
                string.Join("; ", image.Diagnostics.Select(static item => $"{item.Code}: {item.Message}"));
            noEffect = true; // No kernel/memory/execution owner has been created.
            return false;
        }
        var memory = new OwnedExecutableDataMemory(maximumDataBytes: maximumPrivateDataBytes);
        var kernel = new DeterministicRuntimeKernelV1();
        var owner = new CpuBackedChildExecution(null!, memory, kernel, image.PackageSha256);
        execution = owner;
        retainBacking?.Invoke(owner); // Internal owner assignment before loader/kernel/memory effect.
        HybridCpuIseManagedImageLoadResultV1 loaded;
        bool requireGc = image.RuntimeBootstrap is not null;
        if (image.RuntimeBootstrap is not null)
        {
            var helpers = image.RuntimeBootstrap.RuntimeHelpers.ToDictionary(static row => row.Symbol,
                static _ => (HybridCpuRuntimeHelperEntryV1)(_ => true), StringComparer.Ordinal);
            loaded = new HybridCpuIseManagedImageLoaderV1().Load(
                new(image.ImageBytes, image.ImageBase, image.EntryAddress, image.PackageSha256,
                    image.RuntimeBootstrap,
                    HybridCpuManagedHeapOptionsV1.Create(0x2800_0000, 0x0010_0000,
                        HybridCpuManagedHeapOptionsV1.DefaultMaximumObjectSizeBytes, -3),
                    HybridCpuManagedAbiFamilyV1.Default.TargetContractDigest,
                    HybridCpuManagedAbiFamilyV1.Default.NativeAbiDigest,
                    HybridCpuManagedAbiFamilyV1.RuntimePackRevision), memory, kernel, helpers);
        }
        else
        {
            ulong mapped = checked(((ulong)image.ImageBytes.Length + 4095) / 4096 * 4096);
            HybridCpuKernelBootResultV1 boot = kernel.Boot(new(HybridCpuPlatformContractV1.ContractDigest,
                image.OptionsDigest, image.ImageBase, mapped, image.EntryAddress,
                HybridCpuRestrictedStartupOptionsV1.Production.StackBase,
                HybridCpuRestrictedStartupOptionsV1.Production.StackSize, 0));
            if (!boot.IsSuccess || boot.Context is null || !memory.TryWritePhysicalRange(image.ImageBase, image.ImageBytes))
            {
                reason = "RuntimeKernel or ISE memory rejected native HCEXE materialization.";
                return false;
            }
            for (int offset = 0; offset + 256 <= image.ImageBytes.Length; offset += 256)
                memory.PublishVliwBundleAnnotations(image.ImageBase + (ulong)offset, VliwBundleAnnotations.Empty);
            loaded = new(HybridCpuIseManagedImageLoadStatusV1.Success, string.Empty, boot.Context,
                null, null, null, null, null);
        }
        if (!loaded.IsSuccess)
        {
            reason = $"ISE managed image loader rejected HCEXE: {loaded.Status}.";
            return false;
        }

        if (image.RuntimeBootstrap is { } bootstrap &&
            (loaded.Gc is null || !loaded.Gc.BindImageStaticRoots(bootstrap.StaticRoots, (address, count) =>
            {
                byte[] bytes = new byte[count];
                return memory.TryReadPhysicalRange(address, bytes) ? bytes : null;
            })))
        {
            reason = "Original admitted image static roots could not be bound to the collector.";
            return false;
        }
        // Freeze the same admitted order for the runner and observation owner.
        loaded = loaded with { PendingInitializers = (loaded.PendingInitializers ?? []).OrderBy(row => row.Order).ToArray() };
        var request = new HybridCpuIseManagedGuestExecutionRequestV1(
            image.ImageBase, image.EntryAddress, HybridCpuRestrictedStartupOptionsV1.Production.ReturnSentinel,
            registers.StackPointerRegister, registers.FramePointerRegister, registers.ThreadPointerRegister,
            registers.ReturnAddressRegister, registers.ReturnValueRegister, registers.GlobalPointerRegister,
            registers.StackPointer, registers.FramePointer, registers.ThreadPointer, registers.ReturnAddress,
            registers.GlobalPointer, loaded, 0, maximumPipelineCycles, RequireGcSafepointEvidence: requireGc,
            ObservationAttach: binding => owner.AttachObservation(binding));
        owner.request = request;
        owner.admittedInitializerEntries = (loaded.PendingInitializers ?? []).OrderBy(row => row.Order)
            .Select(row => row.EntryAddress).ToArray();
        owner.materialized = true;
        reason = string.Empty;
        return true;
    }

    internal bool Start(out string reason)
    {
        lock (sync)
        {
            if (!materialized || closed || run is not null) { reason = "Execution is unavailable or was already started."; return false; }
            parkRequested = true;
            run = Task.Factory.StartNew(() => new HybridCpuIseManagedGuestExecutionRunnerV1().Execute(request, memory, kernel),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            while (!parked && !run.IsCompleted) Monitor.Wait(sync, TimeSpan.FromMilliseconds(25));
            if (!firstBoundary || !parked)
            {
                reason = "CPU-backed execution reached a terminal result before a running boundary was receipted.";
                return false;
            }
            parkRequested = false;
            Monitor.PulseAll(sync);
            while (parked && !run.IsCompleted) Monitor.Wait(sync, TimeSpan.FromMilliseconds(25));
            if (run.IsCompleted)
            {
                reason = "CPU-backed execution terminated before a running boundary was receipted.";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    internal bool Park(out string reason)
    {
        lock (sync)
        {
            if (run is null || run.IsCompleted) { reason = "CPU-backed execution is not running."; return false; }
            parkRequested = true;
            while (!parked && !run.IsCompleted) Monitor.Wait(sync, TimeSpan.FromMilliseconds(25));
            if (!parked) { reason = "Execution terminated before a parked boundary was receipted."; return false; }
            reason = string.Empty;
            return true;
        }
    }

    internal bool Resume(out string reason)
    {
        lock (sync)
        {
            if (!parked || run is null || run.IsCompleted) { reason = "CPU-backed execution is not parked."; return false; }
            parkRequested = false;
            Monitor.PulseAll(sync);
            while (parked && !run.IsCompleted) Monitor.Wait(sync, TimeSpan.FromMilliseconds(25));
            if (run.IsCompleted) { reason = "Execution terminated before a resumed boundary was receipted."; return false; }
            reason = string.Empty;
            return true;
        }
    }

    internal bool Close(out string reason)
    {
        Task<HybridCpuIseManagedGuestExecutionResultV1>? pending;
        lock (sync) { closed = true; parkRequested = false; Monitor.PulseAll(sync); pending = run; }
        try { if (pending is not null) _ = pending.GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            reason = $"CPU-backed runner terminated with an unreceipted failure: {exception.GetType().Name}.";
            return false;
        }
        // Every runner result is terminal for this bounded owner. A guest fault is not
        // guest success, but it still proves that no CPU execution remains to reclaim.
        lock (sync)
        {
            closed = true;
            materialized = false;
            memory?.Retire();
            request = null!;
            memory = null!;
            kernel = null!;
            run = null;
        }
        reason = string.Empty;
        return true;
    }

    private void OnBoundary()
    {
        lock (sync)
        {
            firstBoundary = true;
            Monitor.PulseAll(sync);
            while (parkRequested)
            {
                parked = true;
                Monitor.PulseAll(sync);
                Monitor.Wait(sync);
            }
            if (parked) { parked = false; Monitor.PulseAll(sync); }
        }
    }

    private IOwnedCoreObservationConsumer AttachObservation(OwnedCoreObservationBinding binding)
    {
        lock (sync)
        {
            bool validIdentity = binding.SegmentId != Guid.Empty && binding.ReadOriginalRetire is not null &&
                initializerObservations.All(row => row.Binding.SegmentId != binding.SegmentId);
            if (validIdentity && originalObservationBinding is null && binding.SegmentKind == "Initializer" &&
                initializerObservations.Count < admittedInitializerEntries.Length &&
                binding.EntryAddress == admittedInitializerEntries[initializerObservations.Count] &&
                initializerObservations.All(row => row.Terminal))
                initializerObservations.Add(new(binding));
            else if (validIdentity && originalObservationBinding is null && binding.SegmentKind == "Main" &&
                binding.EntryAddress == request.EntryAddress &&
                initializerObservations.Count == admittedInitializerEntries.Length &&
                initializerObservations.All(row => row.Terminal))
                originalObservationBinding = binding;
            else
            {
                observationFaulted = true;
                throw new InvalidOperationException("Original execution requires exact admitted initializer sources before one main-core source.");
            }
        }
        return new ExecutionGate(this, binding);
    }

    private void CaptureRetire(OwnedCoreObservationBinding binding, bool terminal = false)
    {
        try
        {
            var raw = binding.ReadOriginalRetire!(); // Owned core query outside the execution gate.
            lock (sync)
            {
                var initializer = initializerObservations.SingleOrDefault(row => ReferenceEquals(row.Binding, binding));
                var prior = initializer is null ? originalRetire : initializer.Retire;
                if (initializer is null && !ReferenceEquals(binding, originalObservationBinding) ||
                    initializer is { Terminal: true } || terminal && raw is null || raw is not null &&
                    (raw.SegmentId != binding.SegmentId || raw.EntryAddress != binding.EntryAddress || raw.Sequence == 0 ||
                     prior is not null && (raw.Sequence < prior.Sequence || raw.Sequence == prior.Sequence && raw.BundlePc != prior.BundlePc)))
                    observationFaulted = true;
                else if (raw is not null)
                {
                    if (initializer is not null) { initializer.Retire = raw; if (terminal) initializer.Terminal = true; }
                    else { originalRetire = raw; if (terminal) terminalRetireCaptured = true; }
                }
            }
        }
        catch { lock (sync) observationFaulted = true; }
    }

    private sealed class ExecutionGate(CpuBackedChildExecution owner, OwnedCoreObservationBinding binding) : IOwnedCoreObservationConsumer
    {
        public void OnBoundary() { owner.CaptureRetire(binding); owner.OnBoundary(); }
        public void Dispose() => owner.CaptureRetire(binding, terminal: true);
    }
}
