using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7h GetChannelState post-validation channel projection.</summary>
public sealed class Rf127hDmaGetChannelStateValidInputCutoverTests
{
    [Fact]
    public void ValidRawChannelReadsThroughCheckedLocalProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(7));
    }

    [Fact]
    public void InvalidRawChannelRetainsErrorOutcomeBeforeProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.Equal(DMAController.ChannelState.Error, dma.GetChannelState(8));
    }

    [Fact]
    public void SourceCreatesCheckedValueOnlyAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public ChannelState GetChannelState(byte channelID)",
            StringComparison.Ordinal);
        int gate = source.IndexOf("if (channelID >= MAX_CHANNELS)", start, StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(channelID);",
            start, StringComparison.Ordinal);

        Assert.True(gate >= start && projection > gate);
        Assert.Contains("return ChannelState.Error;", source[gate..projection],
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
