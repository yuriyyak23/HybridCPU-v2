using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf07ArchitecturePreservationTests
{
    [Fact]
    public void LivePrfRenameCommitFreeListAndRetireCoordinatorConstruction_RemainsProductionOwned()
    {
        string root = FindRepositoryRoot();
        string state = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Architecture/Registers/Retire/RetireCoordinator.cs");

        Assert.Contains("new PhysicalRegisterFile()", state, StringComparison.Ordinal);
        Assert.Contains("new RenameMap(SmtWays)", state, StringComparison.Ordinal);
        Assert.Contains("new CommitMap(SmtWays)", state, StringComparison.Ordinal);
        Assert.Contains("new FreeList()", state, StringComparison.Ordinal);
        Assert.Contains("new RetireCoordinator(", state, StringComparison.Ordinal);
        Assert.Contains("this.ArchRenameMap", state, StringComparison.Ordinal);
        Assert.Contains("this.ArchCommitMap", state, StringComparison.Ordinal);
        Assert.Contains("_archRenameMap.Lookup", retire, StringComparison.Ordinal);
        Assert.Contains("_physicalRegisters.Write", retire, StringComparison.Ordinal);
        Assert.Contains("_archCommitMap.Commit", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void CompilerFactsRemainCompatibilityValidationAndExactSlotFallbackRemainsLive()
    {
        Assert.Equal(
            CompilerTypedSlotPolicyMode.CompatibilityValidation,
            CompilerContract.CurrentTypedSlotPolicy.Mode);
        Assert.False(new MicroOpScheduler().TypedSlotEnabled);

        string root = FindRepositoryRoot();
        string compilerContract = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Contracts/CompilerContract.cs");
        string scheduler = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.SMT.cs");

        Assert.Contains(
            "CompilerTypedSlotPolicy.CompatibilityValidation",
            compilerContract,
            StringComparison.Ordinal);
        Assert.Contains("if (TypedSlotEnabled)", scheduler, StringComparison.Ordinal);
        Assert.Contains("else", scheduler, StringComparison.Ordinal);
        Assert.Contains("ResolveNextInjectableSlot", scheduler, StringComparison.Ordinal);
        Assert.Contains("CanInject", scheduler, StringComparison.Ordinal);
        Assert.Contains("Legacy path: exact slot search + CanInject", scheduler, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCoreContainsNoNewRobPerVtRetireQueueOrPhysicalCommitIdentityOwner()
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
    public void OutcomeAuthorityIsSingularAndRf072lIsLimitedToDeclaredFaultTailsOwnedWaitsExactDenialInvalidSizeAndFspSuppression()
    {
        string root = FindRepositoryRoot();
        string productionRoot = Path.Combine(root, "HybridCPU_ISE");
        string[] sources = Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path))
            .ToArray();

        Assert.Equal(1, sources.Count(path => Regex.IsMatch(
            File.ReadAllText(path),
            @"\bclass\s+ExecutionOutcome\b")));
        Assert.Equal(1, sources.Count(path => Regex.IsMatch(
            File.ReadAllText(path),
            @"\bclass\s+ExecutionRecord\b")));

        string stageFlow = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");
        string executeHelpers = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.ExecuteHelpers.cs");
        string faults = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Faults/CPU_Core.PipelineExecution.Exceptions.cs");
        string memoryStage = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");

        string explicitExecute = Between(
            stageFlow,
            "private void ExecuteExplicitPacketLanes()",
            "private void PipelineStage_Execute()");
        Assert.False(ContainsExactTypeName(explicitExecute, nameof(ExecutionOutcome)));
        Assert.False(ContainsExactTypeName(explicitExecute, nameof(ExecutionRecord)));
        Assert.Contains("ProjectSingleLaneNonFaultExceptionOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLanePageFaultOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("DeliverSingleLanePageFaultOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneAlignmentFaultOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("DeliverSingleLaneAlignmentFaultOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneLoadSegmentRetryOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarLoadRetryOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarLoadBackendUnavailableOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreRetryOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreBackendUnavailableOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreInvalidSizeOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneVectorTransferRetryOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains("ProjectSingleLaneVectorTransferAdmissionBackpressureOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.Contains(
            "loadMicroOp.OwnsPendingMemoryCompletion",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "deniedLoadMicroOp.HasNonSpeculativeFallbackBackendDenial(this)",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "storeMicroOp.OwnsPendingWriteCompletion",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "deniedStoreMicroOp.HasNonSpeculativeFallbackBackendDenial(this)",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "invalidStoreMicroOp.HasInvalidTransferSize",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Contains(
            "suppressedStoreMicroOp.IsSpeculativeFaultSuppressed",
            executeHelpers,
            StringComparison.Ordinal);
        Assert.Equal(
            15,
            Regex.Matches(executeHelpers, @"\bRf07LegacyOutcomeProjection\b", RegexOptions.CultureInvariant).Count);
        Assert.Contains("ProjectExplicitPacketNonFaultExceptionOutcome", executeHelpers, StringComparison.Ordinal);
        Assert.False(ContainsExactTypeName(executeHelpers, nameof(ExecutionRecord)));
        Assert.Contains("ProjectExplicitPacketPageFaultOutcome", faults, StringComparison.Ordinal);
        Assert.Contains("DeliverExplicitPacketPageFaultOutcome", faults, StringComparison.Ordinal);
        Assert.Contains("ProjectExplicitPacketAlignmentFaultOutcome", faults, StringComparison.Ordinal);
        Assert.Contains("DeliverExplicitPacketAlignmentFaultOutcome", faults, StringComparison.Ordinal);
        Assert.Equal(
            2,
            Regex.Matches(faults, @"\bRf07LegacyOutcomeProjection\b", RegexOptions.CultureInvariant).Count);
        Assert.False(ContainsExactTypeName(faults, nameof(ExecutionRecord)));
        Assert.Contains(
            "ProjectExplicitPacketCompletedMemoryRequestFailureOutcome",
            memoryStage,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeliverExplicitPacketCompletedMemoryRequestFailureOutcome",
            memoryStage,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProjectExplicitPacketMemoryNonFaultExceptionOutcome",
            memoryStage,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            Regex.Matches(memoryStage, @"\bRf07LegacyOutcomeProjection\b", RegexOptions.CultureInvariant).Count);
        Assert.False(ContainsExactTypeName(memoryStage, nameof(ExecutionRecord)));
    }

    [Fact]
    public void MemAwaitAndExplicitPacketFallbackRemainOutsideTheMicroOpFalseProjection()
    {
        string root = FindRepositoryRoot();
        string materialization = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");
        string stageFlow = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/ExecutionFlow/StageFlow/CPU_Core.PipelineExecution.cs");

        Assert.Contains(
            "The live widened LSU load/store subset claims readiness from MEM completion",
            materialization,
            StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = false;", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneLoadSegmentRetryOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarLoadRetryOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarLoadBackendUnavailableOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreRetryOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreBackendUnavailableOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreInvalidSizeOutcome", materialization, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome", materialization, StringComparison.Ordinal);

        Assert.Contains("TryPrepareExplicitPacketExecuteMemoryCarrierLane", stageFlow, StringComparison.Ordinal);
        Assert.Contains("lane.ResultReady = false;", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneLoadSegmentRetryOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarLoadRetryOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarLoadBackendUnavailableOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreRetryOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreBackendUnavailableOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreInvalidSizeOutcome", stageFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectSingleLaneScalarStoreSpeculativeSuppressionOutcome", stageFlow, StringComparison.Ordinal);
        string explicitExecute = Between(
            stageFlow,
            "private void ExecuteExplicitPacketLanes()",
            "private void PipelineStage_Execute()");
        Assert.False(ContainsExactTypeName(explicitExecute, nameof(ExecutionOutcome)));
    }

    [Fact]
    public void MutableOutcomeStateDidNotLeakIntoFrozenDecodeAdmissionOrReplayContracts()
    {
        Type[] forbiddenOwners =
        {
            typeof(ExecutionContract),
            typeof(AdmissionRecord),
            typeof(CanonicalDecodedInstruction),
            typeof(CanonicalBundle),
        };
        string[] forbiddenNames =
        {
            "ReadyState", "CompletionState", "ExecutionOutcome", "ExecutionRecord",
            "ResultValue", "Fault", "RemainingLatency", "ResourceToken", "MshrSlot"
        };

        foreach (Type owner in forbiddenOwners)
        {
            Assert.DoesNotContain(
                owner.GetProperties(),
                property => forbiddenNames.Any(name =>
                    property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    property.PropertyType == typeof(ExecutionOutcome) ||
                    property.PropertyType == typeof(ExecutionRecord)));
        }
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string Between(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, startMarker);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, endMarker);
        return source[start..end];
    }

    private static bool ContainsExactTypeName(string source, string typeName) =>
        Regex.IsMatch(source, $@"\b{Regex.Escape(typeName)}\b", RegexOptions.CultureInvariant);

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

    private static bool IsGeneratedOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
