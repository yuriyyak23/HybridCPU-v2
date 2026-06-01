using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Pipeline.Metadata
{
    public readonly record struct AssistCoalescingDescriptor(
        ulong BaseAddress,
        ulong PrefetchLength,
        int SourceRangeCount,
        int NormalizedRangeCount,
        ulong LargestGapBytes)
    {
        public static AssistCoalescingDescriptor None { get; } = new(
            BaseAddress: 0,
            PrefetchLength: 0,
            SourceRangeCount: 0,
            NormalizedRangeCount: 0,
            LargestGapBytes: 0);

        public bool IsValid => SourceRangeCount > 1 && PrefetchLength > 0;

        public bool IsNearContiguous => IsValid && LargestGapBytes > 0;
    }

    internal static class ReadRangeMetadataHelper
    {
        private const ulong AssistNearContiguousGapBytes = 32;

        internal static bool TryNormalizeContiguousReadRanges(
            IReadOnlyList<(ulong Address, ulong Length)> readMemoryRanges,
            out IReadOnlyList<(ulong Address, ulong Length)> normalizedReadMemoryRanges)
        {
            normalizedReadMemoryRanges = readMemoryRanges ?? Array.Empty<(ulong Address, ulong Length)>();
            if (readMemoryRanges == null || readMemoryRanges.Count <= 1)
            {
                return false;
            }

            var sortedRanges = CollectOrderedNonEmptyRanges(readMemoryRanges);
            if (sortedRanges.Count <= 1)
            {
                normalizedReadMemoryRanges = sortedRanges.Count == 0
                    ? Array.Empty<(ulong Address, ulong Length)>()
                    : new[] { sortedRanges[0] };
                return readMemoryRanges.Count != sortedRanges.Count;
            }

            var mergedRanges = new List<(ulong Address, ulong Length)>(sortedRanges.Count);
            ulong currentStart = sortedRanges[0].Address;
            ulong currentEnd = SaturatingAdd(sortedRanges[0].Address, sortedRanges[0].Length);

            for (int index = 1; index < sortedRanges.Count; index++)
            {
                (ulong nextStart, ulong nextLength) = sortedRanges[index];
                ulong nextEnd = SaturatingAdd(nextStart, nextLength);
                if (nextStart <= currentEnd)
                {
                    if (nextEnd > currentEnd)
                    {
                        currentEnd = nextEnd;
                    }

                    continue;
                }

                mergedRanges.Add((currentStart, currentEnd - currentStart));
                currentStart = nextStart;
                currentEnd = nextEnd;
            }

            mergedRanges.Add((currentStart, currentEnd - currentStart));
            if (mergedRanges.Count == sortedRanges.Count && HasSameRangeSequence(sortedRanges, readMemoryRanges))
            {
                return false;
            }

            normalizedReadMemoryRanges = mergedRanges.ToArray();
            return true;
        }

        internal static AssistCoalescingDescriptor BuildAssistCoalescingDescriptor(
            IReadOnlyList<(ulong Address, ulong Length)> readMemoryRanges,
            IReadOnlyList<(ulong Address, ulong Length)> normalizedReadMemoryRanges)
        {
            if (readMemoryRanges == null || readMemoryRanges.Count <= 1)
            {
                return AssistCoalescingDescriptor.None;
            }

            var sortedRanges = CollectOrderedNonEmptyRanges(readMemoryRanges);
            if (sortedRanges.Count <= 1)
            {
                return AssistCoalescingDescriptor.None;
            }

            ulong windowStart = sortedRanges[0].Address;
            ulong windowEnd = SaturatingAdd(sortedRanges[0].Address, sortedRanges[0].Length);
            ulong largestGapBytes = 0;

            for (int index = 1; index < sortedRanges.Count; index++)
            {
                (ulong nextStart, ulong nextLength) = sortedRanges[index];
                ulong nextEnd = SaturatingAdd(nextStart, nextLength);
                ulong gapBytes = nextStart > windowEnd
                    ? nextStart - windowEnd
                    : 0;
                if (gapBytes > AssistNearContiguousGapBytes)
                {
                    return AssistCoalescingDescriptor.None;
                }

                if (gapBytes > largestGapBytes)
                {
                    largestGapBytes = gapBytes;
                }

                if (nextEnd > windowEnd)
                {
                    windowEnd = nextEnd;
                }
            }

            int normalizedRangeCount =
                normalizedReadMemoryRanges == null || normalizedReadMemoryRanges.Count == 0
                    ? sortedRanges.Count
                    : normalizedReadMemoryRanges.Count;
            return new AssistCoalescingDescriptor(
                BaseAddress: windowStart,
                PrefetchLength: windowEnd - windowStart,
                SourceRangeCount: sortedRanges.Count,
                NormalizedRangeCount: normalizedRangeCount,
                LargestGapBytes: largestGapBytes);
        }

        internal static void ValidateCoalescedRangeMetadata(
            IReadOnlyList<(ulong Address, ulong Length)> rawReadMemoryRanges,
            IReadOnlyList<(ulong Address, ulong Length)> normalizedReadMemoryRanges,
            AssistCoalescingDescriptor descriptor)
        {
            if (!descriptor.IsValid)
            {
                return;
            }

            ulong descriptorEnd = SaturatingAdd(descriptor.BaseAddress, descriptor.PrefetchLength);
            ValidateRangesStayInsideWindow(rawReadMemoryRanges, descriptor.BaseAddress, descriptorEnd, nameof(rawReadMemoryRanges));
            ValidateRangesStayInsideWindow(normalizedReadMemoryRanges, descriptor.BaseAddress, descriptorEnd, nameof(normalizedReadMemoryRanges));
        }

        private static List<(ulong Address, ulong Length)> CollectOrderedNonEmptyRanges(
            IReadOnlyList<(ulong Address, ulong Length)> ranges)
        {
            var orderedRanges = new List<(ulong Address, ulong Length)>(ranges?.Count ?? 0);
            if (ranges == null)
            {
                return orderedRanges;
            }

            for (int index = 0; index < ranges.Count; index++)
            {
                (ulong address, ulong length) = ranges[index];
                if (length == 0)
                {
                    continue;
                }

                ulong end = SaturatingAdd(address, length);
                orderedRanges.Add((address, end - address));
            }

            orderedRanges.Sort(static (left, right) => left.Address.CompareTo(right.Address));
            return orderedRanges;
        }

        private static bool HasSameRangeSequence(
            IReadOnlyList<(ulong Address, ulong Length)> orderedRanges,
            IReadOnlyList<(ulong Address, ulong Length)> rawReadMemoryRanges)
        {
            if (orderedRanges == null || rawReadMemoryRanges == null || orderedRanges.Count != rawReadMemoryRanges.Count)
            {
                return false;
            }

            for (int index = 0; index < orderedRanges.Count; index++)
            {
                if (orderedRanges[index] != rawReadMemoryRanges[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateRangesStayInsideWindow(
            IReadOnlyList<(ulong Address, ulong Length)> ranges,
            ulong windowStart,
            ulong windowEnd,
            string paramName)
        {
            if (ranges == null)
            {
                return;
            }

            for (int index = 0; index < ranges.Count; index++)
            {
                (ulong address, ulong length) = ranges[index];
                if (length == 0)
                {
                    continue;
                }

                ulong end = SaturatingAdd(address, length);
                if (address < windowStart || end > windowEnd)
                {
                    throw new InvalidOperationException(
                        $"Coalesced read-range metadata escaped its advisory window while validating {paramName}.");
                }
            }
        }

        private static ulong SaturatingAdd(ulong address, ulong length)
        {
            ulong remaining = ulong.MaxValue - address;
            return length > remaining ? ulong.MaxValue : address + length;
        }
    }
}
