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
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// Generated-catalog RF-05 parity gate. Every declared static descriptor gets
/// one canonical legal carrier; dynamic owner/domain and execution readiness
/// remain outside this decode-only proof.
/// </summary>
public sealed class DecoderGeneratedDescriptorDifferentialTests
{
    private sealed record BundleInput(
        int OccupiedSlotIndex,
        VLIW_Instruction[] Slots,
        VliwBundleAnnotations? Annotations);

    private sealed record Report(int SchemaVersion, string Scope, int GeneratedDescriptorCount, int Accepted, int Rejected, int SemanticProjectionDifferences);

    [Fact]
    public void EveryGeneratedDescriptor_HasCanonicalCarrierWithBundleProjectionParity()
    {
        GeneratedIsaDescriptor[] descriptors = GeneratedIsaCatalog.Descriptors
            .OrderBy(static descriptor => descriptor.Opcode)
            .ToArray();
        Assert.NotEmpty(descriptors);

        int accepted = 0;
        foreach (GeneratedIsaDescriptor descriptor in descriptors)
        {
            BundleInput input = CreateCanonicalInput(descriptor);
            bool declarativeAccepted = DeclarativeDecoderPipeline.TryDecodeBundle(
                input.Slots,
                input.Annotations,
                bundleAddress: 0x6100,
                bundleSerial: 47,
                out DeclarativeDecodedBundle? declarative,
                out DecodeFailure? failure);
            Assert.True(
                declarativeAccepted,
                $"Generated descriptor {descriptor.Mnemonic} ({descriptor.Opcode}) canonical carrier was rejected by declarative decoder: {failure?.Code}/{failure?.Field}.");

            try
            {
                DecodedInstructionBundle legacy = new VliwDecoderV4().DecodeInstructionBundle(
                    input.Slots,
                    input.Annotations,
                    bundleAddress: 0x6100,
                    bundleSerial: 47);
                AssertBundleProjectionParity(legacy, declarative!);
                accepted++;
            }
            catch (Exception exception) when (exception is InvalidOpcodeException or InvalidOperationException)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Generated descriptor {descriptor.Mnemonic} ({descriptor.Opcode}) canonical carrier was accepted by declarative decode but rejected by legacy decode: {exception.Message}");
            }
        }

        Report report = LoadReport();
        Assert.Equal(descriptors.Length, report.GeneratedDescriptorCount);
        Assert.Equal(accepted, report.Accepted);
        Assert.Equal(0, report.Rejected);
        Assert.Equal(0, report.SemanticProjectionDifferences);
    }

    private static BundleInput CreateCanonicalInput(GeneratedIsaDescriptor descriptor)
    {
        ushort opcode = checked((ushort)descriptor.Opcode);
        if (opcode == IsaOpcodeValues.DmaStreamCompute)
        {
            return CreateDmaInput();
        }

        if (OpcodeRegistry.IsSystemDeviceCommandOpcode(opcode))
        {
            return CreateAcceleratorInput((InstructionsEnum)opcode);
        }

        int slotIndex = opcode is IsaOpcodeValues.DSC_STATUS or IsaOpcodeValues.DSC_QUERY_CAPS
            ? 6
            : OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? 3 : 0;
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[slotIndex] = new VLIW_Instruction
        {
            OpCode = opcode,
            Word1 = VLIW_Instruction.PackArchRegs(0, 0, 0),
            DestSrc1Pointer = OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? 0x1000UL : 0,
            Src2Pointer = OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? 0x2000UL : 0,
            StreamLength = OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? 8U : 0,
            Stride = OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? (ushort)4 : (ushort)0,
            RowStride = OpcodeRegistry.RequiresVectorPayloadProjection(opcode) ? (ushort)32 : (ushort)0,
            DataType = (byte)DataTypeEnum.INT32,
        };
        return new BundleInput(slotIndex, slots, null);
    }

    private static BundleInput CreateDmaInput()
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[6] = new VLIW_Instruction { OpCode = IsaOpcodeValues.DmaStreamCompute };
        var metadata = EmptyMetadata();
        metadata[6] = new InstructionSlotMetadata(VtId.Create(0), SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor(),
        };
        return new BundleInput(6, slots, new VliwBundleAnnotations(metadata));
    }

    private static BundleInput CreateAcceleratorInput(InstructionsEnum opcode)
    {
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[7] = L7SdcPhase03TestCases.CreateNativeInstruction(opcode);
        if (opcode != InstructionsEnum.ACCEL_SUBMIT)
        {
            return new BundleInput(7, slots, null);
        }

        AcceleratorCommandDescriptor descriptor = L7SdcTestDescriptorFactory.ParseValidDescriptor();
        return new BundleInput(
            7,
            slots,
            L7SdcNativeCarrierValidationTests.CreateAnnotations(
                7,
                descriptor,
                L7SdcNativeCarrierValidationTests.CreateSystemSingletonSlotMetadata()));
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
            Assert.Equal(legacyProjection.CsrAddress, canonicalSlot.CsrAddress);
            Assert.Equal(legacyProjection.AcquireOrdering, canonicalSlot.AcquireOrdering);
            Assert.Equal(legacyProjection.ReleaseOrdering, canonicalSlot.ReleaseOrdering);
            Assert.Equal(legacyProjection.VectorPayload, declarativeSlot.VectorPayload);
            Assert.Equal((uint)legacyProjection.CanonicalOpcode, canonicalSlot.Opcode);
            Assert.Equal(legacyProjection.Class, canonicalSlot.InstructionClass);
            Assert.Equal(legacyProjection.SerializationClass, canonicalSlot.SerializationClass);
        }
    }

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
                "rf05-generated-descriptor-differential-report.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(report);
        Assert.Equal(1, report!.SchemaVersion);
        Assert.Equal("every generated ISA descriptor with one canonical legal carrier", report.Scope);
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
