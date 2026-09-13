using System;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Phase 04 guard plane: when true, kernel-mode operations (DomainTag==0)
        /// are prohibited from being admitted into user-domain bundles through
        /// scheduler, assist, or replay-aware densification paths. This prevents
        /// timing side-channels where a privileged micro-op could probe user-domain
        /// cache/TLB state.
        /// ISA contract: in user mode (podDomainCert != 0), DomainTag=0 is treated as deny-all.
        /// HLS: single AND gate on the domain certificate comparator.
        /// Runtime legality hot paths consume the effective value through
        /// <see cref="IRuntimeLegalityService"/> rather than by holding a concrete verifier.
        /// </summary>
        public bool KernelDomainIsolation { get; set; } = true;
        bool ILegalityChecker.IsKernelDomainIsolationEnabled => KernelDomainIsolation;

        /// <summary>
        /// Verify domain certificate before scheduler admission, replay reuse, or certificate acceptance.
        /// Hardware gate: checks that the admitted operation belongs to a compatible
        /// memory protection domain by ANDing the operation's
        /// <see cref="MicroOp.Placement"/>.<see cref="SlotPlacementMetadata.DomainTag"/>
        /// with the Pod's domain certificate. If the result is zero, the domains are incompatible
        /// and the operation is rejected before any dependency checking occurs.
        ///
        /// HLS-compatible: single-cycle AND + compare-to-zero in hardware.
        /// </summary>
        public bool VerifyDomainCertificate(MicroOp injectedOp, ulong podDomainCert)
        {
            return EvaluateDomainIsolationProbe(injectedOp, podDomainCert).IsAllowed;
        }

        /// <summary>
        /// Public diagnostics/model helper for explicit domain-isolation probing.
        /// Production scheduler/runtime legality consumes checker-owned decisions through
        /// <see cref="IRuntimeLegalityService"/> instead of treating this probe result as
        /// runtime authority.
        /// </summary>
        public DomainIsolationProbeResult EvaluateDomainIsolationProbe(MicroOp injectedOp, ulong podDomainCert)
        {
            ArgumentNullException.ThrowIfNull(injectedOp);
            return EvaluateDomainIsolationProbe(injectedOp.Placement.DomainTag, podDomainCert);
        }

        private DomainIsolationProbeResult EvaluateDomainIsolationProbe(ulong domainTag, ulong podDomainCert)
        {
            // Phase 04 guard plane: when KernelDomainIsolation is active, kernel ops
            // (DomainTag==0) must not be admitted into user-domain bundles.
            // This closes the timing side-channel where a privileged micro-op could
            // probe user cache/TLB state via replay-aware densification.
            // HLS: single AND gate, zero gate delay addition.
            if (domainTag == 0)
            {
                if (KernelDomainIsolation && podDomainCert != 0)
                    return new DomainIsolationProbeResult(false, false, true);

                return new DomainIsolationProbeResult(true, false, false);
            }

            // podDomainCert 0 = no domain enforcement configured
            if (podDomainCert == 0)
                return new DomainIsolationProbeResult(true, false, false);

            // Hardware AND gate: domain must match certificate
            bool allowed = (domainTag & podDomainCert) != 0;
            return new DomainIsolationProbeResult(allowed, !allowed, false);
        }

        /// <summary>
        /// Evaluate the descriptor-backed lane6 owner/domain guard before descriptor
        /// acceptance, replay reuse, certificate acceptance, or execution. The descriptor
        /// structural read may locate owner fields, but this guard decision is the first
        /// authority allowed to turn those fields into an admissible descriptor.
        /// </summary>
        public DmaStreamComputeOwnerGuardDecision EvaluateDmaStreamComputeOwnerGuard(
            DmaStreamComputeOwnerBinding descriptorOwnerBinding,
            DmaStreamComputeOwnerGuardContext runtimeOwnerContext)
        {
            ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);

            if (descriptorOwnerBinding.OwnerVirtualThreadId != runtimeOwnerContext.OwnerVirtualThreadId)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.OwnerMismatch,
                    "DmaStreamCompute owner VT mismatch; raw VLIW transport hints are not owner authority.");
            }

            if (descriptorOwnerBinding.OwnerContextId != runtimeOwnerContext.OwnerContextId)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.OwnerMismatch,
                    "DmaStreamCompute owner context mismatch.");
            }

            if (descriptorOwnerBinding.OwnerCoreId != runtimeOwnerContext.OwnerCoreId ||
                descriptorOwnerBinding.OwnerPodId != runtimeOwnerContext.OwnerPodId)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.OwnerMismatch,
                    "DmaStreamCompute owner core/pod mismatch; cross-core reuse is not authorized.");
            }

            if (descriptorOwnerBinding.DeviceId != DmaStreamComputeDescriptor.CanonicalLane6DeviceId ||
                descriptorOwnerBinding.DeviceId != runtimeOwnerContext.DeviceId)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.OwnerMismatch,
                    "DmaStreamCompute DeviceId cannot bypass the canonical lane6 owner/domain guard.");
            }

            if (descriptorOwnerBinding.OwnerDomainTag != runtimeOwnerContext.OwnerDomainTag)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.DomainMismatch,
                    "DmaStreamCompute owner domain tag mismatch.");
            }

            DomainIsolationProbeResult domainProbe =
                EvaluateDomainIsolationProbe(
                    descriptorOwnerBinding.OwnerDomainTag,
                    runtimeOwnerContext.ActiveDomainCertificate);
            if (!domainProbe.IsAllowed)
            {
                return RejectDmaStreamComputeOwnerGuard(
                    descriptorOwnerBinding,
                    runtimeOwnerContext,
                    RejectKind.DomainMismatch,
                    domainProbe.IsKernelToUserBlock
                        ? "DmaStreamCompute kernel-domain descriptor is not legal in a user-domain guard context."
                        : "DmaStreamCompute owner domain is not covered by the active domain certificate.");
            }

            return DmaStreamComputeOwnerGuardDecision.Allow(
                descriptorOwnerBinding,
                runtimeOwnerContext,
                "DmaStreamCompute owner/domain guard succeeded.");
        }

        private static DmaStreamComputeOwnerGuardDecision RejectDmaStreamComputeOwnerGuard(
            DmaStreamComputeOwnerBinding descriptorOwnerBinding,
            DmaStreamComputeOwnerGuardContext runtimeOwnerContext,
            RejectKind rejectKind,
            string message)
        {
            return DmaStreamComputeOwnerGuardDecision.Reject(
                descriptorOwnerBinding,
                runtimeOwnerContext,
                rejectKind,
                message);
        }

        /// <summary>
        /// Evaluate inter-core nomination/domain filtering through an explicit checker-owned
        /// decision seam while preserving domain probe classification for telemetry.
        /// </summary>
        internal InterCoreDomainGuardDecision EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            DomainIsolationProbeResult probe =
                requestedDomainTag == 0
                    ? new DomainIsolationProbeResult(true, false, false)
                    : EvaluateDomainIsolationProbe(candidate, requestedDomainTag);
            LegalityDecision decision = probe.IsAllowed
                ? LegalityDecision.Allow(
                    LegalityAuthoritySource.GuardPlane,
                    attemptedReplayCertificateReuse: false)
                : CreateGuardReject(RejectKind.DomainMismatch);
            return new InterCoreDomainGuardDecision(decision, probe);
        }

        InterCoreDomainGuardDecision ILegalityChecker.EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag)
        {
            return EvaluateInterCoreDomainGuard(candidate, requestedDomainTag);
        }

        LegalityDecision ILegalityChecker.EvaluateSmtBoundaryGuard(PhaseCertificateTemplateKey4Way liveTemplateKey)
        {
            return EvaluateSmtBoundaryGuard(liveTemplateKey);
        }

        private LegalityDecision CreateGuardReject(RejectKind rejectKind)
        {
            return LegalityDecision.Reject(
                rejectKind,
                CertificateRejectDetail.None,
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateReuse: false);
        }

        private bool TryResolveInterCoreGuardDecision(
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            out LegalityDecision decision)
        {
            if (requestedDomainTag != 0)
            {
                InterCoreDomainGuardDecision domainGuard =
                    EvaluateInterCoreDomainGuard(candidate, requestedDomainTag);
                if (!domainGuard.IsAllowed)
                {
                    decision = domainGuard.LegalityDecision;
                    return true;
                }
            }

            if (candidate.ScoreboardPending)
            {
                decision = CreateGuardReject(RejectKind.RareHazard);
                return true;
            }

            if (bundleOwnerThreadId == candidate.OwnerThreadId)
            {
                decision = LegalityDecision.Allow(
                    LegalityAuthoritySource.GuardPlane,
                    attemptedReplayCertificateReuse: false);
                return true;
            }

            if (!candidate.AdmissionMetadata.IsStealable)
            {
                decision = CreateGuardReject(RejectKind.RareHazard);
                return true;
            }

            if (candidate.IsControlFlow)
            {
                decision = CreateGuardReject(RejectKind.Ordering);
                return true;
            }

            decision = default;
            return false;
        }

        private bool TryRejectSmtOwnerDomainGuard(
            SmtBundleMetadata4Way bundleMetadata,
            MicroOp candidate,
            out LegalityDecision decision)
        {
            if (bundleMetadata.HasKnownOwnerContext &&
                candidate.OwnerContextId != bundleMetadata.OwnerContextId)
            {
                decision = CreateGuardReject(RejectKind.OwnerMismatch);
                return true;
            }

            if (bundleMetadata.HasDomainRestriction &&
                !EvaluateDomainIsolationProbe(candidate, bundleMetadata.OwnerDomainTag).IsAllowed)
            {
                decision = CreateGuardReject(RejectKind.DomainMismatch);
                return true;
            }

            decision = default;
            return false;
        }

        /// <summary>
        /// Evaluate SMT legality through an explicit checker-owned decision seam.
        /// The scheduler consumes the returned <see cref="LegalityDecision"/> instead of
        /// treating the certificate or mask shape as the top-level legality authority.
        /// </summary>
        internal LegalityDecision EvaluateSmtBoundaryGuard(PhaseCertificateTemplateKey4Way liveTemplateKey)
        {
            if (liveTemplateKey.BoundaryGuard.BlocksSmtInjection)
            {
                return LegalityDecision.Reject(
                    RejectKind.Boundary,
                    CertificateRejectDetail.None,
                    LegalityAuthoritySource.GuardPlane,
                    attemptedReplayCertificateReuse: false);
            }

            return LegalityDecision.Allow(
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateReuse: false);
        }
    }
}
