using System;
using System.ComponentModel;
using HybridCPU.Compiler.Core.IR;
using YAKSys_Hybrid_CPU;
using HybridCPU.Compiler.Core.Threading;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using HybridCPU_ISE.Arch;

namespace HybridCPU.Compiler.Core.API.Facade;

/// <summary>
/// Compatibility application-level assembly facade implementation.
/// Uses canonical compiler emission where phase-02 flat architectural register flow is already available
/// and keeps unsupported control-transfer wrappers fail-closed until canonical branch lowering is wired end-to-end.
/// </summary>
[Obsolete(AsmFacadeDeprecation.Message)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class AppAsmFacade : IAppAsmFacade
{
    private readonly int _coreId;
    private readonly HybridCpuThreadCompilerContext _context;
    private const byte DefaultScalarPredicateMask = 0xFF;
    private const byte ScalarDataType = 0;
    private const byte ScalarPredicateMask = 0;
    private const ulong ScalarStreamLength = 1;
    private const ushort ScalarStride = 8;

    /// <summary>
    /// Creates an AppAsmFacade bound to a specific core and thread compiler context.
    /// </summary>
    public AppAsmFacade(int coreId, HybridCpuThreadCompilerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _coreId = coreId;
        _context = context;
    }

    /// <summary>Resolves facade register to flat architectural register identity.</summary>
    protected static ArchRegId Resolve(AsmRegister reg) => reg.ArchRegisterId;

    /// <summary>Provides access to the core reference for derived facades.</summary>
    protected ref Processor.CPU_Core Core => ref Processor.CPU_Cores[_coreId];

    /// <summary>Provides access to the thread compiler context for derived facades.</summary>
    protected HybridCpuThreadCompilerContext Context => _context;

    /// <summary>Provides the core ID for derived facades.</summary>
    protected int CoreId => _coreId;

    /// <summary>Emits a scalar immediate instruction using packed register operands.</summary>
    protected void EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum opcode, AsmRegister dest, AsmRegister src, short immediate)
    {
        Context.CompileInstruction(
            (uint)opcode,
            ScalarDataType,
            ScalarPredicateMask,
            unchecked((ushort)immediate),
            VLIW_Instruction.PackArchRegs(
                Resolve(dest).Value,
                Resolve(src).Value,
                VLIW_Instruction.NoArchReg),
            0,
            ScalarStreamLength,
            ScalarStride,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    /// <summary>Emits a canonical scalar register-register instruction using packed architectural registers.</summary>
    protected void EmitScalarBinary(Processor.CPU_Core.InstructionsEnum opcode, AsmRegister dest, AsmRegister src1, AsmRegister src2)
    {
        Context.CompileInstruction(
            (uint)opcode,
            ScalarDataType,
            ScalarPredicateMask,
            0,
            VLIW_Instruction.PackArchRegs(
                Resolve(dest).Value,
                Resolve(src1).Value,
                Resolve(src2).Value),
            0,
            ScalarStreamLength,
            ScalarStride,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    /// <summary>Emits a canonical scalar register-only instruction using packed architectural registers.</summary>
    protected void EmitScalarUnary(Processor.CPU_Core.InstructionsEnum opcode, AsmRegister dest, AsmRegister src)
    {
        Context.CompileInstruction(
            (uint)opcode,
            ScalarDataType,
            ScalarPredicateMask,
            0,
            VLIW_Instruction.PackArchRegs(
                Resolve(dest).Value,
                Resolve(src).Value,
                VLIW_Instruction.NoArchReg),
            0,
            ScalarStreamLength,
            ScalarStride,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    protected static InvalidOperationException CreateUnsupportedControlTransferException(string helperName)
    {
        return new InvalidOperationException(
            $"{helperName} already uses the compiler-native control-target contract, but facade-level control-transfer lowering is not wired through canonical branch emission/relocation in HybridCPU_Compiler yet. " +
            "Use thread-compiler metadata APIs or explicit canonical control-flow emission instead.");
    }

    protected static IrEntryPointKind Resolve(AsmControlTarget target)
    {
        return target.Kind switch
        {
            AsmControlTargetKind.ProgramEntry => IrEntryPointKind.ProgramEntry,
            AsmControlTargetKind.EntryPoint => IrEntryPointKind.EntryPoint,
            AsmControlTargetKind.CallTarget => IrEntryPointKind.CallTarget,
            AsmControlTargetKind.InterruptHandler => IrEntryPointKind.InterruptHandler,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unknown control-target kind.")
        };
    }

    protected static void Validate(AsmControlTarget target, string parameterName)
    {
        if (!target.IsDefined)
        {
            throw new ArgumentException("Control target must have a non-empty symbolic name.", parameterName);
        }
    }

    // ── Arithmetic ──

    public void Add(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.Addition, dest, src1, src2);

    public void Sub(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.Subtraction, dest, src1, src2);

    public void Mul(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.Multiplication, dest, src1, src2);

    public void Div(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.Division, dest, src1, src2);

    public void Mod(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.Modulus, dest, src1, src2);

    public void Sqrt(AsmRegister dest, AsmRegister src) =>
        Core.SquareRoot(Resolve(dest), Resolve(src));

    public void Fmac(AsmRegister acc, AsmRegister src1, AsmRegister src2) =>
        Core.FMAC(Resolve(acc), Resolve(src1), Resolve(src2));

    public void Inc(AsmRegister reg) =>
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.ADDI, reg, reg, 1);

    public void Dec(AsmRegister reg) =>
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.ADDI, reg, reg, -1);

    public void ShiftLeft(AsmRegister dest, AsmRegister src, int amount) =>
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.ShiftLeft, dest, src, checked((short)amount));

    public void ShiftRight(AsmRegister dest, AsmRegister src, int amount) =>
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.ShiftRight, dest, src, checked((short)amount));

    public void Xor(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.XOR, dest, src1, src2);

    public void Or(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.OR, dest, src1, src2);

    public void And(AsmRegister dest, AsmRegister src1, AsmRegister src2) =>
        EmitScalarBinary(Processor.CPU_Core.InstructionsEnum.AND, dest, src1, src2);

    public void Not(AsmRegister dest, AsmRegister src) =>
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.XORI, dest, src, -1);

    public void Nop()
    {
        Context.CompileInstruction(
            (uint)Processor.CPU_Core.InstructionsEnum.Nope,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    public void LoadImm(AsmRegister dest, ulong value)
    {
        Context.CompileInstruction(
            (uint)Processor.CPU_Core.InstructionsEnum.Move_Num,
            (byte)DataTypeEnum.INT64,
            DefaultScalarPredicateMask,
            unchecked((ushort)(value & 0xFFFF)),
            VLIW_Instruction.PackArchRegs(
                Resolve(dest).Value,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            value,
            0,
            0,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    // ── Memory ──

    public void Load(ulong address, AsmRegister dest)
    {
        Context.CompileInstruction(
            (uint)Processor.CPU_Core.InstructionsEnum.LD,
            (byte)DataTypeEnum.INT64,
            DefaultScalarPredicateMask,
            0,
            VLIW_Instruction.PackArchRegs(
                Resolve(dest).Value,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
            address,
            0,
            0,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    public void Store(AsmRegister src, ulong address)
    {
        Context.CompileInstruction(
            (uint)Processor.CPU_Core.InstructionsEnum.SD,
            (byte)DataTypeEnum.INT64,
            DefaultScalarPredicateMask,
            0,
            VLIW_Instruction.PackArchRegs(
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg,
                Resolve(src).Value),
            address,
            0,
            0,
            stealabilityPolicy: StealabilityPolicy.NotStealable);
    }

    public void Move(AsmRegister src, AsmRegister dest)
    {
        EmitScalarImmediate(Processor.CPU_Core.InstructionsEnum.ADDI, dest, src, 0);
    }

    // ── Control flow ──

    public AsmControlTarget DefineEntryPoint(string name)
    {
        return new AsmControlTarget(name);
    }

    public void MarkEntryPoint(AsmControlTarget ep)
    {
        Validate(ep, nameof(ep));
        Context.DeclareLabelAtCurrentPosition(ep.Name);
        Context.DeclareEntryPointAtCurrentPosition(ep.Name, Resolve(ep));
    }

    public void Jump(AsmControlTarget target) =>
        throw CreateUnsupportedControlTransferException(nameof(Jump));

    public void JumpIfNotEqual(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint) =>
        throw CreateUnsupportedControlTransferException(nameof(JumpIfNotEqual));

    public void JumpIfBelow(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint) =>
        throw CreateUnsupportedControlTransferException(nameof(JumpIfBelow));

    public void JumpIfAbove(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint) =>
        throw CreateUnsupportedControlTransferException(nameof(JumpIfAbove));

    public void Call(AsmControlTarget target, AsmRegister acc, int hint) =>
        throw CreateUnsupportedControlTransferException(nameof(Call));

    public void Return(AsmControlTarget target, AsmRegister acc) =>
        throw CreateUnsupportedControlTransferException(nameof(Return));

    // ── Parallel hints ──

    /// <summary>
    /// Hint: parallel-for decomposition. Compiler silently falls back to sequential if decomposition fails.
    /// </summary>
    public void ParallelFor(AsmRegister start, AsmRegister end, AsmRegister step, Action body)
    {
        // Phase 07: invoke body sequentially as fallback.
        // Full decomposition occurs at IR level via ParallelForCompiler when invoked through expert path.
        body();
    }

    /// <summary>
    /// Hint: reduction. Compiler silently falls back to sequential if decomposition fails.
    /// </summary>
    public void Reduce(AsmRegister accumulator, AsmRegister start, AsmRegister end, Action body)
    {
        // Phase 07: invoke body sequentially as fallback.
        body();
    }

    // ── Lifecycle ──

    public void Init(AsmRegister acc)
    {
    }
}

