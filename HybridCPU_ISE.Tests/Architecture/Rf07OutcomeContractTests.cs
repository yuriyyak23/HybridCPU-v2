using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07OutcomeContractTests
{
    [Fact]
    public void OutcomeFamily_IsExactlyTheFrozenSixTerminalDispositions()
    {
        Assert.Equal(
            new[]
            {
                ExecutionOutcomeKind.Completed,
                ExecutionOutcomeKind.ArchitecturalFault,
                ExecutionOutcomeKind.Retryable,
                ExecutionOutcomeKind.StructuralBlocked,
                ExecutionOutcomeKind.BackendUnavailable,
                ExecutionOutcomeKind.FatalInvariantViolation,
            },
            Enum.GetValues<ExecutionOutcomeKind>());
    }

    [Fact]
    public void PositiveCoherenceMatrix_AcceptsEveryDispositionWithItsRequiredPayload()
    {
        ExecutionOutcome completed = ExecutionOutcome.Completed(
            ExecutionResultContract.Scalar(42, architecturalEffectCount: 1));
        ExecutionOutcome pageFault = ExecutionOutcome.ArchitecturalFault(
            ExecutionDiagnostic.PageFault(new PageFaultException(0x1000, isWrite: false)));
        ExecutionOutcome retryable = ExecutionOutcome.Retryable(
            ExecutionDiagnostic.Retryable("asynchronous memory request remains pending"));
        ExecutionOutcome structural = ExecutionOutcome.StructuralBlocked(
            ExecutionDiagnostic.StructuralBlocked("required execution lane remains occupied"));
        ExecutionOutcome unavailable = ExecutionOutcome.BackendUnavailable(
            ExecutionDiagnostic.BackendUnavailable("runtime provider is not bound"));
        ExecutionOutcome fatal = ExecutionOutcome.FatalInvariantViolation(
            ExecutionDiagnostic.Fatal(new InvalidOperationException("unknown invariant failure")));

        Assert.Equal(ExecutionOutcomeKind.Completed, completed.Kind);
        Assert.True(completed.HasArchitecturalEffects);
        Assert.Equal(ExecutionDiagnosticCode.PageFault, pageFault.Diagnostic!.Code);
        Assert.Equal(ExecutionDiagnosticCode.ResourceWait, retryable.Diagnostic!.Code);
        Assert.Equal(ExecutionDiagnosticCode.StructuralHazard, structural.Diagnostic!.Code);
        Assert.Equal(ExecutionDiagnosticCode.RuntimeBackendUnavailable, unavailable.Diagnostic!.Code);
        Assert.Equal(ExecutionDiagnosticCode.UnknownException, fatal.Diagnostic!.Code);
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsMissingOrIncompatiblePayloads()
    {
        AssertContractViolation(() => ExecutionOutcome.Create(ExecutionOutcomeKind.Completed));
        AssertContractViolation(() => ExecutionOutcome.Create(ExecutionOutcomeKind.ArchitecturalFault));
        AssertContractViolation(() => ExecutionOutcome.Create(
            ExecutionOutcomeKind.Completed,
            ExecutionResultContract.WithoutScalarResult(),
            ExecutionDiagnostic.Retryable("not a completed diagnostic")));
        AssertContractViolation(() => ExecutionOutcome.Create(
            ExecutionOutcomeKind.Retryable,
            ExecutionResultContract.WithoutScalarResult(architecturalEffectCount: 1),
            ExecutionDiagnostic.Retryable("retry cannot own an architectural effect")));
        AssertContractViolation(() => ExecutionOutcome.Create(
            ExecutionOutcomeKind.StructuralBlocked,
            ExecutionResultContract.WithoutScalarResult(architecturalEffectCount: 1),
            ExecutionDiagnostic.StructuralBlocked("block cannot own an architectural mutation")));
        AssertContractViolation(() => ExecutionOutcome.Retryable(
            ExecutionDiagnostic.BackendUnavailable("backend denial is not resource wait")));
        AssertContractViolation(() => ExecutionOutcome.Retryable(
            ExecutionDiagnostic.Fatal(new InvalidOperationException("unknown exception"))));
        AssertContractViolation(() => Rf07LegacyOutcomeProjection.ProjectSuccessfulExecution(
            legacySuccess: false,
            ExecutionResultContract.WithoutScalarResult()));
        AssertContractViolation(() => Rf07LegacyOutcomeProjection.ProjectKnownRetry(
            legacySuccess: true,
            "generic false must not be inferred"));
    }

    [Fact]
    public void OutcomePayloadTypes_AreImmutablePublicContracts()
    {
        AssertGetOnlyProperties(typeof(ExecutionOutcome));
        AssertGetOnlyProperties(typeof(ExecutionDiagnostic));
        AssertGetOnlyProperties(typeof(ExecutionResultContract));
        AssertGetOnlyProperties(typeof(ExecutionTransition));
    }

    [Fact]
    public void ExecutionRecord_BindsExactScheduledAttemptAndFrozenBinding()
    {
        ScheduledOperation scheduled = CreateScheduledOperation(out _);
        ExecutionRecord record = ExecutionRecord.Create(scheduled);

        Assert.Same(scheduled, record.ScheduledOperation);
        Assert.Equal(scheduled.OperationId, record.OperationId);
        Assert.Same(
            scheduled.Admission.ExecutionContract.GeneratedBinding,
            record.GeneratedBinding);
        Assert.Equal(ExecutionRecordState.Issued, record.State);
        Assert.Null(record.Outcome);

        ExecutionOutcome outcome = ExecutionOutcome.Completed(
            ExecutionResultContract.Scalar(7, architecturalEffectCount: 1));
        record.ApplyTerminalTransition(record.CreateTerminalTransition(outcome));

        Assert.Equal(ExecutionRecordState.Terminal, record.State);
        Assert.Same(outcome, record.Outcome);
    }

    [Fact]
    public void ExecutionRecord_RejectsOperationIdentityMismatch()
    {
        ScheduledOperation scheduled = CreateScheduledOperation(out OperationAttemptIssuer issuer);
        ScheduledOperation otherAttempt = ScheduledOperation.CreateAfterStageB(
            scheduled.Admission,
            workingBundleSequence: 9,
            workingSlotIndex: 0,
            physicalLane: 0,
            issuer);
        ExecutionRecord record = ExecutionRecord.Create(scheduled);
        ExecutionOutcome outcome = ExecutionOutcome.Completed(
            ExecutionResultContract.WithoutScalarResult());

        AssertContractViolation(() => record.ApplyTerminalTransition(
            new ExecutionTransition(otherAttempt.OperationId, record.GeneratedBinding, outcome)));
        Assert.Equal(ExecutionRecordState.Issued, record.State);
    }

    [Fact]
    public void ExecutionRecord_RejectsGeneratedBindingMismatchEvenWhenReconstructedValuesMatch()
    {
        ScheduledOperation scheduled = CreateScheduledOperation(out _);
        ExecutionRecord record = ExecutionRecord.Create(scheduled);
        GeneratedStaticBinding reconstructed = record.GeneratedBinding with { };
        ExecutionOutcome outcome = ExecutionOutcome.Completed(
            ExecutionResultContract.WithoutScalarResult());

        Assert.Equal(record.GeneratedBinding, reconstructed);
        Assert.NotSame(record.GeneratedBinding, reconstructed);
        AssertContractViolation(() => record.ApplyTerminalTransition(
            new ExecutionTransition(record.OperationId, reconstructed, outcome)));
        Assert.Equal(ExecutionRecordState.Issued, record.State);
    }

    [Fact]
    public void ExecutionRecord_RejectsDuplicateAndPostTerminalTransitions()
    {
        ScheduledOperation scheduled = CreateScheduledOperation(out _);
        ExecutionRecord record = ExecutionRecord.Create(scheduled);
        ExecutionTransition completed = record.CreateTerminalTransition(
            ExecutionOutcome.Completed(ExecutionResultContract.WithoutScalarResult()));
        record.ApplyTerminalTransition(completed);

        AssertContractViolation(() => record.ApplyTerminalTransition(completed));
        AssertContractViolation(() => record.ApplyTerminalTransition(
            record.CreateTerminalTransition(
                ExecutionOutcome.Retryable(ExecutionDiagnostic.Retryable("late retry")))));
        Assert.Equal(ExecutionOutcomeKind.Completed, record.Outcome!.Kind);
    }

    [Fact]
    public void ExecutionRecord_HasNoBackendOrRetirementAuthorityMembers()
    {
        string[] forbiddenFragments =
        {
            "Rename", "PhysicalRegister", "DestPhys", "OldPhys", "CommitMap",
            "FreeList", "Retire", "IssueAge", "Checkpoint", "Squash", "Recovery"
        };
        MemberInfo[] members = typeof(ExecutionRecord).GetMembers(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            members,
            member => forbiddenFragments.Any(fragment =>
                member.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private static void AssertGetOnlyProperties(Type type) =>
        Assert.DoesNotContain(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.SetMethod is not null);

    private static void AssertContractViolation(Action action)
    {
        ExecutionOutcomeContractViolationException exception =
            Assert.Throws<ExecutionOutcomeContractViolationException>(action);
        Assert.Equal(
            ExecutionFaultCategory.InvalidInternalOp,
            ExecutionFaultContract.GetCategory(exception));
    }

    private static ScheduledOperation CreateScheduledOperation(
        out OperationAttemptIssuer issuer)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            (uint)IsaOpcodeValues.ADD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "rf07-scalar-v1"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            readRegisters: [1, 2],
            writeRegisters: [3]);
        AdmissionRecord admission = AdmissionRecord.Create(
            new SourceOperationProvenance(
                SemanticInstructionKey.Create([1, 2, 3], "rf07-test", CanonicalDecodeContext.Unbound),
                sourceVirtualThreadId: 0,
                sourceBundleSerial: 4,
                sourceSlotId: SlotId.Zero,
                fetchEpoch: 2),
            contract,
            virtualThreadId: 0,
            ownerContextId: 7,
            domainTag: 11);
        issuer = new OperationAttemptIssuer();
        return ScheduledOperation.CreateAfterStageB(
            admission,
            workingBundleSequence: 9,
            workingSlotIndex: 0,
            physicalLane: 0,
            issuer);
    }
}
