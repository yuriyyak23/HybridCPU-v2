using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class GuestCr0Cr4ReadOnlyProjectionTests
{
    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestCr0Cr4Projection_DeniedBeforeOwnerMaterialization(
        VmcsField field)
    {
        VmxCompatibilityVmReadAdmissionResult result = Project(
            field,
            Descriptor() with { Materialized = false });

        AssertDenied(
            PrivilegedExecutionStateProjectionDecision.DeniedOwnerAdmission,
            result);
        Assert.Equal(
            PrivilegedExecutionStateOwnerDecision.DeniedUnmaterializedDescriptor,
            result.ValueProjection.PrivilegedProjection.OwnerAdmission.Decision);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestCr0Cr4Projection_DeniedWithoutReadOnlySource(
        VmcsField field)
    {
        VmxCompatibilityVmReadAdmissionResult result = Project(
            field,
            descriptor: null);

        AssertDenied(
            PrivilegedExecutionStateProjectionDecision.DeniedNoReadOnlySource,
            result);
    }

    [Fact]
    public void GuestCr0Cr4Projection_DeniedWithoutVisibilityPolicy()
    {
        PrivilegedExecutionStateProjectionResult result =
            ProjectionService().Project(
                ProjectionRequest(
                    PrivilegedControlRegisterKind.GuestCr0,
                    secureVisibilityAllowed: false));

        Assert.False(result.IsAllowed);
        Assert.False(result.ValueAvailable);
        Assert.Equal(
            PrivilegedExecutionStateProjectionDecision.DeniedNoSecureVisibility,
            result.Decision);
    }

    [Fact]
    public void GuestCr0Cr4Projection_DeniedWithoutMigrationClass()
    {
        PrivilegedExecutionStateProjectionResult result =
            ProjectionService().Project(
                ProjectionRequest(
                    PrivilegedControlRegisterKind.GuestCr0,
                    descriptor: Descriptor() with
                    {
                        MigrationClass =
                            PrivilegedExecutionStateMigrationClass.Unclassified,
                    }));

        Assert.False(result.IsAllowed);
        Assert.False(result.ValueAvailable);
        Assert.Equal(
            PrivilegedExecutionStateProjectionDecision.DeniedNoMigrationClass,
            result.Decision);
        Assert.Equal(
            PrivilegedExecutionStateOwnerDecision.DeniedMigrationClass,
            result.OwnerAdmission.Decision);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestCr0Cr4Projection_DeniedWithoutConformanceProof(
        VmcsField field)
    {
        VmxCompatibilityVmReadAdmissionResult result = Project(
            field,
            Descriptor(),
            conformanceProven: false);

        AssertDenied(
            PrivilegedExecutionStateProjectionDecision.DeniedNoConformanceProof,
            result);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0, 0x80000011UL)]
    [InlineData(VmcsField.GuestCr4, 0x00000620UL)]
    public void GuestCr0Cr4Projection_AllowedReadOnlyAfterAllGates(
        VmcsField field,
        ulong expectedValue)
    {
        VmxCompatibilityVmReadAdmissionResult result = Project(
            field,
            Descriptor(),
            conformanceProven: true);

        Assert.Equal(
            VmxCompatibilityVmReadAdmissionDecision.ReadOnlyValueProjected,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.True(result.IsReadOnlyValueProjected);
        Assert.Equal(unchecked((long)expectedValue), result.Value);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision.ReadOnlyValueProjected,
            result.ValueProjection.Decision);
        Assert.Equal(
            PrivilegedExecutionStateProjectionDecision.AllowedReadOnlyProjection,
            result.ValueProjection.PrivilegedProjection.Decision);
        Assert.True(result.ValueProjection.PrivilegedProjection.IsAllowed);
        Assert.True(
            result.ValueProjection.PrivilegedProjection.OwnerAdmission.OwnerAccepted);
        Assert.False(
            result.ValueProjection.PrivilegedProjection
                .OwnerAdmission.ReadOnlyProjectionAuthorized);
        Assert.Contains(
            "neutral privileged execution-state owner",
            result.Reason);
    }

    [Fact]
    public void GuestCr0Cr4Projection_DoesNotAuthorizeBackendSuccessMutationOrPublication()
    {
        VmxCompatibilityVmReadAdmissionResult result = Project(
            VmcsField.GuestCr0,
            Descriptor(),
            conformanceProven: true);
        PrivilegedExecutionStateProjectionResult projection =
            result.ValueProjection.PrivilegedProjection;

        Assert.True(projection.IsAllowed);
        Assert.False(projection.BackendSuccessAuthorized);
        Assert.False(projection.MutationAuthorized);
        Assert.False(projection.CompletionPublicationAuthorized);
        Assert.False(projection.RetirePublicationAuthorized);
    }

    [Fact]
    public void GuestCr0Cr4Projection_UnsupportedPrivilegedFieldRemainsDenied()
    {
        SecureComputeCompatibilityMatrixResult result =
            new SecureComputeCompatibilityBoundaryMatrixPolicy().AdmitVmRead(
                new SecureComputeCompatibilityReadMatrixRequest(
                    FieldId: 0xFFFF,
                    FieldClass:
                        SecureComputeCompatibilityFieldClass.GuestPrivilegedControl,
                    SchemaOwner:
                        SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
                    ExpectedOwner:
                        SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
                    HasNeutralOwner: true,
                    HasReadOnlySource: true,
                    SecureVisibilityAllowed: true,
                    MigrationClassified: true,
                    ConformanceProven: true));

        Assert.False(result.IsAllowed);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision
                .DeniedUnsupportedGuestPrivilegedControlField,
            result.Decision);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void GuestCr0Cr4VmWrite_RemainsDenied(VmcsField field)
    {
        Assert.True(
            VmcsFieldProjectionSchema.TryGet(
                field,
                out VmcsFieldProjectionSchemaEntry entry));
        Assert.False(VmcsFieldProjectionSchema.CanWrite(entry));

        VmcsFieldAliasResult write = new VmcsFieldAliasProjection().ValidateAccess(
            new VmcsFieldAliasRequest(
                field,
                VmcsFieldAliasAccess.Write,
                entry.EvidenceClass,
                entry.IsGeneratedAlias,
                DescriptorValidated: true,
                AllowWrite: true),
            EvidencePolicy());

        Assert.Equal(VmcsFieldAliasDecision.WriteDenied, write.Decision);
        Assert.Null(typeof(VmcsV2Descriptor).GetMethod("TryWriteScalarField"));
    }

    [Fact]
    public void GuestCr0Cr4Projection_SourceGuardIsFieldSpecificAndProjectionOnly()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/PrivilegedExecutionStateProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");

        Assert.Contains("PrivilegedControlRegisterKind.GuestCr0", source);
        Assert.Contains("PrivilegedControlRegisterKind.GuestCr4", source);
        Assert.Contains("DeniedUnsupportedRegister", source);
        Assert.Contains("PrivilegedExecutionStateConformanceProven", source);
        Assert.Contains("BackendSuccessAuthorized: false", source);
        Assert.Contains("MutationAuthorized: false", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);

        foreach (string forbidden in new[]
        {
            "TryReadScalarField",
            "TryWriteScalarField",
            "ReadFieldValue(",
            "WriteFieldValue(",
            "VmcsManager",
            "IVmcsManager",
            "VmxExecutionUnit",
            "VmxCaps.Secure",
            "BackendSuccessAuthorized: true",
            "MutationAuthorized: true",
            "CompletionPublicationAuthorized: true",
            "RetirePublicationAuthorized: true",
        })
        {
            Assert.DoesNotContain(forbidden, source);
        }
    }

    private static void AssertDenied(
        PrivilegedExecutionStateProjectionDecision expectedDecision,
        VmxCompatibilityVmReadAdmissionResult result)
    {
        Assert.Equal(
            VmxCompatibilityVmReadAdmissionDecision.ReadOnlyProjectionDenied,
            result.Decision);
        Assert.True(result.RuntimeAdmissionAllowed);
        Assert.False(result.IsReadOnlyValueProjected);
        Assert.Equal(0, result.Value);
        Assert.Equal(
            VmcsReadOnlyValueProjectionDecision
                .PrivilegedExecutionStateProjectionDenied,
            result.ValueProjection.Decision);
        Assert.Equal(
            expectedDecision,
            result.ValueProjection.PrivilegedProjection.Decision);
        Assert.False(
            result.ValueProjection.PrivilegedProjection.ValueAvailable);
    }

    private static VmxCompatibilityVmReadAdmissionResult Project(
        VmcsField field,
        PrivilegedExecutionStateDescriptor? descriptor,
        bool conformanceProven = true) =>
        new VmxCompatibilityAdmissionService().AdmitVmReadProjection(
            new VmxCompatibilityVmReadAdmissionRequest(
                Context: Context(),
                RootAuthority: Root(),
                EvidencePolicy: EvidencePolicy(),
                Descriptor: null,
                FieldId: (ushort)field,
                DestinationRegister: 3,
                FieldSelectorRegister: 1,
                ReservedRegister: 0,
                DescriptorValidated: true,
                CapabilityValidated: true,
                SchedulingValidated: true,
                NoEmissionValidated: true,
                ProjectionEvidenceValidated: true,
                PrivilegedExecutionState: descriptor,
                CurrentPrivilegedExecutionStateEpoch:
                    new PrivilegedExecutionStateEpoch(11),
                PrivilegedExecutionStateConformanceProven: conformanceProven));

    private static PrivilegedExecutionStateProjectionService ProjectionService() =>
        new();

    private static PrivilegedExecutionStateProjectionRequest ProjectionRequest(
        PrivilegedControlRegisterKind register,
        PrivilegedExecutionStateDescriptor? descriptor = null,
        bool secureVisibilityAllowed = true,
        bool migrationClassified = true,
        bool conformanceProven = true) =>
        new(
            register,
            descriptor ?? Descriptor(),
            RuntimeDomainTag: 7,
            RuntimeAddressSpaceTag: 9,
            CurrentEpoch: new PrivilegedExecutionStateEpoch(11),
            secureVisibilityAllowed,
            migrationClassified,
            conformanceProven);

    private static PrivilegedExecutionStateDescriptor Descriptor() =>
        new(
            DomainTag: 7,
            AddressSpaceTag: 9,
            PolicyEpoch: new PrivilegedExecutionStateEpoch(11),
            Materialized: true,
            GuestCr0: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr0,
                0x80000011UL),
            GuestCr4: new PrivilegedControlRegisterValue(
                PrivilegedControlRegisterKind.GuestCr4,
                0x00000620UL),
            LegalityPolicy: new PrivilegedControlRegisterLegalityPolicy(
                GuestCr0AllowedMask: 0xFFFF_FFFFUL,
                GuestCr0RequiredMask: 0x1,
                GuestCr4AllowedMask: 0xFFFF_FFFFUL,
                GuestCr4RequiredMask: 0x20,
                Materialized: true),
            EvidenceClass:
                PrivilegedExecutionStateEvidenceClass
                    .GuestVisibleReadOnlyProjection,
            MigrationClass:
                PrivilegedExecutionStateMigrationClass.RevalidatedAfterRestore);

    private static DomainRuntimeContext Context() =>
        new(
            execution: new ExecutionDomainDescriptor(),
            memory: new MemoryDomainDescriptor(),
            io: new IoDomainDescriptor(),
            capabilities: new CapabilityDescriptorSet(
                globalHardwareCaps: 0,
                runtimeEnabledCaps: 0,
                domainGrantedCaps: 0),
            secureCompute: null,
            domainTag: 7,
            addressSpaceTag: 9);

    private static RootAuthorityDescriptor Root() =>
        new(
            RootAuthorityClass.RuntimeRoot,
            authorityEpoch: 1,
            grantedCapabilityMask: 0,
            allowCompatibilityFrontendActivation: true,
            allowAuthoritativeStateMutation: false);

    private static EvidencePolicyDescriptor EvidencePolicy() =>
        new(
            allowCompatibilityAliases: true,
            allowGuestArchitecturalState: true,
            allowMigrationSerializableState: false);

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

        throw new DirectoryNotFoundException(
            "Unable to locate HybridCPU ISE repository root.");
    }
}
