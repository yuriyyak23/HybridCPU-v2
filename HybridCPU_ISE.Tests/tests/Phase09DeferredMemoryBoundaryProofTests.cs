using System;
using System.IO;
using System.Linq;
using Xunit;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DeferredMemoryBoundaryProofTests
{
    [Fact]
    public void BurstRead_WhenContiguousRequestCrossesMappedBoundary_ThenFailsClosedOnShortTransfer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        Processor.Memory = null;

        byte[] buffer = new byte[8];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstRead(0xFFC, buffer, elementCount: 2, elementSize: sizeof(uint), stride: sizeof(uint)));

        Assert.Contains(nameof(BurstIO.BurstRead), ex.Message, StringComparison.Ordinal);
        Assert.Contains("materialized only 1 of 2 element(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstWrite_WhenStridedRequestCrossesMappedBoundary_ThenFailsClosedOnShortTransfer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        Processor.Memory = null;

        byte[] buffer = BitConverter.GetBytes(0x1122_3344U).Concat(BitConverter.GetBytes(0x5566_7788U)).ToArray();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstWrite(0xFFC, buffer, elementCount: 2, elementSize: sizeof(uint), stride: 4));

        Assert.Contains(nameof(BurstIO.BurstWrite), ex.Message, StringComparison.Ordinal);
        Assert.Contains("materialized only 1 of 2 element(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstRead2D_WhenRequestCrossesMappedBoundary_ThenFailsClosedOnShortTransfer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        Processor.Memory = null;

        byte[] buffer = new byte[8];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstRead2D(
                baseAddr: 0xFFC,
                buffer,
                elementCount: 2,
                elementSize: sizeof(uint),
                rowLength: 1,
                rowStride: 4,
                colStride: 4));

        Assert.Contains(nameof(BurstIO.BurstRead2D), ex.Message, StringComparison.Ordinal);
        Assert.Contains("materialized only 1 of 2 element(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstGather_WhenIndexedRequestCrossesMappedBoundary_ThenFailsClosedOnShortTransfer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        Processor.Memory = null;

        byte[] indices = new byte[sizeof(uint) * 2];
        BitConverter.GetBytes(0U).CopyTo(indices, 0);
        BitConverter.GetBytes(1024U).CopyTo(indices, sizeof(uint));
        byte[] buffer = new byte[8];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstGather(
                baseAddr: 0,
                buffer,
                elementCount: 2,
                elementSize: sizeof(uint),
                indices,
                indexSize: sizeof(uint),
                indexIsByteOffset: 0));

        Assert.Contains(nameof(BurstIO.BurstGather), ex.Message, StringComparison.Ordinal);
        Assert.Contains("materialized only 1 of 2 element(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstScatter_WhenIndexedRequestCrossesMappedBoundary_ThenFailsClosedOnShortTransfer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        Processor.Memory = null;

        byte[] indices = new byte[sizeof(uint) * 2];
        BitConverter.GetBytes(0U).CopyTo(indices, 0);
        BitConverter.GetBytes(1024U).CopyTo(indices, sizeof(uint));
        byte[] buffer = BitConverter.GetBytes(0x1122_3344U).Concat(BitConverter.GetBytes(0x5566_7788U)).ToArray();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstScatter(
                baseAddr: 0,
                buffer,
                elementCount: 2,
                elementSize: sizeof(uint),
                indices,
                indexSize: sizeof(uint),
                indexIsByteOffset: 0));

        Assert.Contains(nameof(BurstIO.BurstScatter), ex.Message, StringComparison.Ordinal);
        Assert.Contains("materialized only 1 of 2 element(s)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstWrite_WhenLargeContiguousTransferUsesDmaEmulationContour_ThenPreservesCommittedWriteSuccess()
    {
        InitializeCpuMainMemoryIdentityMap(0x8000);
        Processor proc = default;
        Processor.DMAController = new DMAController(ref proc);

        byte[] buffer = new byte[8192];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i & 0xFF);
        }

        ulong written = BurstIO.BurstWrite(0x200, buffer, elementCount: 8192, elementSize: 1, stride: 1);

        Assert.Equal(8192UL, written);
        byte[] materialized = Processor.MainMemory.ReadFromPosition(new byte[8192], 0x200, 8192);
        Assert.Equal(buffer, materialized);
    }

    [Fact]
    public void MemoryRequestToken_WhenAsyncReadCompletesUnsuccessfully_ThenPropagatesTruthfulFailureState()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        byte[] buffer = new byte[4];
        MemorySubsystem.MemoryRequestToken token = Processor.Memory!.EnqueueRead(0, 0x2000, 4, buffer);

        AdvanceUntilComplete(token);

        Assert.True(token.IsComplete);
        Assert.False(token.Succeeded);
        Assert.Contains("failed after bank admission", token.FailureReason, StringComparison.Ordinal);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => token.ThrowIfFailed("test-surface"));
        Assert.Contains("did not materialize successfully", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadMicroOp_WhenAsyncReadCompletesUnsuccessfully_ThenFailsClosedBeforeRegisterPublication()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        var load = new LoadMicroOp
        {
            Address = 0x2000,
            Size = 4,
            DestRegID = 9,
            BaseRegID = 1,
            WritesRegister = true
        };
        load.InitializeMetadata();

        Assert.False(load.Execute(ref core));
        AdvanceAllMemoryWork();

        PageFaultException ex = Assert.Throws<PageFaultException>(() => load.Execute(ref core));
        Assert.NotNull(ex.InnerException);
        Assert.Contains("LoadMicroOp.Execute()", ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Contains("did not materialize successfully", ex.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreMicroOp_WhenAsyncWriteCompletesUnsuccessfully_ThenFailsClosedBeforeMemoryVisibleSuccess()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        var store = new StoreMicroOp
        {
            Address = 0x2000,
            Size = 4,
            Value = 0x1122_3344U,
            SrcRegID = 2,
            BaseRegID = 1
        };
        store.InitializeMetadata();

        Assert.False(store.Execute(ref core));
        AdvanceAllMemoryWork();

        PageFaultException ex = Assert.Throws<PageFaultException>(() => store.Execute(ref core));
        Assert.NotNull(ex.InnerException);
        Assert.Contains("StoreMicroOp.Execute()", ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Contains("did not materialize successfully", ex.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSegmentMicroOp_WhenAsyncReadCompletesUnsuccessfully_ThenFailsClosedBeforePublishingLoadedBuffer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        var microOp = new LoadSegmentMicroOp
        {
            Instruction = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VLOAD,
                DataTypeValue = DataTypeEnum.UINT32,
                DestSrc1Pointer = 0x2000,
                StreamLength = 2,
                Stride = 4
            }
        };
        microOp.InitializeMetadata();

        Assert.False(microOp.Execute(ref core));
        AdvanceAllMemoryWork();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => microOp.Execute(ref core));
        Assert.Contains("LoadSegmentMicroOp.Execute()", ex.Message, StringComparison.Ordinal);
        Assert.Contains("did not materialize successfully", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreSegmentMicroOp_WhenAsyncWriteCompletesUnsuccessfully_ThenFailsClosedBeforePublishingStoredBuffer()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        var microOp = new StoreSegmentMicroOp
        {
            Instruction = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSTORE,
                DataTypeValue = DataTypeEnum.UINT32,
                DestSrc1Pointer = 0x2000,
                StreamLength = 2,
                Stride = 4
            }
        };
        microOp.SetStoreBuffer(BitConverter.GetBytes(0x1122_3344U).Concat(BitConverter.GetBytes(0x5566_7788U)).ToArray());
        microOp.InitializeMetadata();

        Assert.False(microOp.Execute(ref core));
        AdvanceAllMemoryWork();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => microOp.Execute(ref core));
        Assert.Contains("StoreSegmentMicroOp.Execute()", ex.Message, StringComparison.Ordinal);
        Assert.Contains("did not materialize successfully", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitPacketLoadLane_WhenAsyncReadCompletesUnsuccessfully_ThenFailsClosedBeforeDecodeOrRetire()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        const int vtId = 1;
        const ushort destinationRegister = 9;
        const ulong originalDestinationValue = 0xCAFE_BABE_DEAD_BEEFUL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 4,
                pc: 0x2840,
                address: 0x2000,
                destRegId: destinationRegister,
                accessSize: 4,
                vtId));

        Assert.Contains("explicit-packet memory lane", ex.Message, StringComparison.Ordinal);
        Assert.Contains("did not materialize successfully", ex.Message, StringComparison.Ordinal);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
    }

    [Fact]
    public void ReplayToken_WhenCaptureMemoryStateCrossesBoundary_ThenFailsClosed()
    {
        var token = new ReplayToken(Processor.MainMemory);
        ulong address = (ulong)Processor.MainMemory.Length - 2;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => token.CaptureMemoryState(address, 4));

        Assert.Contains("ReplayToken.CaptureMemoryState()", ex.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayToken_WhenRollbackRestoresOutOfRangeMemoryImage_ThenFailsClosed()
    {
        var token = new ReplayToken(Processor.MainMemory);
        token.PreExecutionMemoryState.Add(((ulong)Processor.MainMemory.Length - 2, new byte[4]));
        var core = new Processor.CPU_Core(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => token.Rollback(ref core));

        Assert.Contains("ReplayToken.Rollback()", ex.Message, StringComparison.Ordinal);
        Assert.Contains("fail closed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MainMemoryReadFromPosition_WhenPhysicalReadIsSilentlySquashed_ThenFailsClosed()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        byte[] seed = BitConverter.GetBytes(0x1122_3344U);
        Processor.MainMemory.WriteToPosition(seed, 0x100);

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => Processor.MainMemory.ReadFromPosition(new byte[4], 0x100, 4));
            Assert.Contains("ReadFromPosition(...)", ex.Message, StringComparison.Ordinal);
            Assert.Contains("silently squashed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    [Fact]
    public void MainMemoryVirtualRead_WhenPhysicalReadIsSilentlySquashed_ThenFailsClosed()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        byte[] seed = BitConverter.GetBytes(0x1122_3344U);
        Processor.MainMemory.WriteToPosition(seed, 0x100);
        Processor.MainMemory.Position = 0x100;

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => Processor.MainMemory.Read(new byte[4], 0, 4));
            Assert.Contains("Virtual MainMemory.Read(...)", ex.Message, StringComparison.Ordinal);
            Assert.Contains("silently squashed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    [Fact]
    public void MainMemoryWriteToPosition_WhenPhysicalWriteIsSilentlySquashed_ThenFailsClosed()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(0x5566_7788U), 0x100));
            Assert.Contains("WriteToPosition(...)", ex.Message, StringComparison.Ordinal);
            Assert.Contains("silently squashed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    [Fact]
    public void MainMemoryVirtualWrite_WhenPhysicalWriteIsSilentlySquashed_ThenFailsClosed()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        Processor.MainMemory.Position = 0x100;

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => Processor.MainMemory.Write(BitConverter.GetBytes(0x5566_7788U), 0, 4));
            Assert.Contains("Virtual MainMemory write", ex.Message, StringComparison.Ordinal);
            Assert.Contains("silently squashed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    [Fact]
    public void IommuBurstAndDma_WhenDomainRejectsOrSilentSquashOccurs_ThenSurfaceFailedReadWriteTruth()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);

        byte[] seed = BitConverter.GetBytes(0x1122_3344U);
        Processor.MainMemory.WriteToPosition(seed, 0x100);

        byte[] readBuffer = new byte[4];
        Assert.False(IOMMU.ReadBurst(threadId: 1, ioVirtualAddress: 0x100, readBuffer, checkDomain: true));
        Assert.False(IOMMU.WriteBurst(threadId: 1, ioVirtualAddress: 0x100, seed, checkDomain: true));

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            Assert.Equal(0UL, IOMMU.DMARead(0, 0x100, readBuffer, 0, 4));
            Assert.Equal(0UL, IOMMU.DMAWrite(0, 0x100, seed, 0, 4));
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }
    }

    [Fact]
    public void ExplicitPacketStoreCommit_WhenPhysicalWriteIsSilentlySquashed_ThenFailsClosedBeforeMemoryVisibleSuccess()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        InitializeMemorySubsystem();

        byte[] baseline = BitConverter.GetBytes(0xAABB_CCDDU);
        Processor.MainMemory.WriteToPosition(baseline, 0x100);

        var core = new Processor.CPU_Core(0);
        core.TestPrepareExplicitPacketStoreForWriteBack(
            laneIndex: 4,
            pc: 0x3480,
            address: 0x100,
            data: 0x1122_3344U,
            accessSize: 4,
            vtId: 1);

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => core.TestRunWriteBackStage());
            Assert.Contains("Retired store lane", ex.Message, StringComparison.Ordinal);
            Assert.Contains("failed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }

        Assert.Equal(baseline, Processor.MainMemory.ReadFromPosition(new byte[4], 0x100, 4));
    }

    [Fact]
    public void ExplicitPacketAtomicCommit_WhenPhysicalAccessIsSilentlySquashed_ThenFailsClosedBeforeReplayVisibleCommit()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap(0x1000, preserveCurrentMainMemory: true);
        InitializeMemorySubsystem();

        const int vtId = 1;
        const ushort destinationRegister = 9;
        const ulong originalDestinationValue = 0xCAFE_BABE_DEAD_BEEFUL;
        Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(10U), 0x100);

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, 1, 0x100);
        core.WriteCommittedArch(vtId, 2, 5);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        var microOp = new AtomicMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.AMOADD_W,
            OwnerThreadId = vtId,
            VirtualThreadId = vtId,
            OwnerContextId = vtId,
            DestRegID = destinationRegister,
            BaseRegID = 1,
            SrcRegID = 2,
            Size = 4,
            WritesRegister = true
        };
        microOp.InitializeMetadata();

        core.TestPrepareExplicitPacketAtomicForWriteBack(
            laneIndex: 4,
            atomicMicroOp: microOp,
            pc: 0x3500,
            vtId);

        YAKSys_Hybrid_CPU.MultiBankMemoryArea memory = Assert.IsType<YAKSys_Hybrid_CPU.MultiBankMemoryArea>(Processor.MainMemory);
        memory.SetBankDomainCapability(0, 0x1UL);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0x2UL);

        try
        {
            IOException ex = Assert.Throws<IOException>(() => core.TestRunWriteBackStage());
            Assert.Contains("Atomic main-memory read failed", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
            memory.SetBankDomainCapability(0, 0);
        }

        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(10U, BitConverter.ToUInt32(Processor.MainMemory.ReadFromPosition(new byte[4], 0x100, 4), 0));
    }

    private static void InitializeCpuMainMemoryIdentityMap(ulong size, bool preserveCurrentMainMemory = false)
    {
        if (!preserveCurrentMainMemory)
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        }

        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: size,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void AdvanceUntilComplete(MemorySubsystem.MemoryRequestToken token)
    {
        for (int i = 0; i < 64 && !token.IsComplete; i++)
        {
            Processor.Memory!.AdvanceCycles(1);
        }

        Assert.True(token.IsComplete);
    }

    private static void AdvanceAllMemoryWork()
    {
        for (int i = 0; i < 64; i++)
        {
            Processor.Memory!.AdvanceCycles(1);
        }
    }
}

