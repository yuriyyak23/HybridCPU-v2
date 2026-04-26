namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Authoritative runtime timebase exported from the live pipeline contour.
    /// When the pipeline has not yet published timing facts, callers receive an
    /// explicit unavailable snapshot instead of falling back to legacy counters.
    /// </summary>
    public readonly struct CoreTimebaseSnapshot
    {
        public CoreTimebaseSnapshot(
            ulong cycleCount,
            bool isStalled,
            bool isAvailable,
            string unavailableReason)
        {
            CycleCount = cycleCount;
            IsStalled = isStalled;
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason ?? string.Empty;
        }

        public ulong CycleCount { get; }
        public bool IsStalled { get; }
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
    }
}
