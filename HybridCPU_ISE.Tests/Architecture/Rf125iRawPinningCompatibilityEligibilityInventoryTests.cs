namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125iRawPinningCompatibilityEligibilityInventoryTests
{
    [Fact]
    public void RetainedRawPinningSurfacesAreExplicitAndNonRemovable()
    {
        string root = FindRepositoryRoot();
        string metadata = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "SlotPlacementMetadata.cs");
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string compiler = Read(root, "HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs");
        string packet = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "RuntimeClusterAdmissionPreparation.BundleIssuePacket.cs");

        Assert.Contains("public byte PinnedLaneId;", metadata, StringComparison.Ordinal);
        Assert.Contains("public SlotPlacementMetadata Placement { get; set; }", microOp, StringComparison.Ordinal);
        Assert.Contains("protected void SetPlacement(", microOp, StringComparison.Ordinal);
        Assert.Contains("protected void SetHardPinnedPlacement(SlotClass requiredSlotClass, byte pinnedLaneId)", microOp, StringComparison.Ordinal);
        Assert.Contains("private static byte ResolvePinnedLaneId(", compiler, StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId = ResolvePinnedLaneId(", compiler, StringComparison.Ordinal);
        Assert.Equal(2, Count(packet, "byte pinnedLaneId = placement.PinnedLaneId;"));
        Assert.Equal(2, Count(packet, "if (pinnedLaneId >= 8)"));
    }

    [Fact]
    public void LedgerAndEvidenceForbidRemovalBeforeClosedWorldZeroCallerProof()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.5i-raw-pinning-compatibility-removal-eligibility-inventory.md");

        Assert.Contains("RF-12.5i | closed raw-pinning compatibility/removal eligibility inventory", ledger, StringComparison.Ordinal);
        Assert.Contains("zero-caller proof", evidence, StringComparison.Ordinal);
        Assert.Contains("not eligible for removal", evidence, StringComparison.Ordinal);
        Assert.Contains("reflection", evidence, StringComparison.Ordinal);
        Assert.Contains("TestSupport", evidence, StringComparison.Ordinal);
    }

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
