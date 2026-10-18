using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7ak final DmaChannelId family closed-world exit audit.</summary>
public sealed class Rf127akDmaChannelFamilyExitAuditTests
{
    [Fact]
    public void CheckedChannelIdentityIsLocalToPersistentDmaControllerIndexing()
    {
        string root = Root();
        string production = Path.Combine(root, "HybridCPU_ISE");
        string[] users = Directory.EnumerateFiles(production, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("DmaChannelId", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DMAController.cs", "DmaChannelId.cs"], users);

        string type = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "DMA",
            "DmaChannelId.cs"));
        Assert.Contains("public const byte MinValue = 0;", type, StringComparison.Ordinal);
        Assert.Contains("public const byte MaxValue = 7;", type, StringComparison.Ordinal);
        Assert.Contains("public static DmaChannelId Zero", type, StringComparison.Ordinal);
        Assert.Contains("TryCreate", type, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\bChannelId\b"), type);
    }

    [Fact]
    public void EveryControllerArrayAccessUsesCheckedChannelAndRawIngressIsGated()
    {
        string source = ReadController();

        Assert.Equal(Regex.Matches(source, @"channels\[").Count,
            Regex.Matches(source, @"channels\[channel\]").Count);
        Assert.DoesNotMatch(new Regex(@"channels\[(?:channelID|desc\.ChannelID|selectedChannel|\(byte\))\]"), source);
        Assert.Equal(8, Regex.Matches(source, @"channelID\s*>=\s*MAX_CHANNELS").Count);
        Assert.Equal(1, Regex.Matches(source, @"desc\.ChannelID\s*>=\s*MAX_CHANNELS").Count);
        Assert.DoesNotMatch(new Regex(@"%\s*MAX_CHANNELS|Math\.(?:Min|Max|Clamp).*CHANNEL|channelID\s*=\s*0"), source);
    }

    [Fact]
    public void RawWireAndPublicationFormsStaySeparatedFromCheckedOwnership()
    {
        string source = ReadController();

        Assert.Contains("public byte ChannelID { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("public byte ChannelID;", source, StringComparison.Ordinal);
        Assert.Contains("public delegate void TransferCompletionCallback(byte channelID", source,
            StringComparison.Ordinal);
        Assert.Contains("private void OnTransferCompleted(byte channelID", source, StringComparison.Ordinal);
        Assert.Contains("ushort interruptID = (ushort)(0x90 + channelID);", source,
            StringComparison.Ordinal);
        Assert.Contains("DmaChannelId channel = DmaChannelId.Create((byte)selectedChannel);", source,
            StringComparison.Ordinal);
        Assert.Contains("if (selectedChannel >= 0)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FamilyHasNoSerializerReflectionTestSupportOrCrossFamilyBypass()
    {
        string dmaDirectory = Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory", "DMA");
        string allDma = string.Join("\n", Directory.EnumerateFiles(dmaDirectory, "*.cs")
            .Select(File.ReadAllText));

        Assert.DoesNotContain("JsonSerializer", allDma, StringComparison.Ordinal);
        Assert.DoesNotContain("BindingFlags", allDma, StringComparison.Ordinal);
        Assert.DoesNotContain("TestSupport", allDma, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\b(?:StreamId|DeviceId|QueueId|TokenId|DomainId|LaneId|SlotId)\b"), allDma);
    }

    private static string ReadController() => File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE",
        "CloseToHSL", "Memory", "DMA", "DMAController.cs"));

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
