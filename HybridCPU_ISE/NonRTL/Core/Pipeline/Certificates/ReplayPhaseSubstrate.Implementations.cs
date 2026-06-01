using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;


namespace YAKSys_Hybrid_CPU.Core
{
    /// </summary>
    internal sealed class LegalityCertificateCacheCoordinator : ILegalityCertificateCacheCoordinator
    {
        private readonly ILegalityCertificateCacheTelemetrySink _telemetrySink;
        private readonly IInterCoreLegalityCertificateCache _interCoreCache;
        private readonly ISmtLegalityCertificateCache4Way _smtCache;

        public LegalityCertificateCacheCoordinator(
            ILegalityCertificateCacheTelemetrySink telemetrySink,
            IInterCoreLegalityCertificateCache interCoreCache,
            ISmtLegalityCertificateCache4Way smtCache)
        {
            ArgumentNullException.ThrowIfNull(telemetrySink);
            ArgumentNullException.ThrowIfNull(interCoreCache);
            ArgumentNullException.ThrowIfNull(smtCache);

            _telemetrySink = telemetrySink;
            _interCoreCache = interCoreCache;
            _smtCache = smtCache;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareInterCore(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate)
        {
            _interCoreCache.Prepare(
                CreateInterCoreTemplateKey(phaseKey, certificate),
                certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateInterCoreLegality(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            LegalityCertificateCacheEvaluation<LegalityDecision> legalityEvaluation =
                _interCoreCache.EvaluateLegality(
                    legalityChecker,
                    bundle,
                    bundleCertificate,
                    CreateInterCoreTemplateKey(phaseKey, bundleCertificate),
                    bundleOwnerThreadId,
                    requestedDomainTag,
                    candidate,
                    globalHardwareMask);
            ApplyTelemetry(legalityEvaluation.Telemetry);
            return legalityEvaluation.LegalityResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshInterCoreAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate)
        {
            ApplyTelemetry(
                _interCoreCache.RefreshAfterMutation(
                    CreateInterCoreTemplateKey(phaseKey, certificate),
                    certificate));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareSmt(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            _smtCache.Prepare(
                CreateSmtTemplateKey(
                    phaseKey,
                    certificate,
                    bundleMetadata,
                    boundaryGuard),
                certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateSmtBoundaryGuard(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            return legalityChecker.EvaluateSmtBoundaryGuard(
                CreateSmtTemplateKey(
                    phaseKey,
                    bundleCertificate,
                    bundleMetadata,
                    boundaryGuard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateSmtLegality(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate)
        {
            LegalityCertificateCacheEvaluation<LegalityDecision> legalityEvaluation =
                _smtCache.EvaluateLegality(
                    legalityChecker,
                    bundleCertificate,
                    CreateSmtTemplateKey(
                        phaseKey,
                        bundleCertificate,
                        bundleMetadata,
                        boundaryGuard),
                    candidate);
            ApplyTelemetry(legalityEvaluation.Telemetry);
            return legalityEvaluation.LegalityResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshSmtAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            ApplyTelemetry(
                _smtCache.RefreshAfterMutation(
                    CreateSmtTemplateKey(
                        phaseKey,
                        certificate,
                        bundleMetadata,
                        boundaryGuard),
                    certificate));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidatePhaseMismatch(ReplayPhaseContext phase)
        {
            bool invalidateInterCore = _interCoreCache.IsValid && !_interCoreCache.Matches(phase);
            bool invalidateFourWay = _smtCache.IsValid && !_smtCache.Matches(phase);
            if (invalidateInterCore || invalidateFourWay)
            {
                Invalidate(
                    ReplayPhaseInvalidationReason.PhaseMismatch,
                    invalidateInterCore,
                    invalidateFourWay);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true)
        {
            bool interCoreInvalidated = invalidateInterCore && _interCoreCache.Invalidate();
            bool fourWayInvalidated = invalidateFourWay && _smtCache.Invalidate();
            ApplyTelemetry(
                LegalityCertificateCacheTelemetry.FromInvalidationEvent(
                    reason,
                    interCoreInvalidated || fourWayInvalidated));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyTelemetry(LegalityCertificateCacheTelemetry telemetry)
        {
            if (telemetry.ReadyHitsDelta > 0)
            {
                _telemetrySink.RecordLegalityCertificateCacheHit(
                    telemetry.EstimatedChecksSavedDelta);
            }

            if (telemetry.ReadyMissesDelta > 0)
            {
                _telemetrySink.RecordLegalityCertificateCacheMiss();
            }

            if (telemetry.InvalidationEventsDelta > 0 &&
                telemetry.LastInvalidationReason != ReplayPhaseInvalidationReason.None)
            {
                _telemetrySink.RecordLegalityCertificateCacheInvalidation(
                    telemetry.LastInvalidationReason);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PhaseCertificateTemplateKey CreateInterCoreTemplateKey(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate)
        {
            return new PhaseCertificateTemplateKey(
                phaseKey,
                certificate.StructuralIdentity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PhaseCertificateTemplateKey4Way CreateSmtTemplateKey(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            return new PhaseCertificateTemplateKey4Way(
                phaseKey,
                certificate.StructuralIdentity,
                bundleMetadata,
                boundaryGuard);
        }
    }

    /// <summary>
    /// Internal runtime-local legality service provisioning.
    /// Keeps scheduler construction on a single inter-core/SMT legality service seam
    /// without exposing checker/cache wiring as a public runtime contract.
    /// </summary>
    internal static class RuntimeLegalityServiceFactory
    {
        internal static IRuntimeLegalityService CreateDefault(
            ILegalityCertificateCacheTelemetrySink telemetrySink,
            IInterCoreLegalityCertificateCache? interCoreCache = null,
            ISmtLegalityCertificateCache4Way? smtCache = null,
            ILegalityCertificateCacheCoordinator? legalityCertificateCacheCoordinator = null)
        {
            return CreateCompatibilityDefault(
                telemetrySink,
                RuntimeLegalityCheckerFactory.CreateCompatibilityDefault(),
                interCoreCache,
                smtCache,
                legalityCertificateCacheCoordinator);
        }

        internal static IRuntimeLegalityService CreateCompatibilityDefault(
            ILegalityCertificateCacheTelemetrySink telemetrySink,
            ILegalityChecker legalityChecker,
            IInterCoreLegalityCertificateCache? interCoreCache = null,
            ISmtLegalityCertificateCache4Way? smtCache = null,
            ILegalityCertificateCacheCoordinator? legalityCertificateCacheCoordinator = null)
        {
            ArgumentNullException.ThrowIfNull(telemetrySink);
            ArgumentNullException.ThrowIfNull(legalityChecker);

            ILegalityCertificateCacheCoordinator coordinator =
                legalityCertificateCacheCoordinator ??
                RuntimeLegalityCertificateCacheCoordinatorFactory.CreateDefault(
                    telemetrySink,
                    interCoreCache,
                    smtCache);
            return new RuntimeLegalityService(legalityChecker, coordinator);
        }
    }

    /// <summary>
    /// Internal runtime legality service for inter-core + SMT hot paths plus adjacent diagnostics.
    /// </summary>
    internal sealed class RuntimeLegalityService : IRuntimeLegalityService
    {
        private readonly ILegalityChecker _legalityChecker;
        private readonly ILegalityCertificateCacheCoordinator _cacheCoordinator;

        public bool IsKernelDomainIsolationEnabled => _legalityChecker.IsKernelDomainIsolationEnabled;

        internal RuntimeLegalityService(
            ILegalityChecker legalityChecker,
            ILegalityCertificateCacheCoordinator cacheCoordinator)
        {
            ArgumentNullException.ThrowIfNull(legalityChecker);
            ArgumentNullException.ThrowIfNull(cacheCoordinator);

            _legalityChecker = legalityChecker;
            _cacheCoordinator = cacheCoordinator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InterCoreDomainGuardDecision EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag)
        {
            return _legalityChecker.EvaluateInterCoreDomainGuard(candidate, requestedDomainTag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypedSlotRejectClassification ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind)
        {
            return _legalityChecker.ClassifyReject(
                admissionReject,
                certDetail,
                candidateClass,
                pinningKind);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareInterCore(
            ReplayPhaseContext phase,
            BundleResourceCertificate certificate)
        {
            _cacheCoordinator.PrepareInterCore(phase.Key, certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateInterCoreLegality(
            ReplayPhaseContext phase,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            return _cacheCoordinator.EvaluateInterCoreLegality(
                _legalityChecker,
                phase.Key,
                bundle,
                bundleCertificate,
                bundleOwnerThreadId,
                requestedDomainTag,
                candidate,
                globalHardwareMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshInterCoreAfterMutation(
            ReplayPhaseContext phase,
            BundleResourceCertificate certificate)
        {
            _cacheCoordinator.RefreshInterCoreAfterMutation(phase.Key, certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareSmt(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            _cacheCoordinator.PrepareSmt(
                phase.Key,
                certificate,
                bundleMetadata,
                boundaryGuard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateSmtBoundaryGuard(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            return _cacheCoordinator.EvaluateSmtBoundaryGuard(
                _legalityChecker,
                phase.Key,
                bundleCertificate,
                bundleMetadata,
                boundaryGuard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityDecision EvaluateSmtLegality(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate)
        {
            return _cacheCoordinator.EvaluateSmtLegality(
                _legalityChecker,
                phase.Key,
                bundleCertificate,
                bundleMetadata,
                boundaryGuard,
                candidate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RefreshSmtAfterMutation(
            ReplayPhaseContext phase,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            _cacheCoordinator.RefreshSmtAfterMutation(
                phase.Key,
                certificate,
                bundleMetadata,
                boundaryGuard);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvalidatePhaseMismatch(ReplayPhaseContext phase)
        {
            _cacheCoordinator.InvalidatePhaseMismatch(phase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true)
        {
            _cacheCoordinator.Invalidate(reason, invalidateInterCore, invalidateFourWay);
        }
    }

    /// <summary>
    /// Explicit scheduler-facing cache seam for replay-stable inter-core legality reuse.
    /// Owns template lifecycle and emits cache telemetry instead of exposing raw
    /// template hit/miss semantics to scheduler helpers.
    /// </summary>
    internal struct InterCoreLegalityCertificateCache : IInterCoreLegalityCertificateCache
    {
        private PhaseCertificateTemplate _template;

        public bool IsValid => _template.IsValid;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(ReplayPhaseContext phase)
        {
            return _template.Matches(phase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prepare(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate)
        {
            if (!templateKey.IsValid)
                return;

            if (_template.Matches(templateKey))
                return;

            _template = new PhaseCertificateTemplate(templateKey, certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            LegalityDecision decision = legalityChecker.EvaluateInterCoreLegality(
                bundle,
                bundleCertificate,
                liveTemplateKey,
                _template,
                bundleOwnerThreadId,
                requestedDomainTag,
                candidate,
                globalHardwareMask);
            return new LegalityCertificateCacheEvaluation<LegalityDecision>(
                decision,
                LegalityCertificateCacheTelemetry.FromReuseDecision(
                    decision.AttemptedReplayCertificateReuse,
                    decision.ReusedReplayCertificate,
                    bundleCertificate.OperationCount));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate)
        {
            bool wasInvalidated = Invalidate();
            if (templateKey.IsValid)
            {
                _template = new PhaseCertificateTemplate(templateKey, certificate);
            }

            return LegalityCertificateCacheTelemetry.FromInvalidationEvent(
                ReplayPhaseInvalidationReason.CertificateMutation,
                wasInvalidated);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Invalidate()
        {
            bool wasValid = _template.IsValid;
            _template = default;
            return wasValid;
        }
    }

    /// <summary>
    /// Explicit SMT boundary mode carried by replay/template certificate keys.
    /// Keeps serializing-boundary scope out of implicit phase-only matching.
    /// </summary>
    public enum SmtReplayBoundaryMode : byte
    {
        Open = 0,
        SerializingBundle = 1
    }

    /// <summary>
    /// Boundary guard context for SMT replay/template reuse.
    /// </summary>
    public readonly struct BoundaryGuardState : IEquatable<BoundaryGuardState>
    {
        public BoundaryGuardState(long serializingEpochId, SmtReplayBoundaryMode boundaryMode)
        {
            SerializingEpochId = serializingEpochId;
            BoundaryMode = boundaryMode;
        }

        public static BoundaryGuardState Open(long serializingEpochId) =>
            new BoundaryGuardState(serializingEpochId, SmtReplayBoundaryMode.Open);

        public long SerializingEpochId { get; }

        public SmtReplayBoundaryMode BoundaryMode { get; }

        public bool IsValid => SerializingEpochId >= 0;

        public bool BlocksSmtInjection => BoundaryMode != SmtReplayBoundaryMode.Open;

        public BoundaryGuardState WithOperation(MicroOp? op)
        {
            if (op == null || BlocksSmtInjection)
                return this;

            return op.SerializationClass == Arch.SerializationClass.FullSerial ||
                   op.SerializationClass == Arch.SerializationClass.VmxSerial
                ? new BoundaryGuardState(SerializingEpochId, SmtReplayBoundaryMode.SerializingBundle)
                : this;
        }

        public bool Equals(BoundaryGuardState other)
        {
            return SerializingEpochId == other.SerializingEpochId &&
                   BoundaryMode == other.BoundaryMode;
        }

        public override bool Equals(object? obj)
        {
            return obj is BoundaryGuardState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SerializingEpochId, BoundaryMode);
        }
    }

    /// <summary>
    /// Explicit runtime bundle metadata for SMT replay/template and guard-plane checks.
    /// Tracks owner/domain scope incrementally as the scheduler builds the working bundle,
    /// so legality consumers do not need to reconstruct these facts from a live bundle scan.
    /// </summary>
    public readonly struct SmtBundleMetadata4Way : IEquatable<SmtBundleMetadata4Way>
    {
        public SmtBundleMetadata4Way(
            int ownerVirtualThreadId,
            int ownerContextId,
            ulong ownerDomainTag,
            ulong bundleDomainXor,
            ulong bundleDomainSum,
            int operationCount)
        {
            OwnerVirtualThreadId = ownerVirtualThreadId;
            OwnerContextId = ownerContextId;
            OwnerDomainTag = ownerDomainTag;
            BundleDomainXor = bundleDomainXor;
            BundleDomainSum = bundleDomainSum;
            OperationCount = operationCount;
        }

        public static SmtBundleMetadata4Way Empty(int ownerVirtualThreadId) =>
            new SmtBundleMetadata4Way(ownerVirtualThreadId, -1, 0, 0, 0, 0);

        public int OwnerVirtualThreadId { get; }

        public int OwnerContextId { get; }

        public ulong OwnerDomainTag { get; }

        public ulong BundleDomainXor { get; }

        public ulong BundleDomainSum { get; }

        public int OperationCount { get; }

        public bool IsValid =>
            (uint)OwnerVirtualThreadId < BundleResourceCertificate4Way.SMT_WAYS &&
            OperationCount >= 0;

        public bool HasKnownOwnerContext => OwnerContextId >= 0;

        public bool HasDomainRestriction => OwnerDomainTag != 0;

        public ulong BundleDomainShapeId
        {
            get
            {
                var bundleHasher = new HardwareHash();
                bundleHasher.Initialize();
                bundleHasher.Compress(BundleDomainXor);
                bundleHasher.Compress(BundleDomainSum);
                bundleHasher.Compress((ulong)OperationCount);
                return bundleHasher.Finalize();
            }
        }

        public SmtBundleMetadata4Way WithOperation(MicroOp? op)
        {
            if (op == null)
                return this;

            int ownerContextId = OwnerContextId;
            ulong ownerDomainTag = OwnerDomainTag;
            if (op.VirtualThreadId == OwnerVirtualThreadId && ownerContextId < 0)
            {
                ownerContextId = op.OwnerContextId;
                ownerDomainTag = op.Placement.DomainTag;
            }

            ulong opShape = ComputeOperationDomainShape(op);
            ulong bundleDomainSum;
            unchecked
            {
                bundleDomainSum = BundleDomainSum + opShape;
            }

            return new SmtBundleMetadata4Way(
                OwnerVirtualThreadId,
                ownerContextId,
                ownerDomainTag,
                BundleDomainXor ^ opShape,
                bundleDomainSum,
                OperationCount + 1);
        }

        public bool Equals(SmtBundleMetadata4Way other)
        {
            return OwnerVirtualThreadId == other.OwnerVirtualThreadId &&
                   OwnerContextId == other.OwnerContextId &&
                   OwnerDomainTag == other.OwnerDomainTag &&
                   BundleDomainXor == other.BundleDomainXor &&
                   BundleDomainSum == other.BundleDomainSum &&
                   OperationCount == other.OperationCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is SmtBundleMetadata4Way other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                OwnerVirtualThreadId,
                OwnerContextId,
                OwnerDomainTag,
                BundleDomainXor,
                BundleDomainSum,
                OperationCount);
        }

        private static ulong ComputeOperationDomainShape(MicroOp op)
        {
            var opHasher = new HardwareHash();
            opHasher.Initialize();
            opHasher.Compress((ulong)(byte)op.VirtualThreadId);
            opHasher.Compress((ulong)(uint)op.OwnerContextId);
            opHasher.Compress(op.Placement.DomainTag);
            return opHasher.Finalize();
        }
    }

    /// <summary>
    /// Explicit replay/template key for SMT bundle certificates.
    /// Replay epoch is only one part of the scope; boundary and owner/domain context
    /// are part of the key rather than hidden scheduler-side assumptions.
    /// </summary>
    internal readonly struct PhaseCertificateTemplateKey4Way : IEquatable<PhaseCertificateTemplateKey4Way>
    {
        public PhaseCertificateTemplateKey4Way(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificateIdentity4Way certificateIdentity,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            PhaseKey = phaseKey;
            CertificateIdentity = certificateIdentity;
            BundleMetadata = bundleMetadata;
            BoundaryGuard = boundaryGuard;
        }

        public ReplayPhaseKey PhaseKey { get; }

        public BundleResourceCertificateIdentity4Way CertificateIdentity { get; }

        public SmtBundleMetadata4Way BundleMetadata { get; }

        public BoundaryGuardState BoundaryGuard { get; }

        public bool IsValid =>
            PhaseKey.IsValid &&
            CertificateIdentity.IsValid &&
            BundleMetadata.IsValid &&
            BoundaryGuard.IsValid;

        public bool Equals(PhaseCertificateTemplateKey4Way other)
        {
            return PhaseKey.Equals(other.PhaseKey) &&
                   CertificateIdentity.Equals(other.CertificateIdentity) &&
                   BundleMetadata.Equals(other.BundleMetadata) &&
                   BoundaryGuard.Equals(other.BoundaryGuard);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhaseCertificateTemplateKey4Way other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                PhaseKey,
                CertificateIdentity,
                BundleMetadata,
                BoundaryGuard);
        }
    }

    /// <summary>
    /// Reusable certificate template for replay-stable 4-way SMT bundle packing.
    /// </summary>
    internal readonly struct PhaseCertificateTemplate4Way
    {
        public PhaseCertificateTemplate4Way(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate)
        {
            TemplateKey = templateKey;
            Certificate = certificate;
        }

        public PhaseCertificateTemplateKey4Way TemplateKey { get; }

        public ReplayPhaseKey PhaseKey => TemplateKey.PhaseKey;

        public BundleResourceCertificate4Way Certificate { get; }

        public BundleResourceCertificateIdentity4Way CertificateIdentity => TemplateKey.CertificateIdentity;

        public SmtBundleMetadata4Way BundleMetadata => TemplateKey.BundleMetadata;

        public BoundaryGuardState BoundaryGuard => TemplateKey.BoundaryGuard;

        public bool IsValid => TemplateKey.IsValid;

        public bool Matches(ReplayPhaseContext phase)
        {
            return IsValid && phase.Matches(TemplateKey.PhaseKey);
        }

        public bool Matches(PhaseCertificateTemplateKey4Way templateKey)
        {
            return IsValid && TemplateKey.Equals(templateKey);
        }
    }

    /// <summary>
    /// Explicit scheduler-facing cache seam for SMT legality certificate reuse.
    /// Owns replay-template lifecycle so the scheduler no longer treats the raw
    /// <see cref="PhaseCertificateTemplate4Way"/> as its local source of truth.
    /// </summary>
    internal struct SmtLegalityCertificateCache4Way : ISmtLegalityCertificateCache4Way
    {
        private PhaseCertificateTemplate4Way _template;

        public bool IsValid => _template.IsValid;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(ReplayPhaseContext phase)
        {
            return _template.Matches(phase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            MicroOp candidate)
        {
            LegalityDecision decision = legalityChecker.EvaluateSmtLegality(
                bundleCertificate,
                liveTemplateKey,
                _template,
                candidate);
            return new LegalityCertificateCacheEvaluation<LegalityDecision>(
                decision,
                LegalityCertificateCacheTelemetry.FromReuseDecision(
                    decision.AttemptedReplayCertificateReuse,
                    decision.ReusedReplayCertificate,
                    bundleCertificate.OperationCount));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prepare(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate)
        {
            if (!templateKey.IsValid || _template.Matches(templateKey))
                return;

            _template = new PhaseCertificateTemplate4Way(templateKey, certificate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate)
        {
            bool wasInvalidated = Invalidate();
            _template = templateKey.IsValid
                ? new PhaseCertificateTemplate4Way(templateKey, certificate)
                : default;
            return LegalityCertificateCacheTelemetry.FromInvalidationEvent(
                ReplayPhaseInvalidationReason.CertificateMutation,
                wasInvalidated);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Invalidate()
        {
            bool wasValid = _template.IsValid;
            _template = default;
            return wasValid;
        }
    }

    /// <summary>
    /// Scheduler-side Phase 1 readiness telemetry.
    /// </summary>
    public readonly struct SchedulerPhaseMetrics
    {
        public long ReplayAwareCycles { get; init; }

        public long PhaseCertificateReadyHits { get; init; }

        public long PhaseCertificateReadyMisses { get; init; }

        public long EstimatedChecksSaved { get; init; }

        public long PhaseCertificateInvalidations { get; init; }

        public long PhaseCertificateMutationInvalidations { get; init; }

        public long PhaseCertificatePhaseMismatchInvalidations { get; init; }

        public ReplayPhaseInvalidationReason LastCertificateInvalidationReason { get; init; }

        public long DeterminismReferenceOpportunitySlots { get; init; }

        public long DeterminismReplayEligibleSlots { get; init; }

        public long DeterminismMaskedSlots { get; init; }

        public long DeterminismEstimatedLostSlots { get; init; }

        public long DeterminismConstrainedCycles { get; init; }

        public long DomainIsolationProbeAttempts { get; init; }

        public long DomainIsolationBlockedAttempts { get; init; }

        public long DomainIsolationCrossDomainBlocks { get; init; }

        public long DomainIsolationKernelToUserBlocks { get; init; }

        public long EligibilityMaskedCycles { get; init; }

        public long EligibilityMaskedReadyCandidates { get; init; }

        public byte LastEligibilityRequestedMask { get; init; }

        public byte LastEligibilityNormalizedMask { get; init; }

        public byte LastEligibilityReadyPortMask { get; init; }

        public byte LastEligibilityVisibleReadyMask { get; init; }

        public byte LastEligibilityMaskedReadyMask { get; init; }

        // ── Phase 08: Typed-Slot Telemetry ─────────────────────────

        public long ClassTemplateReuseHits { get; init; }

        public long ClassTemplateInvalidations { get; init; }

        public long TypedSlotFastPathAccepts { get; init; }

        public long TypedSlotStandardPathAccepts { get; init; }

        public long TotalLaneBindings { get; init; }

        public long LaneReuseHits { get; init; }

        public long LaneReuseMisses { get; init; }

        public long AluClassInjects { get; init; }

        public long LsuClassInjects { get; init; }

        public long DmaStreamClassInjects { get; init; }

        public long BranchControlInjects { get; init; }

        public long HardPinnedInjects { get; init; }

        public long ClassFlexibleInjects { get; init; }

        public long NopAvoided { get; init; }

        public long NopDueToPinnedConstraint { get; init; }

        public long NopDueToNoClassCapacity { get; init; }

        public long NopDueToResourceConflict { get; init; }

        public long NopDueToDynamicState { get; init; }

        public long StaticClassOvercommitRejects { get; init; }

        public long DynamicClassExhaustionRejects { get; init; }

        public long PinnedLaneConflicts { get; init; }

        public long LateBindingConflicts { get; init; }

        public long TypedSlotDomainRejects { get; init; }

        public long SmtOwnerContextGuardRejects { get; init; }

        public long SmtDomainGuardRejects { get; init; }

        public long SmtBoundaryGuardRejects { get; init; }

        public long SmtSharedResourceCertificateRejects { get; init; }

        public long SmtRegisterGroupCertificateRejects { get; init; }

        public RejectKind LastSmtLegalityRejectKind { get; init; }

        public LegalityAuthoritySource LastSmtLegalityAuthoritySource { get; init; }

        public long SmtLegalityRejectByAluClass { get; init; }

        public long SmtLegalityRejectByLsuClass { get; init; }

        public long SmtLegalityRejectByDmaStreamClass { get; init; }

        public long SmtLegalityRejectByBranchControl { get; init; }

        public long SmtLegalityRejectBySystemSingleton { get; init; }

        public long ClassTemplateDomainInvalidations { get; init; }

        public long ClassTemplateCapacityMismatchInvalidations { get; init; }

        public long CertificateRejectByAluClass { get; init; }

        public long CertificateRejectByLsuClass { get; init; }

        public long CertificateRejectByDmaStreamClass { get; init; }

        public long CertificateRejectByBranchControl { get; init; }

        public long CertificateRejectBySystemSingleton { get; init; }

        public long CertificateRegGroupConflictVT0 { get; init; }

        public long CertificateRegGroupConflictVT1 { get; init; }

        public long CertificateRegGroupConflictVT2 { get; init; }

        public long CertificateRegGroupConflictVT3 { get; init; }

        public long RejectionsVT0 { get; init; }

        public long RejectionsVT1 { get; init; }

        public long RejectionsVT2 { get; init; }

        public long RejectionsVT3 { get; init; }

        public long RegGroupConflictsVT0 { get; init; }

        public long RegGroupConflictsVT1 { get; init; }

        public long RegGroupConflictsVT2 { get; init; }

        public long RegGroupConflictsVT3 { get; init; }

        public long BankPendingRejectBank0 { get; init; }

        public long BankPendingRejectBank1 { get; init; }

        public long BankPendingRejectBank2 { get; init; }

        public long BankPendingRejectBank3 { get; init; }

        public long BankPendingRejectBank4 { get; init; }

        public long BankPendingRejectBank5 { get; init; }

        public long BankPendingRejectBank6 { get; init; }

        public long BankPendingRejectBank7 { get; init; }

        public long BankPendingRejectBank8 { get; init; }

        public long BankPendingRejectBank9 { get; init; }

        public long BankPendingRejectBank10 { get; init; }

        public long BankPendingRejectBank11 { get; init; }

        public long BankPendingRejectBank12 { get; init; }

        public long BankPendingRejectBank13 { get; init; }

        public long BankPendingRejectBank14 { get; init; }

        public long BankPendingRejectBank15 { get; init; }

        public long MemoryClusteringEvents { get; init; }

        public long TypedSlotHardwareBudgetRejects { get; init; }

        public long TypedSlotAssistQuotaRejects { get; init; }

        public long TypedSlotAssistBackpressureRejects { get; init; }

        // ── Phase 04: Serialising-Event Epoch Telemetry ─────────────

        /// <summary>
        /// Number of <see cref="PackBundleIntraCoreSmt"/> calls that were short-circuited
        /// because the owner bundle contained a <see cref="Arch.SerializationClass.FullSerial"/>
        /// or <see cref="Arch.SerializationClass.VmxSerial"/> operation (G33).
        /// </summary>
        public long SerializingBoundaryRejects { get; init; }

        /// <summary>
        /// Number of epoch boundaries created by <see cref="MicroOpScheduler.NotifySerializingCommit"/>
        /// calls (G34). Each call corresponds to one serialising instruction being committed.
        /// </summary>
        public long SerializingEpochCount { get; init; }

        public long AssistNominations { get; init; }

        public long AssistInjections { get; init; }

        public long AssistRejects { get; init; }

        public long AssistBoundaryRejects { get; init; }

        public long AssistInvalidations { get; init; }

        public long AssistInterCoreNominations { get; init; }

        public long AssistInterCoreInjections { get; init; }

        public long AssistInterCoreRejects { get; init; }

        public long AssistInterCoreDomainRejects { get; init; }

        public long AssistInterCorePodLocalInjections { get; init; }

        public long AssistInterCoreCrossPodInjections { get; init; }

        public long AssistInterCorePodLocalRejects { get; init; }

        public long AssistInterCoreCrossPodRejects { get; init; }

        public long AssistInterCorePodLocalDomainRejects { get; init; }

        public long AssistInterCoreCrossPodDomainRejects { get; init; }

        public long AssistInterCoreSameVtVectorInjects { get; init; }

        public long AssistInterCoreDonorVtVectorInjects { get; init; }

        public long AssistInterCoreSameVtVectorWritebackInjects { get; init; }

        public long AssistInterCoreDonorVtVectorWritebackInjects { get; init; }

        public long AssistInterCoreLane6DefaultStoreDonorPrefetchInjects { get; init; }

        public long AssistInterCoreLane6HotLoadDonorPrefetchInjects { get; init; }

        public long AssistInterCoreLane6HotStoreDonorPrefetchInjects { get; init; }

        public long AssistInterCoreLane6DonorPrefetchInjects { get; init; }

        public long AssistInterCoreLane6ColdStoreLdsaInjects { get; init; }

        public long AssistInterCoreLane6LdsaInjects { get; init; }

        public long AssistQuotaRejects { get; init; }

        public long AssistQuotaIssueRejects { get; init; }

        public long AssistQuotaLineRejects { get; init; }

        public long AssistQuotaLinesReserved { get; init; }

        public long AssistBackpressureRejects { get; init; }

        public long AssistBackpressureOuterCapRejects { get; init; }

        public long AssistBackpressureMshrRejects { get; init; }

        public long AssistBackpressureDmaSrfRejects { get; init; }

        public long AssistDonorPrefetchInjects { get; init; }

        public long AssistLdsaInjects { get; init; }

        public long AssistVdsaInjects { get; init; }

        public long AssistSameVtInjects { get; init; }

        public long AssistDonorVtInjects { get; init; }

        public AssistInvalidationReason LastAssistInvalidationReason { get; init; }

        public ulong LastAssistOwnershipSignature { get; init; }

        public IReadOnlyList<LoopPhaseClassProfile>? LoopPhaseProfiles { get; init; }
    }
}
