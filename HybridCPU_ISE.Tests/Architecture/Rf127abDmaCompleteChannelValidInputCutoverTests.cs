namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ab CompleteChannel checked private-input cutover.</summary>
public sealed class Rf127abDmaCompleteChannelValidInputCutoverTests
{
    [Fact]
    public void ProcessChannelPassesItsCheckedValueOnlyAtRemainingZeroGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int process = source.IndexOf("private void ProcessChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int complete = source.IndexOf("private void CompleteChannel(DmaChannelId channel)",
            process, StringComparison.Ordinal);
        string method = source[process..complete];

        int gate = method.IndexOf("if (remaining == 0)", StringComparison.Ordinal);
        int handoff = method.IndexOf("CompleteChannel(channel);", StringComparison.Ordinal);
        Assert.True(gate >= 0 && handoff > gate);
    }

    [Fact]
    public void CompleteChannelUsesCheckedArrayReferenceButRawPublicationIdentity()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void CompleteChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
        Assert.Contains("ref ChannelControl ch = ref channels[channel];", method,
            StringComparison.Ordinal);
        Assert.Equal(2, Count(method, "ch.State = ChannelState.Completed;"));
        Assert.Contains("ch.Callback?.Invoke(channelID, true, 0);", method,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, false, 0, ch.BytesTransferred);",
            method, StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: false);", method,
            StringComparison.Ordinal);
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
