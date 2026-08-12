using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase50CurrentCompletionProducerCoveragePrerequisiteTests
{
    [Fact]
    public void Prerequisite_BlocksWithoutCanonicalCpuTranslationFaultProducer()
    {
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (byte)value),
            Phase50CurrentCompletionProducerCoveragePrerequisiteContract.Findings.Select(item => item.Number));
        Assert.Equal(4, Phase50CurrentCompletionProducerCoveragePrerequisiteContract.Contours.Length);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.ExistingProducerCoverageComplete);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.CpuTranslationFaultProducerExists);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.CpuRetireReachabilityExists);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.OwnerApprovedReasonMappingExists);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.ExpansionAuthorized);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.D2MayOpen);
        Assert.False(Phase50CurrentCompletionProducerCoveragePrerequisiteContract.RuntimeAuthorityGranted);
    }

    [Fact]
    public void TranslationFacts_AreNotReachableFromCanonicalCpuRetireCompletion()
    {
        string state = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string retire = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");
        string pipeline = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/PipelineEvent.cs",
            "CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.IO.cs");

        Assert.Equal(1, Count(state, ".RegisterProducer("));
        Assert.Contains("CanonicalPipelineTrapEntryProducer", state);
        Assert.DoesNotContain("NestedTranslationResult", retire);
        Assert.DoesNotContain("TranslationViolationInfo", retire);
        Assert.DoesNotContain("GuestPhysicalAddress", retire);
        Assert.DoesNotContain("SecondStageTranslationViolation", retire);
        Assert.DoesNotContain("QualificationBits", pipeline);
    }

    [Fact]
    public void ExistingTypedTranslationFacts_RemainUncalledOrCompatibilityOnly()
    {
        string iommu = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Memory/MMU/IOMMU.DomainBinding.cs");
        string translation = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Memory/Translation/TranslationViolationInfo.cs",
            "CloseToHSL/Core/Runtime/Memory/Translation/NestedTranslationResult.cs");
        string mapper = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Nested/NestedExitMapper.MemoryComposition.partial.cs");

        Assert.Contains("record struct NestedTranslationResult", translation);
        Assert.Contains("ulong QualificationBits", translation);
        Assert.Contains("out NestedTranslationResult translation", iommu);
        Assert.Contains("result.Translation.Violation.QualificationBits", mapper);

        string productionCallers = ReadCloseToHslExcept(
            "Memory/MMU/IOMMU.DomainBinding.cs",
            "Core/Runtime/Memory/Translation/TranslationViolationInfo.cs",
            "Core/Runtime/Memory/Translation/NestedTranslationResult.cs",
            "Core/Runtime/Memory/Translation/NestedPageWalker.Translate.partial.cs",
            "Core/Runtime/Memory/Translation/NestedPageWalker.cs",
            "Core/Runtime/Nested/MemoryComposition/NestedMemoryCompositionService.cs",
            "Core/Virtualization/Compatibility/Frontend/Projection/Nested/NestedExitMapper.MemoryComposition.partial.cs");
        Assert.DoesNotContain("TranslateGuestAccess(", productionCallers);
        Assert.DoesNotContain("NestedMemoryCompositionService", productionCallers);
        Assert.DoesNotContain("QualificationBits", productionCallers);
    }

    [Fact]
    public void MachineStatus_ClosesOnlyPrerequisiteAuditAndKeepsD2Denied()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json")));
        JsonElement root = status.RootElement;
        JsonElement phase50 = root.GetProperty("Phase50CurrentCompletionProducerCoveragePrerequisite");
        JsonElement candidate = root.GetProperty("VmReadCurrentCompletionScalarDeliveryCandidate");

        Assert.Equal("BlockedNoCanonicalCpuTranslationFaultCompletionProducer", phase50.GetProperty("State").GetString());
        Assert.Equal("NotMaterialized", candidate.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized", candidate.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized", candidate.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    private static string ReadCloseToHslExcept(params string[] excludedSuffixes)
    {
        string root = Path.Combine(ActiveVmxConformanceHelpers.FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL");
        return string.Concat(Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !excludedSuffixes.Any(suffix => path.Replace('\\', '/').EndsWith(suffix, StringComparison.Ordinal)))
            .Where(path => !path.Replace('\\', '/').Contains("/Governance/", StringComparison.Ordinal))
            .Where(path => !path.Replace('\\', '/').Contains("/Conformance/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }

    private static int Count(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
