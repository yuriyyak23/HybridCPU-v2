namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.8f execution DomainTag placement and stage-carrier inventory.</summary>
public sealed class Rf128fExecutionDomainTagPlacementStageInventoryTests
{
    [Fact]
    public void PlacementAndThreeStageCarriersRetainRawUlongBaselineZero()
    {
        string root = Root();
        string placement = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling",
            "SlotPlacementMetadata.cs");
        Assert.Contains("public ulong DomainTag", placement, StringComparison.Ordinal);
        Assert.Contains("DomainTag         = 0", placement, StringComparison.Ordinal);
        foreach ((string directory, string file) stage in new[]
        {
            ("Execute", "CPU_Core.Pipeline.Stages.ScalarExecuteLaneState.cs"),
            ("Memory", "CPU_Core.Pipeline.Stages.ScalarMemoryLaneState.cs"),
            ("WriteBack", "CPU_Core.Pipeline.Stages.ScalarWriteBackLaneState.cs"),
        })
        {
            string text = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages",
                stage.directory, stage.file);
            Assert.Contains("public ulong DomainTag", text, StringComparison.Ordinal);
            Assert.Contains("DomainTag = 0", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CertificateConsumesPlacementDomainTagWithoutChangingOwnerAuthority()
    {
        string certificate = Read(Root(), "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Certificates",
            "BundleResourceCertificate.cs");
        Assert.Contains("hasher.Compress(assistMicroOp.Placement.DomainTag)", certificate, StringComparison.Ordinal);
        Assert.Contains("MixAssistStructuralKey(hash, assistMicroOp.Placement.DomainTag)", certificate,
            StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));
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
