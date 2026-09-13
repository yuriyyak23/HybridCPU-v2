using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureComputeCommittedCleanSourceBaselineTests
{
    private static readonly string[] ExpectedDebugPolicySources =
    [
        "SecureDebugAttestationVisibilityPolicy.cs",
        "SecureDebugPolicy.cs",
    ];

    [Fact]
    public void DebugPolicySources_ArePresentUnderExactNarrowIgnoreExceptions()
    {
        string repositoryRoot = FindRepositoryRoot();
        string debugPolicyRoot = Path.Combine(
            repositoryRoot,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Runtime",
            "Domains",
            "SecureCompute",
            "Policies",
            "Debug");

        string[] actualSources = Directory
            .EnumerateFiles(debugPolicyRoot, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(ExpectedDebugPolicySources, actualSources);

        string[] ignoreLines = File.ReadAllLines(Path.Combine(repositoryRoot, ".gitignore"));
        Assert.Contains(
            "!HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Debug/",
            ignoreLines);
        Assert.Contains(
            "HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Debug/*",
            ignoreLines);

        foreach (string source in ExpectedDebugPolicySources)
        {
            Assert.Contains(
                $"!HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Debug/{source}",
                ignoreLines);
        }
    }

    [Fact]
    public void CommittedDebugPolicyBaseline_RemainsFailClosedAndCreatesNoAuthority()
    {
        Assert.Same(SecureDebugPolicy.Denied, SecureDebugPolicy.Denied);
        Assert.Equal(SecureDebugMode.Denied, SecureDebugPolicy.Denied.Mode);
        Assert.False(SecureDebugPolicy.Denied.AllowsDebug);
        Assert.False(SecureDebugPolicy.Denied.ChangesMeasurementClass);

        SecureDebugAttestationVisibilityResult result =
            SecureDebugAttestationVisibilityPolicy.FailClosed.Classify(
                new SecureDebugAttestationVisibilityRequest(
                    SecureDebugAttestationQueryKind.TelemetrySnapshot,
                    SecureComputeDomainDescriptor.Disabled,
                    Measurement: null,
                    NeutralEvidencePolicy: null));

        Assert.True(result.IsAllowed);
        Assert.True(result.HostOnly);
        Assert.False(result.CreatesAnyAuthority);
        Assert.False(result.CreatesRuntimeAuthority);
        Assert.False(result.CreatesVmreadAuthority);
        Assert.False(result.CreatesMigrationAuthority);
        Assert.False(result.CreatesActivationEvidence);
        Assert.False(result.CreatesBackendOwnerProof);
        Assert.False(result.CompletionPublicationAuthorized);
        Assert.False(result.RetirePublicationAuthorized);
    }

    [Fact]
    public void DebugVisibility_CannotBeReusedAsVmreadBackendCompletionOrRetireAuthority()
    {
        AssertDenied(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                SecureComputeDomainDescriptor.Disabled,
                null,
                null,
                RequestsVmreadValueSource: true),
            SecureDebugAttestationVisibilityDecision.DeniedVmreadAuthority);

        AssertDenied(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                SecureComputeDomainDescriptor.Disabled,
                null,
                null,
                RequestsBackendOwnerProof: true),
            SecureDebugAttestationVisibilityDecision.DeniedBackendOwnerProof);

        AssertDenied(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                SecureComputeDomainDescriptor.Disabled,
                null,
                null,
                RequestsCompletionPublication: true),
            SecureDebugAttestationVisibilityDecision.DeniedCompletionPublication);

        AssertDenied(
            new SecureDebugAttestationVisibilityRequest(
                SecureDebugAttestationQueryKind.TelemetrySnapshot,
                SecureComputeDomainDescriptor.Disabled,
                null,
                null,
                RequestsRetirePublication: true),
            SecureDebugAttestationVisibilityDecision.DeniedRetirePublication);
    }

    private static void AssertDenied(
        SecureDebugAttestationVisibilityRequest request,
        SecureDebugAttestationVisibilityDecision expectedDecision)
    {
        SecureDebugAttestationVisibilityResult result =
            SecureDebugAttestationVisibilityPolicy.FailClosed.Classify(request);

        Assert.Equal(expectedDecision, result.Decision);
        Assert.False(result.IsAllowed);
        Assert.False(result.CreatesAnyAuthority);
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
