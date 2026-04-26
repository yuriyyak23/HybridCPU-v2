using HybridCPU_ISE.Arch;

using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Typed vector ALU operations for all data types.
    /// Provides element-wise operations respecting type semantics (signed/unsigned/float).
    ///
    /// Design principles:
    /// - No dynamic allocation (HLS-compatible)
    /// - Uses ElementCodec for uniform load/store
    /// - Respects predicate masks (inactive lanes skip computation)
    /// - RVV semantics: masked-off elements don't write, don't trap
    /// - Handles all data types: INT8-INT64, UINT8-UINT64, FLOAT32-FLOAT64
    /// </summary>
    internal static partial class VectorALU
    {
        /// <summary>
        /// Mark vector unit as dirty (used for context switching optimization).
        /// Should be called at the start of any vector operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MarkVectorDirty(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.VectorDirty = 1;
        }

        /// <summary>
        /// Track divide-by-zero exception.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrackDivByZero(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.DivByZeroCount++;
            CheckExceptionMode(ref core);
        }

        /// <summary>
        /// Track invalid operation exception (e.g., sqrt of negative, NaN operations).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrackInvalidOp(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.InvalidOpCount++;
            CheckExceptionMode(ref core);
        }

        /// <summary>
        /// Track overflow exception (integer/fixed-point overflow).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrackOverflow(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.OverflowCount++;
            CheckExceptionMode(ref core);
        }

        /// <summary>
        /// Track underflow exception (floating-point underflow).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrackUnderflow(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.UnderflowCount++;
            CheckExceptionMode(ref core);
        }

        /// <summary>
        /// Track inexact result exception (floating-point rounding).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TrackInexact(ref Processor.CPU_Core core)
        {
            core.ExceptionStatus.InexactCount++;
            CheckExceptionMode(ref core);
        }

        /// <summary>
        /// Apply rounding mode to a floating-point value.
        /// Implements IEEE 754 rounding modes as specified in VectorExceptionStatus.RoundingMode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ApplyRounding(double value, byte roundingMode, ref Processor.CPU_Core core)
        {
            // Store original value to detect inexact results
            double original = value;

            // RoundingMode:
            // 0 = RNE (Round to Nearest, ties to Even) - IEEE 754 default
            // 1 = RTZ (Round Towards Zero) - truncate
            // 2 = RDN (Round Down) - towards -infinity
            // 3 = RUP (Round Up) - towards +infinity
            // 4 = RMM (Round to Nearest, ties to Max Magnitude)

            double rounded = roundingMode switch
            {
                0 => Math.Round(value, MidpointRounding.ToEven),      // RNE
                1 => Math.Truncate(value),                            // RTZ
                2 => Math.Floor(value),                               // RDN
                3 => Math.Ceiling(value),                             // RUP
                4 => Math.Round(value, MidpointRounding.AwayFromZero), // RMM
                _ => value // Default: no rounding
            };

            // Track inexact result if rounding changed the value
            if (rounded != original && !double.IsNaN(rounded) && !double.IsInfinity(rounded))
            {
                TrackInexact(ref core);
            }

            return rounded;
        }

        /// <summary>
        /// Check for underflow in floating-point result.
        /// Detects denormal/subnormal values that indicate underflow.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckUnderflow(double result, ref Processor.CPU_Core core)
        {
            // Check for underflow: result is non-zero but smaller than the smallest normal double
            // double.MinValue is the most negative value, not the smallest positive value
            // The smallest positive normal value is approximately 2.2250738585072014e-308
            if (result != 0.0 && !double.IsInfinity(result) && !double.IsNaN(result))
            {
                double absValue = Math.Abs(result);
                // Check if result is denormal/subnormal (smaller than smallest normal double)
                if (absValue < 2.2250738585072014e-308)
                {
                    TrackUnderflow(ref core);
                }
            }
        }

        /// <summary>
        /// Check exception mode and handle trap if necessary.
        /// Called after tracking any exception.
        /// Implements mask and priority-based exception handling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckExceptionMode(ref Processor.CPU_Core core)
        {
            // ExceptionMode:
            // 0 = Accumulate (default) - count exceptions, don't interrupt
            // 1 = Trap on first - raise interrupt on first exception
            // 2 = Trap on any - raise interrupt if any lane has exception

            byte exceptionMode = core.ExceptionStatus.ExceptionMode;

            // Mode 0: Accumulate only, no interrupt
            if (exceptionMode == 0)
            {
                return;
            }

            // Check for highest priority unmasked exception
            Processor.CPU_Core.VectorException? highestException = core.ExceptionStatus.GetHighestPriorityException();

            // If no unmasked exceptions, don't trap
            if (!highestException.HasValue)
            {
                return;
            }

            // Mode 1 or 2: Generate trap/interrupt
            // Check if this is the first exception (for mode 1: trap on first)
            uint totalExceptions = core.ExceptionStatus.TotalExceptions();
            bool shouldTrap = false;

            if (exceptionMode == 1)
            {
                // Trap on first: only trap if this is the first exception
                shouldTrap = (totalExceptions == 1);
            }
            else if (exceptionMode == 2)
            {
                // Trap on any: always trap when any exception occurs
                shouldTrap = true;
            }

            if (shouldTrap)
            {
                // Generate vector exception interrupt with exception type
                GenerateVectorExceptionTrap(ref core, highestException.Value);
            }
        }

        /// <summary>
        /// Generate a trap/interrupt for vector exceptions.
        /// Saves full vector context before dispatching to the interrupt handler,
        /// so InterruptReturn can restore the vector unit to its pre-exception state.
        /// </summary>
        /// <param name="core">CPU core reference</param>
        /// <param name="exceptionType">Type of exception that triggered the trap</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GenerateVectorExceptionTrap(ref Processor.CPU_Core core, Processor.CPU_Core.VectorException exceptionType)
        {
            // Base vector exception IRQ: 0x80
            // Exception type encoded in low bits: 0x80 + exception type
            const ushort VECTOR_EXCEPTION_IRQ_BASE = 0x80;
            ushort exceptionIRQ = (ushort)(VECTOR_EXCEPTION_IRQ_BASE + (byte)exceptionType);

            // Save vector context BEFORE dispatching to handler
            core.SavedVectorContext = core.SaveVectorContext();

            // Store exception type in saved context for handler inspection
            core.SavedVectorContext.FaultingOpCode = (uint)exceptionType;

            byte result = core.DispatchInterrupt(
                Processor.DeviceType.VectorUnit,
                exceptionIRQ);

            // Interrupt delivery failures still force the active VT through the
            // canonical interrupt wake transition rather than the legacy core FSM.
            if (result != 0)
            {
                core.ApplyInterruptTransitionToVirtualThread(core.ReadActiveVirtualThreadId());
            }
        }

        /// <summary>
        /// Apply binary operation element-wise across vectors.
        /// Supports all arithmetic and logical operations.
        ///
        /// RVV semantics:
        /// - Inactive lanes (predicate mask bit = 0) are not processed
        /// - No exceptions raised for masked-off elements
        /// - Results written only for active lanes
        /// - Tail/mask policy respected (undisturbed = preserve, agnostic = may overwrite)
        /// </summary>
        /// <param name="op">Operation code (InstructionsEnum value)</param>
        /// <param name="dt">Data type enum</param>
        /// <param name="a">First source operand buffer</param>
        /// <param name="b">Second source operand buffer</param>
        /// <param name="dst">Destination buffer (may alias 'a' for destructive ops)</param>
        /// <param name="elemSize">Size of each element in bytes</param>
        /// <param name="vl">Vector length (number of active elements)</param>
        /// <param name="predIndex">Predicate register index (0 = no masking)</param>
        /// <param name="tailAgnostic">Per-instruction tail-agnostic bit (ta): tail elements may be overwritten</param>
        /// <param name="maskAgnostic">Per-instruction mask-agnostic bit (ma): masked-off elements may be overwritten</param>
        /// <param name="core">Reference to CPU core for predicate access</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyBinary(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> a,
            ReadOnlySpan<byte> b,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                // Check predicate mask: skip inactive lanes for undisturbed policy
                // Effective mask-agnostic = per-instruction bit OR global config
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                if (!laneActive && !effectiveMaskAgnostic)
                {
                    // Undisturbed: preserve destination value, skip computation
                    continue;
                }

                int off = (int)(lane * (ulong)elemSize);

                // Dispatch based on data type category
                if (DataTypeUtils.IsFloatingPoint(dt))
                {
                    // Check if operation is bitwise (requires raw bit manipulation)
                    if (IsBitwiseOp(op))
                    {
                        // Use raw bit operations for bitwise ops on floats
                        ulong x = ElementCodec.LoadRaw(a, off, dt);
                        ulong y = ElementCodec.LoadRaw(b, off, dt);
                        ulong r = ExecuteBitwiseRaw(op, x, y);
                        ElementCodec.StoreRaw(dst, off, dt, r);
                    }
                    else
                    {
                        // Use floating-point arithmetic for non-bitwise ops
                        double x = ElementCodec.LoadF(a, off, dt);
                        double y = ElementCodec.LoadF(b, off, dt);
                        double r = ExecuteBinaryFloat(op, x, y, ref core);

                        // Check for underflow in result
                        CheckUnderflow(r, ref core);

                        // Apply rounding mode before storing
                        r = ApplyRounding(r, core.ExceptionStatus.RoundingMode, ref core);

                        ElementCodec.StoreF(dst, off, dt, r);
                    }
                }
                else if (DataTypeUtils.IsSignedInteger(dt))
                {
                    long x = ElementCodec.LoadI(a, off, dt);
                    long y = ElementCodec.LoadI(b, off, dt);
                    long r = ExecuteBinarySignedInt(op, x, y, ref core);
                    ElementCodec.StoreI(dst, off, dt, r);
                }
                else // Unsigned integer
                {
                    ulong x = ElementCodec.LoadU(a, off, dt);
                    ulong y = ElementCodec.LoadU(b, off, dt);
                    ulong r = ExecuteBinaryUnsignedInt(op, x, y, ref core);
                    ElementCodec.StoreU(dst, off, dt, r);
                }
            }
        }

        /// <summary>
        /// Apply binary operation with immediate value element-wise across vectors.
        /// Used for operations like shift-by-immediate (VSLL.VI, VSRL.VI, VSRA.VI).
        ///
        /// RVV semantics:
        /// - Inactive lanes (predicate mask bit = 0) are not processed
        /// - Immediate value is used as the second operand for all elements
        /// </summary>
        /// <param name="op">Operation code (InstructionsEnum value)</param>
        /// <param name="dt">Data type enum</param>
        /// <param name="a">Source operand buffer</param>
        /// <param name="immediate">Immediate value (used as second operand)</param>
        /// <param name="dst">Destination buffer</param>
        /// <param name="elemSize">Size of each element in bytes</param>
        /// <param name="vl">Vector length (number of active elements)</param>
        /// <param name="predIndex">Predicate register index (0 = no masking)</param>
        /// <param name="tailAgnostic">Per-instruction tail-agnostic bit (ta): tail elements may be overwritten</param>
        /// <param name="maskAgnostic">Per-instruction mask-agnostic bit (ma): masked-off elements may be overwritten</param>
        /// <param name="core">Reference to CPU core for predicate access</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyBinaryImmediate(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> a,
            ushort immediate,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                // Check predicate mask: skip inactive lanes for undisturbed policy
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                if (!laneActive && !effectiveMaskAgnostic)
                {
                    // Undisturbed: preserve destination value, skip computation
                    continue;
                }

                int off = (int)(lane * (ulong)elemSize);

                // Dispatch based on data type category
                if (DataTypeUtils.IsFloatingPoint(dt))
                {
                    // Shift operations on floating point are bitwise (raw bit manipulation)
                    ulong x = ElementCodec.LoadRaw(a, off, dt);
                    ulong r = ExecuteBinaryImmediateRaw(op, x, immediate);
                    ElementCodec.StoreRaw(dst, off, dt, r);
                }
                else if (DataTypeUtils.IsSignedInteger(dt))
                {
                    long x = ElementCodec.LoadI(a, off, dt);
                    long r = ExecuteBinaryImmediateSignedInt(op, x, immediate);
                    ElementCodec.StoreI(dst, off, dt, r);
                }
                else // Unsigned integer
                {
                    ulong x = ElementCodec.LoadU(a, off, dt);
                    ulong r = ExecuteBinaryImmediateUnsignedInt(op, x, immediate);
                    ElementCodec.StoreU(dst, off, dt, r);
                }
            }
        }

        /// <summary>
        /// Apply unary operation element-wise across vector.
        /// Handles operations like NOT, SQRT, NEG that take single operand.
        /// Respects RVV tail/mask policy semantics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyUnary(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                // Check predicate mask: skip inactive lanes for undisturbed policy
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                if (!laneActive && !effectiveMaskAgnostic)
                {
                    // Undisturbed: preserve destination value, skip computation
                    continue;
                }

                int off = (int)(lane * (ulong)elemSize);

                if (DataTypeUtils.IsFloatingPoint(dt))
                {
                    // Check if operation is bitwise (requires raw bit manipulation)
                    if (IsBitwiseOp(op))
                    {
                        // Use raw bit operations for bitwise ops on floats
                        ulong x = ElementCodec.LoadRaw(src, off, dt);
                        ulong r = ExecuteUnaryRaw(op, x);
                        ElementCodec.StoreRaw(dst, off, dt, r);
                    }
                    else
                    {
                        // Use floating-point arithmetic for non-bitwise ops
                        double x = ElementCodec.LoadF(src, off, dt);
                        double r = ExecuteUnaryFloat(op, x, ref core);

                        // Check for underflow in result
                        CheckUnderflow(r, ref core);

                        // Apply rounding mode before storing
                        r = ApplyRounding(r, core.ExceptionStatus.RoundingMode, ref core);

                        ElementCodec.StoreF(dst, off, dt, r);
                    }
                }
                else if (DataTypeUtils.IsSignedInteger(dt))
                {
                    long x = ElementCodec.LoadI(src, off, dt);
                    long r = ExecuteUnarySignedInt(op, x);
                    ElementCodec.StoreI(dst, off, dt, r);
                }
                else
                {
                    ulong x = ElementCodec.LoadU(src, off, dt);
                    ulong r = ExecuteUnaryUnsignedInt(op, x);
                    ElementCodec.StoreU(dst, off, dt, r);
                }
            }
        }

        /// <summary>
        /// Apply ternary FMA operation element-wise across vectors.
        /// FMA (Fused Multiply-Add): performs multiplication and addition as single operation.
        ///
        /// Operations:
        /// - VFMADD: vd[i] = (vd[i] * vs1[i]) + vs2[i]
        /// - VFMSUB: vd[i] = (vd[i] * vs1[i]) - vs2[i]
        /// - VFNMADD: vd[i] = -(vd[i] * vs1[i]) + vs2[i]
        /// - VFNMSUB: vd[i] = -(vd[i] * vs1[i]) - vs2[i]
        ///
        /// Note: vd is both source and destination (destructive operation).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyFMA(
            uint op,
            DataTypeEnum dt,
            Span<byte> vd,          // Destination and multiplicand (vd = vd * vs1 +/- vs2)
            ReadOnlySpan<byte> vs1, // Multiplier
            ReadOnlySpan<byte> vs2, // Addend
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                // Check predicate mask: skip inactive lanes for undisturbed policy
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                if (!laneActive && !effectiveMaskAgnostic)
                {
                    // Undisturbed: preserve destination value, skip computation
                    continue;
                }

                int off = (int)(lane * (ulong)elemSize);

                // FMA operations: separate FP32 and FP64 paths for IEEE 754 compliance
                if (DataTypeUtils.IsFloatingPoint(dt))
                {
                    // Determine if this is FP32 or FP64
                    if (dt == DataTypeEnum.FLOAT32)
                    {
                        // FP32 path: use MathF.FusedMultiplyAdd for proper float precision
                        uint dBits = BitConverter.ToUInt32(vd.Slice(off, 4));
                        uint s1Bits = BitConverter.ToUInt32(vs1.Slice(off, 4));
                        uint s2Bits = BitConverter.ToUInt32(vs2.Slice(off, 4));

                        float fD = BitConverter.UInt32BitsToSingle(dBits);
                        float fS1 = BitConverter.UInt32BitsToSingle(s1Bits);
                        float fS2 = BitConverter.UInt32BitsToSingle(s2Bits);

                        // Execute FMA with single-precision (one rounding step)
                        float result = ExecuteFMAFloat32(op, fD, fS1, fS2);

                        // Check for invalid operation (NaN inputs/outputs)
                        if (float.IsNaN(result) && !(float.IsNaN(fD) || float.IsNaN(fS1) || float.IsNaN(fS2)))
                        {
                            TrackInvalidOp(ref core);
                        }

                        // Check for underflow (denormal result)
                        if (result != 0.0f && !float.IsInfinity(result) && !float.IsNaN(result))
                        {
                            float absValue = MathF.Abs(result);
                            if (absValue < 1.175494351e-38f) // Smallest normal float
                            {
                                TrackUnderflow(ref core);
                            }
                        }

                        // Apply rounding mode (FP32 has 24-bit mantissa)
                        result = ApplyRoundingFloat32(result, core.ExceptionStatus.RoundingMode, ref core);

                        // Store back to destination
                        uint resultBits = BitConverter.SingleToUInt32Bits(result);
                        BitConverter.TryWriteBytes(vd.Slice(off, 4), resultBits);
                    }
                    else if (dt == DataTypeEnum.FLOAT64)
                    {
                        // FP64 path: use Math.FusedMultiplyAdd for proper double precision
                        double d = ElementCodec.LoadF(vd, off, dt);
                        double s1 = ElementCodec.LoadF(vs1, off, dt);
                        double s2 = ElementCodec.LoadF(vs2, off, dt);

                        // Execute FMA with double-precision (one rounding step)
                        double result = ExecuteFMAFloat64(op, d, s1, s2);

                        // Check for invalid operation (NaN inputs/outputs)
                        if (double.IsNaN(result) && !(double.IsNaN(d) || double.IsNaN(s1) || double.IsNaN(s2)))
                        {
                            TrackInvalidOp(ref core);
                        }

                        // Check for underflow (denormal result)
                        CheckUnderflow(result, ref core);

                        // Apply rounding mode (FP64 has 53-bit mantissa)
                        result = ApplyRounding(result, core.ExceptionStatus.RoundingMode, ref core);

                        ElementCodec.StoreF(vd, off, dt, result);
                    }
                    else
                    {
                        // Fallback for other float types (shouldn't happen)
                        double d = ElementCodec.LoadF(vd, off, dt);
                        double s1 = ElementCodec.LoadF(vs1, off, dt);
                        double s2 = ElementCodec.LoadF(vs2, off, dt);
                        double r = ExecuteFMAFloat(op, d, s1, s2);
                        CheckUnderflow(r, ref core);
                        r = ApplyRounding(r, core.ExceptionStatus.RoundingMode, ref core);
                        ElementCodec.StoreF(vd, off, dt, r);
                    }
                }
                else if (DataTypeUtils.IsSignedInteger(dt))
                {
                    long d = ElementCodec.LoadI(vd, off, dt);
                    long s1 = ElementCodec.LoadI(vs1, off, dt);
                    long s2 = ElementCodec.LoadI(vs2, off, dt);
                    long r = ExecuteFMASignedInt(op, d, s1, s2);
                    ElementCodec.StoreI(vd, off, dt, r);
                }
                else // Unsigned integer
                {
                    ulong d = ElementCodec.LoadU(vd, off, dt);
                    ulong s1 = ElementCodec.LoadU(vs1, off, dt);
                    ulong s2 = ElementCodec.LoadU(vs2, off, dt);
                    ulong r = ExecuteFMAUnsignedInt(op, d, s1, s2);
                    ElementCodec.StoreU(vd, off, dt, r);
                }
            }
        }

        /// <summary>
        /// Apply reduction operation to collapse vector to scalar.
        /// Reduces all active elements in a vector to a single result.
        ///
        /// Reduction operations:
        /// - VREDSUM: sum of all elements
        /// - VREDMAX/VREDMIN: maximum/minimum of all elements
        /// - VREDAND/VREDOR/VREDXOR: bitwise AND/OR/XOR of all elements
        ///
        /// RVV semantics:
        /// - Only active lanes (predicate mask) participate in reduction
        /// - Result written to element 0 of destination
        /// - Inactive lanes don't affect the result
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyReduction(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            // Initialize accumulator based on operation type
            bool hasValue = false;

            if (DataTypeUtils.IsFloatingPoint(dt))
            {
                double accumulator = GetReductionIdentityFloat(op);

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    double value = ElementCodec.LoadF(src, off, dt);

                    if (!hasValue)
                    {
                        accumulator = value;
                        hasValue = true;
                    }
                    else
                    {
                        accumulator = ExecuteReductionFloat(op, accumulator, value);
                    }
                }

                // Write result to element 0 of destination
                ElementCodec.StoreF(dst, 0, dt, accumulator);
            }
            else if (DataTypeUtils.IsSignedInteger(dt))
            {
                long accumulator = GetReductionIdentitySignedInt(op);

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    long value = ElementCodec.LoadI(src, off, dt);

                    if (!hasValue)
                    {
                        accumulator = value;
                        hasValue = true;
                    }
                    else
                    {
                        accumulator = ExecuteReductionSignedInt(op, accumulator, value);
                    }
                }

                // Write result to element 0 of destination
                ElementCodec.StoreI(dst, 0, dt, accumulator);
            }
            else // Unsigned integer
            {
                ulong accumulator = GetReductionIdentityUnsignedInt(op);

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    ulong value = ElementCodec.LoadU(src, off, dt);

                    if (!hasValue)
                    {
                        accumulator = value;
                        hasValue = true;
                    }
                    else
                    {
                        accumulator = ExecuteReductionUnsignedInt(op, accumulator, value);
                    }
                }

                // Write result to element 0 of destination
                ElementCodec.StoreU(dst, 0, dt, accumulator);
            }
        }

        /// <summary>
        /// Apply dot-product operation to compute scalar result from two vectors.
        /// Computes sum(vs1[i] * vs2[i]) for all active elements.
        ///
        /// Dot-product operations (ML/DSP optimized):
        /// - VDOT: signed integer dot product
        /// - VDOTU: unsigned integer dot product
        /// - VDOTF: floating-point dot product
        ///
        /// RVV semantics:
        /// - Only active lanes (predicate mask) contribute to sum
        /// - Result written to element 0 of destination
        /// - Inactive lanes don't affect the result
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyDotProduct(
            uint op,
            DataTypeEnum dt,
            ReadOnlySpan<byte> a,
            ReadOnlySpan<byte> b,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            // Reuse the shared widening classification so the active dot-product execute contour
            // follows the same source/destination width truth already published by metadata.
            bool isWideningDotProduct = AddressGen.IsWideningOpcode(op);
            bool isWideningE4M3 = isWideningDotProduct &&
                dt == DataTypeEnum.FLOAT8_E4M3;
            bool isWideningE5M2 = isWideningDotProduct &&
                dt == DataTypeEnum.FLOAT8_E5M2;

            if (isWideningE4M3 || isWideningE5M2)
            {
                // Widening dot product: FP8 (1-byte) > FP32 (4-byte)
                // Read elements as 1-byte FP8, decode on-the-fly, accumulate in FP32
                float accumulator = 0.0f;

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    // Read 1-byte FP8 elements
                    int off = (int)lane; // 1-byte stride
                    byte rawA = a[off];
                    byte rawB = b[off];

                    // Decode FP8 > FP32 on-the-fly (hardware decoder)
                    float valA = isWideningE4M3 ? ElementCodec.DecodeE4M3(rawA) : ElementCodec.DecodeE5M2(rawA);
                    float valB = isWideningE4M3 ? ElementCodec.DecodeE4M3(rawB) : ElementCodec.DecodeE5M2(rawB);

                    // Accumulate in FP32
                    accumulator += (valA * valB);
                }

                // Check for underflow/overflow in final result
                CheckUnderflow(accumulator, ref core);

                // Apply rounding mode before storing
                double rounded = ApplyRounding(accumulator, core.ExceptionStatus.RoundingMode, ref core);

                // Write 4-byte FP32 result to destination (asymmetric write)
                ElementCodec.StoreF(dst, 0, DataTypeEnum.FLOAT32, rounded);
            }
            else if (DataTypeUtils.IsFloatingPoint(dt))
            {
                double accumulator = 0.0;

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    double x = ElementCodec.LoadF(a, off, dt);
                    double y = ElementCodec.LoadF(b, off, dt);
                    accumulator += x * y;
                }

                // Check for underflow in final result
                CheckUnderflow(accumulator, ref core);

                // Apply rounding mode before storing
                accumulator = ApplyRounding(accumulator, core.ExceptionStatus.RoundingMode, ref core);

                // Write result to element 0 of destination
                ElementCodec.StoreF(dst, 0, dt, accumulator);
            }
            else if (DataTypeUtils.IsSignedInteger(dt))
            {
                long accumulator = 0;

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    long x = ElementCodec.LoadI(a, off, dt);
                    long y = ElementCodec.LoadI(b, off, dt);
                    accumulator += x * y;
                }

                // Write result to element 0 of destination
                ElementCodec.StoreI(dst, 0, dt, accumulator);
            }
            else // Unsigned integer
            {
                ulong accumulator = 0;

                for (ulong lane = 0; lane < vl; lane++)
                {
                    bool laneActive = core.LaneActive(predIndex, (int)lane);
                    if (!laneActive) continue;

                    int off = (int)(lane * (ulong)elemSize);
                    ulong x = ElementCodec.LoadU(a, off, dt);
                    ulong y = ElementCodec.LoadU(b, off, dt);
                    accumulator += x * y;
                }

                // Write result to element 0 of destination
                ElementCodec.StoreU(dst, 0, dt, accumulator);
            }
        }

        /// <summary>
        /// Apply compress operation (predicative movement - ARM SVE style).
        /// Packs active elements to the left, skipping masked-off elements.
        ///
        /// Operation: VCOMPRESS
        /// - Collect all elements where mask bit is 1
        /// - Write them sequentially starting from destination[0]
        /// - Inactive elements are skipped (not copied)
        ///
        /// Example: src = [A, B, C, D, E], mask = 0b10110 (0,2,3 active)
        ///          dst = [A, C, D, ?, ?] (3 elements packed left)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyCompress(
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            int dstIndex = 0;

            for (ulong lane = 0; lane < vl; lane++)
            {
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                if (!laneActive) continue;

                int srcOff = (int)(lane * (ulong)elemSize);
                int dstOff = dstIndex * elemSize;

                // Copy element from source to compressed destination
                src.Slice(srcOff, elemSize).CopyTo(dst.Slice(dstOff, elemSize));
                dstIndex++;
            }
        }

        /// <summary>
        /// Apply expand operation (predicative movement - ARM SVE style).
        /// Unpacks elements from packed source according to mask pattern.
        ///
        /// Operation: VEXPAND
        /// - For each active lane (mask bit = 1), read next element from packed source
        /// - For inactive lanes (mask bit = 0), leave destination unchanged unless
        ///   mask-agnostic policy allows deterministic overwrite on that lane
        ///
        /// Example: src = [A, B, C], mask = 0b10110 (0,2,3 active)
        ///          dst = [A, ?, B, C, ?] (elements expanded by mask)
        ///
        /// Inverse operation of VCOMPRESS.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyExpand(
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            int srcIndex = 0;

            for (ulong lane = 0; lane < vl; lane++)
            {
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                bool laneMayOverwrite = laneActive || effectiveMaskAgnostic;

                if (!laneMayOverwrite)
                {
                    // Undisturbed: preserve destination value, skip
                    continue;
                }

                int dstOff = (int)(lane * (ulong)elemSize);

                if (laneActive)
                {
                    // Active lane: read next element from packed source
                    int srcOff = srcIndex * elemSize;
                    src.Slice(srcOff, elemSize).CopyTo(dst.Slice(dstOff, elemSize));
                    srcIndex++;
                }
                else
                {
                    // Deterministic overwrite keeps the packed-source cursor aligned with
                    // active lanes while still honoring agnostic follow-through.
                    dst.Slice(dstOff, elemSize).Clear();
                }
            }
        }

        /// <summary>
        /// Apply permutation operation (gather with index vector).
        /// Performs indexed access: vd[i] = vs1[vs2[i]]
        ///
        /// Operations: VPERMUTE, VRGATHER
        /// - vs2[i] contains index into vs1
        /// - Out-of-bounds indices produce zero
        /// - Supports arbitrary element reordering
        ///
        /// Example: vs1 = [A, B, C, D], vs2 = [2, 0, 3, 1]
        ///          vd = [C, A, D, B] (permuted by indices)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyPermute(
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            ReadOnlySpan<byte> indices,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                bool laneMayOverwrite = laneActive || effectiveMaskAgnostic;

                if (!laneMayOverwrite)
                {
                    // Undisturbed: preserve destination value
                    continue;
                }

                int dstOff = (int)(lane * (ulong)elemSize);

                // Agnostic masked-off lanes are allowed to take the same deterministic
                // gather result as active lanes instead of silently preserving stale data.
                ulong index = ElementCodec.LoadU(indices, dstOff, dt);

                if (index < vl)
                {
                    // Valid index: gather from source
                    int srcOff = (int)(index * (ulong)elemSize);
                    src.Slice(srcOff, elemSize).CopyTo(dst.Slice(dstOff, elemSize));
                }
                else
                {
                    // Out-of-bounds: write zero
                    dst.Slice(dstOff, elemSize).Clear();
                }
            }
        }

        /// <summary>
        /// Apply slide-up operation (shift elements up by immediate offset).
        ///
        /// Operation: VSLIDEUP
        /// - Shifts elements towards higher indices by offset
        /// - vd[i+offset] = vs1[i] for valid indices
        /// - Lower offset elements are preserved from vd (slide into gap)
        ///
        /// Example: vs1 = [A, B, C, D], offset = 2
        ///          vd = [vd[0], vd[1], A, B] (C,D out of range)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplySlideUp(
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            ushort offset,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                bool laneMayOverwrite = laneActive || effectiveMaskAgnostic;

                if (!laneMayOverwrite)
                {
                    // Undisturbed: preserve destination value
                    continue;
                }

                int dstOff = (int)(lane * (ulong)elemSize);

                if (lane >= offset)
                {
                    // Copy from source at (lane - offset)
                    ulong srcLane = lane - offset;
                    int srcOff = (int)(srcLane * (ulong)elemSize);
                    src.Slice(srcOff, elemSize).CopyTo(dst.Slice(dstOff, elemSize));
                }
                // else: slide-up gap lanes preserve destination; agnostic policy does not
                // require every inactive lane to overwrite.
            }
        }

        /// <summary>
        /// Apply slide-down operation (shift elements down by immediate offset).
        ///
        /// Operation: VSLIDEDOWN
        /// - Shifts elements towards lower indices by offset
        /// - vd[i] = vs1[i+offset] for valid source indices
        /// - Upper elements that slide past end are set to zero
        ///
        /// Example: vs1 = [A, B, C, D], offset = 2
        ///          vd = [C, D, 0, 0]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplySlideDown(
            DataTypeEnum dt,
            ReadOnlySpan<byte> src,
            Span<byte> dst,
            int elemSize,
            ulong vl,
            ushort offset,
            byte predIndex,
            bool tailAgnostic,
            bool maskAgnostic,
            ref Processor.CPU_Core core)
        {
            // Mark vector unit as dirty (for context switching)
            MarkVectorDirty(ref core);

            for (ulong lane = 0; lane < vl; lane++)
            {
                bool laneActive = core.LaneActive(predIndex, (int)lane);
                bool effectiveMaskAgnostic = maskAgnostic || (core.VectorConfig.MaskAgnostic != 0);
                bool laneMayOverwrite = laneActive || effectiveMaskAgnostic;

                if (!laneMayOverwrite)
                {
                    // Undisturbed: preserve destination value
                    continue;
                }

                int dstOff = (int)(lane * (ulong)elemSize);

                ulong srcLane = lane + offset;
                if (srcLane < vl)
                {
                    // Valid source index: copy element
                    int srcOff = (int)(srcLane * (ulong)elemSize);
                    src.Slice(srcOff, elemSize).CopyTo(dst.Slice(dstOff, elemSize));
                }
                else
                {
                    // Out of bounds: write zero
                    dst.Slice(dstOff, elemSize).Clear();
                }
            }
        }

        // --- Private operation execution helpers ---

        /// <summary>
        /// Check if operation is bitwise (XOR/OR/AND/NOT).
        /// Bitwise operations on floats require raw bit manipulation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBitwiseOp(uint op)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => true,
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => true,
                _ => false
            };
        }

        /// <summary>
        /// Execute binary bitwise operation on raw bits.
        /// Used for bitwise operations on floating-point types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteBitwiseRaw(uint op, ulong x, ulong y)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => x ^ y,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => x | y,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => x & y,
                _ => 0
            };
        }

        /// <summary>
        /// Execute unary bitwise operation on raw bits.
        /// Used for bitwise operations on floating-point types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteUnaryRaw(uint op, ulong x)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => ~x,
                _ => 0
            };
        }

        /// <summary>
        /// Execute binary immediate operation on raw bits (for floating-point types).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteBinaryImmediateRaw(uint op, ulong x, ushort immediate)
        {
            int shiftAmount = immediate & 0x3F; // Limit shift to 0-63
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => x << shiftAmount,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => x >> shiftAmount,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => x >> shiftAmount, // Logical for raw bits
                _ => 0
            };
        }

        /// <summary>
        /// Execute binary immediate operation for signed integers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExecuteBinaryImmediateSignedInt(uint op, long x, ushort immediate)
        {
            int shiftAmount = immediate & 0x3F; // Limit shift to 0-63
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => x << shiftAmount,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => x >> shiftAmount, // Arithmetic shift
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => (long)((ulong)x >> shiftAmount), // Logical shift
                // Vector-immediate arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => x + (long)(short)immediate, // Sign-extend immediate
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => x - (long)(short)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => x * (long)(short)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => x & (long)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => x | (long)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => x ^ (long)immediate,
                _ => 0
            };
        }

        /// <summary>
        /// Execute binary immediate operation for unsigned integers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteBinaryImmediateUnsignedInt(uint op, ulong x, ushort immediate)
        {
            int shiftAmount = immediate & 0x3F; // Limit shift to 0-63
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => x << shiftAmount,
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => x >> shiftAmount, // Logical shift
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => x >> shiftAmount, // Logical for unsigned
                // Vector-immediate arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => x + (ulong)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => x - (ulong)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => x * (ulong)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => x & (ulong)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => x | (ulong)immediate,
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => x ^ (ulong)immediate,
                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ExecuteBinaryFloat(uint op, double x, double y, ref Processor.CPU_Core core)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => x + y,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => x - y,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => x * y,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => DivideFloat(x, y, ref core),
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => ModuloFloat(x, y, ref core),

                // Min/Max operations (floating-point)
                (uint)Processor.CPU_Core.InstructionsEnum.VMIN => Math.Min(x, y),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAX => Math.Max(x, y),

                _ => 0.0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DivideFloat(double x, double y, ref Processor.CPU_Core core)
        {
            if (y != 0.0) return x / y;
            TrackDivByZero(ref core);
            return 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ModuloFloat(double x, double y, ref Processor.CPU_Core core)
        {
            if (y != 0.0) return x % y;
            TrackDivByZero(ref core);
            return 0.0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExecuteBinarySignedInt(uint op, long x, long y, ref Processor.CPU_Core core)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => x + y,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => x - y,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => x * y,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => DivideSignedInt(x, y, ref core),
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => ModuloSignedInt(x, y, ref core),
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => x ^ y,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => x | y,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => x & y,

                // Shift operations
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => x << (int)(y & 0x3F),
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => x >> (int)(y & 0x3F), // Arithmetic shift
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => (long)((ulong)x >> (int)(y & 0x3F)), // Logical shift

                // Min/Max operations
                (uint)Processor.CPU_Core.InstructionsEnum.VMIN => Math.Min(x, y),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAX => Math.Max(x, y),

                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long DivideSignedInt(long x, long y, ref Processor.CPU_Core core)
        {
            if (y != 0) return x / y;
            TrackDivByZero(ref core);
            return 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ModuloSignedInt(long x, long y, ref Processor.CPU_Core core)
        {
            if (y != 0) return x % y;
            TrackDivByZero(ref core);
            return 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteBinaryUnsignedInt(uint op, ulong x, ulong y, ref Processor.CPU_Core core)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => x + y,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => x - y,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => x * y,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => DivideUnsignedInt(x, y, ref core),
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => ModuloUnsignedInt(x, y, ref core),
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => x ^ y,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => x | y,
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => x & y,

                // Shift operations (all logical for unsigned)
                (uint)Processor.CPU_Core.InstructionsEnum.VSLL => x << (int)(y & 0x3F),
                (uint)Processor.CPU_Core.InstructionsEnum.VSRL => x >> (int)(y & 0x3F),
                (uint)Processor.CPU_Core.InstructionsEnum.VSRA => x >> (int)(y & 0x3F), // Logical shift for unsigned

                // Min/Max operations (unsigned)
                (uint)Processor.CPU_Core.InstructionsEnum.VMINU => Math.Min(x, y),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAXU => Math.Max(x, y),

                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong DivideUnsignedInt(ulong x, ulong y, ref Processor.CPU_Core core)
        {
            if (y != 0) return x / y;
            TrackDivByZero(ref core);
            return 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ModuloUnsignedInt(ulong x, ulong y, ref Processor.CPU_Core core)
        {
            if (y != 0) return x % y;
            TrackDivByZero(ref core);
            return 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ExecuteUnaryFloat(uint op, double x, ref Processor.CPU_Core core)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSQRT => SqrtFloat(x, ref core),
                _ => 0.0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double SqrtFloat(double x, ref Processor.CPU_Core core)
        {
            if (x >= 0.0) return Math.Sqrt(x);
            TrackInvalidOp(ref core);
            return double.NaN;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ExecuteUnarySignedInt(uint op, long x)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSQRT => (long)Math.Sqrt((double)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => ~x,
                // Bit manipulation instructions (treat as unsigned)
                (uint)Processor.CPU_Core.InstructionsEnum.VREVERSE => (long)ReverseBits((ulong)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VPOPCNT => (long)PopCount((ulong)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VCLZ => (long)CountLeadingZeros((ulong)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VCTZ => (long)CountTrailingZeros((ulong)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VBREV8 => (long)ReverseBytes((ulong)x),
                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExecuteUnaryUnsignedInt(uint op, ulong x)
        {
            return op switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSQRT => (ulong)Math.Sqrt((double)x),
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => ~x,
                // Bit manipulation instructions
                (uint)Processor.CPU_Core.InstructionsEnum.VREVERSE => ReverseBits(x),
                (uint)Processor.CPU_Core.InstructionsEnum.VPOPCNT => (ulong)PopCount(x),
                (uint)Processor.CPU_Core.InstructionsEnum.VCLZ => (ulong)CountLeadingZeros(x),
                (uint)Processor.CPU_Core.InstructionsEnum.VCTZ => (ulong)CountTrailingZeros(x),
                (uint)Processor.CPU_Core.InstructionsEnum.VBREV8 => ReverseBytes(x),
                _ => 0
            };
        }

        // ========================================================================
        // Vector Comparison Instructions - Generate Predicate Masks
        // ========================================================================

        /// <summary>
        /// Apply vector comparison operation element-wise and generate predicate mask.
        /// Compares two vectors element-by-element and produces a bit mask result.
        ///
        /// RVV semantics:
        /// - Each element comparison produces 1 bit in the result mask
        /// - Result bit = 1 if comparison is true, 0 if false
        /// - Respects predicate masking (input mask controls which comparisons execute)
        /// - Output is written to a predicate register (not a vector register)
        /// - Type-aware: handles signed/unsigned integers and floating-point correctly
        ///
        /// Comparison operations:
        /// - VCMPEQ: vd[i] = (vs1[i] == vs2[i])
        /// - VCMPNE: vd[i] = (vs1[i] != vs2[i])
        /// - VCMPLT: vd[i] = (vs1[i] < vs2[i])
        /// - VCMPLE: vd[i] = (vs1[i] <= vs2[i])
        /// - VCMPGT: vd[i] = (vs1[i] > vs2[i])
        /// - VCMPGE: vd[i] = (vs1[i] >= vs2[i])
        /// </summary>
        /// <param name="op">Comparison operation code</param>
        /// <param name="dt">Data type for comparison</param>
        /// <param name="a">First source vector buffer</param>
        /// <param name="b">Second source vector buffer</param>
        /// <param name="elemSize">Size of each element in bytes</param>
        /// <param name="vl">Vector length (number of elements to compare)</param>
        /// <param name="predIndex">Input predicate mask index (0 = all active)</param>
        /// <param name="core">CPU core reference for predicate access</param>
        /// <returns>64-bit predicate mask where bit[i]=1 if comparison is true</returns>
    }
}

