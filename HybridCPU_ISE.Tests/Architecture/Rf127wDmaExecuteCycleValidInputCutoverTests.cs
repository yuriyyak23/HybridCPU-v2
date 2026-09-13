using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7w ExecuteCycle bounded controller-array projection.</summary>
public sealed class Rf127wDmaExecuteCycleValidInputCutoverTests
{
    [Fact]
    public void SourceProjectsOnlyBoundedLoopArrayAccesses()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public void ExecuteCycle()", StringComparison.Ordinal);
        int end = source.IndexOf("private void ProcessChannel(DmaChannelId channel)", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        int loop = method.IndexOf("for (int ch = 0; ch < MAX_CHANNELS; ch++)",
            StringComparison.Ordinal);
        int projection = method.IndexOf("DmaChannelId channel = DmaChannelId.Create((byte)ch);",
            StringComparison.Ordinal);
        int handoff = method.IndexOf("ProcessChannel(channel);",
            StringComparison.Ordinal);

        Assert.True(loop >= 0 && projection > loop && handoff > projection);
        Assert.Contains("channels[channel].State == ChannelState.Active", method,
            StringComparison.Ordinal);
        Assert.Contains("channels[channel].CurrentDesc.Priority", method,
            StringComparison.Ordinal);
        Assert.Contains("int selectedChannel = -1;", method, StringComparison.Ordinal);
        Assert.Contains("selectedChannel == -1 || channelPriority > highestPriority", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedLoopAndNoSelectionStateRemainRawTopology()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public void ExecuteCycle()", StringComparison.Ordinal);
        int end = source.IndexOf("private void ProcessChannel(DmaChannelId channel)", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("for (int ch = 0; ch < MAX_CHANNELS; ch++)", method,
            StringComparison.Ordinal);
        Assert.Contains("if (selectedChannel >= 0)", method, StringComparison.Ordinal);
        Assert.Contains("ProcessChannel(channel);", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperRetainsControllerLocalDmaChannelFamily()
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
