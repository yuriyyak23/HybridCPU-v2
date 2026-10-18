using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ai residual DmaChannelId raw-surface inventory decision.</summary>
public sealed class Rf127aiDmaResidualRawSurfaceInventoryTests
{
    [Fact]
    public void RawPublicCompatibilityAndDescriptorSurfacesAreExplicitlyFrozen()
    {
        string source = ReadDmaController();

        Assert.Contains("public byte ChannelID { get; set; }", source,
            StringComparison.Ordinal);
        Assert.Contains("public delegate void TransferCompletionCallback(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("public byte ChannelID;", source, StringComparison.Ordinal);
        Assert.Contains("public bool ConfigureTransfer(TransferDescriptor desc", source,
            StringComparison.Ordinal);
        Assert.Equal(8, Regex.Matches(source, @"public\s+(?:bool|void|\([^)]+\)|ChannelState)\s+\w+\(byte channelID").Count);
        Assert.Contains("private void OnTransferCompleted(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("byte channelID = channel;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerIndexesAreCheckedAndRawCompatibilityFormsDoNotIndex()
    {
        string source = ReadDmaController();

        Assert.DoesNotContain("channels[channelID]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("channels[desc.ChannelID]", source, StringComparison.Ordinal);
        Assert.Contains("channels[channel]", source, StringComparison.Ordinal);
        Assert.Equal(8, Regex.Matches(source, @"channelID\s*>=\s*MAX_CHANNELS").Count);
        Assert.Equal(1, Regex.Matches(source, @"desc\.ChannelID\s*>=\s*MAX_CHANNELS").Count);
    }

    [Fact]
    public void NoDmaFamilyJsonReflectionOrTestSupportMutationSeamExists()
    {
        string root = Root();
        string dmaDirectory = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "DMA");
        string allDma = string.Join("\n", Directory.EnumerateFiles(dmaDirectory, "*.cs")
            .Select(File.ReadAllText));

        Assert.DoesNotContain("JsonSerializer", allDma, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", allDma, StringComparison.Ordinal);
        Assert.DoesNotContain("TestSupport", allDma, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\bChannelId\b"), allDma);
    }

    [Fact]
    public void PaperSeparatesDmaChannelFromOtherIdentifierFamilies()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("DmaChannelId", paper, StringComparison.Ordinal);
        Assert.Contains("not a memory request, stream, device, queue, or token identity",
            paper, StringComparison.Ordinal);
        Assert.Contains("Zero is valid channel 0. Absence is controller state or an outer result.",
            paper, StringComparison.Ordinal);
    }

    private static string ReadDmaController() => File.ReadAllText(Path.Combine(Root(),
        "HybridCPU_ISE", "CloseToHSL", "Memory", "DMA", "DMAController.cs"));

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
