using System.ComponentModel;

namespace HybridCPU.Compiler.Core.API.Facade;

/// <summary>
/// Compatibility application-level assembly facade.
/// Prefers canonical compiler emission for supported scalar and memory paths.
/// The scoped control-flow surface uses compiler-native symbolic targets and canonical branch relocation.
/// </summary>
[Obsolete(AsmFacadeDeprecation.Message)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAppAsmFacade
{
    // ── Arithmetic ──
    void Add(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Sub(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Mul(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void MultiplyWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Div(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void DivideWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void DivideUnsignedWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Mod(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void RemainderWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void RemainderUnsignedWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void SignExtendWord(AsmRegister dest, AsmRegister src);
    void ZeroExtendWord(AsmRegister dest, AsmRegister src);
    void Sqrt(AsmRegister dest, AsmRegister src);
    void Fmac(AsmRegister acc, AsmRegister src1, AsmRegister src2);
    void Inc(AsmRegister reg);
    void Dec(AsmRegister reg);
    void AddWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void AddWordImmediate(AsmRegister dest, AsmRegister src, int immediate);
    void SubWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void ShiftLeft(AsmRegister dest, AsmRegister src, int amount);
    void ShiftLeftWordImmediate(AsmRegister dest, AsmRegister src, int amount);
    void ShiftRight(AsmRegister dest, AsmRegister src, int amount);
    void ShiftRightLogicalWordImmediate(AsmRegister dest, AsmRegister src, int amount);
    void ShiftRightArithmeticWordImmediate(AsmRegister dest, AsmRegister src, int amount);
    void ShiftRightArithmetic(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void ShiftLeftWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void ShiftRightLogicalWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void ShiftRightArithmeticWord(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Xor(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Or(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void And(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Not(AsmRegister dest, AsmRegister src);
    void Nop();
    void LoadImm(AsmRegister dest, ulong value);

    // ── Memory ──
    void Load(ulong address, AsmRegister dest);
    void Store(AsmRegister src, ulong address);
    void Move(AsmRegister src, AsmRegister dest);

    // ── Control flow ──
    AsmControlTarget DefineEntryPoint(string name);
    void MarkEntryPoint(AsmControlTarget ep);
    void Jump(AsmControlTarget target);
    void JumpIfNotEqual(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint);
    void JumpIfBelow(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint);
    void JumpIfAbove(AsmControlTarget target, AsmRegister acc, AsmRegister op1, AsmRegister op2, int hint);
    void Call(AsmControlTarget target, AsmRegister acc, int hint);
    void Return(AsmControlTarget target, AsmRegister acc);

    // ── Parallel hints ──

    /// <summary>
    /// Hint: parallel-for decomposition over [start, end) with the given step.
    /// Compiler may decompose into coordinator + workers. Falls back to sequential if decomposition fails.
    /// </summary>
    void ParallelFor(AsmRegister start, AsmRegister end, AsmRegister step, Action body);

    /// <summary>
    /// Hint: reduction over [start, end) using the specified associative opcode.
    /// Compiler may decompose with worker-local accumulators and coordinator merge. Falls back to sequential if decomposition fails.
    /// </summary>
    void Reduce(AsmRegister accumulator, AsmRegister start, AsmRegister end, Action body);

    // ── Lifecycle ──
    void Init(AsmRegister acc);
}
