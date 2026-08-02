using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7f StartTransfer post-validation channel projection.</summary>
public sealed class Rf127fDmaStartTransferValidInputCutoverTests
{
    [Fact]
    public void ValidRawConfiguredChannelStartsThroughCheckedLocalProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000,
            DestAddress = 0x2000,
            TransferSize = 16,
            ElementSize = 1,
            ChannelID = 7
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.True(dma.StartTransfer(7));
        Assert.Equal(DMAController.ChannelState.Active, dma.GetChannelState(7));
    }

    [Fact]
    public void InvalidAndUnconfiguredRawOutcomesRemainUnchanged()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.False(dma.StartTransfer(8));
        Assert.Equal(DMAController.ChannelState.Error, dma.GetChannelState(8));
        Assert.False(dma.StartTransfer(0));
        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
    }

    [Fact]
    public void SourceCreatesCheckedValueOnlyAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public bool StartTransfer(byte channelID)", StringComparison.Ordinal);
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
