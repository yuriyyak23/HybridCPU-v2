using System;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    /// <summary>
    /// Thrown when production pipeline state addresses a scalar lane outside the
    /// architectural 8-wide issue window.
    /// </summary>
    public sealed class InvalidPipelineLaneException : Exception
    {
        public const byte MinLaneIndex = 0;
        public const byte MaxLaneIndex = 7;

        public string StageName { get; }
        public byte LaneIndex { get; }
        public ulong? BundlePc { get; }
        public int? VtId { get; }
        public ExecutionFaultCategory Category => ExecutionFaultCategory.InvalidPipelineLane;

        public InvalidPipelineLaneException(
            string stageName,
            byte laneIndex,
            ulong? bundlePc = null,
            int? vtId = null)
            : base(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.InvalidPipelineLane,
                    FormatDetail(stageName, laneIndex, bundlePc, vtId)))
        {
            StageName = NormalizeStageName(stageName);
            LaneIndex = laneIndex;
            BundlePc = bundlePc;
            VtId = vtId;
            ExecutionFaultContract.Stamp(this, Category);
        }

        private static string FormatDetail(
            string stageName,
            byte laneIndex,
            ulong? bundlePc,
            int? vtId)
        {
            string normalizedStageName = NormalizeStageName(stageName);
            string context = string.Empty;

            if (bundlePc.HasValue)
            {
                context += $" BundlePc=0x{bundlePc.Value:X}.";
            }

            if (vtId.HasValue)
            {
                context += $" VtId={vtId.Value}.";
            }

            return
                $"{normalizedStageName} attempted to access invalid pipeline lane {laneIndex}. " +
                $"Valid lane range is {MinLaneIndex}..{MaxLaneIndex}.{context}";
        }

        private static string NormalizeStageName(string stageName)
            => string.IsNullOrWhiteSpace(stageName)
                ? "UnknownPipelineStage"
                : stageName;
    }

    internal static class PipelineLaneResolver
    {
        public static byte ResolveLaneOrThrow(
            byte laneIndex,
            string stageName,
            ulong? bundlePc = null,
            int? vtId = null)
        {
            if (laneIndex <= InvalidPipelineLaneException.MaxLaneIndex)
            {
                return laneIndex;
            }

            throw new InvalidPipelineLaneException(stageName, laneIndex, bundlePc, vtId);
        }
    }
}
