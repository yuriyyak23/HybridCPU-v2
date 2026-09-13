namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7z ProcessChannel raw-consumer closure decision.</summary>
public sealed class Rf127zDmaProcessChannelRawConsumerClosureTests
{
    [Fact]
    public void RawCompletionAndPublicationConsumersRemainDistinct()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void ProcessChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private void CompleteChannel(DmaChannelId channel)", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
        Assert.Contains("CompleteChannel(channel);", method, StringComparison.Ordinal);
        Assert.Contains("ch.Callback?.Invoke(channelID, false, ch.ErrorCode);", method,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, true, ch.ErrorCode, ch.BytesTransferred);",
            method, StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: true);", method,
            StringComparison.Ordinal);

        Assert.True(Order(method, "ch.Callback?.Invoke", "OnTransferCompleted"));
        Assert.True(Order(method, "OnTransferCompleted", "RaiseInterrupt"));
    }

    [Fact]
    public void ConsumerDeclarationsRemainRawCompatibilityOrMutationBoundaries()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));

        Assert.Contains("private void CompleteChannel(DmaChannelId channel)", source,
            StringComparison.Ordinal);
        Assert.Contains("private void OnTransferCompleted(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("private void RaiseInterrupt(DmaChannelId channel, bool isError)", source,
            StringComparison.Ordinal);
        Assert.Contains("public delegate void TransferCompletionCallback(byte channelID", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedRepresentationDoesNotGrantCompletionOrPublicationAuthority()
    {
        string identifier = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DmaChannelId.cs"));
        Assert.Contains("completion, interrupt, replay, retirement or publication authority",
            identifier, StringComparison.Ordinal);
    }

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
