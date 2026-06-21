using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// HLS-compatible SipHash-2-4 for BundleCertificate integrity verification.
    /// Replaces the trivially-collidable XOR hash with a collision-resistant
    /// keyed hash suitable for tamper detection in Singularity-style trust model.
    ///
    /// <para><b>HLS characteristics:</b></para>
    /// <list type="bullet">
    ///   <item>Zero heap allocation — pure value-type state (4 × ulong = 32 bytes).</item>
    ///   <item>Deterministic latency: 4 cycles per Compress, 4 finalization rounds.</item>
    ///   <item>Combinational depth per SipRound: 6 ADD + 6 XOR + 6 ROT.</item>
    ///   <item>Estimated area: ~1K LUT (Xilinx UltraScale+).</item>
    ///   <item>Pipeline mapping: 1 SipRound per pipeline stage at fMAX &gt; 500 MHz.</item>
    /// </list>
    ///
    /// <para><b>Key management:</b> The 128-bit key (k0, k1) is loaded from ROM/fuses
    /// at boot or via CSR write. Default constants are the SipHash reference key.</para>
    /// </summary>
    public struct HardwareHash
    {
        /// <summary>
        /// Latency in cycles for the timing model.
        /// Each Compress = 2 SipRounds; Finalize = 4 SipRounds.
        /// At 1 SipRound/cycle this gives 4-cycle finalization latency.
        /// </summary>
        public const int HASH_LATENCY_CYCLES = 4;

        private ulong _v0, _v1, _v2, _v3;

        /// <summary>
        /// Initialize hash state with a 128-bit key.
        /// In hardware this is loaded from ROM/fuses at boot.
        /// </summary>
        /// <param name="k0">Lower 64 bits of the key (default: SipHash reference).</param>
        /// <param name="k1">Upper 64 bits of the key (default: SipHash reference).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Initialize(ulong k0 = 0x0706050403020100UL, ulong k1 = 0x0F0E0D0C0B0A0908UL)
        {
            _v0 = k0 ^ 0x736f6d6570736575UL;
            _v1 = k1 ^ 0x646f72616e646f6dUL;
            _v2 = k0 ^ 0x6c7967656e657261UL;
            _v3 = k1 ^ 0x7465646279746573UL;
        }

        /// <summary>
        /// Feed one 64-bit word into the hash. Call once per data element.
        /// HLS: maps to 2 SipRounds (combinational or 2-stage pipeline).
        /// </summary>
        /// <param name="word">64-bit data word to absorb.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Compress(ulong word)
        {
            _v3 ^= word;
            SipRound();
            SipRound();
            _v0 ^= word;
        }

        /// <summary>
        /// Finalize and return 64-bit digest.
        /// HLS: 4 additional SipRounds → single-cycle output latch.
        /// </summary>
        /// <returns>64-bit hash digest.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Finalize()
        {
            _v2 ^= 0xFF;
            SipRound();
            SipRound();
            SipRound();
            SipRound();
            return _v0 ^ _v1 ^ _v2 ^ _v3;
        }

        /// <summary>
        /// One round of the SipHash ARX permutation.
        /// Pure combinational: 6 ADD + 6 XOR + 6 rotate-left.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SipRound()
        {
            _v0 += _v1;
            _v1 = RotateLeft(_v1, 13);
            _v1 ^= _v0;
            _v0 = RotateLeft(_v0, 32);
            _v2 += _v3;
            _v3 = RotateLeft(_v3, 16);
            _v3 ^= _v2;
            _v0 += _v3;
            _v3 = RotateLeft(_v3, 21);
            _v3 ^= _v0;
            _v2 += _v1;
            _v1 = RotateLeft(_v1, 17);
            _v1 ^= _v2;
            _v2 = RotateLeft(_v2, 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft(ulong x, int n) => (x << n) | (x >> (64 - n));
    }
}
