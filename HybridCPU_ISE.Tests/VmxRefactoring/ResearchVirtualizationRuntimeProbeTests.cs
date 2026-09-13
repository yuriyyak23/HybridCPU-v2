using System.Reflection;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class ResearchVirtualizationRuntimeProbeTests
{
    [Fact]
    public void SafetyVerifierAdmission_AllowsOneStateMinimalResearchExecutionWithoutPublicationAuthority()
    {
        var verifier = new SafetyVerifier();
        var owner = new ResearchVirtualizationRuntimeOwner();
        Attempt attempt = CreateAttempt(verifier);
        ResearchVirtualizationProbeAdmissionResult admission = Admit(verifier, owner, attempt);

        Assert.True(admission.IsIssued);
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate>(admission.Certificate);
        Assert.False(certificate.HasNumericLeaf);
        Assert.False(certificate.CompletionPublicationAuthorized);
        Assert.False(certificate.RetirePublicationAuthorized);

        ResearchVirtualizationProbeExecutionResult execution = owner.Execute(verifier, certificate, attempt.Context);
        Assert.True(execution.Succeeded);
        ResearchVirtualizationRuntimeOwner.ExecutionReceipt receipt =
            Assert.IsType<ResearchVirtualizationRuntimeOwner.ExecutionReceipt>(execution.Receipt);
        Assert.Equal(0, receipt.PayloadLength);
        Assert.Equal(0, receipt.StateMutationCount);
        Assert.False(receipt.CompletionPublicationAuthorized);
        Assert.False(receipt.RetirePublicationAuthorized);
        Assert.Equal(attempt.E1.AttemptId, receipt.Identity.CarrierAttemptId);

        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedDuplicateAttempt,
            owner.Execute(verifier, certificate, attempt.Context).Decision);
    }

    [Fact]
    public void Admission_DeniesForeignMismatchedStaleContextAndStaleE1Identities()
    {
        var verifier = new SafetyVerifier();
        var owner = new ResearchVirtualizationRuntimeOwner();
        Attempt attempt = CreateAttempt(verifier);

        Assert.Equal(
            ResearchVirtualizationProbeAdmissionDecision.DeniedRuntimeIdentitySnapshot,
            verifier.IssueResearchVirtualizationOperationAdmission(
                owner.CapturePolicy(),
                attempt.Context,
                new ResearchVirtualizationOperationContext(0, 42, 7, 9, 13, 17, 19)
                    .Capture(attempt.E1.AttemptId, attempt.E1.ReplayEpoch),
                attempt.Phase,
                attempt.Bundle,
                attempt.Carrier,
                7,
                7,
                attempt.E1).Decision);
        var mismatchedContext = new ResearchVirtualizationOperationContext(0, 42, 8, 9, 13, 17, 19);
        Assert.Equal(
            ResearchVirtualizationProbeAdmissionDecision.DeniedCarrierIdentityMismatch,
            verifier.IssueResearchVirtualizationOperationAdmission(
                owner.CapturePolicy(),
                mismatchedContext,
                mismatchedContext.Capture(attempt.E1.AttemptId, attempt.E1.ReplayEpoch),
                attempt.Phase,
                attempt.Bundle,
                attempt.Carrier,
                7,
                7,
                attempt.E1).Decision);

        ResearchVirtualizationOperationContext.IdentitySnapshot staleSnapshot =
            attempt.Context.Capture(attempt.E1.AttemptId, attempt.E1.ReplayEpoch);
        attempt.Context.Invalidate();
        Assert.Equal(
            ResearchVirtualizationProbeAdmissionDecision.DeniedRuntimeIdentitySnapshot,
            verifier.IssueResearchVirtualizationOperationAdmission(
                owner.CapturePolicy(),
                attempt.Context,
                staleSnapshot,
                attempt.Phase,
                attempt.Bundle,
                attempt.Carrier,
                7,
                7,
                attempt.E1).Decision);

        verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);
        Assert.Equal(
            ResearchVirtualizationProbeAdmissionDecision.DeniedE1Carrier,
            Admit(verifier, owner, attempt).Decision);
    }

    [Fact]
    public void Execution_DeniesForeignOwnerAndStaleOwnerPolicy()
    {
        var verifier = new SafetyVerifier();
        var owner = new ResearchVirtualizationRuntimeOwner();
        Attempt attempt = CreateAttempt(verifier);
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate>(
                Admit(verifier, owner, attempt).Certificate);

        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedForeignOwner,
            new ResearchVirtualizationRuntimeOwner().Execute(verifier, certificate, attempt.Context).Decision);

        owner.InvalidatePolicy();
        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedStalePolicyGeneration,
            owner.Execute(verifier, certificate, attempt.Context).Decision);

        var verifier2 = new SafetyVerifier();
        var owner2 = new ResearchVirtualizationRuntimeOwner();
        Attempt contextAttempt = CreateAttempt(verifier2);
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate contextCertificate =
            Assert.IsType<SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate>(
                Admit(verifier2, owner2, contextAttempt).Certificate);
        contextAttempt.Context.Invalidate();
        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedStaleRuntimeContext,
            owner2.Execute(verifier2, contextCertificate, contextAttempt.Context).Decision);
    }

    [Fact]
    public void Execution_DeniesWhenE1IsRevokedAfterE2Issuance()
    {
        var verifier = new SafetyVerifier();
        var owner = new ResearchVirtualizationRuntimeOwner();
        Attempt attempt = CreateAttempt(verifier);
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate>(
                Admit(verifier, owner, attempt).Certificate);

        verifier.InvalidateVirtualizationAdmissions(ReplayPhaseInvalidationReason.Manual);

        Assert.Equal(
            ResearchVirtualizationProbeExecutionDecision.DeniedStaleAdmission,
            owner.Execute(verifier, certificate, attempt.Context).Decision);
    }

    [Fact]
    public async Task Execution_ConsumesOneCertificateExactlyOnceUnderConcurrency()
    {
        var verifier = new SafetyVerifier();
        var owner = new ResearchVirtualizationRuntimeOwner();
        Attempt attempt = CreateAttempt(verifier);
        SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate certificate =
            Assert.IsType<SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate>(
                Admit(verifier, owner, attempt).Certificate);

        Task<ResearchVirtualizationProbeExecutionResult>[] executions = Enumerable
            .Range(0, 16)
            .Select(_ => Task.Run(() => owner.Execute(verifier, certificate, attempt.Context)))
            .ToArray();
        ResearchVirtualizationProbeExecutionResult[] results = await Task.WhenAll(executions);

        Assert.Single(results, result => result.Decision == ResearchVirtualizationProbeExecutionDecision.Executed);
        Assert.Equal(
            15,
            results.Count(result => result.Decision == ResearchVirtualizationProbeExecutionDecision.DeniedDuplicateAttempt));
    }

    [Fact]
    public void AdmissionPolicyAndReceipt_AreNotPubliclyConstructible()
    {
        Assert.Empty(typeof(SafetyVerifier.ResearchVirtualizationOperationAdmissionCertificate)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Empty(typeof(ResearchVirtualizationRuntimeOwner.PolicySnapshot)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.Empty(typeof(ResearchVirtualizationRuntimeOwner.ExecutionReceipt)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void Source_IsTestingOnlyAndIndependentFromCompatibilityAndPublicationPlanes()
    {
        string ownerSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/Hypercalls/ResearchVirtualizationRuntimeProbe.cs");
        string admissionSource = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/Safety/SafetyVerifier.ResearchVirtualizationProbeAdmission.cs");

        Assert.StartsWith("#if TESTING", ownerSource);
        Assert.StartsWith("#if TESTING", admissionSource);
        Assert.DoesNotContain("Vmx", ownerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Vmcs", ownerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VMCALL", ownerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompletionRecord", ownerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VmxRetireEffect", ownerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BackendExecutionAuthorized", ownerSource, StringComparison.Ordinal);
        Assert.Contains("ValidateVirtualizationAdmission", admissionSource);
        Assert.DoesNotContain("CompletionRecord", admissionSource, StringComparison.Ordinal);
    }

    private static ResearchVirtualizationProbeAdmissionResult Admit(
        SafetyVerifier verifier,
        ResearchVirtualizationRuntimeOwner owner,
        Attempt attempt) =>
        verifier.IssueResearchVirtualizationOperationAdmission(
            owner.CapturePolicy(),
            attempt.Context,
            attempt.Context.Capture(attempt.E1.AttemptId, attempt.E1.ReplayEpoch),
            attempt.Phase,
            attempt.Bundle,
            attempt.Carrier,
            7,
            7,
            attempt.E1);

    private static Attempt CreateAttempt(SafetyVerifier verifier)
    {
        ReplayPhaseContext phase = new(true, 11, 0x4000, 1, 0, 0, 0, ReplayPhaseInvalidationReason.None);
        SmtBundleMetadata4Way bundle = new(0, 42, 7, 7, 7, 1);
        VmxMicroOp carrier = CreateCarrier();
        VirtualizationAdmissionIssueResult issue =
            verifier.IssueVirtualizationAdmissionAfterStageB(phase, bundle, carrier, 7, 7);
        var context = new ResearchVirtualizationOperationContext(
            virtualThreadId: 0,
            ownerContextId: 42,
            domainTag: 7,
            addressSpaceTag: 9,
            capabilityGeneration: 13,
            evidenceGeneration: 17,
            restoreGeneration: 19);
        return new(
            phase,
            bundle,
            carrier,
            Assert.IsType<SafetyVerifier.VirtualizationAdmissionCertificate>(issue.Certificate),
            context);
    }

    private static VmxMicroOp CreateCarrier()
    {
        var carrier = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMCALL,
            OwnerThreadId = 0,
            VirtualThreadId = 0,
            OwnerContextId = 42,
            Rd = 0,
            Rs1 = 0,
            Rs2 = 0,
            Placement = new SlotPlacementMetadata
            {
                RequiredSlotClass = SlotClass.SystemSingleton,
                PinningKind = SlotPinningKind.HardPinned,
                PinnedLaneId = 7,
                DomainTag = 7,
            },
            Instruction = new InstructionIR
            {
                CanonicalOpcode = new IsaOpcode(IsaOpcodeValues.VMCALL),
                Class = InstructionClass.Vmx,
                SerializationClass = SerializationClass.VmxSerial,
                Rd = 0,
                Rs1 = 0,
                Rs2 = 0,
                Imm = 0,
            },
        };
        carrier.RefreshWriteMetadata();
        return carrier;
    }

    private sealed record Attempt(
        ReplayPhaseContext Phase,
        SmtBundleMetadata4Way Bundle,
        VmxMicroOp Carrier,
        SafetyVerifier.VirtualizationAdmissionCertificate E1,
        ResearchVirtualizationOperationContext Context);
}
