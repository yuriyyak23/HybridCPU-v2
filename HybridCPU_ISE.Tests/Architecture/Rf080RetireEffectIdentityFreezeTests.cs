using System.Reflection;
using System.Text.RegularExpressions;
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

public sealed class Rf080RetireEffectIdentityFreezeTests
{
    [Fact]
    public void PositiveCoherenceMatrix_FreezesExactExistingAttemptAndRetireRecord()
    {
        ExecutionRecord record = CreateTerminalRecord(
            virtualThreadId: 2,
            sourceBundleSerial: 41,
            sourceSlotIndex: 3,
            workingBundleSequence: 73,
            workingSlotIndex: 5,
            physicalLane: 1,
            architecturalEffectCount: 2,
            out ScheduledOperation scheduled);

        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.RegisterWrite,
            effectOrdinal: 0,
            effectVirtualThreadId: 2,
            architecturalRegisterId: 9);
        RetireRecord retireRecord = RetireRecord.RegisterWrite(2, 9, 0x1122);
        RetireRecordIdentityProjection projection =
            RetireRecordIdentityProjection.Create(retireRecord, identity);

        Assert.Same(record, identity.ExecutionRecord);
        Assert.Same(scheduled, identity.ScheduledOperation);
        Assert.Equal(scheduled.OperationId, identity.OperationId);
        Assert.Equal((ulong)73, identity.WorkingBundleSequence);
        Assert.Equal(5, identity.WorkingSlotIndex);
        Assert.Equal(1, identity.PhysicalLaneIndex);
        Assert.Equal((ulong)41, identity.SourceBundleSerial);
        Assert.Equal(3, identity.SourceSlotIndex);
        Assert.Equal((ulong)1, identity.OperationAttempt);
        Assert.Same(record.GeneratedBinding, identity.GeneratedBinding);
        Assert.Equal(retireRecord, projection.RetireRecord);
        Assert.Same(identity, projection.Identity);

        RetireVisibleEffectCoherence.ValidateDistinctTerminalEffects(
            [
                identity,
                RetireVisibleEffectIdentity.Freeze(
                    record,
                    RetireVisibleEffectKind.PcWrite,
                    effectOrdinal: 1,
                    effectVirtualThreadId: 2),
            ]);
        RetireVisibleEffectCoherence.ValidatePublicationClaim(
            identity,
            scheduled,
            record,
            record.GeneratedBinding,
            prevalidationComplete: true,
            selectedByRetireProtocol: true,
            requestsStoreVisibility: false);
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsMissingAttemptAndCrossCarrierMismatch()
    {
        Assert.Throws<ArgumentNullException>(() => RetireVisibleEffectIdentity.Freeze(
            null!,
            RetireVisibleEffectKind.PcWrite,
            effectOrdinal: 0,
            effectVirtualThreadId: 0));

        ExecutionRecord record = CreateTerminalRecord(
            0, 4, 0, 9, 0, 0, 1, out ScheduledOperation scheduled);
        ExecutionRecord other = CreateTerminalRecord(
            0, 4, 0, 10, 0, 0, 1, out ScheduledOperation otherScheduled);
        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.PcWrite,
            0,
            0);

