namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
            // --- Flags in Word 0 [23:16] ---

            /// <summary>
            /// Per-instruction mask-agnostic policy bit (RVV ma).
            /// When false (0): undisturbed — masked-off elements preserve previous value.
            /// When true (1): agnostic — masked-off elements are UNDEFINED (allows optimization).
            /// Effective policy = instBit OR global VectorConfig.MaskAgnostic.
            /// </summary>
            public bool MaskAgnostic
            {
                get { return ((word0 >> 19) & 0x1UL) != 0; }  // Bit 19
                set { word0 = value ? (word0 | (1UL << 19)) : (word0 & ~(1UL << 19)); }
            }

            /// <summary>
            /// Per-instruction tail-agnostic policy bit (RVV ta).
            /// When false (0): undisturbed — tail elements (beyond VL) preserve previous value.
            /// When true (1): agnostic — tail elements are UNDEFINED (allows optimization).
            /// Effective policy = instBit OR global VectorConfig.TailAgnostic.
            /// In memory-to-memory model tail is naturally undisturbed (BurstWrite uses VL, not VLMAX).
            /// </summary>
            public bool TailAgnostic
            {
                get { return ((word0 >> 20) & 0x1UL) != 0; }  // Bit 20
                set { word0 = value ? (word0 | (1UL << 20)) : (word0 & ~(1UL << 20)); }
            }

            public bool Indexed
            {
                get { return ((word0 >> 21) & 0x1UL) != 0; }  // Bit 21
                set { word0 = value ? (word0 | (1UL << 21)) : (word0 & ~(1UL << 21)); }
            }

            public bool Is2D
            {
                get { return ((word0 >> 22) & 0x1UL) != 0; }  // Bit 22
                set { word0 = value ? (word0 | (1UL << 22)) : (word0 & ~(1UL << 22)); }
            }

            public bool Reduction
            {
                get { return ((word0 >> 23) & 0x1UL) != 0; }  // Bit 23
                set { word0 = value ? (word0 | (1UL << 23)) : (word0 & ~(1UL << 23)); }
            }

            // --- New v2 flags [18:16] ---

            /// <summary>
            /// Acquire memory ordering fence for atomic operations (bit 18).
            /// </summary>
            public bool Acquire
            {
                get { return ((word0 >> 18) & 0x1UL) != 0; }  // Bit 18
                set { word0 = value ? (word0 | (1UL << 18)) : (word0 & ~(1UL << 18)); }
            }

            /// <summary>
            /// Release memory ordering fence for atomic operations (bit 17).
            /// </summary>
            public bool Release
            {
                get { return ((word0 >> 17) & 0x1UL) != 0; }  // Bit 17
                set { word0 = value ? (word0 | (1UL << 17)) : (word0 & ~(1UL << 17)); }
            }

            /// <summary>
            /// Saturating arithmetic mode hint (bit 16).
            /// </summary>
            public bool Saturating
            {
                get { return ((word0 >> 16) & 0x1UL) != 0; }  // Bit 16
                set { word0 = value ? (word0 | (1UL << 16)) : (word0 & ~(1UL << 16)); }
            }
    }
}
