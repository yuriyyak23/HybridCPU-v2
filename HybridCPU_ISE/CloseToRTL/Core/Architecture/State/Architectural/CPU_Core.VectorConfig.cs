using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Vector CSR (Control & Status Register) address space.
            /// Extends standard CSR space for vector exception management and FSP control.
            /// </summary>
            public enum VectorCSR : ushort
            {
                /// <summary>
                /// Vector Exception Mask Register (VEXCPMASK) - address 0x900
                /// Controls which exceptions generate interrupts.
                /// bit0 = Overflow, bit1 = Underflow, bit2 = DivByZero, bit3 = InvalidOp, bit4 = Inexact
                /// 1 = masked (no interrupt), 0 = unmasked (interrupt enabled)
                /// </summary>
                VEXCPMASK = 0x900,

                /// <summary>
                /// Vector Exception Priority Register (VEXCPPRI) - address 0x901
                /// Controls exception priority for arbitration.
                /// Packed format: [Overflow(3)][Underflow(3)][Div0(3)][Invalid(3)][Inexact(3)]
                /// Each exception has 3-bit priority (0-7, higher = higher priority)
                /// </summary>
                VEXCPPRI = 0x901,

                // ========== Formally Safe Packing (FSP) Control Registers ==========
                // Base address: 0xA00-0xA0F

                /// <summary>
                /// VLIW Steal Enable Register (VLIW_STEAL_ENABLE) - address 0xA00
                /// Global enable/disable for FSP slot stealing.
                /// bit0 = 1: FSP enabled, 0: FSP disabled
                /// Other bits reserved (must be 0)
                /// </summary>
                VLIW_STEAL_ENABLE = 0xA00,

                /// <summary>
                /// VLIW Steal Mask Register (VLIW_STEAL_MASK) - address 0xA01
                /// Per-slot mask controlling which VLIW slots can be stolen.
                /// bit[i] = 1: slot i is stealable, 0: slot i is protected
                /// 8 bits correspond to 8 VLIW slots (bit0 = slot0, ..., bit7 = slot7)
                /// Default: 0xFF (all slots stealable if enabled)
                /// </summary>
                VLIW_STEAL_MASK = 0xA01,

                /// <summary>
                /// VLIW Steal Policy Register (VLIW_STEAL_POLICY) - address 0xA02
                /// Scheduling policy for selecting micro-operations to inject.
                /// 0 = Fair (round-robin across threads)
                /// 1 = Priority (higher priority threads first)
                /// 2 = Latency-hide (prefer memory operations)
                /// Other values reserved
                /// </summary>
                VLIW_STEAL_POLICY = 0xA02,

                /// <summary>
                /// FSP Statistics Register (FSP_STATS_INJECTIONS) - address 0xA10
                /// Read-only counter: total number of successful injections
                /// </summary>
                FSP_STATS_INJECTIONS = 0xA10,

                /// <summary>
                /// FSP Statistics Register (FSP_STATS_REJECTIONS) - address 0xA11
                /// Read-only counter: total number of rejected injection attempts
                /// </summary>
                FSP_STATS_REJECTIONS = 0xA11
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong ReadVectorScalarRegisterValue(IntRegister register, string paramName) =>
                ReadActiveArchValue(RequireArchRegId(register, paramName));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteVectorScalarRegisterValue(IntRegister register, string paramName, ulong value) =>
                WriteActiveArchValue(RequireArchRegId(register, paramName), value);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static InvalidOperationException CreateUnsupportedVectorConfigDirectHelperSurfaceException(
                string helperName,
                string opcodeName)
            {
                return new InvalidOperationException(
                    $"{helperName} reached a retained direct {opcodeName} helper surface without the authoritative lane-7 retire/apply carrier. " +
                    "Direct callers must use the canonical mainline vector-config path so VectorConfig state updates and optional rd writeback stay coupled on the authoritative system-singleton contour.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static InvalidOperationException CreateUnsupportedDirectCsrHelperSurfaceException(
                string helperName,
                ushort csrAddr)
            {
                return new InvalidOperationException(
                    $"{helperName} reached a retained direct CSR helper surface for CSR 0x{csrAddr:X3} without the authoritative lane-7 retire/apply carrier. " +
                    "Direct callers must use the canonical CSR/materializer path instead of eager helper-side read/write mutation, zero-default readback or silent write-ignore behavior.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRejectRetainedDirectHelperSurface() => true;

            /// <summary>
            /// Execute VSETVL instruction - Set Vector Length dynamically.
            /// Retained direct helper surface: unsupported on the authoritative runtime and must fail closed.
            ///
            /// RVV Semantics:
            /// - rd = vsetvl(rs1, rs2)
            /// - rs1 contains Application Vector Length (AVL) - desired number of elements
            /// - rs2 contains vtype encoding (SEW, LMUL, tail/mask policy)
            /// - rd receives the actual VL granted by hardware (min(AVL, VLMAX))
            /// - Updates CPU_Core.VectorConfig.VL with calculated value
            ///
            /// HybridCPU Implementation:
            /// - VLMAX = 32 (fixed hardware limit)
            /// - VL = min(AVL, VLMAX)
            /// - Preserves tail/mask agnostic policies from vtype
            /// - Returns actual VL to destination register
            ///
            /// Use cases:
            /// - Strip-mining loops: set VL for each iteration
            /// - Variable-length operations: adapt to actual data size
            /// - Performance tuning: explicit control over vector length
            /// </summary>
            /// <param name="rdReg">Destination register (receives actual VL)</param>
            /// <param name="rs1Reg">Source register 1 (contains AVL)</param>
            /// <param name="rs2Reg">Source register 2 (contains vtype encoding)</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteVSETVL(IntRegister rdReg, IntRegister rs1Reg, IntRegister rs2Reg)
            {
                throw CreateUnsupportedVectorConfigDirectHelperSurfaceException(
                    nameof(ExecuteVSETVL),
                    "VSETVL");
            }

            /// <summary>
            /// Execute VSETVLI instruction - Set Vector Length with immediate vtype.
            /// Retained direct helper surface: unsupported on the authoritative runtime and must fail closed.
            ///
            /// Similar to VSETVL, but vtype is encoded as immediate in instruction.
            /// rd = vsetvli(rs1=AVL, imm=vtype)
            /// </summary>
            /// <param name="rdReg">Destination register (receives actual VL)</param>
            /// <param name="rs1Reg">Source register (contains AVL)</param>
            /// <param name="vtypeImm">Immediate vtype encoding</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteVSETVLI(IntRegister rdReg, IntRegister rs1Reg, ulong vtypeImm)
            {
                throw CreateUnsupportedVectorConfigDirectHelperSurfaceException(
                    nameof(ExecuteVSETVLI),
                    "VSETVLI");
            }

            /// <summary>
            /// Execute VSETIVLI instruction - Set Vector Length with immediate AVL and vtype.
            /// Retained direct helper surface: unsupported on the authoritative runtime and must fail closed.
            ///
            /// Both AVL and vtype are immediates encoded in instruction.
            /// rd = vsetivli(imm=AVL, imm=vtype)
            ///
            /// This variant is useful for small, known vector lengths (e.g., processing
            /// last few elements in a strip-mining loop).
            /// </summary>
            /// <param name="rdReg">Destination register (receives actual VL)</param>
            /// <param name="avlImm">Immediate AVL value</param>
            /// <param name="vtypeImm">Immediate vtype encoding</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteVSETIVLI(IntRegister rdReg, ulong avlImm, ulong vtypeImm)
            {
                throw CreateUnsupportedVectorConfigDirectHelperSurfaceException(
                    nameof(ExecuteVSETIVLI),
                    "VSETIVLI");
            }

            /// <summary>
            /// Helper method to get current effective vector length.
            /// Returns min(VL, remaining_elements) for strip-mining loops.
            /// </summary>
            /// <param name="remainingElements">Number of elements left to process</param>
            /// <returns>Effective VL for this iteration</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ulong GetEffectiveVL(ulong remainingElements)
            {
                return Math.Min(VectorConfig.VL, Math.Min(remainingElements, RVV_Config.VLMAX));
            }

            /// <summary>
            /// Check if vector configuration is valid.
            /// Called before executing vector instructions.
            /// </summary>
            /// <returns>True if VectorConfig is valid, false otherwise</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsVectorConfigValid()
            {
                // VL must be within hardware limits
                if (VectorConfig.VL > RVV_Config.VLMAX)
                    return false;

                // VL must be non-zero for vector operations
                if (VectorConfig.VL == 0)
                    return false;

                return true;
            }

            /// <summary>
            /// Execute CSR_READ: read a Control/Status Register value into a scalar register.
            /// Retained direct helper surface: unsupported on the authoritative runtime and must fail closed.
            /// CSR address space defined in Appendix A.2 of architecture spec.
            /// </summary>
            /// <param name="destReg">Destination scalar register</param>
            /// <param name="csrAddr">CSR address from Immediate field</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteCSRRead(IntRegister destReg, ushort csrAddr)
            {
                if (ShouldRejectRetainedDirectHelperSurface())
                {
                    throw CreateUnsupportedDirectCsrHelperSurfaceException(
                        nameof(ExecuteCSRRead),
                        csrAddr);
                }

                ulong value = csrAddr switch
                {
                    // Vector Configuration (0x000–0x00F)
                    0x000 => VectorConfig.VL,
                    0x001 => VectorConfig.VTYPE,
                    0x002 => VectorConfig.TailAgnostic,
                    0x003 => VectorConfig.MaskAgnostic,
                    0x004 => RVV_Config.VLMAX,

                    // Floating-Point Control (0x010–0x01F)
                    0x010 => ExceptionStatus.RoundingMode,
                    0x011 => ExceptionStatus.ExceptionMode,

                    // Exception Counters (0x020–0x02F)
                    0x020 => ExceptionStatus.OverflowCount,
                    0x021 => ExceptionStatus.UnderflowCount,
                    0x022 => ExceptionStatus.DivByZeroCount,
                    0x023 => ExceptionStatus.InvalidOpCount,
                    0x024 => ExceptionStatus.InexactCount,
                    0x025 => ExceptionStatus.TotalExceptions(),
                    0x026 => ExceptionStatus.HasExceptions() ? 1UL : 0UL,

                    // Vector Unit Status (0x030–0x03F)
                    0x030 => ExceptionStatus.VectorDirty,
                    0x031 => ExceptionStatus.VectorEnabled,

                    // Vector Exception Control (0x900–0x9FF)
                    0x900 => ExceptionStatus.GetMask(),  // VEXCPMASK
                    0x901 => ReadPackedPriorities(),      // VEXCPPRI

                    // FSP Control Registers (0xA00–0xA0F)
                    0xA00 => VectorConfig.FSP_Enabled,    // VLIW_STEAL_ENABLE
                    0xA01 => VectorConfig.FSP_StealMask,  // VLIW_STEAL_MASK
                    0xA02 => VectorConfig.FSP_Policy,     // VLIW_STEAL_POLICY

                    // FSP Statistics (0xA10–0xA1F) - read-only
                    0xA10 => VectorConfig.FSP_InjectionCount,   // FSP_STATS_INJECTIONS
                    0xA11 => VectorConfig.FSP_RejectionCount,   // FSP_STATS_REJECTIONS

                    _ => 0
                };

                WriteVectorScalarRegisterValue(destReg, nameof(destReg), value);
            }

            /// <summary>
            /// Helper method to read packed priorities into a single register.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private ulong ReadPackedPriorities()
            {
                ulong packed = 0;
                for (int i = 0; i < 5; i++)
                {
                    byte pri = ExceptionStatus.GetPriority(i);
                    packed |= (ulong)(pri & 0x7) << (i * 3);
                }
                return packed;
            }

            /// <summary>
            /// Execute CSR_WRITE: write a scalar register value to a Control/Status Register.
            /// Retained direct helper surface: unsupported on the authoritative runtime and must fail closed.
            /// Read-only CSRs (VL, VLMAX, TotalExceptions, HasExceptions) are silently ignored.
            /// </summary>
            /// <param name="srcReg">Source scalar register containing value to write</param>
            /// <param name="csrAddr">CSR address from Immediate field</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ExecuteCSRWrite(IntRegister srcReg, ushort csrAddr)
            {
                if (ShouldRejectRetainedDirectHelperSurface())
                {
                    throw CreateUnsupportedDirectCsrHelperSurfaceException(
                        nameof(ExecuteCSRWrite),
                        csrAddr);
                }

                ulong sourceValue = ReadVectorScalarRegisterValue(srcReg, nameof(srcReg));
                byte val = (byte)sourceValue;
                switch (csrAddr)
                {
                    // Vector Configuration (VL is read-only — use VSETVL)
                    case 0x001: VectorConfig.VTYPE = sourceValue; break;
                    case 0x002: VectorConfig.TailAgnostic = val; break;
                    case 0x003: VectorConfig.MaskAgnostic = val; break;

                    // Floating-Point Control
                    case 0x010: ExceptionStatus.SetRoundingMode(val); break;
                    case 0x011: ExceptionStatus.SetExceptionMode(val); break;

                    // Vector Unit Status
                    case 0x030: ExceptionStatus.VectorDirty = val; break;
                    case 0x031: ExceptionStatus.VectorEnabled = val; break;

                    // Vector Exception Control
                    case 0x900: Exec_VSETVEXCPMASK(sourceValue); break;  // VEXCPMASK
                    case 0x901: Exec_VSETVEXCPPRI(sourceValue); break;   // VEXCPPRI

                    // FSP Control Registers
                    case 0xA00: VectorConfig.FSP_Enabled = (byte)(sourceValue & 0x1); break;    // VLIW_STEAL_ENABLE
                    case 0xA01: VectorConfig.FSP_StealMask = (byte)(sourceValue & 0xFF); break; // VLIW_STEAL_MASK
                    case 0xA02: VectorConfig.FSP_Policy = (byte)(sourceValue & 0x3); break;     // VLIW_STEAL_POLICY (0-2 valid)

                    // FSP Statistics (0xA10–0xA1F) are read-only, writes ignored
                }
            }

            /// <summary>
            /// Execute VSETVEXCPMASK instruction - Set Vector Exception Mask.
            /// Sets which exceptions are masked (no interrupt generation).
            ///
            /// Instruction format:
            /// - rs1 contains mask value (5 bits valid)
            /// - bit0 = Overflow, bit1 = Underflow, bit2 = DivByZero, bit3 = InvalidOp, bit4 = Inexact
            /// - 1 = masked (no interrupt), 0 = unmasked (interrupt enabled)
            /// </summary>
            /// <param name="rs1Value">GPR value containing mask (5 LSBs used)</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Exec_VSETVEXCPMASK(ulong rs1Value)
            {
                byte mask = (byte)(rs1Value & 0x1F);
                ref var status = ref this.ExceptionStatus;
                status.SetMask(mask);
            }

            /// <summary>
            /// Execute VSETVEXCPPRI instruction - Set Vector Exception Priorities.
            /// Sets priority values for exception arbitration.
            ///
            /// Instruction format:
            /// - rs1 contains packed priorities:
            ///   [2:0]   Overflow priority (0-7)
            ///   [5:3]   Underflow priority (0-7)
            ///   [8:6]   DivByZero priority (0-7)
            ///   [11:9]  InvalidOp priority (0-7)
            ///   [14:12] Inexact priority (0-7)
            /// </summary>
            /// <param name="rs1Value">GPR value containing packed priorities</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Exec_VSETVEXCPPRI(ulong rs1Value)
            {
                ref var status = ref this.ExceptionStatus;

                // Extract and set each priority (3 bits per exception)
                for (int i = 0; i < 5; i++)
                {
                    byte pri = (byte)((rs1Value >> (i * 3)) & 0x7);
                    status.SetPriority(i, pri);
                }
            }
        }
    }
}
