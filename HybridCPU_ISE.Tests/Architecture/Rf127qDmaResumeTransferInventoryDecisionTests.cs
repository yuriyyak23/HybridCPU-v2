using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7q ResumeTransfer channel producer/consumer inventory.</summary>
public sealed class Rf127qDmaResumeTransferInventoryDecisionTests
{

    [Fact]
    public void InvalidAndNonPausedRawInputsRetainFalseWhilePausedResumes()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 0
        };

        Assert.False(dma.ResumeTransfer(8));
        Assert.False(dma.ResumeTransfer(0));
        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.True(dma.StartTransfer(0));
        Assert.True(dma.PauseTransfer(0));
        Assert.True(dma.ResumeTransfer(0));
        Assert.Equal(DMAController.ChannelState.Active, dma.GetChannelState(0));
    }

    [Fact]
    public void PaperKeepsChannelRepresentationControllerLocal()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero is valid channel 0", paper, StringComparison.Ordinal);
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
