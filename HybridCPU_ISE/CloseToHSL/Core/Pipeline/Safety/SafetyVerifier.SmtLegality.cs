using System;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Evaluate SMT legality through an explicit checker-owned decision seam.
        /// Boundary and owner/domain guard-plane checks run before any certificate reuse
        /// or structural resource conflict evaluation. Active authority order is guard
        /// plane first, then replay-phase certificate reuse when the live template
        /// matches, then current-bundle structural certificate fallback.
        /// </summary>
        internal LegalityDecision EvaluateSmtLegality(
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            PhaseCertificateTemplate4Way phaseTemplate,
            MicroOp candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            LegalityDecision boundaryDecision = EvaluateSmtBoundaryGuard(liveTemplateKey);
            if (!boundaryDecision.IsAllowed)
                return boundaryDecision;

            if (TryRejectSmtOwnerDomainGuard(liveTemplateKey.BundleMetadata, candidate, out LegalityDecision guardReject))
                return guardReject;

            bool attemptedReplayReuse = liveTemplateKey.PhaseKey.IsValid;
            if (attemptedReplayReuse && phaseTemplate.Matches(liveTemplateKey))
            {
                return EvaluateSmtCertificate(
                    phaseTemplate.Certificate,
                    candidate,
                    LegalityAuthoritySource.ReplayPhaseCertificate,
                    attemptedReplayReuse);
            }

            return EvaluateSmtCertificate(
                bundleCertificate,
                candidate,
                LegalityAuthoritySource.StructuralCertificate,
                attemptedReplayReuse);
        }

        LegalityDecision ILegalityChecker.EvaluateSmtLegality(
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            PhaseCertificateTemplate4Way phaseTemplate,
            MicroOp candidate)
        {
            return EvaluateSmtLegality(
                bundleCertificate,
                liveTemplateKey,
                phaseTemplate,
                candidate);
        }
    }
}
