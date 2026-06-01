using System.Buffers.Binary;

namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
            private bool TryReadBytesCore(ReadOnlySpan<byte> source, int offset)
            {
                if (source.Length - offset < 32)
                    return false;

                word0 = ValidateWord0ForProductionIngress(
                    BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8)));
                word1 = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset + 8, 8));
                word2 = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset + 16, 8));

                ulong rawWord3 = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset + 24, 8));
                word3 = ValidateWord3ForProductionIngress(rawWord3);
                return true;
            }

            /// <summary>
            /// HLS-compatible: Write instruction to pre-allocated buffer.
            /// No dynamic allocation, uses Span for zero-copy operation.
            /// The canonical VLIW carrier byte order is little-endian.
            /// </summary>
            /// <param name="destination">Pre-allocated buffer (must be >= 32 bytes)</param>
            /// <returns>True if successful, false if buffer too small</returns>
            public bool TryWriteBytes(Span<byte> destination)
            {
                if (destination.Length < 32)
                    return false;

                BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(0, 8), word0);
                BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), word1);
                BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), word2);
                BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(24, 8), word3);
                return true;
            }

            /// <summary>
            /// HLS-compatible: Read instruction from buffer.
            /// Returns false for short buffers. Invalid production-ingress carrier bits
            /// fail closed with <see cref="YAKSys_Hybrid_CPU.Arch.InvalidOpcodeException"/>.
            /// The canonical VLIW carrier byte order is little-endian.
            /// </summary>
            /// <param name="source">Source buffer (must be >= 32 bytes from offset)</param>
            /// <param name="offset">Offset in buffer</param>
            /// <returns>True if successful, false if buffer too small</returns>
            public bool TryReadBytes(ReadOnlySpan<byte> source, int offset = 0)
            {
                return TryReadBytesCore(source, offset);
            }
    }
}
