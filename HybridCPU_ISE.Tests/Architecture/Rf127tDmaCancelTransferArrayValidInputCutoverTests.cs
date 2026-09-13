using YAKSys_Hybrid_CPU;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7t CancelTransfer post-validation channel projection.</summary>
public sealed class Rf127tDmaCancelTransferArrayValidInputCutoverTests
{
    [Fact]
    public void ValidActiveChannelCancelsThroughCheckedProjection()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);
        var descriptor = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000, DestAddress = 0x2000, TransferSize = 8,
            ElementSize = 1, ChannelID = 7
        };

        Assert.True(dma.ConfigureTransfer(descriptor));
        Assert.True(dma.StartTransfer(7));
        Assert.True(dma.CancelTransfer(7));
        Assert.Equal(DMAController.ChannelState.Idle, dma.GetChannelState(7));
    }

    [Fact]
    public void InvalidAndIdleRawOutcomesRemainFalse()
    {
        Processor processor = default;
        var dma = new DMAController(ref processor);

        Assert.False(dma.CancelTransfer(8));
        Assert.False(dma.CancelTransfer(0));
    }

    [Fact]
    public void SourceProjectsOnlyArrayAccessAfterExistingRangeGate()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
            "CloseToHSL", "Memory", "DMA", "DMAController.cs"));
        int start = source.IndexOf("public bool CancelTransfer(byte channelID)", StringComparison.Ordinal);
        int gate = source.IndexOf("if (channelID >= MAX_CHANNELS)", start, StringComparison.Ordinal);
        int projection = source.IndexOf("DmaChannelId channel = DmaChannelId.Create(channelID);",
            start, StringComparison.Ordinal);
        int arrayAccess = source.IndexOf("ref ChannelControl ch = ref channels[channel];",
            start, StringComparison.Ordinal);
        int callback = source.IndexOf("ch.Callback?.Invoke(channelID, false, 0xFF);",
            start, StringComparison.Ordinal);
        int completion = source.IndexOf("OnTransferCompleted(channelID, true, 0xFF, ch.BytesTransferred);",
            start, StringComparison.Ordinal);
        int reset = source.IndexOf("ResetChannel(channelID);", start, StringComparison.Ordinal);

        Assert.True(gate >= start && projection > gate && arrayAccess > projection);
        Assert.Contains("return false;", source[gate..projection], StringComparison.Ordinal);
        Assert.True(callback > arrayAccess && completion > callback && reset > completion);
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
