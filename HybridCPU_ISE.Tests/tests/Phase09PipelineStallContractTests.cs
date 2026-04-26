using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using YAKSys_Hybrid_CPU;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09PipelineStallContractTests
{
    [Theory]
    [InlineData(PipelineStallKind.None, "none", "Pipeline Stall", "Stalled", "STALLED")]
    [InlineData(PipelineStallKind.DataHazard, "data-hazard", "Data Hazard", "DataHazard", "STALLED (Data Hazard)")]
    [InlineData(PipelineStallKind.MemoryWait, "memory", "Memory Wait", "MemoryStall", "STALLED (Memory)")]
    [InlineData(PipelineStallKind.ControlHazard, "control", "Control Hazard", "ControlHazard", "STALLED (Control)")]
    [InlineData(PipelineStallKind.InvariantViolation, "invariant-violation", "Invariant Violation", "InvariantViolation", "STALLED (Invariant Violation)")]
    public void PipelineStallText_RenderingRemainsFrozenAcrossPresentationModes(
        PipelineStallKind kind,
        string expectedTrace,
        string expectedSnapshot,
        string expectedCompact,
        string expectedBanner)
    {
        Assert.Equal(expectedTrace, PipelineStallText.Render(kind, PipelineStallTextStyle.Trace));
        Assert.Equal(expectedSnapshot, PipelineStallText.Render(kind, PipelineStallTextStyle.Snapshot));
        Assert.Equal(expectedCompact, PipelineStallText.Render(kind, PipelineStallTextStyle.Compact));
        Assert.Equal(expectedBanner, PipelineStallText.Render(kind, PipelineStallTextStyle.Banner));
    }

    [Fact]
    public void NoMagicStallReason_ProductionSourcesUseTypedPipelineStallKind()
    {
        string repoRoot = FindRepoRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");

        var offendingLines = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                if (line.Contains("StallReason = 0", StringComparison.Ordinal) ||
                    line.Contains("StallReason = 1", StringComparison.Ordinal) ||
                    line.Contains("StallReason = 2", StringComparison.Ordinal) ||
                    line.Contains("StallReason = 3", StringComparison.Ordinal) ||
                    line.Contains("StallReason = 4", StringComparison.Ordinal))
                {
                    offendingLines.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                }
            }
        }

        Assert.Empty(offendingLines);
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
