using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7l ResetChannel post-validation channel projection.</summary>
public sealed class Rf127lDmaResetChannelValidInputCutoverTests
{
    [Fact]
    public void ValidRawChannelResetsThroughCheckedLocalProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 7
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        dma.ResetChannel(7);
        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(7));
        Assert.Equal((0u, 0u), dma.GetChannelProgress(7));
    }

    [Fact]
    public void InvalidRawChannelRemainsNoOpBeforeProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 0
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        dma.ResetChannel(8);
        Assert.Equal(DMAController.ChannelState.Configured, dma.GetChannelState(0));
    }

    [Fact]
    public void SourceProjectsOnlyAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public void ResetChannel(byte channelID)", StringComparison.Ordinal);
        int gate = source.IndexOf("if (channelID >= MAX_CHANNELS)", start, StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(channelID);",
            start, StringComparison.Ordinal);

        Assert.True(gate >= start && projection > gate);
        Assert.Contains("return;", source[gate..projection], StringComparison.Ordinal);
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
