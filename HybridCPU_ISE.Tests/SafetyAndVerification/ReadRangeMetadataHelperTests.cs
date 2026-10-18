using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;

namespace HybridCPU_ISE.Tests.SafetyAndVerification
{
    public sealed class ReadRangeMetadataHelperTests
    {
        [Fact]
        public void TryNormalizeContiguousReadRanges_MergesAdjacentWindowsWithoutMutatingRawSequence()
        {
            (ulong Address, ulong Length)[] rawRanges =
            {
                (0x3000UL, 16UL),
                (0x3010UL, 16UL)
            };

            bool normalized = ReadRangeMetadataHelper.TryNormalizeContiguousReadRanges(
                rawRanges,
                out var normalizedRanges);

            Assert.True(normalized);
            Assert.Equal(2, rawRanges.Length);
            Assert.Equal((0x3000UL, 16UL), rawRanges[0]);
            Assert.Equal((0x3010UL, 16UL), rawRanges[1]);
            Assert.Single(normalizedRanges);
            Assert.Equal((0x3000UL, 32UL), normalizedRanges[0]);
        }

        [Fact]
        public void BuildAssistCoalescingDescriptor_AcceptsNearContiguousWindowsWithinAssistGapBudget()
        {
            (ulong Address, ulong Length)[] rawRanges =
            {
                (0x3000UL, 16UL),
                (0x3028UL, 16UL)
            };

            bool normalized = ReadRangeMetadataHelper.TryNormalizeContiguousReadRanges(
                rawRanges,
                out var normalizedRanges);
            Assert.False(normalized);

            AssistCoalescingDescriptor descriptor =
                ReadRangeMetadataHelper.BuildAssistCoalescingDescriptor(
                    rawRanges,
                    normalizedRanges);

            Assert.True(descriptor.IsValid);
            Assert.True(descriptor.IsNearContiguous);
            Assert.Equal(0x3000UL, descriptor.BaseAddress);
            Assert.Equal(0x38UL, descriptor.PrefetchLength);
            Assert.Equal(2, descriptor.SourceRangeCount);
            Assert.Equal(2, descriptor.NormalizedRangeCount);
            Assert.Equal(0x18UL, descriptor.LargestGapBytes);

            Exception? validationException = Record.Exception(() =>
                ReadRangeMetadataHelper.ValidateCoalescedRangeMetadata(
                    rawRanges,
                    normalizedRanges,
                    descriptor));
            Assert.Null(validationException);
        }
    }
}
