namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7y ProcessChannel checked private-input cutover.</summary>
public sealed class Rf127yDmaProcessChannelValidInputCutoverTests
{
    [Fact]
    public void SourceProjectsSelectedChannelAfterExistingNoSelectionGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int execute = source.IndexOf("public void ExecuteCycle()", StringComparison.Ordinal);
        int process = source.IndexOf("private void ProcessChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        string method = source[execute..process];

        int gate = method.IndexOf("if (selectedChannel >= 0)", StringComparison.Ordinal);
        int projection = method.IndexOf("DmaChannelId channel = DmaChannelId.Create((byte)selectedChannel);",
            StringComparison.Ordinal);
        int handoff = method.IndexOf("ProcessChannel(channel);", StringComparison.Ordinal);

        Assert.True(gate >= 0 && projection > gate && handoff > projection);
        Assert.Contains("for (int ch = 0; ch < MAX_CHANNELS; ch++)", method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessChannelUsesCheckedArrayReferenceButRawPublicationIdentity()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void ProcessChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private void CompleteChannel(DmaChannelId channel)", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
        Assert.Contains("ref ChannelControl ch = ref channels[channel];", method,
            StringComparison.Ordinal);
        Assert.Contains("CompleteChannel(channel);", method, StringComparison.Ordinal);
        Assert.Contains("ch.Callback?.Invoke(channelID, false, ch.ErrorCode);", method,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, true, ch.ErrorCode, ch.BytesTransferred);",
            method, StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: true);", method,
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
