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
            flags |= DeriveAtomicOrderingFlags(kind, instruction);

            return new InternalOp
            {
                Kind = kind,
                Rs1 = instruction.Rs1,
                Rs2 = instruction.Rs2,
                Rd = instruction.Rd,
                Immediate = instruction.Imm,
                DataType = ResolveDataType(opcode, instruction),
                CsrTarget = ResolveCsrTarget(opcode, instruction),
                Flags = flags,
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
                            case "ADDW":
                                return InternalOpKind.AddW;
                            case "SUBW":
                                return InternalOpKind.SubW;
                            case "SLLW":
                                return InternalOpKind.SllW;
                            case "SRLW":
                                return InternalOpKind.SrlW;
                            case "SRAW":
                                return InternalOpKind.SraW;
                            case "SUB":
                                return InternalOpKind.Sub;
                            case "MUL":
                                return InternalOpKind.Mul;
                            case "MULW":
                                return InternalOpKind.MulW;
                            case "DIVW":
                                return InternalOpKind.DivW;
                            case "DIVUW":
                                return InternalOpKind.DivuW;
                            case "REMW":
                                return InternalOpKind.RemW;
                            case "REMUW":
                                return InternalOpKind.RemuW;
                            case "SEXT.W":
                                return InternalOpKind.SextW;
                            case "ZEXT.W":
                                return InternalOpKind.ZextW;
                            case "CZERO.EQZ":
                                return InternalOpKind.CzeroEqz;
                            case "CZERO.NEZ":
                                return InternalOpKind.CzeroNez;
                            case "CLZ":
                                return InternalOpKind.Clz;
                            case "CTZ":
                                return InternalOpKind.Ctz;
                            case "CPOP":
                                return InternalOpKind.Cpop;
                            case "SEXT.B":
                                return InternalOpKind.SextB;
                            case "SEXT.H":
                                return InternalOpKind.SextH;
                            case "ZEXT.H":
                                return InternalOpKind.ZextH;
                            case "ROL":
                                return InternalOpKind.Rol;
                            case "ROR":
                                return InternalOpKind.Ror;
                            case "ROLI":
                                return InternalOpKind.RolI;
                            case "RORI":
                                return InternalOpKind.RorI;
                            case "BSET":
                                return InternalOpKind.Bset;
                            case "BCLR":
                                return InternalOpKind.Bclr;
                            case "BINV":
                                return InternalOpKind.Binv;
                            case "BEXT":
                                return InternalOpKind.Bext;
                            case "BSETI":
                                return InternalOpKind.BsetI;
                            case "BCLRI":
                                return InternalOpKind.BclrI;
                            case "BINVI":
                                return InternalOpKind.BinvI;
                            case "BEXTI":
                                return InternalOpKind.BextI;
                            case "ANDN":
                                return InternalOpKind.AndN;
                            case "ORN":
                                return InternalOpKind.OrN;
                            case "XNOR":
                                return InternalOpKind.Xnor;
                            case "MIN":
                                return InternalOpKind.Min;
                            case "MAX":
                                return InternalOpKind.Max;
                            case "MINU":
                                return InternalOpKind.MinU;
                            case "MAXU":
                                return InternalOpKind.MaxU;
                            case "REV8":
                                return InternalOpKind.Rev8;
                            case "BREV8":
                                return InternalOpKind.Brev8;
                            case "SH1ADD":
                                return InternalOpKind.Sh1Add;
                            case "SH2ADD":
                                return InternalOpKind.Sh2Add;
                            case "SH3ADD":
                                return InternalOpKind.Sh3Add;
                            case "ADD.UW":
                                return InternalOpKind.AddUw;
                            case "SH1ADD.UW":
                                return InternalOpKind.Sh1AddUw;
                            case "SH2ADD.UW":
                                return InternalOpKind.Sh2AddUw;
                            case "SH3ADD.UW":
                                return InternalOpKind.Sh3AddUw;
                            case "SLLI.UW":
                                return InternalOpKind.SlliUw;
                            case "CLMUL":
                                return InternalOpKind.ClMul;
                            case "CLMULH":
                                return InternalOpKind.ClMulH;
                            case "CLMULR":
                                return InternalOpKind.ClMulR;
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
                            case "ADDIW":
                                return InternalOpKind.AddIW;
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
                            case "SLLIW":
                                return InternalOpKind.SllIW;
                            case "SRLIW":
                                return InternalOpKind.SrlIW;
                            case "SRAIW":
                                return InternalOpKind.SraIW;
                            case "SRLI":
                                return InternalOpKind.SrlI;
                            case "SRA":
                                return InternalOpKind.Sra;
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

                            if (csrMicroOp is CsrReadCounterMicroOp)
                            {
                                return InternalOpKind.CsrReadCounter;
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
                                VmxOperationKind.VmPtrSt => InternalOpKind.VmPtrSt,
                                VmxOperationKind.VmCall => InternalOpKind.VmCall,
                                VmxOperationKind.Invept => InternalOpKind.Invept,
                                VmxOperationKind.Invvpid => InternalOpKind.Invvpid,
                                VmxOperationKind.VmFunc => InternalOpKind.VmFunc,
                                VmxOperationKind.VmSaveX => InternalOpKind.VmSaveX,
                                VmxOperationKind.VmRestX => InternalOpKind.VmRestX,
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
                OpcodeValues.ADDW => InternalOpKind.AddW,
                OpcodeValues.SUBW => InternalOpKind.SubW,
                OpcodeValues.SLLW => InternalOpKind.SllW,
                OpcodeValues.SRLW => InternalOpKind.SrlW,
                OpcodeValues.SRAW => InternalOpKind.SraW,
                OpcodeValues.MULW => InternalOpKind.MulW,
                OpcodeValues.DIVW => InternalOpKind.DivW,
                OpcodeValues.DIVUW => InternalOpKind.DivuW,
                OpcodeValues.REMW => InternalOpKind.RemW,
                OpcodeValues.REMUW => InternalOpKind.RemuW,
                OpcodeValues.SEXT_W => InternalOpKind.SextW,
                OpcodeValues.ZEXT_W => InternalOpKind.ZextW,
                OpcodeValues.CZERO_EQZ => InternalOpKind.CzeroEqz,
                OpcodeValues.CZERO_NEZ => InternalOpKind.CzeroNez,
                OpcodeValues.CLZ => InternalOpKind.Clz,
                OpcodeValues.CTZ => InternalOpKind.Ctz,
                OpcodeValues.CPOP => InternalOpKind.Cpop,
                OpcodeValues.SEXT_B => InternalOpKind.SextB,
                OpcodeValues.SEXT_H => InternalOpKind.SextH,
                OpcodeValues.ZEXT_H => InternalOpKind.ZextH,
                OpcodeValues.ROL => InternalOpKind.Rol,
                OpcodeValues.ROR => InternalOpKind.Ror,
                OpcodeValues.ROLI => InternalOpKind.RolI,
                OpcodeValues.RORI => InternalOpKind.RorI,
                OpcodeValues.BSET => InternalOpKind.Bset,
                OpcodeValues.BCLR => InternalOpKind.Bclr,
                OpcodeValues.BINV => InternalOpKind.Binv,
                OpcodeValues.BEXT => InternalOpKind.Bext,
                OpcodeValues.BSETI => InternalOpKind.BsetI,
                OpcodeValues.BCLRI => InternalOpKind.BclrI,
                OpcodeValues.BINVI => InternalOpKind.BinvI,
                OpcodeValues.BEXTI => InternalOpKind.BextI,
                OpcodeValues.ANDN => InternalOpKind.AndN,
                OpcodeValues.ORN => InternalOpKind.OrN,
                OpcodeValues.XNOR => InternalOpKind.Xnor,
                OpcodeValues.MIN => InternalOpKind.Min,
                OpcodeValues.MAX => InternalOpKind.Max,
                OpcodeValues.MINU => InternalOpKind.MinU,
                OpcodeValues.MAXU => InternalOpKind.MaxU,
                OpcodeValues.REV8 => InternalOpKind.Rev8,
                OpcodeValues.BREV8 => InternalOpKind.Brev8,
                OpcodeValues.SH1ADD => InternalOpKind.Sh1Add,
                OpcodeValues.SH2ADD => InternalOpKind.Sh2Add,
                OpcodeValues.SH3ADD => InternalOpKind.Sh3Add,
                OpcodeValues.ADD_UW => InternalOpKind.AddUw,
                OpcodeValues.SH1ADD_UW => InternalOpKind.Sh1AddUw,
                OpcodeValues.SH2ADD_UW => InternalOpKind.Sh2AddUw,
                OpcodeValues.SH3ADD_UW => InternalOpKind.Sh3AddUw,
                OpcodeValues.SLLI_UW => InternalOpKind.SlliUw,
                OpcodeValues.CLMUL => InternalOpKind.ClMul,
                OpcodeValues.CLMULH => InternalOpKind.ClMulH,
                OpcodeValues.CLMULR => InternalOpKind.ClMulR,
                OpcodeValues.SLL => InternalOpKind.Sll,
                OpcodeValues.SRL => InternalOpKind.Srl,
                OpcodeValues.SRA => InternalOpKind.Sra,
                OpcodeValues.XOR => InternalOpKind.Xor,
                OpcodeValues.OR => InternalOpKind.Or,
                OpcodeValues.AND => InternalOpKind.And,

                // ── Scalar M-extension ────────────────────────────────────────

                // ── Scalar ALU immediate ──────────────────────────────────────

                // ── CSR ───────────────────────────────────────────────────────

                // ── SMT/VT ────────────────────────────────────────────────────

                // ── VMX ───────────────────────────────────────────────────────

                // ── Nop ───────────────────────────────────────────────────────
                OpcodeValues.Nope => InternalOpKind.AddI, // NOP ≡ ADDI x0, x0, 0

                _ => throw new ArgumentOutOfRangeException(nameof(opcode),
                         $"No InternalOpKind mapping for opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)} ({opcode}). " +
                         "Vector opcodes are not mapped through InternalOpBuilder — they use the vector execution path."),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static InternalOpDataType ResolveDataType(
            ushort opcode,
            InstructionIR instruction)
        {
            return opcode switch
            {
                OpcodeValues.SEXT_B => InternalOpDataType.Byte,
                OpcodeValues.SEXT_H or OpcodeValues.ZEXT_H => InternalOpDataType.Half,
                OpcodeValues.ADDIW or OpcodeValues.SLLIW or OpcodeValues.SRLIW or OpcodeValues.SRAIW or OpcodeValues.ADDW or OpcodeValues.SUBW or OpcodeValues.SLLW or OpcodeValues.SRLW or OpcodeValues.SRAW or OpcodeValues.MULW or OpcodeValues.DIVW or OpcodeValues.DIVUW or OpcodeValues.REMW or OpcodeValues.REMUW or OpcodeValues.SEXT_W or OpcodeValues.ZEXT_W => InternalOpDataType.Word,
                _ => instruction.DataType,
            };
        }

        private static ushort? ResolveCsrTarget(
            ushort opcode,
            InstructionIR instruction)
        {
            if (opcode == OpcodeValues.RDCYCLE)
            {
                return CsrAddresses.Cycle;
            }

            return instruction.Class == InstructionClass.Csr
                ? instruction.CsrAddress ?? (ushort)(instruction.Imm & 0xFFF)
                : null;
        }

        private static InternalOpFlags DeriveFlags(ushort opcode)
        {
            OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo(opcode);
            InternalOpKind kind = MapToKind(opcode);

            if (kind is InternalOpKind.Div or
                InternalOpKind.DivW or
                InternalOpKind.RemW or
                InternalOpKind.SextW or
                InternalOpKind.SextB or
                InternalOpKind.SextH or
                InternalOpKind.Rem or
                InternalOpKind.Slt or
                InternalOpKind.SltI)
            {
                return InternalOpFlags.Signed;
            }

            if (kind is InternalOpKind.Sra or InternalOpKind.SraW or InternalOpKind.SraI or InternalOpKind.SraIW)
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
                _ => InternalOpFlags.None,
            };
        }

        private static InternalOpFlags DeriveAtomicOrderingFlags(
            InternalOpKind kind,
            InstructionIR instruction)
        {
            if (kind is not (
                    InternalOpKind.LrW or
                    InternalOpKind.ScW or
                    InternalOpKind.LrD or
                    InternalOpKind.ScD or
                    InternalOpKind.AmoWord or
                    InternalOpKind.AmoDword))
            {
                return InternalOpFlags.None;
            }

            InternalOpFlags flags = InternalOpFlags.None;
            if (instruction.AcquireOrdering)
            {
                flags |= InternalOpFlags.AcquireOrdering;
            }

            if (instruction.ReleaseOrdering)
            {
                flags |= InternalOpFlags.ReleaseOrdering;
            }

            return flags;
        }
    }
}
