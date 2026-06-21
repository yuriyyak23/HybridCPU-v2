using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.SecureComputeRefactoring;

public sealed class SecureMigrationCheckpointRestoreOutputManifestPhase15Tests
{
    [Theory]
    [InlineData(
        SecureCheckpointPayloadClass.HostOwnedEvidence,
        SecureOutputManifestClassificationDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.SchedulerEvidence,
        SecureOutputManifestClassificationDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.BackendBindingEvidence,
        SecureOutputManifestClassificationDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.NativeTokenEvidence,
        SecureOutputManifestClassificationDecision.DeniedHostOwnedEvidence)]
    [InlineData(
        SecureCheckpointPayloadClass.VmcsProjectionMetadata,
        SecureOutputManifestClassificationDecision.DeniedVmcsProjectionAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.CompatibilityProjectionMetadata,
        SecureOutputManifestClassificationDecision.DeniedCompatibilityMetadataAuthority)]
    [InlineData(
        SecureCheckpointPayloadClass.RawMeasurementSecret,
        SecureOutputManifestClassificationDecision.DeniedRawSecret)]
    [InlineData(
        SecureCheckpointPayloadClass.RawSealingKey,
        SecureOutputManifestClassificationDecision.DeniedRawSecret)]
    [InlineData(
        SecureCheckpointPayloadClass.ActiveHostPointer,
        SecureOutputManifestClassificationDecision.DeniedActiveHostPointer)]
    public void OutputManifestClassification_RejectsForbiddenAuthorityPayloadClasses(
        SecureCheckpointPayloadClass payloadClass,
        SecureOutputManifestClassificationDecision expectedDecision)
    {
        SecureOutputManifestClassificationResult result =
            Policy.ClassifyEntry(new SecureOutputManifestEntry(
                SecureOutputManifestEntryKind.GuestVisibleOutput,
                payloadClass,
                OwnerPathClassified: true,
                RestoreValidationProven: true));

        Assert.Equal(expectedDecision, result.Decision);
        AssertDeniedNoAuthority(result);
    }

    [Fact]
    public void InternalBackendResultAndCompletionRecord_AreManifestOnlyNotCheckpointOrRestoreAuthority()
    {
        SecureOutputManifestClassificationResult backendResult =
            Policy.ClassifyEntry(SecureOutputManifestEntry.InternalBackendResult());
        SecureOutputManifestClassificationResult completionRecord =
            Policy.ClassifyEntry(SecureOutputManifestEntry.InternalCompletionRecord());

        AssertManifestOnly(backendResult);
        AssertManifestOnly(completionRecord);
        Assert.False(backendResult.CheckpointPayloadIncluded);
        Assert.False(completionRecord.CheckpointPayloadIncluded);
        Assert.Equal(
            SecureOutputManifestEntryKind.InternalCompletionRecord,
            completionRecord.EntryKind);
    }

    [Fact]
    public void OutputManifestCoverage_MissingRequiredEntryBlocksFuturePositivePathEvidence()
    {
        SecureOutputManifestEntry[] missingRecomputed =
        {
            SecureOutputManifestEntry.RequestState(),
            SecureOutputManifestEntry.InternalBackendResult(),
            SecureOutputManifestEntry.InternalCompletionRecord(),
            SecureOutputManifestEntry.GuestVisibleOutput(),
            SecureOutputManifestEntry.RetireVisibleState(),
        };

        SecureOutputManifestClassificationResult result =
            Policy.ClassifyManifest(missingRecomputed);

        Assert.Equal(
            SecureOutputManifestClassificationDecision.DeniedMissingManifestEntry,
            result.Decision);
        Assert.Equal(
            SecureOutputManifestEntryKind.RecomputedAfterRestoreState,
            result.EntryKind);
        AssertDeniedNoAuthority(result);
    }

    [Fact]
    public void CompleteOutputManifest_IsClassificationEvidenceOnlyAndCreatesNoPublicationAuthority()
    {
        SecureOutputManifestClassificationResult result =
            Policy.ClassifyManifest(CompleteManifest());

        Assert.Equal(
            SecureOutputManifestClassificationDecision.CompleteManifestClassified,
            result.Decision);
        Assert.True(result.ManifestClassified);
        Assert.False(result.CheckpointPayloadIncluded);
        Assert.True(result.RestoreRevalidationRequired);
        AssertNoRuntimePublicationAuthority(result);
    }

    [Fact]
    public void RecomputedAfterRestoreState_RequiresRestoreValidationProof()
    {
        SecureOutputManifestClassificationResult result =
            Policy.ClassifyEntry(new SecureOutputManifestEntry(
                SecureOutputManifestEntryKind.RecomputedAfterRestoreState,
                SecureCheckpointPayloadClass.SecurePolicyDescriptor,
                OwnerPathClassified: true,
                RestoreValidationProven: false));

        Assert.Equal(
            SecureOutputManifestClassificationDecision.DeniedRecomputedStateRestoreProof,
            result.Decision);
        AssertDeniedNoAuthority(result);
    }

