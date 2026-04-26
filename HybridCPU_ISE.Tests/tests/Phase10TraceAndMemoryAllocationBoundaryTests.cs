using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase10;

public sealed class Phase10TraceAndMemoryAllocationBoundaryTests
{
    [Fact]
    public void TraceFullStateCapture_IsGatedBeforePipelineBuildsSnapshotPayloads()
    {
        string repoRoot = FindRepoRoot();
        string traceSinkPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Diagnostics",
            "TraceSink.cs");
        string pipelineExecutionPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.cs");
        string pipelineExecutionTracePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Trace.cs");

        string traceSinkSource = File.ReadAllText(traceSinkPath);
        string pipelineExecutionSource = File.ReadAllText(pipelineExecutionPath);
        string pipelineExecutionTraceSource = File.ReadAllText(pipelineExecutionTracePath);
        string combinedPipelineTraceSource = pipelineExecutionSource + Environment.NewLine + pipelineExecutionTraceSource;

        Assert.Contains(
            "public bool ShouldCaptureFullState => enabled && level == TraceLevel.Full;",
            traceSinkSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (traceSink.ShouldCaptureFullState)",
            combinedPipelineTraceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "private void RecordTraceEvent(",
            pipelineExecutionTraceSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitPacketReadPaths_UseReusableBuffersInsteadOfAdHocAccessSizeArrays()
    {
        string repoRoot = FindRepoRoot();
        string pipelinePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.cs");
        string pipelineExecutionPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.cs");
        string pipelineExecutionMemoryPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.PipelineExecution.Memory.cs");
        string scalarMemoryLanePath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.Pipeline.Stages.ScalarMemoryLaneState.cs");

        string pipelineSource = File.ReadAllText(pipelinePath);
        string pipelineExecutionSource = File.ReadAllText(pipelineExecutionPath);
        string pipelineExecutionMemorySource = File.ReadAllText(pipelineExecutionMemoryPath);
        string scalarMemoryLaneSource = File.ReadAllText(scalarMemoryLanePath);
        string combinedPipelineExecutionSource = pipelineExecutionSource + Environment.NewLine + pipelineExecutionMemorySource;

        Assert.Contains("_explicitPacketImmediateReadBuffer", pipelineSource, StringComparison.Ordinal);
        Assert.Contains("ReadExplicitPacketLoadIntoReusableBuffer(", combinedPipelineExecutionSource, StringComparison.Ordinal);
        Assert.Contains("PendingReadBuffer", scalarMemoryLaneSource, StringComparison.Ordinal);
        Assert.Contains("lane.PendingReadBuffer = new byte[accessSize];", combinedPipelineExecutionSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ReadFromPosition(new byte[accessSize], address, accessSize);",
            combinedPipelineExecutionSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new byte[accessSize]);",
            combinedPipelineExecutionSource,
            StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
