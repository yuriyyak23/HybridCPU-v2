using System.Runtime.InteropServices;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Descriptors for complex addressing modes and multi-operand operations.
    /// These structures are read from memory when instruction flags (Indexed, etc.) are set.
    ///
    /// Design rationale:
    /// - Fixed 256-bit instruction format limits inline operands
    /// - Descriptors enable gather/scatter, FMA, 2D patterns without format changes
    /// - Hardware reads descriptor as small burst before executing operation
    /// - Maintains deterministic, hardware-oriented execution model
    /// </summary>

    /// <summary>
    /// Descriptor for indexed binary operations (gather/scatter).
    /// Used when Indexed flag is set in instruction.
    ///
    /// Word1 in instruction: data base address (dest/src1)
    /// Word2 in instruction: descriptor address (points to this structure)
    ///
    /// Operation: dest[i] = op(dest[i], src2[index[i]])
    ///
    /// Index semantics:
    /// - If IndexIsByteOffset = 0: index[i] is element index → src2[index[i] * sizeof(element)]
    /// - If IndexIsByteOffset = 1: index[i] is byte offset → src2[index[i]]
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Indexed2SrcDesc
    {
        /// <summary>
        /// Base address of second source operand array.
        /// </summary>
        public ulong Src2Base;

        /// <summary>
        /// Base address of index array.
        /// Each index element selects which src2 element to use.
        /// </summary>
        public ulong IndexBase;

        /// <summary>
        /// Byte stride between index elements.
        /// If 0, uses sizeof(index type).
        /// </summary>
        public ushort IndexStride;

        /// <summary>
        /// Index element type:
        /// 0 = uint32 (4 bytes)
        /// 1 = uint64 (8 bytes)
        /// </summary>
        public byte IndexType;

        /// <summary>
        /// Index interpretation mode:
        /// 0 = Element index (multiply by element size to get byte offset)
        /// 1 = Byte offset (use index value directly as byte offset)
        ///
        /// Default (0) is element index, which is more intuitive and matches
        /// typical gather/scatter semantics in vector ISAs like RVV.
        /// Byte offset mode (1) provides flexibility for complex addressing patterns.
        /// </summary>
        public byte IndexIsByteOffset;
    }

    /// <summary>
    /// Descriptor for tri-operand operations (e.g., FMA: a = a + b * c).
    /// Used when descriptor mode is enabled for FMA-class instructions.
    ///
    /// Word1 in instruction: accumulator/destination base address
    /// Word2 in instruction: descriptor address (points to this structure)
    ///
    /// Operation: acc[i] = acc[i] + srcA[i] * srcB[i]
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TriOpDesc
    {
        /// <summary>
        /// Base address of first source operand (multiplicand).
        /// </summary>
        public ulong SrcA;

        /// <summary>
        /// Base address of second source operand (multiplier).
        /// </summary>
        public ulong SrcB;

        /// <summary>
        /// Byte stride between SrcA elements.
        /// If 0, uses element size from instruction DataType.
        /// </summary>
        public ushort StrideA;

        /// <summary>
        /// Byte stride between SrcB elements.
        /// If 0, uses element size from instruction DataType.
        /// </summary>
        public ushort StrideB;
    }
}
