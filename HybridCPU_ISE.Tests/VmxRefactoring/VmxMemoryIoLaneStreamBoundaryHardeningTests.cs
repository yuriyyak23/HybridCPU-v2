using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class VmxMemoryIoLaneStreamBoundaryHardeningTests
{
    [Fact]
    public void MemoryOwnedVmcsFields_RemainReadOnlyProjectionVocabularyWithoutWriteAuthority()
    {
        foreach (VmcsField field in MemoryOwnedVmcsFields())
        {
            Assert.True(VmcsFieldProjectionSchema.TryGet(field, out VmcsFieldProjectionSchemaEntry entry));
            Assert.Equal(VmcsFieldProjectionOwner.MemoryDomainDescriptor, entry.Owner);
            Assert.True(VmcsFieldProjectionSchema.CanRead(entry));
            Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));

            VmcsFieldAliasResult writeAlias = new VmcsFieldAliasProjection().ValidateAccess(
                new VmcsFieldAliasRequest(
                    field,
                    VmcsFieldAliasAccess.Write,
                    entry.EvidenceClass,
                    entry.IsGeneratedAlias,
                    DescriptorValidated: true,
                    AllowWrite: true),
                CreateAliasAndGuestEvidencePolicy());

            Assert.Equal(VmcsFieldAliasDecision.WriteDenied, writeAlias.Decision);
        }
    }

    [Fact]
    public void IoAndLaneRuntimeAdmission_RejectCompatibilityProjectionAsAuthority()
    {
        MemoryDomainRuntimeResult missingMemoryOwner = new MemoryDomainRuntime().Validate(
            new MemoryDomainRuntimeRequest(
                Descriptor: null,
                RequiresAddressSpace: true,
                RequiresTranslationPolicy: true,
                RequiresSecondStageTranslation: true,
                RequiresDirtyTracking: true));
        Assert.Equal(MemoryDomainRuntimeDecision.MissingDescriptor, missingMemoryOwner.Decision);

        var ioCompatibilityOnly = new IoDomainDescriptor(
            virtualizationBlock: null,
            dmaWindow: null,
            ownsDmaAuthority: false,
            ownsIommuAuthority: false,
            compatibilityProjectionEnabled: true);
        IoDomainRuntimeResult ioDenied = new IoDomainRuntime().Validate(
            new IoDomainRuntimeRequest(
                ioCompatibilityOnly,
                RequiresDmaAuthority: true,
                RequiresIommuAuthority: true,
                RequiresVirtualizationBlock: true,
                RequiresDmaWindow: true,
                RequiresCompatibilityProjection: true));
        Assert.Equal(IoDomainRuntimeDecision.DmaAuthorityDenied, ioDenied.Decision);

        var lane6CompatibilityOnly = new Lane6DomainDescriptor(
            Lane6DomainAuthority.CompatibilityProjection,
            VirtualizationLaneBindingPolicy.Lane6Id,
            tokenNamespaceId: 1,
            queueNamespaceId: 2,
            fenceDomainId: 3,
            allowsCompatibilityProjection: true);
        Lane6DomainRuntimeResult lane6Denied = new Lane6DomainRuntime().Validate(
            new Lane6DomainRuntimeRequest(
                lane6CompatibilityOnly,
                RequiresTokenNamespace: true,
                RequiresQueueNamespace: true,
                RequiresFenceDomain: true,
                RequiresCompatibilityProjection: true));
        Assert.Equal(Lane6DomainRuntimeDecision.RuntimeAuthorityRequired, lane6Denied.Decision);

        var lane7CompatibilityOnly = new Lane7AcceleratorDescriptor(
            Lane7AcceleratorAuthority.CompatibilityProjection,
            VirtualizationLaneBindingPolicy.Lane7Id,
            backendBindingId: 1,
            handleNamespaceId: 2,
            tokenNamespaceId: 3,
            completionRouteId: 4,
            requiresRuntimeBackendBinding: true,
            allowsCompatibilityProjection: true);
        Lane7DomainRuntimeResult lane7Denied = new Lane7DomainRuntime().Validate(
            new Lane7DomainRuntimeRequest(
                lane7CompatibilityOnly,
                RequiresBackendBinding: true,
                RequiresHandleNamespace: true,
                RequiresTokenNamespace: true,
                RequiresCompletionRoute: true,
                RequiresCompatibilityProjection: true));
        Assert.Equal(Lane7DomainRuntimeDecision.RuntimeAuthorityRequired, lane7Denied.Decision);
    }

    [Fact]
    public void FrozenVmxIoAliases_ReturnDeniedResultsWithoutIommuMutation()
    {
        IommuDomainBinding binding = IommuDomainBinding.Create(
            ioDomainTag: 7,
            domainId: 9,
            domainTag: 0x1234,
            deviceId: 11,
            permissions: IOMMUAccessPermissions.ReadWrite);

        Assert.True(IOMMU.VmxCompatibilityIoAliasesAreReadOnlyDenied);
        Assert.Equal(default, IOMMU.BindVmxDomain(binding));
        Assert.False(IOMMU.UnbindVmxDomain(7, 9, 11));
        Assert.False(IOMMU.TryGetVmxDomainBinding(7, 9, 11, out IommuDomainBinding projected));
        Assert.Equal(default, projected);
        Assert.False(IOMMU.TryTranslateVmxDma(
            binding,
            ioVirtualAddress: 0x1000,
            accessSize: 8,
            IOMMUAccessPermissions.Read,
            out DmaTranslationResult translation));
        Assert.Equal(default, translation);
        Assert.Equal(0, IOMMU.CountVmxIotlbEntries());
        Assert.Equal(0, IOMMU.InvalidateVmxIotlbAll());
        Assert.Equal(0, IOMMU.InvalidateVmxIotlbByVmid(7));
        Assert.Equal(0, IOMMU.InvalidateVmxIotlbByDomain(7, 9));
        Assert.Equal(0, IOMMU.InvalidateVmxIotlbByDevice(7, 9, 11));
        Assert.Equal(0, IOMMU.InvalidateVmxIotlbByEpoch(7, 9, 11, domainEpoch: 1));
        Assert.Equal(0, IOMMU.ApplyVmxInvalidation(
            VmxInvalidationScope.AllContexts,
            descriptor: 0,
            isEpt: true));

        var policy = new VmxInvalidationScopeAdmissionPolicy();
        VmxInvalidationScopeAdmissionResult directMutation = policy.Validate(
            new VmxInvalidationScopeAdmissionRequest(
                VmxInvalidationScope.AllContexts,
                DescriptorValidated: true,
                CapabilityValidated: true,
                RuntimeInvalidationValidated: true,
                TranslationAuthorityValidated: true,
                AttemptsDirectMmuMutation: true));

        Assert.Equal(
            VmxInvalidationScopeAdmissionDecision.DirectMmuMutationDenied,
            directMutation.Decision);
    }

    [Fact]
    public void TrapRouteFence_StillDeniesCompletionAndRetirePublicationForProjectionOnlyBoundary()
    {
        NeutralTrapResult trap = NeutralTrapResult.Trap(
            TrapRequest.ForVmxOperation(
                VmxOperationKind.VmCall,
                IsaOpcodeValues.VMCALL,
                vtId: 1,
                executionDomainTag: 2,
                addressSpaceTag: 3),
            NeutralTrapResultKind.CompatibilityOperationIntercept);
        RuntimeBoundaryAdmissionResult runtimeAdmission =
            RuntimeBoundaryAdmissionResult.Allowed(DomainRuntimeAuthorityResult.Allowed);
        TrapCompletionRouteRequest routeRequest =
            TrapCompletionRouteRequest.ProjectionOnlyDenied(trap, runtimeAdmission);

        TrapCompletionRouteResult route =
            TrapCompletionRouteService.Default.Authorize(routeRequest);
        TrapCompletionPublicationFenceResult fence =
            TrapCompletionRouteService.Default.EvaluateFence(routeRequest, route);

        Assert.Equal(TrapCompletionRouteDecision.DeniedBackendExecution, route.Decision);
        Assert.True(route.DeniesBackendExecution);
        Assert.False(route.CompletionPublicationAuthorized);
        Assert.False(route.RetirePublicationAuthorized);
        Assert.Equal(TrapCompletionPublicationDecision.DeniedBackendExecution, fence.Decision);
        Assert.False(fence.CompletionPublicationAllowed);
        Assert.False(fence.RetirePublicationAllowed);
        Assert.True(fence.Completion.IsEmpty);
    }

    [Fact]
    public void Source_DoesNotWireLaneStreamHelpersIntoVmxBackendPublicationOrSecureCompute()
    {
        string laneStreamSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs",
            "NonRTL/Core/Execution/DmaStreamCompute/VmxDmaDescriptorValidator.cs",
            "CloseToRTL/Core/Runtime/Domains/Admission/Lane6/Lane6DomainRuntime.cs",
            "CloseToRTL/Core/Runtime/Domains/Admission/Lane7/Lane7DomainRuntime.cs",
            "CloseToRTL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs",
            "CloseToRTL/Core/Runtime/Lanes/Lane7/Completion/Lane7CompletionPolicy.cs");
        string vmxFrontendSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs",
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs",
            "CloseToRTL/Core/Execution/Dispatch/ExecutionDispatcherV4.VmxCompatibility.cs",
            "CloseToRTL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.VmxRetire.cs");
        string secureComputeProjectionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmxCapsProjectionFence.cs",
            "CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmcsProjectionFence.cs");

        Assert.Contains("Guest Lane6 compatibility execution is fail-closed", laneStreamSource);
        Assert.Contains("Guest Lane7 compatibility execution is fail-closed", laneStreamSource);
        Assert.Contains("VmxDmaDescriptorValidator", laneStreamSource);
        Assert.Contains("RuntimeAuthorityRequired", laneStreamSource);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", laneStreamSource);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", laneStreamSource);
        Assert.DoesNotContain("CompletionRecord.TryFromCompatibilityExit", laneStreamSource);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", laneStreamSource);
        Assert.DoesNotContain("VmxRetireEffect.VmCall", laneStreamSource);
        Assert.DoesNotContain("SecureComputeVmxCapsProjectionFence", laneStreamSource);
        Assert.DoesNotContain("SecureComputeCompatibilityBoundaryMatrixPolicy", laneStreamSource);
        Assert.DoesNotContain("VmcsManager", laneStreamSource);
        Assert.DoesNotContain("IVmcsManager", laneStreamSource);
        Assert.DoesNotContain("VmxExecutionUnit", laneStreamSource);

        foreach (string forbidden in new[]
                 {
                     "DmaStreamComputeRuntime",
                     "VmxDmaDescriptorValidator",
                     "ExternalAcceleratorRuntime",
                     "Lane6DomainRuntime",
                     "Lane7DomainRuntime",
                     "Lane7CompletionPolicy",
                     "DmaStreamComputeRetirePublication",
                     "SystemDeviceCommandMicroOp",
                 })
        {
            Assert.DoesNotContain(forbidden, vmxFrontendSource);
            Assert.DoesNotContain(forbidden, secureComputeProjectionSource);
        }
    }

    [Fact]
    public void Source_KeepsMutableVmcsStoresAndCompatibilityExecutionManagersAbsent()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs",
            "CloseToRTL/Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs",
            "CloseToRTL/Core/Runtime/IO/Dma/DmaAuthorityService.cs",
            "CloseToRTL/Core/Runtime/Nested/MemoryComposition/NestedMemoryDomainComposer.cs",
            "CloseToRTL/Core/Runtime/Domains/Admission/Memory/MemoryDomainRuntime.cs",
            "CloseToRTL/Core/Runtime/Domains/Admission/IO/IoDomainRuntime.cs");

        Assert.Contains("VmxCompatibilityIoAliasesAreReadOnlyDenied", source);
        Assert.Contains("RuntimeAuthorityMissing", source);
        Assert.Contains("DescriptorAuthorityDenied", source);
        Assert.DoesNotContain("TryWriteScalarField", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor.RuntimeOwnedPublication", source);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit", source);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", source);
    }

    [Fact]
    public void CompilerCoreAndNonVmxIsa_DoNotEmitVmxActivationOrMutationOpcodesForPhase10()
    {
        string source = ReadRepositorySources(
            "HybridCPU_Compiler/Core",
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions/NonVmx");

        foreach (string forbidden in new[]
                 {
                     "InstructionsEnum.VMXON",
                     "InstructionsEnum.VMXOFF",
                     "InstructionsEnum.VMLAUNCH",
                     "InstructionsEnum.VMRESUME",
                     "InstructionsEnum.VMPTRLD",
                     "InstructionsEnum.VMPTRST",
                     "InstructionsEnum.VMCLEAR",
                     "InstructionsEnum.VMWRITE",
                     "InstructionsEnum.VMCALL",
                     "VMXON",
                     "VMWRITE",
                     "VMCALL",
                 })
        {
            Assert.DoesNotContain(forbidden, source);
        }
    }

    private static VmcsField[] MemoryOwnedVmcsFields() =>
        new[]
        {
            VmcsField.GuestCr3,
            VmcsField.HostCr3,
            VmcsField.EptPointer,
            VmcsField.Vpid,
            VmcsField.Cr3TargetCount,
        };

    private static EvidencePolicyDescriptor CreateAliasAndGuestEvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

    private static string ReadRepositorySources(params string[] relativeRoots)
    {
        string repositoryRoot = ActiveVmxConformanceHelpers.FindRepositoryRoot();
        var sources = new List<string>();
        foreach (string relativeRoot in relativeRoots)
        {
            string root = Path.Combine(
                repositoryRoot,
                relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            sources.AddRange(Directory
                .GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        }

        return string.Concat(sources);
    }
}
