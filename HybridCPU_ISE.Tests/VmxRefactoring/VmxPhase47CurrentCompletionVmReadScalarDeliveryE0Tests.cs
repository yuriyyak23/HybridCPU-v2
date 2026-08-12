using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase47CurrentCompletionVmReadScalarDeliveryE0Tests
{
    [Fact]
    public void E0_BlocksWithoutCanonicalCommitEvidenceOrFieldValidity()
    {
        Assert.Equal(Enumerable.Range(1, 20).Select(value => (byte)value),
            Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.Findings.Select(item => item.Number));
        Assert.True(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ExactFieldIds.SequenceEqual(
            [(ushort)VmcsField.ExitReason, (ushort)VmcsField.ExitQualification,
             (ushort)VmcsField.GuestPhysicalAddress, (ushort)VmcsField.EptViolationQualification]));
        Assert.Equal(9, Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProducerMatrix.Length);
        Assert.All(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProducerMatrix,
            entry => Assert.False(entry.IsEligible));
        Assert.Equal(4, Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.FieldValidityMatrix.Length);
        Assert.All(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.FieldValidityMatrix, entry =>
        {
            Assert.False(entry.IsValidForAnyProducer);
            Assert.Contains("zero fallback is forbidden", entry.DenialReason);
        });
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.CanonicalCommitPointProven);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.AnyProducerEligible);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.FieldValidityProven);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.DomainCurrentCompletionOwnerCreated);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.SpecV2Materialized);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.AcceptanceRecordV2Materialized);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProductionCompositionAuthorized);
    }

    [Fact]
    public void ExistingIngress_StillUsesCallerCompletionAndCannotReachNeutralObservationAuthority()
    {
        string admission = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");
        string completion = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Completion/Records/CompletionRecord.cs",
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionRecordCompatibilityProjection.cs");
        string production = ReadProductionSourceExcludingDefinitions();
        string vmReadAndFrontend = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/VmRead/VmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Runtime/Events/VmRead/MemoryOwnedVmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Runtime/Events/VmRead/GuestPcSpFlagsVmReadScalarDeliveryCanonicalComposition.cs",
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");

        Assert.Contains("CompletionRecord? Completion = null", admission);
        Assert.Contains("Completion: request.Completion", admission);
        Assert.Contains("public CompletionRecord(", completion);
        Assert.Contains("TryFromCompatibilityExit", completion);
        Assert.DoesNotContain("CompletionRecord.FromCompatibilityExit(", production);
        Assert.DoesNotContain("CompletionRecord.TryFromCompatibilityExit(", production);
        Assert.Contains("DomainCompletionObservationOwner", production);
        Assert.Contains("CompletionGenerationAuthority", production);
        Assert.DoesNotContain("DomainCompletionObservationOwner", vmReadAndFrontend);
        Assert.DoesNotContain("CompletionGenerationAuthority", vmReadAndFrontend);
    }

    [Fact]
    public void ExistingCompletionAuthorities_CannotSatisfyCandidateSource()
    {
        CurrentCompletionProducerE0Entry eventEntry =
            Assert.Single(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProducerMatrix,
                entry => entry.RecordClass == CompletionRecordClass.Event);
        CurrentCompletionProducerE0Entry trapEntry =
            Assert.Single(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProducerMatrix,
                entry => entry.RecordClass == CompletionRecordClass.Trap);
        CurrentCompletionProducerE0Entry compatibilityEntry =
            Assert.Single(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ProducerMatrix,
                entry => entry.RecordClass == CompletionRecordClass.CompatibilityExit);

        Assert.Equal(CurrentCompletionProducerE0Disposition.DeniedVmCallSpecificAuthority,
            eventEntry.Disposition);
        Assert.Equal(CurrentCompletionProducerE0Disposition.DeniedNoArchitecturalCommitEvidence,
            trapEntry.Disposition);
        Assert.Equal(CurrentCompletionProducerE0Disposition.DeniedCompatibilityFactoryIsNotAuthority,
            compatibilityEntry.Disposition);

        string hypercall = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Completion/Records/DomainHypercallCompletionOwner.cs");
        string fence = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs");
        Assert.Contains("CompletionRecordClass.Event", hypercall);
        Assert.Contains("CompletionPublicationToken", hypercall);
        Assert.Contains("public readonly record struct TrapCompletionPublicationFenceResult", fence);
        Assert.Contains("CompletionRecordClass.Trap", fence);
    }

    [Fact]
    public void BlockedE0_GrantsNoSideAuthorityAndCreatesNoD2Artifact()
    {
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.RuntimeAuthorityGranted);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.SourceValueAvailable);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.ResultReceiptIssued);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.RegisterWritebackAuthorized);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.RetireCommitAuthorized);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.BackendExecutionAuthorized);
        Assert.False(Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.CompletionPublicationAuthorized);

        string production = ReadProductionSourceExcludingDefinitions();
        Assert.DoesNotContain("class Phase47CurrentCompletionVmReadScalarDeliveryDecisionSpecV2", production);
        Assert.DoesNotContain("class Phase47CurrentCompletionVmReadScalarDeliveryDecisionAcceptanceV2", production);
    }

    private static string ReadProductionSourceExcludingDefinitions()
    {
        string root = Path.Combine(
            ActiveVmxConformanceHelpers.FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL");
        return string.Concat(Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(
                "CompletionRecordCompatibilityProjection.cs", StringComparison.Ordinal) &&
                !path.EndsWith(
                    "Phase47CurrentCompletionVmReadScalarDeliveryE0Contract.cs", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));
    }
}
