using System;
using System.Collections.Generic;
using System.Numerics;

namespace YAKSys_Hybrid_CPU.Core
{
    internal readonly struct BundleIssueFallbackInfo
    {
        public BundleIssueFallbackInfo(
            RuntimeClusterFallbackReasonMask fallbackReasonMask,
            byte blockedScalarCandidateMask,
            bool retainsReferenceSequentialPath)
        {
            FallbackReasonMask = fallbackReasonMask;
            BlockedScalarCandidateMask = blockedScalarCandidateMask;
            RetainsReferenceSequentialPath = retainsReferenceSequentialPath;
        }

        public RuntimeClusterFallbackReasonMask FallbackReasonMask { get; }
        public byte BlockedScalarCandidateMask { get; }
        public bool RetainsReferenceSequentialPath { get; }

        public static BundleIssueFallbackInfo Create(
            RuntimeClusterAdmissionCandidateView candidateView,
            RuntimeClusterAdmissionDecisionDraft decisionDraft)
        {
            return new BundleIssueFallbackInfo(
                decisionDraft.FallbackReasonMask,
                candidateView.BlockedScalarCandidateMask,
                decisionDraft.RetainsReferenceSequentialPath);
        }

        public static BundleIssueFallbackInfo CreateEmpty()
        {
            return new BundleIssueFallbackInfo(
                fallbackReasonMask: RuntimeClusterFallbackReasonMask.None,
                blockedScalarCandidateMask: 0,
                retainsReferenceSequentialPath: true);
        }
    }
}
