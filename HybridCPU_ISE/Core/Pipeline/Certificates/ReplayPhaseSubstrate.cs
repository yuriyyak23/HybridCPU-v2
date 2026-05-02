using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.Diagnostics;


namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Canonical invalidation reasons for replay-phase aware runtime state.
    /// </summary>
    public enum ReplayPhaseInvalidationReason : byte
    {
        None = 0,
        Completed = 1,
        PcMismatch = 2,
        Manual = 3,
        CertificateMutation = 4,
        PhaseMismatch = 5,
        InactivePhase = 6,

        // Phase 07: class-level template invalidation reasons
        DomainBoundary = 7,
        ClassCapacityMismatch = 8,
        ClassTemplateExpired = 9,

        // Phase 04: serialising-event epoch boundary
        SerializingEvent = 10,

        // DmaStreamCompute Task D: overlapping write footprint invalidates replay evidence.
        MemoryFootprintOverlap = 11,

        // DmaStreamCompute Task G: explicit replay evidence envelope mismatches.
        DmaStreamComputeDescriptorMismatch = 12,
        DmaStreamComputeDescriptorPayloadLost = 13,
        DmaStreamComputeCarrierMismatch = 14,
        DmaStreamComputeFootprintMismatch = 15,
        DmaStreamComputeOwnerDomainMismatch = 16,
        DmaStreamComputeCertificateInputMismatch = 17,
        DmaStreamComputeTokenEvidenceMismatch = 18,
        DmaStreamComputeLanePlacementMismatch = 19,
        DmaStreamComputeIncompleteEvidence = 20
    }

    /// <summary>
    /// Class-level replay template: captures per-class free capacity pattern
    /// rather than exact physical slot positions.
    /// More stable across replay iterations where lane drift is expected.
    /// HLS: 5 × 3-bit values = 15 flip-flops.
    /// </summary>
    public readonly struct ClassCapacityTemplate : IEquatable<ClassCapacityTemplate>
    {
        /// <summary>Free ALU-class lanes at template capture time.</summary>
        public byte AluFree { get; }

        /// <summary>Free LSU-class lanes at template capture time.</summary>
        public byte LsuFree { get; }

        /// <summary>Free DmaStream-class lanes at template capture time.</summary>
        public byte DmaStreamFree { get; }

        /// <summary>Free BranchControl-class lanes at template capture time.</summary>
        public byte BranchControlFree { get; }

        /// <summary>Free SystemSingleton-class lanes at template capture time.</summary>
        public byte SystemSingletonFree { get; }

        /// <summary>
        /// Construct a class-capacity template from the current capacity state.
        /// Clamps negative free counts to 0 (possible when over-counted).
        /// </summary>
        public ClassCapacityTemplate(SlotClassCapacityState state)
        {
            AluFree = ClampToByte(state.GetFreeCapacity(SlotClass.AluClass));
            LsuFree = ClampToByte(state.GetFreeCapacity(SlotClass.LsuClass));
            DmaStreamFree = ClampToByte(state.GetFreeCapacity(SlotClass.DmaStreamClass));
            BranchControlFree = ClampToByte(state.GetFreeCapacity(SlotClass.BranchControl));
            SystemSingletonFree = ClampToByte(state.GetFreeCapacity(SlotClass.SystemSingleton));
        }

        /// <summary>
        /// Check if this template is compatible with current class capacity.
        /// Compatible = for every class, current free ≥ template free.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCompatibleWith(SlotClassCapacityState current)
        {
            return current.GetFreeCapacity(SlotClass.AluClass) >= AluFree
                && current.GetFreeCapacity(SlotClass.LsuClass) >= LsuFree
                && current.GetFreeCapacity(SlotClass.DmaStreamClass) >= DmaStreamFree
                && current.GetFreeCapacity(SlotClass.BranchControl) >= BranchControlFree
                && current.GetFreeCapacity(SlotClass.SystemSingleton) >= SystemSingletonFree;
        }

        /// <summary>
        /// Get the captured free capacity for a specific slot class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCapturedFree(SlotClass slotClass) => slotClass switch
        {
            SlotClass.AluClass => AluFree,
            SlotClass.LsuClass => LsuFree,
            SlotClass.DmaStreamClass => DmaStreamFree,
            SlotClass.BranchControl => BranchControlFree,
            SlotClass.SystemSingleton => SystemSingletonFree,
            _ => 0,
        };

        public bool Equals(ClassCapacityTemplate other) =>
            AluFree == other.AluFree && LsuFree == other.LsuFree &&
            DmaStreamFree == other.DmaStreamFree &&
            BranchControlFree == other.BranchControlFree &&
            SystemSingletonFree == other.SystemSingletonFree;

        public override bool Equals(object? obj) =>
            obj is ClassCapacityTemplate other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(AluFree, LsuFree, DmaStreamFree, BranchControlFree, SystemSingletonFree);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampToByte(int value) => value <= 0 ? (byte)0 : (byte)value;
    }

    /// <summary>
    /// Mutable per-class budget for decrement-aware template fast path.
    /// Each successful injection decrements the budget for the consumed class.
    /// When a class budget reaches 0, the fast path is disabled for that class.
    /// HLS: 5 × 3-bit counters = 15 flip-flops.
    /// </summary>
    public struct TemplateBudget
    {
        private byte _aluRemaining;
        private byte _lsuRemaining;
        private byte _dmaStreamRemaining;
        private byte _branchControlRemaining;
        private byte _systemSingletonRemaining;

        /// <summary>
        /// Initialize the budget from a class-capacity template.
        /// </summary>
        public TemplateBudget(ClassCapacityTemplate template)
        {
            _aluRemaining = template.AluFree;
            _lsuRemaining = template.LsuFree;
            _dmaStreamRemaining = template.DmaStreamFree;
            _branchControlRemaining = template.BranchControlFree;
            _systemSingletonRemaining = template.SystemSingletonFree;
        }

        /// <summary>
        /// Get remaining budget for a slot class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int GetRemaining(SlotClass slotClass) => slotClass switch
        {
            SlotClass.AluClass => _aluRemaining,
            SlotClass.LsuClass => _lsuRemaining,
            SlotClass.DmaStreamClass => _dmaStreamRemaining,
            SlotClass.BranchControl => _branchControlRemaining,
            SlotClass.SystemSingleton => _systemSingletonRemaining,
            _ => 0,
        };

        /// <summary>
        /// Decrement the budget for a slot class after a successful injection.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Decrement(SlotClass slotClass)
        {
            switch (slotClass)
            {
                case SlotClass.AluClass:        if (_aluRemaining > 0) _aluRemaining--; break;
                case SlotClass.LsuClass:        if (_lsuRemaining > 0) _lsuRemaining--; break;
                case SlotClass.DmaStreamClass:  if (_dmaStreamRemaining > 0) _dmaStreamRemaining--; break;
                case SlotClass.BranchControl:   if (_branchControlRemaining > 0) _branchControlRemaining--; break;
                case SlotClass.SystemSingleton: if (_systemSingletonRemaining > 0) _systemSingletonRemaining--; break;
            }
        }
    }

    /// <summary>
    /// Stable identity of a replay epoch that can key future certificate reuse.
    /// </summary>
    public readonly struct ReplayPhaseKey : IEquatable<ReplayPhaseKey>
    {
        public ReplayPhaseKey(ulong epochId, ulong cachedPc, byte stableDonorMask, int validSlotCount)
        {
            EpochId = epochId;
            CachedPc = cachedPc;
            StableDonorMask = stableDonorMask;
            ValidSlotCount = validSlotCount;
            ClassTemplate = default;
            DomainScopeId = 0;
        }

        /// <summary>
        /// Phase 07: Extended constructor with class-capacity template and domain scope.
        /// </summary>
        public ReplayPhaseKey(ulong epochId, ulong cachedPc, byte stableDonorMask, int validSlotCount,
                              ClassCapacityTemplate classTemplate, int domainScopeId)
        {
            EpochId = epochId;
            CachedPc = cachedPc;
            StableDonorMask = stableDonorMask;
            ValidSlotCount = validSlotCount;
            ClassTemplate = classTemplate;
            DomainScopeId = domainScopeId;
        }

        public ulong EpochId { get; }

        public ulong CachedPc { get; }

        public byte StableDonorMask { get; }

        public int ValidSlotCount { get; }

        /// <summary>Phase 07: class-level capacity template for stable replay reuse.</summary>
        public ClassCapacityTemplate ClassTemplate { get; }

        /// <summary>Phase 07: domain scope identifier for template isolation.</summary>
        public int DomainScopeId { get; }

        public bool IsValid => EpochId != 0;

        public bool Equals(ReplayPhaseKey other)
        {
            return EpochId == other.EpochId &&
                   CachedPc == other.CachedPc &&
                   StableDonorMask == other.StableDonorMask &&
                   ValidSlotCount == other.ValidSlotCount;
        }

        public override bool Equals(object? obj)
        {
            return obj is ReplayPhaseKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EpochId, CachedPc, StableDonorMask, ValidSlotCount);
        }
    }

    /// <summary>
    /// Shared replay/epoch vocabulary for phase-aware scheduling and validation.
    /// </summary>
    public readonly struct ReplayPhaseContext
    {
        public ReplayPhaseContext(
            bool isActive,
            ulong epochId,
            ulong cachedPc,
            ulong epochLength,
            ulong completedReplays,
            int validSlotCount,
            byte stableDonorMask,
            ReplayPhaseInvalidationReason lastInvalidationReason)
        {
            IsActive = isActive;
            EpochId = epochId;
            CachedPc = cachedPc;
            EpochLength = epochLength;
            CompletedReplays = completedReplays;
            ValidSlotCount = validSlotCount;
            StableDonorMask = stableDonorMask;
            LastInvalidationReason = lastInvalidationReason;
        }

        public bool IsActive { get; }

        public ulong EpochId { get; }

        public ulong CachedPc { get; }

        public ulong EpochLength { get; }

        public ulong CompletedReplays { get; }

        public int ValidSlotCount { get; }

        public byte StableDonorMask { get; }

        public ReplayPhaseInvalidationReason LastInvalidationReason { get; }

        public ReplayPhaseKey Key => new ReplayPhaseKey(EpochId, CachedPc, StableDonorMask, ValidSlotCount);

        public int StableDonorSlotCount => CountBits(StableDonorMask);

        public bool HasStableDonorStructure => IsActive && CompletedReplays > 0 && StableDonorMask != 0;

        public bool CanReusePhaseCertificate => IsActive && Key.IsValid;

        public bool IsDonorStable(int slotIndex)
        {
            if ((uint)slotIndex >= 8)
            {
                return false;
            }

            return (StableDonorMask & (1 << slotIndex)) != 0;
        }

        public bool Matches(ReplayPhaseKey key)
        {
            return IsActive && Key.Equals(key);
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            byte remaining = value;
            while (remaining != 0)
            {
                count += remaining & 1;
                remaining >>= 1;
            }

            return count;
        }
    }

    /// <summary>
    /// Replay-epoch telemetry baseline for Phase 1 observability.
    /// </summary>
    public readonly struct ReplayPhaseMetrics
    {
        public ulong ReplayEpochCount { get; init; }

        public ulong TotalEpochLength { get; init; }

        public ulong StableDonorSlotSamples { get; init; }

        public ulong TotalReplaySlotSamples { get; init; }

        public ulong DeterministicTransitionCount { get; init; }

        public double AverageEpochLength => ReplayEpochCount == 0 ? 0.0 : (double)TotalEpochLength / ReplayEpochCount;

        public double StableDonorSlotRatio => TotalReplaySlotSamples == 0 ? 0.0 : (double)StableDonorSlotSamples / TotalReplaySlotSamples;
    }

    /// <summary>
    /// Explicit telemetry delta emitted by legality certificate cache seams.
    /// Scheduler counters consume this transport directly instead of inferring
    /// replay reuse or invalidation semantics from checker decisions.
    /// </summary>
    internal readonly struct LegalityCertificateCacheTelemetry
    {
        public LegalityCertificateCacheTelemetry(
            long readyHitsDelta,
            long readyMissesDelta,
            long estimatedChecksSavedDelta,
            long invalidationEventsDelta,
            long mutationInvalidationsDelta,
            long phaseMismatchInvalidationsDelta,
            ReplayPhaseInvalidationReason lastInvalidationReason)
        {
            ReadyHitsDelta = readyHitsDelta;
            ReadyMissesDelta = readyMissesDelta;
            EstimatedChecksSavedDelta = estimatedChecksSavedDelta;
            InvalidationEventsDelta = invalidationEventsDelta;
            MutationInvalidationsDelta = mutationInvalidationsDelta;
            PhaseMismatchInvalidationsDelta = phaseMismatchInvalidationsDelta;
            LastInvalidationReason = lastInvalidationReason;
        }

        public long ReadyHitsDelta { get; }

        public long ReadyMissesDelta { get; }

        public long EstimatedChecksSavedDelta { get; }

        public long InvalidationEventsDelta { get; }

        public long MutationInvalidationsDelta { get; }

        public long PhaseMismatchInvalidationsDelta { get; }

        public ReplayPhaseInvalidationReason LastInvalidationReason { get; }

        public static LegalityCertificateCacheTelemetry FromReuseDecision(
            bool attemptedReplayReuse,
            bool reusedReplayCertificate,
            long estimatedChecksSaved)
        {
            if (!attemptedReplayReuse)
                return default;

            return reusedReplayCertificate
                ? new LegalityCertificateCacheTelemetry(
                    readyHitsDelta: 1,
                    readyMissesDelta: 0,
                    estimatedChecksSavedDelta: Math.Max(estimatedChecksSaved, 1),
                    invalidationEventsDelta: 0,
                    mutationInvalidationsDelta: 0,
                    phaseMismatchInvalidationsDelta: 0,
                    lastInvalidationReason: ReplayPhaseInvalidationReason.None)
                : new LegalityCertificateCacheTelemetry(
                    readyHitsDelta: 0,
                    readyMissesDelta: 1,
                    estimatedChecksSavedDelta: 0,
                    invalidationEventsDelta: 0,
                    mutationInvalidationsDelta: 0,
                    phaseMismatchInvalidationsDelta: 0,
                    lastInvalidationReason: ReplayPhaseInvalidationReason.None);
        }

        public static LegalityCertificateCacheTelemetry FromInvalidationEvent(
            ReplayPhaseInvalidationReason reason,
            bool anyCacheInvalidated)
        {
            if (!anyCacheInvalidated)
                return default;

            return new LegalityCertificateCacheTelemetry(
                readyHitsDelta: 0,
                readyMissesDelta: 0,
                estimatedChecksSavedDelta: 0,
                invalidationEventsDelta: 1,
                mutationInvalidationsDelta: reason == ReplayPhaseInvalidationReason.CertificateMutation ? 1 : 0,
                phaseMismatchInvalidationsDelta: reason == ReplayPhaseInvalidationReason.PhaseMismatch ||
                                                 reason == ReplayPhaseInvalidationReason.InactivePhase ? 1 : 0,
                lastInvalidationReason: reason);
        }
    }

    /// <summary>
    /// Generic legality result plus explicit cache telemetry delta.
    /// </summary>
    internal readonly struct LegalityCertificateCacheEvaluation<TResult>
    {
        public LegalityCertificateCacheEvaluation(
            TResult legalityResult,
            LegalityCertificateCacheTelemetry telemetry)
        {
            LegalityResult = legalityResult;
            Telemetry = telemetry;
        }

        public TResult LegalityResult { get; }

        public LegalityCertificateCacheTelemetry Telemetry { get; }
    }

    /// <summary>
    /// Explicit replay/template key for inter-core bundle certificates.
    /// Replay epoch and structural certificate identity are both part of cache validity.
    /// </summary>
    internal readonly struct PhaseCertificateTemplateKey : IEquatable<PhaseCertificateTemplateKey>
    {
        public PhaseCertificateTemplateKey(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificateIdentity certificateIdentity)
        {
            PhaseKey = phaseKey;
            CertificateIdentity = certificateIdentity;
        }

        public ReplayPhaseKey PhaseKey { get; }

        public BundleResourceCertificateIdentity CertificateIdentity { get; }

        public bool IsValid => PhaseKey.IsValid && CertificateIdentity.IsValid;

        public bool Equals(PhaseCertificateTemplateKey other)
        {
            return PhaseKey.Equals(other.PhaseKey) &&
                   CertificateIdentity.Equals(other.CertificateIdentity);
        }

        public override bool Equals(object? obj)
        {
            return obj is PhaseCertificateTemplateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PhaseKey, CertificateIdentity);
        }
    }

    /// <summary>
    /// Reusable certificate template for replay-stable inter-thread bundles.
    /// </summary>
    internal readonly struct PhaseCertificateTemplate
    {
        public PhaseCertificateTemplate(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate)
        {
            TemplateKey = templateKey;
            Certificate = certificate;
        }

        public PhaseCertificateTemplateKey TemplateKey { get; }

        public ReplayPhaseKey PhaseKey => TemplateKey.PhaseKey;

        public BundleResourceCertificateIdentity CertificateIdentity => TemplateKey.CertificateIdentity;

        public BundleResourceCertificate? Certificate { get; }

        public bool IsValid => TemplateKey.IsValid && Certificate != null && Certificate.IsValid();

        public bool Matches(ReplayPhaseContext phase)
        {
            return IsValid && phase.Matches(TemplateKey.PhaseKey);
        }

        public bool Matches(PhaseCertificateTemplateKey templateKey)
        {
            return IsValid && TemplateKey.Equals(templateKey);
        }
    }
}
