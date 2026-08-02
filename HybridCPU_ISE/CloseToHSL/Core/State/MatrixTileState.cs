namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Storage-only leaf for MatrixTile capture, stream-invalidation and replay
/// identity scalars. MatrixTile retire and replay protocols retain authority.
/// </summary>
internal sealed class MatrixTileState
{
    internal ulong StreamInvalidationCount;
    internal ulong NextCaptureOrdinal;
    internal ulong NextReplayCheckpointOrdinal;
    internal ulong ReplayInvalidationEpoch;
}
