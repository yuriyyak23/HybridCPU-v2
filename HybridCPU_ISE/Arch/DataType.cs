using HybridCPU_ISE.Arch;

using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Data type utilities for Hybrid CPU architecture.
    /// <see cref="DataTypeEnum"/> in this directory is the canonical source of truth;
    /// compat containers project into it but do not own it.
    /// Provides type classification and sizing for hardware-oriented code generation.
    /// Compatible with HLS synthesis (no dynamic allocations, pure functions).
    /// </summary>
    public static class DataTypeUtils
    {
        /// <summary>
        /// Get element size in bytes for a given data type.
        /// HLS-friendly: inline, no branches that depend on runtime values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(DataTypeEnum dataType)
        {
            return dataType switch
            {
                DataTypeEnum.INT8 => 1,
                DataTypeEnum.UINT8 => 1,
                DataTypeEnum.FLOAT8_E4M3 => 1,
                DataTypeEnum.FLOAT8_E5M2 => 1,
                DataTypeEnum.INT16 => 2,
                DataTypeEnum.UINT16 => 2,
                DataTypeEnum.FLOAT16 => 2,
                DataTypeEnum.BFLOAT16 => 2,
                DataTypeEnum.INT32 => 4,
                DataTypeEnum.UINT32 => 4,
                DataTypeEnum.FLOAT32 => 4,
                DataTypeEnum.INT64 => 8,
                DataTypeEnum.UINT64 => 8,
                DataTypeEnum.FLOAT64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, $"Unknown data type: {dataType}")
            };
        }

        /// <summary>
        /// Check if data type is signed integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSignedInteger(DataTypeEnum dataType)
        {
            return dataType == DataTypeEnum.INT8 ||
                   dataType == DataTypeEnum.INT16 ||
                   dataType == DataTypeEnum.INT32 ||
                   dataType == DataTypeEnum.INT64;
        }

        /// <summary>
        /// Check if data type is unsigned integer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUnsignedInteger(DataTypeEnum dataType)
        {
            return dataType == DataTypeEnum.UINT8 ||
                   dataType == DataTypeEnum.UINT16 ||
                   dataType == DataTypeEnum.UINT32 ||
                   dataType == DataTypeEnum.UINT64;
        }

        /// <summary>
        /// Check if data type is floating point.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFloatingPoint(DataTypeEnum dataType)
        {
            return dataType == DataTypeEnum.FLOAT8_E4M3 ||
                   dataType == DataTypeEnum.FLOAT8_E5M2 ||
                   dataType == DataTypeEnum.FLOAT16 ||
                   dataType == DataTypeEnum.BFLOAT16 ||
                   dataType == DataTypeEnum.FLOAT32 ||
                   dataType == DataTypeEnum.FLOAT64;
        }

        /// <summary>
        /// Check if data type is any integer type (signed or unsigned).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInteger(DataTypeEnum dataType)
        {
            return IsSignedInteger(dataType) || IsUnsignedInteger(dataType);
        }

        /// <summary>
        /// Get the natural bit width of a data type (8, 16, 32, or 64).
        /// Used for shift operations and mask generation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitWidth(DataTypeEnum dataType)
        {
            return SizeOf(dataType) * 8;
        }

        /// <summary>
        /// Returns true if the data type is a recognized architecture-defined value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(DataTypeEnum dataType)
        {
            return Enum.IsDefined(dataType);
        }
    }
}
