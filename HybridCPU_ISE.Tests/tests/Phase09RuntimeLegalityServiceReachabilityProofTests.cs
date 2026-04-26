using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09RuntimeLegalityServiceReachabilityProofTests
{
    [Fact]
    public void RuntimeLegalityService_ReplayMethodsDelegateToCoordinatorWithoutPhaseKeyDrift()
    {
        var legalityChecker = new StubLegalityChecker();
        var coordinator = new RecordingLegalityCertificateCacheCoordinator
        {
            EvaluateInterCoreLegalityResult = LegalityDecision.Allow(
                LegalityAuthoritySource.StructuralCertificate,
                attemptedReplayCertificateReuse: true),
            EvaluateSmtBoundaryGuardResult = LegalityDecision.Allow(
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateReuse: false),
            EvaluateSmtLegalityResult = LegalityDecision.Reject(
                RejectKind.RareHazard,
                CertificateRejectDetail.RegisterGroupConflict,
                LegalityAuthoritySource.ReplayPhaseCertificate,
                attemptedReplayCertificateReuse: true)
        };
        var runtimeLegalityService = new RuntimeLegalityService(legalityChecker, coordinator);
        ReplayPhaseContext phase = CreateReplayPhaseContext();
        MicroOp[] bundle = CreateInterCoreBundle();
        BundleResourceCertificate interCoreCertificate =
            BundleResourceCertificate.Create(bundle, ownerThreadId: 0, cycleNumber: 1);
        MicroOp interCoreCandidate = MicroOpTestHelper.CreateScalarALU(
            virtualThreadId: 1,
            destReg: 12,
            src1Reg: 13,
            src2Reg: 14);
        SafetyMask128 hardwareMask = new(0x1234UL, 0x5678UL);

        runtimeLegalityService.PrepareInterCore(phase, interCoreCertificate);
        LegalityDecision interCoreDecision = runtimeLegalityService.EvaluateInterCoreLegality(
            phase,
            bundle,
            interCoreCertificate,
            bundleOwnerThreadId: 0,
            requestedDomainTag: 0x55UL,
            interCoreCandidate,
            hardwareMask);
        runtimeLegalityService.RefreshInterCoreAfterMutation(phase, interCoreCertificate);

        BundleResourceCertificate4Way smtCertificate = BundleResourceCertificate4Way.Empty;
        SmtBundleMetadata4Way bundleMetadata = SmtBundleMetadata4Way.Empty(ownerVirtualThreadId: 0);
        BoundaryGuardState boundaryGuard = BoundaryGuardState.Open(serializingEpochId: 4);
        foreach (MicroOp operation in bundle.Where(static op => op != null))
        {
            smtCertificate.AddOperation(operation);
            bundleMetadata = bundleMetadata.WithOperation(operation);
            boundaryGuard = boundaryGuard.WithOperation(operation);
        }

        MicroOp smtCandidate = MicroOpTestHelper.CreateScalarALU(
            virtualThreadId: 1,
            destReg: 18,
            src1Reg: 19,
            src2Reg: 20);

        runtimeLegalityService.PrepareSmt(phase, smtCertificate, bundleMetadata, boundaryGuard);
        LegalityDecision boundaryDecision = runtimeLegalityService.EvaluateSmtBoundaryGuard(
            phase,
            smtCertificate,
            bundleMetadata,
            boundaryGuard);
        LegalityDecision smtDecision = runtimeLegalityService.EvaluateSmtLegality(
            phase,
            smtCertificate,
            bundleMetadata,
            boundaryGuard,
            smtCandidate);
        runtimeLegalityService.RefreshSmtAfterMutation(
            phase,
            smtCertificate,
            bundleMetadata,
            boundaryGuard);
        runtimeLegalityService.InvalidatePhaseMismatch(phase);
        runtimeLegalityService.Invalidate(
            ReplayPhaseInvalidationReason.CertificateMutation,
            invalidateInterCore: false,
            invalidateFourWay: true);

        Assert.Equal(phase.Key, coordinator.PreparedInterCorePhaseKey);
        Assert.Equal(interCoreCertificate.StructuralIdentity, coordinator.PreparedInterCoreCertificateIdentity);
        Assert.Equal(phase.Key, coordinator.EvaluatedInterCorePhaseKey);
        Assert.Same(legalityChecker, coordinator.InterCoreLegalityChecker);
        Assert.Same(bundle, coordinator.InterCoreBundle);
        Assert.Equal(interCoreCertificate.StructuralIdentity, coordinator.EvaluatedInterCoreCertificateIdentity);
        Assert.Equal(0, coordinator.InterCoreBundleOwnerThreadId);
        Assert.Equal(0x55UL, coordinator.RequestedDomainTag);
        Assert.Same(interCoreCandidate, coordinator.InterCoreCandidate);
        Assert.Equal(hardwareMask, coordinator.InterCoreHardwareMask);
        Assert.Equal(coordinator.EvaluateInterCoreLegalityResult, interCoreDecision);
        Assert.Equal(phase.Key, coordinator.RefreshedInterCorePhaseKey);
        Assert.Equal(interCoreCertificate.StructuralIdentity, coordinator.RefreshedInterCoreCertificateIdentity);

        Assert.Equal(phase.Key, coordinator.PreparedSmtPhaseKey);
        Assert.Equal(smtCertificate.StructuralIdentity, coordinator.PreparedSmtCertificateIdentity);
        Assert.Equal(bundleMetadata, coordinator.PreparedSmtBundleMetadata);
        Assert.Equal(boundaryGuard, coordinator.PreparedSmtBoundaryGuard);
        Assert.Equal(phase.Key, coordinator.EvaluatedSmtBoundaryPhaseKey);
        Assert.Same(legalityChecker, coordinator.SmtBoundaryLegalityChecker);
        Assert.Equal(smtCertificate.StructuralIdentity, coordinator.EvaluatedSmtBoundaryCertificateIdentity);
        Assert.Equal(bundleMetadata, coordinator.EvaluatedSmtBoundaryBundleMetadata);
        Assert.Equal(boundaryGuard, coordinator.EvaluatedSmtBoundaryGuard);
        Assert.Equal(coordinator.EvaluateSmtBoundaryGuardResult, boundaryDecision);
        Assert.Equal(phase.Key, coordinator.EvaluatedSmtLegalityPhaseKey);
        Assert.Same(legalityChecker, coordinator.SmtLegalityChecker);
        Assert.Equal(smtCertificate.StructuralIdentity, coordinator.EvaluatedSmtLegalityCertificateIdentity);
        Assert.Equal(bundleMetadata, coordinator.EvaluatedSmtLegalityBundleMetadata);
        Assert.Equal(boundaryGuard, coordinator.EvaluatedSmtLegalityBoundaryGuard);
        Assert.Same(smtCandidate, coordinator.SmtCandidate);
        Assert.Equal(coordinator.EvaluateSmtLegalityResult, smtDecision);
        Assert.Equal(phase.Key, coordinator.RefreshedSmtPhaseKey);
        Assert.Equal(smtCertificate.StructuralIdentity, coordinator.RefreshedSmtCertificateIdentity);
        Assert.Equal(bundleMetadata, coordinator.RefreshedSmtBundleMetadata);
        Assert.Equal(boundaryGuard, coordinator.RefreshedSmtBoundaryGuard);
        Assert.Equal(phase, coordinator.InvalidatedPhaseMismatch);
        Assert.Equal(ReplayPhaseInvalidationReason.CertificateMutation, coordinator.LastInvalidationReason);
        Assert.False(coordinator.LastInvalidateInterCore);
        Assert.True(coordinator.LastInvalidateFourWay);
    }

    [Fact]
    public void LiveProductionReplayCertificateWiring_RemainsPinnedToSubstrateFactoriesAndRuntimeLegalityService()
    {
        string repoRoot = FindRepositoryRoot();
        string productionRoot = Path.Combine(repoRoot, "HybridCPU_ISE");
        string[] productionFiles = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        Dictionary<string, string> fileTexts = productionFiles.ToDictionary(
            static path => path,
            static path => File.ReadAllText(path));

        AssertPinnedToReplayPhaseSubstrate(
            FilesContaining(fileTexts, "new InterCoreLegalityCertificateCache()"));
        AssertPinnedToReplayPhaseSubstrate(
            FilesContaining(fileTexts, "new SmtLegalityCertificateCache4Way()"));
        AssertPinnedToReplayPhaseSubstrate(
            FilesContaining(fileTexts, "new LegalityCertificateCacheCoordinator("));
        AssertPinnedToReplayPhaseSubstrate(
            FilesContaining(fileTexts, "new RuntimeLegalityService("));

        KeyValuePair<string, string>[] schedulerFiles = fileTexts
            .Where(static entry =>
                Path.GetFileName(entry.Key).StartsWith("MicroOpScheduler", StringComparison.Ordinal) &&
                entry.Key.Contains($"{Path.DirectorySeparatorChar}Scheduling{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(schedulerFiles);
        Assert.All(
            schedulerFiles,
            static entry =>
            {
                Assert.DoesNotContain("InterCoreLegalityCertificateCache", entry.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("SmtLegalityCertificateCache4Way", entry.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("LegalityCertificateCacheCoordinator", entry.Value, StringComparison.Ordinal);
                Assert.DoesNotContain("ILegalityCertificateCacheCoordinator", entry.Value, StringComparison.Ordinal);
            });

        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("RuntimeLegalityServiceFactory.CreateDefault(this)", StringComparison.Ordinal));
        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("_runtimeLegalityService.PrepareInterCore(", StringComparison.Ordinal));
        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("_runtimeLegalityService.RefreshInterCoreAfterMutation(", StringComparison.Ordinal));
        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("_runtimeLegalityService.PrepareSmt(", StringComparison.Ordinal));
        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("_runtimeLegalityService.RefreshSmtAfterMutation(", StringComparison.Ordinal));
        Assert.Contains(
            schedulerFiles,
            static entry => entry.Value.Contains("_runtimeLegalityService.InvalidatePhaseMismatch(", StringComparison.Ordinal));
    }

    private static ReplayPhaseContext CreateReplayPhaseContext()
    {
        return new ReplayPhaseContext(
            isActive: true,
            epochId: 11,
            cachedPc: 0x2200,
            epochLength: 8,
            completedReplays: 2,
            validSlotCount: 3,
            stableDonorMask: 0b0000_0111,
            lastInvalidationReason: ReplayPhaseInvalidationReason.None);
    }

    private static MicroOp[] CreateInterCoreBundle()
    {
        return
        [
            MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3),
            MicroOpTestHelper.CreateScalarALU(0, destReg: 4, src1Reg: 5, src2Reg: 6),
            MicroOpTestHelper.CreateScalarALU(0, destReg: 7, src1Reg: 8, src2Reg: 9),
            null!,
            null!,
            null!,
            null!,
            null!
        ];
    }

    private static void AssertPinnedToReplayPhaseSubstrate(IReadOnlyCollection<string> files)
    {
        Assert.NotEmpty(files);
        Assert.All(
            files,
            static file =>
                Assert.Contains(
                    "ReplayPhaseSubstrate.",
                    Path.GetFileName(file),
                    StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<string> FilesContaining(
        IReadOnlyDictionary<string, string> fileTexts,
        string literal)
    {
        return fileTexts
            .Where(entry => entry.Value.Contains(literal, StringComparison.Ordinal))
            .Select(static entry => entry.Key)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")) &&
                Directory.Exists(Path.Combine(current.FullName, "Documentation")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private sealed class StubLegalityChecker : ILegalityChecker
    {
        public bool IsKernelDomainIsolationEnabled => true;

        public InterCoreDomainGuardDecision EvaluateInterCoreDomainGuard(
            MicroOp candidate,
            ulong requestedDomainTag)
        {
            return default;
        }

        public TypedSlotRejectClassification ClassifyReject(
            TypedSlotRejectReason admissionReject,
            CertificateRejectDetail certDetail,
            SlotClass candidateClass,
            SlotPinningKind pinningKind)
        {
            return default;
        }

        public LegalityDecision EvaluateInterCoreLegality(
            IReadOnlyList<MicroOp?> bundle,
            BundleResourceCertificate bundleCertificate,
            PhaseCertificateTemplateKey liveTemplateKey,
            PhaseCertificateTemplate phaseTemplate,
            int bundleOwnerThreadId,
            ulong requestedDomainTag,
            MicroOp candidate,
            SafetyMask128 globalHardwareMask = default)
        {
            return LegalityDecision.Allow(
                LegalityAuthoritySource.StructuralCertificate,
                attemptedReplayCertificateReuse: false);
        }

        public LegalityDecision EvaluateSmtBoundaryGuard(PhaseCertificateTemplateKey4Way liveTemplateKey)
        {
            return LegalityDecision.Allow(
                LegalityAuthoritySource.GuardPlane,
                attemptedReplayCertificateReuse: false);
        }

        public LegalityDecision EvaluateSmtLegality(
            BundleResourceCertificate4Way bundleCertificate,
            PhaseCertificateTemplateKey4Way liveTemplateKey,
            PhaseCertificateTemplate4Way phaseTemplate,
            MicroOp candidate)
        {
            return LegalityDecision.Allow(
                LegalityAuthoritySource.StructuralCertificate,
                attemptedReplayCertificateReuse: false);
        }
    }

    private sealed class RecordingLegalityCertificateCacheCoordinator : ILegalityCertificateCacheCoordinator
    {
        public ReplayPhaseKey PreparedInterCorePhaseKey { get; private set; }

        public BundleResourceCertificateIdentity PreparedInterCoreCertificateIdentity { get; private set; }

        public ILegalityChecker? InterCoreLegalityChecker { get; private set; }

        public ReplayPhaseKey EvaluatedInterCorePhaseKey { get; private set; }

        public IReadOnlyList<MicroOp?>? InterCoreBundle { get; private set; }

        public BundleResourceCertificateIdentity EvaluatedInterCoreCertificateIdentity { get; private set; }

        public int InterCoreBundleOwnerThreadId { get; private set; }

        public ulong RequestedDomainTag { get; private set; }

        public MicroOp? InterCoreCandidate { get; private set; }

        public SafetyMask128 InterCoreHardwareMask { get; private set; }

        public LegalityDecision EvaluateInterCoreLegalityResult { get; init; }

        public ReplayPhaseKey RefreshedInterCorePhaseKey { get; private set; }

        public BundleResourceCertificateIdentity RefreshedInterCoreCertificateIdentity { get; private set; }

        public ReplayPhaseKey PreparedSmtPhaseKey { get; private set; }

        public BundleResourceCertificateIdentity4Way PreparedSmtCertificateIdentity { get; private set; }

        public SmtBundleMetadata4Way PreparedSmtBundleMetadata { get; private set; }

        public BoundaryGuardState PreparedSmtBoundaryGuard { get; private set; }

        public ILegalityChecker? SmtBoundaryLegalityChecker { get; private set; }

        public ReplayPhaseKey EvaluatedSmtBoundaryPhaseKey { get; private set; }

        public BundleResourceCertificateIdentity4Way EvaluatedSmtBoundaryCertificateIdentity { get; private set; }

        public SmtBundleMetadata4Way EvaluatedSmtBoundaryBundleMetadata { get; private set; }

        public BoundaryGuardState EvaluatedSmtBoundaryGuard { get; private set; }

        public LegalityDecision EvaluateSmtBoundaryGuardResult { get; init; }

        public ILegalityChecker? SmtLegalityChecker { get; private set; }

        public ReplayPhaseKey EvaluatedSmtLegalityPhaseKey { get; private set; }

        public BundleResourceCertificateIdentity4Way EvaluatedSmtLegalityCertificateIdentity { get; private set; }

        public SmtBundleMetadata4Way EvaluatedSmtLegalityBundleMetadata { get; private set; }

        public BoundaryGuardState EvaluatedSmtLegalityBoundaryGuard { get; private set; }

        public MicroOp? SmtCandidate { get; private set; }

        public LegalityDecision EvaluateSmtLegalityResult { get; init; }

        public ReplayPhaseKey RefreshedSmtPhaseKey { get; private set; }

        public BundleResourceCertificateIdentity4Way RefreshedSmtCertificateIdentity { get; private set; }

        public SmtBundleMetadata4Way RefreshedSmtBundleMetadata { get; private set; }

        public BoundaryGuardState RefreshedSmtBoundaryGuard { get; private set; }

        public ReplayPhaseContext InvalidatedPhaseMismatch { get; private set; }

        public ReplayPhaseInvalidationReason LastInvalidationReason { get; private set; }

        public bool LastInvalidateInterCore { get; private set; }

        public bool LastInvalidateFourWay { get; private set; }

        public void PrepareInterCore(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate)
        {
            PreparedInterCorePhaseKey = phaseKey;
            PreparedInterCoreCertificateIdentity = certificate.StructuralIdentity;
        }

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
            InterCoreLegalityChecker = legalityChecker;
            EvaluatedInterCorePhaseKey = phaseKey;
            InterCoreBundle = bundle;
            EvaluatedInterCoreCertificateIdentity = bundleCertificate.StructuralIdentity;
            InterCoreBundleOwnerThreadId = bundleOwnerThreadId;
            RequestedDomainTag = requestedDomainTag;
            InterCoreCandidate = candidate;
            InterCoreHardwareMask = globalHardwareMask;
            return EvaluateInterCoreLegalityResult;
        }

        public void RefreshInterCoreAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate certificate)
        {
            RefreshedInterCorePhaseKey = phaseKey;
            RefreshedInterCoreCertificateIdentity = certificate.StructuralIdentity;
        }

        public void PrepareSmt(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            PreparedSmtPhaseKey = phaseKey;
            PreparedSmtCertificateIdentity = certificate.StructuralIdentity;
            PreparedSmtBundleMetadata = bundleMetadata;
            PreparedSmtBoundaryGuard = boundaryGuard;
        }

        public LegalityDecision EvaluateSmtBoundaryGuard(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            SmtBoundaryLegalityChecker = legalityChecker;
            EvaluatedSmtBoundaryPhaseKey = phaseKey;
            EvaluatedSmtBoundaryCertificateIdentity = bundleCertificate.StructuralIdentity;
            EvaluatedSmtBoundaryBundleMetadata = bundleMetadata;
            EvaluatedSmtBoundaryGuard = boundaryGuard;
            return EvaluateSmtBoundaryGuardResult;
        }

        public LegalityDecision EvaluateSmtLegality(
            ILegalityChecker legalityChecker,
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way bundleCertificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard,
            MicroOp candidate)
        {
            SmtLegalityChecker = legalityChecker;
            EvaluatedSmtLegalityPhaseKey = phaseKey;
            EvaluatedSmtLegalityCertificateIdentity = bundleCertificate.StructuralIdentity;
            EvaluatedSmtLegalityBundleMetadata = bundleMetadata;
            EvaluatedSmtLegalityBoundaryGuard = boundaryGuard;
            SmtCandidate = candidate;
            return EvaluateSmtLegalityResult;
        }

        public void RefreshSmtAfterMutation(
            ReplayPhaseKey phaseKey,
            BundleResourceCertificate4Way certificate,
            SmtBundleMetadata4Way bundleMetadata,
            BoundaryGuardState boundaryGuard)
        {
            RefreshedSmtPhaseKey = phaseKey;
            RefreshedSmtCertificateIdentity = certificate.StructuralIdentity;
            RefreshedSmtBundleMetadata = bundleMetadata;
            RefreshedSmtBoundaryGuard = boundaryGuard;
        }

        public void InvalidatePhaseMismatch(ReplayPhaseContext phase)
        {
            InvalidatedPhaseMismatch = phase;
        }

        public void Invalidate(
            ReplayPhaseInvalidationReason reason,
            bool invalidateInterCore = true,
            bool invalidateFourWay = true)
        {
            LastInvalidationReason = reason;
            LastInvalidateInterCore = invalidateInterCore;
            LastInvalidateFourWay = invalidateFourWay;
        }
    }
}
