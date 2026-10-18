using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureRuntimeBoundaryAdmissionHookTests
{
    [Theory]
    [InlineData(SecureDomainOperationClass.Ordinary)]
    [InlineData(SecureDomainOperationClass.EnterSecureDomain)]
    public void RuntimeBoundaryAdmission_ContextIsOnlyDescriptorCarrier(SecureDomainOperationClass operation)
    {
        var descriptor = CreateSecureDescriptor(41, false, false);
        var equivalentCopy = CreateSecureDescriptor(41, false, false);
        var service = new RuntimeBoundaryAdmissionService();
        var mirror = service.Validate(CreateRequest(CreateContext(descriptor), descriptor, operation));
        if (operation == SecureDomainOperationClass.Ordinary)
            Assert.True(mirror.IsAllowed);
        else
        {
            Assert.False(mirror.IsAllowed);
            Assert.Contains("canonical machine admission is unavailable", mirror.Message);
        }
        foreach (var context in new[] { CreateContext(null, 41), CreateContext(descriptor) })
        {
            var denied = service.Validate(CreateRequest(context, equivalentCopy, operation));
            Assert.False(denied.IsAllowed);
            Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, denied.Decision);
            Assert.Contains("exact runtime context descriptor", denied.Message);
        }
    }

    [Theory]
    [InlineData(SecureDomainOperationClass.EnterSecureDomain, false)]
    [InlineData(SecureDomainOperationClass.EnterSecureDomain, true)]
    [InlineData(SecureDomainOperationClass.TouchSecureMemory, false)]
    [InlineData(SecureDomainOperationClass.TouchSecureMemory, true)]
    public void RuntimeBoundaryAdmission_PolicyApprovedCallerClassCannotAuthorizeExecution(
        SecureDomainOperationClass operationClass,
        bool requestCarrier)
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 41, measurementRequired: false, privateMemoryRequired: false);
        Assert.True(new SecureDomainAdmissionPolicy().Admit(
            descriptor, operationClass, measurement: null, memory: null).IsAllowed);

        RuntimeBoundaryAdmissionRequest request = CreateRequest(
            CreateContext(requestCarrier ? null : descriptor, domainTag: 41),
            requestCarrier ? descriptor : null,
            operationClass);
        RuntimeBoundaryAdmissionService service = new();

        for (int retry = 0; retry < 2; retry++)
        {
            RuntimeBoundaryAdmissionResult result = service.Validate(request);
            Assert.False(result.IsAllowed);
            Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
            Assert.Contains(requestCarrier ? "exact runtime context descriptor" : "canonical machine admission is unavailable", result.Message);
            Assert.False(service.CanAdmit(request));
        }
    }

    [Theory]
    [InlineData(11)]
    [InlineData(255)]
    public void RuntimeBoundaryAdmission_FabricatedClassificationIsDenied(byte value)
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(CreateContext(null), null, (SecureDomainOperationClass)value));
        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("Unknown", result.Message);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_OrdinaryOperationRemainsAllowedWhenDescriptorAbsent()
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: null),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.Ordinary));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_OrdinaryOperationRemainsAllowedWhenDescriptorDisabled()
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: SecureComputeDomainDescriptor.Disabled),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.Ordinary));

        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_OrdinaryOperationRemainsAllowedWhenDescriptorUnmaterialized()
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 0,
            measurementRequired: true,
            privateMemoryRequired: true);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.Ordinary));

        Assert.True(descriptor.IsEnabled);
        Assert.False(descriptor.IsMaterialized);
        Assert.True(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.Allowed, result.Decision);
    }

    [Theory]
    [MemberData(nameof(NonOrdinarySecureOperationClasses))]
    public void RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorMissingThroughStageB(
        SecureDomainOperationClass secureOperationClass)
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: null),
                secureDescriptor: null,
                secureOperationClass: secureOperationClass));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("descriptor", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(NonOrdinarySecureOperationClasses))]
    public void RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorDisabledThroughStageB(
        SecureDomainOperationClass secureOperationClass)
    {
        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: SecureComputeDomainDescriptor.Disabled),
                secureDescriptor: null,
                secureOperationClass: secureOperationClass));

        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(NonOrdinarySecureOperationClasses))]
    public void RuntimeBoundaryAdmission_NonOrdinaryOperationDeniesWhenDescriptorUnmaterializedThroughStageB(
        SecureDomainOperationClass secureOperationClass)
    {
        SecureComputeDomainDescriptor descriptor = CreateSecureDescriptor(
            domainTag: 0,
            measurementRequired: true,
            privateMemoryRequired: false);

        RuntimeBoundaryAdmissionResult result = new RuntimeBoundaryAdmissionService().Validate(
            CreateRequest(
                CreateContext(secureCompute: descriptor),
                secureDescriptor: null,
                secureOperationClass: secureOperationClass));

        Assert.True(descriptor.IsEnabled);
        Assert.False(descriptor.IsMaterialized);
        Assert.False(result.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, result.Decision);
        Assert.Contains("materialized", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeBoundaryAdmission_PolicyDenyReasonReachesStageBHook()
    {
        SecureDomainAdmissionPolicy policy = new();

        SecureDomainAdmissionResult missingPolicy = policy.Admit(
            descriptor: null,
            SecureDomainOperationClass.EnterSecureDomain,
            measurement: null,
            memory: null);
        SecureDomainAdmissionResult disabledPolicy = policy.Admit(
            SecureComputeDomainDescriptor.Disabled,
            SecureDomainOperationClass.EnterSecureDomain,
            measurement: null,
            memory: null);

        RuntimeBoundaryAdmissionService service = new();
        RuntimeBoundaryAdmissionResult missingStageB = service.Validate(
            CreateRequest(
                CreateContext(secureCompute: null),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));
        RuntimeBoundaryAdmissionResult disabledStageB = service.Validate(
            CreateRequest(
                CreateContext(secureCompute: SecureComputeDomainDescriptor.Disabled),
                secureDescriptor: null,
                secureOperationClass: SecureDomainOperationClass.EnterSecureDomain));

        Assert.Equal(SecureDomainAdmissionDecision.DeniedMissingDescriptor, missingPolicy.Decision);
        Assert.Equal(SecureDomainAdmissionDecision.DeniedDisabledDescriptor, disabledPolicy.Decision);
        Assert.False(missingStageB.IsAllowed);
        Assert.False(disabledStageB.IsAllowed);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, missingStageB.Decision);
        Assert.Equal(RuntimeBoundaryAdmissionDecision.SecureDomainBoundaryDenied, disabledStageB.Decision);
        Assert.Equal(missingPolicy.Reason, missingStageB.Message);
        Assert.Equal(disabledPolicy.Reason, disabledStageB.Message);

        string activationGate = ReadProjectSource(
            "HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/ProofEvidence/LegacyActivationPlan/06_stage_b_secure_admission_activation_plan.md",
            "HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/ProofEvidence/LegacyActivationPlan/21_conformance_negative_positive_test_matrix.md",
            "HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/ProofEvidence/LegacyActivationPlan/22_limited_securecompute_release_gate.md");

        Assert.Contains("Direct generic-service routing tests remain useful negative regression evidence", activationGate);
        Assert.Contains("CPU Stage-B enforcement remains open", activationGate);
        Assert.Contains("No CPU decode/SafetyVerifier/certificate/issue call graph is proven", activationGate);
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
    public void RuntimeBoundaryAdmission_RequestOnlyDescriptorCannotSupplyMemoryPolicyAuthority()
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
        Assert.Contains("exact runtime context descriptor", result.Message, StringComparison.OrdinalIgnoreCase);
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
    public void RuntimeBoundaryAdmission_NoEnabledDescriptorGuardBypassContractTests()
    {
        string runtimeAdmission = ReadProjectSource(
            "HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs");

        Assert.Contains(
            "request.SecureOperationClass != SecureDomainOperationClass.Ordinary",
            runtimeAdmission);
        Assert.Contains("_secureAdmission.Admit(", runtimeAdmission);
        Assert.DoesNotContain("secureDescriptor is { IsEnabled: true }", runtimeAdmission);
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
            "HybridCPU_ISE/Legacy/CloseToHSL/Core/Decoder/DecodedBundleTransportProjector.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Metadata/MicroOpAdmissionMetadata.cs",
            "HybridCPU_ISE/CloseToHSL/Core/ISA/Instructions");

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

    public static System.Collections.Generic.IEnumerable<object[]> NonOrdinarySecureOperationClasses()
    {
        yield return new object[] { SecureDomainOperationClass.EnterSecureDomain };
        yield return new object[] { SecureDomainOperationClass.TouchSecureMemory };
        yield return new object[] { SecureDomainOperationClass.CreateEvidence };
        yield return new object[] { SecureDomainOperationClass.PublishCompletion };
        yield return new object[] { SecureDomainOperationClass.PublishRetireSideEffect };
        yield return new object[] { SecureDomainOperationClass.SecureIo };
        yield return new object[] { SecureDomainOperationClass.SecureHypercall };
        yield return new object[] { SecureDomainOperationClass.SecureMigration };
        yield return new object[] { SecureDomainOperationClass.NestedSecureDomain };
        yield return new object[] { SecureDomainOperationClass.CompatibilityProjection };
    }

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
