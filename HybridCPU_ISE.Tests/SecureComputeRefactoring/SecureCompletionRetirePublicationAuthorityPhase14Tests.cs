using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureCompletionRetirePublicationAuthorityPhase14Tests
{
    [Fact]
    public void ProofOnlyBackendOwnerAdmission_CannotPublishCompletionOrRetire()
    {
        SecureBackendOwnerAdmissionResult admission =
            SecureBackendOwnerAdmissionPolicy.Default.Admit(
                new SecureBackendOwnerAdmissionRequest(
                    Owner(),
                    SecureBackendRfcAdrState.Approved,
                    CurrentEpoch,
                    RequestsBackendExecution: false));

        Assert.Equal(
            SecureBackendOwnerAdmissionDecision.AllowedProofOnlyNoExecution,
            admission.Decision);
        Assert.False(admission.BackendExecutionAuthorized);

        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletionFromBackendOwnerAdmission(admission);
        SecureCompletionRetirePublicationResult retire =
            PublicationPolicy.AdmitRetireFromBackendOwnerAdmission(admission);

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedProofOnlyAdmission,
            completion.Decision);
        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedProofOnlyAdmission,
            retire.Decision);
        AssertNoPublication(completion);
        AssertNoPublication(retire);
    }

    [Fact]
    public void AdmittedDeniedSecureHypercall_CannotPublishCompletionOrRetire()
    {
        SecureIoHypercallAdmissionResult admission =
            SecureIoHypercallAdmissionResult.AllowedAdmittedDenied();

        Assert.True(admission.IsAdmittedDenied);
        Assert.False(admission.BackendExecutionAuthorized);
        Assert.False(admission.CompletionPublicationAuthorized);
        Assert.False(admission.RetirePublicationAuthorized);

        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletionFromSecureIoHypercallAdmission(admission);
        SecureCompletionRetirePublicationResult retire =
            PublicationPolicy.AdmitRetireFromSecureIoHypercallAdmission(admission);

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedAdmittedDeniedAdmission,
            completion.Decision);
        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedAdmittedDeniedAdmission,
            retire.Decision);
        AssertNoPublication(completion);
        AssertNoPublication(retire);
    }

    [Fact]
    public void RegistryBackedPhase13OwnerServiceAdmission_CannotPublishCompletionOrRetire()
    {
        SecureHypercallBackendContractAdmissionResult admission =
            SecureHypercallBackendContractAdmissionPolicy.Default.Admit(
                SecureHypercallBackendOwnerAbiRegistry.ProductionContract,
                ContractRequest());

        Assert.True(admission.IsProofOnly);
        Assert.False(admission.BackendExecutionAuthorized);
        Assert.False(admission.CompletionPublicationAuthorized);
        Assert.False(admission.RetirePublicationAuthorized);

        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletionFromHypercallContractAdmission(admission);
        SecureCompletionRetirePublicationResult retire =
            PublicationPolicy.AdmitRetireFromHypercallContractAdmission(admission);

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRegistryBackedProofOnlyAdmission,
            completion.Decision);
        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRegistryBackedProofOnlyAdmission,
            retire.Decision);
        AssertNoPublication(completion);
        AssertNoPublication(retire);
    }

    [Fact]
    public void CompletionFenceAlone_CannotCrossBackendResultBoundary()
    {
        SecureCompletionRetirePublicationResult result =
            PublicationPolicy.AdmitCompletion(new SecureCompletionPublicationAuthorityRequest(
                SecurePublicationPathKind.NeutralRuntimeBackendResult,
                SecurePublicationBackendResultState.Missing,
                BackendResultOwner: null,
                CompletionOwner: Owner(),
                PublicationFence: RetireFence(),
                CurrentEpoch,
                CompletionRecordMaterialized: true));

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedNoBackendResult,
            result.Decision);
        Assert.Equal(SecurePublicationLadderStep.NoBackendResult, result.LadderStep);
        AssertNoPublication(result);
    }

    [Fact]
    public void RetirePublication_RequiresSeparateRetireOwnerAndExplicitRetireFence()
    {
        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletion(CompletionRequest(CompletionFence()));

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.CompletionPublishedRetirePending,
            completion.Decision);
        Assert.True(completion.IsCompletionOnly);

        SecureCompletionRetirePublicationResult missingOwner =
            PublicationPolicy.AdmitRetire(new SecureRetirePublicationAuthorityRequest(
                completion,
                RetireOwner: null,
                PublicationFence: RetireFence(),
                CurrentEpoch,
                EvidenceVisibilityClass.GuestArchitecturalState,
                TrapCompletionMigrationClass.RecomputedAfterRestore));

        SecureCompletionRetirePublicationResult missingRetireFence =
            PublicationPolicy.AdmitRetire(new SecureRetirePublicationAuthorityRequest(
                completion,
                RetireOwner: Owner(),
                PublicationFence: CompletionFence(),
                CurrentEpoch,
                EvidenceVisibilityClass.GuestArchitecturalState,
                TrapCompletionMigrationClass.RecomputedAfterRestore));

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRetireOwnerMissing,
            missingOwner.Decision);
        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRetireFence,
            missingRetireFence.Decision);
        AssertNoPublication(missingOwner);
        AssertNoPublication(missingRetireFence);
    }

    [Fact]
    public void RetirePublication_RejectsUnsafeEvidenceAndMigrationClass()
    {
        SecureCompletionRetirePublicationResult completion =
            PublicationPolicy.AdmitCompletion(CompletionRequest(CompletionFence()));

        SecureCompletionRetirePublicationResult hostEvidence =
            PublicationPolicy.AdmitRetire(new SecureRetirePublicationAuthorityRequest(
                completion,
                Owner(),
                RetireFence(),
                CurrentEpoch,
                EvidenceVisibilityClass.HostOwnedRuntimeEvidence,
                TrapCompletionMigrationClass.RecomputedAfterRestore));

        SecureCompletionRetirePublicationResult unclassifiedMigration =
            PublicationPolicy.AdmitRetire(new SecureRetirePublicationAuthorityRequest(
                completion,
                Owner(),
                RetireFence(),
                CurrentEpoch,
                EvidenceVisibilityClass.CompatibilityAlias,
                TrapCompletionMigrationClass.Unclassified));

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRetireEvidence,
            hostEvidence.Decision);
        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedRetireMigrationClass,
            unclassifiedMigration.Decision);
        AssertNoPublication(hostEvidence);
        AssertNoPublication(unclassifiedMigration);
    }

    [Fact]
    public void GenericTrapRouteFlags_AreNotSecureComputeAuthorityWithoutOwnerPathReachability()
    {
        SecureCompletionRetirePublicationResult result =
            PublicationPolicy.DenyGenericTrapRouteFlags(
                completionPublicationFlag: true,
                retirePublicationFlag: true);

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedGenericTrapRouteNotSecureAuthority,
            result.Decision);
        AssertNoPublication(result);

        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Publication/SecureCompletionRetirePublicationAuthorityPolicy.cs");

        Assert.Contains("owner/path reachability", source);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor", source);
        Assert.DoesNotContain("TrapCompletionRouteService", source);
        Assert.DoesNotContain("TrapCompletionPublicationFence", source);
    }

    [Fact]
    public void VmxProjectionOnly_RemainsZeroAuthorityForSecureComputePublication()
    {
        SecureCompletionRetirePublicationResult result =
            PublicationPolicy.DenyVmxCompatibilityProjectionOnly();

        Assert.Equal(
            SecureCompletionRetirePublicationDecision.DeniedVmxProjectionOnly,
            result.Decision);
        Assert.Equal(SecurePublicationLadderStep.NoBackendResult, result.LadderStep);
        AssertNoPublication(result);
    }

    [Fact]
    public void MigrationPayloadClasses_ExcludeHostEvidenceTokensRawSecretsPointersAndCompatibilityMetadata()
    {
        SecureCheckpointPayloadClass[] deniedPayloads =
        [
            SecureCheckpointPayloadClass.HostOwnedEvidence,
            SecureCheckpointPayloadClass.SchedulerEvidence,
            SecureCheckpointPayloadClass.BackendBindingEvidence,
            SecureCheckpointPayloadClass.NativeTokenEvidence,
            SecureCheckpointPayloadClass.RawMeasurementSecret,
            SecureCheckpointPayloadClass.RawSealingKey,
            SecureCheckpointPayloadClass.ActiveHostPointer,
            SecureCheckpointPayloadClass.VmcsProjectionMetadata,
            SecureCheckpointPayloadClass.CompatibilityProjectionMetadata,
        ];

        foreach (SecureCheckpointPayloadClass payload in deniedPayloads)
        {
            Assert.NotEqual(
                SecureCheckpointPayloadDecision.Allowed,
                SecureCheckpointPayloadPolicy.FailClosed.Classify(payload));
        }
    }

    [Fact]
    public void CompilerNoEmission_RemainsClosedForSecureBackendShortcuts()
    {
        var contract = new SecureComputeNoEmissionContract();

        Assert.Equal(
            SecureComputeNoEmissionViolation.None,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.NewInstructionEncoding,
            contract.Validate(
                emitsNewInstructionEncoding: true,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.CapabilityAwareLoadStoreFetch,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: true,
                emitsVmxSecureMode: false));
        Assert.Equal(
            SecureComputeNoEmissionViolation.VmxSecureModeEmission,
            contract.Validate(
                emitsNewInstructionEncoding: false,
                emitsNewOperandFormat: false,
                emitsCapabilityAwareLoadStoreFetch: false,
                emitsVmxSecureMode: true));
    }

    private static SecureCompletionRetirePublicationAuthorityPolicy PublicationPolicy =>
        SecureCompletionRetirePublicationAuthorityPolicy.Default;

    private static SecureRevocationEpoch CurrentEpoch =>
        SecureHypercallBackendOwnerAbiRegistry.OwnerEpoch;

    private static SecureBackendOwnerDescriptor Owner(
        SecureBackendOwnerSource source = SecureBackendOwnerSource.NeutralRuntimeService,
        bool completionFenceValidated = true,
        bool retireFenceValidated = true) =>
        SecureHypercallBackendOwnerAbiRegistry.CreateOwnerDescriptor(
            source,
            CurrentEpoch,
            grantProofValidated: true,
            evidenceProofValidated: true,
            completionFenceValidated,
            retireFenceValidated,
            negativeTestsPresent: true);

    private static SecureCompletionPublicationAuthorityRequest CompletionRequest(
        SecureCompletionPublicationFence fence) =>
        new(
            SecurePublicationPathKind.NeutralRuntimeBackendResult,
            SecurePublicationBackendResultState.InternalNeutralResult,
            BackendResultOwner: Owner(),
            CompletionOwner: Owner(),
            PublicationFence: fence,
            CurrentEpoch,
            CompletionRecordMaterialized: true);

    private static SecureCompletionPublicationFence CompletionFence() =>
        new(
            SecureCompletionFenceState.CompletionAllowed,
            SecureRetirePublicationRule.CompletionFenceRequired);

    private static SecureCompletionPublicationFence RetireFence() =>
        new(
            SecureCompletionFenceState.RetireAllowed,
            SecureRetirePublicationRule.ExplicitRetireFenceRequired);

    private static SecureHypercallBackendContractRequest ContractRequest() =>
        new(
            SecureHypercallBackendOwnerAbiRegistry.TransportOpcode,
            SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf,
            SecureHypercallBackendOwnerAbiRegistry.ServiceId,
            SecureHypercallBackendOwnerAbiRegistry.ContractVersion,
            Owner(),
            CurrentEpoch,
            SecureHypercallBackendOwnerAbiRegistry.RequiredGrant,
            EvidenceValidated: true,
            EvidenceEpoch: CurrentEpoch,
            IoPolicy: null,
            ValidatedDomainTag: 7,
            Arguments: Array.Empty<SecureHypercallBackendArgument>(),
            IsReplay: false,
            IdempotentRetry: false,
            ReplayTokenMatches: false,
            CancellationRequested: false);

    private static void AssertNoPublication(SecureCompletionRetirePublicationResult result)
    {
        Assert.True(result.IsDenied);
        Assert.False(result.CompletionPublished);
        Assert.False(result.RetirePublished);
        Assert.NotEqual(SecurePublicationLadderStep.RetirePublication, result.LadderStep);
    }

    private static string ReadProjectSource(params string[] relativePaths)
    {
        string root = FindRepositoryRoot();
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            root,
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
}
