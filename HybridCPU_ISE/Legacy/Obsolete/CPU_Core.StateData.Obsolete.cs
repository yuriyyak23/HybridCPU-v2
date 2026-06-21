using System;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <param name="CoreID">Zero-based hardware core index.</param>
            /// <param name="mainMemory">Optional main-memory instance. When null,
            /// falls back to the legacy global <see cref="Processor.MainMemory"/> adapter.</param>
            [Obsolete("Use CPU_Core(ushort coreId, CpuCorePlatformContext platformContext) for new production paths. This overload keeps legacy global runtime fallback for compat callers.")]
            public CPU_Core(ushort CoreID, Processor.MainMemoryArea? mainMemory = null)
                : this(CoreID, CpuCorePlatformContext.CreateLegacy(mainMemory))
            {
            }
        }
    }
}
