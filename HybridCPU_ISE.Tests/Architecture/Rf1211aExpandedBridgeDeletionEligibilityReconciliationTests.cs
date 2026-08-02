namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1211aExpandedBridgeDeletionEligibilityReconciliationTests
{
    [Fact]
    public void ExpandedMatrixRetainsEveryNonZeroCallerSurfaceGroup()
    {
        string evidence = Read("Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.11a-expanded-bridge-compatibility-deletion-eligibility-reconciliation.md");
        string[] groups =
        [
            "VT compiler/JSON/trace", "decoder/register and resource-mask raw APIs",
            "slot provenance numeric compatibility", "lane/pinning compatibility",
            "scheduler-visible bank raw carriers/masks", "physical-bank geometry/binding storage",
            "DMA channel and stream-engine adapters", "accelerator/device/queue descriptor and checkpoint forms",
            "owner context/domain and I/O/address-space composites", "accepted `MemoryRequestId`",
            "DmaStream/accelerator/Lane-6/Lane-7 token seams", "`VliwOperationId`, ReplayToken and epoch observations",
            "internal certificate/cache/MatrixTile/generated identities", "reflection/TestSupport/diagnostics"
        ];

        foreach (string group in groups)
            Assert.Contains($"| {group} |", evidence, StringComparison.Ordinal);

        Assert.Contains("No deletion is eligible and no production change is", evidence, StringComparison.Ordinal);
        Assert.Contains("fresh closed-world zero-caller proof", evidence, StringComparison.Ordinal);
        Assert.Contains("Already-removed seams are not reopened", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void RepresentativeLiveCallersStillPreventDeletion()
    {
        string compiler = Read("HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs");
        string microOp = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string placement = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs");
        string dma = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "DMA", "DMAController.cs");
        string replay = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");

        Assert.Contains("VtId.Create(instruction.VirtualThreadId)", compiler, StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId = ResolvePinnedLaneId(", compiler, StringComparison.Ordinal);
        Assert.Contains("protected void SetHardPinnedPlacement(SlotClass requiredSlotClass, byte pinnedLaneId)", microOp, StringComparison.Ordinal);
        Assert.Contains("public static ExecutionPlacement Create(", placement, StringComparison.Ordinal);
        Assert.Contains("DmaChannelId", dma, StringComparison.Ordinal);
        Assert.Contains("public static ReplayToken FromJson", replay, StringComparison.Ordinal);
        Assert.Contains("BindMainMemory", replay, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerOpensOnlyTheFinalPostReconciliationAudit()
    {
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        Assert.Contains("RF-12.11a | closed expanded deletion-eligibility reconciliation", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12.12c | superseded final exit audit", ledger, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(path).ToArray()));

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