    [Fact]
    public void OwnerPathReachabilityClassification_IsRequiredBeforeManifestEntryIsAccepted()
    {
        SecureOutputManifestClassificationResult result =
            Policy.ClassifyEntry(new SecureOutputManifestEntry(
                SecureOutputManifestEntryKind.GuestVisibleOutput,
                SecureCheckpointPayloadClass.GuestVisibleState,
                OwnerPathClassified: false,
                RestoreValidationProven: true));

        Assert.Equal(
            SecureOutputManifestClassificationDecision.DeniedOwnerPathUnclassified,
            result.Decision);
        AssertDeniedNoAuthority(result);
    }

    [Fact]
    public void SecureOutputManifestSources_DoNotCreateVmxVmreadPublicationBackendOrCompilerAuthority()
    {
        string source = ReadProjectSource(
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureOutputManifestClassificationPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Checkpoint/SecureCheckpointPayloadPolicy.cs",
            "HybridCPU_ISE/CloseToRTL/Core/Runtime/Domains/SecureCompute/Policies/Migration/SecureMigrationAdmissionPolicy.cs");

        Assert.Contains("DeniedHostOwnedEvidence", source);
        Assert.Contains("DeniedVmcsProjectionAuthority", source);
        Assert.Contains("DeniedCompatibilityMetadataAuthority", source);
        Assert.Contains("DeniedRawSecret", source);
        Assert.Contains("DeniedActiveHostPointer", source);
        Assert.Contains("InternalCompletionRecord", source);
        Assert.Contains("RecomputedAfterRestoreState", source);
        Assert.Contains("CreatesRuntimeAuthority: false", source);
        Assert.Contains("CreatesCompletionPublicationAuthority: false", source);
        Assert.Contains("CreatesRetirePublicationAuthority: false", source);

        Assert.DoesNotContain("VmcsField", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("IVmcsManager", source);
        Assert.DoesNotContain("VmxCaps", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VMREAD", source);
        Assert.DoesNotContain("VMWRITE", source);
        Assert.DoesNotContain("ReadFieldValue(", source);
        Assert.DoesNotContain("WriteFieldValue(", source);
        Assert.DoesNotContain("BackendExecutionAuthorized: true", source);
        Assert.DoesNotContain("CompletionPublicationAuthorized: true", source);
        Assert.DoesNotContain("RetirePublicationAuthorized: true", source);
        Assert.DoesNotContain("SecureBackendExecutionRequest", source);
        Assert.DoesNotContain("SecureBackendExecutionDecision", source);
        Assert.DoesNotContain("TrapCompletionRouteDescriptor", source);
        Assert.DoesNotContain("TrapCompletionRouteService", source);
        Assert.DoesNotContain("SecureComputeControlledEmission", source);
    }

    private static SecureOutputManifestClassificationPolicy Policy =>
        SecureOutputManifestClassificationPolicy.FailClosed;

    private static SecureOutputManifestEntry[] CompleteManifest() =>
        new[]
        {
            SecureOutputManifestEntry.RequestState(),
            SecureOutputManifestEntry.InternalBackendResult(),
            SecureOutputManifestEntry.InternalCompletionRecord(),
            SecureOutputManifestEntry.GuestVisibleOutput(),
            SecureOutputManifestEntry.RetireVisibleState(),
            SecureOutputManifestEntry.RecomputedAfterRestoreState(),
        };

    private static void AssertManifestOnly(SecureOutputManifestClassificationResult result)
    {
        Assert.Equal(
            SecureOutputManifestClassificationDecision.EntryClassifiedForManifestOnly,
            result.Decision);
        Assert.True(result.ManifestClassified);
        Assert.True(result.RestoreRevalidationRequired);
        AssertNoRuntimePublicationAuthority(result);
    }

    private static void AssertDeniedNoAuthority(SecureOutputManifestClassificationResult result)
    {
        Assert.True(result.IsDenied);
        Assert.False(result.ManifestClassified);
        Assert.False(result.CheckpointPayloadIncluded);
        AssertNoRuntimePublicationAuthority(result);
    }

    private static void AssertNoRuntimePublicationAuthority(
        SecureOutputManifestClassificationResult result)
    {
        Assert.False(result.CreatesRuntimeAuthority);
        Assert.False(result.CreatesCompletionPublicationAuthority);
        Assert.False(result.CreatesRetirePublicationAuthority);
        Assert.False(result.CreatesAnyRuntimeOrPublicationAuthority);
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
