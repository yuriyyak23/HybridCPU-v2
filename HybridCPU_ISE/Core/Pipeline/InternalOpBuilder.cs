// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Pipeline
// V5 Phase 2: Decoder Disentangling
// ─────────────────────────────────────────────────────────────────────────────

using System;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using IsaOpcode = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcode;
using OpcodeValues = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues;

namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    /// <summary>
    /// Default <see cref="IInternalOpBuilder"/> implementation.
    /// <para>
    /// Provides the single authoritative mapping from canonical numeric opcodes to
    /// <see cref="InternalOpKind"/>.  All execution units derive their dispatch selector
    /// from this table — there must be no independent mapping tables elsewhere in the pipeline.
    /// </para>
    /// </summary>
    public sealed class InternalOpBuilder : IInternalOpBuilder
    {
        /// <inheritdoc />
        public InternalOp Build(InstructionIR instruction)
        {
            ushort opcode = instruction.CanonicalOpcode.Value;
            InternalOpKind kind = MapToKind(opcode);
            InternalOpFlags flags = DeriveFlags(opcode);

            return new InternalOp
            {
                Kind      = kind,
                Rs1       = instruction.Rs1,
                Rs2       = instruction.Rs2,
                Rd        = instruction.Rd,
                Immediate = instruction.Imm,
                Flags     = flags,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Opcode → InternalOpKind table
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Maps every canonical ISA v4 opcode to its abstract <see cref="InternalOpKind"/>.
        /// </summary>
        public static InternalOpKind MapToKind(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo(opcode);
            if (opcodeInfo.HasValue)
            {
                switch (opcodeInfo.Value.InstructionClass)
                {
                    case InstructionClass.ScalarAlu:
                        switch (opcodeInfo.Value.Mnemonic)
                        {
                            case "ADD":
                                return InternalOpKind.Add;
                            case "SUB":
                                return InternalOpKind.Sub;
                            case "MUL":
                                return InternalOpKind.Mul;
                            case "DIV":
                                return InternalOpKind.Div;
                            case "SLT":
                                return InternalOpKind.Slt;
                            case "SLTU":
                                return InternalOpKind.Sltu;
                            case "MULH":
                                return InternalOpKind.MulH;
                            case "MULHU":
                                return InternalOpKind.MulHu;
                            case "MULHSU":
                                return InternalOpKind.MulHsu;
                            case "DIVU":
                                return InternalOpKind.Divu;
                            case "REM":
                                return InternalOpKind.Rem;
                            case "REMU":
                                return InternalOpKind.Remu;
                            case "ADDI":
                                return InternalOpKind.AddI;
                            case "ANDI":
                                return InternalOpKind.AndI;
                            case "ORI":
                                return InternalOpKind.OrI;
                            case "XORI":
                                return InternalOpKind.XorI;
                            case "SLTI":
                                return InternalOpKind.SltI;
                            case "SLTIU":
                                return InternalOpKind.SltiU;
                            case "SLLI":
                                return InternalOpKind.SllI;
                            case "SRLI":
                                return InternalOpKind.SrlI;
                            case "SRAI":
                                return InternalOpKind.SraI;
                            case "LUI":
                                return InternalOpKind.Lui;
                            case "AUIPC":
                                return InternalOpKind.Auipc;
                        }
                        break;

                    case InstructionClass.ControlFlow:
                        return opcode switch
                        {
                            OpcodeValues.JAL => InternalOpKind.Jal,
                            OpcodeValues.JALR => InternalOpKind.Jalr,
                            _ => InternalOpKind.Branch,
                        };

                    case InstructionClass.Atomic:
                        InstructionFlags atomicFlags = opcodeInfo.Value.Flags;
                        var atomicInstruction = new InstructionIR
                        {
                            CanonicalOpcode = new IsaOpcode(opcode),
                            Class = InstructionClass.Atomic,
                            SerializationClass = opcodeInfo.Value.SerializationClass,
                            Rd = 0,
                            Rs1 = 0,
                            Rs2 = 0,
                            Imm = 0,
                        };
                        if (!InstructionRegistry.TryResolvePublishedAtomicAccessSize(in atomicInstruction, out byte accessSize))
                        {
                            throw new InvalidOperationException(
                                $"Published atomic opcode {opcode} did not resolve an authoritative access size.");
                        }

                        bool isDwordAtomic = accessSize > 4;
                        if (opcodeInfo.Value.OperandCount == 1 &&
                            (atomicFlags & InstructionFlags.MemoryRead) != 0 &&
                            (atomicFlags & InstructionFlags.MemoryWrite) == 0)
                        {
                            return isDwordAtomic
                                ? InternalOpKind.LrD
                                : InternalOpKind.LrW;
                        }

                        if (opcodeInfo.Value.OperandCount == 2 &&
                            (atomicFlags & InstructionFlags.MemoryRead) == 0 &&
                            (atomicFlags & InstructionFlags.MemoryWrite) != 0)
                        {
                            return isDwordAtomic
                                ? InternalOpKind.ScD
                                : InternalOpKind.ScW;
                        }

                        if ((atomicFlags & (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite)) ==
                            (InstructionFlags.MemoryRead | InstructionFlags.MemoryWrite))
                        {
                            return isDwordAtomic
                                ? InternalOpKind.AmoDword
                                : InternalOpKind.AmoWord;
                        }
                        break;

                    case InstructionClass.Memory when !opcodeInfo.Value.IsVector:
                        if ((opcodeInfo.Value.Flags & InstructionFlags.MemoryRead) != 0 &&
                            (opcodeInfo.Value.Flags & InstructionFlags.MemoryWrite) == 0)
                        {
                            return InternalOpKind.Load;
                        }

                        if ((opcodeInfo.Value.Flags & InstructionFlags.MemoryWrite) != 0)
                        {
                            return InternalOpKind.Store;
                        }
                        break;

                    case InstructionClass.System:
                        if (!opcodeInfo.Value.IsVector && opcodeInfo.Value.OperandCount > 0)
                        {
                            return InternalOpKind.Store;
                        }

                        var systemInstruction = new InstructionIR
                        {
                            CanonicalOpcode = new IsaOpcode(opcode),
                            Class = InstructionClass.System,
                            SerializationClass = opcodeInfo.Value.SerializationClass,
                            Rd = 0,
                            Rs1 = 0,
                            Rs2 = 0,
                            Imm = 0,
                        };

                        if (InstructionRegistry.TryResolvePublishedSystemEventKind(
                            in systemInstruction,
                            out SystemEventKind systemEventKind))
                        {
                            return systemEventKind switch
                            {
                                SystemEventKind.Fence => InternalOpKind.Fence,
                                SystemEventKind.FenceI => InternalOpKind.FenceI,
                                SystemEventKind.Ecall => InternalOpKind.Ecall,
                                SystemEventKind.Ebreak => InternalOpKind.Ebreak,
                                SystemEventKind.Mret => InternalOpKind.Mret,
                                SystemEventKind.Sret => InternalOpKind.Sret,
                                SystemEventKind.Wfi => InternalOpKind.Wfi,
                                _ => throw new InvalidOperationException(
                                    $"Published system opcode {opcode} did not resolve to an authoritative internal-op kind."),
                            };
                        }
                        break;

                    case InstructionClass.Csr:
                        var csrInstruction = new InstructionIR
                        {
                            CanonicalOpcode = new IsaOpcode(opcode),
                            Class = InstructionClass.Csr,
                            SerializationClass = opcodeInfo.Value.SerializationClass,
                            Rd = opcodeInfo.Value.OperandCount == 0 ? (byte)0 : (byte)1,
                            Rs1 = 0,
                            Rs2 = 0,
                            Imm = 0,
                        };

                        if (InstructionRegistry.TryCreatePublishedCsrMicroOp(in csrInstruction, out CSRMicroOp? csrMicroOp) &&
                            csrMicroOp is not null)
                        {
                            if (csrMicroOp is CsrReadWriteMicroOp or CsrReadWriteImmediateMicroOp)
                            {
                                return InternalOpKind.CsrReadWrite;
                            }

                            if (csrMicroOp is CsrReadSetMicroOp or CsrReadSetImmediateMicroOp)
                            {
                                return InternalOpKind.CsrReadSet;
                            }

                            if (csrMicroOp is CsrReadClearMicroOp or CsrReadClearImmediateMicroOp)
                            {
                                return InternalOpKind.CsrReadClear;
                            }

                            if (csrMicroOp is CsrClearMicroOp)
                            {
                                return InternalOpKind.CsrClear;
                            }
                        }
                        break;

                    case InstructionClass.SmtVt:
                        var smtVtInstruction = new InstructionIR
                        {
                            CanonicalOpcode = new IsaOpcode(opcode),
                            Class = InstructionClass.SmtVt,
                            SerializationClass = opcodeInfo.Value.SerializationClass,
                            Rd = 0,
                            Rs1 = 0,
                            Rs2 = 0,
                            Imm = 0,
                        };

                        if (InstructionRegistry.TryResolvePublishedSystemEventKind(
                            in smtVtInstruction,
                            out SystemEventKind smtVtEventKind))
                        {
                            return smtVtEventKind switch
                            {
                                SystemEventKind.Wfe => InternalOpKind.Wfe,
                                SystemEventKind.Sev => InternalOpKind.Sev,
                                SystemEventKind.PodBarrier => InternalOpKind.PodBarrier,
                                SystemEventKind.VtBarrier => InternalOpKind.VtBarrier,
                                _ => InternalOpKind.Yield,
                            };
                        }

                        if ((opcodeInfo.Value.Flags & InstructionFlags.Privileged) == 0)
                        {
                            return InternalOpKind.Yield;
                        }
                        break;

                    case InstructionClass.Vmx:
                        var vmxInstruction = new InstructionIR
                        {
                            CanonicalOpcode = new IsaOpcode(opcode),
                            Class = InstructionClass.Vmx,
                            SerializationClass = opcodeInfo.Value.SerializationClass,
                            Rd = 0,
                            Rs1 = 0,
                            Rs2 = 0,
                            Imm = 0,
                        };

                        if (InstructionRegistry.TryResolvePublishedVmxOperationKind(in vmxInstruction, out VmxOperationKind operationKind))
                        {
                            return operationKind switch
                            {
                                VmxOperationKind.VmxOn => InternalOpKind.VmxOn,
                                VmxOperationKind.VmxOff => InternalOpKind.VmxOff,
                                VmxOperationKind.VmLaunch => InternalOpKind.VmLaunch,
                                VmxOperationKind.VmResume => InternalOpKind.VmResume,
                                VmxOperationKind.VmRead => InternalOpKind.VmRead,
                                VmxOperationKind.VmWrite => InternalOpKind.VmWrite,
                                VmxOperationKind.VmClear => InternalOpKind.VmClear,
                                VmxOperationKind.VmPtrLd => InternalOpKind.VmPtrLd,
                                _ => throw new InvalidOperationException(
                                    $"Published VMX opcode {opcode} did not resolve to an authoritative internal-op kind."),
                            };
                        }
                        break;
                }
            }

            return opcode switch
            {
                // ── Scalar ALU reg-reg ────────────────────────────────────────
                OpcodeValues.Modulus       => InternalOpKind.Rem,
                OpcodeValues.ShiftLeft     => InternalOpKind.Sll,
                OpcodeValues.ShiftRight    => InternalOpKind.Srl,
                OpcodeValues.XOR           => InternalOpKind.Xor,
                OpcodeValues.OR            => InternalOpKind.Or,
                OpcodeValues.AND           => InternalOpKind.And,

                // ── Scalar M-extension ────────────────────────────────────────

                // ── Scalar ALU immediate ──────────────────────────────────────

                // ── Scalar Memory ─────────────────────────────────────────────
                OpcodeValues.Store         => InternalOpKind.Store,
                OpcodeValues.Load          => InternalOpKind.Load,
                OpcodeValues.Move          => InternalOpKind.AddI, // mv rd, rs1 ≡ addi rd, rs1, 0
                OpcodeValues.Move_Num      => InternalOpKind.Lui,  // mvi ≡ lui + optional addi

                // ── Control Flow ──────────────────────────────────────────────
                OpcodeValues.JumpIfNotEqual       => InternalOpKind.Branch,
                OpcodeValues.JumpIfBelow          => InternalOpKind.Branch,
                OpcodeValues.JumpIfBelowOrEqual   => InternalOpKind.Branch,
                OpcodeValues.JumpIfAbove          => InternalOpKind.Branch,
                OpcodeValues.JumpIfAboveOrEqual   => InternalOpKind.Branch,

                // ── Atomics (word) ────────────────────────────────────────────

                // ── Atomics (doubleword) ──────────────────────────────────────

                // ── System / privilege ────────────────────────────────────────
                OpcodeValues.Interrupt     => InternalOpKind.Interrupt,
                OpcodeValues.InterruptReturn => InternalOpKind.InterruptReturn,

                // ── CSR ───────────────────────────────────────────────────────

                // ── SMT/VT ────────────────────────────────────────────────────

                // ── VMX ───────────────────────────────────────────────────────

                // ── Nop ───────────────────────────────────────────────────────
                OpcodeValues.Nope          => InternalOpKind.AddI, // NOP ≡ ADDI x0, x0, 0

                _ => throw new ArgumentOutOfRangeException(nameof(opcode),
                         $"No InternalOpKind mapping for opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)} ({opcode}). " +
                         "Vector opcodes are not mapped through InternalOpBuilder — they use the vector execution path."),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static InternalOpFlags DeriveFlags(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo(opcode);
            InternalOpKind kind = MapToKind(opcode);

            if (kind is InternalOpKind.Div or
                InternalOpKind.Rem or
                InternalOpKind.Slt or
                InternalOpKind.SltI)
            {
                return InternalOpFlags.Signed;
            }

            if (kind == InternalOpKind.SraI)
            {
                return InternalOpFlags.ArithmeticShift;
            }

            if (opcodeInfo.HasValue)
            {
                string mnemonic = opcodeInfo.Value.Mnemonic;
                if (opcodeInfo.Value.InstructionClass == InstructionClass.ControlFlow &&
                    mnemonic is "BLT" or "BGE")
                {
                    return InternalOpFlags.Signed;
                }
            }

            return opcode switch
            {
                OpcodeValues.Modulus or
                OpcodeValues.JumpIfBelow or
                OpcodeValues.JumpIfBelowOrEqual or
                OpcodeValues.JumpIfAbove or
                OpcodeValues.JumpIfAboveOrEqual => InternalOpFlags.Signed,
                _ => InternalOpFlags.None,
            };
        }
    }
}
