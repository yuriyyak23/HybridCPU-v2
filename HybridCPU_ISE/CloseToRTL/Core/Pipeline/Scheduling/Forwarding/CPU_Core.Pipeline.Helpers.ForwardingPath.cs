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
            /// Data forwarding/bypass path from one stage to another.
            /// Enables resolving data hazards without stalling.
            /// Phase 2: Added cycle-accurate latency modeling (1-2 cycles)
            /// </summary>
            public struct ForwardingPath
            {
                public bool Valid;              // Is forwarding data available?
                public ushort DestRegID;        // Which register is being forwarded?
                public ulong ForwardedValue;    // The value to forward
                public long AvailableCycle;     // Phase 2: Cycle when data becomes available
                public PipelineStage SourceStage; // Phase 2: Where is data coming from?

                public void Clear()
                {
                    Valid = false;
                    DestRegID = 0;
                    ForwardedValue = 0;
                    AvailableCycle = 0;
                    SourceStage = PipelineStage.None;
                }

                /// <summary>
                /// Check if forwarding data is available at the given cycle.
                /// (Phase 2: Requirement 2.3)
                /// </summary>
                public bool IsAvailable(long currentCycle)
                {
                    return Valid && currentCycle >= AvailableCycle;
                }

                /// <summary>
                /// Calculate delay in cycles until data is available.
                /// (Phase 2: Requirement 2.3)
                /// </summary>
                public int GetDelayCycles(long currentCycle)
                {
                    if (!Valid) return -1;
                    if (currentCycle >= AvailableCycle) return 0;
                    return (int)(AvailableCycle - currentCycle);
                }
            }
        }
    }
}
