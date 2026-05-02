using System;
using System.Buffers.Binary;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeReplayEvidenceTests
{
    [Fact]
    public void DmaStreamComputeReplayEvidence_ExactEnvelopeMatch_IsRequiredForReuseHit()
    {
        DmaStreamComputeReplayEvidence baseline = CreateEvidence();
        DmaStreamComputeReplayEvidence equivalent = CreateEvidence();

        DmaStreamComputeReplayEvidenceComparison hit =
            DmaStreamComputeReplayEvidenceComparer.Compare(baseline, equivalent);

        Assert.True(hit.CanReuse, hit.MismatchField);
        Assert.Equal(ReplayPhaseInvalidationReason.None, hit.InvalidationReason);

        AssertInvalidates(
            baseline,
            CreateEvidence(operation: DmaStreamComputeOperationKind.Mul),
            ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorMismatch,
            "Operation");
        AssertInvalidates(
            baseline,
            CreateEvidence(elementType: DmaStreamComputeElementType.UInt16),
            ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorMismatch,
            "ElementType");
        AssertInvalidates(
            baseline,
            CreateEvidence(certificateInputHash: 0xBAD_CE17UL),
            ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch,
            "CertificateInputHash");
        AssertInvalidates(
            baseline,
            CreateEvidence(ownerDomainTag: 0xF00DUL),
            ReplayPhaseInvalidationReason.DmaStreamComputeOwnerDomainMismatch,
            "OwnerDomain");
        AssertInvalidates(
            baseline,
            CreateEvidence(writeRanges: new[] { new DmaStreamComputeMemoryRange(0xA000, 16) }),
            ReplayPhaseInvalidationReason.DmaStreamComputeFootprintMismatch,
            "Footprint");
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_CarrierPayloadLossOrCustomRegistryCarrier_FailsClosed()
    {
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor());
        DmaStreamComputeReplayEvidence baseline =
            DmaStreamComputeReplayEvidence.CreateForDescriptor(descriptor);

        DmaStreamComputeReplayEvidence payloadLost = baseline with
        {
            CarrierEvidence =
                DmaStreamComputeCarrierEvidence.MissingDescriptorPayload(
                    descriptor.DescriptorReference)
        };
        DmaStreamComputeReplayEvidence customRegistry = baseline with
        {
            CarrierEvidence =
                DmaStreamComputeCarrierEvidence.CustomRegistryCarrier(
                    descriptor.DescriptorReference,
                    customOpcode: 0xC000)
        };

        AssertInvalidates(
            baseline,
            payloadLost,
            ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorPayloadLost,
            "CarrierPayload");
        AssertInvalidates(
            baseline,
            customRegistry,
            ReplayPhaseInvalidationReason.DmaStreamComputeCarrierMismatch,
            "Carrier");
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_CertificateIdentityCarriesFullEnvelopeNotOnlyDescriptorAndFootprint()
    {
        DmaStreamComputeDescriptor baselineDescriptor = ParseValid(BuildDescriptor());
        DmaStreamComputeDescriptor ownerDriftDescriptor = ParseValid(BuildDescriptor(ownerContextId: 99));

        var baselineCertificate = BundleResourceCertificate4Way.Empty;
        baselineCertificate.AddOperation(new DmaStreamComputeMicroOp(baselineDescriptor));

        var ownerDriftCertificate = BundleResourceCertificate4Way.Empty;
        ownerDriftCertificate.AddOperation(new DmaStreamComputeMicroOp(ownerDriftDescriptor));

        Assert.Equal(
            baselineDescriptor.DescriptorIdentityHash,
            ownerDriftDescriptor.DescriptorIdentityHash);
        Assert.Equal(
            baselineDescriptor.NormalizedFootprintHash,
            ownerDriftDescriptor.NormalizedFootprintHash);
        Assert.NotEqual(
            baselineCertificate.StructuralIdentity,
            ownerDriftCertificate.StructuralIdentity);
        Assert.NotEqual(
            baselineCertificate.DmaStreamComputeReplayEnvelopeHash,
            ownerDriftCertificate.DmaStreamComputeReplayEnvelopeHash);
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_MemoryFootprintOverlap_UsesExistingAppendOnlyInvalidationReason()
    {
        DmaStreamComputeMicroOp writer = new(ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 64) })));
        DmaStreamComputeMicroOp replayEvidence = new(ParseValid(BuildDescriptor(
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x9020, 16),
                new DmaStreamComputeMemoryRange(0x4000, 16)
            },
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0xA000, 64) })));

        bool invalidates =
            SafetyVerifier.TryClassifyMemoryFootprintInvalidation(
                writer,
                replayEvidence,
                out ReplayPhaseInvalidationReason reason);

        Assert.True(invalidates);
        Assert.Equal(ReplayPhaseInvalidationReason.MemoryFootprintOverlap, reason);
        Assert.Equal(11, (byte)ReplayPhaseInvalidationReason.MemoryFootprintOverlap);
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_Lane6ReplayChoice_RequiresMatchingEvidenceEnvelope()
    {
        DmaStreamComputeReplayEvidence baseline = CreateEvidence();
        DmaStreamComputeReplayEvidence live = CreateEvidence();

        bool selected =
            DeterministicLaneChooser.TrySelectDmaStreamLaneWithReplayEvidence(
                freeLanes: 0b0100_0000,
                previousLane: 6,
                expected: baseline,
                live: live,
                out int lane,
                out ReplayPhaseInvalidationReason reason);

        Assert.True(selected);
        Assert.Equal(6, lane);
        Assert.Equal(ReplayPhaseInvalidationReason.None, reason);

        bool driftSelected =
            DeterministicLaneChooser.TrySelectDmaStreamLaneWithReplayEvidence(
                freeLanes: 0b0100_0000,
                previousLane: 6,
                expected: baseline,
                live: CreateEvidence(certificateInputHash: 0xBAD_CE17UL),
                out int driftLane,
                out ReplayPhaseInvalidationReason driftReason);

        Assert.False(driftSelected);
        Assert.Equal(-1, driftLane);
        Assert.Equal(
            ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch,
            driftReason);
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_IncompleteEvidence_FailsClosedWithoutLaneFallback()
    {
        bool selected =
            DeterministicLaneChooser.TrySelectDmaStreamLaneWithReplayEvidence(
                freeLanes: 0b0100_0000,
                previousLane: 6,
                expected: default,
                live: CreateEvidence(),
                out int lane,
                out ReplayPhaseInvalidationReason reason);

        Assert.False(selected);
        Assert.Equal(-1, lane);
        Assert.Equal(
            ReplayPhaseInvalidationReason.DmaStreamComputeIncompleteEvidence,
            reason);
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_TokenLifecycleEvidence_DoesNotAuthorizeCommit()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x55, 16);
        byte[] staged = Fill(0xCC, 16);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            operation: DmaStreamComputeOperationKind.Copy,
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, staged);

        DmaStreamComputeTokenLifecycleEvidence tokenEvidence = token.ExportLifecycleEvidence();
        DmaStreamComputeReplayEvidence envelope =
            DmaStreamComputeReplayEvidence.CreateForDescriptor(
                descriptor,
                tokenLifecycleEvidence: tokenEvidence);

        Assert.Equal(DmaStreamComputeTokenState.CommitPending, tokenEvidence.State);
        Assert.Equal(token.TokenId, tokenEvidence.TokenId);
        Assert.NotEqual(0UL, envelope.EnvelopeHash);
        Assert.Equal(original, ReadMemory(0x9000, 16));

        DmaStreamComputeOwnerGuardDecision staleCommitGuard =
            DmaStreamComputeOwnerGuardDecision.Allow(
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext with { OwnerDomainTag = 0x4000 },
                "matching replay evidence is not commit authority");

        var core = new Processor.CPU_Core(0);
        Assert.Throws<DomainFaultException>(
            () => core.TestApplyDmaStreamComputeTokenCommit(token, staleCommitGuard));
        Assert.Equal(original, ReadMemory(0x9000, 16));
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
    }

    [Fact]
    public void DmaStreamComputeReplayEvidence_InvalidationReasonValuesRemainAppendOnlyAfterMemoryFootprintOverlap()
    {
        Assert.Equal(11, (byte)ReplayPhaseInvalidationReason.MemoryFootprintOverlap);
        Assert.Equal(12, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorMismatch);
        Assert.Equal(13, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeDescriptorPayloadLost);
        Assert.Equal(14, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeCarrierMismatch);
        Assert.Equal(15, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeFootprintMismatch);
        Assert.Equal(16, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeOwnerDomainMismatch);
        Assert.Equal(17, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch);
        Assert.Equal(18, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeTokenEvidenceMismatch);
        Assert.Equal(19, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeLanePlacementMismatch);
        Assert.Equal(20, (byte)ReplayPhaseInvalidationReason.DmaStreamComputeIncompleteEvidence);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const ulong DefaultCertificateInputHash = 0xC011EC7EUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;

    private static DmaStreamComputeReplayEvidence CreateEvidence(
        DmaStreamComputeOperationKind operation = DmaStreamComputeOperationKind.Add,
        DmaStreamComputeElementType elementType = DmaStreamComputeElementType.UInt32,
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D,
        ulong certificateInputHash = DefaultCertificateInputHash,
        ulong ownerDomainTag = 0xD0A11UL,
        uint ownerContextId = 77,
        DmaStreamComputeMemoryRange[]? readRanges = null,
        DmaStreamComputeMemoryRange[]? writeRanges = null)
    {
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            operation,
            elementType,
            shape,
            certificateInputHash,
            ownerDomainTag,
            ownerContextId,
            readRanges,
            writeRanges));
        return DmaStreamComputeReplayEvidence.CreateForDescriptor(descriptor);
    }

    private static void AssertInvalidates(
        DmaStreamComputeReplayEvidence baseline,
        DmaStreamComputeReplayEvidence candidate,
        ReplayPhaseInvalidationReason expectedReason,
        string expectedField)
    {
        DmaStreamComputeReplayEvidenceComparison comparison =
            DmaStreamComputeReplayEvidenceComparer.Compare(baseline, candidate);

        Assert.False(comparison.CanReuse);
        Assert.Equal(expectedReason, comparison.InvalidationReason);
        Assert.Contains(expectedField, comparison.MismatchField, StringComparison.OrdinalIgnoreCase);
    }

    private static DmaStreamComputeToken CreateCommitPendingToken(
        DmaStreamComputeDescriptor descriptor,
        byte[] staged)
    {
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0xFEED);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(descriptor.NormalizedWriteMemoryRanges[0].Address, staged);
        DmaStreamComputeCommitResult result = token.MarkComputeComplete();

        Assert.False(result.Succeeded);
        Assert.False(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        return token;
    }

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
        ulong certificateInputHash = DefaultCertificateInputHash,
        ulong ownerDomainTag = 0xD0A11UL,
        uint ownerContextId = 77,
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
        writeRanges ??= new[] { new DmaStreamComputeMemoryRange(0x9000, 16) };

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
        WriteUInt64(bytes, 32, certificateInputHash);
        WriteUInt16(bytes, 40, (ushort)operation);
        WriteUInt16(bytes, 42, (ushort)elementType);
        WriteUInt16(bytes, 44, (ushort)shape);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt16(bytes, 60, 1);
        WriteUInt32(bytes, 64, ownerContextId);
        WriteUInt32(bytes, 68, 1);
        WriteUInt32(bytes, 72, 2);
        WriteUInt32(bytes, 76, DmaStreamComputeDescriptor.CanonicalLane6DeviceId);
        WriteUInt64(bytes, 80, ownerDomainTag);
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

    private static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    private static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static void WriteMemory(ulong address, byte[] bytes) =>
        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));

    private static byte[] ReadMemory(ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
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
