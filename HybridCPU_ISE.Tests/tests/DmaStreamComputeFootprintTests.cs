using System;
using System.Buffers.Binary;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeFootprintTests
{
    [Fact]
    public void OverlappingDestinationRanges_FailWithAliasOverlapFaultBeforeDescriptorAcceptance()
    {
        byte[] descriptorBytes = BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 64),
                new DmaStreamComputeMemoryRange(0x2000, 64)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9000, 64),
                new DmaStreamComputeMemoryRange(0x9020, 64)
            });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.AliasOverlapFault, result.Fault);
        Assert.Null(result.Descriptor);
    }

    [Fact]
    public void PartialSourceDestinationOverlap_FailsClosedWithAliasOverlapFault()
    {
        byte[] descriptorBytes = BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 64),
                new DmaStreamComputeMemoryRange(0x2000, 64)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1020, 64)
            });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.AliasOverlapFault, result.Fault);
        Assert.Contains("source/destination", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactSourceDestinationInPlaceOverlap_IsAcceptedWithExplicitAliasPolicy()
    {
        byte[] descriptorBytes = BuildDescriptor(
            operation: DmaStreamComputeOperationKind.Copy,
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x3000, 64)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x3000, 64)
            });

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.True(result.IsValid, result.Message);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(DmaStreamComputeAliasPolicy.ExactInPlaceSnapshot, result.Descriptor.AliasPolicy);
        Assert.Single(result.Descriptor.NormalizedReadMemoryRanges);
        Assert.Single(result.Descriptor.NormalizedWriteMemoryRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x3000, 64), result.Descriptor.NormalizedReadMemoryRanges[0]);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x3000, 64), result.Descriptor.NormalizedWriteMemoryRanges[0]);
    }

    [Fact]
    public void EquivalentRangeOrdering_ProducesSameNormalizedFootprintHashAndRanges()
    {
        byte[] descriptorBytesA = BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x2000, 16),
                new DmaStreamComputeMemoryRange(0x1000, 16)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9000, 16)
            });
        byte[] descriptorBytesB = BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9000, 16)
            });

        DmaStreamComputeDescriptor descriptorA = ParseValid(descriptorBytesA);
        DmaStreamComputeDescriptor descriptorB = ParseValid(descriptorBytesB);

        Assert.Equal(descriptorB.NormalizedFootprintHash, descriptorA.NormalizedFootprintHash);
        Assert.Equal(
            descriptorB.NormalizedReadMemoryRanges,
            descriptorA.NormalizedReadMemoryRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x1000, 16), descriptorA.NormalizedReadMemoryRanges[0]);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x2000, 16), descriptorA.NormalizedReadMemoryRanges[1]);
    }

    [Fact]
    public void OverlappingReadRanges_NormalizeFootprintWithoutAliasFault()
    {
        byte[] descriptorBytes = BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 32),
                new DmaStreamComputeMemoryRange(0x1010, 32)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9000, 64)
            });

        DmaStreamComputeDescriptor descriptor = ParseValid(descriptorBytes);
        DmaStreamComputeMicroOp microOp = new(descriptor);

        Assert.Equal(DmaStreamComputeAliasPolicy.Disjoint, descriptor.AliasPolicy);
        Assert.Equal(2, descriptor.ReadMemoryRanges.Count);
        Assert.Single(descriptor.NormalizedReadMemoryRanges);
        Assert.Equal(new DmaStreamComputeMemoryRange(0x1000, 48), descriptor.NormalizedReadMemoryRanges[0]);
        Assert.Single(microOp.ReadMemoryRanges);
        Assert.Equal((0x1000UL, 48UL), microOp.ReadMemoryRanges[0]);
    }

    [Fact]
    public void DmaStreamComputeWriteFootprintOverlap_InvalidatesReplayEvidenceWithAppendOnlyReason()
    {
        DmaStreamComputeMicroOp writer = new(ParseValid(BuildDescriptor(
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9000, 64)
            })));
        DmaStreamComputeMicroOp replayEvidence = new(ParseValid(BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9020, 16),
                new DmaStreamComputeMemoryRange(0x4000, 16)
            },
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0xA000, 64)
            })));

        bool invalidates =
            SafetyVerifier.TryClassifyMemoryFootprintInvalidation(
                writer,
                replayEvidence,
                out ReplayPhaseInvalidationReason reason);

        Assert.True(invalidates);
        Assert.Equal(ReplayPhaseInvalidationReason.MemoryFootprintOverlap, reason);
        Assert.Equal(11, (byte)ReplayPhaseInvalidationReason.MemoryFootprintOverlap);

        var scheduler = new MicroOpScheduler();
        scheduler.SetReplayPhaseContext(new ReplayPhaseContext(
            isActive: true,
            epochId: 1,
            cachedPc: 0x1000,
            epochLength: 4,
            completedReplays: 1,
            validSlotCount: 1,
            stableDonorMask: 0b0100_0000,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None));
        var replayBundle = new MicroOp?[8];
        replayBundle[6] = replayEvidence;
        scheduler.PackBundleIntraCoreSmt(
            replayBundle,
            ownerVirtualThreadId: replayEvidence.OwnerThreadId,
            localCoreId: 0,
            eligibleVirtualThreadMask: 0);

        long invalidationsBefore = scheduler.PhaseCertificateInvalidations;

        Assert.True(scheduler.TryInvalidatePhaseCertificateTemplatesForMemoryFootprintOverlap(
            writer,
            replayEvidence));
        Assert.Equal(invalidationsBefore + 1, scheduler.PhaseCertificateInvalidations);
        Assert.Equal(
            ReplayPhaseInvalidationReason.MemoryFootprintOverlap,
            scheduler.LastPhaseCertificateInvalidationReason);
    }

    [Fact]
    public void UnsupportedRangeEncoding_GatherScatterRemainsFailClosed()
    {
        byte[] descriptorBytes = BuildDescriptor();
        WriteUInt16(descriptorBytes, HeaderRangeEncodingOffset, 99);

        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.False(result.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.UnsupportedShape, result.Fault);
        Assert.DoesNotContain("gather", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scatter", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplayInvalidationReason_ExistingValuesRemainAppendOnlyStable()
    {
        Assert.Equal(0, (byte)ReplayPhaseInvalidationReason.None);
        Assert.Equal(1, (byte)ReplayPhaseInvalidationReason.Completed);
        Assert.Equal(2, (byte)ReplayPhaseInvalidationReason.PcMismatch);
        Assert.Equal(3, (byte)ReplayPhaseInvalidationReason.Manual);
        Assert.Equal(4, (byte)ReplayPhaseInvalidationReason.CertificateMutation);
        Assert.Equal(5, (byte)ReplayPhaseInvalidationReason.PhaseMismatch);
        Assert.Equal(6, (byte)ReplayPhaseInvalidationReason.InactivePhase);
        Assert.Equal(7, (byte)ReplayPhaseInvalidationReason.DomainBoundary);
        Assert.Equal(8, (byte)ReplayPhaseInvalidationReason.ClassCapacityMismatch);
        Assert.Equal(9, (byte)ReplayPhaseInvalidationReason.ClassTemplateExpired);
        Assert.Equal(10, (byte)ReplayPhaseInvalidationReason.SerializingEvent);
        Assert.Equal(11, (byte)ReplayPhaseInvalidationReason.MemoryFootprintOverlap);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;
    private const int HeaderRangeEncodingOffset = 46;

    private static DmaStreamComputeDescriptor ParseValid(byte[] descriptorBytes)
    {
        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.True(result.IsValid, result.Message);
        return result.RequireDescriptorForAdmission();
    }

    private static DmaStreamComputeOwnerGuardDecision CreateGuardDecision(byte[] descriptorBytes)
    {
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
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
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D,
        DmaStreamComputeMemoryRange[]? readRanges = null,
        DmaStreamComputeMemoryRange[]? writeRanges = null)
    {
        readRanges ??= operation switch
        {
            DmaStreamComputeOperationKind.Copy => new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16)
            },
            DmaStreamComputeOperationKind.Fma => new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16),
                new DmaStreamComputeMemoryRange(0x3000, 16)
            },
            _ => new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16)
            }
        };
        writeRanges ??= new[]
        {
            new DmaStreamComputeMemoryRange(0x9000, 16)
        };

        ushort sourceRangeCount = checked((ushort)readRanges.Length);
        ushort destinationRangeCount = checked((ushort)writeRanges.Length);
        int sourceRangeTableOffset = HeaderSize;
        int destinationRangeTableOffset = HeaderSize + (sourceRangeCount * RangeEntrySize);
        uint totalSize = (uint)(HeaderSize + ((sourceRangeCount + destinationRangeCount) * RangeEntrySize));
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Magic);
        WriteUInt16(bytes, 4, DmaStreamComputeDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, 6, HeaderSize);
        WriteUInt32(bytes, 8, totalSize);
        WriteUInt64(bytes, 24, IdentityHash);
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, 40, (ushort)operation);
        WriteUInt16(bytes, 42, (ushort)elementType);
        WriteUInt16(bytes, 44, (ushort)shape);
        WriteUInt16(bytes, HeaderRangeEncodingOffset, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt16(bytes, 60, 1);
        WriteUInt32(bytes, 64, 77);
        WriteUInt32(bytes, 68, 1);
        WriteUInt32(bytes, 72, 2);
        WriteUInt32(bytes, 76, DmaStreamComputeDescriptor.CanonicalLane6DeviceId);
        WriteUInt64(bytes, 80, 0xD0A11);
        WriteUInt32(bytes, 96, (uint)sourceRangeTableOffset);
        WriteUInt32(bytes, 100, (uint)destinationRangeTableOffset);

        for (int i = 0; i < readRanges.Length; i++)
        {
            WriteRange(bytes, sourceRangeTableOffset + (i * RangeEntrySize), readRanges[i]);
        }

        for (int i = 0; i < writeRanges.Length; i++)
        {
            WriteRange(bytes, destinationRangeTableOffset + (i * RangeEntrySize), writeRanges[i]);
        }

        return bytes;
    }

    private static void WriteRange(byte[] bytes, int offset, DmaStreamComputeMemoryRange range)
    {
        WriteUInt64(bytes, offset, range.Address);
        WriteUInt64(bytes, offset + 8, range.Length);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
