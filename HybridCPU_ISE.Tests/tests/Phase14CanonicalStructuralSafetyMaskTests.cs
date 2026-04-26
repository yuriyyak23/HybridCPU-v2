using System;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase14;

public sealed class Phase14CanonicalStructuralSafetyMaskTests
{
    private sealed class CanonicalControlFlowWithoutMaskMicroOp : MicroOp
    {
        public CanonicalControlFlowWithoutMaskMicroOp()
        {
            IsControlFlow = true;
            Class = MicroOpClass.Control;
            SerializationClass = SerializationClass.Free;
            SetHardPinnedPlacement(SlotClass.BranchControl, 7);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "canonical-control-without-mask";
    }

    private sealed class RegisterSafetyMaskWithoutRegisterFactsMicroOp : MicroOp
    {
        public RegisterSafetyMaskWithoutRegisterFactsMicroOp()
        {
            SafetyMask = ResourceMaskBuilder.ForRegisterWrite128(5);
        }

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "register-mask-without-register-facts";
    }

    [Fact]
    public void LoadMicroOp_AdmissionMetadata_PublishesNonZeroStructuralSafetyMask()
    {
        var microOp = new LoadMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Load,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            DestRegID = 5,
            BaseRegID = 10,
            Address = 0x1000,
            Size = 4,
            WritesRegister = true
        };

        microOp.InitializeMetadata();

        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.True(microOp.AdmissionMetadata.SharedStructuralMask.IsNonZero);
    }

    [Fact]
    public void BranchMicroOp_UnconditionalWithoutRegisterTraffic_PublishesLane7StructuralSafetyMask()
    {
        var microOp = new BranchMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.JAL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            DestRegID = VLIW_Instruction.NoReg,
            Reg1ID = VLIW_Instruction.NoReg,
            Reg2ID = VLIW_Instruction.NoReg,
            IsConditional = false
        };

        microOp.InitializeMetadata();

        Assert.Equal(0UL, microOp.AdmissionMetadata.StructuralSafetyMask.Low);
        Assert.Equal(1UL << 63, microOp.AdmissionMetadata.StructuralSafetyMask.High);
    }

    [Fact]
    public void SysEventMicroOp_Yield_PublishesNonZeroStructuralSafetyMaskWithoutFallback()
    {
        SysEventMicroOp microOp = SysEventMicroOp.ForYield();

        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
    }

    [Fact]
    public void VConfigMicroOp_ImmediateContour_PublishesNonZeroStructuralSafetyMask()
    {
        var microOp = new VConfigMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSETIVLI,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            DestRegID = VLIW_Instruction.NoReg
        };

        microOp.ConfigureForImmediateAvlAndVType(
            encodedAvlImmediate: 8,
            encodedVTypeImmediate: 0x80);
        microOp.InitializeMetadata();

        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
    }

    [Fact]
    public void VectorBinaryOpMicroOp_PublishesExplicitStructuralSafetyMaskForReadWriteContour()
    {
        var microOp = new VectorBinaryOpMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VADD,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            Instruction = CreateVectorInstruction(
                (uint)Processor.CPU_Core.InstructionsEnum.VADD,
                destSrc1Pointer: 0x200,
                src2Pointer: 0x240)
        };

        microOp.InitializeMetadata();

        Assert.True(microOp.AdmissionMetadata.StructuralSafetyMask.IsNonZero);
        Assert.True((microOp.AdmissionMetadata.StructuralSafetyMask.Low & (1UL << 48)) != 0);
        Assert.True((microOp.AdmissionMetadata.StructuralSafetyMask.Low & (1UL << 49)) != 0);
    }

    [Fact]
    public void RefreshAdmissionMetadata_WhenCanonicalControlFlowOmitsExplicitStructuralMask_Throws()
    {
        var microOp = new CanonicalControlFlowWithoutMaskMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.JAL
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => microOp.RefreshAdmissionMetadata());

        Assert.Contains("explicit structural safety mask", exception.Message);
    }

    [Fact]
    public void AdmissionMetadata_DoesNotRecoverRegisterHazardsFromStructuralMaskLowBits()
    {
        var microOp = new RegisterSafetyMaskWithoutRegisterFactsMicroOp();

        MicroOpAdmissionMetadata admission = microOp.AdmissionMetadata;

        Assert.Empty(admission.ReadRegisters);
        Assert.Empty(admission.WriteRegisters);
        Assert.Equal(0U, admission.RegisterHazardMask);
    }

    private static VLIW_Instruction CreateVectorInstruction(
        uint opcode,
        ulong destSrc1Pointer,
        ulong src2Pointer)
    {
        return new VLIW_Instruction
        {
            OpCode = opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = destSrc1Pointer,
            Src2Pointer = src2Pointer,
            StreamLength = 8,
            Stride = 4,
            VirtualThreadId = 0
        };
    }
}
