using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private MemorySubsystem? _memorySubsystem;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static MemorySubsystem? CaptureDefaultMemorySubsystem() => Processor.Memory;

            /// <summary>
            /// Returns the core-bound <see cref="MemorySubsystem"/> captured at construction time.
            /// Falls back to <see cref="Processor.Memory"/> on first access if the field was not
            /// yet populated (lazy capture, same pattern as <see cref="_mainMemory"/>).
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal MemorySubsystem? GetBoundMemorySubsystem()
            {
                // Unlike _mainMemory (non-nullable), _memorySubsystem may legitimately be null
                // when the processor runs without a MemorySubsystem.  We use a separate
                // sentinel to distinguish "not yet captured" from "captured as null".
                if (!_memorySubsystemCaptured)
                {
                    _memorySubsystem = CaptureDefaultMemorySubsystem();
                    _memorySubsystemCaptured = true;
                }

                return _memorySubsystem;
            }

            private bool _memorySubsystemCaptured;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int GetBoundMemorySubsystemCurrentQueuedRequests() =>
                GetBoundMemorySubsystem()?.CurrentQueuedRequests ?? 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int[]? GetBoundMemorySubsystemBankQueueDepths() =>
                GetBoundMemorySubsystem()?.GetBankQueueDepthsSnapshot();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal long GetBoundMemorySubsystemCurrentCycle() =>
                GetBoundMemorySubsystem()?.CurrentCycle ?? 0;
        }
    }
}
