using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class DmaStreamComputeExecutionTests
{
    [Theory]
    [MemberData(nameof(Phase06DirectMicroOpCases))]
    public void DmaStreamComputeMicroOpExecute_Phase06Dsc1OpsStageAndRetire(
        DmaStreamComputeOperationKind operation,
        DmaStreamComputeShapeKind shape,
        DmaStreamComputeMemoryRange[] readRanges,
        DmaStreamComputeMemoryRange[] writeRanges,
        uint[] expected)
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x3000, 7, 8, 9, 10);
        WriteUInt32Array(0x9000, 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            operation,
            shape,
            readRanges,
            writeRanges);
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);

        Assert.True(microOp.Execute(ref core));

        DmaStreamComputeExecutionResult execution = Assert.IsType<DmaStreamComputeExecutionResult>(
            microOp.LastExecutionResult);
        Assert.True(execution.IsStoreTracked);
        Assert.False(execution.TokenHandle.IsDefault);
        Assert.Equal(1, microOp.ActiveTokenCount);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, execution.Token.State);
        Assert.Equal(
            operation == DmaStreamComputeOperationKind.Reduce
                ? new uint[] { 0xDEAD0001 }
                : new uint[] { 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004 },
            ReadUInt32Array(0x9000, expected.Length));
        Assert.True(execution.Telemetry.UsedLane6Backend);
        Assert.Equal(0, execution.Telemetry.AluLaneOccupancyDelta);
        Assert.Equal(0, execution.Telemetry.DirectDestinationWriteCount);
        Assert.True(microOp.LastExecutionReplayEvidence.IsComplete);
        Assert.Equal(
            DmaStreamComputeTokenState.CommitPending,
            microOp.LastExecutionReplayEvidence.TokenLifecycleEvidence.State);
        Assert.True(microOp.LastExecutionReplayEvidence.LanePlacementEvidence.IsComplete);

        microOp.Commit(ref core);

        Assert.True(microOp.LastRetireCommitResult!.Succeeded);
        Assert.Equal(DmaStreamComputeTokenState.Committed, execution.Token.State);
        Assert.Equal(expected, ReadUInt32Array(0x9000, expected.Length));
    }

    [Fact]
    public void DmaStreamComputeMicroOpExecute_UnsupportedDescriptorFailsClosedWithoutFallback()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x9000, 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Add,
            shape: DmaStreamComputeShapeKind.FixedReduce);
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));

        Assert.Contains("Phase 06 DSC1 contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("fails closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, microOp.ActiveTokenCount);
        Assert.Null(microOp.LastExecutionResult);
        Assert.Equal(new uint[] { 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void LegacyVmcsManager_Lane6GuestCompatibilityExecutionFailsClosedBeforeTokenOrWrite()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy);
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);
        core.Csr.Write(CsrAddresses.VmxEnable, 1, PrivilegeLevel.Machine);
        core.WriteVirtualThreadPipelineState(1, PipelineState.GuestExecution);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => microOp.Execute(ref core));

        Assert.Contains("Guest Lane6 compatibility execution is fail-closed", exception.Message);
        Assert.Equal(0, microOp.ActiveTokenCount);
        Assert.Null(microOp.LastExecutionResult);
        Assert.Equal(
            new uint[] { 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004 },
            ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeMicroOpExecute_RevalidatesOwnerDomainGuardAtRetire()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xFACE0001, 0xFACE0002, 0xFACE0003, 0xFACE0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1000, 16) });
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);

        Assert.True(microOp.Execute(ref core));
        DmaStreamComputeOwnerGuardDecision staleCommitGuard =
            DmaStreamComputeOwnerGuardDecision.Allow(
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision.RuntimeOwnerContext with { OwnerDomainTag = 0x4000 },
                "stale guard must be revalidated before retire publication");

        DomainFaultException ex = Assert.Throws<DomainFaultException>(
            () => core.TestApplyDmaStreamComputeTokenCommit(
                microOp.LastExecutionToken!,
                staleCommitGuard));

        Assert.Equal((int)descriptor.OwnerBinding.OwnerVirtualThreadId, ex.VirtualThreadId);
        Assert.Equal(DmaStreamComputeTokenFaultKind.DomainViolation, microOp.LastExecutionToken!.LastFault!.FaultKind);
        Assert.Equal(new uint[] { 0xFACE0001, 0xFACE0002, 0xFACE0003, 0xFACE0004 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeMicroOpExecute_ReplayDiscardRollsBackStagedBytes()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xABCD0001, 0xABCD0002, 0xABCD0003, 0xABCD0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1000, 16) });
        var microOp = new DmaStreamComputeMicroOp(descriptor);
        var core = new Processor.CPU_Core(0);

        Assert.True(microOp.Execute(ref core));
        DmaStreamComputeReplayEvidence beforeCancel = microOp.LastExecutionReplayEvidence;
        microOp.LastExecutionToken!.Cancel(DmaStreamComputeTokenCancelReason.ReplayDiscard);
        DmaStreamComputeCommitResult canceled =
            core.TestApplyDmaStreamComputeTokenCommit(
                microOp.LastExecutionToken,
                descriptor.OwnerGuardDecision);
        DmaStreamComputeReplayEvidence afterCancel = microOp.ExportReplayEvidence(
            microOp.LastExecutionToken.ExportLifecycleEvidence(),
            DmaStreamComputeLanePlacementEvidence.MaterializedLane6(
                selectedLane: 6,
                freeLaneMask: SlotClassLaneMap.GetLaneMask(SlotClass.DmaStreamClass),
                stableDonorMask: 0,
                replayActive: true));
        DmaStreamComputeReplayEvidenceComparison comparison =
            DmaStreamComputeReplayEvidenceComparer.Compare(beforeCancel, afterCancel);

        Assert.True(canceled.IsCanceled);
        Assert.False(canceled.RequiresRetireExceptionPublication);
        Assert.False(comparison.CanReuse);
        Assert.Equal(
            ReplayPhaseInvalidationReason.DmaStreamComputeTokenEvidenceMismatch,
            comparison.InvalidationReason);
        Assert.Equal(new uint[] { 0xABCD0001, 0xABCD0002, 0xABCD0003, 0xABCD0004 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeExecution_Copy_StagesTransferAndPublishesOnlyAtTokenCommit()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1000, 16) });

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(DmaStreamComputeTokenState.CommitPending, execution.Token.State);
        Assert.Equal(new uint[] { 0xDEAD0001, 0xDEAD0002, 0xDEAD0003, 0xDEAD0004 }, ReadUInt32Array(0x9000, 4));
        Assert.True(execution.Telemetry.UsedLane6Backend);
        Assert.Equal(0, execution.Telemetry.AluLaneOccupancyDelta);
        Assert.Equal(0, execution.Telemetry.DirectDestinationWriteCount);
        Assert.Equal(16UL, execution.Telemetry.BytesRead);
        Assert.Equal(16UL, execution.Telemetry.BytesStaged);

        var core = new Processor.CPU_Core(0);
        DmaStreamComputeCommitResult commit =
            core.TestApplyDmaStreamComputeTokenCommit(execution.Token, descriptor.OwnerGuardDecision);

        Assert.True(commit.Succeeded);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, ReadUInt32Array(0x9000, 4));
    }

    [Theory]
    [InlineData(DmaStreamComputeOperationKind.Add, new uint[] { 11, 22, 33, 44 })]
    [InlineData(DmaStreamComputeOperationKind.Mul, new uint[] { 10, 40, 90, 160 })]
    public void DmaStreamComputeExecution_AddMul_ComputeIntoStagedDestination(
        DmaStreamComputeOperationKind operation,
        uint[] expected)
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x9000, 0xFACE0001, 0xFACE0002, 0xFACE0003, 0xFACE0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            operation,
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16)
            });

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(new uint[] { 0xFACE0001, 0xFACE0002, 0xFACE0003, 0xFACE0004 }, ReadUInt32Array(0x9000, 4));
        Assert.True(execution.Telemetry.ModeledLatencyCycles > 0);
        Assert.Equal(2, execution.Telemetry.ReadBurstCount);

        var core = new Processor.CPU_Core(0);
        core.TestApplyDmaStreamComputeTokenCommit(execution.Token, descriptor.OwnerGuardDecision);

        Assert.Equal(expected, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeExecution_Fma_UsesLane6BackendWithoutAluPlacement()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x2000, 10, 20, 30, 40);
        WriteUInt32Array(0x3000, 7, 8, 9, 10);
        WriteUInt32Array(0x9000, 0, 0, 0, 0);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Fma,
            readRanges: new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16),
                new DmaStreamComputeMemoryRange(0x3000, 16)
            });
        DmaStreamComputeMicroOp microOp = new(descriptor);

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(SlotClass.DmaStreamClass, microOp.Placement.RequiredSlotClass);
        Assert.Equal(MicroOpClass.Dma, microOp.Class);
        Assert.Equal(0, execution.Telemetry.AluLaneOccupancyDelta);

        var core = new Processor.CPU_Core(0);
        core.TestApplyDmaStreamComputeTokenCommit(execution.Token, descriptor.OwnerGuardDecision);

        Assert.Equal(new uint[] { 17, 48, 99, 170 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeExecution_Reduce_StagesSingleElementResult()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xBAD0BAD0);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Reduce,
            shape: DmaStreamComputeShapeKind.FixedReduce,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1000, 16) },
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 4) });

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(new uint[] { 0xBAD0BAD0 }, ReadUInt32Array(0x9000, 1));

        var core = new Processor.CPU_Core(0);
        core.TestApplyDmaStreamComputeTokenCommit(execution.Token, descriptor.OwnerGuardDecision);

        Assert.Equal(new uint[] { 10 }, ReadUInt32Array(0x9000, 1));
    }

    [Fact]
    public void DmaStreamComputeExecution_ExactInPlaceSnapshot_ReadsBeforeStagingAndDoesNotPublishBeforeCommit()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x9000, 1, 2, 3, 4);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            aliasPolicy: DmaStreamComputeAliasPolicy.ExactInPlaceSnapshot);

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.True(execution.IsCommitPending);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, ReadUInt32Array(0x9000, 4));
        Assert.Equal(0, execution.Telemetry.DirectDestinationWriteCount);

        var core = new Processor.CPU_Core(0);
        core.TestApplyDmaStreamComputeTokenCommit(execution.Token, descriptor.OwnerGuardDecision);

        Assert.Equal(new uint[] { 1, 2, 3, 4 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeExecution_UnsupportedOperation_FaultsClosedWithoutDestinationWrite()
    {
        InitializeMainMemory(0x10000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x9000, 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1000, 16) })
            with
            {
                Operation = (DmaStreamComputeOperationKind)0xFFFF
            };

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.False(execution.IsCommitPending);
        Assert.True(execution.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenState.Faulted, execution.Token.State);
        Assert.Equal(DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation, execution.Token.LastFault!.FaultKind);
        Assert.Equal(0, execution.Telemetry.DirectDestinationWriteCount);
        Assert.Equal(new uint[] { 0xCAFE0001, 0xCAFE0002, 0xCAFE0003, 0xCAFE0004 }, ReadUInt32Array(0x9000, 4));
    }

    [Fact]
    public void DmaStreamComputeExecution_SourceReadFault_PublishesTokenFaultWithoutSilentSuccess()
    {
        InitializeMainMemory(0x2000);
        WriteUInt32Array(0x1000, 1, 2, 3, 4);
        WriteUInt32Array(0x1800, 0xABCD0001, 0xABCD0002, 0xABCD0003, 0xABCD0004);

        DmaStreamComputeDescriptor descriptor = CreateDescriptor(
            DmaStreamComputeOperationKind.Copy,
            readRanges: new[] { new DmaStreamComputeMemoryRange(0x1FFC, 16) },
            writeRanges: new[] { new DmaStreamComputeMemoryRange(0x1800, 16) });

        DmaStreamComputeExecutionResult execution =
            DmaStreamComputeRuntime.ExecuteToCommitPending(descriptor);

        Assert.False(execution.IsCommitPending);
        Assert.True(execution.RequiresRetireExceptionPublication);
        Assert.Equal(DmaStreamComputeTokenFaultKind.MemoryFault, execution.Token.LastFault!.FaultKind);
        Assert.Equal(0x1FFCUL, execution.Token.LastFault.FaultAddress);
        Assert.Equal(new uint[] { 0xABCD0001, 0xABCD0002, 0xABCD0003, 0xABCD0004 }, ReadUInt32Array(0x1800, 4));
    }

    private const ulong IdentityHash = 0xF007000000000001UL;

    public static IEnumerable<object[]> Phase06DirectMicroOpCases()
    {
        yield return new object[]
        {
            DmaStreamComputeOperationKind.Copy,
            DmaStreamComputeShapeKind.Contiguous1D,
            new[] { new DmaStreamComputeMemoryRange(0x1000, 16) },
            new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            new uint[] { 1, 2, 3, 4 }
        };
        yield return new object[]
        {
            DmaStreamComputeOperationKind.Add,
            DmaStreamComputeShapeKind.Contiguous1D,
            new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16)
            },
            new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            new uint[] { 11, 22, 33, 44 }
        };
        yield return new object[]
        {
            DmaStreamComputeOperationKind.Mul,
            DmaStreamComputeShapeKind.Contiguous1D,
            new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16)
            },
            new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            new uint[] { 10, 40, 90, 160 }
        };
        yield return new object[]
        {
            DmaStreamComputeOperationKind.Fma,
            DmaStreamComputeShapeKind.Contiguous1D,
            new[]
            {
                new DmaStreamComputeMemoryRange(0x1000, 16),
                new DmaStreamComputeMemoryRange(0x2000, 16),
                new DmaStreamComputeMemoryRange(0x3000, 16)
            },
            new[] { new DmaStreamComputeMemoryRange(0x9000, 16) },
            new uint[] { 17, 48, 99, 170 }
        };
        yield return new object[]
        {
            DmaStreamComputeOperationKind.Reduce,
            DmaStreamComputeShapeKind.FixedReduce,
            new[] { new DmaStreamComputeMemoryRange(0x1000, 16) },
            new[] { new DmaStreamComputeMemoryRange(0x9000, 4) },
            new uint[] { 10 }
        };
    }

    private static void InitializeMainMemory(ulong bytes)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, bytes);
        Processor.Memory = null;
    }

    private static void WriteUInt32Array(ulong address, params uint[] values)
    {
        byte[] bytes = new byte[checked(values.Length * sizeof(uint))];
        for (int i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(i * sizeof(uint), sizeof(uint)),
                values[i]);
        }

        Assert.True(Processor.MainMemory.TryWritePhysicalRange(address, bytes));
    }

    private static uint[] ReadUInt32Array(ulong address, int count)
    {
        byte[] bytes = new byte[checked(count * sizeof(uint))];
        Assert.True(Processor.MainMemory.TryReadPhysicalRange(address, bytes));
        uint[] values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(i * sizeof(uint), sizeof(uint)));
        }

        return values;
    }

    private static DmaStreamComputeDescriptor CreateDescriptor(
        DmaStreamComputeOperationKind operation,
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D,
        DmaStreamComputeMemoryRange[]? readRanges = null,
        DmaStreamComputeMemoryRange[]? writeRanges = null,
        DmaStreamComputeAliasPolicy aliasPolicy = DmaStreamComputeAliasPolicy.Disjoint)
    {
        readRanges ??= operation switch
        {
            DmaStreamComputeOperationKind.Copy or
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
        writeRanges ??= new[] { new DmaStreamComputeMemoryRange(0x9000, 16) };

        var ownerBinding = new DmaStreamComputeOwnerBinding
        {
            OwnerVirtualThreadId = 1,
            OwnerContextId = 77,
            OwnerCoreId = 1,
            OwnerPodId = 2,
            OwnerDomainTag = 0xD0A11,
            DeviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId
        };
        var ownerContext = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId);
        DmaStreamComputeOwnerGuardDecision guardDecision =
            new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(ownerBinding, ownerContext);

        return new DmaStreamComputeDescriptor
        {
            DescriptorReference = new DmaStreamComputeDescriptorReference(
                descriptorAddress: 0x7000,
                descriptorSize: 128,
                descriptorIdentityHash: IdentityHash),
            AbiVersion = DmaStreamComputeDescriptorParser.CurrentAbiVersion,
            HeaderSize = 128,
            TotalSize = 128,
            DescriptorIdentityHash = IdentityHash,
            CertificateInputHash = 0xC011EC7EUL,
            Operation = operation,
            ElementType = DmaStreamComputeElementType.UInt32,
            Shape = shape,
            RangeEncoding = DmaStreamComputeRangeEncoding.InlineContiguous,
            PartialCompletionPolicy = DmaStreamComputePartialCompletionPolicy.AllOrNone,
            OwnerBinding = ownerBinding,
            OwnerGuardDecision = guardDecision,
            ReadMemoryRanges = readRanges,
            NormalizedReadMemoryRanges = Normalize(readRanges),
            WriteMemoryRanges = writeRanges,
            NormalizedWriteMemoryRanges = Normalize(writeRanges),
            AliasPolicy = aliasPolicy,
            NormalizedFootprintHash = 0xF007F007UL
        };
    }

    private static DmaStreamComputeMemoryRange[] Normalize(DmaStreamComputeMemoryRange[] ranges)
    {
        DmaStreamComputeMemoryRange[] sorted = (DmaStreamComputeMemoryRange[])ranges.Clone();
        Array.Sort(
            sorted,
            static (left, right) =>
            {
                int addressCompare = left.Address.CompareTo(right.Address);
                return addressCompare != 0
                    ? addressCompare
                    : left.Length.CompareTo(right.Length);
            });

        var normalized = new System.Collections.Generic.List<DmaStreamComputeMemoryRange>();
        ulong currentStart = sorted[0].Address;
        ulong currentEnd = sorted[0].Address + sorted[0].Length;
        for (int i = 1; i < sorted.Length; i++)
        {
            ulong nextEnd = sorted[i].Address + sorted[i].Length;
            if (sorted[i].Address <= currentEnd)
            {
                if (nextEnd > currentEnd)
                {
                    currentEnd = nextEnd;
                }

                continue;
            }

            normalized.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
            currentStart = sorted[i].Address;
            currentEnd = nextEnd;
        }

        normalized.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
        return normalized.ToArray();
    }
}
