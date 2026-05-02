using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Accelerators;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeTypedSlotTests
{
    [Fact]
    public void DmaStreamComputeMicroOp_RequiresLane6DmaStreamClassWithoutAluOccupancy()
    {
        DmaStreamComputeMicroOp microOp = CreateValidMicroOp();

        Assert.Equal(SlotClass.DmaStreamClass, microOp.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, microOp.Placement.PinningKind);
        Assert.Equal((byte)0b_0100_0000, SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass));
        Assert.Equal(1, SlotClassLaneMap.GetClassCapacity(SlotClass.DmaStreamClass));
        Assert.Equal(4, SlotClassLaneMap.GetClassCapacity(SlotClass.AluClass));

        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        bundle[6] = microOp;

        SlotClassCapacityState capacity = SlotClassCapacity.ComputeFromBundle(bundle);
        TypedSlotBundleFacts facts = TypedSlotBundleFacts.FromBundle(bundle);

        Assert.Equal(1, capacity.DmaStreamOccupied);
        Assert.Equal(0, capacity.AluOccupied);
        Assert.Equal(0, capacity.LsuOccupied);
        Assert.Equal(1, facts.DmaStreamCount);
        Assert.Equal(0, facts.AluCount);
        Assert.Equal(0, facts.LsuCount);
    }

    [Fact]
    public void DmaStreamComputeMicroOp_PublishesRetireVisibleNonAssistFaultVisibleSemantics()
    {
        DmaStreamComputeMicroOp microOp = CreateValidMicroOp();

        Assert.False(microOp.IsAssist);
        Assert.True(microOp.IsRetireVisible);
        Assert.False(microOp.IsReplayDiscardable);
        Assert.False(microOp.SuppressesArchitecturalFaults);
        Assert.True(microOp.IsMemoryOp);
        Assert.True(microOp.HasSideEffects);
        Assert.Equal(MicroOpClass.Dma, microOp.Class);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, microOp.CanonicalDecodePublication);
    }

    [Fact]
    public void DmaStreamComputeMicroOp_RequiresReadAndWriteFootprintsForMemoryWritingOps()
    {
        DmaStreamComputeDescriptor descriptor = ParseValidDescriptor();

        InvalidOperationException missingReads = Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor with
            {
                ReadMemoryRanges = Array.Empty<DmaStreamComputeMemoryRange>()
            }));
        InvalidOperationException missingWrites = Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeMicroOp(descriptor with
            {
                WriteMemoryRanges = Array.Empty<DmaStreamComputeMemoryRange>()
            }));

        Assert.Contains("ReadMemoryRanges", missingReads.Message, StringComparison.Ordinal);
        Assert.Contains("WriteMemoryRanges", missingWrites.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DmaStreamComputeMicroOp_PreservesDescriptorAndNormalizedFootprintIdentity()
    {
        DmaStreamComputeDescriptor descriptor = ParseValidDescriptor();
        DmaStreamComputeMicroOp microOp = new(descriptor);

        Assert.Equal(descriptor.DescriptorReference, microOp.DescriptorReference);
        Assert.Equal(descriptor.DescriptorIdentityHash, microOp.DescriptorIdentityHash);
        Assert.Equal(descriptor.NormalizedFootprintHash, microOp.NormalizedFootprintHash);
        Assert.Equal(descriptor.CertificateInputHash, microOp.CertificateInputHash);
        Assert.Equal(descriptor.Operation, microOp.Operation);
        Assert.Equal(descriptor.ElementType, microOp.ElementType);
        Assert.Equal(descriptor.Shape, microOp.Shape);

        Assert.Equal(
            descriptor.ReadMemoryRanges.Select(range => (range.Address, range.Length)).ToArray(),
            microOp.ReadMemoryRanges);
        Assert.Equal(
            descriptor.WriteMemoryRanges.Select(range => (range.Address, range.Length)).ToArray(),
            microOp.WriteMemoryRanges);
    }

    [Fact]
    public void BundleResourceCertificate4Way_CarriesDmaStreamComputeDescriptorAndFootprintIdentity()
    {
        DmaStreamComputeDescriptor descriptor = ParseValidDescriptor();
        DmaStreamComputeMicroOp microOp = new(descriptor);

        BundleResourceCertificate4Way certificate = BundleResourceCertificate4Way.Empty;
        certificate.AddOperation(microOp);

        Assert.Equal(1, certificate.DmaStreamComputeIdentityCount);
        Assert.Equal(descriptor.DescriptorIdentityHash, certificate.DmaStreamComputeDescriptorIdentityHash);
        Assert.Equal(descriptor.NormalizedFootprintHash, certificate.DmaStreamComputeNormalizedFootprintHash);
        Assert.Equal(1, certificate.ClassOccupancy.DmaStreamOccupied);

        DmaStreamComputeDescriptor otherDescriptor = descriptor with
        {
            DescriptorReference = new DmaStreamComputeDescriptorReference(0x8800, descriptor.TotalSize, OtherIdentityHash),
            DescriptorIdentityHash = OtherIdentityHash
        };
        BundleResourceCertificate4Way otherCertificate = BundleResourceCertificate4Way.Empty;
        otherCertificate.AddOperation(new DmaStreamComputeMicroOp(otherDescriptor));

        Assert.NotEqual(certificate.StructuralIdentity, otherCertificate.StructuralIdentity);
    }

    [Fact]
    public void DmaStreamComputeMicroOp_ExecuteThrowsFailClosedWhileExecutionDisabled()
    {
        DmaStreamComputeMicroOp microOp = CreateValidMicroOp();
        var core = new Processor.CPU_Core(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));

        Assert.False(DmaStreamComputeDescriptorParser.ExecutionEnabled);
        Assert.Contains("execution is disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomAcceleratorMicroOp_ExecuteStillThrowsFailClosed()
    {
        var microOp = new CustomAcceleratorMicroOp
        {
            OpCode = 0xC000
        };
        var core = new Processor.CPU_Core(0);

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => microOp.Execute(ref core));

        Assert.Contains("custom accelerator", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RegisteredCustomAcceleratorOpcode_StillFailsClosedAndIsNotDmaStreamComputePath()
    {
        InstructionRegistry.Initialize();
        InstructionRegistry.RegisterAccelerator(new MatMulAccelerator());

        var context = new DecoderContext
        {
            OpCode = 0xC000,
            Reg1ID = 1,
            Reg2ID = 2,
            Reg3ID = 3
        };

        Assert.True(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => InstructionRegistry.CreateMicroOp(0xC000, context));

        Assert.Contains("custom accelerator", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedSlotRejectReason_ExistingValuesRemainAppendOnlyStable()
    {
        Assert.Equal(0, (byte)TypedSlotRejectReason.None);
        Assert.Equal(1, (byte)TypedSlotRejectReason.StaticClassOvercommit);
        Assert.Equal(2, (byte)TypedSlotRejectReason.DynamicClassExhaustion);
        Assert.Equal(3, (byte)TypedSlotRejectReason.ResourceConflict);
        Assert.Equal(4, (byte)TypedSlotRejectReason.DomainReject);
        Assert.Equal(5, (byte)TypedSlotRejectReason.ScoreboardReject);
        Assert.Equal(6, (byte)TypedSlotRejectReason.BankPendingReject);
        Assert.Equal(7, (byte)TypedSlotRejectReason.HardwareBudgetReject);
        Assert.Equal(8, (byte)TypedSlotRejectReason.SpeculationBudgetReject);
        Assert.Equal(9, (byte)TypedSlotRejectReason.AssistQuotaReject);
        Assert.Equal(10, (byte)TypedSlotRejectReason.AssistBackpressureReject);
        Assert.Equal(11, (byte)TypedSlotRejectReason.PinnedLaneConflict);
        Assert.Equal(12, (byte)TypedSlotRejectReason.LateBindingConflict);
        Assert.Equal(20, (byte)TypedSlotRejectReason.FairnessDeferred);
    }

    [Fact]
    public void Phase10_AddsNativeIsaCompilerEmissionWithoutCustomAcceleratorRegistryPath()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string registryText = ReadAllSourceText(Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Diagnostics"), "InstructionRegistry*.cs");
        string compilerText = ReadAllSourceText(Path.Combine(repoRoot, "HybridCPU_Compiler"), "*.cs");

        Assert.DoesNotContain(nameof(DmaStreamComputeMicroOp), registryText, StringComparison.Ordinal);
        Assert.False(InstructionRegistry.IsCustomAcceleratorOpcode((uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute));
        Assert.True(InstructionRegistry.IsRegistered((uint)Processor.CPU_Core.InstructionsEnum.DmaStreamCompute));
        Assert.Contains("DmaStreamCompute", registryText, StringComparison.Ordinal);
        Assert.Contains("DmaStreamCompute", compilerText, StringComparison.Ordinal);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const ulong OtherIdentityHash = 0xBEEFBEEFUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;
    private const int HeaderAbiVersionOffset = 4;
    private const int HeaderOperationOffset = 40;
    private const int HeaderElementTypeOffset = 42;
    private const int HeaderShapeOffset = 44;

    private static DmaStreamComputeMicroOp CreateValidMicroOp() =>
        new(ParseValidDescriptor());

    private static DmaStreamComputeDescriptor ParseValidDescriptor()
    {
        byte[] descriptorBytes = BuildDescriptor();
        var reference = new DmaStreamComputeDescriptorReference(
            descriptorAddress: 0x8000,
            descriptorSize: (uint)descriptorBytes.Length,
            descriptorIdentityHash: IdentityHash);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes, reference),
                reference);

        Assert.True(result.IsValid, result.Message);
        return result.RequireDescriptorForAdmission();
    }

    private static DmaStreamComputeOwnerGuardDecision CreateGuardDecision(
        byte[] descriptorBytes,
        DmaStreamComputeDescriptorReference descriptorReference)
    {
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(
                descriptorBytes,
                descriptorReference);
        Assert.True(structuralRead.IsValid, structuralRead.Message);
        DmaStreamComputeOwnerBinding ownerBinding =
            structuralRead.RequireOwnerBindingForGuard();
        var context = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId);
        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
            ownerBinding,
            context);
    }

    private static byte[] BuildDescriptor(
        DmaStreamComputeOperationKind operation = DmaStreamComputeOperationKind.Add,
        DmaStreamComputeElementType elementType = DmaStreamComputeElementType.UInt32,
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D)
    {
        ushort sourceRangeCount = operation switch
        {
            DmaStreamComputeOperationKind.Copy => 1,
            DmaStreamComputeOperationKind.Fma => 3,
            _ => 2
        };
        ushort destinationRangeCount = 1;
        int sourceRangeTableOffset = HeaderSize;
        int destinationRangeTableOffset = HeaderSize + (sourceRangeCount * RangeEntrySize);
        uint totalSize = (uint)(HeaderSize + ((sourceRangeCount + destinationRangeCount) * RangeEntrySize));
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Magic);
        WriteUInt16(bytes, HeaderAbiVersionOffset, DmaStreamComputeDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, 6, HeaderSize);
        WriteUInt32(bytes, 8, totalSize);
        WriteUInt64(bytes, 24, IdentityHash);
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, HeaderOperationOffset, (ushort)operation);
        WriteUInt16(bytes, HeaderElementTypeOffset, (ushort)elementType);
        WriteUInt16(bytes, HeaderShapeOffset, (ushort)shape);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt32(bytes, 64, 77);
        WriteUInt32(bytes, 68, 1);
        WriteUInt32(bytes, 72, 2);
        WriteUInt32(bytes, 76, DmaStreamComputeDescriptor.CanonicalLane6DeviceId);
        WriteUInt64(bytes, 80, 0xD0A11);
        WriteUInt32(bytes, 96, (uint)sourceRangeTableOffset);
        WriteUInt32(bytes, 100, (uint)destinationRangeTableOffset);

        for (int i = 0; i < sourceRangeCount; i++)
        {
            WriteRange(bytes, sourceRangeTableOffset + (i * RangeEntrySize), 0x1000UL + ((ulong)i * 0x1000UL), 16);
        }

        WriteRange(bytes, destinationRangeTableOffset, 0x9000, 16);
        return bytes;
    }

    private static void WriteRange(byte[] bytes, int offset, ulong address, ulong length)
    {
        WriteUInt64(bytes, offset, address);
        WriteUInt64(bytes, offset + 8, length);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);

    private static string ReadAllSourceText(string root, string searchPattern)
    {
        Assert.True(Directory.Exists(root), $"Missing source root: {root}");
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
