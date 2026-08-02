namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125jRawPinningCompatibilityRetentionDecisionTests
{
    [Fact]
    public void NonZeroCallerRawPinningSurfacesRemainRetained()
    {
        string root = FindRepositoryRoot();
        string metadata = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "SlotPlacementMetadata.cs");
        string microOp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string compiler = Read(root, "HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs");
        string executionPlacement = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs");
        string packet = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Core", "RuntimeClusterAdmissionPreparation.BundleIssuePacket.cs");

        Assert.Contains("public byte PinnedLaneId;", metadata, StringComparison.Ordinal);
        Assert.Contains("public SlotPlacementMetadata Placement { get; set; }", microOp, StringComparison.Ordinal);
        Assert.Contains("protected void SetPlacement(", microOp, StringComparison.Ordinal);
        Assert.Contains("protected void SetHardPinnedPlacement(SlotClass requiredSlotClass, byte pinnedLaneId)", microOp, StringComparison.Ordinal);
        Assert.Contains("private static byte ResolvePinnedLaneId(", compiler, StringComparison.Ordinal);
        Assert.Contains("public byte PinnedLaneId { get; }", executionPlacement, StringComparison.Ordinal);
        Assert.Contains("public static ExecutionPlacement Create(", executionPlacement, StringComparison.Ordinal);
        Assert.Equal(2, Count(packet, "byte pinnedLaneId = placement.PinnedLaneId;"));
        Assert.Equal(2, Count(packet, "if (pinnedLaneId >= 8)"));
    }

    [Fact]
    public void PaperExpiryConditionAndCurrentHandoffRequireRetention()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "3_Architectural_Overview_and_Frontend_Contract.md");
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.5j-raw-pinning-compatibility-retention-decision.md");

        Assert.Contains("It expires only after valid-input parity", paper, StringComparison.Ordinal);
        Assert.Contains("and a zero-caller", paper, StringComparison.Ordinal);
        Assert.Contains("RF-12.5j | closed raw-pinning compatibility retention decision", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("are **retained** because their caller count is", evidence, StringComparison.Ordinal);
        Assert.Contains("non-zero", evidence, StringComparison.Ordinal);
        Assert.Contains("fresh closed-world zero-caller proof", evidence, StringComparison.Ordinal);
        Assert.Contains("No raw declaration, constructor, parameter, return, storage field, factory", evidence, StringComparison.Ordinal);
        Assert.Contains("RF-12.10a", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void RetentionDoesNotCreateAnotherLaneOrGenericIdentifierFamily()
    {
        string root = FindRepositoryRoot();
        string laneId = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "LaneId.cs");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12", "rf12.5j-raw-pinning-compatibility-retention-decision.md");

        Assert.Contains("public readonly record struct LaneId", laneId, StringComparison.Ordinal);
        Assert.DoesNotContain("struct ChannelId", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("struct DomainId", evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("struct TokenId", evidence, StringComparison.Ordinal);
        Assert.Contains("`SlotId`, `LaneId`, pinning", evidence, StringComparison.Ordinal);
        Assert.Contains("cast, clamp, modulo, key, sentinel or unresolved fallback", evidence, StringComparison.Ordinal);
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
