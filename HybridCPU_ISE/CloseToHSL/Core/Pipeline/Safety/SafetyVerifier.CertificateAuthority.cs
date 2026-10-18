namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        private static LegalityDecision EvaluateSmtCertificate(
            BundleResourceCertificate4Way certificate,
            MicroOp candidate,
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayReuse)
        {
            if (certificate.CanInjectWithReason(candidate, out CertificateRejectDetail detail))
            {
                return LegalityDecision.Allow(authoritySource, attemptedReplayReuse);
            }

            return LegalityDecision.Reject(
                MapCertificateReject(detail),
                detail,
                authoritySource,
                attemptedReplayReuse);
        }

        private static LegalityDecision EvaluateInterCoreCertificate(
            BundleResourceCertificate certificate,
            MicroOp candidate,
            LegalityAuthoritySource authoritySource,
            bool attemptedReplayReuse)
        {
            if (certificate.CanInjectWithReason(candidate, out CertificateRejectDetail detail))
            {
                return LegalityDecision.Allow(authoritySource, attemptedReplayReuse);
            }

            return LegalityDecision.Reject(
                MapCertificateReject(detail),
                detail,
                authoritySource,
                attemptedReplayReuse);
        }

        private static RejectKind MapCertificateReject(CertificateRejectDetail detail)
        {
            return detail switch
            {
                CertificateRejectDetail.SharedResourceConflict => RejectKind.CrossLaneConflict,
                CertificateRejectDetail.RegisterGroupConflict => RejectKind.CrossLaneConflict,
                _ => RejectKind.CrossLaneConflict
            };
        }
    }
}
