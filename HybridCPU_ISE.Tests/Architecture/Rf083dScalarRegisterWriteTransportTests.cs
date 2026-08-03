using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3d: the sole first-family carrier is made only by the existing typed
/// Stage-B commit arm and reaches scalar WB as the same reference.  These tests
/// intentionally do not make the carrier a retirement authority.
/// </summary>
public sealed class Rf083dScalarRegisterWriteTransportTests
{
    [Fact]
    public void Success_UsesTheSameStageBAttemptAndFrozenBindingForExactlyOneEffect()
    {
        GeneratedStaticBinding binding = CreateBinding();
        PostStageBIssuedAttempt attempt = CreateAttempt(virtualThreadId: 2, binding, physicalLane: 3);

        attempt.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(2, 9, 0xCAFEUL));

        ScalarRegisterWriteRetireEffect effect = Assert.IsType<ScalarRegisterWriteRetireEffect>(
            attempt.ScalarRegisterWriteEffect);
        Assert.Same(attempt.ScheduledOperation, attempt.ExecutionRecord.ScheduledOperation);
        Assert.Same(binding, attempt.GeneratedBinding);
        Assert.Same(binding, attempt.ExecutionRecord.GeneratedBinding);
        Assert.Same(attempt.ExecutionRecord, effect.Identity.ExecutionRecord);
        Assert.Same(attempt.ScheduledOperation, effect.Identity.ScheduledOperation);
        Assert.Same(binding, effect.Identity.GeneratedBinding);
        Assert.Equal(3, effect.Identity.PhysicalLaneIndex);
        Assert.Equal(2, effect.VirtualThreadId);
        Assert.Equal(9, effect.ArchitecturalRegisterId);
        Assert.Equal(0xCAFEUL, effect.Value);

