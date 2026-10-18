namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7aa CompleteChannel producer/consumer inventory decision.</summary>
public sealed class Rf127aaDmaCompleteChannelInventoryDecisionTests
{
    [Fact]
    public void PrivateCompletionHasOneProducerAndDistinctStatePublicationConsumers()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int processStart = source.IndexOf("private void ProcessChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int completeStart = source.IndexOf("private void CompleteChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int completeEnd = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel",
            completeStart, StringComparison.Ordinal);
        string process = source[processStart..completeStart];
        string complete = source[completeStart..completeEnd];

        Assert.Contains("if (remaining == 0)", process, StringComparison.Ordinal);
        Assert.Contains("CompleteChannel(channel);", process, StringComparison.Ordinal);
        Assert.Contains("byte channelID = channel;", complete, StringComparison.Ordinal);
        Assert.Contains("ref ChannelControl ch = ref channels[channel];", complete,
            StringComparison.Ordinal);
        Assert.Equal(2, Count(complete, "ch.State = ChannelState.Completed;"));
        Assert.Contains("ch.CurrentDesc.NextDescriptor != 0", complete,
            StringComparison.Ordinal);
        Assert.Contains("ch.Callback?.Invoke(channelID, true, 0);", complete,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, false, 0, ch.BytesTransferred);",
            complete, StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: false);", complete,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessPublicationOrderAndRawIdentityAreFrozen()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void CompleteChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.True(Order(method, "ch.Callback?.Invoke", "OnTransferCompleted"));
        Assert.True(Order(method, "OnTransferCompleted", "RaiseInterrupt"));
        Assert.Contains("private void OnTransferCompleted(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("private void RaiseInterrupt(DmaChannelId channel, bool isError)", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedChannelHasNoCompletionAuthority()
    {
        string identifier = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DmaChannelId.cs"));
        Assert.Contains("does not grant", identifier, StringComparison.Ordinal);
        Assert.Contains("completion, interrupt", identifier, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static bool Order(string source, string first, string second) =>
        source.IndexOf(first, StringComparison.Ordinal) < source.IndexOf(second, StringComparison.Ordinal);

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
