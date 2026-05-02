using System;
using System.Buffers.Binary;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeCommitTokenTests
{
    [Fact]
    public void DmaStreamComputeCommitToken_StagedDestinationWrite_RemainsInvisibleUntilRetireCommit()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x11, 16);
        byte[] staged = Fill(0xA5, 16);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, staged);

        Assert.Equal(original, ReadMemory(0x9000, 16));
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        Assert.False(token.HasCommitted);

        var core = new Processor.CPU_Core(0);
        DmaStreamComputeCommitResult result =
            core.TestApplyDmaStreamComputeTokenCommit(token, descriptor.OwnerGuardDecision);

        Assert.True(result.Succeeded);
        Assert.Equal(DmaStreamComputeTokenState.Committed, token.State);
        Assert.True(token.HasCommitted);
        Assert.Equal(staged, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_SplitStagedDestinationWrite_CommitsOnlyWhenCoverageIsExact()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x13, 16);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 2);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, Fill(0xAB, 8));
        token.StageDestinationWrite(0x9008, Fill(0xCD, 8));

        DmaStreamComputeCommitResult pending = token.MarkComputeComplete();
        Assert.False(pending.RequiresRetireExceptionPublication);
        Assert.Equal(original, ReadMemory(0x9000, 16));

        var core = new Processor.CPU_Core(0);
        DmaStreamComputeCommitResult result =
            core.TestApplyDmaStreamComputeTokenCommit(token, descriptor.OwnerGuardDecision);

        Assert.True(result.Succeeded);
        Assert.Equal(
            new byte[]
            {
                0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xAB, 0xAB,
                0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD
            },
            ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_CancelOnFlush_DiscardsStagedWritesWithoutArchitecturalPublication()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x22, 16);
        byte[] staged = Fill(0x5A, 16);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, staged);

        token.Cancel(DmaStreamComputeTokenCancelReason.Flush);
        var core = new Processor.CPU_Core(0);
        DmaStreamComputeCommitResult result =
            core.TestApplyDmaStreamComputeTokenCommit(token, descriptor.OwnerGuardDecision);

        Assert.False(result.Succeeded);
        Assert.True(result.IsCanceled);
        Assert.False(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Canceled, token.State);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_PartialStagedWrite_FaultsAllOrNoneWithoutVisibleSuccess()
    {
        InitializeMainMemory(0x10000);
        byte[] original = Fill(0x33, 16);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 3);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x9000, Fill(0x77, 8));

        DmaStreamComputeCommitResult result = token.MarkComputeComplete();

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.PartialCompletionFault, result.Fault!.FaultKind);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(original, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_OutOfRangeCommitFault_PublishesPreciseRetireExceptionAndNoPartialWrite()
    {
        InitializeMainMemory(0x2000);
        byte[] original = Fill(0x44, 4);
        WriteMemory(0x100, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x100, 4),
                new DmaStreamComputeMemoryRange(0x1FFC, 8)
            }));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 4);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(0x100, Fill(0x99, 4));
        token.StageDestinationWrite(0x1FFC, Fill(0xAA, 8));
        Assert.False(token.MarkComputeComplete().RequiresRetireExceptionPublication);

        var core = new Processor.CPU_Core(0);
        PageFaultException ex = Assert.Throws<PageFaultException>(
            () => core.TestApplyDmaStreamComputeTokenCommit(token, descriptor.OwnerGuardDecision));

        Assert.True(ex.IsWrite);
        Assert.Equal(0x1FFCUL, ex.FaultAddress);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.MemoryFault, token.LastFault!.FaultKind);
        Assert.Equal(0, token.StagedWriteCount);
        Assert.Equal(original, ReadMemory(0x100, 4));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_OwnerDomainGuard_IsRequiredBeforeTokenCreationAndCommit()
    {
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        DmaStreamComputeDescriptor noFootprintDescriptor = descriptor with
        {
            WriteMemoryRanges = Array.Empty<DmaStreamComputeMemoryRange>(),
            NormalizedWriteMemoryRanges = Array.Empty<DmaStreamComputeMemoryRange>()
        };

        Assert.Throws<InvalidOperationException>(
            () => new DmaStreamComputeToken(noFootprintDescriptor, tokenId: 5));

        InitializeMainMemory(0x10000);
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, Fill(0xCC, 16));
        DmaStreamComputeOwnerGuardDecision staleReplayLikeDecision =
            DmaStreamComputeOwnerGuardDecision.Allow(
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext with { OwnerDomainTag = 0x4000 },
                "stale replay/certificate identity is not commit authority");

        var core = new Processor.CPU_Core(0);
        DomainFaultException ex = Assert.Throws<DomainFaultException>(
            () => core.TestApplyDmaStreamComputeTokenCommit(token, staleReplayLikeDecision));

        Assert.Equal((int)descriptor.OwnerBinding.OwnerVirtualThreadId, ex.VirtualThreadId);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DomainViolation, token.LastFault!.FaultKind);
    }

    [Fact]
    public void DmaStreamComputeCommitToken_AdmissionReject_RemainsTelemetryNotArchitecturalFault()
    {
        DmaStreamComputeValidationResult validationReject =
            DmaStreamComputeValidationResult.Fail(
                DmaStreamComputeValidationFault.QuotaAdmissionReject,
                "lane6 token quota exhausted");

        DmaStreamComputeTokenAdmissionResult result =
            DmaStreamComputeToken.TryAdmit(validationReject, tokenId: 6);

        Assert.False(result.IsAccepted);
        Assert.True(result.IsTelemetryOnlyReject);
        Assert.False(result.RequiresRetireExceptionPublication);
        Assert.Null(result.Token);
        Assert.Null(result.Fault);
    }

    [Fact]
    public void DmaStreamComputeCommitToken_DeviceAndDescriptorFaults_PublishExplicitRetireFaults()
    {
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        var token = new DmaStreamComputeToken(descriptor, tokenId: 7);
        token.MarkIssued();

        DmaStreamComputeCommitResult descriptorFault = token.PublishFault(
            DmaStreamComputeTokenFaultKind.DescriptorDecodeFault,
            "descriptor payload changed after issue",
            faultAddress: descriptor.DescriptorReference.DescriptorAddress,
            isWrite: false);

        Assert.True(descriptorFault.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, token.State);
        Assert.IsType<InvalidOperationException>(descriptorFault.CreateRetireException());

        var deviceFaultToken = new DmaStreamComputeToken(descriptor, tokenId: 8);
        deviceFaultToken.MarkIssued();
        DmaStreamComputeCommitResult deviceFault = deviceFaultToken.PublishFault(
            DmaStreamComputeTokenFaultKind.DmaDeviceFault,
            "DMA device reported partial completion",
            faultAddress: 0x9000,
            isWrite: true);

        Assert.True(deviceFault.RequiresRetireExceptionPublication);
        Assert.IsType<PageFaultException>(deviceFault.CreateRetireException());
    }

    [Fact]
    public void DmaStreamComputeCommitToken_DirectDmaBurst_CannotRetireArchitecturalSuccessBeforeTokenCommit()
    {
        InitializeMainMemory(0x10000);
        byte[] source = Fill(0x12, 16);
        byte[] staged = Fill(0xE1, 16);
        byte[] original = Fill(0x6C, 16);
        WriteMemory(0x1000, source);
        WriteMemory(0x9000, original);

        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        DmaStreamComputeToken token = CreateCommitPendingToken(descriptor, staged);

        Processor proc = default;
        var dma = new DMAController(ref proc);
        var transfer = new DMAController.TransferDescriptor
        {
            SourceAddress = 0x1000,
            DestAddress = 0x9000,
            TransferSize = 16,
            SourceStride = 0,
            DestStride = 0,
            ElementSize = 1,
            UseIOMMU = false,
            ChannelID = 0,
            Priority = 1
        };

        Assert.True(dma.ConfigureTransfer(transfer));
        Assert.True(dma.StartTransfer(0));
        while (dma.GetChannelState(0) == DMAController.ChannelState.Active)
        {
            dma.ExecuteCycle();
        }

        Assert.Equal(DMAController.ChannelState.Completed, dma.GetChannelState(0));
        Assert.Equal(source, ReadMemory(0x9000, 16));
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        Assert.False(token.HasCommitted);

        var core = new Processor.CPU_Core(0);
        core.TestApplyDmaStreamComputeTokenCommit(token, descriptor.OwnerGuardDecision);

        Assert.Equal(DmaStreamComputeTokenState.Committed, token.State);
        Assert.Equal(staged, ReadMemory(0x9000, 16));
    }

    [Fact]
    public void DmaStreamComputeCommitToken_MicroOpExecution_RemainsFailClosed()
    {
        DmaStreamComputeDescriptor descriptor = ParseValid(BuildDescriptor(
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) }));
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));

        Assert.Contains("execution is disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DmaStreamComputeCommitToken_DoesNotExposeDmaChannelControlSurface()
    {
        Type tokenType = typeof(DmaStreamComputeToken);

        Assert.Null(tokenType.GetMethod("PauseTransfer"));
        Assert.Null(tokenType.GetMethod("ResumeTransfer"));
        Assert.Null(tokenType.GetMethod("ResetChannel"));
        Assert.Null(tokenType.GetMethod("Fence"));
        Assert.NotNull(typeof(DMAController).GetMethod(nameof(DMAController.PauseTransfer)));
        Assert.NotNull(typeof(DMAController).GetMethod(nameof(DMAController.ResumeTransfer)));
        Assert.NotNull(typeof(DMAController).GetMethod(nameof(DMAController.CancelTransfer)));
    }

    private const ulong IdentityHash = 0xA11CE5EEDUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;

    private static DmaStreamComputeToken CreateCommitPendingToken(
        DmaStreamComputeDescriptor descriptor,
        byte[] staged)
    {
        var token = new DmaStreamComputeToken(descriptor, descriptor.DescriptorIdentityHash);
        token.MarkIssued();
        token.MarkReadsComplete();
        token.StageDestinationWrite(descriptor.NormalizedWriteMemoryRanges[0].Address, staged);
        DmaStreamComputeCommitResult result = token.MarkComputeComplete();

        Assert.False(result.Succeeded);
        Assert.False(result.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, token.State);
        return token;
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
        DmaStreamComputeOperationKind operation = DmaStreamComputeOperationKind.Copy,
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
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, 40, (ushort)operation);
        WriteUInt16(bytes, 42, (ushort)elementType);
        WriteUInt16(bytes, 44, (ushort)shape);
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
