namespace YAKSys_Hybrid_CPU.Core.Memory
{
    /// <summary>
    /// Deterministic half-open range helpers for modeled cache/SRF invalidation.
    /// Overflowing ranges are treated as malformed instead of wrapping.
    /// </summary>
    public static class MemoryRangeOverlap
    {
        public static bool IsNonEmptyWellFormed(ulong address, ulong length) =>
            length != 0 && length <= ulong.MaxValue - address;

        public static bool TryGetEndExclusive(
            ulong address,
            ulong length,
            out ulong endExclusive)
        {
            if (length == 0 || length > ulong.MaxValue - address)
            {
                endExclusive = address;
                return false;
            }

            endExclusive = address + length;
            return true;
        }

        public static bool RangesOverlap(
            ulong leftAddress,
            ulong leftLength,
            ulong rightAddress,
            ulong rightLength)
        {
            if (!TryGetEndExclusive(leftAddress, leftLength, out ulong leftEnd) ||
                !TryGetEndExclusive(rightAddress, rightLength, out ulong rightEnd))
            {
                return false;
            }

            return leftAddress < rightEnd && rightAddress < leftEnd;
        }
    }
}
