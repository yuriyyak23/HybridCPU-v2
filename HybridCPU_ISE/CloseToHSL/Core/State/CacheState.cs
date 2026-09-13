namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned storage for CPU-local L1/L2 cache arrays and their existing
/// query/replacement cursors. This container owns no timing, coherence, PTW or
/// memory-publication authority.
/// </summary>
internal sealed class CacheState
{
    internal Processor.CPU_Core.Cache_VLIWBundle_Object[] L1VliwBundles = null!;
    internal Processor.CPU_Core.Cache_Data_Object[] L1Data = null!;
    internal Processor.CPU_Core.Cache_VLIWBundle_Object[] L2VliwBundles =
        new Processor.CPU_Core.Cache_VLIWBundle_Object[65536];
    internal Processor.CPU_Core.Cache_Data_Object[] L2Data =
        new Processor.CPU_Core.Cache_Data_Object[65536];

    internal ulong MinimumL1Query;
    internal ulong MinimumL2Query;
    internal ulong CurrentVliwBundlePosition;
    internal ulong CurrentDataObjectPosition;
}
