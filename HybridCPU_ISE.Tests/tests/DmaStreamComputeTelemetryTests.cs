using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeTelemetryTests
{
    [Fact]
    public void DmaStreamComputeTelemetry_ParseRuntimeStagingCommitAndExport_AreObservationOnly()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x9000, 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004);

        var telemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = BuildDescriptor(DmaStreamComputeOperationKind.Add);
        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes),
                telemetry: telemetry);

        Assert.True(validation.IsValid, validation.Message);
        DmaStreamComputeDescriptor descriptor = validation.RequireDescriptorForAdmission();

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor, telemetry: telemetry);
        Assert.True(execution.IsCommitPending);
        Assert.Equal(new uint[] { 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004 }, ReadUInt32Array(0x9000, 4));

        DmaStreamComputeCommitResult commit =
            execution.Token.Commit(Processor.MainMemory, descriptor.OwnerGuardDecision);
        Assert.True(commit.Succeeded);

        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.DescriptorParseAttempts);
        Assert.Equal(1, snapshot.DescriptorAccepted);
        Assert.Equal(1, snapshot.ComputeAccepted);
        Assert.Equal(1, snapshot.ComputeActive);
        Assert.Equal(1, snapshot.ComputeStaged);
        Assert.Equal(1, snapshot.ComputeCommitted);
        Assert.Equal(0, snapshot.ComputeFaulted);
        Assert.Equal(32UL, snapshot.BytesRead);
        Assert.Equal(16UL, snapshot.BytesStaged);
        Assert.Equal(4UL, snapshot.ElementOperations);
        Assert.Equal(4UL, snapshot.AddOperations);
        Assert.Equal(0, execution.Telemetry.AluLaneOccupancyDelta);
        Assert.Equal(0, execution.Telemetry.DirectDestinationWriteCount);

        TypedSlotTelemetryProfile profile =
            TelemetryExporter.BuildProfile(new MicroOpScheduler(), "dma-stream-compute", snapshot);
        string json = TelemetryExporter.SerializeToJson(profile);
        TypedSlotTelemetryProfile? roundTripped = TelemetryExporter.DeserializeFromJson(json);

        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.DmaStreamComputeTelemetry);
        Assert.Equal(1, roundTripped.DmaStreamComputeTelemetry!.ComputeCommitted);
        Assert.Equal(4UL, roundTripped.DmaStreamComputeTelemetry.AddOperations);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_OwnerDomainReject_IsCountedBeforeDescriptorAcceptance()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = BuildDescriptor(DmaStreamComputeOperationKind.Copy);
        DmaStreamComputeStructuralReadResult structural =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        DmaStreamComputeOwnerBinding ownerBinding = structural.RequireOwnerBindingForGuard();
        var staleContext = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerDomainTag: 0x4000,
            activeDomainCertificate: 0x4000,
            deviceId: ownerBinding.DeviceId);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(ownerBinding, staleContext);

        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision, telemetry: telemetry);

        Assert.False(validation.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, validation.Fault);
        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.DescriptorParseAttempts);
        Assert.Equal(0, snapshot.DescriptorAccepted);
        Assert.Equal(1, snapshot.DescriptorRejected);
        Assert.Equal(1, snapshot.OwnerDomainFaults);
        Assert.Equal(1, snapshot.ComputeRejected);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_ReplayEnvelopeReject_IsEvidenceBoundedOnly()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeDescriptor baselineDescriptor = ParseValid(BuildDescriptor(DmaStreamComputeOperationKind.Add));
        DmaStreamComputeDescriptor driftDescriptor = ParseValid(BuildDescriptor(
            DmaStreamComputeOperationKind.Add,
            certificateInputHash: 0xBAD_CE17UL));
        DmaStreamComputeReplayEvidence baseline =
            DmaStreamComputeReplayEvidence.CreateForDescriptor(baselineDescriptor);
        DmaStreamComputeReplayEvidence drift =
            DmaStreamComputeReplayEvidence.CreateForDescriptor(driftDescriptor);

        DmaStreamComputeReplayEvidenceComparison comparison =
            DmaStreamComputeReplayEvidenceComparer.Compare(baseline, drift, telemetry);

        Assert.False(comparison.CanReuse);
        Assert.Equal(ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch, comparison.InvalidationReason);
        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.ReplayEnvelopeRejects);
        Assert.Equal(ReplayPhaseInvalidationReason.DmaStreamComputeCertificateInputMismatch, snapshot.LastReplayInvalidationReason);
        Assert.Equal("CertificateInputHash", snapshot.LastReplayMismatchField);
        Assert.Equal(0, snapshot.ComputeCommitted);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_TokenLifecycleEvidence_CannotAuthorizeCommit()
    {
        InitializeMainMemory(0x10000);
        WriteMemory(0x9000, Fill(0x11, 16));
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0xD5C0, telemetry);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, Fill(0xA5, 16));
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        DmaStreamComputeTokenLifecycleEvidence lifecycle = token.ExportLifecycleEvidence();
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, lifecycle.State);
        Assert.NotEqual(0UL, lifecycle.EvidenceHash);

        DmaStreamComputeOwnerGuardDecision staleGuard =
            DmaStreamComputeOwnerGuardDecision.Allow(
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext with { OwnerDomainTag = 0x4000 },
                "telemetry evidence is not commit authority");

        DmaStreamComputeCommitResult result = token.Commit(Processor.MainMemory, staleGuard);

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DomainViolation, result.Fault!.FaultKind);
        Assert.Equal(Fill(0x11, 16), ReadMemory(0x9000, 16));

        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.ComputeFaulted);
        Assert.Equal(0, snapshot.ComputeCommitted);
        Assert.Equal(1, snapshot.TokenFaults);
        Assert.Equal(1, snapshot.OwnerDomainFaults);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DomainViolation, snapshot.LastTokenFaultKind);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_DescriptorAndDeviceFaults_AreComputeSpecific()
    {
        var descriptorTelemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeValidationResult decodeReject =
            DmaStreamComputeDescriptorParser.Parse(new byte[8], telemetry: descriptorTelemetry);

        Assert.False(decodeReject.IsValid);
        DmaStreamComputeTelemetrySnapshot descriptorSnapshot = descriptorTelemetry.Snapshot();
        Assert.Equal(1, descriptorSnapshot.DescriptorParseAttempts);
        Assert.Equal(1, descriptorSnapshot.DescriptorRejected);
        Assert.Equal(1, descriptorSnapshot.DescriptorFaults);
        Assert.Equal(1, descriptorSnapshot.ComputeRejected);

        var deviceTelemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = BuildDescriptor(DmaStreamComputeOperationKind.Copy);
        DmaStreamComputeStructuralReadResult structural =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(descriptorBytes);
        DmaStreamComputeOwnerBinding ownerBinding = structural.RequireOwnerBindingForGuard();
        var staleDeviceContext = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId + 1);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(ownerBinding, staleDeviceContext);

        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(descriptorBytes, guardDecision, telemetry: deviceTelemetry);

        Assert.False(validation.IsValid);
        Assert.Equal(DmaStreamComputeValidationFault.OwnerDomainFault, validation.Fault);
        DmaStreamComputeTelemetrySnapshot deviceSnapshot = deviceTelemetry.Snapshot();
        Assert.Equal(1, deviceSnapshot.DescriptorRejected);
        Assert.Equal(1, deviceSnapshot.DeviceFaults);
        Assert.Equal(0, deviceSnapshot.OwnerDomainFaults);
        Assert.Equal(1, deviceSnapshot.ComputeRejected);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_DeviceTokenFault_IsCountedAndDoesNotCommit()
    {
        InitializeMainMemory(0x10000);
        WriteMemory(0x9000, Fill(0x11, 16));
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor, telemetry: telemetry);
        Assert.True(execution.IsCommitPending);

        DmaStreamComputeOwnerGuardDecision staleDeviceGuard =
            DmaStreamComputeOwnerGuardDecision.Allow(
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext with { DeviceId = descriptor.OwnerBinding.DeviceId + 1 },
                "telemetry evidence is not device commit authority");

        DmaStreamComputeCommitResult commit =
            execution.Token.Commit(Processor.MainMemory, staleDeviceGuard);

        Assert.False(commit.Succeeded);
        Assert.True(commit.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DmaDeviceFault, commit.Fault!.FaultKind);
        Assert.Equal(Fill(0x11, 16), ReadMemory(0x9000, 16));

        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.ComputeFaulted);
        Assert.Equal(1, snapshot.TokenFaults);
        Assert.Equal(1, snapshot.DeviceFaults);
        Assert.Equal(0, snapshot.ComputeCommitted);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DmaDeviceFault, snapshot.LastTokenFaultKind);
    }

    [Fact]
    public void DmaStreamComputeTelemetry_CanceledToken_IsExplicitAndDiscardable()
    {
        InitializeMainMemory(0x10000);
        WriteMemory(0x9000, Fill(0x11, 16));
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(DmaStreamComputeOperationKind.Copy));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 0xCA11, telemetry);

        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, Fill(0xA5, 16));
        token.Cancel(DmaStreamComputeTokenCancelReason.ReplayDiscard);
        DmaStreamComputeCommitResult commit = token.Commit(Processor.MainMemory, descriptor.OwnerGuardDecision);

        Assert.False(commit.Succeeded);
        Assert.True(commit.IsCanceled);
        Assert.Equal(Fill(0x11, 16), ReadMemory(0x9000, 16));

        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.ComputeAccepted);
        Assert.Equal(1, snapshot.ComputeActive);
        Assert.Equal(1, snapshot.ComputeStaged);
        Assert.Equal(1, snapshot.ComputeCanceled);
        Assert.Equal(0, snapshot.ComputeCommitted);
        Assert.Equal(0, snapshot.ComputeFaulted);
    }

    [Theory]
    [InlineData(DmaStreamComputeOperationKind.Copy)]
    [InlineData(DmaStreamComputeOperationKind.Add)]
    [InlineData(DmaStreamComputeOperationKind.Mul)]
    [InlineData(DmaStreamComputeOperationKind.Fma)]
    [InlineData(DmaStreamComputeOperationKind.Reduce)]
    public void DmaStreamComputeTelemetry_OperationKindCounters_AreExplicit(
        DmaStreamComputeOperationKind operation)
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x3000, 100, 200, 300, 400);
        WriteMemory(0x9000, Fill(0x11, 16));
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(operation));

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor, telemetry: telemetry);

        Assert.True(execution.IsCommitPending);
        DmaStreamComputeTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(4UL, snapshot.ElementOperations);
        Assert.Equal(operation == DmaStreamComputeOperationKind.Copy ? 4UL : 0UL, snapshot.CopyOperations);
        Assert.Equal(operation == DmaStreamComputeOperationKind.Add ? 4UL : 0UL, snapshot.AddOperations);
        Assert.Equal(operation == DmaStreamComputeOperationKind.Mul ? 4UL : 0UL, snapshot.MulOperations);
        Assert.Equal(operation == DmaStreamComputeOperationKind.Fma ? 4UL : 0UL, snapshot.FmaOperations);
        Assert.Equal(operation == DmaStreamComputeOperationKind.Reduce ? 4UL : 0UL, snapshot.ReduceOperations);
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;

    internal static DmaStreamComputeDescriptor ParseValid(byte[] descriptorBytes)
    {
        DmaStreamComputeValidationResult result =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes));

        Assert.True(result.IsValid, result.Message);
        return result.RequireDescriptorForAdmission();
    }

    internal static DmaStreamComputeOwnerGuardDecision CreateGuardDecision(byte[] descriptorBytes)
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
        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(ownerBinding, context);
    }

    internal static byte[] BuildDescriptor(
        DmaStreamComputeOperationKind operation,
        ulong certificateInputHash = 0xC011EC7EUL)
    {
        DmaStreamComputeMemoryRange[] readRanges = operation switch
        {
            DmaStreamComputeOperationKind.Copy => new[] { new DmaStreamComputeMemoryRange(0x1000, 16) },
            DmaStreamComputeOperationKind.Reduce => new[] { new DmaStreamComputeMemoryRange(0x1000, 16) },
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
        DmaStreamComputeMemoryRange[] writeRanges =
        {
            new(0x9000, operation == DmaStreamComputeOperationKind.Reduce ? 4UL : 16UL)
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
        WriteUInt64(bytes, 32, certificateInputHash);
        WriteUInt16(bytes, 40, (ushort)operation);
        WriteUInt16(bytes, 42, (ushort)DmaStreamComputeElementType.UInt32);
        WriteUInt16(bytes, 44, (ushort)DmaStreamComputeShapeKind.Contiguous1D);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
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

    internal static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    internal static void WriteUInt32Array(ulong address, params uint[] values)
    {
        byte[] bytes = new byte[checked(values.Length * sizeof(uint))];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)), values[i]);
        }

        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));
    }

    internal static uint[] ReadUInt32Array(ulong address, int count)
    {
        byte[] bytes = new byte[checked(count * sizeof(uint))];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
        uint[] values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)));
        }

        return values;
    }

    internal static byte[] Fill(byte value, int count)
    {
        byte[] bytes = new byte[count];
        Array.Fill(bytes, value);
        return bytes;
    }

    internal static void WriteMemory(ulong address, byte[] bytes) =>
        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));

    internal static byte[] ReadMemory(ulong address, int length)
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

