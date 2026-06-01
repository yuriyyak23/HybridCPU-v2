using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureRuntimeBoundaryAdmissionHookTests
{
    [Fact]
    public void RuntimeBoundaryAdmission_DoesNotEvaluateSecureChecksWhenDescriptorAbsent()
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: null),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_DoesNotEvaluateSecureChecksWhenDescriptorDisabled()
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: SecureComputeDomainDescriptor.Disabled),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_ActiveDescriptorDoesNotOverDenyOrdinaryOperation()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 41,
            measurementRequired: true,
            privateMemoryRequired: true);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.Ordinary));

        Assert.True(descriptor.IsActive);
        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_ActiveDescriptorDeniesSecureOperationMissingMeasurement()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 43,
            measurementRequired: true,
            privateMemoryRequired: false);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("measurement", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_RequestDescriptorDeniesSecureOperationMissingMemoryPolicy()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 47,
            measurementRequired: false,
            privateMemoryRequired: true);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: null, domainTag: descriptor.DomainTag),
                secureDescriptor: descriptor,
                secureOperationClass: SecureDomainOperationClass.TouchSecureMemory));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("memory", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_EnabledUnmaterializedDescriptorFailsClosedForSecureOperation()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 0,
            measurementRequired: true,
            privateMemoryRequired: false);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.True(descriptor.IsEnabled);
        Assert.False(descriptor.IsActive);
        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_ActiveDescriptorMustMatchNeutralRuntimeDomainTag()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 53,
            measurementRequired: false,
            privateMemoryRequired: false);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor, domainTag: 54),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("domain tag", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StageASources_DoNotRequireSecureComputePolicy()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/NonRTL/Core/Decoder/DecodedBundleTransportProjector.cs",
            "HybridCPU_ISE/NonRTL/Core/Pipeline/Metadata/MicroOpAdmissionMetadata.cs",
            "HybridCPU_ISE/CloseToRTL/Core/ISA/Instructions");

        Assert.DoesNotContain("SecureCompute", source);
        Assert.DoesNotContain("SecureDomainAdmission", source);
        Assert.DoesNotContain("SecureOperationClass", source);
    }

    private static RuntimeBoundaryAdmissionRequest CreateRequest(
        DomainRuntimeContext context,
        SecureComputeDomainDescriptor? secureDescriptor,
        SecureDomainOperationClass secureOperationClass)
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;

        return new RuntimeBoundaryAdmissionRequest(
            Context: context,
            RootAuthority: CreateRoot(capability),
            EvidencePolicy: new EvidencePolicyDescriptor(
                allowCompatibilityAliases: false,
                allowGuestArchitecturalState: true,
                allowMigrationSerializableState: false),
            Operation: new DomainRuntimeOperation(
                DomainRuntimeOperationKind.EnterDomain,
                DomainRuntimeOperationSource.RuntimeService,
                requiresCapabilityGrant: true,
                isProjectionOnly: false),
            DomainBoundary: DomainBoundaryDescriptor.FullDomainRuntime,
            CapabilityRequirement: CapabilityBoundaryRequirement.TypedGrant(
                capability,
                CapabilityGrantScope.CompatibilityProjection),
            EvidenceRequirement: EvidenceBoundaryRequirement.GuestVisible(
                EvidenceVisibilityClass.GuestArchitecturalState),
            SecureDescriptor: secureDescriptor,
            SecureOperationClass: secureOperationClass,
            SecureMeasurement: null,
            SecureMemory: null);
    }

    private static DomainRuntimeContext CreateContext(
        SecureComputeDomainDescriptor? secureCompute,
        ulong? domainTag = null)
    {
        const ulong capability = VmxV2InstructionCaps.VmFunc;

        return new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: capability,
                runtimeEnabledCaps: capability,
                domainGrantedCaps: capability),
            secureCompute: secureCompute,
            domainTag: domainTag ?? secureCompute?.DomainTag ?? 0,
            addressSpaceTag: 9);
    }

    private static RootAuthorityDescriptor CreateRoot(ulong capabilities) =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: capabilities,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: true);

    private static SecureComputeDomainDescriptor CreateSecureDescriptor(
        ulong domainTag,
        bool measurementRequired,
        bool privateMemoryRequired) =>
        new(
            domainTag,
            SecureComputeSecurityLevel.Measured,
            measurementRequired,
            privateMemoryRequired,
            SecureHostInspectionPolicy.DenyAll,
            SecureEvidencePolicy.FailClosed,
            SecureMigrationDescriptor.Disabled,
            SecureIoDomainDescriptor.Disabled,
            SecureHypercallDescriptor.Disabled,
            SecureDebugPolicy.Denied,
            SecureCompatibilityProjectionPolicy.DenyAll);

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path =>
        {
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            if (Directory.Exists(fullPath))
            {
                return string.Concat(Directory
                    .EnumerateFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            }

            throw new FileNotFoundException("Unable to locate source path.", fullPath);
        }));
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
