using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class SecureComputeVmxPhase17NamedPositivePathZeroAuthorityTests
{
    [Theory]
    [MemberData(nameof(NamedPaths))]
    public void NamedSecureComputePaths_RemainVmxZeroAuthority(
        SecureComputeNamedPositivePath path)
    {
        SecureComputeNamedPathVmxZeroAuthorityResult result =
            Policy.Classify(new SecureComputeNamedPathVmxZeroAuthorityRequest(path));

        Assert.True(result.IsAllowed);
        Assert.False(result.CompatibilityProjectionAllowed);
        Assert.False(result.CreatesAnyVmxAuthority);
        Assert.False(result.VmxActivationAuthorized);
        Assert.False(result.VmxCapsAuthorityAuthorized);
        Assert.False(result.VmcsStateStoreAuthorized);
        Assert.False(result.ActiveVmcsPointerIdentityAuthorized);
        Assert.False(result.VmreadSecureStateAuthorityAuthorized);
        Assert.False(result.VmwriteSecureStateMutationAuthorized);
        Assert.False(result.VmcsCheckpointAuthorityAuthorized);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void CompatibilityProjection_RequiresNeutralRuntimeResultAndRemainsZeroAuthority()
    {
        SecureComputeNamedPathVmxZeroAuthorityResult missingNeutral =
            Policy.Classify(new SecureComputeNamedPathVmxZeroAuthorityRequest(
                SecureComputeNamedPositivePath.FutureRestrictedRuntimeExecution,
                RequestsCompatibilityProjection: true));

        Assert.Equal(
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedProjectionWithoutNeutralResult,
            missingNeutral.Decision);
        Assert.False(missingNeutral.CompatibilityProjectionAllowed);
        Assert.False(missingNeutral.CreatesAnyVmxAuthority);

        SecureComputeNamedPathVmxZeroAuthorityResult afterNeutral =
            Policy.Classify(new SecureComputeNamedPathVmxZeroAuthorityRequest(
                SecureComputeNamedPositivePath.FutureRestrictedRuntimeExecution,
                HasNeutralRuntimeResult: true,
                RequestsCompatibilityProjection: true));

        Assert.True(afterNeutral.IsAllowed);
        Assert.True(afterNeutral.CompatibilityProjectionAllowed);
        Assert.False(afterNeutral.CreatesAnyVmxAuthority);
        Assert.False(afterNeutral.CompletionPublicationAuthorized);
        Assert.False(afterNeutral.RetirePublicationAuthorized);
    }

    [Theory]
    [MemberData(nameof(AuthorityShortcutCases))]
    public void VmxAuthorityShortcutRequests_AreDenied(
        SecureComputeNamedPathVmxZeroAuthorityRequest request,
        SecureComputeNamedPathVmxZeroAuthorityDecision expectedDecision)
    {
        SecureComputeNamedPathVmxZeroAuthorityResult result =
            Policy.Classify(request);

        Assert.Equal(expectedDecision, result.Decision);
        Assert.False(result.IsAllowed);
        Assert.False(result.CompatibilityProjectionAllowed);
        Assert.False(result.CreatesAnyVmxAuthority);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void ExistingBoundaryMatrix_StillKeepsProjectionAndBackendSeparated()
    {
        var boundary = new SecureComputeVmxAuthorityBoundaryContract();
        var matrix = new SecureComputeCompatibilityBoundaryMatrixPolicy();

        Assert.Equal(
            SecureComputeAuthorityBoundaryViolation.VmxActivation,
            boundary.Validate(
                vmxActivatesSecureCompute: true,
                vmxCapsGrantsSecureCompute: false,
                vmcsStoresSecureState: false,
                activeVmcsPointerIsDomainIdentity: false));
        Assert.Equal(
            SecureComputeAuthorityBoundaryViolation.VmxCapsAuthority,
            boundary.Validate(
                vmxActivatesSecureCompute: false,
                vmxCapsGrantsSecureCompute: true,
                vmcsStoresSecureState: false,
                activeVmcsPointerIsDomainIdentity: false));
        Assert.Equal(
            SecureComputeCompatibilityMatrixDecision.DeniedBackendSuccess,
            matrix.AdmitProjectionCompletion(attemptsBackendSuccess: true).Decision);
        Assert.False(matrix.AdmitProjectionCompletion(attemptsBackendSuccess: true).BackendSuccessAuthorized);
    }

    [Fact]
    public void VmxZeroAuthoritySources_DoNotCreateRuntimeBackendPublicationOrCompilerAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Conformance/AuthorityBoundary/SecureComputeNamedPathVmxZeroAuthorityPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Conformance/AuthorityBoundary/SecureComputeVmxAuthorityBoundaryContract.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeCompatibilityBoundaryMatrixPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmReadVisibilityPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmWriteDenyPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmcsProjectionFence.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Virtualization/SecureCompute/Compatibility/Projection/SecureComputeVmxCapsProjectionFence.cs");

        Assert.Contains("DeniedVmxActivation", source);
        Assert.Contains("DeniedVmxCapsAuthority", source);
        Assert.Contains("DeniedVmcsStateStore", source);
        Assert.Contains("DeniedVmreadSecureStateAuthority", source);
        Assert.Contains("DeniedVmwriteSecureStateMutation", source);
        Assert.Contains("DeniedVmcsCheckpointAuthority", source);
        Assert.Contains("DeniedProjectionWithoutNeutralResult", source);
        Assert.Contains("VmxActivationAuthorized: false", source);
        Assert.Contains("VmxCapsAuthorityAuthorized: false", source);
        Assert.Contains("VmcsStateStoreAuthorized: false", source);
        Assert.Contains("VmreadSecureStateAuthorityAuthorized: false", source);
        Assert.Contains("VmwriteSecureStateMutationAuthorized: false", source);
        Assert.Contains("VmcsCheckpointAuthorityAuthorized: false", source);
        Assert.Contains("CompletionPublicationAuthorized: false", source);
        Assert.Contains("RetirePublicationAuthorized: false", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("VmxCaps.Secure", source);
        Assert.DoesNotContain("new SecureComputeDomainDescriptor", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("SecureComputeControlledEmission", source);
    }

    public static IEnumerable<object[]> NamedPaths()
    {
        foreach (SecureComputeNamedPositivePath path in Enum.GetValues<SecureComputeNamedPositivePath>())
        {
            yield return new object[] { path };
        }
    }

    public static IEnumerable<object[]> AuthorityShortcutCases()
    {
        SecureComputeNamedPositivePath path =
            SecureComputeNamedPositivePath.FutureRestrictedRuntimeExecution;

        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmxActivation: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmxActivation,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmxCapsGrant: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmxCapsAuthority,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmcsStateStore: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmcsStateStore,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsActiveVmcsPointerIdentity: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedActiveVmcsPointerIdentity,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmreadSecureStateAuthority: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmreadSecureStateAuthority,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmwriteSecureStateMutation: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmwriteSecureStateMutation,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsVmcsCheckpointAuthority: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedVmcsCheckpointAuthority,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsCompletionPublication: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedCompletionPublication,
        };
        yield return new object[]
        {
            new SecureComputeNamedPathVmxZeroAuthorityRequest(path, AttemptsRetirePublication: true),
            SecureComputeNamedPathVmxZeroAuthorityDecision.DeniedRetirePublication,
        };
    }

    private static SecureComputeNamedPathVmxZeroAuthorityPolicy Policy =>
        SecureComputeNamedPathVmxZeroAuthorityPolicy.FailClosed;

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