public sealed class DmaStreamComputeQuotaTests
{
    [Fact]
    public void DmaStreamComputeQuota_ValidationFaultValuesRemainAppendOnly()
    {
        Assert.Equal(14, (int)DmaStreamComputeValidationFault.QuotaAdmissionReject);
        Assert.Equal(15, (int)DmaStreamComputeValidationFault.ExecutionDisabled);
        Assert.Equal(16, (int)DmaStreamComputeValidationFault.BackpressureAdmissionReject);
        Assert.Equal(17, (int)DmaStreamComputeValidationFault.TokenCapAdmissionReject);
        Assert.Equal(5, (int)DmaStreamComputePressureRejectKind.OutstandingTokenCap);
    }

    [Fact]
    public void DmaStreamComputeQuota_OutstandingTokenCapRejectsBeforeTokenCreation()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeValidationResult validation = DmaStreamComputeDescriptorParser.Parse(
            DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy),
            DmaStreamComputeTelemetryTests.CreateGuardDecision(
                DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy)));
        var policy = new DmaStreamComputePressurePolicy(
            requiredDmaCredits: 1,
            requiredSrfCredits: 1,
            requiredMemoryCredits: 1,
            outstandingTokenCap: 1);
        var snapshot = new DmaStreamComputePressureSnapshot(
            lane6Available: true,
            dmaCreditsAvailable: 1,
            srfCreditsAvailable: 1,
            memorySubsystemCreditsAvailable: 1,
            outstandingTokens: 1);

        DmaStreamComputeTokenAdmissionResult admission =
            DmaStreamComputeAdmissionController.TryAdmit(validation, tokenId: 9, policy, snapshot, telemetry);

        Assert.False(admission.IsAccepted);
        Assert.True(admission.IsTelemetryOnlyReject);
        Assert.Null(admission.Token);
        Assert.False(admission.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeValidationFault.TokenCapAdmissionReject, admission.ValidationFault);

        DmaStreamComputeTelemetrySnapshot counters = telemetry.Snapshot();
        Assert.Equal(1, counters.ComputeRejected);
        Assert.Equal(1, counters.QuotaRejects);
        Assert.Equal(1, counters.OutstandingTokenCapRejects);
        Assert.Equal(DmaStreamComputePressureRejectKind.OutstandingTokenCap, counters.LastPressureRejectKind);
        Assert.Equal((ushort)1, counters.LastOutstandingTokens);
        Assert.Equal((ushort)1, counters.LastOutstandingTokenCap);
    }

    [Fact]
    public void DmaStreamComputeQuota_ExistingTelemetryRejectFaultStaysNonArchitectural()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        DmaStreamComputeValidationResult validation =
            DmaStreamComputeValidationResult.Fail(
                DmaStreamComputeValidationFault.QuotaAdmissionReject,
                "lane6 compute quota exhausted");

        DmaStreamComputeTokenAdmissionResult admission =
            DmaStreamComputeToken.TryAdmit(validation, tokenId: 10, telemetry);

        Assert.False(admission.IsAccepted);
        Assert.True(admission.IsTelemetryOnlyReject);
        Assert.Null(admission.Token);
        Assert.False(admission.RequiresRetireExceptionPublication);

        DmaStreamComputeTelemetrySnapshot counters = telemetry.Snapshot();
        Assert.Equal(1, counters.ComputeRejected);
        Assert.Equal(1, counters.QuotaRejects);
        Assert.Equal(DmaStreamComputeValidationFault.QuotaAdmissionReject, counters.LastValidationFault);
    }
}

