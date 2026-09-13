using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrCOwnerPolicySnapshotTests
{
    [Fact]
    public void ExactAcceptedD2_LoadsOneDeterministicImmutableO1Policy()
    {
        VirtualizationOperationOwnerSnapshotLoadResult first =
            VirtualizationOperationOwnerSnapshotLoader.LoadExactAcceptedPolicy();
        VirtualizationOperationOwnerSnapshotLoadResult second =
            VirtualizationOperationOwnerSnapshotLoader.LoadExactAcceptedPolicy();

        Assert.True(first.IsLoaded);
        Assert.True(second.IsLoaded);
        VirtualizationOperationOwnerSnapshot snapshot = Assert.IsType<VirtualizationOperationOwnerSnapshot>(first.Snapshot);
        Assert.Equal(second.Snapshot!.PolicyDigest, snapshot.PolicyDigest);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedDecisionId, snapshot.DecisionId);
        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.ExpectedSpecDigest, snapshot.SpecDigest);
        Assert.Equal(Phase38VirtualizationDecisionAcceptanceV2.ExpectedAcceptanceDigest, snapshot.AcceptanceDigest);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOwnerId, snapshot.OwnerId);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOperationNamespace, snapshot.OperationNamespace);
        Assert.Equal(VirtualizationDecisionValidatorV2.ExpectedOperationId, snapshot.OperationId);
        Assert.Equal((ushort)1, snapshot.NumericLeaf);
        Assert.Equal(RuntimeCapabilityIds.VmCallProbeNoStateV1Mask, snapshot.CapabilityMask);
        Assert.True(snapshot.RequiresTypedGrant);
        Assert.True(snapshot.RequireNonZeroDomainTag);
        Assert.False(snapshot.RequiresMemoryDomain);
        Assert.False(snapshot.RequiresIoDomain);
        Assert.Equal(VirtualizationSecureDomainPolicyV2.Deny, snapshot.SecureDomainPolicy);
        Assert.False(snapshot.IsCapability);
        Assert.False(snapshot.RuntimeAuthorityGranted);
        Assert.False(snapshot.BackendExecutionAuthorized);
        Assert.False(snapshot.CompletionPublicationAuthorized);
        Assert.False(snapshot.RetirePublicationAuthorized);

        Assert.Empty(typeof(VirtualizationOperationOwnerSnapshot).GetConstructors());
        Assert.DoesNotContain(
            typeof(VirtualizationOperationOwnerSnapshot).GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.SetMethod is not null);
    }

    [Fact]
    public void O1Registry_ResolvesOnlyExactNamespaceAndLeaf()
    {
        Assert.True(Phase38VirtualizationOperationOwnerSnapshotRegistry.TryResolve(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            1,
            out VirtualizationOperationOwnerSnapshot exact));
        Assert.False(Phase38VirtualizationOperationOwnerSnapshotRegistry.TryResolve(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            0,
            out _));
        Assert.False(Phase38VirtualizationOperationOwnerSnapshotRegistry.TryResolve(
            VirtualizationDecisionValidatorV2.ExpectedOperationNamespace,
            2,
            out _));
        Assert.False(Phase38VirtualizationOperationOwnerSnapshotRegistry.TryResolve(
            "HybridCPU.VMFUNC.FrozenAbi.v1",
            1,
            out _));
        Assert.Equal((ushort)1, exact.NumericLeaf);
    }

    [Fact]
    public void O1Loader_DeniesWrongDecisionDigestOwnerPolicyLeafAbiAndAllocation()
    {
        AcceptedVirtualizationDecision accepted =
            Phase38AcceptedVirtualizationDecisionRegistry.ExactEntry.Policy;
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase38VirtualizationDecisionAcceptanceV2.Record;
        HypercallRuntimeOwnerAllocation allocation = HypercallRuntimeOwnerRegistry.Phase38ProbeOwner;

        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.DecisionMismatch,
            accepted with { DecisionId = "wrong" }, spec, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.DigestMismatch,
            accepted with { SpecDigest = new string('a', 64) }, spec, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.OwnerMismatch,
            accepted with { OwnerId = 0 }, spec, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.PolicyVersionMismatch,
            accepted with { OwnerPolicyVersion = 2 }, spec, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.NamespaceOrLeafMismatch,
            accepted with { NumericLeaf = 2 }, spec, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.AbiMismatch,
            accepted, spec with { OperandAbiVersion = 2 }, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.PolicyMismatch,
            accepted, spec with { RequiresMemoryDomain = true }, acceptance, allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.AllocationMismatch,
            accepted, spec, acceptance, allocation with { OwnerRole = "CompatibilityFrontend" });
    }

    [Fact]
    public void O1Loader_DeniesDraftRevokedSupersededAndClonedSources()
    {
        AcceptedVirtualizationDecision accepted =
            Phase38AcceptedVirtualizationDecisionRegistry.ExactEntry.Policy;
        VirtualizationDecisionSpecV2 spec = Phase38VirtualizationDecisionSpecV2.Instance;
        VirtualizationDecisionAcceptanceRecordV2 acceptance =
            Phase38VirtualizationDecisionAcceptanceV2.Record;
        HypercallRuntimeOwnerAllocation allocation = HypercallRuntimeOwnerRegistry.Phase38ProbeOwner;

        foreach (VirtualizationDecisionAcceptanceStateV2 state in new[]
                 {
                     VirtualizationDecisionAcceptanceStateV2.Draft,
                     VirtualizationDecisionAcceptanceStateV2.Revoked,
                     VirtualizationDecisionAcceptanceStateV2.Superseded,
                 })
        {
            AssertDecision(
                VirtualizationOperationOwnerSnapshotLoadDecision.AcceptanceNotCurrent,
                accepted,
                spec,
                acceptance with { AcceptanceState = state },
                allocation);
        }

        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.AcceptanceNotCurrent,
            accepted,
            spec,
            acceptance with
            {
                SupersedesDecisionId = "another",
                SupersedesAcceptanceDigest = new string('a', 64),
            },
            allocation);
        AssertDecision(
            VirtualizationOperationOwnerSnapshotLoadDecision.NotExactMachineAcceptedSource,
            accepted with { },
            spec with { },
            acceptance with { },
            allocation with { });
    }

    private static void AssertDecision(
        VirtualizationOperationOwnerSnapshotLoadDecision expected,
        AcceptedVirtualizationDecision accepted,
        VirtualizationDecisionSpecV2 spec,
        VirtualizationDecisionAcceptanceRecordV2 acceptance,
        HypercallRuntimeOwnerAllocation allocation)
    {
        VirtualizationOperationOwnerSnapshotLoadResult result =
            VirtualizationOperationOwnerSnapshotLoader.TryLoad(
                accepted,
                spec,
                acceptance,
                allocation);

        Assert.False(result.IsLoaded);
        Assert.Null(result.Snapshot);
        Assert.Equal(expected, result.Decision);
    }
}
