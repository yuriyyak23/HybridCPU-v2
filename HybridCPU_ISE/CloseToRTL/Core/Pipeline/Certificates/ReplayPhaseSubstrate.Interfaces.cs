using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;


namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Runtime-local inter-core legality certificate cache seam.
    /// Keeps scheduler/runtime consumers on an explicit cache service contract
    /// instead of the concrete replay-template container type.
    /// </summary>
    internal interface IInterCoreLegalityCertificateCache
    {
        bool IsValid { get; }

        bool Matches(ReplayPhaseContext phase);

        void Prepare(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate);

        LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default);

        LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate);

        bool Invalidate();
    }

    /// <summary>
    /// Runtime-local SMT legality certificate cache seam.
    /// Mirrors the 4-way replay/template lifecycle without exposing the concrete
    /// cache container type to scheduler/runtime consumers.
    /// </summary>
    internal interface ISmtLegalityCertificateCache4Way
    {
        bool IsValid { get; }

        bool Matches(ReplayPhaseContext phase);

        LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            MicroOp candidate);

        void Prepare(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate);

        LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate);

        bool Invalidate();
    }

    /// <summary>
    /// Default runtime-local legality cache provisioning.
    /// Keeps scheduler construction on the cache seam rather than concrete cache types.
    /// </summary>
    internal static class RuntimeLegalityCertificateCacheFactory
    {
        internal static IInterCoreLegalityCertificateCache CreateInterCore()
        {
            return new InterCoreLegalityCertificateCache();
        }

        internal static ISmtLegalityCertificateCache4Way CreateSmt4Way()
        {
            return new SmtLegalityCertificateCache4Way();
        }
    }

    /// <summary>
    /// Scheduler-local sink for replay legality-cache telemetry.
    /// Exposes semantic counter updates instead of raw cache telemetry transport.
    /// </summary>
    internal interface ILegalityCertificateCacheTelemetrySink
    {
        void RecordLegalityCertificateCacheHit(long estimatedChecksSaved);

        void RecordLegalityCertificateCacheMiss();

        void RecordLegalityCertificateCacheInvalidation(ReplayPhaseInvalidationReason reason);
    }

    /// <summary>
    /// Runtime-local legality service entrypoint for inter-core + SMT hot paths plus
    /// adjacent scheduler diagnostics. Owns live replay-phase handoff so scheduler
    /// callers do not coordinate checker and cache/coordinator services manually.
    /// </summary>
    public interface IRuntimeLegalityService
    {
        bool IsKernelDomainIsolationEnabled { get; }

        InterCoreDomainGuardDecision EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag);

        TypedSlotRejectClassification ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind);

        void PrepareInterCore(
            ReplayPhaseContext phase,
            BundleResourceCertificate certificate);

        LegalityDecision EvaluateInterCoreLegality(
            ReplayPhaseContext phase,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default);

        void RefreshInterCoreAfterMutation(
            ReplayPhaseContext phase,
            BundleResourceCertificate certificate);

        void PrepareSmt(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        LegalityDecision EvaluateSmtBoundaryGuard(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        LegalityDecision EvaluateSmtLegality(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate);

        void RefreshSmtAfterMutation(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        void InvalidatePhaseMismatch(ReplayPhaseContext phase);

        void Invalidate(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true);
    }

    /// <summary>
    /// Runtime-local coordinator that owns cache lifecycle seams adjacent to legality checking.
    /// Keeps scheduler callers off raw cache invalidation/telemetry mechanics while preserving
    /// the existing prepare/evaluate/refresh/invalidate behavior.
    /// </summary>
    internal interface ILegalityCertificateCacheCoordinator
    {
        void PrepareInterCore(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate);

        LegalityDecision EvaluateInterCoreLegality(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default);

        void RefreshInterCoreAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate);

        void PrepareSmt(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        LegalityDecision EvaluateSmtBoundaryGuard(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        LegalityDecision EvaluateSmtLegality(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate);

        void RefreshSmtAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard);

        void InvalidatePhaseMismatch(ReplayPhaseContext phase);

        void Invalidate(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true);
    }

    /// <summary>
    /// Default runtime-local legality cache coordinator provisioning.
    /// Keeps scheduler construction on a single cache/coordinator seam.
    /// </summary>
    internal static class RuntimeLegalityCertificateCacheCoordinatorFactory
    {
        internal static ILegalityCertificateCacheCoordinator CreateDefault(
            ILegalityCertificateCacheTelemetrySink telemetrySink,
            IInterCoreLegalityCertificateCache? interCoreCache = null,
            ISmtLegalityCertificateCache4Way? smtCache = null)
        {
            return new LegalityCertificateCacheCoordinator(
                telemetrySink,
                interCoreCache ?? RuntimeLegalityCertificateCacheFactory.CreateInterCore(),
                smtCache ?? RuntimeLegalityCertificateCacheFactory.CreateSmt4Way());
        }
    }

    /// <summary>
    /// Default runtime legality/cache coordinator for inter-core + SMT replay certificate reuse.
}