public sealed class DmaStreamComputeBackpressureTests
{
    [Theory]
    [InlineData(false, 1, 1, 1, DmaStreamComputePressureRejectKind.Lane6Unavailable)]
    [InlineData(true, 0, 1, 1, DmaStreamComputePressureRejectKind.DmaCredits)]
    [InlineData(true, 1, 0, 1, DmaStreamComputePressureRejectKind.SrfCredits)]
    [InlineData(true, 1, 1, 0, DmaStreamComputePressureRejectKind.MemorySubsystemPressure)]
    public void DmaStreamComputeBackpressure_RejectsExplicitPressureBeforeTokenCreation(
        bool lane6Available,
        byte dmaCredits,
        ushort srfCredits,
        byte memoryCredits,
        DmaStreamComputePressureRejectKind expectedReject)
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy);
        DmaStreamComputeValidationResult validation = DmaStreamComputeDescriptorParser.Parse(
            descriptorBytes,
            DmaStreamComputeTelemetryTests.CreateGuardDecision(descriptorBytes));
        var policy = new DmaStreamComputePressurePolicy(
            requiredDmaCredits: 1,
            requiredSrfCredits: 1,
            requiredMemoryCredits: 1,
            outstandingTokenCap: 4);
        var snapshot = new DmaStreamComputePressureSnapshot(
            lane6Available,
            dmaCredits,
            srfCredits,
            memoryCredits,
            outstandingTokens: 0);

        DmaStreamComputeTokenAdmissionResult admission =
            DmaStreamComputeAdmissionController.TryAdmit(validation, tokenId: 11, policy, snapshot, telemetry);

        Assert.False(admission.IsAccepted);
        Assert.True(admission.IsTelemetryOnlyReject);
        Assert.Null(admission.Token);
        Assert.Equal(DmaStreamComputeValidationFault.BackpressureAdmissionReject, admission.ValidationFault);

        DmaStreamComputeTelemetrySnapshot counters = telemetry.Snapshot();
        Assert.Equal(1, counters.ComputeRejected);
        Assert.Equal(1, counters.BackpressureRejects);
        Assert.Equal(expectedReject, counters.LastPressureRejectKind);
        Assert.Equal(lane6Available, counters.LastLane6Available);
        Assert.Equal(dmaCredits, counters.LastDmaCreditsAvailable);
        Assert.Equal(srfCredits, counters.LastSrfCreditsAvailable);
        Assert.Equal(memoryCredits, counters.LastMemorySubsystemCreditsAvailable);
    }

    [Fact]
    public void DmaStreamComputeBackpressure_PermissivePressureAdmitsTokenWithoutUsingTelemetryAsAuthority()
    {
        var telemetry = new DmaStreamComputeTelemetryCounters();
        byte[] descriptorBytes = DmaStreamComputeTelemetryTests.BuildDescriptor(DmaStreamComputeOperationKind.Copy);
        DmaStreamComputeValidationResult validation = DmaStreamComputeDescriptorParser.Parse(
            descriptorBytes,
            DmaStreamComputeTelemetryTests.CreateGuardDecision(descriptorBytes));
        DmaStreamComputePressurePolicy policy = DmaStreamComputePressurePolicy.Default;

        DmaStreamComputeTokenAdmissionResult admission =
            DmaStreamComputeAdmissionController.TryAdmit(
                validation,
                tokenId: 12,
                policy,
                DmaStreamComputePressureSnapshot.Permissive(policy),
                telemetry);

        Assert.True(admission.IsAccepted);
        Assert.NotNull(admission.Token);
        Assert.Equal(DmaStreamComputeTokenState.Admitted, admission.Token!.State);

        DmaStreamComputeTelemetrySnapshot counters = telemetry.Snapshot();
        Assert.Equal(1, counters.ComputeAccepted);
        Assert.Equal(0, counters.ComputeRejected);
        Assert.True(counters.LastLane6Available);
    }
}
