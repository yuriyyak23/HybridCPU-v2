namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ad RaiseInterrupt producer/consumer inventory decision.</summary>
public sealed class Rf127adDmaRaiseInterruptInventoryDecisionTests
{
    [Fact]
    public void TwoPrivateRawProducersFeedDistinctInterruptRoutingPath()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel, bool isError)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private bool PerformBurst(", start, StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Equal(2, Count(source, "RaiseInterrupt(channel, isError:"));
        Assert.Contains("ushort interruptID = (ushort)(0x90 + channelID);", method,
            StringComparison.Ordinal);
        Assert.Contains("interruptID = (ushort)(0xA0 + channelID);", method,
            StringComparison.Ordinal);
        Assert.Contains("if (isError)", method, StringComparison.Ordinal);
        Assert.Contains("_interruptDispatch(Processor.DeviceType.DMAController, interruptID, 0);",
            method, StringComparison.Ordinal);
        Assert.Contains("Processor.InterruptData.CallInterrupt(", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeviceInterruptAndCoreRoutingRemainSeparateFamilies()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel, bool isError)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private bool PerformBurst(", start, StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("Processor.DeviceType.DMAController", method,
            StringComparison.Ordinal);
        Assert.Contains("ushort interruptID", method, StringComparison.Ordinal);
        Assert.Contains("0 // Core ID 0 for now", method, StringComparison.Ordinal);
        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedChannelDoesNotConferInterruptAuthority()
    {
        string identifier = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DmaChannelId.cs"));
        Assert.Contains("does not grant", identifier, StringComparison.Ordinal);
        Assert.Contains("interrupt", identifier, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

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
