using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerEmissionInventoryTests
{
    private static readonly (string EnumName, int OpcodeValue, string FacadeHelperName)[] CompilerDeferredScalarRows = [];

    public static TheoryData<InstructionsEnum, uint, string> CompilerScalarWordBinaryTailContours => new()
    {
        { InstructionsEnum.DIVW, 311u, nameof(AppAsmFacade.DivideWord) },
        { InstructionsEnum.DIVUW, 312u, nameof(AppAsmFacade.DivideUnsignedWord) },
        { InstructionsEnum.REMW, 313u, nameof(AppAsmFacade.RemainderWord) },
        { InstructionsEnum.REMUW, 314u, nameof(AppAsmFacade.RemainderUnsignedWord) },
    };

    public static TheoryData<InstructionsEnum, uint, string> CompilerScalarWordUnaryTailContours => new()
    {
        { InstructionsEnum.SEXT_W, 320u, nameof(AppAsmFacade.SignExtendWord) },
        { InstructionsEnum.ZEXT_W, 321u, nameof(AppAsmFacade.ZeroExtendWord) },
    };

    [Fact]
    public void AppShiftRightArithmetic_EmitsCanonicalSraRuntimeClosedGate()
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        facade.ShiftRightArithmetic(
            new AsmRegister(7),
            new AsmRegister(5),
            new AsmRegister(6));
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(InstructionsEnum.SRA, (InstructionsEnum)raw.OpCode);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(0, raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(7, rawRd);
        Assert.Equal(5, rawRs1);
        Assert.Equal(6, rawRs2);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.SRA, ir.Opcode);
        Assert.Equal(InstructionClass.ScalarAlu, ir.InstructionClass);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(IrResourceClass.ScalarAlu, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.AluClass, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.ClassFlexible, ir.Annotation.BindingKind);
        Assert.Contains(ir.Annotation.Defs, operand => operand.Name == "rd" && operand.Value == 7UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 5UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs2" && operand.Value == 6UL);
        Assert.Null(ir.Annotation.MemoryReadRegion);
        Assert.Null(ir.Annotation.MemoryWriteRegion);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.FlexibleOpCount);
        Assert.Equal(0, facts.SystemSingletonCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        Assert.Equal(InstructionsEnum.SRA, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(7, loweredRd);
        Assert.Equal(5, loweredRs1);
        Assert.Equal(6, loweredRs2);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrier);
        Assert.Equal(InstructionsEnum.SRA, (InstructionsEnum)scalar.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, scalar.CanonicalDecodePublication);
        Assert.Equal(SlotClass.AluClass, scalar.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, scalar.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(new[] { 5, 6 }, scalar.ReadRegisters);
        Assert.Equal(new[] { 7 }, scalar.WriteRegisters);
        Assert.Empty(scalar.ReadMemoryRanges);
        Assert.Empty(scalar.WriteMemoryRanges);
        Assert.True(scalar.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, scalar.AdmissionMetadata.RegisterHazardMask);
        Assert.True(scalar.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void AppShiftRightArithmeticWordImmediate_EmitsCanonicalSraiwRuntimeClosedGate()
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        facade.ShiftRightArithmeticWordImmediate(
            new AsmRegister(7),
            new AsmRegister(5),
            -1);
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(InstructionsEnum.SRAIW, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT32, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(unchecked((ushort)-1), raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(7, rawRd);
        Assert.Equal(5, rawRs1);
        Assert.Equal(0, rawRs2);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.SRAIW, ir.Opcode);
        Assert.Equal(InstructionClass.ScalarAlu, ir.InstructionClass);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(IrResourceClass.ScalarAlu, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.AluClass, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.ClassFlexible, ir.Annotation.BindingKind);
        Assert.Contains(ir.Annotation.Defs, operand => operand.Name == "rd" && operand.Value == 7UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 5UL);
        Assert.DoesNotContain(ir.Annotation.Uses, operand => operand.Name == "rs2");

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.FlexibleOpCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        Assert.Equal(InstructionsEnum.SRAIW, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(DataTypeEnum.INT32, lowered.DataTypeValue);
        Assert.Equal(unchecked((ushort)-1), lowered.Immediate);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(7, loweredRd);
        Assert.Equal(5, loweredRs1);
        Assert.Equal(0, loweredRs2);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrier);
        Assert.Equal(InstructionsEnum.SRAIW, (InstructionsEnum)scalar.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, scalar.CanonicalDecodePublication);
        Assert.True(scalar.UsesImmediate);
        Assert.Equal(ulong.MaxValue, scalar.Immediate);
        Assert.Equal(new[] { 5 }, scalar.ReadRegisters);
        Assert.Equal(new[] { 7 }, scalar.WriteRegisters);
        Assert.Empty(scalar.ReadMemoryRanges);
        Assert.Empty(scalar.WriteMemoryRanges);
        Assert.True(scalar.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, scalar.AdmissionMetadata.RegisterHazardMask);
        Assert.True(scalar.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void AppMultiplyWord_EmitsCanonicalMulwRuntimeClosedGate()
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        facade.MultiplyWord(
            new AsmRegister(7),
            new AsmRegister(5),
            new AsmRegister(6));
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(310u, raw.OpCode);
        Assert.Equal(InstructionsEnum.MULW, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT32, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(0, raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(7, rawRd);
        Assert.Equal(5, rawRs1);
        Assert.Equal(6, rawRs2);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.MULW, ir.Opcode);
        Assert.Equal(InstructionClass.ScalarAlu, ir.InstructionClass);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(IrResourceClass.ScalarAlu, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.AluClass, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.ClassFlexible, ir.Annotation.BindingKind);
        Assert.Contains(ir.Annotation.Defs, operand => operand.Name == "rd" && operand.Value == 7UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 5UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs2" && operand.Value == 6UL);
        Assert.Null(ir.Annotation.MemoryReadRegion);
        Assert.Null(ir.Annotation.MemoryWriteRegion);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.FlexibleOpCount);
        Assert.Equal(0, facts.SystemSingletonCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        Assert.Equal(InstructionsEnum.MULW, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(DataTypeEnum.INT32, lowered.DataTypeValue);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(7, loweredRd);
        Assert.Equal(5, loweredRs1);
        Assert.Equal(6, loweredRs2);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrier);
        Assert.Equal(InstructionsEnum.MULW, (InstructionsEnum)scalar.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, scalar.CanonicalDecodePublication);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { 5, 6 }, scalar.ReadRegisters);
        Assert.Equal(new[] { 7 }, scalar.WriteRegisters);
        Assert.Empty(scalar.ReadMemoryRanges);
        Assert.Empty(scalar.WriteMemoryRanges);
        Assert.True(scalar.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, scalar.AdmissionMetadata.RegisterHazardMask);
        Assert.True(scalar.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [MemberData(nameof(CompilerScalarWordBinaryTailContours))]
    public void AppScalarWordBinaryTail_EmitsCanonicalRuntimeClosedGate(
        InstructionsEnum opcode,
        uint expectedOpcode,
        string helperName)
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        EmitScalarWordBinaryTail(facade, opcode, new AsmRegister(7), new AsmRegister(5), new AsmRegister(6));
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(expectedOpcode, raw.OpCode);
        Assert.Equal(opcode, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT32, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(0, raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(7, rawRd);
        Assert.Equal(5, rawRs1);
        Assert.Equal(6, rawRs2);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(opcode, ir.Opcode);
        Assert.Equal(InstructionClass.ScalarAlu, ir.InstructionClass);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(IrResourceClass.ScalarAlu, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.AluClass, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.ClassFlexible, ir.Annotation.BindingKind);
        Assert.Contains(ir.Annotation.Defs, operand => operand.Name == "rd" && operand.Value == 7UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 5UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs2" && operand.Value == 6UL);
        Assert.Null(ir.Annotation.MemoryReadRegion);
        Assert.Null(ir.Annotation.MemoryWriteRegion);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.FlexibleOpCount);
        Assert.Equal(0, facts.SystemSingletonCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        Assert.Equal(opcode, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(DataTypeEnum.INT32, lowered.DataTypeValue);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(7, loweredRd);
        Assert.Equal(5, loweredRs1);
        Assert.Equal(6, loweredRs2);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrier);
        Assert.Equal(opcode, (InstructionsEnum)scalar.OpCode);
        Assert.Equal(helperName, ResolveCompilerTailHelperName(opcode));
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, scalar.CanonicalDecodePublication);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { 5, 6 }, scalar.ReadRegisters);
        Assert.Equal(new[] { 7 }, scalar.WriteRegisters);
        Assert.Empty(scalar.ReadMemoryRanges);
        Assert.Empty(scalar.WriteMemoryRanges);
        Assert.True(scalar.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, scalar.AdmissionMetadata.RegisterHazardMask);
        Assert.True(scalar.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Theory]
    [MemberData(nameof(CompilerScalarWordUnaryTailContours))]
    public void AppScalarWordUnaryTail_EmitsCanonicalRuntimeClosedGate(
        InstructionsEnum opcode,
        uint expectedOpcode,
        string helperName)
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new AppAsmFacade(0, context);
        EmitScalarWordUnaryTail(facade, opcode, new AsmRegister(7), new AsmRegister(5));
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(expectedOpcode, raw.OpCode);
        Assert.Equal(opcode, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT32, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(0, raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(7, rawRd);
        Assert.Equal(5, rawRs1);
        Assert.Equal(0, rawRs2);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(opcode, ir.Opcode);
        Assert.Equal(InstructionClass.ScalarAlu, ir.InstructionClass);
        Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        Assert.Equal(IrResourceClass.ScalarAlu, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.AluClass, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.ClassFlexible, ir.Annotation.BindingKind);
        Assert.Contains(ir.Annotation.Defs, operand => operand.Name == "rd" && operand.Value == 7UL);
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 5UL);
        Assert.DoesNotContain(ir.Annotation.Uses, operand => operand.Name == "rs2");
        Assert.Null(ir.Annotation.MemoryReadRegion);
        Assert.Null(ir.Annotation.MemoryWriteRegion);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.FlexibleOpCount);
        Assert.Equal(0, facts.SystemSingletonCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(materializedSlot!.SlotIndex);
        Assert.Equal(opcode, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(DataTypeEnum.INT32, lowered.DataTypeValue);
        Assert.Equal(0, lowered.Immediate);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(7, loweredRd);
        Assert.Equal(5, loweredRs1);
        Assert.Equal(0, loweredRs2);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            compiledProgram.LoweredBundleAnnotations[0],
            materializedSlot.SlotIndex);

        ScalarALUMicroOp scalar = Assert.IsType<ScalarALUMicroOp>(carrier);
        Assert.Equal(opcode, (InstructionsEnum)scalar.OpCode);
        Assert.Equal(helperName, ResolveCompilerTailHelperName(opcode));
        Assert.Equal(InstructionClass.ScalarAlu, scalar.InstructionClass);
        Assert.Equal(SerializationClass.Free, scalar.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, scalar.CanonicalDecodePublication);
        Assert.False(scalar.UsesImmediate);
        Assert.Equal(new[] { 5 }, scalar.ReadRegisters);
        Assert.DoesNotContain(0, scalar.ReadRegisters);
        Assert.Equal(new[] { 7 }, scalar.WriteRegisters);
        Assert.Empty(scalar.ReadMemoryRanges);
        Assert.Empty(scalar.WriteMemoryRanges);
        Assert.True(scalar.ResourceMask.IsNonZero);
        Assert.NotEqual(0U, scalar.AdmissionMetadata.RegisterHazardMask);
        Assert.True(scalar.AdmissionMetadata.CertificateMask.IsNonZero);
    }

    [Fact]
    public void PlatformVSetVli_EmitsCanonicalVsetvliRuntimeClosedContour()
    {
        var context = new HybridCpuThreadCompilerContext(0);

#pragma warning disable CS0618
        var facade = new PlatformAsmFacade(0, context);
        facade.VSetVli(new AsmRegister(5), new AsmRegister(6), DataTypeEnum.INT32);
#pragma warning restore CS0618

        VLIW_Instruction raw = Assert.Single(context.GetCompiledInstructions().ToArray());
        Assert.Equal(InstructionsEnum.VSETVLI, (InstructionsEnum)raw.OpCode);
        Assert.Equal(DataTypeEnum.INT32, raw.DataTypeValue);
        Assert.Equal(0, raw.PredicateMask);
        Assert.Equal(0, raw.Immediate);
        Assert.Equal(1u, raw.StreamLength);
        Assert.Equal(0, raw.Stride);
        Assert.Equal(0UL, raw.Src2Pointer);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(raw.Word1, out byte rawRd, out byte rawRs1, out byte rawRs2));
        Assert.Equal(5, rawRd);
        Assert.Equal(6, rawRs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, rawRs2);

        VliwBundleAnnotations sourceAnnotations = context.GetBundleAnnotations();
        Assert.True(sourceAnnotations.TryGetInstructionSlotMetadata(0, out InstructionSlotMetadata sourceMetadata));
        Assert.Null(sourceMetadata.DmaStreamComputeDescriptor);
        Assert.Null(sourceMetadata.AcceleratorCommandDescriptor);
        Assert.Equal(SlotClass.SystemSingleton, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.PinningKind);
        Assert.Equal(7, sourceMetadata.SlotMetadata.AdmissionMetadata.Placement.PinnedLaneId);

        HybridCpuCompiledProgram compiledProgram = context.CompileProgram();
        IrInstruction ir = Assert.Single(compiledProgram.BundleLayout.Program.Instructions);
        Assert.Equal(InstructionsEnum.VSETVLI, ir.Opcode);
        Assert.Equal(InstructionClass.System, ir.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, ir.SerializationClass);
        Assert.Equal(IrResourceClass.System, ir.Annotation.ResourceClass);
        Assert.Equal(SlotClass.SystemSingleton, ir.Annotation.RequiredSlotClass);
        Assert.Equal(IrSlotBindingKind.HardPinned, ir.Annotation.BindingKind);
        Assert.Equal(IrIssueSlotMask.Slot7, ir.Annotation.LegalSlots);
        Assert.Null(ir.Annotation.MemoryReadRegion);
        Assert.Null(ir.Annotation.MemoryWriteRegion);
        Assert.Collection(
            ir.Annotation.Defs,
            operand =>
            {
                Assert.Equal("rd", operand.Name);
                Assert.Equal(5UL, operand.Value);
            });
        Assert.Contains(ir.Annotation.Uses, operand => operand.Name == "rs1" && operand.Value == 6UL);

        IrMaterializedBundle materializedBundle =
            Assert.Single(compiledProgram.BundleLayout.BlockResults.SelectMany(block => block.Bundles));
        Assert.True(materializedBundle.TryGetSlotForInstruction(ir.Index, out IrMaterializedBundleSlot? materializedSlot));
        Assert.NotNull(materializedSlot);
        Assert.Equal(7, materializedSlot!.SlotIndex);

        TypedSlotBundleFacts facts = HybridCpuBundleLowerer.EmitFactsForBundle(materializedBundle);
        Assert.Equal(1, facts.SystemSingletonCount);
        Assert.Equal(0, facts.BranchControlCount);
        Assert.Equal(0, facts.DmaStreamCount);
        Assert.True(compiledProgram.AdmissibilityAgreement.AllTypedSlotFactsValid);

        VLIW_Instruction lowered = compiledProgram.LoweredBundles[0].GetInstruction(7);
        Assert.Equal(InstructionsEnum.VSETVLI, (InstructionsEnum)lowered.OpCode);
        Assert.Equal(DataTypeEnum.INT32, lowered.DataTypeValue);
        Assert.Equal(0, lowered.VirtualThreadId);
        Assert.Equal(0UL, lowered.Src2Pointer);
        Assert.True(VLIW_Instruction.TryUnpackArchRegs(lowered.Word1, out byte loweredRd, out byte loweredRs1, out byte loweredRs2));
        Assert.Equal(5, loweredRd);
        Assert.Equal(6, loweredRs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, loweredRs2);

        VliwBundleAnnotations loweredAnnotations = Assert.Single(compiledProgram.LoweredBundleAnnotations);
        Assert.True(loweredAnnotations.TryGetInstructionSlotMetadata(7, out InstructionSlotMetadata loweredMetadata));
        Assert.Null(loweredMetadata.DmaStreamComputeDescriptor);
        Assert.Null(loweredMetadata.AcceleratorCommandDescriptor);

        MicroOp carrier = L7SdcCompilerEmissionTests.DecodeAndProjectSingleCarrier(
            compiledProgram.LoweredBundles[0],
            loweredAnnotations,
            slotIndex: 7);

        VConfigMicroOp vConfig = Assert.IsType<VConfigMicroOp>(carrier);
        Assert.Equal(InstructionsEnum.VSETVLI, (InstructionsEnum)vConfig.OpCode);
        Assert.Equal(VectorConfigOperationKind.Vsetvli, vConfig.OperationKind);
        Assert.Equal(InstructionClass.System, vConfig.InstructionClass);
        Assert.Equal(SerializationClass.FullSerial, vConfig.SerializationClass);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, vConfig.CanonicalDecodePublication);
        Assert.Equal(SlotClass.SystemSingleton, vConfig.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.HardPinned, vConfig.Placement.PinningKind);
        Assert.Equal(7, vConfig.Placement.PinnedLaneId);
        Assert.True(vConfig.WritesRegister);
        Assert.Equal(new[] { 6 }, vConfig.ReadRegisters);
        Assert.Equal(new[] { 5 }, vConfig.WriteRegisters);
        Assert.Empty(vConfig.ReadMemoryRanges);
        Assert.Empty(vConfig.WriteMemoryRanges);
        Assert.False(vConfig.ResourceMask.IsZero);
        Assert.False(vConfig.SafetyMask.IsZero);
    }

    [Fact]
    public void CurrentCompilerSources_DoNotScopeCompilerDeferredScalarEmission()
    {
        Assert.Empty(CompilerDeferredScalarRows);

        string compilerSource = ReadAllCompilerSource();

        foreach ((string enumName, int opcodeValue, string facadeHelperName) in CompilerDeferredScalarRows)
        {
            Assert.DoesNotContain(
                $"InstructionsEnum.{enumName}",
                compilerSource,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                $"IsaOpcodeValues.{enumName}",
                compilerSource,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                $"OpcodeValues.{enumName}",
                compilerSource,
                StringComparison.Ordinal);

            Assert.DoesNotContain(
                $"\"{enumName}\"",
                compilerSource,
                StringComparison.Ordinal);

            Assert.DoesNotMatch(@$"\b{opcodeValue}\b", compilerSource);

            Assert.DoesNotContain(
                facadeHelperName,
                typeof(IAppAsmFacade).GetMethods().Select(static method => method.Name));

            Assert.DoesNotContain(
                facadeHelperName,
                typeof(AppAsmFacade)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(static method => method.Name));
        }
    }

#pragma warning disable CS0618
    private static void EmitScalarWordBinaryTail(
        AppAsmFacade facade,
        InstructionsEnum opcode,
        AsmRegister dest,
        AsmRegister src1,
        AsmRegister src2)
    {
        switch (opcode)
        {
            case InstructionsEnum.DIVW:
                facade.DivideWord(dest, src1, src2);
                break;
            case InstructionsEnum.DIVUW:
                facade.DivideUnsignedWord(dest, src1, src2);
                break;
            case InstructionsEnum.REMW:
                facade.RemainderWord(dest, src1, src2);
                break;
            case InstructionsEnum.REMUW:
                facade.RemainderUnsignedWord(dest, src1, src2);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unsupported binary scalar tail opcode.");
        }
    }

    private static void EmitScalarWordUnaryTail(
        AppAsmFacade facade,
        InstructionsEnum opcode,
        AsmRegister dest,
        AsmRegister src)
    {
        switch (opcode)
        {
            case InstructionsEnum.SEXT_W:
                facade.SignExtendWord(dest, src);
                break;
            case InstructionsEnum.ZEXT_W:
                facade.ZeroExtendWord(dest, src);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unsupported unary scalar tail opcode.");
        }
    }
#pragma warning restore CS0618

    private static string ResolveCompilerTailHelperName(InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.DIVW => nameof(AppAsmFacade.DivideWord),
            InstructionsEnum.DIVUW => nameof(AppAsmFacade.DivideUnsignedWord),
            InstructionsEnum.REMW => nameof(AppAsmFacade.RemainderWord),
            InstructionsEnum.REMUW => nameof(AppAsmFacade.RemainderUnsignedWord),
            InstructionsEnum.SEXT_W => nameof(AppAsmFacade.SignExtendWord),
            InstructionsEnum.ZEXT_W => nameof(AppAsmFacade.ZeroExtendWord),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unsupported compiler tail opcode.")
        };
    }

    private static string ReadAllCompilerSource()
    {
        string compilerDirectory = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            "HybridCPU_Compiler");

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(compilerDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !CompatFreezeScanner.IsGeneratedPath(path))
                .Select(File.ReadAllText));
    }
}
