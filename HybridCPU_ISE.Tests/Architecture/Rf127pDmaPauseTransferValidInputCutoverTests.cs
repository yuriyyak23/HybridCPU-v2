using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7p PauseTransfer post-validation channel projection.</summary>
public sealed class Rf127pDmaPauseTransferValidInputCutoverTests
{
    [Fact]
    public void ValidActiveChannelPausesThroughCheckedProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 7
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.True(dma.StartTransfer(7));
        Assert.True(dma.PauseTransfer(7));
        Assert.Equal(DMAController.ChannelState.Paused, dma.GetChannelState(7));
    }

    [Fact]
    public void InvalidAndInactiveRawOutcomesRemainFalse()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.False(dma.PauseTransfer(8));
        Assert.False(dma.PauseTransfer(0));
    }

    [Fact]
    public void SourceProjectsOnlyAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public bool PauseTransfer(byte channelID)", StringComparison.Ordinal);
        int gate = source.IndexOf("if (channelID >= MAX_CHANNELS)", start, StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(channelID);",
            start, StringComparison.Ordinal);

        Assert.True(gate >= start && projection > gate);
        Assert.Contains("return false;", source[gate..projection], StringComparison.Ordinal);
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
