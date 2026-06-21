using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureHypercallBackendOwnerPhase13Tests
{
    private static readonly SecureHypercallDecodedLeaf ProductionDecodedLeaf =
        SecureHypercallBackendOwnerAbiRegistry.DecodedLeaf;
    private static readonly SecureComputeServiceId ProductionServiceId =
        SecureHypercallBackendOwnerAbiRegistry.ServiceId;
    private static readonly SecureBackendOwnerId ProductionOwnerId =
        SecureHypercallBackendOwnerAbiRegistry.OwnerId;
    private static readonly SecureHypercallContractVersion ProductionVersion =
        SecureHypercallBackendOwnerAbiRegistry.ContractVersion;
    private static readonly SecureRevocationEpoch CurrentEpoch =
        SecureHypercallBackendOwnerAbiRegistry.OwnerEpoch;
    private static readonly SecureGrantHandle RequiredGrant =
        SecureHypercallBackendOwnerAbiRegistry.RequiredGrant;

    [Fact]
    public void ReviewedRegistryAllocatesExactProductionIdentifiers()
    {
        Assert.Equal("ADR-SC-HYP-BACKEND-OWNER", SecureHypercallBackendOwnerAbiRegistry.RegistrySource);
        Assert.Equal((ushort)CompatAbiFreezeContract.FrozenCallOpcode, SecureHypercallBackendOwnerAbiRegistry.TransportOpcode.Value);
        Assert.Equal(0x5343_4859_5042UL, ProductionDecodedLeaf.Value);
        Assert.Equal(0x5343_5356_4345UL, ProductionServiceId.Value);
        Assert.Equal(0x5343_4F57_4E52UL, ProductionOwnerId.Value);
        Assert.Equal(new SecureHypercallContractVersion(1, 0), ProductionVersion);
        Assert.Equal(new SecureRevocationEpoch(13), CurrentEpoch);
        Assert.Equal(0x5343_4752_4E54UL, RequiredGrant.LocalId);
        Assert.Equal(0x5343_4144_5250UL, RequiredGrant.ProvenanceHash);
    }

    [Fact]
    public void TransportOpcodeDecodedLeafServiceAndOwnerAreDistinctTypes()
    {
        SecureHypercallTransportOpcode transport =
            SecureHypercallBackendOwnerAbiRegistry.TransportOpcode;

        Assert.Equal((ushort)259, transport.Value);
        Assert.NotEqual(0x10UL, ProductionDecodedLeaf.Value);
        Assert.NotEqual(18UL, ProductionDecodedLeaf.Value);
        Assert.NotEqual((ulong)transport.Value, ProductionDecodedLeaf.Value);
        Assert.NotEqual((ulong)transport.Value, ProductionServiceId.Value);
        Assert.NotEqual((ulong)transport.Value, ProductionOwnerId.Value);
        Assert.NotEqual(ProductionDecodedLeaf.Value, ProductionServiceId.Value);
        Assert.NotEqual(ProductionDecodedLeaf.Value, ProductionOwnerId.Value);
        Assert.NotEqual(ProductionServiceId.Value, ProductionOwnerId.Value);

        Type[] identityTypes =
        [
            typeof(SecureHypercallTransportOpcode),
            typeof(SecureHypercallDecodedLeaf),
            typeof(SecureComputeServiceId),
            typeof(SecureBackendOwnerId),
        ];
        Assert.Equal(identityTypes.Length, identityTypes.Distinct().Count());
    }

    [Fact]
    public void UnresolvedProductionContractFailsClosed()
    {
        SecureHypercallBackendContractAdmissionResult result =
            Admit(SecureHypercallBackendContractDescriptor.Unresolved);

        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedUnresolvedContract,
            result.Decision);
        AssertClosed(result);
    }

    [Fact]
    public void MissingWrongAndStaleOwnerAreDenied()
    {
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedMissingOwner,
            Admit(request: Request(missingOwner: true)).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedWrongOwner,
            Admit(request: Request(owner: Owner(ownerId: 0xBAD))).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedOwnerEpochMismatch,
            Admit(request: Request(owner: Owner(epoch: 6))).Decision);
    }

    [Fact]
    public void UnknownServiceLeafAndVersionAreDenied()
    {
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedUnknownServiceId,
            Admit(request: Request(serviceId: new SecureComputeServiceId(0xDEAD))).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedDecodedLeafMismatch,
            Admit(request: Request(decodedLeaf: new SecureHypercallDecodedLeaf(0x11))).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedUnsupportedContractVersion,
            Admit(request: Request(version: new SecureHypercallContractVersion(2, 0))).Decision);
    }

    [Fact]
    public void WrongTransportOpcodeIsDeniedWithoutTreatingOpcodeAsService()
    {
        SecureHypercallBackendContractAdmissionResult result =
            Admit(request: Request(transportOpcode: new SecureHypercallTransportOpcode(260)));

        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedTransportOpcodeMismatch,
            result.Decision);
        AssertClosed(result);
    }

    [Fact]
    public void MissingGrantAndMissingOrStaleEvidenceAreDenied()
    {
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedMissingGrant,
            Admit(request: Request(grant: SecureGrantHandle.None)).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedMissingEvidence,
            Admit(request: Request(evidenceValidated: false)).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedStaleEvidence,
            Admit(request: Request(evidenceEpoch: new SecureRevocationEpoch(6))).Decision);
    }

    [Fact]
    public void InvalidSharedBufferRawPointerAndOpaqueHandleAreDenied()
    {
        SecureHypercallBackendArgument invalidShared = new(
            0,
            SecureHypercallArgumentOwnership.ExplicitSharedBuffer,
            Value: 1,
            Length: 0x200,
            RequiredGrant);
        SecureHypercallBackendArgument rawPointer = new(
            0,
            SecureHypercallArgumentOwnership.RawHostPointerDenied,
            Value: 0x1234,
            Length: 8,
            SecureGrantHandle.None);
        SecureHypercallBackendArgument invalidOpaque = new(
            0,
            SecureHypercallArgumentOwnership.OpaqueRuntimeHandle,
            Value: 0,
            Length: 0,
            SecureGrantHandle.None);

        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedInvalidSharedBuffer,
            Admit(request: Request(arguments: [invalidShared])).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedRawPointerRepresentation,
            Admit(request: Request(arguments: [rawPointer])).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedInvalidOpaqueHandle,
            Admit(request: Request(arguments: [invalidOpaque])).Decision);
    }

    [Fact]
    public void ReplayAndCancellationRemainPreExecutionDenials()
    {
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedReplayViolation,
            Admit(request: Request(isReplay: true)).Decision);
        Assert.Equal(
            SecureHypercallBackendContractDecision.DeniedCancelledBeforeExecution,
            Admit(request: Request(cancellationRequested: true)).Decision);
    }

    [Fact]
    public void ValidProductionContractIsProofOnlyAndPublishesNothing()
    {
        SecureHypercallBackendContractAdmissionResult result = Admit();

        Assert.True(result.IsProofOnly);
        Assert.Equal(
            SecureHypercallBackendContractDecision.AllowedProofOnlyNoExecution,
            result.Decision);
        AssertClosed(result);
    }

    [Fact]
    public void ResultAndSourceHaveNoPositiveExecutionOrCompatibilityAuthority()
    {
        string[] properties = typeof(SecureHypercallBackendContractAdmissionResult)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallBackendOwnerAbiRegistry.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/Hypercalls/SecureHypercallBackendContract.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Backend/SecureHypercallBackendContractAdmissionPolicy.cs");

        Assert.Contains(nameof(SecureHypercallBackendContractAdmissionResult.BackendExecutionAuthorized), properties);
        Assert.Contains(nameof(SecureHypercallBackendContractAdmissionResult.CompletionPublicationAuthorized), properties);
        Assert.Contains(nameof(SecureHypercallBackendContractAdmissionResult.RetirePublicationAuthorized), properties);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("VmExitReason", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("Vmcs", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("0x10", source);
    }

    private static SecureHypercallBackendContractAdmissionResult Admit(
        SecureHypercallBackendContractDescriptor? contract = null,
        SecureHypercallBackendContractRequest? request = null) =>
        SecureHypercallBackendContractAdmissionPolicy.Default.Admit(
            contract ?? Contract(),
            request ?? Request());

    private static SecureHypercallBackendContractDescriptor Contract() =>
        SecureHypercallBackendOwnerAbiRegistry.ProductionContract;

    private static SecureHypercallBackendContractRequest Request(
        SecureHypercallTransportOpcode? transportOpcode = null,
        SecureHypercallDecodedLeaf? decodedLeaf = null,
        SecureComputeServiceId? serviceId = null,
        SecureHypercallContractVersion? version = null,
        SecureBackendOwnerDescriptor? owner = default,
        bool missingOwner = false,
        SecureGrantHandle? grant = null,
        bool evidenceValidated = true,
        SecureRevocationEpoch? evidenceEpoch = null,
        IReadOnlyList<SecureHypercallBackendArgument>? arguments = null,
        bool isReplay = false,
        bool cancellationRequested = false) =>
        new(
            transportOpcode ?? SecureHypercallBackendOwnerAbiRegistry.TransportOpcode,
            decodedLeaf ?? ProductionDecodedLeaf,
            serviceId ?? ProductionServiceId,
            version ?? ProductionVersion,
            missingOwner ? null : owner ?? Owner(),
            CurrentEpoch,
            grant ?? RequiredGrant,
            evidenceValidated,
            evidenceEpoch ?? CurrentEpoch,
            IoPolicy(),
            ValidatedDomainTag: 7,
            arguments ?? Array.Empty<SecureHypercallBackendArgument>(),
            isReplay,
            IdempotentRetry: false,
            ReplayTokenMatches: false,
            cancellationRequested);

    private static SecureBackendOwnerDescriptor Owner(
        ulong? ownerId = null,
        ulong? epoch = null)
    {
        SecureBackendOwnerDescriptor owner =
            SecureHypercallBackendOwnerAbiRegistry.CreateOwnerDescriptor(
                epoch: epoch.HasValue
                    ? new SecureRevocationEpoch(epoch.Value)
                    : CurrentEpoch);

        return ownerId.HasValue
            ? owner with { OwnerId = ownerId.Value }
            : owner;
    }

    private static SecureIoDomainDescriptor IoPolicy() =>
        new(
            SecureIoDmaPolicy.ExplicitSharedBuffersOnly,
            [
                new SecureSharedBufferDescriptor(
                    BufferId: 1,
                    Start: 0x2000,
                    Length: 0x100,
                    Direction: SecureSharedBufferDirection.Bidirectional,
                    Grant: new SecureGrantHandle(
                        SecureGrantHandleKind.IoPolicy,
                        LocalId: 1,
                        ProvenanceHash: 0x51,
                        Epoch: 7),
                    EvidenceClass: SecureEvidenceVisibilityClass.HostOwnedQuarantined,
                    OwnerDomainTag: 7,
                    LifetimeEpoch: 7),
            ],
            requireCompletionFence: true,
            neutralIoOwnerMaterialized: true);

    private static void AssertClosed(
        SecureHypercallBackendContractAdmissionResult result)
    {
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
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
