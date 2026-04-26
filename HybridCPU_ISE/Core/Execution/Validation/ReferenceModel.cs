using HybridCPU_ISE.Arch;

using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// ReferenceModel: Simple, unoptimized implementation of vector operations.
    /// Used for self-checking and validation of the optimized VectorALU.
    ///
    /// Design principles:
    /// - Simplicity over performance
    /// - Readability over optimization
    /// - Correctness is paramount
    /// - No SIMD, no intrinsics, no tricks
    /// - Uses native float/double operations
    /// - One element at a time (no vectorization)
    ///
    /// Usage:
    /// - Enable self-check mode in Processor
    /// - Compare VectorALU results with ReferenceModel
    /// - Report discrepancies for debugging
    ///
    /// Note: This model is intentionally slow and should only be used
    /// in debug/test builds for validation purposes.
    /// </summary>
    internal static class ReferenceModel
    {
        /// <summary>
        /// Reference implementation of binary floating-point operations.
        /// Uses straightforward float/double arithmetic for validation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double BinaryFloat(uint opCode, double a, double b)
        {
            return opCode switch
            {
                // Arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => a + b,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => a - b,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => a * b,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => b != 0.0 ? a / b : double.NaN,

                // Min/Max
                (uint)Processor.CPU_Core.InstructionsEnum.VMIN => Math.Min(a, b),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAX => Math.Max(a, b),

                _ => ThrowUnknownReferenceOpcode<double>(nameof(BinaryFloat), opCode)
            };
        }

        /// <summary>
        /// Reference implementation of FMA for 32-bit floats.
        /// Uses MathF.FusedMultiplyAdd for single-precision validation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FMAFloat32(uint opCode, float d, float s1, float s2)
        {
            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => MathF.FusedMultiplyAdd(d, s1, s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => MathF.FusedMultiplyAdd(d, s1, -s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => MathF.FusedMultiplyAdd(-d, s1, s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => MathF.FusedMultiplyAdd(-d, s1, -s2),
                _ => ThrowUnknownReferenceOpcode<float>(nameof(FMAFloat32), opCode)
            };
        }

        /// <summary>
        /// Reference implementation of FMA for 64-bit doubles.
        /// Uses Math.FusedMultiplyAdd for double-precision validation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double FMAFloat64(uint opCode, double d, double s1, double s2)
        {
            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VFMADD => Math.FusedMultiplyAdd(d, s1, s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFMSUB => Math.FusedMultiplyAdd(d, s1, -s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMADD => Math.FusedMultiplyAdd(-d, s1, s2),
                (uint)Processor.CPU_Core.InstructionsEnum.VFNMSUB => Math.FusedMultiplyAdd(-d, s1, -s2),
                _ => ThrowUnknownReferenceOpcode<double>(nameof(FMAFloat64), opCode)
            };
        }

        /// <summary>
        /// Reference implementation of unary floating-point operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double UnaryFloat(uint opCode, double a)
        {
            return opCode switch
            {
                (uint)Processor.CPU_Core.InstructionsEnum.VSQRT => Math.Sqrt(a),
                (uint)Processor.CPU_Core.InstructionsEnum.VNOT => BitConverter.Int64BitsToDouble(~BitConverter.DoubleToInt64Bits(a)),
                _ => ThrowUnknownReferenceOpcode<double>(nameof(UnaryFloat), opCode)
            };
        }

        /// <summary>
        /// Reference implementation of binary integer operations.
        /// Supports both signed and unsigned arithmetic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BinarySignedInt(uint opCode, long a, long b)
        {
            return opCode switch
            {
                // Arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => a + b,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => a - b,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => a * b,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => b != 0 ? a / b : 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => b != 0 ? a % b : 0,

                // Logical
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => a & b,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => a | b,
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => a ^ b,

                // Min/Max (signed)
                (uint)Processor.CPU_Core.InstructionsEnum.VMIN => Math.Min(a, b),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAX => Math.Max(a, b),

                _ => ThrowUnknownReferenceOpcode<long>(nameof(BinarySignedInt), opCode)
            };
        }

        /// <summary>
        /// Reference implementation of binary unsigned integer operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BinaryUnsignedInt(uint opCode, ulong a, ulong b)
        {
            return opCode switch
            {
                // Arithmetic
                (uint)Processor.CPU_Core.InstructionsEnum.VADD => a + b,
                (uint)Processor.CPU_Core.InstructionsEnum.VSUB => a - b,
                (uint)Processor.CPU_Core.InstructionsEnum.VMUL => a * b,
                (uint)Processor.CPU_Core.InstructionsEnum.VDIV => b != 0 ? a / b : 0,
                (uint)Processor.CPU_Core.InstructionsEnum.VMOD => b != 0 ? a % b : 0,

                // Logical
                (uint)Processor.CPU_Core.InstructionsEnum.VAND => a & b,
                (uint)Processor.CPU_Core.InstructionsEnum.VOR => a | b,
                (uint)Processor.CPU_Core.InstructionsEnum.VXOR => a ^ b,

                // Min/Max (unsigned)
                (uint)Processor.CPU_Core.InstructionsEnum.VMINU => Math.Min(a, b),
                (uint)Processor.CPU_Core.InstructionsEnum.VMAXU => Math.Max(a, b),

                _ => ThrowUnknownReferenceOpcode<ulong>(nameof(BinaryUnsignedInt), opCode)
            };
        }

        private static T ThrowUnknownReferenceOpcode<T>(string operationClass, uint opCode)
        {
            string opcodeIdentifier = $"0x{opCode:X}";
            throw new InvalidOpcodeException(
                $"ReferenceModel {operationClass} rejected unsupported opcode {opcodeIdentifier}. " +
                "Reference validation must fail closed instead of projecting a neutral value.",
                opcodeIdentifier,
                slotIndex: -1,
                isProhibited: false);
        }

        /// <summary>
        /// Compare two floating-point values with tolerance.
        /// Returns true if values are "close enough" considering floating-point precision.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCloseFloat32(float a, float b, float relativeTolerance = 1e-5f)
        {
            // Handle exact equality (including infinity and zero)
            if (a == b) return true;

            // Handle NaN: both must be NaN
            if (float.IsNaN(a) && float.IsNaN(b)) return true;
            if (float.IsNaN(a) || float.IsNaN(b)) return false;

            // Relative difference check
            float diff = MathF.Abs(a - b);
            float absA = MathF.Abs(a);
            float absB = MathF.Abs(b);
            float largest = Math.Max(absA, absB);

            return diff <= largest * relativeTolerance;
        }

        /// <summary>
        /// Compare two double-precision values with tolerance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCloseFloat64(double a, double b, double relativeTolerance = 1e-10)
        {
            // Handle exact equality (including infinity and zero)
            if (a == b) return true;

            // Handle NaN: both must be NaN
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;

            // Relative difference check
            double diff = Math.Abs(a - b);
            double absA = Math.Abs(a);
            double absB = Math.Abs(b);
            double largest = Math.Max(absA, absB);

            return diff <= largest * relativeTolerance;
        }

        /// <summary>
        /// Compare two narrow floating-point values decoded to double.
        /// Used for FLOAT16, BFLOAT16, FLOAT8_E4M3, FLOAT8_E5M2 validation.
        /// Tolerance is derived from the mantissa bit width of the target format.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCloseNarrowFloat(double a, double b, DataTypeEnum dataType)
        {
            // Tolerance based on mantissa precision of target format
            double relativeTolerance = dataType switch
            {
                DataTypeEnum.FLOAT16    => 1e-3,   // 10 mantissa bits
                DataTypeEnum.BFLOAT16   => 8e-3,   // 7 mantissa bits
                DataTypeEnum.FLOAT8_E4M3 => 0.125, // 3 mantissa bits
                DataTypeEnum.FLOAT8_E5M2 => 0.25,  // 2 mantissa bits
                _ => throw ExecutionFaultContract.CreateReferenceModelValidationException(
                    nameof(dataType),
                    dataType,
                    $"AreCloseNarrowFloat not applicable to {dataType}")
            };

            if (a == b) return true;
            if (double.IsNaN(a) && double.IsNaN(b)) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;

            double diff = Math.Abs(a - b);
            double largest = Math.Max(Math.Abs(a), Math.Abs(b));

            return diff <= largest * relativeTolerance;
        }

        /// <summary>
        /// Validate VectorALU result against ReferenceModel.
        /// Used for self-checking in debug mode.
        /// </summary>
        /// <param name="opCode">Operation code</param>
        /// <param name="dataType">Element data type</param>
        /// <param name="actual">Actual result from VectorALU</param>
        /// <param name="expected">Expected result from ReferenceModel</param>
        /// <param name="elementIndex">Index of element being validated</param>
        /// <returns>True if results match within tolerance</returns>
        public static bool ValidateResult(
            uint opCode,
            DataTypeEnum dataType,
            ReadOnlySpan<byte> actual,
            ReadOnlySpan<byte> expected,
            int elementIndex)
        {
            int elemSize = Arch.DataTypeUtils.SizeOf(dataType);
            int offset = elementIndex * elemSize;

            if (Arch.DataTypeUtils.IsFloatingPoint(dataType))
            {
                if (dataType == DataTypeEnum.FLOAT32)
                {
                    float actualVal = BitConverter.ToSingle(actual.Slice(offset, 4));
                    float expectedVal = BitConverter.ToSingle(expected.Slice(offset, 4));
                    return AreCloseFloat32(actualVal, expectedVal);
                }
                else if (dataType == DataTypeEnum.FLOAT64)
                {
                    double actualVal = BitConverter.ToDouble(actual.Slice(offset, 8));
                    double expectedVal = BitConverter.ToDouble(expected.Slice(offset, 8));
                    return AreCloseFloat64(actualVal, expectedVal);
                }
                else
                {
                    // Narrow FP types: FLOAT16, BFLOAT16, FLOAT8_E4M3, FLOAT8_E5M2
                    // Decode raw bytes to double via ElementCodec, then compare with
                    // format-appropriate tolerance.
                    double actualVal = ElementCodec.LoadF(actual, offset, dataType);
                    double expectedVal = ElementCodec.LoadF(expected, offset, dataType);
                    return AreCloseNarrowFloat(actualVal, expectedVal, dataType);
                }
            }
            else
            {
                // For integers, expect exact match
                for (int i = 0; i < elemSize; i++)
                {
                    if (actual[offset + i] != expected[offset + i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
