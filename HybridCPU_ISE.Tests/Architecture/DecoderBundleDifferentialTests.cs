using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.MemoryAccelerators;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-05 whole-bundle differential corpus for the static descriptor/sideband
/// boundary. It deliberately excludes owner/domain admission and projection
/// faults, which have different authorities.
/// </summary>
public sealed class DecoderBundleDifferentialTests
{
    private sealed record BundleInput(VLIW_Instruction[] Slots, VliwBundleAnnotations? Annotations);

    private sealed record BundleCase(string RuleId, string CaseId, bool IsAccepted, Func<BundleInput> Create);

    private sealed record Report(int SchemaVersion, string Scope, int Accepted, int Rejected, int SemanticProjectionDifferences);

    private static readonly BundleCase[] Corpus =
    [
        Accept("baseline-bundle", "scalar-clean", ScalarBundle),
        Accept("dsc-queue-control-fixed-lane", "dsc-status-lane6-clean", () => DscStatusBundle()),
        Accept("dma-native-carrier-abi", "dma-lane6-valid-descriptor", ValidDmaBundle),
        Accept("accelerator-native-carrier-abi", "accel-query-caps-lane7-clean", AcceleratorQueryCapsBundle),
        Accept("accelerator-submit-required-descriptor", "accel-submit-lane7-valid-descriptor", ValidAcceleratorSubmitBundle),

        Reject("empty-slot-descriptor-sideband", "dma-descriptor-on-empty-slot", () => EmptyDescriptorBundle(dma: true)),
        Reject("empty-slot-descriptor-sideband", "accelerator-descriptor-on-empty-slot", () => EmptyDescriptorBundle(dma: false)),
        Reject("dma-descriptor-opcode-association", "dma-descriptor-on-scalar", () => DescriptorOnScalarBundle(dma: true)),
        Reject("accelerator-descriptor-opcode-association", "accelerator-descriptor-on-scalar", () => DescriptorOnScalarBundle(dma: false)),
        Reject("dma-fixed-lane", "dma-lane5-valid-descriptor", () => DmaBundle(slotIndex: 5, attachDescriptor: true)),
        Reject("dma-required-descriptor", "dma-lane6-without-descriptor", () => DmaBundle(slotIndex: 6, attachDescriptor: false)),
        Reject("dma-native-carrier-abi", "dma-vt-hint", () => DmaBundle(slotIndex: 6, attachDescriptor: true, mutate: static instruction => { instruction.VirtualThreadId = 1; return instruction; })),
        Reject("dsc-queue-control-fixed-lane", "dsc-status-lane5", () => DscStatusBundle(slotIndex: 5)),
        Reject("dma-descriptor-opcode-association", "dsc-status-with-dma-descriptor", DscStatusWithDmaDescriptorBundle),
        Reject("accelerator-submit-required-descriptor", "accel-submit-without-descriptor", () => AcceleratorBundle(InstructionsEnum.ACCEL_SUBMIT, slotIndex: 7, attachDescriptor: false)),
        Reject("accelerator-native-carrier-abi", "accel-query-caps-lane6", () => AcceleratorBundle(InstructionsEnum.ACCEL_QUERY_CAPS, slotIndex: 6, attachDescriptor: false)),
        Reject("accelerator-native-carrier-abi", "accel-query-caps-vt-hint", () => AcceleratorBundle(InstructionsEnum.ACCEL_QUERY_CAPS, slotIndex: 7, attachDescriptor: false, mutate: static instruction => { instruction.VirtualThreadId = 1; return instruction; })),
        Reject("accelerator-descriptor-opcode-association", "accel-poll-with-submit-descriptor", () => AcceleratorBundle(InstructionsEnum.ACCEL_POLL, slotIndex: 7, attachDescriptor: true)),
    ];

