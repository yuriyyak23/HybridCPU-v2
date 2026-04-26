using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pipeline stage enum for forwarding source tracking (Phase 2)
            /// </summary>
            public enum PipelineStage
            {
                None = 0,
                Fetch = 1,
                Decode = 2,
                Execute = 3,
                Memory = 4,
                WriteBack = 5
            }
        }
    }
}
