using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ah constructor bounded-array valid-input cutover.</summary>
public sealed class Rf127ahDmaConstructorArrayValidInputCutoverTests
{
    [Fact]
    public void SourceProjectsOnlyWithinExistingConstructorLoopBound()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public DMAController(", StringComparison.Ordinal);
        int end = source.IndexOf("public bool ConfigureTransfer(", start, StringComparison.Ordinal);
        string constructor = source[start..end];

        int loop = constructor.IndexOf("for (int i = 0; i < MAX_CHANNELS; i++)",
            StringComparison.Ordinal);
        int projection = constructor.IndexOf("DmaChannelId channel = DmaChannelId.Create((byte)i);",
            StringComparison.Ordinal);
        Assert.True(loop >= 0 && projection > loop);
        Assert.Contains("channels[channel].State = ChannelState.Idle;", constructor,
            StringComparison.Ordinal);
        Assert.Contains("channels[channel].BytesTransferred = 0;", constructor,
            StringComparison.Ordinal);
        Assert.Contains("channels[channel].TotalBytes = 0;", constructor,
            StringComparison.Ordinal);
        Assert.Contains("channels[channel].ErrorCode = 0;", constructor,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorPreservesValidZeroAndAllIdleState()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        for (byte channel = 0; channel < 8; channel++)
        {
            Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(channel));
            Assert.Equal((0u, 0u), dma.GetChannelProgress(channel));
        }
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
