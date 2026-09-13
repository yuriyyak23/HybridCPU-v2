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
    private readonly HybridCpuIseManagedGuestExecutionRequestV1 request;
    private readonly Processor.MainMemoryArea memory;
    private readonly IHybridCpuRuntimeKernelV1 kernel;
    private Task<HybridCpuIseManagedGuestExecutionResultV1>? run;
    private bool firstBoundary;
    private bool parkRequested;
    private bool parked;

    private CpuBackedChildExecution(HybridCpuIseManagedGuestExecutionRequestV1 request,
        Processor.MainMemoryArea memory, IHybridCpuRuntimeKernelV1 kernel, string packageSha256)
    {
        this.request = request;
        this.memory = memory;
        this.kernel = kernel;
        PackageSha256 = packageSha256;
    }

    internal string PackageSha256 { get; }
    internal ulong ImageBase => request.ImageBase;

    internal bool ExecuteToTerminal(out HybridCpuIseManagedGuestExecutionResultV1? result, out string reason)
    {
        Task<HybridCpuIseManagedGuestExecutionResultV1> pending;
        lock (sync)
        {
            if (run is not null)
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
        result = pending.GetAwaiter().GetResult();
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
    {
        execution = null;
        if (packageBytes is null || packageBytes.Length == 0 || maximumPipelineCycles <= 0)
        {
            reason = "A non-empty HCEXE package and positive finite cycle budget are required.";
            return false;
        }
        HybridCpuRestrictedImageV1 image = new HybridCpuRestrictedImageBuilderV1().Inspect(packageBytes);
        if (image.Status != HybridCpuStartupStatusV1.Success ||
            image.InitialRegisters is not HybridCpuStartupRegisterStateV1 registers)
        {
            reason = $"HCEXE inspection rejected the executable package (status={image.Status}, bytes={packageBytes.Length}): " +
                string.Join("; ", image.Diagnostics.Select(static item => $"{item.Code}: {item.Message}"));
            return false;
        }
        var memory = new HybridCpuIseSparseMainMemoryAreaV1();
        var kernel = new DeterministicRuntimeKernelV1();
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
        CpuBackedChildExecution? owner = null;
        var request = new HybridCpuIseManagedGuestExecutionRequestV1(
            image.ImageBase, image.EntryAddress, HybridCpuRestrictedStartupOptionsV1.Production.ReturnSentinel,
            registers.StackPointerRegister, registers.FramePointerRegister, registers.ThreadPointerRegister,
            registers.ReturnAddressRegister, registers.ReturnValueRegister, registers.GlobalPointerRegister,
            registers.StackPointer, registers.FramePointer, registers.ThreadPointer, registers.ReturnAddress,
            registers.GlobalPointer, loaded, 0, maximumPipelineCycles, RequireGcSafepointEvidence: requireGc,
            ObservationAttach: _ => new ExecutionGate(owner!));
        owner = new(request, memory, kernel, image.PackageSha256);
        execution = owner;
        reason = string.Empty;
        return true;
    }

    internal bool Start(out string reason)
    {
        lock (sync)
        {
            if (run is not null) { reason = "Execution was already started."; return false; }
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
        lock (sync) { parkRequested = false; Monitor.PulseAll(sync); pending = run; }
        if (pending is null) { reason = string.Empty; return true; }
        _ = pending.GetAwaiter().GetResult();
        // Every runner result is terminal for this bounded owner. A guest fault is not
        // guest success, but it still proves that no CPU execution remains to reclaim.
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

    private sealed class ExecutionGate(CpuBackedChildExecution owner) : IOwnedCoreObservationConsumer
    {
        public void OnBoundary() => owner.OnBoundary();
        public void Dispose() { }
    }
}
