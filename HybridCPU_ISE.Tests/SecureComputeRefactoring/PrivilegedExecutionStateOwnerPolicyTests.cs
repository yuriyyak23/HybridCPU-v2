using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class PrivilegedExecutionStateOwnerPolicyTests
{
    [Fact]
    public void PrivilegedExecutionStateOwner_MissingDescriptor_Denied()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                new PrivilegedExecutionStateOwnerRequest(
                    Descriptor: null,
                    RuntimeDomainTag: 7,
                    RuntimeAddressSpaceTag: 9,
                    CurrentEpoch: new PrivilegedExecutionStateEpoch(11)));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedMissingDescriptor,
            result);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_UnmaterializedDescriptor_Denied()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(Descriptor() with { Materialized = false }));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedUnmaterializedDescriptor,
            result);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_DomainTagMismatch_Denied()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(runtimeDomainTag: 8));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedDomainTagMismatch,
            result);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_AddressSpaceTagMismatch_Denied()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(runtimeAddressSpaceTag: 10));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedAddressSpaceTagMismatch,
            result);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_StaleEpoch_Denied()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(currentEpoch: 8));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedStaleEpoch,
            result);
    }

    [Theory]
    [InlineData(PrivilegedControlRegisterKind.GuestCr0)]
    [InlineData(PrivilegedControlRegisterKind.GuestCr4)]
    public void PrivilegedExecutionStateOwner_ReservedBits_Denied(
        PrivilegedControlRegisterKind register)
    {
        PrivilegedExecutionStateDescriptor descriptor = Descriptor();
        descriptor = register == PrivilegedControlRegisterKind.GuestCr0
            ? descriptor with
            {
                GuestCr0 = new PrivilegedControlRegisterValue(
                    PrivilegedControlRegisterKind.GuestCr0,
                    1UL << 63),
            }
            : descriptor with
            {
                GuestCr4 = new PrivilegedControlRegisterValue(
                    PrivilegedControlRegisterKind.GuestCr4,
                    1UL << 63),
            };

        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(Request(descriptor));

        AssertDenied(
            register == PrivilegedControlRegisterKind.GuestCr0
                ? PrivilegedExecutionStateOwnerDecision.DeniedGuestCr0ReservedBits
                : PrivilegedExecutionStateOwnerDecision.DeniedGuestCr4ReservedBits,
            result);
    }

    [Theory]
    [InlineData(PrivilegedControlRegisterKind.GuestCr0)]
    [InlineData(PrivilegedControlRegisterKind.GuestCr4)]
    public void PrivilegedExecutionStateOwner_RequiredBits_Denied(
        PrivilegedControlRegisterKind register)
    {
        PrivilegedExecutionStateDescriptor descriptor = Descriptor();
        descriptor = register == PrivilegedControlRegisterKind.GuestCr0
            ? descriptor with
            {
                GuestCr0 = new PrivilegedControlRegisterValue(
                    PrivilegedControlRegisterKind.GuestCr0,
                    0),
            }
            : descriptor with
            {
                GuestCr4 = new PrivilegedControlRegisterValue(
                    PrivilegedControlRegisterKind.GuestCr4,
                    0),
            };

        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(Request(descriptor));

        AssertDenied(
            register == PrivilegedControlRegisterKind.GuestCr0
                ? PrivilegedExecutionStateOwnerDecision.DeniedGuestCr0RequiredBits
                : PrivilegedExecutionStateOwnerDecision.DeniedGuestCr4RequiredBits,
            result);
    }

    [Theory]
    [InlineData(PrivilegedExecutionStateEvidenceClass.Unclassified)]
    [InlineData(PrivilegedExecutionStateEvidenceClass.HostOwnedQuarantined)]
    [InlineData(PrivilegedExecutionStateEvidenceClass.CompatibilityAlias)]
    public void PrivilegedExecutionStateOwner_NonGuestVisibleEvidence_Denied(
        PrivilegedExecutionStateEvidenceClass evidenceClass)
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(Descriptor() with { EvidenceClass = evidenceClass }));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedEvidenceClass,
            result);
    }

    [Theory]
    [InlineData(PrivilegedExecutionStateMigrationClass.Unclassified)]
    [InlineData(PrivilegedExecutionStateMigrationClass.DomainLocal)]
    public void PrivilegedExecutionStateOwner_NonRevalidatedMigrationClass_Denied(
        PrivilegedExecutionStateMigrationClass migrationClass)
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(
                Request(Descriptor() with { MigrationClass = migrationClass }));

        AssertDenied(
            PrivilegedExecutionStateOwnerDecision.DeniedMigrationClass,
            result);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_ValidDescriptor_AcceptsOwnerOnly()
    {
        PrivilegedExecutionStateOwnerResult result =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(Request());

        Assert.Equal(
            PrivilegedExecutionStateOwnerDecision.AllowedOwnerMaterializedProjectionClosed,
            result.Decision);
        Assert.True(result.IsAllowed);
        Assert.True(result.OwnerAccepted);
        Assert.False(result.ReadOnlyProjectionAuthorized);
        Assert.False(result.MutationAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void PrivilegedExecutionStateOwner_AdmissionAloneWithoutProjectionInputs_DoesNotOpenVmread(
        VmcsField field)
    {
        PrivilegedExecutionStateOwnerResult owner =
            PrivilegedExecutionStateOwnerPolicy.Default.Admit(Request());
        Assert.True(owner.IsAllowed);

        VmcsReadOnlyValueProjectionResult projection =
            new VmcsReadOnlyValueProjectionService().Project(
                new VmcsReadOnlyValueProjectionRequest(
                    FieldId: (ushort)field,
                    RuntimeAdmission: RuntimeBoundaryAdmissionResult.Allowed(default),
                    EvidencePolicy: new EvidencePolicyDescriptor(
                        allowCompatibilityAliases: true,
                        allowGuestArchitecturalState: true,
                        allowMigrationSerializableState: false),
                    DescriptorValidated: true,
                    Execution: new ExecutionDomainDescriptor(),
                    Memory: null,
                    Completion: null));

        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied,
            projection.Decision);
        Assert.False(projection.IsProjected);
        Assert.Equal(0, projection.Value);
    }

    [Fact]
    public void PrivilegedExecutionStateOwner_NotVmcsBacked_SourceGuard()
    {
        string ownerSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/PrivilegedExecutionStateDescriptor.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/PrivilegedExecutionStateOwnerPolicy.cs");
        string projectionSource = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("PrivilegedExecutionStateDescriptor", ownerSource);
        Assert.Contains("PrivilegedControlRegisterLegalityPolicy", ownerSource);
        Assert.Contains("RevalidatedAfterRestore", ownerSource);
        Assert.Contains("ReadOnlyProjectionAuthorized: false", ownerSource);
        Assert.Contains("BackendExecutionAuthorized: false", ownerSource);
        Assert.Contains("CompletionPublicationAuthorized: false", ownerSource);
        Assert.Contains("RetirePublicationAuthorized: false", ownerSource);

        foreach (string forbidden in new[]
        {
            "Vmcs",
            "Vmx",
            "VmxCaps",
            "ReadFieldValue(",
            "WriteFieldValue(",
            "TryReadScalarField",
            "RuntimeOwnedPublication",
        })
        {
            Assert.DoesNotContain(forbidden, ownerSource);
        }

        Assert.Contains("PrivilegedExecutionStateDescriptor", projectionSource);
        Assert.Contains("PrivilegedExecutionStateProjectionService", projectionSource);
        Assert.Contains("PrivilegedExecutionStateProjectionDenied", projectionSource);
        Assert.DoesNotContain("TryReadScalarField", projectionSource);
        Assert.DoesNotContain("ReadFieldValue(", projectionSource);
        Assert.DoesNotContain("WriteFieldValue(", projectionSource);
        Assert.DoesNotContain("VmcsManager", projectionSource);
        Assert.DoesNotContain("VmxExecutionUnit", projectionSource);
    }

    private static void AssertDenied(
        PrivilegedExecutionStateOwnerDecision expectedDecision,
        PrivilegedExecutionStateOwnerResult result)
    {
        Assert.Equal(expectedDecision, result.Decision);
        Assert.False(result.IsAllowed);
        Assert.False(result.OwnerAccepted);
        Assert.False(result.ReadOnlyProjectionAuthorized);
        Assert.False(result.MutationAuthorized);
        Assert.False(result.BackendExecutionAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    private static PrivilegedExecutionStateOwnerRequest Request(
        PrivilegedExecutionStateDescriptor? descriptor = null,
        ulong runtimeDomainTag = 7,
        ulong runtimeAddressSpaceTag = 9,
        ulong currentEpoch = 11) =>
        new(
            descriptor ?? Descriptor(),
            runtimeDomainTag,
            runtimeAddressSpaceTag,
            new PrivilegedExecutionStateEpoch(currentEpoch));

    private static PrivilegedExecutionStateDescriptor Descriptor() =>
        new(
            DomainTag: 7,
            AddressSpaceTag: 9,
            PolicyEpoch: new PrivilegedExecutionStateEpoch(11),
            Materialized: true,
            GuestCr0: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr0,
                0x1),
            GuestCr4: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr4,
                0x2),
            LegalityPolicy: new PrivilegedControlRegisterLegalityPolicy(
                GuestCr0AllowedMask: 0xFFFF,
                GuestCr0RequiredMask: 0x1,
                GuestCr4AllowedMask: 0xFFFF,
                GuestCr4RequiredMask: 0x2,
                Materialized: true),
            EvidenceClass: PrivilegedExecutionStateEvidenceClass.GuestVisibleReadOnlyProjection,
            MigrationClass: PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore);

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
