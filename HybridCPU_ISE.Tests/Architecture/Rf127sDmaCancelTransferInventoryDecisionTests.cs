using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7s CancelTransfer channel producer/consumer inventory.</summary>
public sealed class Rf127sDmaCancelTransferInventoryDecisionTests
{
    [Fact]
    public void RawCancellationKeepsCallbackEventAndResetConsumersDistinct()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public bool CancelTransfer(byte channelID)", StringComparison.Ordinal);
        int end = source.IndexOf("private void OnTransferCompleted(byte channelID", start,
            StringComparison.Ordinal);
        string method = source[start..end];

        Assert.Contains("if (channelID >= MAX_CHANNELS)", method, StringComparison.Ordinal);
        Assert.Contains("ref ChannelControl ch = ref channels[channel];", method,
            StringComparison.Ordinal);
        Assert.Contains("ch.Callback?.Invoke(channelID, false, 0xFF);", method,
            StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID, true, 0xFF, ch.BytesTransferred);",
            method, StringComparison.Ordinal);
        Assert.Contains("ResetChannel(channelID);", method, StringComparison.Ordinal);
        Assert.Equal(2, Count(method, "return false;"));
        Assert.Equal(1, Count(method, "return true;"));
    }

    [Fact]
    public void InvalidAndIdleRawInputsRetainFalseWhileActiveCancelsToIdle()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 0
        };

        Assert.False(dma.CancelTransfer(8));
        Assert.False(dma.CancelTransfer(0));
        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.True(dma.StartTransfer(0));
        Assert.True(dma.CancelTransfer(0));
        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(0));
    }

    [Fact]
    public void PaperKeepsChannelRepresentationControllerLocal()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("It is not a memory request, stream, device, queue, or token identity.",
            paper, StringComparison.Ordinal);
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
