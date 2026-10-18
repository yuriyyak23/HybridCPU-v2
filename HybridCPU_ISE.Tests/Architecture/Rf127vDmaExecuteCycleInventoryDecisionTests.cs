using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7v ExecuteCycle DMA controller-loop inventory decision.</summary>
public sealed class Rf127vDmaExecuteCycleInventoryDecisionTests
{

    [Fact]
    public void SingleProductionCycleOwnerRemainsMemorySubsystem()
    {
        string helpers = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "Subsystem", "MemorySubsystem.Helpers.cs"));

        Assert.Equal(1, Count(helpers, "dma?.ExecuteCycle();"));
        Assert.Contains("internal void AdvanceBoundDmaAgentOneCycle()", helpers,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperMakesChannelZeroValidAndNoSelectionOuterState()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero is valid channel 0. Absence is controller state or an outer result.",
            paper, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
