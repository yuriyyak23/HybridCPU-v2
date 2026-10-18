namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Storage-only containment for the existing per-core GRLB, token,
/// scoreboard-reference and synchronization bookkeeping. Existing CPU_Core
/// methods retain acquire, release, reset and diagnostic authority.
/// </summary>
internal sealed class ResourceState
{
    internal ResourceBitset GlobalResourceLocks;
    internal ulong TokenGeneration;
    internal ulong[] ResourceTokens = new ulong[128];
    internal ulong StructuralStalls;
    internal ulong[] ResourceUsageCounts = new ulong[128];
    internal ulong[] ResourceContentionCounts = new ulong[128];
    internal byte[] ReadCounters = new byte[16];
    internal ulong SyncCounter;
    internal uint[] GrlbBanks = new uint[4];
    internal ulong[] BankContentionCounts = new ulong[4];
}
