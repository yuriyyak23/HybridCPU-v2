using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;

public sealed class Lane7AcceleratorCompletionRouter
{
    private readonly Lane7StateBlock _lane7;

    internal Lane7AcceleratorCompletionRouter(Lane7StateBlock lane7)
    {
        _lane7 = lane7 ?? throw new ArgumentNullException(nameof(lane7));
    }

    public bool TryBuildCompletion(
        AcceleratorToken token,
        ushort sourceOpcode,
        ulong runtimeQueueSequence,
        out LaneCompletionDescriptor completion,
        out Lane7Fault fault) =>
        _lane7.TryBuildCompletion(
            token,
            sourceOpcode,
            runtimeQueueSequence,
            out completion,
            out fault);
}
