using System;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests;

internal static class SafetyVerifierCompatibilityTestModel
{
    private static readonly ReplayPhaseContext InactiveReplayPhase = new(
        isActive: false,
        epochId: 0,
        cachedPc: 0,
        epochLength: 0,
        completedReplays: 0,
        validSlotCount: 0,
        stableDonorMask: 0,
        lastInvalidationReason: ReplayPhaseInvalidationReason.None);

    private static readonly ILegalityCertificateCacheTelemetrySink NullTelemetrySink =
        new NullLegalityCertificateCacheTelemetrySink();

    public static bool VerifyInjection(
        SafetyVerifier verifier,
        MicroOp[] bundle,
        int targetSlot,
        MicroOp candidate,
        int bundleOwnerThreadId,
        int candidateOwnerThreadId,
        SafetyMask128 globalHardwareMask = default)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (bundle == null || bundle.Length != 8)
        {
            return false;
        }

        if (targetSlot < 0 || targetSlot >= 8)
        {
            return false;
        }

        if (candidate == null)
        {
            return false;
        }

        return EvaluateInterCoreLegalityDecision(
            verifier,
            bundle,
            candidate,
            bundleOwnerThreadId,
            candidateOwnerThreadId,
            requestedDomainTag: 0,
            globalHardwareMask).IsAllowed;
    }

    public static bool VerifyInjection(
        SafetyVerifier verifier,
        MicroOp[] bundle,
        int targetSlot,
        MicroOp candidate,
        int bundleOwnerThreadId,
        int candidateOwnerThreadId,
        ulong podDomainCert,
        SafetyMask128 globalHardwareMask = default)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (candidate == null)
        {
            return false;
        }

        return EvaluateInterCoreLegalityDecision(
            verifier,
            bundle,
            candidate,
            bundleOwnerThreadId,
            candidateOwnerThreadId,
            podDomainCert,
            globalHardwareMask).IsAllowed;
    }

    public static LegalityDecision EvaluateInterCoreLegalityDecision(
        SafetyVerifier verifier,
        MicroOp[] bundle,
        MicroOp candidate,
        int bundleOwnerThreadId,
        int candidateOwnerThreadId,
        ulong requestedDomainTag = 0,
        SafetyMask128 globalHardwareMask = default)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (bundle == null || bundle.Length != 8 || candidate == null)
        {
            return default;
        }

        int originalOwnerThreadId = candidate.OwnerThreadId;

        try
        {
            candidate.OwnerThreadId = candidateOwnerThreadId;

            IRuntimeLegalityService legalityService =
                RuntimeLegalityServiceFactory.CreateCompatibilityDefault(NullTelemetrySink, verifier);
            BundleResourceCertificate certificate = verifier.CreateCertificate(bundle, bundleOwnerThreadId, 0);
            return legalityService.EvaluateInterCoreLegality(
                InactiveReplayPhase,
                bundle,
                certificate,
                bundleOwnerThreadId,
                requestedDomainTag,
                candidate,
                globalHardwareMask);
        }
        finally
        {
            candidate.OwnerThreadId = originalOwnerThreadId;
        }
    }

    public static bool VerifyInjectionFast(
        SafetyVerifier verifier,
        MicroOp[] bundle,
        MicroOp candidate,
        SafetyMask128 globalHardwareMask = default)
    {
        ArgumentNullException.ThrowIfNull(verifier);

        if (bundle == null || candidate == null)
        {
            return false;
        }

        if (bundle.Length != 8)
        {
            return false;
        }

        MicroOpAdmissionMetadata admission = candidate.AdmissionMetadata;
        if (admission.SharedStructuralMask.IsZero && admission.RegisterHazardMask == 0)
        {
            return false;
        }

        if (globalHardwareMask.ConflictsWith(admission.SharedStructuralMask))
        {
            return false;
        }

        int ownerThreadId = 0;
        for (int i = 0; i < bundle.Length; i++)
        {
            if (bundle[i] != null)
            {
                ownerThreadId = bundle[i]!.OwnerThreadId;
                break;
            }
        }

        BundleResourceCertificate certificate = verifier.CreateCertificate(bundle, ownerThreadId, 0);
        return verifier.VerifyInjectionWithCertificate(certificate, candidate);
    }

    private sealed class NullLegalityCertificateCacheTelemetrySink : ILegalityCertificateCacheTelemetrySink
    {
        public void RecordLegalityCertificateCacheHit(long estimatedChecksSaved)
        {
        }

        public void RecordLegalityCertificateCacheMiss()
        {
        }

        public void RecordLegalityCertificateCacheInvalidation(ReplayPhaseInvalidationReason reason)
        {
        }
    }
}
