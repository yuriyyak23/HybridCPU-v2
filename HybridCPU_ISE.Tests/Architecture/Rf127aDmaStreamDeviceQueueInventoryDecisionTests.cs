namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7a closed-world DMA/stream/device/queue inventory guard.</summary>
public sealed class Rf127aDmaStreamDeviceQueueInventoryDecisionTests
{
    [Fact]
    public void PaperSeparatesAllFourFamiliesAndTheirZeroForms()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));

        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("local to stream-resource selection", paper,
            StringComparison.Ordinal);
        Assert.Contains("remain accelerator-specific", paper,
            StringComparison.Ordinal);
        Assert.Contains("Guest queue identity, virtual queue identity, and queue epoch are separate",
            paper, StringComparison.Ordinal);
        Assert.Contains("no universal", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionOwnersRemainSeparateAndUseExistingRawOrEnumForms()
    {
        string root = Root();
        string dma = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory", "DMA",
            "DMAController.cs");
        string accelerator = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Execution", "ExternalAccelerators", "Descriptors", "AcceleratorCommandDescriptor.cs");
        string queues = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime",
            "Lanes", "Lane6", "Lane6QueueRuntime.cs");

        Assert.Contains("public byte ChannelID;", dma, StringComparison.Ordinal);
        Assert.Contains("if (desc.ChannelID >= MAX_CHANNELS)", dma, StringComparison.Ordinal);
        Assert.Contains("public enum AcceleratorDeviceId : ushort", accelerator,
            StringComparison.Ordinal);
        Assert.Contains("ReferenceMatMul = 1", accelerator, StringComparison.Ordinal);
        Assert.Contains("guestQueueId == 0 ? BuildDefaultGuestQueueId", queues,
            StringComparison.Ordinal);
        Assert.Contains("private ulong _nextVirtualQueueId = 0x1_0000UL", queues,
            StringComparison.Ordinal);
        Assert.Contains("private ulong _queueEpoch;", queues, StringComparison.Ordinal);
        Assert.Contains("Dictionary<(ushort IoDomainTag, uint DomainId, uint DeviceId, ushort VtId)",
            queues, StringComparison.Ordinal);
    }

    [Fact]
    public void NoUniversalChannelDeviceOrQueueTypeHasBeenIntroduced()
    {
        string production = string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(Root(), "HybridCPU_ISE"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));
        Assert.DoesNotMatch(@"\b(?:class|struct|record\s+struct)\s+(?:ChannelId|DeviceId|QueueId)\b",
            production);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

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
