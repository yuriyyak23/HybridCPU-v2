namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ac CompleteChannel raw-consumer closure decision.</summary>
public sealed class Rf127acDmaCompleteChannelRawConsumerClosureTests
{
    [Fact]
    public void SuccessCompletionKeepsRawCompatibilityConsumersAndOrder()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("private void CompleteChannel(DmaChannelId channel)",
            StringComparison.Ordinal);
        int end = source.IndexOf("private void RaiseInterrupt(DmaChannelId channel", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("byte channelID = channel;", method, StringComparison.Ordinal);
        Assert.Contains("ch.Callback?.Invoke(channelID, true, 0);", method,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, false, 0, ch.BytesTransferred);",
            method, StringComparison.Ordinal);
        Assert.Contains("RaiseInterrupt(channel, isError: false);", method,
            StringComparison.Ordinal);
        Assert.True(Order(method, "ch.Callback?.Invoke", "OnTransferCompleted"));
        Assert.True(Order(method, "OnTransferCompleted", "RaiseInterrupt"));
    }

    [Fact]
    public void PublicCallbackAndEventAndPrivateInterruptRemainRawByteBoundaries()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));

        Assert.Contains("public delegate void TransferCompletionCallback(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("public byte ChannelID { get; set; }", source,
            StringComparison.Ordinal);
        Assert.Contains("private void OnTransferCompleted(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("private void RaiseInterrupt(DmaChannelId channel, bool isError)", source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedIdRemainsRepresentationalOnly()
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
