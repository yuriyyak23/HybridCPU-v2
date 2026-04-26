using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Create the legacy inter-core resource certificate for the current bundle.
        ///
        /// This returns a structural conflict summary over the live bundle. It is not a
        /// standalone proof or external attestation object. Active SMT typed-slot hot paths
        /// instead route through <see cref="BundleResourceCertificate4Way"/>,
        /// <see cref="LegalityDecision"/>, and <see cref="IRuntimeLegalityService"/>.
        /// </summary>
        /// <param name="bundle">Current VLIW bundle (8 slots)</param>
        /// <param name="ownerThreadId">Thread ID that owns this bundle</param>
        /// <param name="cycleNumber">Current cycle number</param>
        /// <returns>Bundle resource certificate</returns>
        public BundleResourceCertificate CreateCertificate(
            IReadOnlyList<MicroOp?> bundle,
            int ownerThreadId,
            ulong cycleNumber)
        {
            return BundleResourceCertificate.Create(bundle, ownerThreadId, cycleNumber);
        }

        /// <summary>
        /// Compatibility/test-facing certificate helper.
        /// Runtime legality hot paths no longer use this as top-level authority; they route
        /// through checker/cache/service seams and return <see cref="LegalityDecision"/>.
        /// </summary>
        /// <param name="certificate">Pre-computed bundle certificate</param>
        /// <param name="candidate">Candidate micro-operation to inject</param>
        /// <returns>True if injection is safe, false otherwise</returns>
        public bool VerifyInjectionWithCertificate(BundleResourceCertificate certificate, MicroOp candidate)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));

            // Use certificate's built-in conflict check
            return certificate.CanInject(candidate);
        }

        /// <summary>
        /// Evaluate inter-core legality through an explicit checker-owned decision seam.
        /// Replay-template reuse remains local to the checker/cache contract instead of
        /// returning a bare bool to scheduler-side callers.
        /// </summary>
        internal LegalityDecision EvaluateInterCoreLegality(
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            PhaseCertificateTemplate phaseTemplate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            ArgumentNullException.ThrowIfNull(bundle);
            ArgumentNullException.ThrowIfNull(bundleCertificate);
            ArgumentNullException.ThrowIfNull(candidate);

            if (bundle.Count != 8)
            {
                throw new ArgumentException("Bundle must contain exactly 8 slots.", nameof(bundle));
            }

            if (TryResolveInterCoreGuardDecision(
                    bundleOwnerThreadId,
                    requestedDomainTag,
                    candidate,
                    out LegalityDecision guardDecision))
            {
                return guardDecision;
            }

            SafetyMask128 candidateSharedMask = candidate.AdmissionMetadata.SharedStructuralMask;
            bool attemptedReplayReuse = liveTemplateKey.PhaseKey.IsValid && candidateSharedMask.IsNonZero;
            if (attemptedReplayReuse)
            {
                if (globalHardwareMask.ConflictsWith(candidateSharedMask))
                {
                    return LegalityDecision.Reject(
                        RejectKind.CrossLaneConflict,
                        CertificateRejectDetail.SharedResourceConflict,
                        LegalityAuthoritySource.GuardPlane,
                        attemptedReplayReuse);
                }

                if (phaseTemplate.Matches(liveTemplateKey))
                {
                    return EvaluateInterCoreCertificate(
                        phaseTemplate.Certificate!,
                        candidate,
                        LegalityAuthoritySource.ReplayPhaseCertificate,
                        attemptedReplayReuse);
                }

                return EvaluateInterCoreCertificate(
                    bundleCertificate,
                    candidate,
                    LegalityAuthoritySource.StructuralCertificate,
                    attemptedReplayReuse);
            }

            return EvaluateInterCoreCompatibility(
                bundle,
                candidate,
                bundleOwnerThreadId,
                globalHardwareMask);
        }

        LegalityDecision ILegalityChecker.EvaluateInterCoreLegality(
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            PhaseCertificateTemplate phaseTemplate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask)
        {
            return EvaluateInterCoreLegality(
                bundle,
                bundleCertificate,
                liveTemplateKey,
                phaseTemplate,
                bundleOwnerThreadId,
                requestedDomainTag,
                candidate,
                globalHardwareMask);
        }
    }
}
