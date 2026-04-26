namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
            private bool TryReadBytesCore(ReadOnlySpan<byte> source, int offset)
            {
                if (source.Length - offset < 32)
                    return false;

                word0 = BitConverter.ToUInt64(source.Slice(offset, 8));
                word1 = BitConverter.ToUInt64(source.Slice(offset + 8, 8));
                word2 = BitConverter.ToUInt64(source.Slice(offset + 16, 8));

                ulong rawWord3 = BitConverter.ToUInt64(source.Slice(offset + 24, 8));
                word3 = ValidateWord3ForProductionIngress(rawWord3);
                return true;
            }

            /// <summary>
            /// HLS-compatible: Write instruction to pre-allocated buffer.
            /// No dynamic allocation, uses Span for zero-copy operation.
            /// </summary>
            /// <param name="destination">Pre-allocated buffer (must be >= 32 bytes)</param>
            /// <returns>True if successful, false if buffer too small</returns>
            public bool TryWriteBytes(Span<byte> destination)
            {
                if (destination.Length < 32)
                    return false;

                BitConverter.TryWriteBytes(destination.Slice(0, 8), word0);
                BitConverter.TryWriteBytes(destination.Slice(8, 8), word1);
                BitConverter.TryWriteBytes(destination.Slice(16, 8), word2);
                BitConverter.TryWriteBytes(destination.Slice(24, 8), word3);
                return true;
            }

            /// <summary>
            /// HLS-compatible: Read instruction from buffer.
            /// No exceptions thrown, returns success flag.
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