        Assert.Throws<RetireEffectIdentityContractViolationException>(() =>
            attempt.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(2, 9, 0xCAFEUL)));
        Assert.Same(effect, attempt.ScalarRegisterWriteEffect);
    }

    [Fact]
    public void StageBIdentity_PreservesSourceWorkingAndPhysicalPositionsThroughRetireProjection()
    {
        GeneratedStaticBinding binding = CreateBinding();
        PostStageBIssuedAttempt attempt = CreateAttempt(
            virtualThreadId: 2,
            binding,
            physicalLane: 3,
            sourceSlotIndex: 1,
            workingSlotIndex: 5);

        attempt.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(2, 9, 0xCAFEUL));

        ScalarRegisterWriteRetireEffect effect = Assert.IsType<ScalarRegisterWriteRetireEffect>(
            attempt.ScalarRegisterWriteEffect);
        RetireRecordIdentityProjection projection = effect.Projection;

        Assert.Equal(1, attempt.ScheduledOperation.Admission.SourceProvenance.SourceSlotIndex);
        Assert.Equal(5, attempt.ScheduledOperation.OperationId.WorkingSlotIndex);
        Assert.Equal(3, attempt.ScheduledOperation.PhysicalLane);
        Assert.Equal(5, attempt.ExecutionRecord.OperationId.WorkingSlotIndex);
        Assert.Same(attempt.ScheduledOperation, attempt.ExecutionRecord.ScheduledOperation);
        Assert.Equal(1, effect.Identity.SourceSlotIndex);
        Assert.Equal(5, effect.Identity.WorkingSlotIndex);
        Assert.Equal(3, effect.Identity.PhysicalLaneIndex);
        Assert.Same(effect.Identity, projection.Identity);
        Assert.Equal(1, projection.Identity.SourceSlotIndex);
        Assert.Equal(5, projection.Identity.WorkingSlotIndex);
        Assert.Equal(3, projection.Identity.PhysicalLaneIndex);
    }

    [Fact]
    public void SourcePreservingWorkingBundleProducer_AssignsWorkingFactsOnceBeforeStageB()
    {
        GeneratedStaticBinding binding = CreateBinding();
        AdmissionRecord admission = CreateAdmission(
            virtualThreadId: 2,
            binding,
            sourceBundleSerial: 101,
            sourceSlotIndex: 2);
        var carrier = new ScalarALUMicroOp();

        WorkingBundleEntry workingEntry = WorkingBundleEntry.CreateSourcePreserving(
            admission,
            carrier,
            workingBundleSequence: 101,
            workingSlotId: SlotId.Create(2));
        PostStageBIdentityTemplate template =
            workingEntry.CreatePostStageBIdentityTemplate(new OperationAttemptIssuer());

        Assert.Same(admission, workingEntry.Admission);
        Assert.Same(carrier, workingEntry.Carrier);
        Assert.Equal((ulong)101, workingEntry.WorkingBundleSequence);
        Assert.Equal(SlotId.Create(2), workingEntry.WorkingSlotId);
        Assert.Same(admission, template.Admission);
        Assert.Equal((ulong)101, template.WorkingBundleSequence);
        Assert.Equal(SlotId.Create(2), template.WorkingSlotId);

        PostStageBIssuedAttempt attempt =
            PostStageBIssuedAttempt.CreateAfterSuccessfulStageB(template, LaneId.Create(6));
        attempt.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(2, 9, 0xCAFEUL));
        ScalarRegisterWriteRetireEffect effect = Assert.IsType<ScalarRegisterWriteRetireEffect>(
            attempt.ScalarRegisterWriteEffect);
        Assert.Equal(2, effect.Identity.SourceSlotIndex);
        Assert.Equal(2, effect.Identity.WorkingSlotIndex);
        Assert.Equal(6, effect.Identity.PhysicalLaneIndex);
        Assert.Equal(2, effect.Projection.Identity.SourceSlotIndex);
        Assert.Equal(2, effect.Projection.Identity.WorkingSlotIndex);
        Assert.Equal(6, effect.Projection.Identity.PhysicalLaneIndex);

        Assert.Throws<ArgumentException>(() => WorkingBundleEntry.CreateSourcePreserving(
            admission,
            carrier,
            workingBundleSequence: 102,
            workingSlotId: SlotId.Create(2)));
        Assert.Throws<ArgumentException>(() => WorkingBundleEntry.CreateSourcePreserving(
            admission,
            carrier,
            workingBundleSequence: 101,
            workingSlotId: SlotId.Create(5)));
    }

    [Fact]
    public void FaultDenialAndX0_ProduceNoScalarRegisterWriteEffect()
    {
        GeneratedStaticBinding binding = CreateBinding();
        PostStageBIssuedAttempt faulted = CreateAttempt(0, binding, physicalLane: 0);
        faulted.ExecutionRecord.ApplyTerminalTransition(faulted.ExecutionRecord.CreateTerminalTransition(
            ExecutionOutcome.ArchitecturalFault(
                ExecutionDiagnostic.PageFault(new PageFaultException(0x2000, false)))));
        Assert.Throws<RetireEffectIdentityContractViolationException>(() =>
            faulted.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(0, 5, 1)));
        Assert.Null(faulted.ScalarRegisterWriteEffect);

        // A Stage-B denial does not call CreateAfterSuccessfulStageB, so no
        // attempt object exists that could manufacture a scalar effect.
        PostStageBIssuedAttempt? denied = null;
        Assert.Null(denied);

        PostStageBIssuedAttempt x0 = CreateAttempt(1, binding, physicalLane: 1);
        x0.CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(1, 0, 0x1234UL));
        Assert.Null(x0.ScalarRegisterWriteEffect);
        Assert.Equal(ExecutionOutcomeKind.Completed, x0.ExecutionRecord.Outcome!.Kind);
        Assert.Equal(0, x0.ExecutionRecord.Outcome.Result!.ArchitecturalEffectCount);
    }

    [Fact]
    public void FourVirtualThreadsRemainIsolatedByTheSameCarrierIdentity()
    {
        GeneratedStaticBinding binding = CreateBinding();
        var attempts = new PostStageBIssuedAttempt[4];

        for (int vt = 0; vt < attempts.Length; vt++)
        {
            attempts[vt] = CreateAttempt(vt, binding, physicalLane: vt);
            attempts[vt].CompleteScalarRegisterWrite(RetireRecord.RegisterWrite(vt, (ushort)(10 + vt), (ulong)vt));
        }

        for (int vt = 0; vt < attempts.Length; vt++)
        {
            ScalarRegisterWriteRetireEffect effect = Assert.IsType<ScalarRegisterWriteRetireEffect>(
                attempts[vt].ScalarRegisterWriteEffect);
            Assert.Equal(vt, effect.VirtualThreadId);
            Assert.Equal(vt, effect.Identity.OperationId.VirtualThreadId);
            Assert.Equal((ushort)(10 + vt), effect.ArchitecturalRegisterId);
            Assert.All(attempts.Where((_, otherVt) => otherVt != vt), other =>
                Assert.NotSame(attempts[vt].ExecutionRecord, other.ExecutionRecord));
        }
    }

    [Fact]
    public void ProductionTransportUsesOnlyTypedStageBAndLeavesBackendAuthorityUnchanged()
    {
        string root = FindRepositoryRoot();
        string scheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Smt", "MicroOpScheduler.SMT.cs");
        string pipelinedScheduler = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "MicroOpScheduler.FSPPipeline.cs");
        string fspProducer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "PostStageBIssuedAttempt.cs");
        string materialization = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "Materialization", "CPU_Core.PipelineExecution.Materialization.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence", "CPU_Core.PipelineExecution.Retire.cs");
        string stageFlow = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow", "CPU_Core.PipelineExecution.cs");

        Assert.Equal(1, Count(scheduler, "MaterializePostStageBIssuedAttempt(candidate, lane)"));
        Assert.DoesNotContain("MaterializePostStageBIssuedAttempt(candidate, slot)", scheduler, StringComparison.Ordinal);
        Assert.Contains("result[lane] = candidate;\n                    MaterializePostStageBIssuedAttempt(candidate, lane);", scheduler, StringComparison.Ordinal);
        Assert.Contains("bundle[lane] = candidate;\n                    MaterializePostStageBIssuedAttempt(", pipelinedScheduler, StringComparison.Ordinal);
        Assert.Contains("pipelineEntry.IdentityTemplate", pipelinedScheduler, StringComparison.Ordinal);
        Assert.Contains("WorkingBundleEntry.CreateSourcePreserving(", fspProducer, StringComparison.Ordinal);
        Assert.Contains("workingEntry.CreatePostStageBIdentityTemplate(rf08OperationAttemptIssuer)", fspProducer, StringComparison.Ordinal);
        Assert.DoesNotContain("new Core.PostStageBIdentityTemplate", fspProducer, StringComparison.Ordinal);
        Assert.Equal(1, Count(fspProducer, "SlotId.Create(entry.SlotIndex)"));
        Assert.Contains("lane.PostStageBIssuedAttempt = issueLane.MicroOp?.PostStageBIssuedAttempt;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.PostStageBIssuedAttempt = executeLane.PostStageBIssuedAttempt;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.PostStageBIssuedAttempt = memoryLane.PostStageBIssuedAttempt;", materialization, StringComparison.Ordinal);
        Assert.Contains("lane.PostStageBIssuedAttempt.CompleteScalarRegisterWrite(retireRecord);", retire, StringComparison.Ordinal);
        Assert.True(
            retire.IndexOf("CompleteScalarRegisterWrite(retireRecord)", StringComparison.Ordinal) <
            retire.IndexOf("retireBatch.AppendRetireRecord(retireRecord)", StringComparison.Ordinal));
        Assert.True(
            stageFlow.IndexOf("PrevalidateRetireWindowBatchForPublication(", StringComparison.Ordinal) <
            stageFlow.IndexOf("FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane)", StringComparison.Ordinal));

        foreach (string forbiddenAuthority in new[]
                 {
                     "PhysicalRegisterFile", "RenameMap", "CommitMap", "FreeList", "RetireCoordinator",
                     "Publish", "Commit", "Retire("
                 })
        {
            Assert.DoesNotContain(forbiddenAuthority, carrier, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdrDefinesWorkingSlotAuthorityAndTheCurrentSourcePreservingProjection()
    {
        string root = FindRepositoryRoot();
        string adr = Read(
            root,
            "Documentation",
            "Documentation",
            "ArchitectureAuthorityRefactor",
            "02_Authority",
            "ADR-009_VLIW_Retirement.md");

        Assert.Contains("stable logical position of an operation within one", adr, StringComparison.Ordinal);
        Assert.Contains("working-bundle formation owner assigns", adr, StringComparison.Ordinal);
        Assert.Contains("currently connected typed-FSP scalar RF-08.3d contour uses an explicit", adr, StringComparison.Ordinal);
        Assert.Contains("`WorkingSlotId` is numerically equal to `SourceSlotId`", adr, StringComparison.Ordinal);
        Assert.Contains("`WorkingBundleSequence` is numerically equal to `SourceBundleSerial`", adr, StringComparison.Ordinal);
        Assert.Contains("`ScalarClusterIssueEntry.SlotIndex` remains decode-derived source position", adr, StringComparison.Ordinal);
        Assert.Contains("packed-result index written as `result[lane]` is physical placement only", adr, StringComparison.Ordinal);
        Assert.Contains("may not assign, change, or reconstruct", adr, StringComparison.Ordinal);
    }

    private static PostStageBIssuedAttempt CreateAttempt(
        int virtualThreadId,
        GeneratedStaticBinding binding,
        int physicalLane,
        int? sourceSlotIndex = null,
        int? workingSlotIndex = null)
    {
        int sourceSlot = sourceSlotIndex ?? virtualThreadId;
        int workingSlot = workingSlotIndex ?? sourceSlot;
        AdmissionRecord admission = CreateAdmission(
            virtualThreadId,
            binding,
            sourceBundleSerial: (ulong)(100 + virtualThreadId),
            sourceSlotIndex: sourceSlot);
        return PostStageBIssuedAttempt.CreateAfterSuccessfulStageB(
            new PostStageBIdentityTemplate(
                admission,
                (ulong)(200 + virtualThreadId),
                SlotId.Create(workingSlot),
                new OperationAttemptIssuer()),
            LaneId.Create(physicalLane));
    }

    private static AdmissionRecord CreateAdmission(
        int virtualThreadId,
        GeneratedStaticBinding binding,
        ulong sourceBundleSerial,
        int sourceSlotIndex)
    {
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "rf08.3d-scalar"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            readRegisters: [1, 2],
            writeRegisters: [9],
            isRetireVisible: true,
            isAssist: false);
        return AdmissionRecord.Create(
            new SourceOperationProvenance(
                SemanticInstructionKey.Create([1, 2, 3], "rf08.3d", CanonicalDecodeContext.Unbound),
                virtualThreadId,
                sourceBundleSerial,
                sourceSlotId: SlotId.Create(sourceSlotIndex),
                fetchEpoch: 7),
            contract,
            virtualThreadId,
            ownerContextId: 20 + virtualThreadId,
            domainTag: 31);
    }

    private static GeneratedStaticBinding CreateBinding()
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor((uint)IsaOpcodeValues.ADD, out GeneratedIsaDescriptor descriptor));
        return GeneratedStaticBinding.FromDescriptor(in descriptor);
    }

    private static int Count(string source, string value)
    {
        int count = 0;
        for (int index = source.IndexOf(value, StringComparison.Ordinal); index >= 0;
             index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
