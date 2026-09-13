namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7x ProcessChannel producer/consumer inventory decision.</summary>
public sealed class Rf127xDmaProcessChannelInventoryDecisionTests
{

    [Fact]
    public void TransferExecutionAndPublicationOwnersRemainOutsideRepresentation()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int processStart = source.IndexOf("private void ProcessChannel(DmaChannelId channel)", StringComparison.Ordinal);
        int processEnd = source.IndexOf("private void CompleteChannel(DmaChannelId channel)", processStart,
            StringComparison.Ordinal);
        string process = source[processStart..processEnd];

        Assert.Contains("PerformBurst(srcAddr, dstAddr, burstBytes, desc.UseIOMMU);", process,
            StringComparison.Ordinal);
        Assert.Contains("ch.State = ChannelState.Error;", process, StringComparison.Ordinal);
        Assert.Contains("totalErrors++;", process, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedChannelRemainsRepresentationOnly()
    {
        string identifier = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DmaChannelId.cs"));
        Assert.Contains("does not grant", identifier, StringComparison.Ordinal);
        Assert.Contains("channel availability, execution,", identifier,
            StringComparison.Ordinal);
        Assert.Contains("completion, interrupt,", identifier,
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
