using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072rCompletedTokenFailureProjectionTests
{
    [Theory]
    [InlineData(0x400UL, false)]
    [InlineData(0x408UL, true)]
    public void CompletedTokenFailurePageFault_ProjectsExactArchitecturalFault(
        ulong address,
        bool isWrite)
    {
        var failure = new PageFaultException(address, isWrite);

        ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(failure);

        Assert.Equal(ExecutionOutcomeKind.ArchitecturalFault, outcome.Kind);
        Assert.Equal(ExecutionDiagnosticCode.PageFault, outcome.Diagnostic!.Code);
        Assert.Equal(address, outcome.Diagnostic.FaultAddress);
        Assert.Equal(isWrite, outcome.Diagnostic.FaultIsWrite);
        Assert.Null(outcome.Result);
        Assert.False(outcome.HasArchitecturalEffects);
    }
}
