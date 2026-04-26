using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.DiagnosticsConsole;

/// <summary>
/// Centralizes the legacy Processor bootstrap and runtime access surface used by
/// TestAssemblerConsoleApps so architectural probes do not reach into the global
/// compat runtime from every workload file.
/// </summary>
internal sealed class DiagnosticRuntimeSession
{
    public const int DefaultCoreId = 0;

    public DiagnosticRuntimeSession(int coreId = DefaultCoreId)
    {
        if (coreId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(coreId), coreId, "Core id must be non-negative.");
        }

        CoreId = coreId;
    }

    public int CoreId { get; }

    public void BootstrapCompilerRuntime()
    {
#pragma warning disable CS0618
        _ = new Processor(ProcessorMode.Compiler);
#pragma warning restore CS0618

        Processor.ConfigureProfiling(true, ProfilingOptions.Default());
        Processor.ResetPerformanceCounters();
    }

    public void ResetProfiling()
    {
        Processor.ConfigureProfiling(false);
    }

    public void WriteMemory(ulong address, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        Processor.MainMemory.WriteToPosition(bytes, address);
    }

    public void PublishBundleAnnotations(ulong bundleAddress, VliwBundleAnnotations annotations)
    {
        Processor.MainMemory.PublishVliwBundleAnnotations(bundleAddress, annotations);
    }

    public void WriteCommittedRegister(byte virtualThreadId, int registerId, ulong value)
    {
        Processor.CPU_Cores[CoreId].WriteCommittedArch(virtualThreadId, registerId, value);
    }

    public void PrepareExecutionStart(ulong pc)
    {
        Processor.CurrentProcessorMode = ProcessorMode.Emulation;
        Processor.CPU_Cores[CoreId].PrepareExecutionStart(pc);
    }

    public void ConfigureFspForDiagnostics()
    {
        Processor.CPU_Cores[CoreId].VectorConfig.FSP_Enabled = 1;
        Processor.CPU_Cores[CoreId].VectorConfig.FSP_StealMask = 0xFF;

        var pod = Processor.GetPodForCore(CoreId);
        if (pod?.Scheduler is { } scheduler)
        {
            scheduler.TypedSlotEnabled = true;
        }
    }

    public void SetPipelineMode(bool enabled)
    {
        Processor.CPU_Cores[CoreId].SetPipelineMode(enabled);
    }

    public void ExecutePipelineCycle()
    {
        Processor.CPU_Cores[CoreId].ExecutePipelineCycle();
    }

    public ulong ReadActiveLivePc()
    {
        return Processor.CPU_Cores[CoreId].ReadActiveLivePc();
    }

    public int ReadActiveVirtualThreadId()
    {
        return Processor.CPU_Cores[CoreId].ReadActiveVirtualThreadId();
    }

    public Processor.CPU_Core.PipelineControl GetPipelineControl()
    {
        return Processor.CPU_Cores[CoreId].GetPipelineControl();
    }

    public Processor.CPU_Core.PipelineControl CapturePipelineControl()
    {
        return Processor.CPU_Cores != null &&
               CoreId < Processor.CPU_Cores.Length
            ? Processor.CPU_Cores[CoreId].GetPipelineControl()
            : default;
    }

    public PerformanceReport GetPerformanceStats()
    {
        return Processor.GetPerformanceStats();
    }

    public PerformanceReport CapturePerformanceStats()
    {
        try
        {
            return Processor.GetPerformanceStats();
        }
        catch
        {
            return new PerformanceReport();
        }
    }

    public Processor.CPU_Core GetCore()
    {
        return Processor.CPU_Cores[CoreId];
    }

    public void SetCore(Processor.CPU_Core core)
    {
        Processor.CPU_Cores[CoreId] = core;
    }

    public Processor.CPU_Core.LiveCpuStateAdapter CreateLiveCpuStateAdapter(byte virtualThreadId)
    {
        return Processor.CPU_Cores[CoreId].CreateLiveCpuStateAdapter(virtualThreadId);
    }

    public void ApplyLiveStateAdapter(Processor.CPU_Core.LiveCpuStateAdapter state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var core = GetCore();
        state.ApplyTo(ref core);
        SetCore(core);
    }
}