    [Fact]
    public void BundleDifferentialCorpus_CoversEveryStaticSidebandRuleAndTheEmptySlotBoundary()
    {
        string[] expected = SidebandValidator.RegisteredStaticRuleIds
            .Append("empty-slot-descriptor-sideband")
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        string[] actual = Corpus
            .Where(static test => !test.IsAccepted)
            .Select(static test => test.RuleId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BundleDifferentialCorpus_HasZeroUnexplainedAcceptRejectOrSemanticProjectionDifferences()
    {
        foreach (BundleCase test in Corpus)
        {
            BundleInput input = test.Create();
            bool accepted = DeclarativeDecoderPipeline.TryDecodeBundle(
                input.Slots,
                input.Annotations,
                bundleAddress: 0x5A00,
                bundleSerial: 31,
                out DeclarativeDecodedBundle? declarative,
                out DecodeFailure? failure);
            Assert.True(
                accepted == test.IsAccepted,
                $"{test.RuleId}/{test.CaseId}: expected accepted={test.IsAccepted}, " +
                $"actual accepted={accepted}, failure={failure?.Code}/{failure?.Field}.");

            if (!test.IsAccepted)
            {
                Assert.NotNull(failure);
                Assert.Equal(DecodeFailureCode.Sideband, failure!.Code);
                Assert.ThrowsAny<Exception>(() => new VliwDecoderV4().DecodeInstructionBundle(
                    input.Slots,
                    input.Annotations,
                    bundleAddress: 0x5A00,
                    bundleSerial: 31));
                continue;
            }

            Assert.NotNull(declarative);
            DecodedInstructionBundle legacy = new VliwDecoderV4().DecodeInstructionBundle(
                input.Slots,
                input.Annotations,
                bundleAddress: 0x5A00,
                bundleSerial: 31);
            AssertBundleProjectionParity(legacy, declarative!);
        }
    }

    [Fact]
    public void BundleDifferentialReportLock_MatchesExecutableCorpus()
    {
        Report? report = JsonSerializer.Deserialize<Report>(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "Documentation",
                "Documentation",
                "ArchitectureAuthorityRefactor",
                "Evidence",
                "RF05",
                "rf05-bundle-differential-report.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(report);
        Assert.Equal(1, report!.SchemaVersion);
        Assert.Equal("known static descriptor/sideband paths and accepted carrier witnesses", report.Scope);
        Assert.Equal(Corpus.Count(static test => test.IsAccepted), report.Accepted);
        Assert.Equal(Corpus.Count(static test => !test.IsAccepted), report.Rejected);
        Assert.Equal(0, report.SemanticProjectionDifferences);
    }

    private static BundleCase Accept(string ruleId, string caseId, Func<BundleInput> create) =>
        new(ruleId, caseId, true, create);

    private static BundleCase Reject(string ruleId, string caseId, Func<BundleInput> create) =>
        new(ruleId, caseId, false, create);

    private static BundleInput ScalarBundle()
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.ADD,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3),
        };
        return new BundleInput(slots, null);
    }

