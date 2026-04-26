using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal enum RuntimeClusterAdmissionExecutionMode : byte
    {
        Empty = 0,
        ReferenceSequential = 1,
        ClusterPrepared = 2,
        /// <summary>
        /// Diagnostics label for cluster-prepared decodes that fell back to the narrow reference path.
        /// This is not an authority for system/CSR/privileged execution semantics.
        /// </summary>
        ReferenceSequentialFallback = 3,
        AuxiliaryOnlyReference = 4,
        /// <summary>
        /// Stage 7 Phase B: cluster-prepared via refined (hazard-triage-aware) mask
        /// when conservative PreparedScalarMask == 0 but RefinedPreparedScalarMask != 0.
        /// </summary>
        ClusterPreparedRefined = 5
    }
}
