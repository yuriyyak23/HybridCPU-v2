using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SafetyVerifierGuardMatrixProofTests
{
    [Fact]
    public void EvaluateSmtBoundaryGuard_WhenSerializingBoundaryIsClosed_ReturnsGuardPlaneBoundaryReject()
    {
        var verifier = new SafetyVerifier();
        PhaseCertificateTemplateKey4Way liveTemplateKey = CreateLiveTemplateKey(
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            boundaryGuard: new BoundaryGuardState(7, SmtReplayBoundaryMode.SerializingBundle),
            out _,
            out _);

        LegalityDecision decision = verifier.EvaluateSmtBoundaryGuard(liveTemplateKey);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RejectKind.Boundary, decision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, decision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, decision.AuthoritySource);
        Assert.False(decision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void EvaluateSmtLegality_WhenBoundaryGuardBlocks_ReturnsBoundaryRejectBeforeReplayReuse()
    {
        var verifier = new SafetyVerifier();
        MicroOp candidate = CreateScopedScalarAlu(
            virtualThreadId: 1,
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            destReg: 10,
            src1Reg: 11,
            src2Reg: 12);
        PhaseCertificateTemplateKey4Way liveTemplateKey = CreateLiveTemplateKey(
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            boundaryGuard: new BoundaryGuardState(7, SmtReplayBoundaryMode.SerializingBundle),
            out BundleResourceCertificate4Way bundleCertificate,
            out _);
        PhaseCertificateTemplate4Way phaseTemplate = new(liveTemplateKey, bundleCertificate);

        LegalityDecision decision = verifier.EvaluateSmtLegality(
            bundleCertificate,
            liveTemplateKey,
            phaseTemplate,
            candidate);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RejectKind.Boundary, decision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, decision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, decision.AuthoritySource);
        Assert.False(decision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void EvaluateSmtLegality_WhenOwnerContextMismatchesKnownBundleOwner_ReturnsOwnerMismatchGuardReject()
    {
        var verifier = new SafetyVerifier();
        MicroOp candidate = CreateScopedScalarAlu(
            virtualThreadId: 1,
            ownerContextId: 200,
            ownerDomainTag: 0x2,
            destReg: 10,
            src1Reg: 11,
            src2Reg: 12);
        PhaseCertificateTemplateKey4Way liveTemplateKey = CreateLiveTemplateKey(
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            boundaryGuard: BoundaryGuardState.Open(7),
            out BundleResourceCertificate4Way bundleCertificate,
            out _);
        PhaseCertificateTemplate4Way phaseTemplate = new(liveTemplateKey, bundleCertificate);

        LegalityDecision decision = verifier.EvaluateSmtLegality(
            bundleCertificate,
            liveTemplateKey,
            phaseTemplate,
            candidate);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RejectKind.OwnerMismatch, decision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, decision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, decision.AuthoritySource);
        Assert.False(decision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void EvaluateSmtLegality_WhenOwnerDomainProbeRejects_ReturnsDomainMismatchGuardReject()
    {
        var verifier = new SafetyVerifier();
        MicroOp candidate = CreateScopedScalarAlu(
            virtualThreadId: 1,
            ownerContextId: 100,
            ownerDomainTag: 0x4,
            destReg: 10,
            src1Reg: 11,
            src2Reg: 12);
        PhaseCertificateTemplateKey4Way liveTemplateKey = CreateLiveTemplateKey(
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            boundaryGuard: BoundaryGuardState.Open(7),
            out BundleResourceCertificate4Way bundleCertificate,
            out _);
        PhaseCertificateTemplate4Way phaseTemplate = new(liveTemplateKey, bundleCertificate);

        LegalityDecision decision = verifier.EvaluateSmtLegality(
            bundleCertificate,
            liveTemplateKey,
            phaseTemplate,
            candidate);

        Assert.False(decision.IsAllowed);
        Assert.Equal(RejectKind.DomainMismatch, decision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, decision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.GuardPlane, decision.AuthoritySource);
        Assert.False(decision.AttemptedReplayCertificateReuse);
    }

    [Fact]
    public void EvaluateSmtLegality_WhenGuardsPassAndTemplateMatches_UsesReplayPhaseCertificateAuthority()
    {
        var verifier = new SafetyVerifier();
        MicroOp candidate = CreateScopedScalarAlu(
            virtualThreadId: 1,
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            destReg: 10,
            src1Reg: 11,
            src2Reg: 12);
        PhaseCertificateTemplateKey4Way liveTemplateKey = CreateLiveTemplateKey(
            ownerContextId: 100,
            ownerDomainTag: 0x2,
            boundaryGuard: BoundaryGuardState.Open(7),
            out BundleResourceCertificate4Way bundleCertificate,
            out _);
        PhaseCertificateTemplate4Way phaseTemplate = new(liveTemplateKey, bundleCertificate);

        LegalityDecision decision = verifier.EvaluateSmtLegality(
            bundleCertificate,
            liveTemplateKey,
            phaseTemplate,
            candidate);

        Assert.True(decision.IsAllowed);
        Assert.Equal(RejectKind.None, decision.RejectKind);
        Assert.Equal(CertificateRejectDetail.None, decision.CertificateDetail);
        Assert.Equal(LegalityAuthoritySource.ReplayPhaseCertificate, decision.AuthoritySource);
        Assert.True(decision.AttemptedReplayCertificateReuse);
        Assert.True(decision.ReusedReplayCertificate);
    }

    private static PhaseCertificateTemplateKey4Way CreateLiveTemplateKey(
        int ownerContextId,
        ulong ownerDomainTag,
        BoundaryGuardState boundaryGuard,
        out BundleResourceCertificate4Way bundleCertificate,
        out SmtBundleMetadata4Way bundleMetadata)
    {
        ReplayPhaseContext phase = new(
            isActive: true,
            epochId: 13,
            cachedPc: 0x3300,
            epochLength: 8,
            completedReplays: 3,
            validSlotCount: 1,
            stableDonorMask: 0b0000_0001,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None);
        MicroOp ownerOperation = CreateScopedScalarAlu(
            virtualThreadId: 0,
            ownerContextId,
            ownerDomainTag,
            destReg: 1,
            src1Reg: 2,
            src2Reg: 3);

        bundleCertificate = BundleResourceCertificate4Way.Empty;
        bundleCertificate.AddOperation(ownerOperation);
        bundleMetadata = SmtBundleMetadata4Way.Empty(ownerVirtualThreadId: 0).WithOperation(ownerOperation);

        return new PhaseCertificateTemplateKey4Way(
            phase.Key,
            bundleCertificate.StructuralIdentity,
            bundleMetadata,
            boundaryGuard);
    }

    private static ScalarALUMicroOp CreateScopedScalarAlu(
        int virtualThreadId,
        int ownerContextId,
        ulong ownerDomainTag,
        ushort destReg,
        ushort src1Reg,
        ushort src2Reg)
    {
        ScalarALUMicroOp microOp = MicroOpTestHelper.CreateScalarALU(
            virtualThreadId,
            destReg,
            src1Reg,
            src2Reg);
        microOp.OwnerContextId = ownerContextId;
        microOp.Placement = microOp.Placement with { DomainTag = ownerDomainTag };
        microOp.InitializeMetadata();
        return microOp;
    }
}
