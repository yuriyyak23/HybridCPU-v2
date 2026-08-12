using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase45MemoryOwnedVmReadScalarDeliveryE0D2SpecTests
{
    [Fact]
    public void E0_ClosesFreshnessGapWithoutOpeningProductionComposition()
    {
        Assert.Equal(Enumerable.Range(1, 16).Select(value => (byte)value),
            Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.Findings.Select(item => item.Number));
        Assert.True(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.GuestCr3, (ushort)VmcsField.EptPointer,
             (ushort)VmcsField.Vpid, (ushort)VmcsField.Cr3TargetCount]));
        Assert.Contains(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.Findings,
            item => item.Name == "GenerationGap" && item.ExactContract.Contains("CallerProvided"));
        Assert.Contains(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.Findings,
            item => item.Name == "GenerationClosure" && item.ExactContract.Contains("MemoryDomainRuntime"));
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.SourceValueAvailable);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ResultReceiptIssued);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void Spec_IsExactMemoryOwnedGovernanceOnlyProfile()
    {
        VirtualizationDecisionSpecV2 spec = Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance;
        Assert.Equal(Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.ExpectedSpecDigest, spec.SpecDigest);
        Assert.Equal(VirtualizationDecisionAuthorityPlaneV2.MemoryAddressSpaceReadProjection, spec.AuthorityPlane);
        Assert.Equal(VirtualizationCapabilityRequirementV2.None, spec.CapabilityRequirement);
        Assert.Equal(VirtualizationOperationMigrationPolicyV2.DrainOnly, spec.OperationMigrationPolicy);
        Assert.Equal(4, spec.OwnerMap.Length);
        Assert.All(spec.OwnerMap, entry =>
        {
            Assert.Equal("MemoryDomainDescriptor", entry.Owner);
            Assert.StartsWith("MemoryDomainReadOnlyTranslationView.", entry.ValueSource);
            Assert.Equal("None", entry.CapabilityPolicy);
            Assert.Equal("DrainOnly", entry.MigrationClass);
        });
        Assert.True(MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(spec).IsExactPolicyShape);
        Assert.False(Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.ProductionCompositionAuthorized);
    }

    [Fact]
    public void Validator_DeniesBroadFieldsCapabilityFallbackAndCompletion()
    {
        VirtualizationDecisionSpecV2 valid = Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance;
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            { ExactFieldIds = [.. valid.ExactFieldIds!.Value, (ushort)VmcsField.HostCr3] }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            { CapabilityRequirement = VirtualizationCapabilityRequirementV2.DomainGrantedVmCallProbeNoStateV1,
              CapabilityMask = 1UL << 41 }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedOwnerMap,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            { OwnerMap = [valid.OwnerMap[0] with { ValueSource = "VmcsV2Descriptor.BackingStore" },
                valid.OwnerMap[1], valid.OwnerMap[2], valid.OwnerMap[3]] }).Decision);
        Assert.Equal(VmReadScalarDeliveryDecisionValidationDecisionV2.DeniedProfile,
            MemoryOwnedVmReadScalarDeliveryDecisionValidatorV2.ValidateSpecShape(valid with
            { CompletionPolicy = VirtualizationCompletionPolicyV2.AtomicE3ToCompletionRecordAndE5 }).Decision);
    }

    [Fact]
    public void EarlierDecisionsRemainDistinctAndByteStable()
    {
        Assert.Equal("ccda8698dbeb3f6eef1b4f13e22a3fb7607e939f493138fb7e3373674e234309",
            Phase41VmReadScalarDeliveryDecisionSpecV2.Instance.SpecDigest);
        Assert.Equal("e67ff2620ff6a1fd193b8303c5b6ae1d532e51241e6b3e405f3c6cedefe2d754",
            Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance.SpecDigest);
        Assert.NotEqual(Phase43GuestPcSpFlagsVmReadScalarDeliveryDecisionSpecV2.Instance.DecisionId,
            Phase45MemoryOwnedVmReadScalarDeliveryDecisionSpecV2.Instance.DecisionId);
    }
}
