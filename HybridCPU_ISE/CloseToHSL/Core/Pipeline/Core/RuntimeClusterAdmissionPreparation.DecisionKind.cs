using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal enum RuntimeClusterAdmissionDecisionKind : byte
    {
        Empty = 0,
        AdvisoryReferenceSequential = 1,
        AdvisoryClusterCandidate = 2,
        AdvisoryAuxiliaryOnly = 3
    }
}
