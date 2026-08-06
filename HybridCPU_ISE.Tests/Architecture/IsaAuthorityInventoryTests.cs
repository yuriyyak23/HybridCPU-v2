using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-01 inventory of current ISA authority surfaces. This is test-only evidence;
/// it does not nominate any listed source as the future manifest authority.
/// </summary>
public sealed class IsaAuthorityInventoryTests
{
    private const string StaticPolicyAuthority = "GeneratedIsaCatalog served through OpcodeRegistry.Opcodes (manifest-derived static metadata; RF-13.36 facade)";
    private const string EncodingLegalityAuthority = "VliwDecoderV4 (mixed current encoding legality; RF-05 closure)";
    private const string RuntimeLegalityOwner = "Runtime admission/materialization services (state-dependent legality; RF-06 named contracts)";
    private const string EvidenceProducer = "InstructionSupportStatusCatalog plus decoder/materializer/retire probes (report only; RF-06 join)";
    private const string GapOwner = "ISA architecture owner";

    [Fact]
    public void EveryPublishedNumericOpcode_HasExactlyOneCurrentAuthorityInventoryRow()
    {
        IReadOnlyList<IsaAuthorityInventoryRow> rows = BuildCurrentInventory();

        Assert.NotEmpty(rows);
        Assert.Equal(OpcodeRegistry.Opcodes.Length, rows.Count);
        Assert.Empty(rows.GroupBy(static row => row.NumericOpcode).Where(static group => group.Count() != 1));
        Assert.Empty(rows.GroupBy(static row => row.Mnemonic, StringComparer.Ordinal).Where(static group => group.Count() != 1));
    }

    [Fact]
    public void EveryInventoryRow_NamesFourAuthorityBoundariesAndAClosureOwner()
    {
        foreach (IsaAuthorityInventoryRow row in BuildCurrentInventory())
        {
            Assert.False(string.IsNullOrWhiteSpace(row.StaticPolicyAuthority));
            Assert.False(string.IsNullOrWhiteSpace(row.EncodingLegalityAuthority));
            Assert.False(string.IsNullOrWhiteSpace(row.RuntimeLegalityOwner));
            Assert.False(string.IsNullOrWhiteSpace(row.EvidenceProducer));
            Assert.False(string.IsNullOrWhiteSpace(row.GapOwner));
            Assert.Contains(row.ClosurePhase, new[] { "RF-02", "RF-05", "RF-06" });
        }
    }

    [Fact]
    public void Inventory_RecordsCurrentDuplicateAuthoritySurfacesWithoutTreatingEvidenceAsExecutionProof()
    {
        IReadOnlyList<IsaAuthorityInventoryRow> rows = BuildCurrentInventory();

        Assert.All(rows, row =>
        {
            Assert.Equal(StaticPolicyAuthority, row.StaticPolicyAuthority);
            Assert.Contains("VliwDecoderV4", row.EncodingLegalityAuthority, StringComparison.Ordinal);
            Assert.Contains("report only", row.EvidenceProducer, StringComparison.Ordinal);
            Assert.Equal(GapOwner, row.GapOwner);
        });
    }

    [Fact]
    public void RegistryLookupAndInternalOpProjection_AreInventoryConsumersNotAdditionalRows()
    {
        int internalOpProjectionRows = 0;
        int alternateProjectionRows = 0;
        int vectorRouteRows = 0;

        foreach (IsaAuthorityInventoryRow row in BuildCurrentInventory())
        {
            OpcodeInfo? lookup = OpcodeRegistry.GetInfo(row.NumericOpcode);
            Assert.True(lookup.HasValue);
            Assert.Equal(row.Mnemonic, lookup.Value.Mnemonic);

            // InternalOpBuilder is a scalar/non-vector projection consumer, not an
            // opcode authority and not the routing authority for vector execution.
            // The inventory intentionally makes no claim about materialization or
            // retirement for either route.
            if (lookup.Value.IsVector)
            {
                vectorRouteRows++;
                continue;
            }

            try
            {
                _ = InternalOpBuilder.MapToKind((ushort)row.NumericOpcode);
                internalOpProjectionRows++;
            }
            catch (ArgumentOutOfRangeException exception) when (
                exception.Message.Contains("No InternalOpKind mapping", StringComparison.Ordinal))
            {
                // This is an observed incomplete legacy projection boundary, not
                // evidence that the opcode is executable on any alternate path.
                alternateProjectionRows++;
            }
        }

        Assert.True(vectorRouteRows > 0, "The published ISA inventory must retain the separate vector projection route.");
        Assert.True(internalOpProjectionRows > 0, "The published ISA inventory must retain the InternalOp projection consumer.");
        Assert.True(alternateProjectionRows > 0, "The inventory must expose incomplete InternalOp projection instead of inventing a fallback authority.");
    }

    private static IReadOnlyList<IsaAuthorityInventoryRow> BuildCurrentInventory() =>
        OpcodeRegistry.Opcodes
            .OrderBy(static opcode => opcode.OpCode)
            .Select(static opcode => new IsaAuthorityInventoryRow(
                NumericOpcode: opcode.OpCode,
                Mnemonic: opcode.Mnemonic,
                StaticPolicyAuthority: StaticPolicyAuthority,
                EncodingLegalityAuthority: EncodingLegalityAuthority,
                RuntimeLegalityOwner: RuntimeLegalityOwner,
                EvidenceProducer: EvidenceProducer,
                GapOwner: GapOwner,
                ClosurePhase: ClassifyClosurePhase(opcode)))
            .ToArray();

    private static string ClassifyClosurePhase(OpcodeInfo opcode) =>
        opcode.Category is OpcodeCategory.Vector or OpcodeCategory.Memory or OpcodeCategory.Atomic
            ? "RF-06"
            : "RF-05";

    private sealed record IsaAuthorityInventoryRow(
        uint NumericOpcode,
        string Mnemonic,
        string StaticPolicyAuthority,
        string EncodingLegalityAuthority,
        string RuntimeLegalityOwner,
        string EvidenceProducer,
        string GapOwner,
        string ClosurePhase);
}
