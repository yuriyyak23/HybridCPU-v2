using YAKSys_Hybrid_CPU.Core;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public enum VmxCompatibilityProjectionInventoryKind : byte
{
    GeneratedLineage = 0,
    ContractOnly = 1,
    DeniedOnly = 2,
    ForbiddenAuthority = 3,
}

public readonly record struct VmxCompatibilityProjectionInventoryEntry(
    string RelativePath,
    VmxCompatibilityProjectionInventoryKind Kind,
    string[] RequiredMarkers);

public static class VmxCompatibilityProjectionInventoryContract
{
    private static readonly VmxCompatibilityProjectionInventoryEntry[] EntryTable =
    {
        new(
            "Core/VMX/Compatibility/Frontend/Projection/CapabilityCompatibilityProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "FromCompatibilityMasks", "CapabilityDescriptorSetSchema.VmxCompatibility" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "ProjectToVmx", "CompletionRecord" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "FromCompatibilityExit", "CompletionRecordClass.CompatibilityExit" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Events/TrapDecision.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "TrapRequest", "TrapDecision" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyService.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "TrapPolicyDescriptor", "RuntimeAuthorityMissing" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "NeutralTrapResultKind", "VmExitReason", "ForVmxOperation" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Lanes/Lane7CheckpointVmcsEvidenceProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "ContainsHostEvidence", "VmcsV2HostEvidenceKind" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Lanes/VectorStreamSnapshotVmcsEvidenceProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "ContainsHostEvidence", "VmcsV2HostEvidenceKind" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Memory/MemoryTranslationControl.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "IsReadOnlyCompatibilityProjection", "CompatibilityProjectionIdentity" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/ChildDomainIntentDescriptor.cs",
            VmxCompatibilityProjectionInventoryKind.DeniedOnly,
            new[] { "IsReadOnlyCompatibilityProjection", "neutral runtime-owned nested intent state", "DefaultNestedL1Visible" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedCompletionMapper.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "NestedCompletionMappingDecision", "RuntimeAuthorityRequired" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedDomainController.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "NestedEnablementProof", "NestedProofAuthoritySource" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "NestedExitMapping", "SecurityPolicyViolation" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedExitMapper.MemoryComposition.partial.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "FromNestedMemoryComposition", "NestedMemoryCompositionResult" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "NestedInterceptPolicy", "FirstReleaseNoLanePassthrough" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/Nested/NestedInterceptTranslator.Translate.partial.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "Translate", "L0 mandatory intercept dominates" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/VmxCompatProjectionService.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "ValidateProjection", "AuthoritativeMutationDenied" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/VmxCompletion.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "VmxCompletionKind", "VmFailCode" }),
        new(
            "Core/VMX/Compatibility/Frontend/Projection/VmxFrontendResultMapper.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "FromNestedValidation", "CompatibilityProjectionFailed" }),
        new(
            "Core/VMX/Compatibility/Generated/AliasMaps/CompatAliasMap.cs",
            VmxCompatibilityProjectionInventoryKind.GeneratedLineage,
            new[] { "CanonicalSchema", "TryGetEntry" }),
        new(
            "Core/VMX/Compatibility/Generated/CapabilityProjection/CapabilityDescriptorSetSchema.cs",
            VmxCompatibilityProjectionInventoryKind.GeneratedLineage,
            new[] { "VmxCompatibilityBitTable", "VmxCompatibilityBitSchema" }),
        new(
            "Core/VMX/Compatibility/Generated/CsrProjection/VmxCapabilityDescriptorSource.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "StaticVmxCapabilityDescriptorSource", "GetCapabilityDescriptorSet" }),
        new(
            "Core/VMX/Compatibility/Generated/CsrProjection/VmxCapsProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "Read(", "EvaluateWrite" }),
        new(
            "Core/VMX/Compatibility/Generated/SpecArtifacts/CompatSpecArtifactSet.cs",
            VmxCompatibilityProjectionInventoryKind.GeneratedLineage,
            new[] { "CompatSpecArtifactLineageKind", "ProjectionContractOnly" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/NestedDomainControllerCompatibilityProjection.cs",
            VmxCompatibilityProjectionInventoryKind.DeniedOnly,
            new[] { "new ShadowVmcsNestedProjectionService", "InvalidVmcs12" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs",
            VmxCompatibilityProjectionInventoryKind.DeniedOnly,
            new[] { "CompatibilityProjectionFailed", "return false" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldAliasProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "WriteDenied", "read-only" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs",
            VmxCompatibilityProjectionInventoryKind.GeneratedLineage,
            new[] { "CanonicalArtifact", "CanWrite" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Blocks.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "IsReadOnlyCompatibilityProjection", "RecordVectorException" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2DescriptorProjection.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "WritableProjectionDenied", "AuthoritativeMutationDenied" }),
        new(
            "Core/VMX/Compatibility/Generated/VmcsProjection/VmcsV2Header.cs",
            VmxCompatibilityProjectionInventoryKind.ContractOnly,
            new[] { "IsReadOnlyCompatibilityProjection", "IsLaunched => false", "InvalidationEpoch => 0" }),
    };

    public static IReadOnlyList<VmxCompatibilityProjectionInventoryEntry> Entries => EntryTable;

    public static IReadOnlyList<(string RemovedProjectionPath, string RuntimeOwnerPath, string[] RequiredMarkers)> ExtractedAuthorityCarriers { get; } =
        new (string RemovedProjectionPath, string RuntimeOwnerPath, string[] RequiredMarkers)[]
        {
            (
                "Core/VMX/Compatibility/Frontend/Projection/Events/SchedulingBudgetTimer.cs",
                "Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs",
                new[] { "public void Arm(", "public bool TryConsumeExpired", "AdvanceEpoch" }),
            (
                "Core/VMX/Compatibility/Frontend/Projection/Events/TrapPolicyBitmap.cs",
                "Core/Runtime/Events/Traps/TrapPolicyBitmap.cs",
                new[] { "HashSet<ushort>", "public void EnableInstruction", "AdvanceEpoch" }),
        };

    public static IReadOnlyList<string> ForbiddenAuthorityPaths { get; } =
        EntryTable
            .Where(entry => entry.Kind == VmxCompatibilityProjectionInventoryKind.ForbiddenAuthority)
            .Select(entry => entry.RelativePath)
            .ToArray();

    public static IReadOnlyList<string> ForbiddenRuntimeOwnerMarkers { get; } =
        new[]
        {
            "VmcsManager",
            "IVmcsManager",
            "VmxExecutionUnit",
            "VmcsManagerAdapter",
            "VmxRuntimeManager",
            "VmcsProjectionRuntimeManager",
            "VmcsV2RuntimeManager",
            "HardwareWrite(",
            "DirectWrite(",
            "ReadFieldValue(",
            "WriteFieldValue(",
            "new NestedProjectionService(",
            "new NestedDomainProjectionCheckpointService(",
            "IotlbInvalidationService",
            "DmaAuthorityService",
            "BindIoDomain(",
            "TryTranslateDma(",
            "PostedEventQueue",
            "DomainCheckpointImage",
            "RestoreValidationService",
        };

    public static IReadOnlyList<string> ForbiddenAuthorityMarkersForNonForbiddenEntries { get; } =
        new[]
        {
            "public void Arm(",
            "public void EnableInstruction",
            "public bool TryWriteIntentField(",
            "public static ChildDomainIntentDescriptor RestoreSnapshot(",
            "public void MarkLaunched(",
            "public void ResetLaunchState(",
            "public ulong AdvanceInvalidationEpoch(",
            "HashSet<ushort>",
            "Dictionary<ushort, long>",
        };
}

public sealed class VmxCompatibilityProjectionInventoryTests
{
    [Fact]
    public void Inventory_CoversEveryGeneratedAndFrontendProjectionSource()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string[] actual = EnumerateScopedProjectionSources(projectRoot);
        string[] expected = VmxCompatibilityProjectionInventoryContract.Entries
            .Select(entry => entry.RelativePath)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Inventory_ClassifiesLineageDeniedContractAndForbiddenAuthorityBuckets()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        Assert.Equal(
            GeneratedProjectionLineageBuildContract.RequiredGeneratedOutputs.OrderBy(static path => path),
            VmxCompatibilityProjectionInventoryContract.Entries
                .Where(static entry => entry.Kind == VmxCompatibilityProjectionInventoryKind.GeneratedLineage)
                .Select(static entry => entry.RelativePath)
                .OrderBy(static path => path));

        Assert.Equal(4, VmxCompatibilityProjectionInventoryContract.Entries.Count(
            static entry => entry.Kind == VmxCompatibilityProjectionInventoryKind.GeneratedLineage));
        Assert.Equal(23, VmxCompatibilityProjectionInventoryContract.Entries.Count(
            static entry => entry.Kind == VmxCompatibilityProjectionInventoryKind.ContractOnly));
        Assert.Equal(3, VmxCompatibilityProjectionInventoryContract.Entries.Count(
            static entry => entry.Kind == VmxCompatibilityProjectionInventoryKind.DeniedOnly));
        Assert.Empty(VmxCompatibilityProjectionInventoryContract.ForbiddenAuthorityPaths);

        foreach (VmxCompatibilityProjectionInventoryEntry entry in VmxCompatibilityProjectionInventoryContract.Entries)
        {
            string source = ReadProjectSource(projectRoot, entry.RelativePath);

            foreach (string marker in entry.RequiredMarkers)
            {
                Assert.Contains(marker, source);
            }
        }
    }

    [Fact]
    public void Inventory_ExtractsMutableTimerAndTrapBitmapStateOutOfProjectionScope()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        foreach ((string removedProjectionPath, string runtimeOwnerPath, string[] requiredMarkers) in
                 VmxCompatibilityProjectionInventoryContract.ExtractedAuthorityCarriers)
        {
            Assert.False(File.Exists(Path.Combine(
                projectRoot,
                removedProjectionPath.Replace('/', Path.DirectorySeparatorChar))));

            string runtimeSource = ReadProjectSource(projectRoot, runtimeOwnerPath);
            foreach (string marker in requiredMarkers)
            {
                Assert.Contains(marker, runtimeSource);
            }
        }
    }

    [Fact]
    public void Inventory_ForbidsRuntimeManagersStoresAndBackendAuthorityInProjectionScope()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");

        foreach (VmxCompatibilityProjectionInventoryEntry entry in VmxCompatibilityProjectionInventoryContract.Entries)
        {
            string source = ReadProjectSource(projectRoot, entry.RelativePath);

            foreach (string marker in VmxCompatibilityProjectionInventoryContract.ForbiddenRuntimeOwnerMarkers)
            {
                Assert.DoesNotContain(marker, source);
            }

            if (entry.Kind == VmxCompatibilityProjectionInventoryKind.ForbiddenAuthority)
            {
                continue;
            }

            foreach (string marker in VmxCompatibilityProjectionInventoryContract.ForbiddenAuthorityMarkersForNonForbiddenEntries)
            {
                Assert.DoesNotContain(marker, source);
            }
        }
    }

    private static string[] EnumerateScopedProjectionSources(string projectRoot)
    {
        string[] roots =
        {
            Path.Combine(projectRoot, "Core", "VMX", "Compatibility", "Generated"),
            Path.Combine(projectRoot, "Core", "VMX", "Compatibility", "Frontend", "Projection"),
        };

        return roots
            .SelectMany(static root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(projectRoot, path).Replace('\\', '/'))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadProjectSource(string projectRoot, string relativePath) =>
        File.ReadAllText(Path.Combine(
            projectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
