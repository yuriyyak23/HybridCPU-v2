using System.Text.Json;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase49CurrentCompletionVmReadScalarDeliveryE0Tests
{
    [Fact]
    public void E0_BlocksExactGroupOnIncompleteProducerFieldCoverage()
    {
        Assert.Equal(Enumerable.Range(1, 16).Select(value => (byte)value),
            Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.Findings.Select(item => item.Number));
        Assert.Equal(4, Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.FieldMatrix.Length);
        Assert.All(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.FieldMatrix,
            entry => Assert.False(entry.IsD2Eligible));
        Assert.True(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.CanonicalCommitPointProven);
        Assert.True(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.NeutralObservationOwnerProven);
        Assert.True(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.ExplicitPresenceAndSemanticContractProven);
        Assert.True(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.ExactProductionProducerRegistered);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.ExactFourFieldCoverageProven);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.OwnerApprovedProjectionMappingProven);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.SpecV2Materialized);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.AcceptanceRecordV2Materialized);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase49CurrentCompletionVmReadScalarDeliveryE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void ProductionRegistration_ProvesOnlyTrapReasonAndDeniesOtherSemantics()
    {
        string state = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string retire = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");

        Assert.Equal(1, Count(state, ".RegisterProducer("));
        Assert.Contains("\"CanonicalPipelineTrapEntryProducer\"", state);
        Assert.Contains("NeutralArchitecturalCompletionClass.TrapEntry", state);
        Assert.Contains("RequiresReason: true", state);
        Assert.Contains("AllowsQualification: false", state);
        Assert.Contains("NeutralFaultAddressSemantic.VirtualAddress", state);
        Assert.Contains("NeutralFaultAuxiliarySemantic.None", state);
        Assert.Contains("NeutralScalarFact.Present(trapEntry.CauseCode)", retire);
        Assert.Contains("Core.NeutralScalarFact.Absent", retire);
        Assert.Contains("Core.NeutralAuxiliaryFact.Absent", retire);
        Assert.DoesNotContain("NeutralFaultAddressSemantic.GuestPhysicalAddress", retire);
        Assert.DoesNotContain("NeutralFaultAuxiliarySemantic.SecondStageTranslationViolation", retire);
    }

    [Fact]
    public void MachineStatus_RecordsBlockedE0WithoutOpeningD2()
    {
        string repositoryRoot = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json")));
        JsonElement root = status.RootElement;
        JsonElement phase49 = root.GetProperty("Phase49CurrentCompletionVmReadScalarDeliveryE0");
        JsonElement candidate = root.GetProperty("VmReadCurrentCompletionScalarDeliveryCandidate");

        Assert.Equal("BlockedE0IncompleteExactProducerFieldSemanticCoverage", phase49.GetProperty("State").GetString());
        Assert.Equal("NotMaterialized", candidate.GetProperty("SpecV2").GetString());
        Assert.Equal("NotMaterialized", candidate.GetProperty("AcceptanceRecordV2").GetString());
        Assert.Equal("NotAuthorized", candidate.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            root.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void BlockedE0_CreatesNoVmReadReceiptOrProductionReachability()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/VmRead/VmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Runtime/Events/VmRead/MemoryOwnedVmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Runtime/Events/VmRead/GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");
        Assert.DoesNotContain("DomainCompletionObservationOwner", source);
        Assert.DoesNotContain("ArchitecturalCompletionCommitReceipt", source);
        Assert.DoesNotContain("CurrentCompletionVmReadScalarResultReceipt", source);
        Assert.DoesNotContain("Phase49CurrentCompletionVmReadScalarDeliveryDecisionSpecV2", source);
        Assert.DoesNotContain("Phase49CurrentCompletionVmReadScalarDeliveryDecisionAcceptanceV2", source);
    }

    private static int Count(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) /
        value.Length;
}
