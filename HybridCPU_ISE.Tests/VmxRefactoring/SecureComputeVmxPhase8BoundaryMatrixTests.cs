using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class SecureComputeVmxPhase8BoundaryMatrixTests
{
    [Theory]
    [InlineData(
        VmcsField.HostCr3,
        SecureComputeCompatibilityFieldClass.HostAddressSpaceAlias,
        SecureComputeCompatibilityMatrixDecision.DeniedHostAddressSpaceOwnerMissing)]
    [InlineData(
        VmcsField.HostPc,
        SecureComputeCompatibilityFieldClass.HostExecutionAlias,
        SecureComputeCompatibilityMatrixDecision.DeniedHostExecutionOwnerMissing)]
    [InlineData(
        VmcsField.PinBasedControls,
        SecureComputeCompatibilityFieldClass.CompatibilityControl,
        SecureComputeCompatibilityMatrixDecision.DeniedCompatibilityControlContractMissing)]
    public void SecureComputeVmxReadMatrix_DeniesSecureSensitiveCompatibilityFields(
        VmcsField field,
        SecureComputeCompatibilityFieldClass fieldClass,
        SecureComputeCompatibilityMatrixDecision expectedDecision)
    {
        SecureComputeCompatibilityMatrixResult result =
            new SecureComputeCompatibilityBoundaryMatrixPolicy().AdmitVmRead(
                CreateReadRequest(field, fieldClass));

        Assert.False(result.IsAllowed);
        Assert.False(result.ValueAvailable);
        Assert.False(result.BackendSuccessAuthorized);
        Assert.Equal(expectedDecision, result.Decision);
    }

    [Theory]
    [InlineData(VmcsField.GuestCr0)]
    [InlineData(VmcsField.GuestCr4)]
    public void SecureComputeVmxReadMatrix_AllowsOnlyPhase10GuestControlFields(
        VmcsField field)
    {
        SecureComputeCompatibilityMatrixResult result =
            new SecureComputeCompatibilityBoundaryMatrixPolicy().AdmitVmRead(
                CreateReadRequest(
                    field,
                    SecureComputeCompatibilityFieldClass.GuestPrivilegedControl));

        Assert.True(result.IsAllowed);
        Assert.True(result.ValueAvailable);
        Assert.False(result.BackendSuccessAuthorized);
        Assert.False(result.MutationAuthorized);
    }

    [Fact]
    public void SecureComputeVmxReadMatrix_DeniesSecureEvidenceDebugMigrationFieldsByDefault()
    {
        SecureComputeCompatibilityMatrixResult result =
            new SecureComputeCompatibilityBoundaryMatrixPolicy().AdmitVmRead(
                CreateReadRequest(
                    field: (VmcsField)0x5EC0,
                    SecureComputeCompatibilityFieldClass.SecureMeasurementEvidenceDebugMigration,
                    owner: SecureComputeProjectionOwnerKind.SecureEvidencePolicy));

        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedSecureSensitiveField,
            result.Decision);
        Assert.False(result.ValueAvailable);
    }

    [Fact]
    public void SecureComputeVmxReadMatrix_RequiresCompleteNeutralProofChain()
    {
        var policy = new SecureComputeCompatibilityBoundaryMatrixPolicy();

        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoNeutralOwner,
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc, hasNeutralOwner: false)).Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoReadOnlySource,
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc, hasReadOnlySource: false)).Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoSecureVisibility,
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc, secureVisibilityAllowed: false)).Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoMigrationClass,
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc, migrationClassified: false)).Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedNoConformanceProof,
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc, conformanceProven: false)).Decision);

        SecureComputeCompatibilityMatrixResult allowed =
            policy.AdmitVmRead(CreateReadRequest(VmcsField.GuestPc));

        Assert.True(allowed.IsAllowed);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.AllowedReadOnlyProjection,
            allowed.Decision);
        Assert.False(allowed.BackendSuccessAuthorized);
    }

    [Fact]
    public void SecureComputeVmxReadMatrix_DeniesGeneratedSchemaOwnerMismatch()
    {
        SecureComputeCompatibilityMatrixResult result =
            new SecureComputeCompatibilityBoundaryMatrixPolicy().AdmitVmRead(
                CreateReadRequest(
                    VmcsField.GuestPc,
                    owner: SecureComputeProjectionOwnerKind.SecureMemoryDescriptor,
                    expectedOwner: SecureComputeProjectionOwnerKind.SecureEvidencePolicy));

        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedSchemaOwnerMismatch,
            result.Decision);
        Assert.False(result.ValueAvailable);
    }

    [Fact]
    public void SecureComputeVmxWriteVmxCapsCheckpointAndBackendProjectionAreDenied()
    {
        var policy = new SecureComputeCompatibilityBoundaryMatrixPolicy();

        SecureComputeCompatibilityMatrixResult write =
            policy.AdmitVmWrite(secureSensitiveField: true);
        SecureComputeCompatibilityMatrixResult caps =
            policy.AdmitVmxCapsDescriptorMaterialization(
                attemptsSecureDescriptorMaterialization: true);
        SecureComputeCompatibilityMatrixResult checkpoint =
            policy.AdmitVmcsCheckpointAuthority(containsSecureMetadata: true);
        SecureComputeCompatibilityMatrixResult backend =
            policy.AdmitProjectionCompletion(attemptsBackendSuccess: true);

        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedWriteMutation, write.Decision);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedVmxCapsAuthority, caps.Decision);
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedVmcsCheckpointAuthority,
            checkpoint.Decision);
        Assert.Equal(SecureComputeCompatibilityMatrixDecision.DeniedBackendSuccess, backend.Decision);
        Assert.False(write.MutationAuthorized);
        Assert.False(caps.BackendSuccessAuthorized);
        Assert.False(checkpoint.BackendSuccessAuthorized);
        Assert.False(backend.BackendSuccessAuthorized);
    }

    [Fact]
    public void SecureComputeVmxPhase8Sources_DoNotIntroduceVmxAuthorityBackend()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityProjectionService.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmReadVisibilityPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmWriteDenyPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmcsProjectionFence.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmxCapsProjectionFence.cs");

        Assert.Contains("DeniedVmxCapsAuthority", source);
        Assert.Contains("DeniedVmcsCheckpointAuthority", source);
        Assert.Contains("DeniedBackendSuccess", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("VmxCaps.Secure", source);
        Assert.DoesNotContain("new SecureComputeDomainDescriptor", source);
        Assert.DoesNotContain("AllowBackendExecution = true", source);
    }

    private static SecureComputeCompatibilityReadMatrixRequest CreateReadRequest(
        VmcsField field,
        SecureComputeCompatibilityFieldClass fieldClass =
            SecureComputeCompatibilityFieldClass.OrdinaryReadOnlyProjection,
        SecureComputeProjectionOwnerKind owner =
            SecureComputeProjectionOwnerKind.SecureCompatibilityProjectionPolicy,
        SecureComputeProjectionOwnerKind? expectedOwner = null,
        bool hasNeutralOwner = true,
        bool hasReadOnlySource = true,
        bool secureVisibilityAllowed = true,
        bool migrationClassified = true,
        bool conformanceProven = true) =>
        new(
            FieldId: (ulong)field,
            FieldClass: fieldClass,
            SchemaOwner: owner,
            ExpectedOwner: expectedOwner ?? owner,
            HasNeutralOwner: hasNeutralOwner,
            HasReadOnlySource: hasReadOnlySource,
            SecureVisibilityAllowed: secureVisibilityAllowed,
            MigrationClassified: migrationClassified,
            ConformanceProven: conformanceProven);

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
