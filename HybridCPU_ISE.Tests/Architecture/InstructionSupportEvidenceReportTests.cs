using System.Linq;
using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class InstructionSupportEvidenceReportTests
{
    [Fact]
    public void GeneratedReport_IsVersionedReadOnlyEvidenceProjection()
    {
        InstructionSupportEvidenceReport report = InstructionSupportStatusCatalog.GeneratedEvidenceReport;

        Assert.Equal(1, report.Version);
        Assert.Equal("instruction-support-declaration-evidence", report.Kind);
        Assert.False(report.IsRuntimeAuthority);
        Assert.Equal(InstructionSupportStatusCatalog.ExplicitStatuses.Count, report.Rows.Count);
        Assert.NotSame(InstructionSupportStatusCatalog.ExplicitStatuses, report.Rows);
        Assert.Equal(
            InstructionSupportStatusCatalog.ExplicitStatuses.Select(status => status.Mnemonic),
            report.Rows.Select(status => status.Mnemonic));
    }

    [Fact]
    public void GeneratedReport_DoesNotPromoteDeclaredOrClosedRowsToExecutionAuthority()
    {
        InstructionSupportEvidenceReport report = InstructionSupportStatusCatalog.GeneratedEvidenceReport;

        Assert.False(report.IsRuntimeAuthority);
        Assert.Contains(report.Rows, status =>
            status.RuntimeEvidence == RuntimeInstructionEvidence.DeclaredOnly &&
            !status.IsExecutableClaim);
        Assert.Contains(report.Rows, status =>
            status.RuntimeEvidence == RuntimeInstructionEvidence.None &&
            !status.IsExecutableClaim);
    }
}
