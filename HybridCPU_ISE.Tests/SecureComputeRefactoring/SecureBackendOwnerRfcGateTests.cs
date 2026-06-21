using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureBackendOwnerRfcGateTests
{
    private static ulong ProductionHypercallId =>
        SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf.Value;

    [Fact]
    public void SecureBackendOwnerGate_MissingNeutralOwnerIsDenied()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: SecureBackendOwnerDescriptor.None));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedMissingNeutralOwner,
            result.Decision);
        Assert.False(result.ProofChainAccepted);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Theory]
    [InlineData(SecureBackendOwnerSource.CompatibilityProjection)]
    [InlineData(SecureBackendOwnerSource.VmxFrontend)]
    [InlineData(SecureBackendOwnerSource.VmcsProjection)]
    [InlineData(SecureBackendOwnerSource.VmxCapsProjection)]
    [InlineData(SecureBackendOwnerSource.ShadowVmcsProjection)]
    public void SecureBackendOwnerGate_CompatibilityAndVmxSourcesAreDenied(
        SecureBackendOwnerSource source)
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: Owner(source: source)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedNonNeutralAuthoritySource,
            result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Theory]
    [InlineData(SecureBackendRfcAdrState.Missing)]
    [InlineData(SecureBackendRfcAdrState.Draft)]
    public void SecureBackendOwnerGate_MissingApprovedRfcAdrIsDenied(
        SecureBackendRfcAdrState rfcAdrState)
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(rfcAdrState: rfcAdrState));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedMissingRfcAdrApproval,
            result.Decision);
    }

    [Fact]
    public void SecureBackendOwnerGate_IncompleteProofChainIsDenied()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: Owner(grantProofValidated: false)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedMissingProofChain,
            result.Decision);
    }

    [Fact]
    public void SecureBackendOwnerGate_StaleEpochIsDenied()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: Owner(epoch: 6)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedStaleEpoch,
            result.Decision);
    }

    [Fact]
    public void SecureBackendOwnerGate_MissingNegativeTestsIsDenied()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: Owner(negativeTestsPresent: false)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedMissingNegativeTests,
            result.Decision);
    }

    [Fact]
    public void SecureBackendOwnerGate_ValidProofChainIsPolicyEvidenceOnly()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(CreateRequest());

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
            result.Decision);
        Assert.True(result.ProofChainAccepted);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Theory]
    [InlineData(SecureBackendOwnerSource.NeutralRuntimeService)]
    [InlineData(SecureBackendOwnerSource.NeutralDeviceModel)]
    [InlineData(SecureBackendOwnerSource.NeutralMigrationService)]
    public void SecureBackendOwnerGate_AllNeutralOwnerSourcesRemainProofOnlyNoExecution(
        SecureBackendOwnerSource source)
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(owner: Owner(source: source)));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
            result.Decision);
        Assert.True(result.ProofChainAccepted);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureBackendOwnerGate_BackendExecutionRequestStillFailsClosed()
    {
        SecureBackendOwnerAdmissionResult result =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                CreateRequest(requestsBackendExecution: true));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.DeniedBackendExecutionClosed,
            result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureBackendOwnerGate_ResultShapeHasNoPublicationRetireOrNestedAuthorityFlags()
    {
        string[] resultProperties = typeof(SecureBackendOwnerAdmissionResult)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(nameof(SecureBackendOwnerAdmissionResult.ProofChainAccepted), resultProperties);
        Assert.Contains(nameof(SecureBackendOwnerAdmissionResult.BackendExecutionAuthorized), resultProperties);
        Assert.DoesNotContain("CompletionPublicationAuthorized", resultProperties);
        Assert.DoesNotContain("RetirePublicationAuthorized", resultProperties);
        Assert.DoesNotContain("MutableNestedStateAuthorized", resultProperties);
        Assert.DoesNotContain("NestedBackendExecutionAuthorized", resultProperties);
    }

    [Fact]
    public void SecureBackendOwnerGate_DoesNotOpenSecureHypercallBackendExecution()
    {
        SecureIoHypercallAdmissionResult result =
            SecureIoHypercallAdmissionPolicy.Default.AdmitHypercall(
                CreateHypercallPolicy(allowBackendExecution: true),
                hypercallId: ProductionHypercallId,
                CreateCapabilities(HypercallGrantMask),
                EvidencePolicy(),
                CreateIoPolicy(neutralOwner: true),
                new SecureRevocationEpoch(7),
                RetireFence(),
                neutralBackendOwnerMaterialized: true,
                domainValidated: true);

        Assert.Equal(
            SecureIoHypercallAdmissionDecision.DeniedBackendSuccessClosed,
            result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
    }

    [Fact]
    public void SecureBackendOwnerGatePlans_RecordClosedPolicyAdmissionOnlyStatus()
    {
        string plan11 = ReadPlan("11-test-and-conformance-master-plan.md");
        string plan12 = ReadPlan("12-phasing-and-pr-breakdown.md");

        Assert.Contains("Post-Phase10 secure backend owner/RFC proof gate closed on 2026-05-31", plan11);
        Assert.Contains("Future coverage pool transferred to `Plan2/14-securecompute-open-decision-backlog.md`", plan11);
        Assert.Contains("Post-Phase10 secure backend owner/RFC proof gate closed on 2026-05-31", plan12);
        Assert.Contains("AllowedProofOnlyNoExecution", plan12);
        Assert.Contains("no `BackendExecutionAuthorized: true`", plan12);
    }

    [Fact]
    public void SecureBackendOwnerGateSources_DoNotUseVmxVmcsOrVmxCapsAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Backend/SecureBackendOwnerDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureBackendOwnerAdmissionPolicy.cs");

        Assert.Contains("DeniedBackendExecutionClosed", source);
        Assert.Contains("AllowedProofOnlyNoExecution", source);
        Assert.DoesNotContain("VmxCaps.Secure", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("AllowedInternalExecutionNoPublication", source);
        Assert.DoesNotContain("AllowedCompletionRecordNoPublication", source);
        Assert.DoesNotContain("SecureCompletionRecord", source);
        Assert.DoesNotContain("NoSideEffectProbe", source);
        Assert.DoesNotContain("NestedBackendExecution", source);
        Assert.DoesNotContain("MutableNestedStateAuthorized: true", source);
        Assert.DoesNotContain("RuntimeOwnedPublication", source);
        Assert.DoesNotContain("TrapCompletionPublicationFence", source);
    }

    private const ulong HypercallGrantMask = 1UL << 46;

    private static SecureBackendOwnerAdmissionRequest CreateRequest(
        SecureBackendOwnerDescriptor? owner = null,
        SecureBackendRfcAdrState rfcAdrState = SecureBackendRfcAdrState.Approved,
        bool requestsBackendExecution = false) =>
        new(
            owner ?? Owner(),
            rfcAdrState,
            new SecureRevocationEpoch(7),
            requestsBackendExecution);

    private static SecureBackendOwnerDescriptor Owner(
        SecureBackendOwnerSource source = SecureBackendOwnerSource.NeutralRuntimeService,
        ulong epoch = 7,
        bool grantProofValidated = true,
        bool negativeTestsPresent = true) =>
        SecureHypercallBackendOwnerAbiRegistry.CreateOwnerDescriptor(
            source,
            new SecureRevocationEpoch(epoch),
            grantProofValidated,
            evidenceProofValidated: true,
            completionFenceValidated: true,
            retireFenceValidated: true,
            negativeTestsPresent: negativeTestsPresent);

    private static SecureHypercallDescriptor CreateHypercallPolicy(bool allowBackendExecution) =>
        new(
            neutralBackendOwnerRequired: true,
            allowBackendExecution,
            allowedHypercallIds: new[] { ProductionHypercallId },
            requiredGrant: new SecureGrantHandle(
                SecureGrantHandleKind.HypercallPolicy,
                LocalId: HypercallGrantMask,
                ProvenanceHash: 0xA11,
                Epoch: 7),
            arguments: Array.Empty<SecureHypercallArgumentDescriptor>(),
            requireEvidenceApproval: true,
            requireCompletionFence: true,
            requireRetirePublicationRule: true);

    private static SecureIoDomainDescriptor CreateIoPolicy(bool neutralOwner) =>
        new(
            SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
            Array.Empty<SecureSharedBufferDescriptor>(),
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: neutralOwner);

    private static CapabilityDescriptorSet CreateCapabilities(ulong capabilityMask) =>
        new(new CapabilityGrantCollection(new[]
        {
            new CapabilityGrant(
                capabilityMask,
                CapabilityGrantScope.DomainGranted,
                true,
                7,
                CapabilityDelegationPolicy.NonDelegable,
                CapabilityRevocationPolicy.RuntimeRevocable,
                CapabilityMigrationClass.DomainLocal,
                CapabilityEvidenceVisibility.HostOnly,
                CapabilityFrontendProjectionPolicy.NeverProject),
        }));

    private static EvidencePolicyDescriptor EvidencePolicy() =>
        new(
            allowCompatibilityAliases: false,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

    private static SecureCompletionPublicationFence RetireFence() =>
        new(
            SecureCompletionFenceState.RetireAllowed,
            SecureRetirePublicationRule.ExplicitRetireFenceRequired);

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    private static string ReadPlan(string fileName)
    {
        string root = FindRepositoryRoot();
        string legacyPath = Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute",
            "Plan",
            fileName);
        string archivedPath = Path.Combine(
            root,
            "HybridCPU_ISE",
            "docs",
            "securecompute prv",
            "Plan",
            fileName);

        return File.ReadAllText(File.Exists(legacyPath) ? legacyPath : archivedPath);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
