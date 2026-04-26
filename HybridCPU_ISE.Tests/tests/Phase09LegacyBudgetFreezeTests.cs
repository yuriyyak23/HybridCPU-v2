using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Phase 0 CI gate for canonical ISA runtime suites.
/// Canonical production paths must not consume legacy raw fallback or
/// invariant-violation budget while they retire architecturally visible work.
/// </summary>
public sealed class Phase09LegacyBudgetFreezeTests
{
    private sealed class SucceedingScalarMicroOp : MicroOp
    {
        public SucceedingScalarMicroOp(uint opCode, ushort destReg)
        {
            OpCode = opCode;
            DestRegID = destReg;
            WritesRegister = true;
            Class = MicroOpClass.Alu;
            InstructionClass = InstructionClass.ScalarAlu;
            SerializationClass = SerializationClass.Free;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Phase 0 succeeding scalar MicroOp";
    }

    [Fact]
    public void CanonicalExecutePath_DoesNotConsumeLegacyRetireCompatibilityBudget()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();

        core.TestRunExecuteStageWithDecodedInstruction(
            CreateScalarInstruction(InstructionsEnum.Addition, rd: 1, rs1: 2, rs2: 3),
            new SucceedingScalarMicroOp((uint)InstructionsEnum.Addition, destReg: 1),
            writesRegister: true,
            reg1Id: 1,
            reg2Id: 2,
            reg3Id: 3,
            pc: 0x6000UL);

        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        Assert.Equal(0UL, ctrl.InvariantViolationCount);
    }

    [Fact]
    public void ExecutionDispatcherCsrRetireWindowPublication_PathKeepsPhase0LegacyBudgetAtZero()
    {
        const int vtId = 0;
        const ulong retiredPc = 0x6050UL;
        const ushort sourceRegister = 1;
        const ushort destinationRegister = 9;
        const ulong oldCsrValue = 0x55UL;
        const ulong newCsrValue = 0xABUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(retiredPc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, retiredPc);
        core.Csr.Write(CsrAddresses.Mstatus, oldCsrValue, PrivilegeLevel.Machine);
        core.WriteCommittedArch(vtId, sourceRegister, newCsrValue);

        var dispatcher = new ExecutionDispatcherV4(csrFile: core.Csr);
        ICanonicalCpuState state = core.CreateLiveCpuStateAdapter(vtId);
        var ir = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.CSRRW,
            Class = InstructionClassifier.GetClass(InstructionsEnum.CSRRW),
            SerializationClass = InstructionClassifier.GetSerializationClass(InstructionsEnum.CSRRW),
            Rd = (byte)destinationRegister,
            Rs1 = (byte)sourceRegister,
            Rs2 = 0,
            Imm = CsrAddresses.Mstatus
        };

        core.TestApplyExecutionDispatcherRetireWindowPublications(
            dispatcher,
            ir,
            state,
            bundleSerial: 1,
            vtId: (byte)vtId);

        Assert.Equal(oldCsrValue, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(newCsrValue, core.Csr.DirectRead(CsrAddresses.Mstatus));

        PipelineControl ctrl = core.GetPipelineControl();
        AssertCanonicalPhase0BudgetIsZero(core, ctrl);
    }

    [Fact]
    public void CanonicalScalarDecodeExecuteRetire_PathKeepsPhase0LegacyBudgetAtZero()
    {
        const int vtId = 2;
        const ulong pc = 0x6100UL;
        const ushort sourceRegister = 5;
        const ushort destinationRegister = 7;
        const ulong sourceValue = 9UL;
        const ulong originalDestinationValue = 0xAAAA_BBBB_CCCC_DDDDUL;
        const ulong expectedResult = 8UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, sourceRegister, sourceValue);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        PrimeReplayScheduler(ref core, pc);

        VLIW_Instruction[] rawSlots =
            CreateBundle(
                CreateScalarInstruction(
                    InstructionsEnum.ADDI,
                    rd: (byte)destinationRegister,
                    rs1: (byte)sourceRegister,
                    immediate: 0xFFFF));

        core.TestRunDecodeStageWithFetchedBundle(rawSlots, pc);
        core.TestRunExecuteStageFromCurrentDecodeState();
        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(expectedResult, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(pc, core.ReadCommittedPc(vtId));

        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(1UL, ctrl.InstructionsRetired);
        Assert.Equal(1UL, ctrl.ScalarLanesRetired);
        Assert.Equal(0UL, ctrl.NonScalarLanesRetired);
        AssertCanonicalPhase0BudgetIsZero(core, ctrl);
    }

