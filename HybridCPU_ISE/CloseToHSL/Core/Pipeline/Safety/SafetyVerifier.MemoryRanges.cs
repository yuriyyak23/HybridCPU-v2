using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        public static bool TryClassifyMemoryFootprintInvalidation(
            MicroOp writer,
            MicroOp replayEvidence,
            out ReplayPhaseInvalidationReason reason)
        {
            reason = ReplayPhaseInvalidationReason.None;
            if (writer == null || replayEvidence == null)
            {
                return false;
            }

            IReadOnlyList<(ulong Address, ulong Length)> writerRanges =
                writer.WriteMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
            if (writerRanges.Count == 0)
            {
                return false;
            }

            IReadOnlyList<(ulong Address, ulong Length)> evidenceReadRanges =
                GetSafetyVisibleReadRanges(replayEvidence);
            IReadOnlyList<(ulong Address, ulong Length)> evidenceWriteRanges =
                replayEvidence.WriteMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();

            if (!HasAnyMemoryRangeOverlap(writerRanges, evidenceReadRanges) &&
                !HasAnyMemoryRangeOverlap(writerRanges, evidenceWriteRanges))
            {
                return false;
            }

            reason = ReplayPhaseInvalidationReason.MemoryFootprintOverlap;
            return true;
        }

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

        private static bool HasAnyMemoryRangeOverlap(
            IReadOnlyList<(ulong Address, ulong Length)> leftRanges,
            IReadOnlyList<(ulong Address, ulong Length)> rightRanges)
        {
            if (leftRanges == null || rightRanges == null ||
                leftRanges.Count == 0 || rightRanges.Count == 0)
            {
                return false;
            }

            foreach ((ulong leftAddress, ulong leftLength) in leftRanges)
            {
                if (leftLength == 0)
                {
                    continue;
                }

                ulong leftEnd = SaturatingRangeEnd(leftAddress, leftLength);
                foreach ((ulong rightAddress, ulong rightLength) in rightRanges)
                {
                    if (rightLength == 0)
                    {
                        continue;
                    }

                    ulong rightEnd = SaturatingRangeEnd(rightAddress, rightLength);
                    if (leftAddress < rightEnd && rightAddress < leftEnd)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ulong SaturatingRangeEnd(ulong address, ulong length)
        {
            ulong remaining = ulong.MaxValue - address;
            return length > remaining ? ulong.MaxValue : address + length;
        }
    }
}
