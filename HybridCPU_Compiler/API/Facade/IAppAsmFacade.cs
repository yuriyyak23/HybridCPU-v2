using System.ComponentModel;

namespace HybridCPU.Compiler.Core.API.Facade;

/// <summary>
/// Compatibility application-level assembly facade.
/// Prefers canonical compiler emission for supported scalar and memory paths.
/// The control-flow surface uses compiler-native symbolic targets; control-transfer wrappers remain fail-closed until branch lowering is wired end-to-end.
/// </summary>
[Obsolete(AsmFacadeDeprecation.Message)]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IAppAsmFacade
{
    // ── Arithmetic ──
    void Add(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Sub(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Mul(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Div(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Mod(AsmRegister dest, AsmRegister src1, AsmRegister src2);
    void Sqrt(AsmRegister dest, AsmRegister src);
    void Fmac(AsmRegister acc, AsmRegister src1, AsmRegister src2);
    void Inc(AsmRegister reg);
    void Dec(AsmRegister reg);
    void ShiftLeft(AsmRegister dest, AsmRegister src, int amount);
    void ShiftRight(AsmRegister dest, AsmRegister src, int amount);
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
