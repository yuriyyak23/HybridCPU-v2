using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            /// <summary>
            /// Pipeline stage for instruction fetch
            /// </summary>
            public struct FetchStage
            {
                public bool Valid;              // Is this stage occupied?
                public ulong PC;                // Program counter (instruction pointer)
                public byte[] VLIWBundle;       // Fetched 256-byte VLIW bundle
                public VliwBundleAnnotations? BundleAnnotations;
                public bool HasBundleAnnotations;
                public bool PrefetchComplete;   // Has prefetch completed?

                public void Clear()
                {
                    Valid = false;
                    PC = 0;
                    VLIWBundle = Array.Empty<byte>();
                    BundleAnnotations = null;
                    HasBundleAnnotations = false;
                    PrefetchComplete = false;
                }
            }
        }
    }
}
