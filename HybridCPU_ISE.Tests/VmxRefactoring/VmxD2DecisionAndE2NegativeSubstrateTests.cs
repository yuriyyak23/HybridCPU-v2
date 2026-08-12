using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxD2DecisionAndE2NegativeSubstrateTests
{
    [Fact]
    public void D2Validator_DefaultManifestFailsClosedWithoutThrowing()
    {
        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                default,
                default);

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedSchemaVersion,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void D2Validator_DeniesEveryNonAcceptedDecision(int stateValue)
    {
        var state = (VirtualizationOperationDecisionState)stateValue;
        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(state),
                MissingAttribution());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedDecisionNotAccepted,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void D2Validator_DeniesAcceptedShapeWithoutCodeOwnersAttribution()
    {
        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                MissingAttribution());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedCodeOwnersRule,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
    }

    [Fact]
    public void D2Validator_DeniesMalformedAcceptedCommitShaBeforeAttribution()
    {
        VirtualizationOperationDecisionManifest manifest =
            CreateManifest(VirtualizationOperationDecisionState.Accepted) with
            {
                AcceptedCommitSha = "not-a-sha",
            };

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                manifest,
                FullyAttributed());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedAcceptedCommitSha,
            result.Decision);
    }

    [Fact]
    public void D2Validator_DeniesReviewerMismatchBeforeLeafEvaluation()
    {
        VirtualizationDecisionAttributionEvidence attribution = FullyAttributed() with
        {
            ApprovedReviewers = Array.Empty<string>(),
        };

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                attribution);

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedReviewerMismatch,
            result.Decision);
    }

    [Fact]
    public void LegacyV1Validator_DoesNotConsumeV2CodeOwnersAsImplicitAttribution()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string[] candidatePaths =
        [
            Path.Combine(root, "CODEOWNERS"),
            Path.Combine(root, ".github", "CODEOWNERS"),
            Path.Combine(root, "docs", "CODEOWNERS"),
        ];

        Assert.False(File.Exists(candidatePaths[0]), candidatePaths[0]);
        Assert.True(File.Exists(candidatePaths[1]), candidatePaths[1]);
        Assert.False(File.Exists(candidatePaths[2]), candidatePaths[2]);

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                MissingAttribution());
        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedCodeOwnersRule,
            result.Decision);
    }

    [Fact]
    public void D2Validator_DeniesCompatibilitySelfApprovalBeforeLeafEvaluation()
    {
        VirtualizationDecisionAttributionEvidence attribution = FullyAttributed() with
        {
            CompatibilityFrontendSelfApproved = true,
        };

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                attribution);

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedCompatibilitySelfApproval,
            result.Decision);
    }

    [Fact]
    public void D2Validator_DeniesAbsentExactLeafWithoutInventingAValue()
    {
        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                FullyAttributed());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedExactLeafCardinality,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
    }

    [Fact]
    public void D2Validator_DeniesDuplicateLeafCardinalityWithoutAllocatingAValue()
    {
        VirtualizationOperationDecisionManifest manifest =
            CreateManifest(VirtualizationOperationDecisionState.Accepted) with
            {
                ExactNumericLeaves = new ulong[2],
            };

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                manifest,
                FullyAttributed());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedDuplicateExactLeaf,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
    }

    [Fact]
    public void D2Validator_DeniesIncompleteOwnerMapWithoutSupplyingALeaf()
    {
        VirtualizationOperationDecisionManifest manifest =
            CreateManifest(VirtualizationOperationDecisionState.Accepted) with
            {
                EvidenceClass = string.Empty,
            };

        VirtualizationOperationDecisionValidationResult result =
            VirtualizationOperationDecisionManifestValidator.Validate(
                manifest,
                FullyAttributed());

        Assert.Equal(
            VirtualizationOperationDecisionValidationDecision.DeniedOwnerMapIncomplete,
            result.Decision);
        Assert.False(result.IsStructurallyValidGovernanceEvidence);
    }

    [Fact]
    public void DisabledRepositoryOwnerReviewWorkflow_CannotAppointOrAcceptAnOwner()
    {
        VirtualizationRepositoryOwnerReviewResult noAttribution =
            DisabledVirtualizationRepositoryOwnerReviewWorkflow.Instance.Evaluate(
                new("UNASSIGNED", MissingAttribution()));
        Assert.Equal(
            VirtualizationRepositoryOwnerReviewDecision.DeniedCodeOwnersAttributionAbsent,
            noAttribution.Decision);

        VirtualizationRepositoryOwnerReviewResult disabled =
            DisabledVirtualizationRepositoryOwnerReviewWorkflow.Instance.Evaluate(
                new("UNASSIGNED", FullyAttributed()));
        Assert.Equal(
            VirtualizationRepositoryOwnerReviewDecision.DeniedWorkflowDisabled,
            disabled.Decision);
        Assert.False(disabled.OwnerAppointmentAuthorized);
        Assert.False(disabled.DecisionAcceptanceAuthorized);
        Assert.False(disabled.BackendExecutionAuthorized);

        MethodInfo[] methods = typeof(DisabledVirtualizationRepositoryOwnerReviewWorkflow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Approve", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Accept", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Appoint", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DisabledOwnerInterface_HasNoExecutionMethodAndAlwaysDenies()
    {
        MethodInfo[] methods = typeof(INeutralVirtualizationOperationOwner).GetMethods();
        Assert.Single(methods);
        Assert.Equal("Resolve", methods[0].Name);
        Assert.DoesNotContain(methods, method =>
            method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase));

        NeutralVirtualizationOperationOwnerResult result =
            DisabledNeutralVirtualizationOperationOwner.Instance.Resolve(
                new(
                    "UNASSIGNED",
                    new(
                        VirtualizationOperationDecisionValidationDecision.ValidGovernanceArtifactOnly,
                        "shape only")));

        Assert.Equal(
            NeutralVirtualizationOperationOwnerDecision.DeniedOwnerInterfaceDisabled,
            result.Decision);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void E2Certificate_IsOpaqueUnissuedAndEvaluationRemainsDenied()
    {
        Type certificate = typeof(SafetyVerifier.VirtualizationOperationAdmissionCertificate);
        Assert.Empty(certificate.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Single(certificate.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));

        var verifier = new SafetyVerifier();
        VirtualizationOperationAdmissionResult result =
            verifier.EvaluateVirtualizationOperationAdmission(new(
                CreateManifest(VirtualizationOperationDecisionState.Accepted),
                MissingAttribution(),
                CanonicalRuntimeLeafCaptured: false,
                CapabilityGrantIdentityPresent: false,
                EvidencePolicyIdentityPresent: false,
                AddressSpaceIdentityPresent: false,
                RestoreGenerationPresent: false));

        Assert.Equal(
            VirtualizationOperationAdmissionDecision.DeniedD2DecisionArtifact,
            result.Decision);
        Assert.Null(result.Certificate);
        Assert.False(result.IsIssued);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void D2JsonSchema_IsSchemaOnlyAndContainsNoLeafOrOwnerAppointment()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(
            root,
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualizationActivationPlan",
            "evidence",
            "virtualization-operation-decision-manifest.schema.json");
        string text = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(text);

        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            document.RootElement.GetProperty("$schema").GetString());
        Assert.Contains("accepted", text);
        Assert.Contains("exact_numeric_leaves", text);
        Assert.Contains("minItems", text);
        Assert.Contains("maxItems", text);
        Assert.DoesNotContain("DomainHypercallRuntimeOwner", text);
        Assert.DoesNotContain("HCPU_HV_PROBE_V1", text);
        Assert.DoesNotContain("0x48594350_00000001", text);
    }

    [Fact]
    public void D2AndE2Sources_DoNotCreateRuntimeOrPublicationShortcuts()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/Hypercalls/VirtualizationOperationDecisionManifest.cs",
            "CloseToHSL/Core/Runtime/Events/Hypercalls/DisabledNeutralVirtualizationOperationOwner.cs",
            "CloseToHSL/Core/Runtime/Events/Hypercalls/DisabledVirtualizationRepositoryOwnerReviewWorkflow.cs",
            "CloseToHSL/Core/Pipeline/Safety/SafetyVerifier.VirtualizationOperationAdmission.cs");

        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("HypercallBackendAdmissionDecision.Allowed", source);
        Assert.DoesNotContain("CompletionRecord", source);
        Assert.DoesNotContain("VmxRetireEffect", source);
        Assert.DoesNotContain("ExecutionDispatcher", source);
        Assert.DoesNotContain("DomainHypercallRuntimeOwner", source);
        Assert.DoesNotContain("HCPU_HV_PROBE_V1", source);
        Assert.DoesNotContain("0x48594350_00000001", source);
        Assert.DoesNotContain("OwnerAppointmentAuthorized: true", source);
        Assert.DoesNotContain("DecisionAcceptanceAuthorized: true", source);

        string frontend = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs");
        Assert.Contains("HypercallBackendAdmissionRequest.MissingNeutralOwner", frontend);
        Assert.Contains("TrapCompletionRouteRequest.ProjectionOnlyDenied", frontend);
        Assert.DoesNotContain("VirtualizationOperationAdmissionCertificate", frontend);
    }

    private static VirtualizationOperationDecisionManifest CreateManifest(
        VirtualizationOperationDecisionState state) =>
        new(
            SchemaVersion: VirtualizationOperationDecisionManifestValidator.CurrentSchemaVersion,
            DecisionId: "UNASSIGNED",
            State: state,
            OperationName: "UNASSIGNED",
            DecisionOwner: "UNASSIGNED",
            OwnerSource: NeutralHypercallBackendOwnerSource.NeutralRuntimeOwner,
            AcceptedCommitSha: new string('0', 40),
            RequiredReviewers: ["UNASSIGNED"],
            ExactNumericLeaves: Array.Empty<ulong>(),
            ValueSource: "UNASSIGNED",
            CapabilityPolicy: "UNASSIGNED",
            EvidenceClass: "UNASSIGNED",
            MigrationClass: "UNASSIGNED",
            DenialReason: "deny while D2 is absent");

    private static VirtualizationDecisionAttributionEvidence MissingAttribution() =>
        new(
            DecisionOwner: "UNASSIGNED",
            AcceptedCommitSha: new string('0', 40),
            CodeOwnersRulePresent: false,
            CodeOwnersRuleMatched: false,
            RequiredReviewersApproved: false,
            ApprovedReviewers: Array.Empty<string>(),
            CompatibilityFrontendSelfApproved: false);

    private static VirtualizationDecisionAttributionEvidence FullyAttributed() =>
        MissingAttribution() with
        {
            CodeOwnersRulePresent = true,
            CodeOwnersRuleMatched = true,
            RequiredReviewersApproved = true,
            ApprovedReviewers = ["UNASSIGNED"],
        };
}
