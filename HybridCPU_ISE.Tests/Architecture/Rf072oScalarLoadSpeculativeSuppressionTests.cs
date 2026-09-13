using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072oScalarLoadSpeculativeSuppressionTests
{
    [Fact]
    public void FaultedSpeculativeLoad_ProjectsNoEffectStructuralBlocked()
    {
        var load = new LoadMicroOp { Address = 0x400, Size = 8 };
        load.MarkSpeculative();
        load.MarkFaulted();

        ExecutionOutcome outcome =
            Processor.CPU_Core.ProjectSingleLaneScalarLoadSpeculativeSuppressionOutcome(load, false);

        Assert.True(load.IsSpeculativeFaultSuppressed);
        Assert.Equal(ExecutionOutcomeKind.StructuralBlocked, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.SpeculativeFaultSuppressed, outcome.Diagnostic!.Code);
        Assert.Null(outcome.Result);
        Assert.False(outcome.HasArchitecturalEffects);
    }

    [Fact]
    public void NonFaultedOrCompletedLoad_FailsClosed()
    {
        var load = new LoadMicroOp { Address = 0x400, Size = 8 };
        load.MarkSpeculative();
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarLoadSpeculativeSuppressionOutcome(load, false));
        load.MarkFaulted();
        Assert.Throws<ExecutionOutcomeContractViolationException>(
            () => Processor.CPU_Core.ProjectSingleLaneScalarLoadSpeculativeSuppressionOutcome(load, true));
    }
}
