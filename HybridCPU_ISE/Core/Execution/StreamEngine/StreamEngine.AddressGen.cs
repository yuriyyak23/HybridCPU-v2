using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Execution
{
    /// <summary>
    /// Port type for stream operations - used for widening address calculations.
    /// </summary>
    public enum PortType
    {
        SourceA,
        SourceB,
        Destination
    }

    /// <summary>
    /// Address generation for stream operations.
    /// Supports: 1D (stride), 2D (row/column), and indexed (gather/scatter).
    /// Supports widening operations (FP8 → FP32) via asymmetric addressing.
    ///
    /// Design goals:
    /// - HLS-friendly: deterministic, no dynamic allocation
    /// - Compiler-oriented: explicit modes via instruction flags
    /// - AXI4-aware: can split addresses at 4KB boundaries
    /// - Widening-aware: supports asymmetric element sizes for source and destination
    /// </summary>
    internal static class AddressGen
    {
        /// <summary>
        /// Generate address for 1D strided access.
        /// Simple pattern: base + (element_index * stride)
        /// </summary>
        /// <param name="baseAddr">Base address</param>
        /// <param name="elementIndex">Element index in stream</param>
        /// <param name="stride">Byte stride between elements</param>
        /// <returns>Physical address for element</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Gen1D(ulong baseAddr, ulong elementIndex, ushort stride)
        {
            return baseAddr + (elementIndex * stride);
        }

        /// <summary>
        /// Generate address for 2D (row-major) access.
        /// Pattern: base + (row * rowStride) + (col * colStride)
        ///
        /// Instruction encoding for 2D (when Is2D=1):
        /// - RowStride: byte pitch between rows
        /// - Stride: byte stride between columns (element stride)
        /// - Immediate: number of elements per row (rowLength)
        /// - StreamLength: total elements (rows * cols)
        ///
        /// Compute: row = elementIndex / rowLength
        ///          col = elementIndex % rowLength
        /// </summary>
        /// <param name="baseAddr">Base address</param>
        /// <param name="elementIndex">Linear element index</param>
        /// <param name="rowLength">Number of elements per row</param>
        /// <param name="rowStride">Byte pitch between rows</param>
        /// <param name="colStride">Byte stride between columns</param>
        /// <returns>Physical address for element</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Gen2D(ulong baseAddr, ulong elementIndex, uint rowLength, ushort rowStride, ushort colStride)
        {
            if (rowLength == 0) return baseAddr; // Safety: avoid division by zero

            ulong row = elementIndex / rowLength;
            ulong col = elementIndex % rowLength;

            return baseAddr + (row * rowStride) + (col * colStride);
        }

        /// <summary>
        /// Check if address crosses a 4KB boundary.
        /// AXI4 rule: burst cannot cross 4KB boundaries.
        /// Used to split large transfers into multiple bursts.
        /// </summary>
        /// <param name="startAddr">Start address of transfer</param>
        /// <param name="length">Length of transfer in bytes</param>
        /// <returns>True if transfer crosses 4KB boundary</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Crosses4KBBoundary(ulong startAddr, ulong length)
        {
            ulong endAddr = startAddr + length - 1;
            ulong startPage = startAddr >> 12; // Divide by 4096
            ulong endPage = endAddr >> 12;
            return startPage != endPage;
        }

        /// <summary>
        /// Compute bytes until next 4KB boundary from given address.
        /// Used to determine maximum burst size without crossing boundary.
        /// </summary>
        /// <param name="addr">Starting address</param>
        /// <returns>Bytes remaining in current 4KB page</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BytesTo4KBBoundary(ulong addr)
        {
            ulong offset = addr & 0xFFF; // Offset within 4KB page
            return 4096 - offset;
        }

        /// <summary>
        /// Compute maximum number of elements that can be read in one burst
        /// without crossing 4KB boundary.
        /// </summary>
        /// <param name="startAddr">Starting address</param>
        /// <param name="elementSize">Size of each element in bytes</param>
        /// <param name="maxElements">Maximum elements requested</param>
        /// <returns>Actual number of elements to read in this burst</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ComputeBurstLength(ulong startAddr, int elementSize, ulong maxElements)
        {
            if (elementSize <= 0) return 0;

            ulong bytesAvailable = BytesTo4KBBoundary(startAddr);
            ulong elementsInPage = bytesAvailable / (ulong)elementSize;

            // Return minimum of: elements in page, max elements, AXI4 limit (256)
            ulong result = elementsInPage;
            if (result > maxElements) result = maxElements;
            if (result > 256) result = 256; // AXI4 burst limit

            return result;
        }

        // ========================================================================
        // Widening Operation Support (FP8 → FP32)
        // ========================================================================

        /// <summary>
        /// Check if opcode is a widening operation (reads narrow elements, writes wide result).
        /// Widening operations: VDOT_FP8 with FP8 DataType (read 1-byte FP8, write 4-byte FP32).
        /// Hardware MUX for HLS synthesis (no branches).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWideningOpcode(uint opCode)
        {
            var op = (Processor.CPU_Core.InstructionsEnum)opCode;
            // Hardware-level decision: VDOT_FP8 widens from 1-byte to 4-byte (DataType selects FP8 format)
            return op == Processor.CPU_Core.InstructionsEnum.VDOT_FP8;
        }

        /// <summary>
        /// Get effective element size for address generation based on port type.
        /// For widening operations: source reads 1-byte, destination writes 4-byte.
        /// Returns effective size via pure combinational MUX (HLS-friendly).
        /// </summary>
        /// <param name="opCode">Instruction opcode</param>
        /// <param name="port">Port type (SourceA, SourceB, or Destination)</param>
        /// <param name="baseElementSize">Base element size from data type (1 byte for FP8)</param>
        /// <returns>Effective element size for this port (1 or 4 bytes)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetEffectiveElementSize(uint opCode, PortType port, byte baseElementSize)
        {
            // Ternary operator compiles to pure MUX in HLS:
            // If widening opcode AND destination port, use 4 bytes, else use base size
            return (IsWideningOpcode(opCode) && port == PortType.Destination) ? 4u : (uint)baseElementSize;
        }
    }
}
