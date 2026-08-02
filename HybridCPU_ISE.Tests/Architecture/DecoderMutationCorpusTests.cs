using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// Bounded exhaustive RF-05 mutation matrix for every currently registered
/// decode-owned C# legality family.  The JSON report is evidence only; the
/// executable C# validator and this C# corpus remain the authority.
/// </summary>
public sealed class DecoderMutationCorpusTests
{
    private sealed record MutationCase(
        string FamilyId,
        string CaseId,
        bool IsAccepted,
        int SlotIndex,
        VLIW_Instruction Instruction,
        string? ExpectedField = null);

    private sealed record CorpusReport(int SchemaVersion, string Authority, CorpusReportFamily[] Families);

    private sealed record CorpusReportFamily(string Id, int Accepted, int Rejected);

    private static readonly MutationCase[] Corpus =
    [
        Accept("reserved-word0-zero", "clean-add", 0, Scalar(IsaOpcodeValues.ADD)),
        Reject("reserved-word0-zero", "reserved-bit", 0, Scalar(IsaOpcodeValues.ADD, reserved: 1), "Reserved"),

        Accept("retired-policy-gap-zero", "clean-add", 0, Scalar(IsaOpcodeValues.ADD)),
        Reject("retired-policy-gap-zero", "word3-policy-gap", 0, Scalar(IsaOpcodeValues.ADD, word3: 1UL << 50), "Word3[50]"),

        Accept("opcode-flag-legality", "clean-add", 0, Scalar(IsaOpcodeValues.ADD)),
        Reject("opcode-flag-legality", "non-atomic-acquire", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Acquire = true }, "AcquireRelease"),
        Reject("opcode-flag-legality", "non-atomic-release", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Release = true }, "AcquireRelease"),
        Reject("opcode-flag-legality", "non-vadd-saturating", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Saturating = true }, "Saturating"),
        Reject("opcode-flag-legality", "non-reduction-reduction", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Reduction = true }, "Reduction"),
        Reject("opcode-flag-legality", "scalar-indexed", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Indexed = true }, "Indexed"),
        Reject("opcode-flag-legality", "scalar-2d", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, Is2D = true }, "Is2D"),
        Reject("opcode-flag-legality", "scalar-tail-mask", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD, TailAgnostic = true }, "TailMaskAgnostic"),

        Accept("fence-zero-payload", "canonical-fence", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.FENCE }),
        Reject("fence-zero-payload", "immediate-payload", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.FENCE, Immediate = 1 }, "FencePayload"),

        Accept("scalar-word-shift-immediate-rs2-zero", "slliw-rs2-zero", 0, Scalar(IsaOpcodeValues.SLLIW, rs2: 0)),
        Reject("scalar-word-shift-immediate-rs2-zero", "slliw-rs2-nonzero", 0, Scalar(IsaOpcodeValues.SLLIW, rs2: 1), "rs2"),

        Accept("scalar-unary-rs2-and-immediate-zero", "sext-w-clean", 0, Scalar(IsaOpcodeValues.SEXT_W, rs2: 0)),
        Reject("scalar-unary-rs2-and-immediate-zero", "sext-w-rs2-nonzero", 0, Scalar(IsaOpcodeValues.SEXT_W, rs2: 1), "rs2"),

        Accept("scalar-address-generation-immediate-zero", "sh1add-clean", 0, Scalar(IsaOpcodeValues.SH1ADD)),
        Reject("scalar-address-generation-immediate-zero", "sh1add-immediate", 0, Scalar(IsaOpcodeValues.SH1ADD, immediate: 1), "Immediate"),

        Accept("scalar-address-generation-imm6", "slli-uw-imm63", 0, Scalar(IsaOpcodeValues.SLLI_UW, immediate: 63, rs2: 0)),
        Reject("scalar-address-generation-imm6", "slli-uw-imm64", 0, Scalar(IsaOpcodeValues.SLLI_UW, immediate: 64, rs2: 0), "Immediate"),

        Accept("scalar-carry-less-immediate-zero", "clmul-clean", 0, Scalar(IsaOpcodeValues.CLMUL)),
        Reject("scalar-carry-less-immediate-zero", "clmul-immediate", 0, Scalar(IsaOpcodeValues.CLMUL, immediate: 1), "Immediate"),

        Accept("scalar-rotate-immediate-zero", "rol-clean", 0, Scalar(IsaOpcodeValues.ROL)),
        Reject("scalar-rotate-immediate-zero", "rol-immediate", 0, Scalar(IsaOpcodeValues.ROL, immediate: 1), "Immediate"),

        Accept("scalar-rotate-imm6", "roli-imm63", 0, Scalar(IsaOpcodeValues.ROLI, immediate: 63, rs2: 0)),
        Reject("scalar-rotate-imm6", "roli-imm64", 0, Scalar(IsaOpcodeValues.ROLI, immediate: 64, rs2: 0), "Immediate"),

        Accept("scalar-boolean-invert-immediate-zero", "andn-clean", 0, Scalar(IsaOpcodeValues.ANDN)),
        Reject("scalar-boolean-invert-immediate-zero", "andn-immediate", 0, Scalar(IsaOpcodeValues.ANDN, immediate: 1), "Immediate"),

        Accept("scalar-bitfield-immediate-zero", "bset-clean", 0, Scalar(IsaOpcodeValues.BSET)),
        Reject("scalar-bitfield-immediate-zero", "bset-immediate", 0, Scalar(IsaOpcodeValues.BSET, immediate: 1), "Immediate"),

        Accept("scalar-bitfield-imm6", "bseti-imm63", 0, Scalar(IsaOpcodeValues.BSETI, immediate: 63, rs2: 0)),
        Reject("scalar-bitfield-imm6", "bseti-imm64", 0, Scalar(IsaOpcodeValues.BSETI, immediate: 64, rs2: 0), "Immediate"),

        Accept("scalar-minmax-immediate-zero", "min-clean", 0, Scalar(IsaOpcodeValues.MIN)),
        Reject("scalar-minmax-immediate-zero", "min-immediate", 0, Scalar(IsaOpcodeValues.MIN, immediate: 1), "Immediate"),

        Accept("scalar-zeroing-select-immediate-zero", "czero-nez-clean", 0, Scalar(IsaOpcodeValues.CZERO_NEZ)),
        Reject("scalar-zeroing-select-immediate-zero", "czero-nez-immediate", 0, Scalar(IsaOpcodeValues.CZERO_NEZ, immediate: 1), "Immediate"),

        Accept("counter-read-source-and-immediate-zero", "rdcycle-clean", 0, Scalar(IsaOpcodeValues.RDCYCLE, rs1: 0, rs2: 0)),
        Reject("counter-read-source-and-immediate-zero", "rdcycle-rs1", 0, Scalar(IsaOpcodeValues.RDCYCLE, rs1: 1, rs2: 0), "rs1"),

        Accept("dsc-queue-control-clean-register-abi", "dsc-status-clean", 6, Scalar(IsaOpcodeValues.DSC_STATUS, rs1: 0, rs2: 0)),
        Accept("dsc-queue-control-clean-register-abi", "dsc-query-caps-clean", 6, Scalar(IsaOpcodeValues.DSC_QUERY_CAPS, rs1: 0, rs2: 0)),
        Reject("dsc-queue-control-clean-register-abi", "dsc-status-rs2", 6, Scalar(IsaOpcodeValues.DSC_STATUS, rs1: 0, rs2: 1), "rs2"),
        Reject("dsc-queue-control-clean-register-abi", "dsc-query-caps-rs1", 6, Scalar(IsaOpcodeValues.DSC_QUERY_CAPS, rs1: 1, rs2: 0), "rs1/rs2"),

        Accept("control-flow-no-legacy-src2-target", "jal-clean", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.JAL }),
        Reject("control-flow-no-legacy-src2-target", "jal-legacy-src2-target", 0, new VLIW_Instruction { OpCode = IsaOpcodeValues.JAL, Src2Pointer = 1 }, "Src2Pointer"),
    ];

    [Fact]
    public void MutationCorpus_CoversEveryRegisteredDecodeOwnedConstraintFamily()
    {
        string[] actual = Corpus.Select(static test => test.FamilyId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        string[] expected = EncodingConstraintValidator.RegisteredRawFormConstraintIds
            .Concat(EncodingConstraintValidator.RegisteredConstraintIds)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.All(
            Corpus.GroupBy(static test => test.FamilyId),
            static family =>
            {
                Assert.Contains(family, static test => test.IsAccepted);
                Assert.Contains(family, static test => !test.IsAccepted);
            });
    }

    [Fact]
    public void MutationCorpus_HasAcceptRejectParityWithLegacyDecoder()
    {
        foreach (MutationCase mutation in Corpus)
        {
            VLIW_Instruction instruction = mutation.Instruction;
            RawSlot slot = RawSlotReader.Read(in instruction, mutation.SlotIndex);
            bool accepted = DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
                in slot,
                InstructionSlotMetadata.Default,
                out var decoded,
                out var failure);

            Assert.True(
                accepted == mutation.IsAccepted,
                $"{mutation.FamilyId}/{mutation.CaseId}: expected accepted={mutation.IsAccepted}, " +
                $"actual accepted={accepted}, failure={failure?.Code}/{failure?.Field}.");
            if (mutation.IsAccepted)
            {
                Assert.NotNull(decoded);
                Assert.Null(failure);
                _ = new VliwDecoderV4().Decode(in instruction, mutation.SlotIndex);
            }
            else
            {
                Assert.Null(decoded);
                Assert.NotNull(failure);
                Assert.Equal(DecodeFailureCode.ReservedEncoding, failure!.Code);
                Assert.Equal(mutation.ExpectedField, failure.Field);
                Assert.ThrowsAny<Exception>(() => new VliwDecoderV4().Decode(in instruction, mutation.SlotIndex));
            }
        }
    }

    [Fact]
    public void MutationCorpus_ReportLockMatchesTheExecutableCSharpCorpus()
    {
        CorpusReport? report = JsonSerializer.Deserialize<CorpusReport>(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "Documentation",
                "ArchitectureAuthorityRefactor",
                "Evidence",
                "RF05",
                "rf05-decoder-mutation-corpus.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(report);
        Assert.Equal(1, report!.SchemaVersion);
        Assert.Equal("CSharp executable corpus; JSON evidence lock only", report.Authority);

        CorpusReportFamily[] actual = Corpus
            .GroupBy(static test => test.FamilyId, StringComparer.Ordinal)
            .Select(static family => new CorpusReportFamily(
                family.Key,
                family.Count(static test => test.IsAccepted),
                family.Count(static test => !test.IsAccepted)))
            .OrderBy(static family => family.Id, StringComparer.Ordinal)
            .ToArray();
        CorpusReportFamily[] expected = report.Families
            .OrderBy(static family => family.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(actual, expected);
    }

    private static MutationCase Accept(string familyId, string caseId, int slotIndex, VLIW_Instruction instruction) =>
        new(familyId, caseId, true, slotIndex, instruction);

    private static MutationCase Reject(
        string familyId,
        string caseId,
        int slotIndex,
        VLIW_Instruction instruction,
        string expectedField) =>
        new(familyId, caseId, false, slotIndex, instruction, expectedField);

    private static VLIW_Instruction Scalar(
        ushort opcode,
        byte rd = 1,
        byte rs1 = 2,
        byte rs2 = 3,
        ushort immediate = 0,
        byte reserved = 0,
        ulong word3 = 0) =>
        new()
        {
            OpCode = opcode,
            Immediate = immediate,
            Reserved = reserved,
            Word1 = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Word3 = word3,
        };

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
