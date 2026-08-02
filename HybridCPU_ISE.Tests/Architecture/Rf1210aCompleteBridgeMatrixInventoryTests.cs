namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1210aCompleteBridgeMatrixInventoryTests
{
    [Fact]
    public void MatrixCoversEveryPaperFamilyAndRequiredBridgeDimension()
    {
        string matrix = Read("Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.10a-complete-parser-serializer-compiler-runtime-bridge-matrix.md");

        string[] dimensions =
        [
            "Presence, zero and absence", "Raw -> checked re-entry",
            "Checked -> raw / wire form", "Invalid input behavior",
            "Owner, version and round-trip status", "Disposition / removal condition"
        ];
        foreach (string dimension in dimensions)
            Assert.Contains(dimension, matrix, StringComparison.Ordinal);

        string[] families =
        [
            "SMT VT", "architectural / physical registers", "bundle slot / source provenance",
            "physical lane / pinning", "scheduler-visible memory bank",
            "topology-local bank geometry", "DMA channel", "stream engine",
            "accelerator / I/O device / queue", "execution owner context / domain tag",
            "I/O/address-space domains", "accepted memory request",
            "DmaStream / accelerator / Lane-6 / Lane-7 tokens",
            "issued attempt / replay token / epochs",
            "replay/certificate/MatrixTile/generated binding identities"
        ];
        foreach (string family in families)
            Assert.Contains($"| {family}", matrix, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceShapesFreezeDistinctJsonBinaryCompilerAndObservationBridges()
    {
        string registerIds = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Registers", "Architectural", "RegisterIdentity.cs");
        string slotId = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "BinaryFormat", "SlotEncoding", "SlotId.cs");
        string laneId = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "LaneId.cs");
        string dmaId = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "DMA", "DmaChannelId.cs");
        string streamId = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Architecture", "Common", "StreamEngineId.cs");
        string provenance = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs");
        string compiler = Read("HybridCPU_Compiler", "Core", "IR", "Bundling", "HybridCpuBundleLowerer.cs");
        string descriptor = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Descriptors", "AcceleratorDescriptorParser.cs");
        string replay = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps", "Replay", "ReplayToken.cs");

        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", registerIds, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", slotId, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", laneId, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", dmaId, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Json.Serialization.JsonConstructor]", streamId, StringComparison.Ordinal);
        Assert.Contains("[JsonIgnore]\n    public SlotId SourceSlotId", provenance.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.Contains("ValidateRawSourceSlotIndex(sourceSlotIndex)", provenance, StringComparison.Ordinal);
        Assert.Contains("VtId.Create(instruction.VirtualThreadId)", compiler, StringComparison.Ordinal);
        Assert.Contains("PinnedLaneId = ResolvePinnedLaneId(", compiler, StringComparison.Ordinal);
        Assert.Contains("public const ushort CurrentAbiVersion = 1;", descriptor, StringComparison.Ordinal);
        Assert.Contains("public const uint Magic = 0x31434453", descriptor, StringComparison.Ordinal);
        Assert.Contains("JsonSerializer.Deserialize<ReplayToken>(json)", replay, StringComparison.Ordinal);
        Assert.Contains("BindMainMemory", replay, StringComparison.Ordinal);
    }

    [Fact]
    public void LedgerAdvancesOnlyToExpandedDeletionEligibilityReconciliation()
    {
        string ledger = Read("Documentation", "ArchitectureAuthorityRefactor", "11_RF12", "00_ENTRY_STATUS_AND_ROADMAP.md");
        string matrix = Read("Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
            "rf12.10a-complete-parser-serializer-compiler-runtime-bridge-matrix.md");

        Assert.Contains("RF-12.10a | closed complete bridge matrix inventory", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-12 is closed at RF-12.12h", ledger, StringComparison.Ordinal);
        Assert.Contains("Historical RF-12.11 named", matrix, StringComparison.Ordinal);
        Assert.Contains("selects no representation, wire, invalid-input or", matrix, StringComparison.Ordinal);
        Assert.Contains("No second VT type", matrix, StringComparison.Ordinal);
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
