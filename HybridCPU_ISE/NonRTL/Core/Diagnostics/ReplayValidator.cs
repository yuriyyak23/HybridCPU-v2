// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Diagnostics / Tracing
// Phase 11: Deterministic Replay and Trace Integration
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Result of a deterministic replay validation.
    /// </summary>
    public readonly struct ReplayValidationResult
    {
        /// <summary><c>true</c> if replay produced an identical event sequence.</summary>
        public bool IsDeterministic { get; }

        /// <summary>Number of events compared.</summary>
        public int ComparedEventCount { get; }

        /// <summary>
        /// Index of the first divergent event in the original trace, or -1 if no divergence.
        /// </summary>
        public int DivergenceIndex { get; }

        /// <summary>
        /// Human-readable description of the divergence, or null if deterministic.
        /// </summary>
        public string? DivergenceDescription { get; }

        private ReplayValidationResult(
            bool isDeterministic,
            int comparedEventCount,
            int divergenceIndex,
            string? divergenceDescription)
        {
            IsDeterministic        = isDeterministic;
            ComparedEventCount     = comparedEventCount;
            DivergenceIndex        = divergenceIndex;
            DivergenceDescription  = divergenceDescription;
        }

        /// <summary>Create a successful (deterministic) result.</summary>
        public static ReplayValidationResult Deterministic(int comparedEventCount)
            => new(
                isDeterministic: true,
                comparedEventCount: comparedEventCount,
                divergenceIndex: -1,
                divergenceDescription: null);

        /// <summary>Create a divergent (non-deterministic) result.</summary>
        public static ReplayValidationResult Diverged(
            int comparedEventCount,
            int divergenceIndex,
            string divergenceDescription)
            => new(
                isDeterministic: false,
                comparedEventCount: comparedEventCount,
                divergenceIndex: divergenceIndex,
                divergenceDescription: divergenceDescription);

        /// <inheritdoc/>
        public override string ToString()
            => IsDeterministic
                ? $"Deterministic: {ComparedEventCount} events matched"
                : $"Diverged at event {DivergenceIndex}/{ComparedEventCount}: {DivergenceDescription}";
    }

    /// <summary>
    /// Validates that a replayed trace is identical to the original trace
    /// up to the comparison point.
    /// <para>
    /// The <see cref="ReplayValidator"/> is the canonical correctness test for
    /// deterministic execution — its pass/fail is a hard gate for Phase 12.
    /// </para>
    /// <para>
    /// Comparison semantics: two <see cref="V4TraceEvent"/> records are equal when
    /// <see cref="V4TraceEvent.Kind"/>, <see cref="V4TraceEvent.VtId"/>,
    /// <see cref="V4TraceEvent.FsmState"/>, and <see cref="V4TraceEvent.Payload"/>
    /// are all equal. <see cref="V4TraceEvent.BundleSerial"/> is compared only after
    /// offsetting by the difference between the original and replay start serials.
    /// </para>
    /// </summary>
    public sealed class ReplayValidator
    {
        /// <summary>
        /// Compare two v4 trace event sequences and return a validation result.
        /// </summary>
        /// <param name="original">
        /// The original trace, starting at the first event after the snapshot anchor.
        /// </param>
        /// <param name="replay">
        /// The replay trace, starting at the first event after the snapshot was restored.
        /// </param>
        /// <returns>
        /// A <see cref="ReplayValidationResult"/> describing whether the replay was
        /// deterministic.
        /// </returns>
        public ReplayValidationResult ValidateTrace(
            IReadOnlyList<V4TraceEvent> original,
            IReadOnlyList<V4TraceEvent> replay)
        {
            int compareCount = System.Math.Min(original.Count, replay.Count);

            // Compute the bundle serial base for each trace independently so that
            // relative offsets can be compared without signed overflow.
            ulong origBase  = compareCount > 0 ? original[0].BundleSerial : 0UL;
            ulong repBase   = compareCount > 0 ? replay[0].BundleSerial   : 0UL;

            for (int i = 0; i < compareCount; i++)
            {
                var orig = original[i];
                var rep  = replay[i];

                if (orig.Kind != rep.Kind)
                    return ReplayValidationResult.Diverged(
                        compareCount, i,
                        $"Kind mismatch at event {i}: original={orig.Kind}, replay={rep.Kind}");

                if (orig.VtId != rep.VtId)
                    return ReplayValidationResult.Diverged(
                        compareCount, i,
                        $"VtId mismatch at event {i}: original={orig.VtId}, replay={rep.VtId}");

                if (orig.FsmState != rep.FsmState)
                    return ReplayValidationResult.Diverged(
                        compareCount, i,
                        $"FsmState mismatch at event {i}: original={orig.FsmState}, replay={rep.FsmState}");

                if (orig.Payload != rep.Payload)
                    return ReplayValidationResult.Diverged(
                        compareCount, i,
                        $"Payload mismatch at event {i}: original=0x{orig.Payload:X}, replay=0x{rep.Payload:X}");

                // BundleSerial must be offset-equal (replay starts from a different serial)
                ulong origRelSerial = orig.BundleSerial - origBase;
                ulong repRelSerial  = rep.BundleSerial  - repBase;
                if (origRelSerial != repRelSerial)
                    return ReplayValidationResult.Diverged(
                        compareCount, i,
                        $"BundleSerial mismatch at event {i}: original relative={origRelSerial}, replay relative={repRelSerial}");
            }

            // If one trace is longer, compare count differences constitute a divergence
            if (original.Count != replay.Count)
                return ReplayValidationResult.Diverged(
                    compareCount, compareCount,
                    $"Trace length mismatch: original={original.Count}, replay={replay.Count}");

            return ReplayValidationResult.Deterministic(compareCount);
        }

        /// <summary>
        /// Compare two trace sequences, but only up to <paramref name="maxBundleCount"/> bundles.
        /// </summary>
        /// <param name="original">Original trace.</param>
        /// <param name="replay">Replay trace.</param>
        /// <param name="maxBundleCount">
        /// Maximum number of bundles to compare (comparison stops when this many
        /// <see cref="TraceEventKind.BundleRetired"/> events have been seen in the original).
        /// </param>
        public ReplayValidationResult ValidateTrace(
            IReadOnlyList<V4TraceEvent> original,
            IReadOnlyList<V4TraceEvent> replay,
            int maxBundleCount)
        {
            var croppedOriginal = CropToBundles(original, maxBundleCount);
            var croppedReplay   = CropToBundles(replay,   maxBundleCount);
            return ValidateTrace(croppedOriginal, croppedReplay);
        }

        private static List<V4TraceEvent> CropToBundles(
            IReadOnlyList<V4TraceEvent> trace, int maxBundles)
        {
            var result = new List<V4TraceEvent>();
            int bundleCount = 0;
            foreach (var evt in trace)
            {
                result.Add(evt);
                if (evt.Kind == TraceEventKind.BundleRetired)
                {
                    bundleCount++;
                    if (bundleCount >= maxBundles) break;
                }
            }
            return result;
        }
    }
}
