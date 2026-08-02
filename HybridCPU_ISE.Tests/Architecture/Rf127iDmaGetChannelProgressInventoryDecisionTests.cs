using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7i GetChannelProgress channel producer/consumer inventory.</summary>
public sealed class Rf127iDmaGetChannelProgressInventoryDecisionTests
{

    [Fact]
    public void ValidAndInvalidRawQueriesKeepTheirEstablishedTupleForms()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.Equal((0u, 0u), dma.GetChannelProgress(0));
        Assert.Equal((0u, 0u), dma.GetChannelProgress(8));
    }

    [Fact]
    public void PaperKeepsChannelRepresentationControllerLocal()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("DmaChannelId", paper, StringComparison.Ordinal);
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Absence is controller state or an outer result.", paper,
            StringComparison.Ordinal);
    }

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
