using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09FallbackDiagnosticsMirrorBoundaryTests
{
    [Fact]
    public void DiagnosticsSnapshotType_IsNamedAsReadOnlyMirror()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string snapshotPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "RuntimeClusterAdmissionPreparation.FallbackDiagnosticsSnapshot.cs");
        string snapshotText = File.ReadAllText(snapshotPath);

        Assert.Contains("ClusterFallbackDiagnosticsSnapshot", snapshotText, StringComparison.Ordinal);
        Assert.Contains("Read-only diagnostics snapshot", snapshotText, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeClusterFallbackDiagnostics", snapshotText, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticAdmissionOwners_DoNotReadFallbackDiagnosticsSnapshot()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            patterns:
            [
                "ClusterFallbackDiagnosticsSnapshot",
                "FallbackDiagnosticsSnapshot",
                "SuggestsFallbackDiagnostics",
                "FallbackDiagnosticsMask"
            ],
            allowedRelativePaths: Array.Empty<string>(),
            productionRootRelativePaths:
            [
                @"HybridCPU_ISE\Core\Pipeline\Core\RuntimeClusterAdmissionPreparation.DecisionDraft.cs",
                @"HybridCPU_ISE\Core\Pipeline\Core\RuntimeClusterAdmissionPreparation.BundleIssuePacket.cs",
                @"HybridCPU_ISE\Core\Pipeline\Core\RuntimeClusterAdmissionPreparation.BundleIssueFallbackInfo.cs",
                @"HybridCPU_ISE\Core\Pipeline\Core\RuntimeClusterAdmissionPreparation.Handoff.cs",
                @"HybridCPU_ISE\Core\Pipeline\Core\CPU_Core.PipelineExecution.Materialization.cs"
            ]);

        Assert.Empty(violations);
    }
}
