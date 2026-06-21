using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class MicroOpScheduler
    {

        // ══════════════════════════════════════════════════════════════
        //  Nomination API
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Nominate a micro-operation from a core for potential FSP injection.
        /// Control-flow and null operations are silently ignored.
        /// Nominations from stalled cores are annulled.
        ///
        /// HLS: single-cycle write to nomination port register.
        /// </summary>
        /// <param name="coreId">Core index within Pod (0–15)</param>
        /// <param name="op">Candidate micro-operation (may be null)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Nominate(int coreId, MicroOp? op)
        {
            if ((uint)coreId >= NUM_PORTS) return;
            if (op == null) return;
            if (op.IsControlFlow) return;
            if (!op.AdmissionMetadata.IsStealable) return;
            if (_coreStalled[coreId]) return;

            _ports[coreId] = op;
            _portValid[coreId] = true;
        }

        /// <summary>
        /// Nominate an SMT candidate from a virtual thread for intra-core scheduling.
        /// HLS: single-cycle write to VT nomination port.
        /// </summary>
        /// <param name="vtId">Virtual thread ID (0–3)</param>
        /// <param name="op">Candidate micro-operation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void NominateSmtCandidate(int vtId, MicroOp? op)
        {
            if ((uint)vtId >= SMT_WAYS) return;
            if (op == null) return;

            _smtPorts[vtId] = op;
            _smtPortValid[vtId] = true;
        }

        /// <summary>
        /// Try to steal a slot from any nominated core.
        /// Priority encoder: scans ports 0→15, returns first valid candidate
        /// that passes domain filtering. Consumes the port on success.
        ///
        /// HLS: 16-input priority encoder with domain-tag comparator.
        /// </summary>
        /// <param name="requestingCoreId">Core requesting the steal (excluded from search)</param>
        /// <param name="requestedDomainTag">Domain tag filter (0 = no filtering)</param>
        /// <returns>Stolen MicroOp, or null if none available</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MicroOp? TryStealSlot(int requestingCoreId, ulong requestedDomainTag)
        {
            return TryStealSlot(requestingCoreId, requestedDomainTag, CaptureInterCoreNominationSnapshot());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MicroOp? TryStealSlot(
            int requestingCoreId,
            ulong requestedDomainTag,
            InterCoreNominationSnapshot nominationSnapshot)
        {
            for (int i = 0; i < NUM_PORTS; i++)
            {
                if (!nominationSnapshot.TryGetCandidate(i, out MicroOp candidate))
                    continue;

                // Domain isolation gate: keep the existing filtering behavior, but
                // classify rejected probes so stress evidence remains explicit.
                if (requestedDomainTag != 0)
                {
                    InterCoreDomainGuardDecision domainGuard =
                        _runtimeLegalityService.EvaluateInterCoreDomainGuard(candidate, requestedDomainTag);
                    RecordDomainIsolationProbe(domainGuard.ProbeResult);
                    if (!domainGuard.IsAllowed)
                        continue;
                }

                // Consume the port (single-use per cycle)
                ConsumeInterCoreNomination(i);
                return candidate;
            }

            return null;
        }

        /// <summary>
        /// Mark a core as stalled (annuls its nominations) or unstalled.
        /// HLS: single-bit write to stalled register.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCoreStalled(int coreId, bool stalled)
        {
            if ((uint)coreId >= NUM_PORTS) return;
            _coreStalled[coreId] = stalled;

            // Annul existing nomination if stalling
            if (stalled)
            {
                _ports[coreId] = null;
                _portValid[coreId] = false;
                ClearInterCoreAssistNominationPort(coreId);
            }
        }

        /// <summary>
        /// Latch the currently visible inter-core nomination state for cycle-local consumers.
        /// Called at start of scheduling cycle by PodController.BeginCycle().
        /// HLS: 16 × D-flip-flop transfer (one clock edge).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LatchNominations()
        {
            _latchedInterCoreNominationReadyMask = CaptureInterCoreNominationSnapshot().ReadyMask;
        }

        /// <summary>
        /// Clear all live nomination ports for fresh nominations.
        /// Called after LatchNominations by PodController.BeginCycle().
        /// HLS: 16-bit register clear.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearNominationPorts()
        {
            for (int i = 0; i < NUM_PORTS; i++)
            {
                _ports[i] = null;
                _portValid[i] = false;
            }
        }

    }
}