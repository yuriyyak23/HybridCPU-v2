using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09ReplayCertificateCoordinatorProofTests
{
    [Fact]
    public void InvalidatePhaseMismatch_WhenOnlyInterCoreCacheMismatches_ThenInvalidatesOnlyThatCacheAndRecordsSingleTelemetryEvent()
    {
        var telemetrySink = new RecordingLegalityCertificateCacheTelemetrySink();
        var interCoreCache = new StubInterCoreLegalityCertificateCache
        {
            IsValidValue = true,
            MatchesValue = false,
            InvalidateValue = true
        };
        var smtCache = new StubSmtLegalityCertificateCache4Way
        {
            IsValidValue = true,
            MatchesValue = true,
            InvalidateValue = true
        };
        var coordinator = new LegalityCertificateCacheCoordinator(telemetrySink, interCoreCache, smtCache);
        var phase = CreateReplayPhaseContext();

        coordinator.InvalidatePhaseMismatch(phase);

        Assert.Equal(1, interCoreCache.InvalidateCalls);
        Assert.Equal(0, smtCache.InvalidateCalls);
        Assert.Empty(telemetrySink.HitEstimatedChecksSaved);
        Assert.Equal(0, telemetrySink.MissCalls);
        Assert.Single(telemetrySink.InvalidationReasons);
        Assert.Equal(ReplayPhaseInvalidationReason.PhaseMismatch, telemetrySink.InvalidationReasons[0]);
    }

    [Fact]
    public void InvalidatePhaseMismatch_WhenCachesAlreadyMatchOrAreInvalid_ThenDoesNotEmitSpuriousTelemetry()
    {
        var telemetrySink = new RecordingLegalityCertificateCacheTelemetrySink();
        var interCoreCache = new StubInterCoreLegalityCertificateCache
        {
            IsValidValue = false,
            MatchesValue = false,
            InvalidateValue = true
        };
        var smtCache = new StubSmtLegalityCertificateCache4Way
        {
            IsValidValue = true,
            MatchesValue = true,
            InvalidateValue = true
        };
        var coordinator = new LegalityCertificateCacheCoordinator(telemetrySink, interCoreCache, smtCache);
        var phase = CreateReplayPhaseContext();

        coordinator.InvalidatePhaseMismatch(phase);

        Assert.Equal(0, interCoreCache.InvalidateCalls);
        Assert.Equal(0, smtCache.InvalidateCalls);
        Assert.Empty(telemetrySink.HitEstimatedChecksSaved);
        Assert.Equal(0, telemetrySink.MissCalls);
        Assert.Empty(telemetrySink.InvalidationReasons);
    }

    private static ReplayPhaseContext CreateReplayPhaseContext()
    {
        return new ReplayPhaseContext(
            isActive: true,
            epochId: 7,
            cachedPc: 0x4000,
            epochLength: 4,
            completedReplays: 1,
            validSlotCount: 2,
            stableDonorMask: 0b0000_0011,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None);
    }

    private sealed class RecordingLegalityCertificateCacheTelemetrySink : ILegalityCertificateCacheTelemetrySink
    {
        public List<long> HitEstimatedChecksSaved { get; } = new();

        public int MissCalls { get; private set; }

        public List<ReplayPhaseInvalidationReason> InvalidationReasons { get; } = new();

        public void RecordLegalityCertificateCacheHit(long estimatedChecksSaved)
            => HitEstimatedChecksSaved.Add(estimatedChecksSaved);

        public void RecordLegalityCertificateCacheMiss()
            => MissCalls++;

        public void RecordLegalityCertificateCacheInvalidation(ReplayPhaseInvalidationReason reason)
            => InvalidationReasons.Add(reason);
    }

    private sealed class StubInterCoreLegalityCertificateCache : IInterCoreLegalityCertificateCache
    {
        public bool IsValidValue { get; set; }

        public bool MatchesValue { get; set; }

        public bool InvalidateValue { get; set; }

        public int InvalidateCalls { get; private set; }

        public bool IsValid => IsValidValue;

        public bool Matches(ReplayPhaseContext phase)
            => MatchesValue;

        public void Prepare(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate)
            => throw new NotSupportedException();

        public LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
            => throw new NotSupportedException();

        public LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey templateKey,
            BundleResourceCertificate certificate)
            => throw new NotSupportedException();

        public bool Invalidate()
        {
            InvalidateCalls++;
            IsValidValue = false;
            return InvalidateValue;
        }
    }

    private sealed class StubSmtLegalityCertificateCache4Way : ISmtLegalityCertificateCache4Way
    {
        public bool IsValidValue { get; set; }

        public bool MatchesValue { get; set; }

        public bool InvalidateValue { get; set; }

        public int InvalidateCalls { get; private set; }

        public bool IsValid => IsValidValue;

        public bool Matches(ReplayPhaseContext phase)
            => MatchesValue;

        public LegalityCertificateCacheEvaluation<LegalityDecision> EvaluateLegality(
            ILegalityChecker legalityChecker,
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            MicroOp candidate)
            => throw new NotSupportedException();

        public void Prepare(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate)
            => throw new NotSupportedException();

        public LegalityCertificateCacheTelemetry RefreshAfterMutation(
            PhaseCertificateTemplateKey4Way templateKey,
            BundleResourceCertificate4Way certificate)
            => throw new NotSupportedException();

        public bool Invalidate()
        {
            InvalidateCalls++;
            IsValidValue = false;
            return InvalidateValue;
        }
    }
}
