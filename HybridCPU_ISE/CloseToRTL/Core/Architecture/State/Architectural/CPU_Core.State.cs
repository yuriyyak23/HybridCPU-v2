using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// RVV-style configuration for vector operations.
            /// Defines active vector length (VL) and element type (VTYPE).
            /// </summary>
            public struct RVV_Config
            {
                /// <summary>
                /// Active vector length (number of elements to process).
                /// Architectural maximum (VLMAX) is defined by hardware implementation.
                /// Application sets VL ≤ VLMAX via vsetvl instruction.
                /// </summary>
                public ulong VL;

                /// <summary>
                /// Vector type configuration.
                /// Encodes: SEW (selected element width), LMUL (register grouping), tail/mask policy.
                /// For simplicity, we use DataTypeEnum directly in instructions.
                /// </summary>
                public ulong VTYPE;

                /// <summary>
                /// Tail policy: defines behavior for elements beyond VL (tail elements).
                /// 0 = Undisturbed: tail elements retain their previous values (default)
                /// 1 = Agnostic: tail elements may be overwritten with arbitrary values
                ///
                /// RVV semantics: Undisturbed is the safe default for correctness.
                /// Agnostic allows hardware optimization but requires careful compiler support.
                /// </summary>
                public byte TailAgnostic;

                /// <summary>
                /// Mask policy: defines behavior for masked-off elements (predicate bit = 0).
                /// 0 = Undisturbed: masked-off elements retain their previous values (default)
                /// 1 = Agnostic: masked-off elements may be overwritten with arbitrary values
                ///
                /// RVV semantics: Undisturbed is the safe default for correctness.
                /// Agnostic allows hardware optimization but requires careful compiler support.
                /// </summary>
                public byte MaskAgnostic;

                /// <summary>
                /// Maximum vector length supported by this hardware implementation.
                /// Determined by register file width and datapath design.
                /// For our design: 32 elements per strip-mine iteration.
                /// </summary>
                public const ulong VLMAX = 32;

                // ========== Formally Safe Packing (FSP) Configuration ==========

                /// <summary>
                /// FSP Enable: Global enable/disable for FSP slot stealing.
                /// 0 = FSP disabled (default), 1 = FSP enabled
                /// </summary>
                public byte FSP_Enabled;

                /// <summary>
                /// FSP Steal Mask: Per-slot mask controlling which VLIW slots can be stolen.
                /// bit[i] = 1: slot i is stealable, 0: slot i is protected
                /// 8 bits correspond to 8 VLIW slots
                /// Default: 0xFF (all slots stealable)
                /// </summary>
                public byte FSP_StealMask;

                /// <summary>
                /// FSP Scheduling Policy:
                /// 0 = Fair (round-robin), 1 = Priority, 2 = Latency-hide
                /// </summary>
                public byte FSP_Policy;

                /// <summary>
                /// FSP Statistics: Total number of successful injections
                /// </summary>
                public ulong FSP_InjectionCount;

                /// <summary>
                /// FSP Statistics: Total number of rejected injection attempts
                /// </summary>
                public ulong FSP_RejectionCount;

                /// <summary>
                /// Reset configuration to safe defaults.
                /// </summary>
                public void Reset()
                {
                    VL = VLMAX;
                    VTYPE = 0;
                    TailAgnostic = 0; // Undisturbed (safe default)
                    MaskAgnostic = 0; // Undisturbed (safe default)

                    // FSP defaults: disabled for safety
                    FSP_Enabled = 0;
                    FSP_StealMask = 0xFF; // All slots stealable when enabled
                    FSP_Policy = 0; // Fair scheduling
                    FSP_InjectionCount = 0;
                    FSP_RejectionCount = 0;
                }

                /// <summary>
                /// Validate vector configuration consistency.
                /// Ensures VL/SEW/LMUL are correct before executing strip-mining.
                /// Should be called:
                /// 1. When setting configuration (VSETVL, VSETVLI)
                /// 2. At the start of each strip-mining iteration
                /// 3. Before decoding vector instructions
                ///
                /// RVV Invariants:
                /// - VL must not exceed VLMAX
                /// - VL must be non-negative
                /// - SEW must be valid (8, 16, 32, 64 bits)
                /// - LMUL must be valid (1/8, 1/4, 1/2, 1, 2, 4, 8)
                /// - Register groups must not exceed register file size
                ///
                /// HLS Note: Uses Debug.Assert for zero runtime overhead in release builds.
                /// </summary>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Validate()
                {
                    // VL must not exceed VLMAX
                    System.Diagnostics.Debug.Assert(VL <= VLMAX, $"VL {VL} exceeds VLMAX {VLMAX}");

                    // VL must be non-negative (implicit: ulong is always >= 0)
                    // No check needed

                    // Validate SEW (Selected Element Width)
                    // Extract SEW from VTYPE[5:3] (RVV standard encoding)
                    ulong sew_enc = (VTYPE >> 3) & 0x7;
                    bool sewValid = sew_enc switch
                    {
                        0 => true,  // SEW = 8 bits
                        1 => true,  // SEW = 16 bits
                        2 => true,  // SEW = 32 bits
                        3 => true,  // SEW = 64 bits
                        _ => false  // Invalid SEW
                    };
                    System.Diagnostics.Debug.Assert(sewValid, $"Invalid SEW encoding: {sew_enc}");

                    // Validate LMUL (Length Multiplier)
                    // Extract LMUL from VTYPE[2:0] (RVV standard encoding)
                    ulong lmul_enc = VTYPE & 0x7;
                    bool lmulValid = lmul_enc switch
                    {
                        0 => true,  // LMUL = 1
                        1 => true,  // LMUL = 2
                        2 => true,  // LMUL = 4
                        3 => true,  // LMUL = 8
                        5 => true,  // LMUL = 1/8
                        6 => true,  // LMUL = 1/4
                        7 => true,  // LMUL = 1/2
                        _ => false  // Invalid LMUL (4 is reserved)
                    };
                    System.Diagnostics.Debug.Assert(lmulValid, $"Invalid LMUL encoding: {lmul_enc}");

                    // Additional invariant: TailAgnostic and MaskAgnostic must be 0 or 1
                    System.Diagnostics.Debug.Assert(TailAgnostic <= 1, $"Invalid TailAgnostic: {TailAgnostic}");
                    System.Diagnostics.Debug.Assert(MaskAgnostic <= 1, $"Invalid MaskAgnostic: {MaskAgnostic}");
                }
            }

            /// <summary>
            /// Vector exception tracking and status flags.
            /// Accumulates exceptions without immediate interrupts for high-throughput processing.
            /// Supports per-exception masks and priorities for fine-grained control.
            /// </summary>
            public struct VectorExceptionStatus
            {
                /// <summary>
                /// Count of overflow exceptions (integer/fixed-point overflow)
                /// </summary>
                public uint OverflowCount;

                /// <summary>
                /// Count of underflow exceptions (floating-point underflow)
                /// </summary>
                public uint UnderflowCount;

                /// <summary>
                /// Count of divide-by-zero exceptions
                /// </summary>
                public uint DivByZeroCount;

                /// <summary>
                /// Count of invalid operation exceptions (e.g., sqrt of negative, NaN operations)
                /// </summary>
                public uint InvalidOpCount;

                /// <summary>
                /// Count of inexact result exceptions (floating-point rounding)
                /// </summary>
                public uint InexactCount;

                /// <summary>
                /// Bitmask: 1 = masked (no interrupt)
                /// bit0 = Overflow, bit1 = Underflow, bit2 = DivByZero, bit3 = InvalidOp, bit4 = Inexact
                /// </summary>
                private byte _exceptionMask;

                /// <summary>
                /// Packed priorities: 3 bits per exception (0-7)
                /// Layout: [Overflow(3)][Underflow(3)][Div0(3)][Invalid(3)][Inexact(3)]
                /// Total: 15 bits packed into ushort
                /// </summary>
                private ushort _exceptionPriorityPacked;

                /// <summary>
                /// Dirty vector state flag: indicates vector unit has been used since last context switch.
                /// OS/Hypervisor uses this to optimize context save/restore.
                /// 0 = Clean (vector state not modified), 1 = Dirty (vector state modified)
                /// </summary>
                public byte VectorDirty;

                /// <summary>
                /// Vector state enabled flag: indicates if vector operations are allowed.
                /// Used by OS to disable vector unit when not needed (power saving).
                /// 0 = Disabled, 1 = Enabled
                /// </summary>
                public byte VectorEnabled;

                /// <summary>
                /// Floating-point rounding mode for vector operations.
                /// 0 = RNE (Round to Nearest, ties to Even) - IEEE 754 default
                /// 1 = RTZ (Round Towards Zero) - truncate
                /// 2 = RDN (Round Down) - towards -infinity
                /// 3 = RUP (Round Up) - towards +infinity
                /// 4 = RMM (Round to Nearest, ties to Max Magnitude)
                /// </summary>
                public byte RoundingMode;

                /// <summary>
                /// Exception handling mode:
                /// 0 = Accumulate (default) - count exceptions, don't interrupt
                /// 1 = Trap on first - raise interrupt on first exception
                /// 2 = Trap on any - raise interrupt if any lane has exception
                /// </summary>
                public byte ExceptionMode;

                /// <summary>
                /// Get the exception mask register value (5 bits)
                /// </summary>
                public byte GetMask() => _exceptionMask;

                /// <summary>
                /// Set the exception mask register (5 bits valid)
                /// </summary>
                public void SetMask(byte mask) => _exceptionMask = (byte)(mask & 0x1F);

                /// <summary>
                /// Get priority for a specific exception (0-7)
                /// </summary>
                /// <param name="exceptionIndex">0=Overflow, 1=Underflow, 2=DivByZero, 3=InvalidOp, 4=Inexact</param>
                public byte GetPriority(int exceptionIndex)
                {
                    int shift = exceptionIndex * 3;
                    return (byte)((_exceptionPriorityPacked >> shift) & 0x7);
                }

                /// <summary>
                /// Set priority for a specific exception (0-7)
                /// </summary>
                /// <param name="exceptionIndex">0=Overflow, 1=Underflow, 2=DivByZero, 3=InvalidOp, 4=Inexact</param>
                /// <param name="priority">Priority value (0-7, higher = higher priority)</param>
                public void SetPriority(int exceptionIndex, byte priority)
                {
                    priority &= 0x7;
                    int shift = exceptionIndex * 3;
                    _exceptionPriorityPacked &= (ushort)~(0x7 << shift);
                    _exceptionPriorityPacked |= (ushort)(priority << shift);
                }

                /// <summary>
                /// Check if exception is masked
                /// </summary>
                /// <param name="exceptionIndex">0=Overflow, 1=Underflow, 2=DivByZero, 3=InvalidOp, 4=Inexact</param>
                public bool IsMasked(int exceptionIndex)
                {
                    return (_exceptionMask & (1 << exceptionIndex)) != 0;
                }

                /// <summary>
                /// Reset all exception counters and status flags
                /// </summary>
                public void Reset()
                {
                    OverflowCount = 0;
                    UnderflowCount = 0;
                    DivByZeroCount = 0;
                    InvalidOpCount = 0;
                    InexactCount = 0;
                    // Initialize masks to 0 (no masking by default)
                    _exceptionMask = 0;
                    // Initialize priorities (higher value = higher priority)
                    // Overflow=5, Underflow=3, DivByZero=7, InvalidOp=6, Inexact=1
                    _exceptionPriorityPacked = 0;
                    SetPriority(0, 5);  // Overflow
                    SetPriority(1, 3);  // Underflow
                    SetPriority(2, 7);  // DivByZero (highest)
                    SetPriority(3, 6);  // InvalidOp
                    SetPriority(4, 1);  // Inexact (lowest)
                    VectorDirty = 0;
                    VectorEnabled = 1; // Enabled by default
                    RoundingMode = 0; // RNE (IEEE 754 default)
                    ExceptionMode = 0; // Accumulate (high-throughput default)
                }

                /// <summary>
                /// Check if any exceptions have been recorded
                /// </summary>
                public bool HasExceptions()
                {
                    return OverflowCount > 0 || UnderflowCount > 0 || DivByZeroCount > 0 ||
                           InvalidOpCount > 0 || InexactCount > 0;
                }

                /// <summary>
                /// Get total exception count
                /// </summary>
                public uint TotalExceptions()
                {
                    return OverflowCount + UnderflowCount + DivByZeroCount + InvalidOpCount + InexactCount;
                }

                /// <summary>
                /// Set exception mode with validation.
                /// </summary>
                /// <param name="mode">Exception mode: 0 = Accumulate, 1 = Trap on first, 2 = Trap on any</param>
                /// <returns>True if mode was valid and set, false otherwise</returns>
                public bool SetExceptionMode(byte mode)
                {
                    if (mode > 2)
                    {
                        return false; // Invalid mode
                    }
                    ExceptionMode = mode;
                    return true;
                }

                /// <summary>
                /// Set rounding mode with validation.
                /// </summary>
                /// <param name="mode">Rounding mode: 0 = RNE, 1 = RTZ, 2 = RDN, 3 = RUP, 4 = RMM</param>
                /// <returns>True if mode was valid and set, false otherwise</returns>
                public bool SetRoundingMode(byte mode)
                {
                    if (mode > 4)
                    {
                        return false; // Invalid mode
                    }
                    RoundingMode = mode;
                    return true;
                }

                /// <summary>
                /// Clear all exception counters without changing modes or flags.
                /// Useful for resetting exception tracking between computation phases.
                /// </summary>
                public void ClearExceptionCounters()
                {
                    OverflowCount = 0;
                    UnderflowCount = 0;
                    DivByZeroCount = 0;
                    InvalidOpCount = 0;
                    InexactCount = 0;
                }

                /// <summary>
                /// Alias for ClearExceptionCounters for convenience.
                /// </summary>
                public void ClearCounters()
                {
                    ClearExceptionCounters();
                }

                /// <summary>
                /// Set exception mask for a specific exception type.
                /// </summary>
                /// <param name="exception">Exception type</param>
                /// <param name="masked">True to mask (disable interrupts), false to unmask</param>
                public void SetExceptionMask(VectorException exception, bool masked)
                {
                    int bit = 1 << (int)exception;
                    if (masked)
                        _exceptionMask |= (byte)bit;
                    else
                        _exceptionMask &= (byte)~bit;
                }

                /// <summary>
                /// Get exception mask for a specific exception type.
                /// </summary>
                public bool GetExceptionMask(VectorException exception)
                {
                    return (_exceptionMask & (1 << (int)exception)) != 0;
                }

                /// <summary>
                /// Set exception priority for a specific exception type.
                /// </summary>
                /// <param name="exception">Exception type</param>
                /// <param name="priority">Priority value (0-7, higher = higher priority)</param>
                public void SetExceptionPriority(VectorException exception, byte priority)
                {
                    SetPriority((int)exception, priority);
                }

                /// <summary>
                /// Get exception priority for a specific exception type.
                /// </summary>
                public byte GetExceptionPriority(VectorException exception)
                {
                    return GetPriority((int)exception);
                }

                /// <summary>
                /// Get the highest priority pending exception (considering masks).
                /// Returns null if no unmasked exceptions are pending.
                /// </summary>
                public VectorException? GetHighestPriorityException()
                {
                    VectorException? highestException = null;
                    byte highestPriority = 0;

                    // Check each exception type
                    for (int i = 0; i < 5; i++)
                    {
                        uint count = GetExceptionCount(i);
                        if (count == 0) continue;
                        if (IsMasked(i)) continue;

                        byte pri = GetPriority(i);
                        if (highestException == null || pri > highestPriority)
                        {
                            highestException = (VectorException)i;
                            highestPriority = pri;
                        }
                    }

                    return highestException;
                }

                /// <summary>
                /// Get exception count by index (helper for GetHighestPriorityException)
                /// </summary>
                private uint GetExceptionCount(int exceptionIndex)
                {
                    return exceptionIndex switch
                    {
                        0 => OverflowCount,
                        1 => UnderflowCount,
                        2 => DivByZeroCount,
                        3 => InvalidOpCount,
                        4 => InexactCount,
                        _ => 0
                    };
                }
            }

            /// <summary>
            /// Vector exception types for mask and priority configuration.
            /// </summary>
            public enum VectorException : byte
            {
                Overflow = 0,
                Underflow = 1,
                DivByZero = 2,
                InvalidOp = 3,
                Inexact = 4
            }

            /// <summary>
            /// Predicate (mask) registers for conditional execution.
            /// RVV-style: each bit controls whether corresponding vector element is active.
            ///
            /// Architecture contract:
            /// - PredicateMask field in instruction encodes predicate register INDEX (not inline mask)
            /// - PredicateMask = 0 → masking disabled (all lanes active)
            /// - PredicateMask = 1..15 → use PredicateRegisters[index]
            /// - Masked-off elements: do not write results, do not trap on exceptions (RVV rule)
            ///
            /// Each register is 64 bits, supporting up to 64 vector elements.
            /// For larger vectors, implementation may use multiple registers or extend width.
            ///
            /// HLS Note: Fixed-size array (16 registers) for zero dynamic allocation.
            /// </summary>
            private ulong predReg0, predReg1, predReg2, predReg3, predReg4, predReg5, predReg6, predReg7;
            private ulong predReg8, predReg9, predReg10, predReg11, predReg12, predReg13, predReg14, predReg15;

            /// <summary>
            /// RVV configuration state.
            /// </summary>
            public RVV_Config VectorConfig;

            /// <summary>
            /// Vector exception tracking and status.
            /// </summary>
            public VectorExceptionStatus ExceptionStatus;

            /// <summary>
            /// Check if a vector lane is active for the given predicate register index.
            ///
            /// Semantics:
            /// - predIndex = 0: masking disabled, all lanes active (return true)
            /// - predIndex = 1..15: check bit [lane] in predicate register
            /// - lane out of bounds: inactive (return false for safety)
            ///
            /// HLS-friendly: inline, minimal branching, direct field access.
            /// </summary>
            /// <param name="predIndex">Predicate register index (0 = disabled, 1-15 = register)</param>
            /// <param name="lane">Lane/element index within vector</param>
            /// <returns>True if lane is active (should execute/writeback)</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool LaneActive(byte predIndex, int lane)
            {
                // Masking disabled: all lanes active
                if (predIndex == 0) return true;

                // Out of bounds check
                if (lane < 0 || lane >= 64) return false;

                // Get predicate register value by index
                ulong mask = predIndex switch
                {
                    1 => predReg1,
                    2 => predReg2,
                    3 => predReg3,
                    4 => predReg4,
                    5 => predReg5,
                    6 => predReg6,
                    7 => predReg7,
                    8 => predReg8,
                    9 => predReg9,
                    10 => predReg10,
                    11 => predReg11,
                    12 => predReg12,
                    13 => predReg13,
                    14 => predReg14,
                    15 => predReg15,
                    _ => 0xFFFFFFFFFFFFFFFFUL // Default: all active for out of range
                };

                // Check bit [lane] in selected predicate register
                return ((mask >> lane) & 1UL) != 0;
            }

            /// <summary>
            /// Check if a vector lane is active with full RVV semantics.
            /// Returns true if lane i should be written, considering:
            /// 1. VL (vector length): lane must be < VL
            /// 2. Mask bit: lane must have maskBit = true, unless MaskAgnostic
            /// 3. Tail policy: beyond VL, lanes are only written if TailAgnostic = true
            ///
            /// Invariant: [Invariant("Masked/tailed lanes remain unchanged when undisturbed")]
            /// When MaskAgnostic = 0 or TailAgnostic = 0, inactive lanes MUST NOT be written.
            ///
            /// RVV Specification compliance:
            /// - Undisturbed policy (agnostic=0): destination elements unchanged
            /// - Agnostic policy (agnostic=1): destination elements may be overwritten
            ///
            /// HLS-friendly: inline, minimal branching, deterministic behavior.
            /// </summary>
            /// <param name="laneIndex">Lane/element index (0-based)</param>
            /// <param name="maskBit">Predicate mask bit for this lane (true = active)</param>
            /// <returns>True if lane should be written</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsLaneActive(int laneIndex, bool maskBit)
            {
                // Lane beyond VL: only active if TailAgnostic = 1
                if (laneIndex >= (int)VectorConfig.VL)
                {
                    return VectorConfig.TailAgnostic != 0;
                }

                // Lane within VL: check mask policy
                // If mask bit is false and MaskAgnostic is 0 (undisturbed), lane is NOT active
                if (!maskBit && VectorConfig.MaskAgnostic == 0)
                {
                    return false;
                }

                // Lane is active: either mask bit is true, or MaskAgnostic = 1
                return true;
            }

            /// <summary>
            /// Initialize predicate registers and vector configuration.
            /// Called during CPU core initialization.
            /// HLS Note: No dynamic allocation - all registers are struct fields.
            /// </summary>
            private void InitializeVectorState()
            {
                // Initialize all predicate registers to "all lanes active" (all bits set)
                predReg0 = 0xFFFFFFFFFFFFFFFFUL;
                predReg1 = 0xFFFFFFFFFFFFFFFFUL;
                predReg2 = 0xFFFFFFFFFFFFFFFFUL;
                predReg3 = 0xFFFFFFFFFFFFFFFFUL;
                predReg4 = 0xFFFFFFFFFFFFFFFFUL;
                predReg5 = 0xFFFFFFFFFFFFFFFFUL;
                predReg6 = 0xFFFFFFFFFFFFFFFFUL;
                predReg7 = 0xFFFFFFFFFFFFFFFFUL;
                predReg8 = 0xFFFFFFFFFFFFFFFFUL;
                predReg9 = 0xFFFFFFFFFFFFFFFFUL;
                predReg10 = 0xFFFFFFFFFFFFFFFFUL;
                predReg11 = 0xFFFFFFFFFFFFFFFFUL;
                predReg12 = 0xFFFFFFFFFFFFFFFFUL;
                predReg13 = 0xFFFFFFFFFFFFFFFFUL;
                predReg14 = 0xFFFFFFFFFFFFFFFFUL;
                predReg15 = 0xFFFFFFFFFFFFFFFFUL;

                // Initialize RVV config
                VectorConfig.Reset();

                // Initialize exception status
                ExceptionStatus.Reset();

                // Initialize scratch buffers for stream operations
                InitializeScratchBuffers();
            }

            /// <summary>
            /// Set a predicate register to a given mask value.
            /// Used by mask-generation instructions (e.g., vmslt, vmseq, etc.)
            /// </summary>
            /// <param name="predIndex">Predicate register index (1-15)</param>
            /// <param name="mask">64-bit mask (one bit per lane)</param>
            public void SetPredicateRegister(int predIndex, ulong mask)
            {
                switch (predIndex)
                {
                    case 0: predReg0 = mask; break;
                    case 1: predReg1 = mask; break;
                    case 2: predReg2 = mask; break;
                    case 3: predReg3 = mask; break;
                    case 4: predReg4 = mask; break;
                    case 5: predReg5 = mask; break;
                    case 6: predReg6 = mask; break;
                    case 7: predReg7 = mask; break;
                    case 8: predReg8 = mask; break;
                    case 9: predReg9 = mask; break;
                    case 10: predReg10 = mask; break;
                    case 11: predReg11 = mask; break;
                    case 12: predReg12 = mask; break;
                    case 13: predReg13 = mask; break;
                    case 14: predReg14 = mask; break;
                    case 15: predReg15 = mask; break;
                }
            }

            /// <summary>
            /// Get a predicate register value.
            /// </summary>
            /// <param name="predIndex">Predicate register index (0-15)</param>
            /// <returns>64-bit mask value</returns>
            public ulong GetPredicateRegister(int predIndex)
            {
                return predIndex switch
                {
                    0 => predReg0,
                    1 => predReg1,
                    2 => predReg2,
                    3 => predReg3,
                    4 => predReg4,
                    5 => predReg5,
                    6 => predReg6,
                    7 => predReg7,
                    8 => predReg8,
                    9 => predReg9,
                    10 => predReg10,
                    11 => predReg11,
                    12 => predReg12,
                    13 => predReg13,
                    14 => predReg14,
                    15 => predReg15,
                    _ => 0xFFFFFFFFFFFFFFFFUL // Default: all active
                };
            }

            /// <summary>
            /// Saved vector context for exception handling and context switching.
            /// Captures all vector-unit state needed to resume after an interrupt handler.
            /// </summary>
            public struct VectorContext
            {
                public RVV_Config Config;
                public VectorExceptionStatus ExceptionStatus;
                public ulong PredReg0, PredReg1, PredReg2, PredReg3;
                public ulong PredReg4, PredReg5, PredReg6, PredReg7;
                public ulong PredReg8, PredReg9, PredReg10, PredReg11;
                public ulong PredReg12, PredReg13, PredReg14, PredReg15;
                public ulong FaultingPC;
                public ulong FaultingLane;
                public uint FaultingOpCode;
                public bool Valid;
            }

            /// <summary>
            /// Saved vector context slot (used by GenerateVectorExceptionTrap / InterruptReturn).
            /// </summary>
            public VectorContext SavedVectorContext;

            /// <summary>
            /// Snapshot the full vector-unit state into a VectorContext.
            /// Called before dispatching to a vector exception handler.
            /// </summary>
            public VectorContext SaveVectorContext()
            {
                VectorContext ctx;
                ctx.Config = VectorConfig;
                ctx.ExceptionStatus = ExceptionStatus;
                ctx.PredReg0 = predReg0;   ctx.PredReg1 = predReg1;
                ctx.PredReg2 = predReg2;   ctx.PredReg3 = predReg3;
                ctx.PredReg4 = predReg4;   ctx.PredReg5 = predReg5;
                ctx.PredReg6 = predReg6;   ctx.PredReg7 = predReg7;
                ctx.PredReg8 = predReg8;   ctx.PredReg9 = predReg9;
                ctx.PredReg10 = predReg10; ctx.PredReg11 = predReg11;
                ctx.PredReg12 = predReg12; ctx.PredReg13 = predReg13;
                ctx.PredReg14 = predReg14; ctx.PredReg15 = predReg15;
                ctx.FaultingPC = ReadActiveLivePc();
                ctx.FaultingLane = 0;
                ctx.FaultingOpCode = 0;
                ctx.Valid = true;
                return ctx;
            }

            /// <summary>
            /// Restore full vector-unit state from a previously saved VectorContext.
            /// Called on InterruptReturn when a vector exception context exists.
            /// </summary>
            public void RestoreVectorContext(VectorContext ctx)
            {
                if (!ctx.Valid) return;
                VectorConfig = ctx.Config;
                ExceptionStatus = ctx.ExceptionStatus;
                predReg0 = ctx.PredReg0;   predReg1 = ctx.PredReg1;
                predReg2 = ctx.PredReg2;   predReg3 = ctx.PredReg3;
                predReg4 = ctx.PredReg4;   predReg5 = ctx.PredReg5;
                predReg6 = ctx.PredReg6;   predReg7 = ctx.PredReg7;
                predReg8 = ctx.PredReg8;   predReg9 = ctx.PredReg9;
                predReg10 = ctx.PredReg10; predReg11 = ctx.PredReg11;
                predReg12 = ctx.PredReg12; predReg13 = ctx.PredReg13;
                predReg14 = ctx.PredReg14; predReg15 = ctx.PredReg15;
            }

            /// <summary>
            /// PHASE 1: Public accessor for UI
            /// Get saved vector context
            /// </summary>
            public VectorContext GetSavedVectorContext()
            {
                return SavedVectorContext;
            }

            /// <summary>
            /// PHASE 1: Public accessor for UI
            /// Get all predicate registers as an array
            /// </summary>
            public ulong[] GetAllPredicateRegisters()
            {
                return new ulong[]
                {
                    predReg0, predReg1, predReg2, predReg3,
                    predReg4, predReg5, predReg6, predReg7,
                    predReg8, predReg9, predReg10, predReg11,
                    predReg12, predReg13, predReg14, predReg15
                };
            }
        }
    }
}
