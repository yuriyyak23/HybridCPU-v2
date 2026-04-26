using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09BranchMaterializationConvergenceTests
{
    [Fact]
    public void BranchExecuteContour_UsesSharedPayloadAndMaterializationOnlyHelpers()
    {
        string repoRoot = FindRepoRoot();
        string controlFlowPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.ControlFlow.cs");
        string executeHelpersPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string pipelineExecutionPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.cs");
        string branchMicroOpPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "MicroOps",
            "MicroOp.Control.cs");

        string controlFlowText = File.ReadAllText(controlFlowPath);
        string executeHelpersText = File.ReadAllText(executeHelpersPath);
        string pipelineExecutionText = File.ReadAllText(pipelineExecutionPath);
        string branchMicroOpText = File.ReadAllText(branchMicroOpPath);

        Assert.Contains("BranchExecutionPayload", branchMicroOpText, StringComparison.Ordinal);
        Assert.Contains("ResolveExecutionPayload(", branchMicroOpText, StringComparison.Ordinal);

        Assert.Contains("ResolveBranchExecutionPayload(", controlFlowText, StringComparison.Ordinal);
        Assert.Contains("MaterializeBranchExecuteCarrier(", controlFlowText, StringComparison.Ordinal);
        Assert.DoesNotContain("EvaluateBranchCondition(", controlFlowText, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveConditionalTargetAddress(", controlFlowText, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveUnconditionalTargetAddress(", controlFlowText, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureResolvedRetireTargetAddress(", controlFlowText, StringComparison.Ordinal);
        Assert.DoesNotContain("TryExecuteExplicitPacketLane7ConditionalBranch(", controlFlowText, StringComparison.Ordinal);

        Assert.DoesNotContain("TryExecuteExplicitPacketLane7UnconditionalBranch(", executeHelpersText, StringComparison.Ordinal);

        Assert.Contains("TryExecuteExplicitPacketLane7Branch(", pipelineExecutionText, StringComparison.Ordinal);
        Assert.DoesNotContain("TryExecuteExplicitPacketLane7ConditionalBranch(", pipelineExecutionText, StringComparison.Ordinal);
        Assert.DoesNotContain("TryExecuteExplicitPacketLane7UnconditionalBranch(", pipelineExecutionText, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
