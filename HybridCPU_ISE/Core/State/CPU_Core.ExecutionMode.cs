using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
        public partial struct Processor
        {
            public partial struct CPU_Core
            {
            private CpuCorePlatformContext _platformContext;
            private ProcessorMode _executionMode;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsEmulationExecutionMode() => _executionMode == ProcessorMode.Emulation;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsCompilerExecutionMode() => _executionMode == ProcessorMode.Compiler;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SynchronizeExecutionMode()
            {
                if (_platformContext.IsConfigured)
                {
                    _executionMode = _platformContext.ResolveExecutionMode();
                    return;
                }

                _executionMode = Processor.CurrentProcessorMode;
            }
        }
    }
}
