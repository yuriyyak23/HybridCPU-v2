using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07OutcomeDifferentialTests
{
    private sealed class RetryWithoutMutationMicroOp : MicroOp
    {
        public RetryWithoutMutationMicroOp()
        {
            Class = MicroOpClass.Lsu;
            InstructionClass = InstructionClass.Memory;
            SerializationClass = SerializationClass.MemoryOrdered;
            SetClassFlexiblePlacement(SlotClass.LsuClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => false;

        public override string GetDescription() => "synthetic no-mutation wait";
    }

    [Fact]
    public void SuccessfulExecution_ProjectsToCompletedWithResultParity()
    {
        var core = new Processor.CPU_Core(0);
        var nop = new NopMicroOp();
        bool legacySuccess = nop.Execute(ref core);

        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectSuccessfulExecution(
            legacySuccess,
            ExecutionResultContract.WithoutScalarResult());

        Assert.True(legacySuccess);
        Assert.Equal(ExecutionOutcomeKind.Completed, outcome.Kind);
        Assert.NotNull(outcome.Result);
        Assert.Null(outcome.Diagnostic);
    }

    [Fact]
    public void RetryWait_ProjectsToRetryableAndHasNoArchitecturalMutation()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;
        var retry = new RetryWithoutMutationMicroOp();
        bool legacySuccess = retry.Execute(ref core);

        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectKnownRetry(
            legacySuccess,
            "memory completion remains pending");

        Assert.False(legacySuccess);
        Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
        Assert.False(outcome.HasArchitecturalEffects);
        Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
    }

    [Fact]
    public void PageFault_ProjectsToTypedArchitecturalFault()
    {
        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(
            new PageFaultException(0xCAFE, isWrite: false));

        Assert.Equal(ExecutionOutcomeKind.ArchitecturalFault, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.PageFault, outcome.Diagnostic!.Code);
        Assert.Equal(0xCAFEUL, outcome.Diagnostic.FaultAddress);
        Assert.Equal(false, outcome.Diagnostic.FaultIsWrite);
        Assert.False(outcome.HasArchitecturalEffects);
    }

    [Fact]
    public void AlignmentFault_ProjectsToTypedArchitecturalFaultWithoutChangingDeliveryPolicy()
    {
        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(
            new MemoryAlignmentException(0x1003, 4, "SYNTH"),
            alignmentIsWrite: true);

        Assert.Equal(ExecutionOutcomeKind.ArchitecturalFault, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.AlignmentFault, outcome.Diagnostic!.Code);
        Assert.Equal(0x1003UL, outcome.Diagnostic.FaultAddress);
        Assert.Equal(true, outcome.Diagnostic.FaultIsWrite);
    }

    [Fact]
    public void UnknownException_ProjectsOnlyToFatalInvariantViolationNeverRetryOrNotReady()
    {
        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(
            new InvalidOperationException("synthetic unknown execute exception"));

        Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.UnknownException, outcome.Diagnostic!.Code);
        Assert.Null(outcome.Result);
        Assert.NotEqual(ExecutionOutcomeKind.Retryable, outcome.Kind);
        Assert.NotEqual(ExecutionOutcomeKind.StructuralBlocked, outcome.Kind);
    }

    [Fact]
    public void UnknownException_WithEmptyMessage_StillProjectsFailClosed()
    {
        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(
            new InvalidOperationException());

        Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.UnknownException, outcome.Diagnostic!.Code);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Diagnostic.Reason));
    }

    [Fact]
    public void ExistingFailClosedException_PreservesLegacyCategoryInsideFatalDiagnostic()
    {
        InvalidOperationException exception = ExecutionFaultContract.CreateWrappedException(
            ExecutionFaultCategory.InvalidInternalOp,
            "known fail-closed execution fault",
            new InvalidOperationException("inner"));

        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(exception);

        Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.ExistingExecutionFault, outcome.Diagnostic!.Code);
        Assert.Equal(ExecutionFaultCategory.InvalidInternalOp, outcome.Diagnostic.LegacyFaultCategory);
    }
}
