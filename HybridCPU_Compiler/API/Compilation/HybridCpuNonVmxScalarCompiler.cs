using System;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;
using HybridCPU.Compiler.Core.IR.Lowering;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core;

public sealed record CompilerNonVmxScalarEmissionPlan(
    string Mnemonic,
    InstructionsEnum Opcode,
    byte DestinationRegister,
    byte SourceRegister1,
    byte SourceRegister2,
    ushort Immediate,
    DataTypeEnum DataType,
    CompilerNonVmxScalarFeature? RequiredFeature,
    uint StreamLength,
    ushort Stride);

/// <summary>
/// Exact, typed Non-VMX scalar carrier producer. Each public method fixes the
/// opcode and operand ABI. It cannot emit outside its closed scalar table and
/// grants no runtime authority.
/// </summary>
public sealed class HybridCpuNonVmxScalarCompiler
{
    private const string SourceApi = "HybridCpuNonVmxScalarCompiler";

    private readonly HybridCpuThreadCompilerContext _context;
    private readonly CompilerNonVmxScalarCapabilityModel _capabilities;

    public HybridCpuNonVmxScalarCompiler(
        HybridCpuThreadCompilerContext context,
        CompilerNonVmxScalarCapabilityModel? capabilities = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _capabilities = capabilities ?? CompilerNonVmxScalarCapabilityModel.Default;
    }

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> SetBitRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("BSET", InstructionsEnum.BSET, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ClearBitRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("BCLR", InstructionsEnum.BCLR, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> InvertBitRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("BINV", InstructionsEnum.BINV, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ExtractBitRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("BEXT", InstructionsEnum.BEXT, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> SetBitImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("BSETI", InstructionsEnum.BSETI, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, immediate);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ClearBitImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("BCLRI", InstructionsEnum.BCLRI, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, immediate);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> InvertBitImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("BINVI", InstructionsEnum.BINVI, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, immediate);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ExtractBitImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("BEXTI", InstructionsEnum.BEXTI, CompilerNonVmxScalarFeature.ScalarBitfield, rd, rs1, immediate);

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> AndWithInvertedSecond(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("ANDN", InstructionsEnum.ANDN, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> OrWithInvertedSecond(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("ORN", InstructionsEnum.ORN, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ExclusiveNor(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("XNOR", InstructionsEnum.XNOR, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ScalarMinSigned(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("MIN", InstructionsEnum.MIN, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ScalarMaxSigned(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("MAX", InstructionsEnum.MAX, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ScalarMinUnsigned(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("MINU", InstructionsEnum.MINU, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ScalarMaxUnsigned(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("MAXU", InstructionsEnum.MAXU, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> BinaryPolynomialProductLow(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("CLMUL", InstructionsEnum.CLMUL, CompilerNonVmxScalarFeature.ScalarCarryLessChecksum, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> BinaryPolynomialProductHigh(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("CLMULH", InstructionsEnum.CLMULH, CompilerNonVmxScalarFeature.ScalarCarryLessChecksum, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> BinaryPolynomialProductReverse(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("CLMULR", InstructionsEnum.CLMULR, CompilerNonVmxScalarFeature.ScalarCarryLessChecksum, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ZeroIfConditionEqualZero(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("CZERO.EQZ", InstructionsEnum.CZERO_EQZ, CompilerNonVmxScalarFeature.ScalarSelectCzero, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ZeroIfConditionNotEqualZero(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("CZERO.NEZ", InstructionsEnum.CZERO_NEZ, CompilerNonVmxScalarFeature.ScalarSelectCzero, rd, rs1, rs2);

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> AddUnsignedWord(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("ADD.UW", InstructionsEnum.ADD_UW, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftOneAndAdd(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH1ADD", InstructionsEnum.SH1ADD, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftTwoAndAdd(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH2ADD", InstructionsEnum.SH2ADD, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftThreeAndAdd(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH3ADD", InstructionsEnum.SH3ADD, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftOneAndAddUnsignedWord(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH1ADD.UW", InstructionsEnum.SH1ADD_UW, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftTwoAndAddUnsignedWord(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH2ADD.UW", InstructionsEnum.SH2ADD_UW, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftThreeAndAddUnsignedWord(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("SH3ADD.UW", InstructionsEnum.SH3ADD_UW, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ShiftLeftUnsignedWordByImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("SLLI.UW", InstructionsEnum.SLLI_UW, CompilerNonVmxScalarFeature.ScalarAddressGeneration, rd, rs1, immediate);

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> RotateLeftRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("ROL", InstructionsEnum.ROL, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> RotateRightRegister(AsmRegister rd, AsmRegister rs1, AsmRegister rs2) => Binary("ROR", InstructionsEnum.ROR, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, rs2);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> RotateLeftByImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("ROLI", InstructionsEnum.ROLI, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, immediate);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> RotateRightByImmediate(AsmRegister rd, AsmRegister rs1, int immediate) => Immediate6("RORI", InstructionsEnum.RORI, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, immediate);

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> CountLeadingZeros(AsmRegister rd, AsmRegister rs1) => Unary("CLZ", InstructionsEnum.CLZ, null, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> CountTrailingZeros(AsmRegister rd, AsmRegister rs1) => Unary("CTZ", InstructionsEnum.CTZ, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> CountSetBits(AsmRegister rd, AsmRegister rs1) => Unary("CPOP", InstructionsEnum.CPOP, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ReverseByteOrder(AsmRegister rd, AsmRegister rs1) => Unary("REV8", InstructionsEnum.REV8, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ReverseBitsInEachByte(AsmRegister rd, AsmRegister rs1) => Unary("BREV8", InstructionsEnum.BREV8, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> SignExtendByte(AsmRegister rd, AsmRegister rs1) => Unary("SEXT.B", InstructionsEnum.SEXT_B, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> SignExtendHalf(AsmRegister rd, AsmRegister rs1) => Unary("SEXT.H", InstructionsEnum.SEXT_H, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ZeroExtendHalf(AsmRegister rd, AsmRegister rs1) => Unary("ZEXT.H", InstructionsEnum.ZEXT_H, CompilerNonVmxScalarFeature.ScalarBitmanipCore, rd, rs1, DataTypeEnum.UINT64);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> SignExtendWord(AsmRegister rd, AsmRegister rs1) => Unary("SEXT.W", InstructionsEnum.SEXT_W, null, rd, rs1, DataTypeEnum.INT32, 1, 8);
    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ZeroExtendWord(AsmRegister rd, AsmRegister rs1) => Unary("ZEXT.W", InstructionsEnum.ZEXT_W, null, rd, rs1, DataTypeEnum.INT32, 1, 8);

    public CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> ReadSystemCycleCounter(AsmRegister rd) =>
        Emit("RDCYCLE", InstructionsEnum.RDCYCLE, CompilerNonVmxScalarFeature.ScalarSystemCounter, rd, default, default, 0, DataTypeEnum.UINT64);

    private CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> Binary(
        string mnemonic,
        InstructionsEnum opcode,
        CompilerNonVmxScalarFeature? feature,
        AsmRegister rd,
        AsmRegister rs1,
        AsmRegister rs2) =>
        Emit(mnemonic, opcode, feature, rd, rs1, rs2, 0, DataTypeEnum.UINT64);

    private CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> Unary(
        string mnemonic,
        InstructionsEnum opcode,
        CompilerNonVmxScalarFeature? feature,
        AsmRegister rd,
        AsmRegister rs1,
        DataTypeEnum dataType,
        uint streamLength = 0,
        ushort stride = 0) =>
        Emit(mnemonic, opcode, feature, rd, rs1, default, 0, dataType, streamLength, stride);

    private CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> Immediate6(
        string mnemonic,
        InstructionsEnum opcode,
        CompilerNonVmxScalarFeature feature,
        AsmRegister rd,
        AsmRegister rs1,
        int immediate6)
    {
        if ((uint)immediate6 > 0x3Fu)
        {
            throw new ArgumentOutOfRangeException(
                nameof(immediate6),
                immediate6,
                $"{mnemonic} immediate must fit imm6 [0, 63].");
        }

        return Emit(
            mnemonic,
            opcode,
            feature,
            rd,
            rs1,
            default,
            (ushort)immediate6,
            DataTypeEnum.UINT64);
    }

    private CompilerPositiveEmissionResult<CompilerNonVmxScalarEmissionPlan> Emit(
        string mnemonic,
        InstructionsEnum opcode,
        CompilerNonVmxScalarFeature? feature,
        AsmRegister rd,
        AsmRegister rs1,
        AsmRegister rs2,
        ushort immediate,
        DataTypeEnum dataType,
        uint streamLength = 0,
        ushort stride = 0)
    {
        if (feature is CompilerNonVmxScalarFeature requiredFeature)
        {
            _capabilities.Require(requiredFeature, mnemonic);
        }

        byte destination = rd.ArchRegisterId.Value;
        byte source1 = rs1.ArchRegisterId.Value;
        byte source2 = rs2.ArchRegisterId.Value;
        _context.CompileInstruction(
            (uint)opcode,
            (byte)dataType,
            0,
            immediate,
            VLIW_Instruction.PackArchRegs(destination, source1, source2),
            0,
            streamLength,
            stride,
            stealabilityPolicy: StealabilityPolicy.NotStealable);

        var plan = new CompilerNonVmxScalarEmissionPlan(
            mnemonic,
            opcode,
            destination,
            source1,
            source2,
            immediate,
            dataType,
            feature,
            streamLength,
            stride);
        string reason =
            $"{mnemonic} exact Non-VMX scalar carrier produced; runtime legality, execution, publication, commit and retire remain independently required.";
        CompilerLoweringDecision decision = CompilerLoweringDecision.FromTypedCarrierEmission(
            $"{SourceApi}.{mnemonic}",
            SemanticIntentKind.ScalarAlu,
            ExecutionContourKind.NativeVliwScalar,
            $"typed-non-vmx-scalar:{mnemonic}:no-fallback",
            reason);
        return new(decision, plan, SourceApi, reason);
    }
}
