using YAKSys_Hybrid_CPU.Core.Decoder;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned containment for per-core loop replay and decode-context
/// identity. It is not a pipeline image, rename checkpoint or rollback owner.
/// </summary>
internal sealed class ReplayState
{
    internal LoopBuffer LoopBuffer;
    internal ulong CodeGenerationEpoch;
    internal ulong ObservedRelevantMemoryEpoch;
    internal ReplaySemanticShadowLookup? SemanticShadowLookup;
}