        AssertViolation(() => RetireVisibleEffectCoherence.ValidatePublicationClaim(
            identity,
            otherScheduled,
            other,
            record.GeneratedBinding,
            prevalidationComplete: true,
            selectedByRetireProtocol: true,
            requestsStoreVisibility: false));
        Assert.NotEqual(scheduled.OperationId, otherScheduled.OperationId);
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsBindingVtRetireRecordAndX0Mismatch()
    {
        ExecutionRecord record = CreateTerminalRecord(
            1, 6, 1, 12, 1, 2, 1, out ScheduledOperation scheduled);

        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.RegisterWrite,
            0,
            effectVirtualThreadId: 0,
            architecturalRegisterId: 3));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.RegisterWrite,
            0,
            effectVirtualThreadId: 1,
            architecturalRegisterId: 0));

        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.RegisterWrite,
            0,
            effectVirtualThreadId: 1,
            architecturalRegisterId: 3);
        AssertViolation(() => RetireRecordIdentityProjection.Create(
            RetireRecord.RegisterWrite(0, 3, 7),
            identity));
        AssertViolation(() => RetireRecordIdentityProjection.Create(
            RetireRecord.RegisterWrite(1, 4, 7),
            identity));
        AssertViolation(() => RetireRecordIdentityProjection.Create(
            RetireRecord.PcWrite(1, 7),
            identity));

        GeneratedStaticBinding reconstructed = record.GeneratedBinding with { };
        Assert.Equal(record.GeneratedBinding, reconstructed);
        Assert.NotSame(record.GeneratedBinding, reconstructed);
        AssertViolation(() => RetireVisibleEffectCoherence.ValidatePublicationClaim(
            identity,
            scheduled,
            record,
            reconstructed,
            prevalidationComplete: true,
            selectedByRetireProtocol: true,
            requestsStoreVisibility: false));
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsDuplicateOrPostFaultEffectTransition()
    {
        ExecutionRecord completed = CreateTerminalRecord(
            0, 7, 0, 14, 0, 0, 1, out _);
        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            completed,
            RetireVisibleEffectKind.PcWrite,
            0,
            0);
        AssertViolation(() => RetireVisibleEffectCoherence.ValidateDistinctTerminalEffects(
            [identity, identity]));

        ExecutionRecord fault = CreateIssuedRecord(
            0, 7, 0, 15, 0, 0, isRetireVisible: true, isAssist: false, out _);
        fault.ApplyTerminalTransition(fault.CreateTerminalTransition(
            ExecutionOutcome.ArchitecturalFault(
                ExecutionDiagnostic.PageFault(new PageFaultException(0x1000, false)))));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            fault,
            RetireVisibleEffectKind.PcWrite,
            0,
            0));

        ExecutionRecord denial = CreateIssuedRecord(
            0, 7, 0, 16, 0, 0, isRetireVisible: true, isAssist: false, out _);
        denial.ApplyTerminalTransition(denial.CreateTerminalTransition(
            ExecutionOutcome.BackendUnavailable(
                ExecutionDiagnostic.BackendUnavailable("backend denied"))));
        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            denial,
            RetireVisibleEffectKind.PcWrite,
            0,
            0));
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsPublicationBeforePrevalidationAndUnselectedStoreVisibility()
    {
        ExecutionRecord record = CreateTerminalRecord(
            3, 8, 2, 19, 2, 5, 1, out ScheduledOperation scheduled);
        RetireVisibleEffectIdentity store = RetireVisibleEffectIdentity.Freeze(
            record,
            RetireVisibleEffectKind.DeferredStoreCommit,
            0,
            3);

        AssertViolation(() => RetireVisibleEffectCoherence.ValidatePublicationClaim(
            store,
            scheduled,
            record,
            record.GeneratedBinding,
            prevalidationComplete: false,
            selectedByRetireProtocol: true,
            requestsStoreVisibility: true));
        AssertViolation(() => RetireVisibleEffectCoherence.ValidatePublicationClaim(
            store,
            scheduled,
            record,
            record.GeneratedBinding,
            prevalidationComplete: true,
            selectedByRetireProtocol: false,
            requestsStoreVisibility: true));

        RetireVisibleEffectCoherence.ValidatePublicationClaim(
            store,
            scheduled,
            record,
            record.GeneratedBinding,
            prevalidationComplete: true,
            selectedByRetireProtocol: true,
            requestsStoreVisibility: true);
    }

    [Fact]
    public void NegativeCoherenceMatrix_RejectsAssistAsRetireVisibleEffect()
    {
        ExecutionRecord assist = CreateIssuedRecord(
            0, 9, 0, 20, 0, 6, isRetireVisible: false, isAssist: true, out _);
        assist.ApplyTerminalTransition(assist.CreateTerminalTransition(
            ExecutionOutcome.Completed(
                ExecutionResultContract.WithoutScalarResult(architecturalEffectCount: 1))));

        AssertViolation(() => RetireVisibleEffectIdentity.Freeze(
            assist,
            RetireVisibleEffectKind.AcceleratorCommit,
            0,
            0));
    }

    [Fact]
    public void ContractsAreImmutableAndExecutionRecordStillHasNoRobOrCommitAuthority()
    {
        foreach (Type type in new[]
                 {
                     typeof(RetireVisibleEffectIdentity),
                     typeof(RetireRecordIdentityProjection),
                 })
        {
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod is not null);
        }

        string[] forbiddenFragments =
        {
            "Rob", "Rename", "PhysicalRegister", "DestPhys", "OldPhys",
            "CommitMap", "FreeList", "Checkpoint", "Squash", "Recovery",
            "RetireSelection", "Publish"
        };
        foreach (Type type in new[] { typeof(ExecutionRecord), typeof(RetireVisibleEffectIdentity) })
        {
            MemberInfo[] members = type.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.DoesNotContain(
                members,
                member => forbiddenFragments.Any(fragment =>
                    member.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [Fact]
    public void Rf080ContractIsAdditiveAndProductionRetirementRoutingIsUnchanged()
    {
        string root = FindRepositoryRoot();
        string stageFlow = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");
        string coordinator = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/RetireCoordinator.cs");

        Assert.DoesNotContain(nameof(RetireVisibleEffectIdentity), stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(RetireVisibleEffectIdentity), retire, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(RetireVisibleEffectIdentity), coordinator, StringComparison.Ordinal);
        Assert.Contains("ResolveStableRetireOrder", stageFlow, StringComparison.Ordinal);
        Assert.Contains("TruncateRetireOrderBeforeWriteBackFaultWinner", stageFlow, StringComparison.Ordinal);
        Assert.Contains("ApplyRetireBatchImmediateEffects", stageFlow, StringComparison.Ordinal);
        Assert.Contains("RetireCoordinator.Retire(retireBatch.RetireRecords)", retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit", retire, StringComparison.Ordinal);
    }

    private static ExecutionRecord CreateTerminalRecord(
        int virtualThreadId,
        ulong sourceBundleSerial,
        int sourceSlotIndex,
        ulong workingBundleSequence,
        int workingSlotIndex,
        int physicalLane,
        int architecturalEffectCount,
        out ScheduledOperation scheduled)
    {
        ExecutionRecord record = CreateIssuedRecord(
            virtualThreadId,
            sourceBundleSerial,
            sourceSlotIndex,
            workingBundleSequence,
            workingSlotIndex,
            physicalLane,
            isRetireVisible: true,
            isAssist: false,
            out scheduled);
        record.ApplyTerminalTransition(record.CreateTerminalTransition(
            ExecutionOutcome.Completed(
                ExecutionResultContract.WithoutScalarResult(architecturalEffectCount))));
        return record;
    }

    private static ExecutionRecord CreateIssuedRecord(
        int virtualThreadId,
        ulong sourceBundleSerial,
        int sourceSlotIndex,
        ulong workingBundleSequence,
        int workingSlotIndex,
        int physicalLane,
        bool isRetireVisible,
        bool isAssist,
        out ScheduledOperation scheduled)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            (uint)IsaOpcodeValues.ADD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        ExecutionContract contract = ExecutionContract.Create(
            binding,
            new RuntimeExecutionProviderBinding(binding.RuntimeExecutionProviderId, "rf08-effect-v1"),
            InstructionClass.ScalarAlu,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.AluClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.None,
            readRegisters: [1, 2],
            writeRegisters: [3],
            isRetireVisible: isRetireVisible,
            isAssist: isAssist);
        AdmissionRecord admission = AdmissionRecord.Create(
            new SourceOperationProvenance(
                SemanticInstructionKey.Create([1, 2, 3], "rf08-test", CanonicalDecodeContext.Unbound),
                virtualThreadId,
                sourceBundleSerial,
                SlotId.Create(sourceSlotIndex),
                fetchEpoch: 2),
            contract,
            virtualThreadId,
            ownerContextId: virtualThreadId + 10,
            domainTag: 21);
        scheduled = ScheduledOperation.CreateAfterStageB(
            admission,
            workingBundleSequence,
            workingSlotIndex,
            physicalLane,
            new OperationAttemptIssuer());
        return ExecutionRecord.Create(scheduled);
    }

    private static void AssertViolation(Action action) =>
        Assert.Throws<RetireEffectIdentityContractViolationException>(action);

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

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

public sealed class Rf080ArchitecturePreservationTests
{
    [Fact]
    public void LiveBackendOwnersCompilerPolicyAndExactSlotFallbackRemainAuthoritative()
    {
        string root = FindRepositoryRoot();
        string state = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/RetireCoordinator.cs");
        string scheduler = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.SMT.cs");
        string decoderFacade = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Frontend/Decode/VliwDecoderV4Bridge/VliwDecoderV4.cs");

        Assert.Contains("new PhysicalRegisterFile()", state, StringComparison.Ordinal);
        Assert.Contains("new RenameMap(SmtWays)", state, StringComparison.Ordinal);
        Assert.Contains("new CommitMap(SmtWays)", state, StringComparison.Ordinal);
        Assert.Contains("new FreeList()", state, StringComparison.Ordinal);
        Assert.Contains("new RetireCoordinator(", state, StringComparison.Ordinal);
        Assert.Contains("_archRenameMap.Lookup", retire, StringComparison.Ordinal);
        Assert.Contains("_physicalRegisters.Write", retire, StringComparison.Ordinal);
        Assert.Contains("_archCommitMap.Commit", retire, StringComparison.Ordinal);
        Assert.Equal(
            CompilerTypedSlotPolicyMode.CompatibilityValidation,
            CompilerContract.CurrentTypedSlotPolicy.Mode);
        Assert.False(new MicroOpScheduler().TypedSlotEnabled);
        Assert.Contains("if (TypedSlotEnabled)", scheduler, StringComparison.Ordinal);
        Assert.Contains("Legacy path: exact slot search + CanInject", scheduler, StringComparison.Ordinal);
        Assert.Contains("public sealed class VliwDecoderV4 : IDecoderFrontend", decoderFacade, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionHasNoRobPerVtRetireSpeculativeQueueOrNewPhysicalCommitOwner()
    {
        string root = FindRepositoryRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        var forbiddenDeclarations = new Regex(
            @"\b(?:class|record|struct)\s+(?:Rob|RobEntry|ReorderBuffer|ReorderBufferEntry|RetireQueue|PerVtRetireQueue|RenameCheckpoint|SpeculativeIssueQueue|SpeculativeCommitQueue)\b",
            RegexOptions.CultureInvariant);
        var forbiddenOwnerIdentity = new Regex(
            @"\b(?:class|record|struct)\s+\w*(?:Physical|Commit)\w*(?:Owner|Identity)\w*\b",
            RegexOptions.CultureInvariant);
        var forbiddenLifecycle = new Regex(
            @"\b(?:DestPhys|OldPhys|retireQueuesByVt|perVtRetireQueue|vtRetireQueue)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        foreach (string path in Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsGeneratedOutput(path)))
        {
            string source = File.ReadAllText(path);
            Assert.False(forbiddenDeclarations.IsMatch(source), Path.GetRelativePath(root, path));
            Assert.False(forbiddenOwnerIdentity.IsMatch(source), Path.GetRelativePath(root, path));
            Assert.False(forbiddenLifecycle.IsMatch(source), Path.GetRelativePath(root, path));
        }
    }

    [Fact]
    public void ExecuteStageHasNoRetireDecisionFaultWinnerOrArchitecturalPublicationAuthority()
    {
        string root = FindRepositoryRoot();
        string executeRoot = Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "ExecutionFlow",
            "Materialization");
        string source = string.Join(
            "\n",
            Directory.GetFiles(executeRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("RetireCoordinator.Retire", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyRetireBatchImmediateEffects", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveStableRetireOrder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TruncateRetireOrderBeforeWriteBackFaultWinner", source, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(RetireVisibleEffectIdentity), source, StringComparison.Ordinal);
    }

    private static bool IsGeneratedOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