    [Fact]
    public void CanonicalExplicitPacketLoadRetire_PathKeepsPhase0LegacyBudgetAtZero()
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        const int vtId = 1;
        const ulong pc = 0x6140UL;
        const ulong address = 0x1880UL;
        const ushort destinationRegister = 9;
        const ulong originalDestinationValue = 0xDEAD_BEEF_CAFE_BABEUL;
        const ulong loadedValue = 0x0102_0304_0506_0708UL;

        WriteBytes(address, BitConverter.GetBytes(loadedValue));

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(pc, activeVtId: vtId);
        core.WriteCommittedPc(vtId, pc);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);
        core.TestPrepareExplicitPacketLoadForWriteBack(
            laneIndex: 4,
            pc,
            address,
            destinationRegister,
            accessSize: 8,
            vtId);

        core.TestRunWriteBackStage();

        Assert.Equal(loadedValue, core.ReadArch(vtId, destinationRegister));
        Assert.Equal(pc, core.ReadCommittedPc(vtId));
        Assert.Equal(pc, core.ReadActiveLivePc());

        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(1UL, ctrl.InstructionsRetired);
        Assert.Equal(0UL, ctrl.ScalarLanesRetired);
        Assert.Equal(1UL, ctrl.NonScalarLanesRetired);
        AssertCanonicalPhase0BudgetIsZero(core, ctrl);
    }

    [Fact]
    public void CanonicalConditionalBranchRedirect_PathKeepsPhase0LegacyBudgetAtZero_WithRetireOwnedRedirect()
    {
        const int vtId = 2;
        const ulong startPc = 0x6180UL;
        const ulong untouchedVt0Pc = 0x1100UL;
        const ushort branchImmediate = 0x0040;
        const ulong targetPc = startPc + branchImmediate;
        const ushort rs1 = 3;
        const ushort rs2 = 4;
        const ulong compareValue = 0x5566_7788_99AA_BBCCUL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(startPc, activeVtId: vtId);
        core.WriteCommittedPc(0, untouchedVt0Pc);
        core.WriteCommittedPc(vtId, startPc);
        core.WriteCommittedArch(vtId, rs1, compareValue);
        core.WriteCommittedArch(vtId, rs2, compareValue);

        VLIW_Instruction instruction =
            CreateControlInstruction(
                InstructionsEnum.BEQ,
                rs1: (byte)rs1,
                rs2: (byte)rs2,
                immediate: branchImmediate);
        BranchMicroOp microOp = CreateConditionalBranchMicroOp(InstructionsEnum.BEQ, instruction, vtId);

        core.TestRunExecuteStageWithDecodedInstruction(
            instruction,
            microOp,
            isBranchOp: true,
            reg1Id: instruction.Reg1ID,
            reg2Id: instruction.Reg2ID,
            reg3Id: instruction.Reg3ID,
            pc: startPc);

        Assert.Equal(startPc, core.ReadActiveLivePc());
        Assert.Equal(startPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));

        core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

        Assert.Equal(targetPc, core.ReadActiveLivePc());
        Assert.Equal(targetPc, core.ReadCommittedPc(vtId));
        Assert.Equal(untouchedVt0Pc, core.ReadCommittedPc(0));

        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(1UL, ctrl.InstructionsRetired);
        Assert.Equal(1UL, ctrl.ScalarLanesRetired);
        Assert.Equal(0UL, ctrl.NonScalarLanesRetired);
        AssertCanonicalPhase0BudgetIsZero(core, ctrl);
    }

    [Fact]
    public void CanonicalSystemLane7Retire_PathKeepsPhase0LegacyBudgetAtZero()
    {
        const int vtId = 2;
        const ulong retiredPc = 0x61C0UL;

        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(retiredPc, vtId);
        core.WriteCommittedPc(vtId, retiredPc);

        SysEventMicroOp microOp = CreateSystemEventMicroOp(InstructionsEnum.FENCE, vtId);

        core.TestRetireExplicitLane7SingletonMicroOp(
            microOp,
            pc: retiredPc,
            vtId);

        Assert.Equal(retiredPc, core.ReadCommittedPc(vtId));
        Assert.Equal(retiredPc, core.ReadActiveLivePc());

        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(1UL, ctrl.InstructionsRetired);
        Assert.Equal(0UL, ctrl.ScalarLanesRetired);
        Assert.Equal(1UL, ctrl.NonScalarLanesRetired);
        AssertCanonicalPhase0BudgetIsZero(core, ctrl);
    }

    [Fact]
    public void DirectStreamScalarHelper_PathPublishesThroughRetireWindowWithoutCompatApply()
    {
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(3, 0b1011_0101UL);

        var inst = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(3 | (6 << 8)),
            StreamLength = 8
        };

        core.ExecuteDirectStreamCompat(inst);

        Assert.Equal(5UL, core.ReadArch(0, 6));
        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        Assert.Equal(0UL, ctrl.InvariantViolationCount);
    }

    [Fact]
    public void DirectStreamRetireWindowSeam_PathPublishesWithoutCompatApply()
    {
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(3, 0b1011_0101UL);

        var inst = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VPOPC,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            Immediate = (ushort)(3 | (6 << 8)),
            StreamLength = 8
        };

        core.TestApplyStreamRetireWindowPublications(inst);

        Assert.Equal(5UL, core.ReadArch(0, 6));
        PipelineControl ctrl = core.GetPipelineControl();
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        Assert.Equal(0UL, ctrl.InvariantViolationCount);
    }

    private static void AssertCanonicalPhase0BudgetIsZero(
        Processor.CPU_Core core,
        PipelineControl ctrl)
    {
        Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
        Assert.Equal(0UL, ctrl.InvariantViolationCount);
        Assert.Equal(0UL, ctrl.DecodeFallbackCount);
        Assert.Equal(0UL, ctrl.DecodeFaultBundleCount);
    }

    private static void PrimeReplayScheduler(
        ref Processor.CPU_Core core,
        ulong retiredPc)
    {
        core.TestInitializeFSPScheduler();
        core.TestPrimeReplayPhase(
            pc: retiredPc,
            totalIterations: 8,
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3));

        MicroOpScheduler scheduler = core.TestGetFSPScheduler()!;
        var capacityState = new SlotClassCapacityState();
        capacityState.InitializeFromLaneMap();
        scheduler.TestSetClassCapacityTemplate(new ClassCapacityTemplate(capacityState));
        scheduler.TestSetClassTemplateValid(true);
        scheduler.TestSetClassTemplateDomainId(0);
    }

    private static BranchMicroOp CreateConditionalBranchMicroOp(
        InstructionsEnum opcode,
        VLIW_Instruction instruction,
        int vtId)
    {
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = instruction.Immediate,
            HasImmediate = true,
            PackedRegisterTriplet = instruction.DestSrc1Pointer,
            HasPackedRegisterTriplet = true,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        BranchMicroOp microOp =
            Assert.IsType<BranchMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static SysEventMicroOp CreateSystemEventMicroOp(
        InstructionsEnum opcode,
        int vtId)
    {
        VLIW_Instruction instruction = CreateSystemInstruction(opcode);
        var context = new DecoderContext
        {
            OpCode = (uint)opcode,
            Reg1ID = instruction.Reg1ID,
            Reg2ID = instruction.Reg2ID,
            Reg3ID = instruction.Reg3ID,
            AuxData = instruction.Src2Pointer,
            PredicateMask = instruction.PredicateMask
        };

        SysEventMicroOp microOp =
            Assert.IsType<SysEventMicroOp>(InstructionRegistry.CreateMicroOp((uint)opcode, context));
        microOp.OwnerThreadId = vtId;
        microOp.VirtualThreadId = vtId;
        microOp.OwnerContextId = vtId;
        microOp.RefreshAdmissionMetadata();
        return microOp;
    }

    private static VLIW_Instruction[] CreateBundle(
        params VLIW_Instruction[] slots)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < slots.Length && slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = slots[slotIndex];
        }

        return rawSlots;
    }

    private static VLIW_Instruction CreateScalarInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ushort immediate = 0)
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
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateControlInstruction(
        InstructionsEnum opcode,
        byte rd = 0,
        byte rs1 = 0,
        byte rs2 = 0,
        ulong targetPc = 0,
        ushort immediate = 0)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(rd, rs1, rs2),
            Src2Pointer = targetPc,
            Immediate = immediate,
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static VLIW_Instruction CreateSystemInstruction(InstructionsEnum opcode)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            StreamLength = 0,
            Stride = 0,
            VirtualThreadId = 0
        };
    }

    private static void WriteBytes(ulong address, byte[] bytes)
    {
        Processor.MainMemory.WriteToPosition(bytes, address);
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

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }
}

