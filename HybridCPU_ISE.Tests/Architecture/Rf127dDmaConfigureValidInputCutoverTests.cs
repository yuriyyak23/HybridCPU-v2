using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7d ConfigureTransfer post-validation channel projection.</summary>
public sealed class Rf127dDmaConfigureValidInputCutoverTests
{
    [Fact]
    public void ValidRawChannelConfiguresThroughCheckedLocalProjection()
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
        Assert.Equal(DMAController.ChannelState.Configured, dma.GetChannelState(7));
    }

    [Fact]
    public void InvalidRawChannelRetainsFalseOutcomeBeforeCheckedProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000,
            DestAddress = 0x2000,
            TransferSize = 16,
            ElementSize = 1,
            ChannelID = 8
        };

        Assert.False(dma.ConfigureTransfer(descriptor));
        Assert.Equal(DMAController.ChannelState.Error, dma.GetChannelState(8));
    }

    [Fact]
    public void SourceKeepsRawApiAndCreatesCheckedValueOnlyAfterExistingGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int gate = source.IndexOf("if (desc.ChannelID >= MAX_CHANNELS)", StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(desc.ChannelID);",
            StringComparison.Ordinal);

        Assert.True(gate >= 0 && projection > gate);
        Assert.Contains("public bool ConfigureTransfer(TransferDescriptor desc", source,
            StringComparison.Ordinal);
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
