using System.Runtime.InteropServices;

namespace HybridCPU_ISE.Arch
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VLIW_Bundle
    {
        private VLIW_Instruction inst0;
        private VLIW_Instruction inst1;
        private VLIW_Instruction inst2;
        private VLIW_Instruction inst3;
        private VLIW_Instruction inst4;
        private VLIW_Instruction inst5;
        private VLIW_Instruction inst6;
        private VLIW_Instruction inst7;

        public VLIW_Instruction GetInstruction(int index)
        {
            switch (index)
            {
                case 0: return inst0;
                case 1: return inst1;
                case 2: return inst2;
                case 3: return inst3;
                case 4: return inst4;
                case 5: return inst5;
                case 6: return inst6;
                case 7: return inst7;
                default: throw new ArgumentOutOfRangeException("index", "Bundle index must be 0-7");
            }
        }

        public void SetInstruction(int index, VLIW_Instruction instruction)
        {
            instruction.ValidateRetiredPolicyGapBitForProductionIngress();
            SetInstructionUnchecked(index, instruction);
        }

        private void SetInstructionUnchecked(int index, VLIW_Instruction instruction)
        {
            switch (index)
            {
                case 0: inst0 = instruction; break;
                case 1: inst1 = instruction; break;
                case 2: inst2 = instruction; break;
                case 3: inst3 = instruction; break;
                case 4: inst4 = instruction; break;
                case 5: inst5 = instruction; break;
                case 6: inst6 = instruction; break;
                case 7: inst7 = instruction; break;
                default: throw new ArgumentOutOfRangeException("index", "Bundle index must be 0-7");
            }
        }

        /// <summary>
        /// HLS-compatible: Write bundle to pre-allocated buffer.
        /// No dynamic allocation, uses Span for zero-copy operation.
        /// </summary>
        /// <param name="destination">Pre-allocated buffer (must be >= 256 bytes)</param>
        /// <returns>True if successful, false if buffer too small</returns>
        public bool TryWriteBytes(Span<byte> destination)
        {
            if (destination.Length < 256)
                return false;

            inst0.TryWriteBytes(destination.Slice(0, 32));
            inst1.TryWriteBytes(destination.Slice(32, 32));
            inst2.TryWriteBytes(destination.Slice(64, 32));
            inst3.TryWriteBytes(destination.Slice(96, 32));
            inst4.TryWriteBytes(destination.Slice(128, 32));
            inst5.TryWriteBytes(destination.Slice(160, 32));
            inst6.TryWriteBytes(destination.Slice(192, 32));
            inst7.TryWriteBytes(destination.Slice(224, 32));
            return true;
        }

        /// <summary>
        /// HLS-compatible: Read bundle from buffer.
        /// No exceptions thrown, returns success flag.
        /// </summary>
        /// <param name="source">Source buffer (must be >= 256 bytes from offset)</param>
        /// <param name="offset">Offset in buffer</param>
        /// <returns>True if successful, false if buffer too small</returns>
        public bool TryReadBytes(ReadOnlySpan<byte> source, int offset = 0)
        {
            if (source.Length - offset < 256)
                return false;

            inst0.TryReadBytes(source, offset);
            inst1.TryReadBytes(source, offset + 32);
            inst2.TryReadBytes(source, offset + 64);
            inst3.TryReadBytes(source, offset + 96);
            inst4.TryReadBytes(source, offset + 128);
            inst5.TryReadBytes(source, offset + 160);
            inst6.TryReadBytes(source, offset + 192);
            inst7.TryReadBytes(source, offset + 224);

            return true;
        }
    }
}
