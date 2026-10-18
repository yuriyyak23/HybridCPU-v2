using System.Text.RegularExpressions;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7c DMA ChannelID producer/consumer inventory guard.</summary>
public sealed class Rf127cDmaChannelProducerConsumerInventoryTests
{

    [Fact]
    public void PaperAllowsCheckedRepresentationButNotNewControllerAuthority()
    {
        string paper = File.ReadAllText(Path.Combine(Root(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md"));
        Assert.Contains("local to the persistent DMA controller", paper,
            StringComparison.Ordinal);
        Assert.Contains("Zero is valid channel 0", paper, StringComparison.Ordinal);
        Assert.Contains("No universal channel, domain, device, token", paper,
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
