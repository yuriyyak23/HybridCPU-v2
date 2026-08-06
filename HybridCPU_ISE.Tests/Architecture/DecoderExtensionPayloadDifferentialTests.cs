using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// Exhaustive generated-catalog differential gate for the RF-05 extension
/// payload projection surface. Matrix semantic faults remain post-decode
/// outcomes and are intentionally not reclassified here.
/// </summary>
public sealed class DecoderExtensionPayloadDifferentialTests
{
    private sealed record Report(
        int SchemaVersion,
        string Scope,
        int GeneratedVectorPayloadDescriptorCount,
        int Accepted,
        int Rejected,
        int SemanticProjectionDifferences);

    [Fact]
    public void GeneratedVectorPayloadDescriptors_HaveAcceptRejectAndPayloadProjectionParity()
    {
        GeneratedIsaDescriptor[] descriptors = GeneratedIsaCatalog.Descriptors
            .Where(static descriptor => OpcodeRegistry.RequiresVectorPayloadProjection(descriptor.Opcode))
            .OrderBy(static descriptor => descriptor.Opcode)
            .ToArray();
        Assert.NotEmpty(descriptors);

        int accepted = 0;
        int rejected = 0;
        foreach (GeneratedIsaDescriptor descriptor in descriptors)
        {
            VLIW_Instruction instruction = CreateCanonicalVectorCarrier(checked((ushort)descriptor.Opcode));
            RawSlot slot = RawSlotReader.Read(in instruction, slotIndex: 3);
            bool declarativeAccepted = DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
                in slot,
                InstructionSlotMetadata.Default,
                out DeclarativeDecodedSlot? declarative,
                out DecodeFailure? failure);

            try
            {
                InstructionIR legacy = new VliwDecoderV4().Decode(in instruction, slotIndex: 3);
                Assert.True(
                    declarativeAccepted,
                    $"Generated vector payload descriptor {descriptor.Mnemonic} ({descriptor.Opcode}) was accepted by legacy decode but rejected by declarative decode: {failure?.Code}/{failure?.Field}.");
                Assert.NotNull(declarative);
                Assert.Equal(legacy.VectorPayload, declarative!.VectorPayload);
                Assert.Equal(legacy.Rd, declarative.Operands.Rd);
                Assert.Equal(legacy.Rs1, declarative.Operands.Rs1);
                Assert.Equal(legacy.Rs2, declarative.Operands.Rs2);
                Assert.Equal(legacy.Imm, declarative.Operands.Immediate);
                Assert.Equal((uint)legacy.CanonicalOpcode, declarative.CanonicalInstruction.Opcode);
                accepted++;
            }
            catch (InvalidOpcodeException)
            {
                Assert.False(
                    declarativeAccepted,
                    $"Generated vector payload descriptor {descriptor.Mnemonic} ({descriptor.Opcode}) was rejected by legacy decode but accepted by declarative decode.");
                Assert.Null(declarative);
                Assert.NotNull(failure);
                rejected++;
            }
        }

        Report report = LoadReport();
        Assert.Equal(descriptors.Length, report.GeneratedVectorPayloadDescriptorCount);
        Assert.Equal(accepted, report.Accepted);
        Assert.Equal(rejected, report.Rejected);
        Assert.Equal(0, report.SemanticProjectionDifferences);
    }

    [Fact]
    public void ExtensionPayloadMutations_PreserveLegalVectorFieldsAndRejectOnlyOutOfContourFlags()
    {
        var vadd = CreateCanonicalVectorCarrier(IsaOpcodeValues.VADD);
        vadd.Indexed = true;
        vadd.Is2D = true;
        vadd.TailAgnostic = true;
        vadd.MaskAgnostic = true;
        vadd.Saturating = true;
        AssertVectorPayloadParity(in vadd, "VADD full legal payload mutation");

        var reduction = CreateCanonicalVectorCarrier(IsaOpcodeValues.VREDSUM);
        reduction.Reduction = true;
        AssertVectorPayloadParity(in reduction, "VREDSUM reduction payload mutation");

        var matrix = CreateCanonicalVectorCarrier(IsaOpcodeValues.MTILE_LOAD);
        matrix.StreamLength = 0; // decoded payload is legal; projection records a later typed fault.
        AssertVectorPayloadParity(in matrix, "MTILE_LOAD post-decode projection-fault payload");

        var scalarIllegalVectorFlag = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Indexed = true };
        RawSlot scalarSlot = RawSlotReader.Read(in scalarIllegalVectorFlag, slotIndex: 3);
        Assert.False(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in scalarSlot,
            InstructionSlotMetadata.Default,
            out _,
            out DecodeFailure? scalarFailure));
        Assert.NotNull(scalarFailure);
        Assert.Equal(DecodeFailureCode.ReservedEncoding, scalarFailure!.Code);
        Assert.Equal("Indexed", scalarFailure.Field);
        Assert.Throws<InvalidOpcodeException>(() => new VliwDecoderV4().Decode(in scalarIllegalVectorFlag, slotIndex: 3));
    }

    private static void AssertVectorPayloadParity(in VLIW_Instruction instruction, string caseId)
    {
        RawSlot slot = RawSlotReader.Read(in instruction, slotIndex: 3);
        Assert.True(DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
            in slot,
            InstructionSlotMetadata.Default,
            out DeclarativeDecodedSlot? declarative,
            out DecodeFailure? failure),
            $"{caseId}: declarative reject {failure?.Code}/{failure?.Field}.");
        InstructionIR legacy = new VliwDecoderV4().Decode(in instruction, slotIndex: 3);
        Assert.NotNull(declarative);
        Assert.Equal(legacy.VectorPayload, declarative!.VectorPayload);
    }

    private static VLIW_Instruction CreateCanonicalVectorCarrier(ushort opcode) =>
        new()
        {
            OpCode = opcode,
            DestSrc1Pointer = 0x1000,
            Src2Pointer = 0x2000,
            StreamLength = 8,
            Stride = 4,
            RowStride = 32,
            PredicateMask = 0x3,
            DataType = (byte)DataTypeEnum.INT32,
        };

    private static Report LoadReport()
    {
        Report? report = JsonSerializer.Deserialize<Report>(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "Documentation",
                "Documentation",
                "ArchitectureAuthorityRefactor",
                "Evidence",
                "RF05",
                "rf05-extension-payload-differential-report.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(report);
        Assert.Equal(1, report!.SchemaVersion);
        Assert.Equal("all generated descriptors requiring VectorInstructionPayload projection", report.Scope);
        return report;
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
