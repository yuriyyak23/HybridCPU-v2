namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned storage for the current fetch latch, live frontend cursor
/// and private fetch helpers. Decode progress, committed architectural PC,
/// cache arrays/replacement state and admission are intentionally excluded.
/// </summary>
internal sealed class FrontendState
{
    internal Processor.CPU_Core.FetchStage Fetch;
    internal byte[]? FetchVliwBuffer;
    internal Processor.CPU_Core.BranchPredictor BranchPredictor;
    internal ulong ActiveLivePc;
    internal bool HasMaterializedVliwFetchState;
}
