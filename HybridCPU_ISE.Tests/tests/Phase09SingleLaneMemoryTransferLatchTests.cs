using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneMemoryTransferLatchTests
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

        public override string GetDescription() => "Synthetic single-lane memory-transfer carrier";
    }

    [Fact]
    public void SingleLaneMemory_WhenExecuteTransfersIntoMemoryStage_ThenMemoryStageCarriesLiveLatchMetadata()
    {
        const ushort destinationRegister = 13;
        const ushort baseRegister = 5;
        const int ownerThreadId = 1;
        const int virtualThreadId = 2;
        const int ownerContextId = 7;
        const ulong domainTag = 0x24UL;
        const ulong address = 0x280UL;
        const ulong pc = 0x8B00UL;

        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        InitializeCpuMainMemoryIdentityMap();
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);

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

        ExecuteStage executeStage = core.GetExecuteStage();

        core.TestRunMemoryStageFromCurrentExecuteState();

        ExecuteStage consumedExecuteStage = core.GetExecuteStage();
        MemoryStage memoryStage = core.GetMemoryStage();

        Assert.True(executeStage.Valid);
        Assert.False(consumedExecuteStage.Valid);
        Assert.True(memoryStage.Valid);
        Assert.Equal(pc, memoryStage.PC);
        Assert.Equal(instruction.OpCode, memoryStage.OpCode);
        Assert.True(memoryStage.WritesRegister);
        Assert.Equal(destinationRegister, memoryStage.DestRegID);
        Assert.True(memoryStage.ResultReady);
        Assert.Equal(domainTag, memoryStage.DomainTag);
        Assert.Equal(executeStage.MshrScoreboardSlot, memoryStage.MshrScoreboardSlot);
        Assert.Equal(virtualThreadId, memoryStage.MshrVirtualThreadId);
        Assert.Equal(executeStage.ResourceMask, memoryStage.ResourceMask);
        Assert.Equal(executeStage.ResourceToken, memoryStage.ResourceToken);
        Assert.Equal(ownerThreadId, memoryStage.OwnerThreadId);
        Assert.Equal(virtualThreadId, memoryStage.VirtualThreadId);
        Assert.Equal(ownerContextId, memoryStage.OwnerContextId);
        Assert.True(memoryStage.WasFspInjected);
        Assert.Equal(ownerThreadId, memoryStage.OriginalThreadId);
        Assert.Equal(RuntimeClusterAdmissionExecutionMode.ClusterPrepared, memoryStage.AdmissionExecutionMode);
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
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }
}

