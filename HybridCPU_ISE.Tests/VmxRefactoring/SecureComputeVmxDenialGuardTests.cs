using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmx;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class SecureComputeVmxDenialGuardTests
{
    [Fact]
    public void VmxCompatibilityBoundary_CannotActivateSecureCompute()
    {
        SecureComputeCompatibilityBoundaryResult result =
            new SecureComputeCompatibilityBoundary().Admit(
                SecureComputeCompatibilityOperation.ActivateSecureCompute,
                projectionOnly: false);

        Assert.False(result.IsAllowed);
        Assert.Equal(
            SecureComputeCompatibilityBoundaryDecision.DeniedVmxActivation,
            result.Decision);
    }

    [Fact]
    public void VmxCapsFence_CannotGrantOrActivateSecureCompute()
    {
        var fence = new SecureComputeVmxCapsProjectionFence();

        Assert.Equal(
            SecureComputeVmxCapsProjectionFenceDecision.DeniedAuthorityGrant,
            fence.Validate(
                attemptsAuthorityGrant: true,
                attemptsActivation: false,
                attemptsWriteMutation: false));
        Assert.Equal(
            SecureComputeVmxCapsProjectionFenceDecision.DeniedActivationBit,
            fence.Validate(
                attemptsAuthorityGrant: false,
                attemptsActivation: true,
                attemptsWriteMutation: false));
        Assert.Equal(
            SecureComputeVmxCapsProjectionFenceDecision.DeniedWriteMutation,
            fence.Validate(
                attemptsAuthorityGrant: false,
                attemptsActivation: false,
                attemptsWriteMutation: true));
    }

    [Fact]
    public void VmxCapsProjection_CannotPublishUnknownSecureComputeBit()
    {
        const ulong hypotheticalSecureComputeBit = 1UL << 63;
        var descriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: hypotheticalSecureComputeBit,
            runtimeEnabledCaps: hypotheticalSecureComputeBit,
            domainGrantedCaps: hypotheticalSecureComputeBit);
        var projection = new VmxCapsProjection();

        Assert.Equal(0UL, projection.Read(descriptorSet));
        Assert.False(
            CapabilityDescriptorSetSchema.VmxCompatibility.ContainsOnlyKnownCompatibilityCaps(
                hypotheticalSecureComputeBit));
        Assert.False(projection.CanPublishCapability(descriptorSet, hypotheticalSecureComputeBit));
    }

    [Fact]
    public void VmxCapsProjection_CannotAdvertiseMeasurementOrAttestationBit()
    {
        const ulong hypotheticalAttestationBit = 1UL << 62;
        var descriptorSet = new CapabilityDescriptorSet(
            globalHardwareCaps: hypotheticalAttestationBit,
            runtimeEnabledCaps: hypotheticalAttestationBit,
            domainGrantedCaps: hypotheticalAttestationBit);
        var projection = new VmxCapsProjection();

        Assert.Equal(0UL, projection.Read(descriptorSet));
        Assert.False(projection.CanPublishCapability(descriptorSet, hypotheticalAttestationBit));
    }

    [Fact]
    public void VmcsFence_CannotStoreSecureState()
    {
        SecureComputeVmcsProjectionFenceDecision result =
            new SecureComputeVmcsProjectionFence().Validate(
                storesSecureState: true,
                usedAsCheckpointAuthority: false);

        Assert.Equal(SecureComputeVmcsProjectionFenceDecision.DeniedSecureStateStore, result);
    }

    [Fact]
    public void AuthorityBoundary_DeniesVmcsStoreAndActivePointerIdentity()
    {
        var contract = new SecureComputeVmxAuthorityBoundaryContract();

        Assert.Equal(
            SecureComputeAuthorityBoundaryViolation.VmcsStateStore,
            contract.Validate(
                vmxActivatesSecureCompute: false,
                vmxCapsGrantsSecureCompute: false,
                vmcsStoresSecureState: true,
                activeVmcsPointerIsDomainIdentity: false));
        Assert.Equal(
            SecureComputeAuthorityBoundaryViolation.ActiveVmcsPointerIdentity,
            contract.Validate(
                vmxActivatesSecureCompute: false,
                vmxCapsGrantsSecureCompute: false,
                vmcsStoresSecureState: false,
                activeVmcsPointerIsDomainIdentity: true));
    }

    [Fact]
    public void SecureSensitiveVmread_DeniesUnlessAllNeutralProofsExist()
    {
        var policy = new SecureComputeVmReadVisibilityPolicy();

        Assert.Equal(
            SecureComputeVmReadVisibilityDecision.DeniedNoNeutralOwner,
            policy.Validate(
                hasNeutralOwner: false,
                hasReadOnlySource: false,
                hasSecureVisibility: false,
                hasMigrationClassification: false,
                hasConformanceProof: false));
        Assert.Equal(
            SecureComputeVmReadVisibilityDecision.DeniedNoReadOnlySource,
            policy.Validate(
                hasNeutralOwner: true,
                hasReadOnlySource: false,
                hasSecureVisibility: false,
                hasMigrationClassification: false,
                hasConformanceProof: false));
        Assert.Equal(
            SecureComputeVmReadVisibilityDecision.DeniedNoSecureVisibility,
            policy.Validate(
                hasNeutralOwner: true,
                hasReadOnlySource: true,
                hasSecureVisibility: false,
                hasMigrationClassification: false,
                hasConformanceProof: false));
        Assert.Equal(
            SecureComputeVmReadVisibilityDecision.DeniedNoMigrationClass,
            policy.Validate(
                hasNeutralOwner: true,
                hasReadOnlySource: true,
                hasSecureVisibility: true,
                hasMigrationClassification: false,
                hasConformanceProof: false));
        Assert.Equal(
            SecureComputeVmReadVisibilityDecision.DeniedNoConformanceProof,
            policy.Validate(
                hasNeutralOwner: true,
                hasReadOnlySource: true,
                hasSecureVisibility: true,
                hasMigrationClassification: true,
                hasConformanceProof: false));
    }

    [Fact]
    public void SecureSensitiveVmread_DoesNotReturnValueWhenConformanceProofIsMissing()
    {
        SecureComputeCompatibilityProjectionResult result =
            new SecureComputeCompatibilityProjectionService().ProjectReadOnlyValue(
                new SecureComputeCompatibilityProjectionRequest(
                    FieldId: 0x5EC0,
                    AliasBit: 1,
                    HasNeutralOwner: true,
                    HasReadOnlySource: true,
                    SecureVisibilityAllowed: true,
                    MigrationClassified: true,
                    ConformanceProven: false),
                value: 0x1234);

        Assert.False(result.IsAllowed);
        Assert.False(result.ValueAvailable);
        Assert.Equal(0UL, result.Value);
        Assert.Equal(SecureComputeVmReadVisibilityDecision.DeniedNoConformanceProof, result.Decision);
    }

    [Fact]
    public void AttestationVmread_DoesNotReturnValueWithoutExplicitVisibilityContract()
    {
        SecureComputeCompatibilityProjectionResult result =
            new SecureComputeCompatibilityProjectionService().ProjectReadOnlyValue(
                new SecureComputeCompatibilityProjectionRequest(
                    FieldId: 0xA775,
                    AliasBit: 1,
                    HasNeutralOwner: true,
                    HasReadOnlySource: true,
                    SecureVisibilityAllowed: false,
                    MigrationClassified: false,
                    ConformanceProven: false),
                value: 0x5EC0);

        Assert.False(result.IsAllowed);
        Assert.False(result.ValueAvailable);
        Assert.Equal(0UL, result.Value);
        Assert.Equal(SecureComputeVmReadVisibilityDecision.DeniedNoSecureVisibility, result.Decision);
    }

    [Fact]
    public void VmwriteSecureState_IsDeniedWithoutBackendMutation()
    {
        SecureComputeCompatibilityBoundaryResult boundary =
            new SecureComputeCompatibilityBoundary().Admit(
                SecureComputeCompatibilityOperation.WriteProjection,
                projectionOnly: false);
        SecureComputeVmWriteDecision write =
            new SecureComputeVmWriteDenyPolicy().Deny(secureSensitiveField: true);

        Assert.False(boundary.IsAllowed);
        Assert.Equal(SecureComputeCompatibilityBoundaryDecision.DeniedWriteMutation, boundary.Decision);
        Assert.Equal(SecureComputeVmWriteDecision.DeniedSecureStateMutation, write);
    }

    [Fact]
    public void AdmittedDeniedVmcall_CannotPublishBackendCompletionOrRetire()
    {
        VmxCompatibilityTrapAdmissionResult result =
            new VmxCompatibilityAdmissionService().AdmitVmCallTrapProjection(
                CreateVmCallRequest());

        Assert.Equal(
            VmxCompatibilityTrapAdmissionDecision.TrapProjectionDeniedBackend,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.BackendAdmission.DeniesBackendExecution);
        Assert.Equal(
            TrapCompletionRouteDecision.DeniedBackendExecution,
            result.CompletionRoute.Decision);
        Assert.Equal(
            TrapCompletionPublicationDecision.DeniedBackendExecution,
            result.PublicationFence.Decision);
        Assert.False(result.PublicationFence.CompletionPublicationAllowed);
        Assert.False(result.PublicationFence.RetirePublicationAllowed);
    }

    [Fact]
    public void VmcsAndVmxCapsProductionSources_DoNotExposeSecureComputeAuthority()
    {
        string source = ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Decode/VmxInstructionPayload.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs",
            "NonRTL/Core/System/Vmcs/V2/VmcsV2Descriptor.cs");

        Assert.DoesNotContain("SecureCompute", source);
        Assert.DoesNotContain("SecureDomain", source);
        Assert.DoesNotContain("VmxCaps.Secure", source);
        Assert.DoesNotContain("SecureComputeDomainDescriptor", source);
        Assert.DoesNotContain("SecureComputeSecurityLevel", source);
        Assert.DoesNotContain("DomainMeasurementDescriptor", source);
        Assert.DoesNotContain("SecureMeasurement", source);
        Assert.DoesNotContain("Attestation", source);
        Assert.DoesNotContain("WriteSecure", source);
    }

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            projectRoot,
            path.Replace('/', Path.DirectorySeparatorChar)))));
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

    private static VmxCompatibilityVmCallTrapAdmissionRequest CreateVmCallRequest() =>
        new(
            Context: CreateContext(),
            RootAuthority: CreateRoot(),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: true,
                allowGuestArchitecturalState: false,
                allowMigrationSerializableState: false),
            TrapPolicy: new TrapPolicyDescriptor().WithEnabledClasses(
                TrapPolicyClass.CompatibilityOperation),
            TrapBitmap: CreateVmCallTrapBitmap(),
            VtId: 1,
            HypercallLeafRegister: 2,
            DescriptorRegister: 3,
            ExecutionDomainTag: 4,
            AddressSpaceTag: 5,
            DescriptorValidated: true,
            CapabilityValidated: true,
            SchedulingValidated: true,
            NoEmissionValidated: true,
            ProjectionEvidenceValidated: true,
            DomainValidated: true);

    private static TrapPolicyBitmap CreateVmCallTrapBitmap()
    {
        var bitmap = new TrapPolicyBitmap();
        bitmap.EnableVmxOperation(VmxOperationKind.VmCall);
        return bitmap;
    }

    private static DomainRuntimeContext CreateContext() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0));

    private static RootAuthorityDescriptor CreateRoot() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);
}
