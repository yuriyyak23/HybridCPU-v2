using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        private static IReadOnlyList<(ulong Address, ulong Length)> GetSafetyVisibleReadRanges(MicroOp microOp)
        {
            if (microOp == null)
            {
                return Array.Empty<(ulong Address, ulong Length)>();
            }

            IReadOnlyList<(ulong Address, ulong Length)> normalizedReadRanges =
                microOp.AdmissionMetadata.NormalizedReadMemoryRanges;
            if (normalizedReadRanges != null && normalizedReadRanges.Count != 0)
            {
                return normalizedReadRanges;
            }

            return microOp.ReadMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
        }
    }
}
