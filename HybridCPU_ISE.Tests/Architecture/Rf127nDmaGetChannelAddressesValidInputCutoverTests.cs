using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7n GetChannelAddresses post-validation projection.</summary>
public sealed class Rf127nDmaGetChannelAddressesValidInputCutoverTests
{
    [Fact]
    public void ValidRawChannelReadsAddressesThroughCheckedProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1234, DestAddress = 0x5678, TransferSize = 8,
            ElementSize = 1, ChannelID = 7
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.Equal((0x1234ul, 0x5678ul), dma.GetChannelAddresses(7));
    }

    [Fact]
    public void InvalidRawChannelRetainsZeroTupleBeforeProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        Assert.Equal((0ul, 0ul), dma.GetChannelAddresses(8));
    }

    [Fact]
    public void SourceProjectsOnlyAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public (ulong srcAddr, ulong dstAddr) GetChannelAddresses(byte channelID)",
            StringComparison.Ordinal);
        int gate = source.IndexOf("if (channelID >= MAX_CHANNELS)", start, StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(channelID);",
            start, StringComparison.Ordinal);

        Assert.True(gate >= start && projection > gate);
        Assert.Contains("return (0, 0);", source[gate..projection], StringComparison.Ordinal);
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
