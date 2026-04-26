using System.Runtime.InteropServices;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Arch
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public partial struct VLIW_Instruction
    {
            // V6 B10: VLIW_Instruction is a fixed-width bitfield container (4 × 64-bit
            // words) that encodes the opcode as an opaque 16-bit field in word0[63:48].  Adding new
            // opcodes to InstructionsEnum never requires changes to this struct — the struct is
            // fully decoupled from opcode-space evolution.  Architectural classification
            // (IsControlFlow, IsMathOrVector, …) lives exclusively in OpcodeInfo/OpcodeRegistry.
            //
            // V6 B11: word3[50] is a retired legacy scheduling-policy bit, NOT an
            // architectural flag; word3[49:48] = VirtualThreadId is a transport hint only,
            // NOT ISA state. These bits must not influence functional decode. Architectural flags
            // (Acquire/Release/Saturating/MaskAgnostic/…) are encoded in word0[23:16].
            private ulong word0; // [63:48] OpCode (16 bit) | [47:40] Reserved (8 bit) | [39:32] DataType (8 bit) | [31:24] PredicateMask (8 bit) | [23:16] Flags (8 bit) | [15:0] Immediate (16 bit)
            private ulong word1; // Dest / Src1 Pointer
            private ulong word2; // Src2 Pointer / Index Array Pointer (if Indexed)
            private ulong word3; // [63:51] RowStride (13 bit) | [50] Reserved legacy policy gap (must be zero on production ingress) | [49:48] VirtualThreadId (transport hint only) | [47:16] StreamLength (32 bit) | [15:0] Stride (16 bit)

            public ulong Word0
            {
                get { return word0; }
                set { word0 = value; }
            }

            public ulong Word1
            {
                get { return word1; }
                set { word1 = value; }
            }

            public ulong Word2
            {
                get { return word2; }
                set { word2 = value; }
            }

            public ulong Word3
            {
                get { return word3; }
                set { word3 = value; }
            }

            // --- HLS-friendly Word 0 decoders ---
            public uint OpCode
            {
                get { return (uint)((word0 >> 48) & 0xFFFF); }
                set { word0 = (word0 & 0x0000FFFFFFFFFFFFUL) | ((ulong)(value & 0xFFFF) << 48); }
            }

            public byte DataType
            {
                get { return (byte)((word0 >> 32) & 0xFF); }
                set { word0 = (word0 & 0xFFFF00FFFFFFFFFFUL) | ((ulong)value << 32); }
            }

            // Helper property to get/set DataType as enum
            public DataTypeEnum DataTypeValue
            {
                get { return (DataTypeEnum)DataType; }
                set { DataType = (byte)value; }
            }

            public byte PredicateMask
            {
                get { return (byte)((word0 >> 24) & 0xFF); }
                set { word0 = (word0 & 0xFFFFFFFF00FFFFFFUL) | ((ulong)value << 24); }
            }

            public ushort Immediate
            {
                get { return (ushort)(word0 & 0xFFFF); }  // Bits [15:0] — 16 bits (max 65535)
                set { word0 = (word0 & 0xFFFFFFFFFFFF0000UL) | ((ulong)value & 0xFFFFUL); }
            }

            // --- Reserved [47:40] ---

            /// <summary>
            /// Reserved 8-bit future metadata field (bits [47:40]).
            /// </summary>
            public byte Reserved
            {
                get { return (byte)((word0 >> 40) & 0xFF); }
                set { word0 = (word0 & 0xFFFF00FFFFFFFFFFUL) | ((ulong)value << 40); }
            }

            // --- Addressing (Pointer Swapping) ---
            public ulong DestSrc1Pointer
            {
                get { return word1; }
                set { word1 = value; }
            }

            public ulong Src2Pointer
            {
                get { return word2; }
                set { word2 = value; }
            }

            // --- 32-bit Addressing for MemPtr_MemPtr_MemPtr_DataLength ---
            public uint SourceAPointer
            {
                get { return (uint)(word1 & 0xFFFFFFFFUL); }
                set { word1 = (word1 & 0xFFFFFFFF00000000UL) | value; }
            }

            public uint SourceBPointer
            {
                get { return (uint)((word1 >> 32) & 0xFFFFFFFFUL); }
                set { word1 = (word1 & 0x00000000FFFFFFFFUL) | ((ulong)value << 32); }
            }

            public uint DestinationPointer
            {
                get { return (uint)(word2 & 0xFFFFFFFFUL); }
                set { word2 = (word2 & 0xFFFFFFFF00000000UL) | value; }
            }

            public uint VectorDataLength
            {
                get { return (uint)((word2 >> 32) & 0xFFFFFFFFUL); }
                set { word2 = (word2 & 0x00000000FFFFFFFFUL) | ((ulong)value << 32); }
            }

            // --- Extended Word 3: RowStride (13 bit) + SMT hint fields (3 bit) + StreamLength (32 bit) + Stride (16 bit) ---
            // Word3 layout (PHASE 4 SMT EXTENSION):
            // [63:51] RowStride (13 bits, reduced from 16 bits - max 8191, sufficient for 2D operations)
            // [50]    Reserved legacy policy bit (1 bit) - must be zero on production ingress
            // [49:48] VirtualThreadId (2 bits, 0-3) - transport hint only, not an execution binding
            // [47:16] StreamLength (32 bits) - unchanged
            // [15:0]  Stride (16 bits) - unchanged

            public ushort RowStride
            {
                get { return (ushort)((word3 >> 51) & 0x1FFF); }  // 13 bits (max 8191)
                set
                {
                    if (value > 0x1FFF)
                        throw new ArgumentOutOfRangeException(nameof(value), "RowStride must be <= 8191 (13 bits)");
                    word3 = (word3 & 0x0007FFFFFFFFFFFFUL) | ((ulong)(value & 0x1FFF) << 51);
                }
            }

            /// <summary>
            /// Phase 4 SMT Extension: Virtual Thread ID (0-3) transport hint only.
            /// Preserved for compat ingress/egress and diagnostics; does not bind execution
            /// to a specific SMT context and does not participate in functional decode.
            /// </summary>
            public byte VirtualThreadId
            {
                get { return (byte)((word3 >> 48) & 0x3); }  // Bits [49:48] - 2 bits (0-3)
                set
                {
                    if (value > 3)
                        throw new ArgumentOutOfRangeException(nameof(value), "VirtualThreadId must be 0-3");
                    word3 = (word3 & 0xFFFFCFFFFFFFFFFFUL) | ((ulong)(value & 0x3) << 48);
                }
            }

            public uint StreamLength
            {
                get { return (uint)((word3 >> 16) & 0xFFFFFFFF); }
                set { word3 = (word3 & 0xFFFF00000000FFFFUL) | ((ulong)value << 16); }
            }

            public ushort Stride
            {
                get { return (ushort)(word3 & 0xFFFF); }
                set { word3 = (word3 & 0xFFFFFFFFFFFF0000UL) | value; }
            }

            public void Clear()
            {
                word0 = 0;
                word1 = 0;
                word2 = 0;
                word3 = 0;
            }
    }
}
