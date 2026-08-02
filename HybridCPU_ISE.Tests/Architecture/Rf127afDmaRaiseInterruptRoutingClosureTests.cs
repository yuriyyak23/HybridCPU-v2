namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7af RaiseInterrupt routing-consumer closure decision.</summary>
public sealed class Rf127afDmaRaiseInterruptRoutingClosureTests
{
    [Fact]
    public void InterruptIdDeviceCoreAndDispatchRemainSeparateFromChannelRepresentation()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel, bool isError)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private bool PerformBurst(", start, StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
        Assert.Contains("ushort interruptID = (ushort)(0x90 + channelID);", method,
            StringComparison.Ordinal);
        Assert.Contains("if (isError)", method, StringComparison.Ordinal);
        Assert.Contains("interruptID = (ushort)(0xA0 + channelID);", method,
            StringComparison.Ordinal);
        Assert.Contains("Processor.DeviceType.DMAController", method,
            StringComparison.Ordinal);
        Assert.Contains("_interruptDispatch(Processor.DeviceType.DMAController, interruptID, 0);",
            method, StringComparison.Ordinal);
        Assert.Contains("Processor.InterruptData.CallInterrupt(", method,
            StringComparison.Ordinal);
        Assert.Contains("0 // Core ID 0 for now", method, StringComparison.Ordinal);
    }

    [Fact]
    public void BothCheckedProducersRetainErrorAndSuccessRouting()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        Assert.Contains("RaiseInterrupt(channel, isError: true);", source,
            StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: false);", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PaperExcludesUniversalDeviceDomainOrTokenIdentity()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("no universal", paper, StringComparison.Ordinal);
        Assert.Contains("not a memory request, stream, device, queue, or token identity",
            paper, StringComparison.Ordinal);
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
