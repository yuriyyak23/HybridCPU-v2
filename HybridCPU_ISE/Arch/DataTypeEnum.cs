namespace HybridCPU_ISE.Arch
{
    /// <summary>
    /// Canonical architectural data type surface shared by ISA encoding,
    /// execution helpers, and compat transport adapters.
    /// </summary>
    public enum DataTypeEnum : byte
    {
        INT8 = 0,       // Signed 8-bit integer
        UINT8 = 1,      // Unsigned 8-bit integer
        INT16 = 2,      // Signed 16-bit integer
        UINT16 = 3,     // Unsigned 16-bit integer
        INT32 = 4,      // Signed 32-bit integer
        UINT32 = 5,     // Unsigned 32-bit integer
        FLOAT32 = 6,    // 32-bit floating point
        INT64 = 7,      // Signed 64-bit integer
        UINT64 = 8,     // Unsigned 64-bit integer
        FLOAT64 = 9,    // 64-bit floating point (double)
        FLOAT16 = 10,   // 16-bit floating point (IEEE 754 half precision) - ML/GPU optimized
        BFLOAT16 = 11,  // 16-bit Brain Float (truncated FP32 mantissa) - ML/DL optimized
        FLOAT8_E4M3 = 12, // 8-bit floating point (NVIDIA E4M3 format) - AI/ML optimized
        FLOAT8_E5M2 = 13  // 8-bit floating point (NVIDIA E5M2 format) - AI/ML optimized
    }
}
