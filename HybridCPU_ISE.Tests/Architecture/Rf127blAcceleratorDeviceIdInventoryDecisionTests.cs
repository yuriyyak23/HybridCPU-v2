using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bl AcceleratorDeviceId closed-world inventory guard.</summary>
public sealed class Rf127blAcceleratorDeviceIdInventoryDecisionTests
{
    [Fact]
    public void EnumIsAcceleratorSpecificAndHasNoZeroMember()
    {
        AcceleratorDeviceId[] values = Enum.GetValues<AcceleratorDeviceId>();

        Assert.Equal([
            AcceleratorDeviceId.ReferenceMatMul,
            AcceleratorDeviceId.TensorMetadata,
            AcceleratorDeviceId.TopologyQueueMetadata,
            AcceleratorDeviceId.FftMetadata,
            AcceleratorDeviceId.CryptoHashMetadata,
            AcceleratorDeviceId.SparseGraphMetadata
        ], values);
        Assert.DoesNotContain((AcceleratorDeviceId)0, values);
        Assert.Equal((ushort)1, (ushort)AcceleratorDeviceId.ReferenceMatMul);
        Assert.Equal((ushort)6, (ushort)AcceleratorDeviceId.SparseGraphMetadata);
    }


    [Fact]
    public void PaperSeparatesAcceleratorIdentityFromIommuAndBurstIoAndInventoryRecordsTheGap()
    {
        string root = Root();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");
        string production = string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, "HybridCPU_ISE"), "*.cs",
            SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Contains("Accelerator and I/O device", paper, StringComparison.Ordinal);
        Assert.Contains("defined non-zero enum values", paper, StringComparison.Ordinal);
        Assert.Contains("IOMMU device identity and BurstIO endpoint selection are separate families", paper,
            StringComparison.Ordinal);
        Assert.Contains("BurstIO endpoint zero may continue to mean the CPU endpoint", paper,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\b(?:class|struct|record\s+struct)\s+DeviceId\b"), production);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine([root, .. parts]));

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
