using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxD2V2MaterializedSpecTests
{
    [Fact]
    public void Phase38Spec_IsOneExactCanonicalPolicyArtifactWithoutRuntimeAuthority()
    {
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;

        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, spec.DecisionId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, spec.OperationNamespace);
        Assert.Equal((ushort)1, spec.NumericLeaf);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, spec.OwnerId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedCapabilityMask, spec.CapabilityMask);
        Assert.Equal(10, spec.OwnerMap.Length);
        Assert.True(VirtualizationDecisionCanonicalEncoderV2.IsCanonicalSha256(spec.SpecDigest));
        Assert.Equal(
            "33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8",
            spec.SpecDigest);
        Assert.Equal(
            VirtualizationDecisionCanonicalEncoderV2.ComputeSpecDigest(spec),
            spec.SpecDigest);
        Assert.True(VirtualizationDecisionCanonicalEncoderV2.EncodeSpec(spec)
            .AsSpan()
            .SequenceEqual(Phase38VirtualizationDecisionSpecV2.CanonicalBytes.AsSpan()));

        Assert.False(Phase38VirtualizationDecisionSpecV2.RuntimeAuthorityGranted);
        Assert.False(Phase38VirtualizationDecisionSpecV2.BackendExecutionAuthorized);
        Assert.False(Phase38VirtualizationDecisionSpecV2.CompletionPublicationAuthorized);
        Assert.False(Phase38VirtualizationDecisionSpecV2.RetirePublicationAuthorized);
    }

    [Fact]
    public void CodeOwners_UsesOneAttributableRepositoryPrincipalForEveryPhase38Scope()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string codeOwners = File.ReadAllText(Path.Combine(repositoryRoot, ".github", "CODEOWNERS"));
        string[] scopes =
        [
            "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/",
            "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/",
            "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/",
            "/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/",
            "/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/",
            "/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/",
        ];

        string[] rules = codeOwners.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string scope in scopes)
        {
            Assert.Contains(rules, rule =>
                rule.StartsWith(scope, StringComparison.Ordinal) &&
                rule.EndsWith(" @yaksysdev", StringComparison.Ordinal));
        }

        Assert.DoesNotContain("CompatibilityFrontend", codeOwners);
    }
}
