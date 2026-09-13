using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06SpecializedCapabilityProjectionTests
{
    [Fact]
    public void ControlAndSerializingFamilies_ProjectExactStaticCarrierFacts()
    {
        BranchMicroOp branch = new()
        {
            OpCode = IsaOpcodeValues.JAL,
            IsConditional = false,
        };
        Rf06ControlCapability control = Rf06SpecializedCapabilityProjection.ProjectControl(
            branch,
            BindingFor(IsaOpcodeValues.JAL));

        Assert.True(control.RedirectsProgramCounter);
        Assert.False(control.IsStealable);
        Assert.Equal(SlotClass.BranchControl, control.Contract.Placement.RequiredSlotClass);
        Assert.Equal("PcWrite", control.Contract.StaticEffectContract);

        CsrReadCounterMicroOp csr = new()
        {
            OpCode = IsaOpcodeValues.RDCYCLE,
            DestRegID = 3,
        };
        csr.InitializeMetadata();
        Rf06SerializingCapability serializing =
            Rf06SpecializedCapabilityProjection.ProjectSerializing(
                csr,
                BindingFor(IsaOpcodeValues.RDCYCLE));

        Assert.Equal(SerializationClass.CsrOrdered, serializing.SerializationClass);
        Assert.False(serializing.Contract.IsStealable);
        Assert.Equal(SlotClass.SystemSingleton, serializing.RequiredSlotClass);
        Assert.Equal(YAKSys_Hybrid_CPU.Arch.InstructionClass.Csr, serializing.Contract.InstructionClass);
    }

    [Fact]
    public void AssistProjection_IsNonRetiringAndExcludesMutableQuotaState()
    {
        Rf06AssistCapability capability = ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                AssistMicroOp assist = new(
                    AssistKind.Ldsa,
                    AssistExecutionMode.CachePrefetch,
                    AssistCarrierKind.LsuHosted,
                    baseAddress: 0x1000,
                    prefetchLength: 32,
                    elementSize: 4,
                    elementCount: 8,
                    new AssistOwnerBinding(
                        carrierVirtualThreadId: 0,
                        donorVirtualThreadId: 0,
                        targetVirtualThreadId: 0,
                        ownerContextId: 7,
                        domainTag: 9,
                        replayEpochId: 11,
                        assistEpochId: 13,
                        LocalityHint.None));

                return Rf06SpecializedCapabilityProjection.ProjectAssist(
                    assist,
                    BindingFor(IsaOpcodeValues.ADD));
            });

        Assert.False(capability.Contract.IsRetireVisible);
        Assert.True(capability.Contract.IsAssist);
        Assert.Equal(MemoryCapabilityKind.Load, capability.Contract.Memory.Kind);
        Assert.Equal(new MemoryBankId(0), capability.Bank);
        Assert.DoesNotContain(
            capability.Contract.GetType().GetProperties().Select(property => property.Name),
            name => name.Contains("Budget", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Outcome", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DmaProjection_FreezesIndependentReadWriteFootprints()
    {
        DmaStreamComputeDescriptor descriptor =
            DmaStreamComputeTestDescriptorFactory.CreateDescriptor();
        DmaStreamComputeMicroOp carrier = new(descriptor);

        Rf06DmaCapability capability = Rf06SpecializedCapabilityProjection.ProjectDma(
            carrier,
            BindingFor(IsaOpcodeValues.DmaStreamCompute));

        Assert.Equal(SlotClass.DmaStreamClass, capability.Contract.Placement.RequiredSlotClass);
        Assert.Equal((byte)6, capability.Contract.Placement.PinnedLaneId);
        Assert.Equal(descriptor.DescriptorIdentityHash, capability.DescriptorIdentityHash);
        Assert.Equal(
            carrier.ReadMemoryRanges.Count,
            capability.ReadFootprint.Length);
        Assert.Equal(
            carrier.WriteMemoryRanges.Count,
            capability.WriteFootprint.Length);
        Assert.Equal(MemoryCapabilityKind.NoMemory, capability.Contract.Memory.Kind);
    }

    [Fact]
    public void AcceleratorAndMatrixTileFamilies_KeepPlacementAndDependencyContracts()
    {
        AcceleratorWaitMicroOp accelerator = new(destinationRegister: 3, tokenRegister: 4);
        Rf06AcceleratorCapability acceleratorCapability =
            Rf06SpecializedCapabilityProjection.ProjectAccelerator(
                accelerator,
                BindingFor(IsaOpcodeValues.ACCEL_WAIT));

        Assert.Equal(SystemDeviceCommandKind.Wait, acceleratorCapability.CommandKind);
        Assert.False(acceleratorCapability.Contract.IsStealable);
        Assert.Equal((byte)7, acceleratorCapability.PinnedLaneId);
        Assert.Equal(SerializationClass.FullSerial, acceleratorCapability.Contract.SerializationClass);

        Rf06MatrixTileCapability matrixCapability =
            CreateMatrixTileCapability();
        Assert.Equal(MatrixTileProjectedOperationKind.Load, matrixCapability.OperationKind);
        Assert.Equal(MatrixTileRuntimeResourceClass.MatrixTileMemory, matrixCapability.RuntimeResourceClass);
        Assert.True(matrixCapability.WritesTileState);
        Assert.Equal(SlotClass.MatrixTileStreamClass, matrixCapability.RequiredSlotClass);
        Assert.Equal(MemoryCapabilityKind.Load, matrixCapability.Contract.Memory.Kind);
    }

    [Fact]
    public void FamilyProjection_RejectsBindingFromAnotherCarrier()
    {
        BranchMicroOp branch = new() { OpCode = IsaOpcodeValues.JAL };

        Assert.Throws<InvalidOperationException>(() =>
            Rf06SpecializedCapabilityProjection.ProjectControl(
                branch,
                BindingFor(IsaOpcodeValues.BNE)));
    }

    [Fact]
    public void FamilyProjection_DoesNotCreateSchedulerOrExecutionIdentity()
    {
        DirectoryInfo root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root.FullName,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Decoder",
            "Rf06SpecializedCapabilityProjection.cs"));

        Assert.DoesNotContain("InstructionRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeRegistry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryFromOpcode", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new MicroOp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdmissionRecord", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VliwOperationId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutionOutcome", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReplayEntry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RemainingLatency", source, StringComparison.Ordinal);
    }

    private static Rf06MatrixTileCapability CreateMatrixTileCapability()
    {
        return ProcessorMemoryScope.WithProcessorMemory(
            ProcessorMemoryScope.CreateMemorySubsystem(16, 64),
            () =>
            {
                var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
                slots[0] = InstructionEncoder.EncodeVector1D(
                    (uint)InstructionsEnum.MTILE_LOAD,
                    DataTypeEnum.INT32,
                    destSrc1Ptr: 0x1000,
                    src2Ptr: 0x2000,
                    streamLength: 4,
                    stride: 16);
                var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
                for (int index = 0; index < metadata.Length; index++)
                {
                    metadata[index] = InstructionSlotMetadata.Default;
                }

                DecodedInstructionBundle bundle = new VliwDecoderV4().DecodeInstructionBundle(
                    slots,
                    new VliwBundleAnnotations(metadata),
                    bundleAddress: 0x1000,
                    bundleSerial: 11);
                MicroOp?[] carriers =
                    DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                        slots,
                        bundle);
                MatrixTileMicroOp matrix = Assert.IsType<MtileLoadMicroOp>(carriers[0]);
                return Rf06SpecializedCapabilityProjection.ProjectMatrixTile(
                    matrix,
                    BindingFor(IsaOpcodeValues.MTILE_LOAD));
            });
    }

    private static GeneratedStaticBinding BindingFor(ushort opcode)
    {
        Assert.True(
            GeneratedStaticBinding.TryFromOpcode(opcode, out GeneratedStaticBinding binding),
            $"Generated binding missing for opcode {opcode}.");
        return binding;
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current;
            }
        }

        throw new DirectoryNotFoundException("HybridCPU ISE repository root was not found.");
    }
}
