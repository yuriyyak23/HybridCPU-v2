using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneWriteBackTransferLatchTests
{
    private sealed class TaggedLoadMicroOp : LoadMicroOp
    {
        public TaggedLoadMicroOp(
            ushort destRegId,
            ushort baseRegId,
            ulong address,
            byte size,
            int ownerThreadId,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag)
        {
            OpCode = (uint)InstructionsEnum.Load;
            Address = address;
            Size = size;
            BaseRegID = baseRegId;
            DestRegID = destRegId;
            WritesRegister = true;
            OwnerThreadId = ownerThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            IsFspInjected = true;
            Placement = Placement with { DomainTag = domainTag };
            InitializeMetadata();
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Synthetic single-lane write-back transfer carrier";
    }

    private sealed class TaggedScalarMicroOp : MicroOp
    {
        private readonly ulong _resultValue;

        public TaggedScalarMicroOp(
            ushort destRegId,
            ulong resultValue,
            int ownerThreadId,
            int virtualThreadId,
            int ownerContextId,
            ulong domainTag)
        {
            _resultValue = resultValue;
            OpCode = (uint)InstructionsEnum.ADDI;
            DestRegID = destRegId;
            WritesRegister = true;
            OwnerThreadId = ownerThreadId;
            VirtualThreadId = virtualThreadId;
            OwnerContextId = ownerContextId;
            IsFspInjected = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
            Placement = Placement with { DomainTag = domainTag };
            RefreshAdmissionMetadata();
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override bool TryGetPrimaryWriteBackResult(out ulong value)
        {
            value = _resultValue;
            return true;
        }

        public override string GetDescription() => "Synthetic single-lane write-back domain-squash carrier";
    }

    [Fact]
    public void SingleLaneWriteBack_WhenMemoryCarrierTransfersFromMem_ThenWriteBackCarriesLiveLatchMetadata()
    {
        const ushort destinationRegister = 13;
        const ushort baseRegister = 5;
        const int ownerThreadId = 1;
        const int virtualThreadId = 2;
        const int ownerContextId = 7;
        const ulong domainTag = 0x24UL;
        const ulong address = 0x280UL;
        const ulong loadedValue = 0x0102_0304_0506_0708UL;
        const ulong pc = 0x8F00UL;

        InitializeCpuMainMemoryIdentityMap();
        Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(loadedValue), address);

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestInitializeFSPScheduler();

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.LW,
                rd: (byte)destinationRegister,
                rs1: (byte)baseRegister,
                virtualThreadId: (byte)virtualThreadId);
        var microOp = new TaggedLoadMicroOp(
            destinationRegister,
            baseRegister,
            address,
            size: 8,
            ownerThreadId,
            virtualThreadId,
            ownerContextId,
            domainTag);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            isMemoryOp: true,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: pc,
            admissionExecutionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared);

        core.TestRunMemoryStageFromCurrentExecuteState();

        MemoryStage memoryStage = core.GetMemoryStage();
        ScalarMemoryLaneState memoryLane = core.GetMemoryStageLane(memoryStage.ActiveLaneIndex);

        core.TestLatchMemoryToWriteBackTransferState();

        MemoryStage consumedMemoryStage = core.GetMemoryStage();
        WriteBackStage writeBackStage = core.GetWriteBackStage();
        ScalarWriteBackLaneState writeBackLane = core.GetWriteBackStageLane(writeBackStage.ActiveLaneIndex);

        Assert.True(memoryStage.Valid);
        Assert.False(consumedMemoryStage.Valid);
        Assert.True(writeBackStage.Valid);
        Assert.Equal(memoryStage.PC, writeBackStage.PC);
        Assert.Equal(memoryStage.OpCode, writeBackStage.OpCode);
        Assert.Equal(memoryStage.ResultValue, writeBackStage.ResultValue);
        Assert.Equal(memoryStage.WritesRegister, writeBackStage.WritesRegister);
        Assert.Equal(memoryStage.DestRegID, writeBackStage.DestRegID);
        Assert.Equal(memoryStage.DomainTag, writeBackStage.DomainTag);
        Assert.Equal(memoryStage.MshrScoreboardSlot, writeBackStage.MshrScoreboardSlot);
        Assert.Equal(memoryStage.MshrVirtualThreadId, writeBackStage.MshrVirtualThreadId);
        Assert.Equal(memoryStage.ResourceMask, writeBackStage.ResourceMask);
        Assert.Equal(memoryStage.ResourceToken, writeBackStage.ResourceToken);
        Assert.Equal(memoryStage.OwnerThreadId, writeBackStage.OwnerThreadId);
        Assert.Equal(memoryStage.VirtualThreadId, writeBackStage.VirtualThreadId);
        Assert.Equal(memoryStage.OwnerContextId, writeBackStage.OwnerContextId);
        Assert.Equal(memoryStage.WasFspInjected, writeBackStage.WasFspInjected);
        Assert.Equal(memoryStage.OriginalThreadId, writeBackStage.OriginalThreadId);
        Assert.Equal(memoryLane.MemoryAddress, writeBackLane.MemoryAddress);
        Assert.Equal(memoryLane.IsLoad, writeBackLane.IsLoad);
        Assert.Equal(memoryLane.MemoryAccessSize, writeBackLane.MemoryAccessSize);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ClusterPrepared, writeBackStage.AdmissionExecutionMode);
    }

    [Fact]
    public void SingleLaneWriteBack_WhenDomainCertRejectsLatchedCarrier_ThenFailClosesWriteAndCountsSquash()
    {
        const ushort destinationRegister = 12;
        const int ownerThreadId = 1;
        const int virtualThreadId = 2;
        const int ownerContextId = 7;
        const ulong domainTag = 0x4UL;
        const ulong pc = 0x8F80UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        VLIW_Instruction instruction =
            CreateScalarInstruction(
                InstructionsEnum.ADDI,
                rd: (byte)destinationRegister,
                rs1: 4,
                rs2: 7,
                virtualThreadId: (byte)virtualThreadId);
        var microOp = new TaggedScalarMicroOp(
            destinationRegister,
            resultValue: 0xAA55UL,
            ownerThreadId,
            virtualThreadId,
            ownerContextId,
            domainTag);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            writesRegister: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: pc,
            admissionExecutionMode: RuntimeClusterAdmissionExecutionMode.ClusterPrepared);

        core.TestRunMemoryStageFromCurrentExecuteState();
        core.TestLatchMemoryToWriteBackTransferState();
        core.CsrMemDomainCert = 0x2UL;

        bool squashed = core.TestApplyWriteBackStageDomainSquash();
        WriteBackStage writeBackStage = core.GetWriteBackStage();
        PipelineControl control = core.GetPipelineControl();

        Assert.True(squashed);
        Assert.True(writeBackStage.Valid);
        Assert.Equal(pc, writeBackStage.PC);
        Assert.Equal(domainTag, writeBackStage.DomainTag);
        Assert.False(writeBackStage.WritesRegister);
        Assert.Equal(0UL, writeBackStage.ResultValue);
        Assert.Equal(1UL, control.DomainSquashCount);
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0,
        byte virtualThreadId = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = virtualThreadId
        };
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
    }
}

