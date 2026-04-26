using System.Numerics;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Replay-stable lane selector for ClassFlexible operations.
    /// Applies fixed priority and replay-hint rules inside the supplied free-lane mask.
    /// This is deterministic within that local replay/evidence envelope; it is not
    /// a global determinism claim over every runtime state dimension.
    /// <para>
    /// HLS: 8-bit priority encoder (1 LUT layer, ~0.5 ns at 7-series).
    /// Pure combinational — no state, no allocation, no side effects.
    /// </para>
    /// </summary>
    public static class DeterministicLaneChooser
    {
        /// <summary>
        /// Tier 1: Select the lowest free physical lane from a bitmask.
        /// <para>HLS: 8-bit priority encoder = 3 LUTs, 1 combinational layer.</para>
        /// </summary>
        /// <param name="freeLanes">Bitmask of available lanes (already intersected with class mask).</param>
        /// <returns>Selected lane index (0–7), or -1 if no free lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SelectLowestFree(byte freeLanes)
        {
            if (freeLanes == 0) return -1;
            return BitOperations.TrailingZeroCount(freeLanes);
        }

        /// <summary>
        /// Select with optional replay-phase lane reuse (Tier 2).
        /// If replay is active and the previous lane is free, reuse it.
        /// Otherwise fallback to <see cref="SelectLowestFree"/>.
        /// The claim is bounded to this call's free-lane mask and previous-lane hint.
        /// <para>
        /// HLS: 3-bit comparator + AND gate + 2-to-1 MUX = &lt; 6 LUTs total.
        /// </para>
        /// </summary>
        /// <param name="freeLanes">Available lanes bitmask.</param>
        /// <param name="replayActive">Whether a replay phase is currently active.</param>
        /// <param name="previousLane">Lane used in previous replay iteration (-1 if unknown).</param>
        /// <returns>Selected lane index (0–7), or -1 if no free lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SelectWithReplayHint(byte freeLanes, bool replayActive, int previousLane)
        {
            if (freeLanes == 0) return -1;

            // Tier 2: replay lane reuse
            if (replayActive && previousLane >= 0 && previousLane < 8)
            {
                if ((freeLanes & (1 << previousLane)) != 0)
                    return previousLane;
            }

            // Tier 1: lowest free
            return BitOperations.TrailingZeroCount(freeLanes);
        }
    }
}