    private static BundleInput DscStatusBundle(int slotIndex = 6)
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[slotIndex] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.DSC_STATUS,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 0),
        };
        return new BundleInput(slots, null);
    }

    private static BundleInput DscStatusWithDmaDescriptorBundle()
    {
        BundleInput input = DscStatusBundle();
        var metadata = EmptyMetadata();
        metadata[6] = InstructionSlotMetadata.Default with
        {
            DmaStreamComputeDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor(),
        };
        return new BundleInput(input.Slots, new VliwBundleAnnotations(metadata));
    }

    private static BundleInput ValidDmaBundle() => DmaBundle(slotIndex: 6, attachDescriptor: true);

    private static BundleInput DmaBundle(
        int slotIndex,
        bool attachDescriptor,
        Func<VLIW_Instruction, VLIW_Instruction>? mutate = null)
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        VLIW_Instruction instruction = new()
        {
            OpCode = IsaOpcodeValues.DmaStreamCompute,
            DataType = 0,
            PredicateMask = 0,
            Immediate = 0,
            DestSrc1Pointer = 0,
            Src2Pointer = 0,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0,
        };
        instruction = mutate?.Invoke(instruction) ?? instruction;
        slots[slotIndex] = instruction;

        if (!attachDescriptor)
        {
            return new BundleInput(slots, null);
        }

        var metadata = EmptyMetadata();
        metadata[slotIndex] = new InstructionSlotMetadata(VtId.Create(0), SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor(),
        };
        return new BundleInput(slots, new VliwBundleAnnotations(metadata));
    }

    private static BundleInput AcceleratorQueryCapsBundle() =>
        AcceleratorBundle(InstructionsEnum.ACCEL_QUERY_CAPS, slotIndex: 7, attachDescriptor: false);

    private static BundleInput ValidAcceleratorSubmitBundle() =>
        AcceleratorBundle(InstructionsEnum.ACCEL_SUBMIT, slotIndex: 7, attachDescriptor: true);

    private static BundleInput AcceleratorBundle(
        InstructionsEnum opcode,
        int slotIndex,
        bool attachDescriptor,
        Func<VLIW_Instruction, VLIW_Instruction>? mutate = null)
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        VLIW_Instruction instruction = L7SdcPhase03TestCases.CreateNativeInstruction(opcode);
        instruction = mutate?.Invoke(instruction) ?? instruction;
        slots[slotIndex] = instruction;

        if (!attachDescriptor)
        {
            return new BundleInput(slots, null);
        }

        AcceleratorCommandDescriptor descriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor();
        return new BundleInput(
            slots,
            L7SdcNativeCarrierValidationTests.CreateAnnotations(
                slotIndex,
                descriptor,
                L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()));
    }

    private static BundleInput DescriptorOnScalarBundle(bool dma)
    {
        BundleInput input = ScalarBundle();
        var metadata = EmptyMetadata();
        metadata[0] = dma
            ? InstructionSlotMetadata.Default with { DmaStreamComputeDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor() }
            : InstructionSlotMetadata.Default with { AcceleratorCommandDescriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor() };
        return new BundleInput(input.Slots, new VliwBundleAnnotations(metadata));
    }

    private static BundleInput EmptyDescriptorBundle(bool dma)
    {
        var metadata = EmptyMetadata();
        metadata[2] = dma
            ? InstructionSlotMetadata.Default with { DmaStreamComputeDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor() }
            : InstructionSlotMetadata.Default with { AcceleratorCommandDescriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor() };
        return new BundleInput(new VLIW_Instruction[BundleMetadata.BundleSlotCount], new VliwBundleAnnotations(metadata));
    }

    private static InstructionSlotMetadata[] EmptyMetadata()
    {
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        Array.Fill(metadata, InstructionSlotMetadata.Default);
        return metadata;
    }

    private static void AssertBundleProjectionParity(DecodedInstructionBundle legacy, DeclarativeDecodedBundle declarative)
    {
        for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            DecodedInstruction legacySlot = legacy.GetDecodedSlot(slotIndex);
            CanonicalDecodedInstruction canonicalSlot = declarative.CanonicalBundle.GetSlot(slotIndex);
            Assert.Equal(legacySlot.IsOccupied, canonicalSlot.IsOccupied);
            if (!legacySlot.IsOccupied)
            {
                continue;
            }

            InstructionIR legacyProjection = legacySlot.RequireInstruction();
            DeclarativeDecodedSlot declarativeSlot = Assert.IsType<DeclarativeDecodedSlot>(declarative.Slots[slotIndex]);
            Assert.Equal(legacyProjection.Rd, declarativeSlot.Operands.Rd);
            Assert.Equal(legacyProjection.Rs1, declarativeSlot.Operands.Rs1);
            Assert.Equal(legacyProjection.Rs2, declarativeSlot.Operands.Rs2);
            Assert.Equal(legacyProjection.Imm, declarativeSlot.Operands.Immediate);
            Assert.Equal(legacyProjection.VectorPayload, declarativeSlot.VectorPayload);
            Assert.Equal((uint)legacyProjection.CanonicalOpcode, canonicalSlot.Opcode);
            Assert.Equal(legacyProjection.Class, canonicalSlot.InstructionClass);
            Assert.Equal(legacyProjection.SerializationClass, canonicalSlot.SerializationClass);
        }
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
