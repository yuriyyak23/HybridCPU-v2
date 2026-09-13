using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7e StartTransfer channel producer/consumer inventory guard.</summary>
public sealed class Rf127eDmaStartTransferInventoryDecisionTests
{

    [Fact]
    public void InvalidAndUnconfiguredRawInputsRetainSeparateNoMutationOutcomes()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.False(dma.StartTransfer(8));
        Assert.Equal(DMAController.ChannelState.Error, dma.GetChannelState(8));
        Assert.False(dma.StartTransfer(0));
        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
    }

    [Fact]
    public void PaperPermitsOnlyControllerLocalChannelRepresentation()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("DmaChannelId", paper, StringComparison.Ordinal);
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("It is not a memory request, stream, device, queue, or token identity.",
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
