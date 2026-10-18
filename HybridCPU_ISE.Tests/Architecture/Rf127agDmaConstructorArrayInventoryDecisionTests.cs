using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ag DMAController constructor-array inventory decision.</summary>
public sealed class Rf127agDmaConstructorArrayInventoryDecisionTests
{
    [Fact]
    public void BoundedConstructorLoopInitializesEveryControllerChannel()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public DMAController(", StringComparison.Ordinal);
        int end = source.IndexOf("public bool ConfigureTransfer(", start, StringComparison.Ordinal);
        string constructor = source[start..end];

        Assert.Contains("this.channels = new ChannelControl[MAX_CHANNELS];", constructor,
            StringComparison.Ordinal);
        Assert.Contains("for (int i = 0; i < MAX_CHANNELS; i++)", constructor,
            StringComparison.Ordinal);
        Assert.Contains("DmaChannelId channel = DmaChannelId.Create((byte)i);", constructor,
            StringComparison.Ordinal);
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
    public void ChannelZeroIsInitializedAndRemainsValidNotAbsence()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
        Assert.Equal((0u, 0u), dma.GetChannelProgress(0));
    }

    [Fact]
    public void PaperKeepsZeroAsValidControllerLocalChannel()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero is valid channel 0.", paper, StringComparison.Ordinal);
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
