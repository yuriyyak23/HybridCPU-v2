using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneMemoryFollowThroughTests
{
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

        public override string GetDescription() => "Synthetic single-lane MEM follow-through carrier";
    }

    [Fact]
    public void SingleLaneMemory_WhenDomainCertRejectsExecuteCarrier_ThenMemoryStageFailClosesWriteAndCountsSquash()
    {
        const ushort destinationRegister = 12;
        const int ownerThreadId = 1;
        const int virtualThreadId = 2;
        const int ownerContextId = 7;
        const ulong domainTag = 0x4UL;
        const ulong pc = 0x8C00UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.CsrMemDomainCert = 0x2UL;

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

        MemoryStage memoryStage = core.GetMemoryStage();
        PipelineControl control = core.GetPipelineControl();

        Assert.True(memoryStage.Valid);
        Assert.Equal(pc, memoryStage.PC);
        Assert.Equal(domainTag, memoryStage.DomainTag);
        Assert.False(memoryStage.WritesRegister);
        Assert.Equal(0UL, memoryStage.ResultValue);
        Assert.True(memoryStage.ResultReady);
        Assert.Equal(1UL, control.DomainSquashCount);
    }

    [Fact]
    public void SingleLaneMemory_WhenLoadCarrierReachesMemoryStage_ThenPublishesLoadedValue()
    {
        const ushort destinationRegister = 14;
        const ulong address = 0x280UL;
        const ulong loadedValue = 0x8877_6655_4433_2211UL;
        const ulong pc = 0x8C80UL;

        InitializeCpuMainMemoryIdentityMap();
        WriteBytes(address, BitConverter.GetBytes(loadedValue));

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
            isMemoryOp: true,
            isLoad: true,
            writesRegister: true,
            destRegId: destinationRegister,
            memoryAddress: address,
            memoryAccessSize: 8,
            pc: pc,
            opCode: (uint)InstructionsEnum.Load);

        core.TestRunMemoryStageFromCurrentExecuteState();

        MemoryStage memoryStage = core.GetMemoryStage();
        ScalarMemoryLaneState lane = core.GetMemoryStageLane(memoryStage.ActiveLaneIndex);

        Assert.True(memoryStage.Valid);
        Assert.True(memoryStage.WritesRegister);
        Assert.True(memoryStage.ResultReady);
        Assert.Equal(loadedValue, memoryStage.ResultValue);
        Assert.True(lane.IsLoad);
        Assert.Equal(address, lane.MemoryAddress);
    }

    [Fact]
    public void SingleLaneMemory_WhenStoreCarrierReachesMemoryStage_ThenDefersCommitAndPublishesStoreData()
    {
        const ulong address = 0x300UL;
        const ulong storeValue = 0x1122_3344_5566_7788UL;
        const ulong pc = 0x8D00UL;

        InitializeCpuMainMemoryIdentityMap();
        byte[] baseline = { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };
        WriteBytes(address, baseline);

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
            isMemoryOp: true,
            isLoad: false,
            writesRegister: false,
            destRegId: 0,
            memoryAddress: address,
            memoryData: storeValue,
            memoryAccessSize: 8,
            pc: pc,
            opCode: (uint)InstructionsEnum.Store);

        core.TestRunMemoryStageFromCurrentExecuteState();

        MemoryStage memoryStage = core.GetMemoryStage();
        ScalarMemoryLaneState lane = core.GetMemoryStageLane(memoryStage.ActiveLaneIndex);

        Assert.True(memoryStage.Valid);
        Assert.False(memoryStage.WritesRegister);
        Assert.True(memoryStage.ResultReady);
        Assert.Equal(storeValue, memoryStage.ResultValue);
        Assert.False(lane.IsLoad);
        Assert.True(lane.DefersStoreCommitToWriteBack);
        Assert.Equal(storeValue, lane.MemoryData);
        Assert.Equal(baseline, ReadBytes(address, baseline.Length));
    }

    [Fact]
    public void SingleLaneMemory_WhenNonMemoryCarrierReachesMemoryStage_ThenForwardsExecuteResult()
    {
        const ushort destinationRegister = 15;
        const ulong resultValue = 0x55AA_1234_0000_7777UL;
        const ulong pc = 0x8D80UL;

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
            isMemoryOp: false,
            isLoad: false,
            writesRegister: true,
            destRegId: destinationRegister,
            resultValue: resultValue,
            pc: pc,
            opCode: (uint)InstructionsEnum.ADDI);

        core.TestRunMemoryStageFromCurrentExecuteState();

        MemoryStage memoryStage = core.GetMemoryStage();
        ScalarMemoryLaneState lane = core.GetMemoryStageLane(memoryStage.ActiveLaneIndex);

        Assert.True(memoryStage.Valid);
        Assert.False(lane.IsMemoryOp);
        Assert.True(memoryStage.WritesRegister);
        Assert.True(memoryStage.ResultReady);
        Assert.Equal(resultValue, memoryStage.ResultValue);
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

    private static byte[] ReadBytes(ulong address, int length)
    {
        return Processor.MainMemory.ReadFromPosition(new byte[length], address, (ulong)length);
    }

    private static void WriteBytes(ulong address, byte[] bytes)
    {
        Processor.MainMemory.WriteToPosition(bytes, address);
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

