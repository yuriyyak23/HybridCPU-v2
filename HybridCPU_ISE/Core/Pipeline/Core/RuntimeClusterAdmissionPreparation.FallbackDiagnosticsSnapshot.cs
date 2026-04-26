using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    [Flags]
    internal enum RuntimeClusterFallbackReasonMask : byte
    {
        None = 0,
        ResidualDiagnostics = 1 << 0,
        BlockedScalarCandidates = 1 << 1,
        RegisterHazard = 1 << 2,
        OrderingHazard = 1 << 3
    }

    internal readonly struct ClusterFallbackDiagnosticsSnapshot
    {
        public ClusterFallbackDiagnosticsSnapshot(
            byte fallbackDiagnosticsMask,
            bool suggestsFallbackDiagnostics,
            RuntimeClusterFallbackReasonMask fallbackReasonMask = RuntimeClusterFallbackReasonMask.None)
        {
            FallbackDiagnosticsMask = fallbackDiagnosticsMask;
            SuggestsFallbackDiagnostics = suggestsFallbackDiagnostics;
            FallbackReasonMask = fallbackReasonMask;
        }

        /// <summary>
        /// Runtime-readable mirror of the decode-side residual narrow-path mask.
        /// Read-only diagnostics snapshot: it must not become a semantic owner for system/CSR/FSM behavior.
        /// </summary>
        public byte FallbackDiagnosticsMask { get; }

        /// <summary>
        /// Advisory decode/runtime hint that some slots still rely on the narrow reference path.
        /// Observability only; runtime legality, retire, and privileged semantics remain explicit elsewhere.
        /// </summary>
        public bool SuggestsFallbackDiagnostics { get; }

        /// <summary>
        /// Category mask explaining why a scalar-cluster candidate still degrades to the reference path.
        /// This is derived from decode/runtime diagnostics so semantic fallback accounting can
        /// use an explicit authority instead of reading the diagnostics mirror directly.
        /// </summary>
        public RuntimeClusterFallbackReasonMask FallbackReasonMask { get; }

        public static RuntimeClusterFallbackReasonMask DeriveReasonMask(
            byte fallbackDiagnosticsMask,
            byte blockedScalarCandidateMask,
            byte registerHazardMask,
            byte orderingHazardMask)
        {
            RuntimeClusterFallbackReasonMask reasonMask = RuntimeClusterFallbackReasonMask.None;

            if (fallbackDiagnosticsMask != 0)
                reasonMask |= RuntimeClusterFallbackReasonMask.ResidualDiagnostics;

            if (blockedScalarCandidateMask != 0)
                reasonMask |= RuntimeClusterFallbackReasonMask.BlockedScalarCandidates;

            if (registerHazardMask != 0)
                reasonMask |= RuntimeClusterFallbackReasonMask.RegisterHazard;

            if (orderingHazardMask != 0)
                reasonMask |= RuntimeClusterFallbackReasonMask.OrderingHazard;

            return reasonMask;
        }
    }
}
