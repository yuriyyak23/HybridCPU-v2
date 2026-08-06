using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-12.6ad authoritative valid-input physical-bank geometry replacement.
/// These guards do not authorize request-binding, resolver-fallback,
/// compatibility-setter invalid behavior, wider-topology projection or wire
/// migration.
/// </summary>
public sealed class
    Rf126adMemoryBankGeometryAuthoritativeReplacementValidInputCutoverTests
{
    [Fact]
    public void PaperFixesWinnerPreparationGenerationAndAtomicPublicationOrder()
    {
        string paper = Paper(FindRepositoryRoot());

        Order(
            paper,
            "Rejection precedence is `InvalidBankCount`",
            "`InvalidBankWidth`, then `Busy`, then `GenerationExhausted`, then",
            "`PlatformRejected`; otherwise the result is `Applied`",
            "Validation and the",
            "quiescence check occur before candidate storage is prepared",
            "fresh non-zero generation is issued only for the atomic publish");
        Assert.Contains(
            "Every",
            paper,
            StringComparison.Ordinal);
        Assert.Contains(
            "rejection leaves the old geometry, generation, queues and owner state",
            paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorPublishesFirstOwnerLocalGeneration()
    {
        MemorySubsystem memory = CreateMemory();

        PhysicalMemoryBankGeometry geometry =
            memory.PublishedPhysicalBankGeometry;
        Assert.Equal(8, geometry.BankCount);
        Assert.Equal(64, geometry.BankWidthBytes);
        Assert.Equal(1UL, geometry.Generation.Value);
        Assert.True(geometry.IsWellFormed);
    }

    [Fact]
    public void AppliedReplacementPublishesTupleAndPreparedTopologyAtomically()
    {
        MemorySubsystem memory = CreateMemory();
        object oldOccupied = Field(memory, "bankOccupied");
        object oldLastAccess = Field(memory, "bankLastAccessCycle");
        object oldQueues = Field(memory, "bankQueues");
        object oldPorts = Field(memory, "_portStates");

        MemoryBankGeometryUpdateResult result =
            memory.TryReplacePhysicalMemoryBankGeometry(4, 128);

        Assert.True(result.IsApplied);
        Assert.Null(result.RejectReason);
        Assert.Equal(4, memory.NumBanks);
        Assert.Equal(128, memory.BankWidthBytes);
        PhysicalMemoryBankGeometry geometry =
            memory.PublishedPhysicalBankGeometry;
        Assert.Equal(4, geometry.BankCount);
        Assert.Equal(128, geometry.BankWidthBytes);
        Assert.Equal(2UL, geometry.Generation.Value);
        Assert.Equal(4, ((Array)Field(memory, "bankOccupied")).Length);
        Assert.Equal(4, ((Array)Field(memory, "bankLastAccessCycle")).Length);
        Assert.Equal(4, ((Array)Field(memory, "bankQueues")).Length);
        Assert.NotSame(oldOccupied, Field(memory, "bankOccupied"));
        Assert.NotSame(oldLastAccess, Field(memory, "bankLastAccessCycle"));
        Assert.NotSame(oldQueues, Field(memory, "bankQueues"));
        Assert.NotSame(oldPorts, Field(memory, "_portStates"));
        Assert.Equal(0, memory.CurrentQueuedRequests);
    }

    [Fact]
    public void SuccessfulReplacementsIssueExactMonotonicSuccessors()
    {
        MemorySubsystem memory = CreateMemory();

        Assert.True(memory.TryReplacePhysicalMemoryBankGeometry(2, 32).IsApplied);
        Assert.Equal(2UL,
            memory.PublishedPhysicalBankGeometry.Generation.Value);
        Assert.True(memory.TryReplacePhysicalMemoryBankGeometry(3, 96).IsApplied);
        Assert.Equal(3UL,
            memory.PublishedPhysicalBankGeometry.Generation.Value);
    }

    [Fact]
    public void InvalidCountAndWidthWinBeforeBusyWithoutMutation()
    {
        MemorySubsystem memory = CreateMemory();
        byte[] buffer = new byte[8];
        MemorySubsystem.MemoryRequestToken token =
            memory.EnqueueRead(0, 0, buffer.Length, buffer);
        PhysicalMemoryBankGeometry before =
            memory.PublishedPhysicalBankGeometry;

        AssertRejected(
            memory.TryReplacePhysicalMemoryBankGeometry(0, 0),
            MemoryBankGeometryUpdateRejectReason.InvalidBankCount);
        AssertRejected(
            memory.TryReplacePhysicalMemoryBankGeometry(4, 0),
            MemoryBankGeometryUpdateRejectReason.InvalidBankWidth);
        Assert.Equal(before, memory.PublishedPhysicalBankGeometry);
        Assert.Equal(1, memory.CurrentQueuedRequests);
        Assert.True(memory.CancelPendingRequest(token));
    }

    [Fact]
    public void LegacyAndControllerNativeLiveRequestsProduceBusy()
    {
        MemorySubsystem legacyBusy = CreateMemory();
        byte[] buffer = new byte[8];
        MemorySubsystem.MemoryRequestToken legacyToken =
            legacyBusy.EnqueueRead(0, 64, buffer.Length, buffer);
        AssertRejected(
            legacyBusy.TryReplacePhysicalMemoryBankGeometry(4, 64),
            MemoryBankGeometryUpdateRejectReason.Busy);
        Assert.True(legacyBusy.CancelPendingRequest(legacyToken));

        MemorySubsystem controllerBusy = CreateMemory();
        MemoryAdmissionResult admission =
            controllerBusy.CycleController.TryAcceptSingleLaneScalarLoad(
                0,
                64,
                8);
        Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
        AssertRejected(
            controllerBusy.TryReplacePhysicalMemoryBankGeometry(4, 64),
            MemoryBankGeometryUpdateRejectReason.Busy);
        Assert.True(controllerBusy.CycleController.TryCancel(admission.RequestId));
    }

    [Fact]
    public void GenerationExhaustionWinsBeforePlatformRejectionAndDoesNotWrap()
    {
        MemorySubsystem memory = CreateMemory();
        MemoryBankGeometryGeneration exhausted =
            MemoryBankGeometryGeneration.Create(ulong.MaxValue);
        SetField(memory,
            "_lastIssuedPhysicalBankGeometryGeneration",
            ulong.MaxValue);
        SetField(memory,
            "_publishedPhysicalBankGeometry",
            PhysicalMemoryBankGeometry.Create(8, 64, exhausted));

        MemoryBankGeometryUpdateResult result =
            memory.TryReplacePhysicalMemoryBankGeometry(int.MaxValue, 64);

        AssertRejected(
            result,
            MemoryBankGeometryUpdateRejectReason.GenerationExhausted);
        Assert.Equal(
            ulong.MaxValue,
            memory.PublishedPhysicalBankGeometry.Generation.Value);
        Assert.Equal(8, memory.NumBanks);
    }

    [Fact]
    public void PlatformRejectionConsumesNoGenerationOrOwnerState()
    {
        MemorySubsystem memory = CreateMemory();
        PhysicalMemoryBankGeometry before =
            memory.PublishedPhysicalBankGeometry;
        object queuesBefore = Field(memory, "bankQueues");

        AssertRejected(
            memory.TryReplacePhysicalMemoryBankGeometry(int.MaxValue, 64),
            MemoryBankGeometryUpdateRejectReason.PlatformRejected);
        Assert.Equal(before, memory.PublishedPhysicalBankGeometry);
        Assert.Same(queuesBefore, Field(memory, "bankQueues"));
        Assert.Equal(8, memory.NumBanks);
        Assert.Equal(64, memory.BankWidthBytes);

        Assert.True(memory.TryReplacePhysicalMemoryBankGeometry(4, 128).IsApplied);
        Assert.Equal(
            before.Generation.Value + 1UL,
            memory.PublishedPhysicalBankGeometry.Generation.Value);
    }

    [Fact]
    public void CompatibilitySettersRetainBehaviorAndDoNotPublish()
    {
        MemorySubsystem memory = CreateMemory();
        PhysicalMemoryBankGeometry published =
            memory.PublishedPhysicalBankGeometry;

        memory.NumBanks = 0;
        memory.BankWidthBytes = -17;

        Assert.Equal(1, memory.NumBanks);
        Assert.Equal(-17, memory.BankWidthBytes);
        Assert.Equal(published, memory.PublishedPhysicalBankGeometry);
    }

    [Fact]
    public void SourceFreezesControllerOuterOwnerInnerAndExactBusyStores()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string controller = Controller(root);

        Assert.Matches(
            @"(?s)internal MemoryBankGeometryUpdateResult TryReplacePhysicalMemoryBankGeometry\(.*?lock \(_gate\).*?_readQueue\.Count == 0.*?_scalarStoreQueue\.Count == 0.*?_outstanding\.Count == 0.*?_nextCompletions\.Count == 0.*?_publishedCompletions\.Count == 0.*?TryReplacePhysicalMemoryBankGeometryUnderControllerGate",
            controller);
        Assert.Matches(
            @"(?s)TryReplacePhysicalMemoryBankGeometryUnderControllerGate\(.*?lock \(geometryLifecycleGate\).*?IsBankCountRepresentable.*?IsBankWidthRepresentable.*?controllerIsQuiescent.*?IsPhysicalBankGeometryOwnerQuiescent.*?GenerationExhausted.*?TryPreparePhysicalBankTopologyCandidate.*?PlatformRejected.*?MemoryBankGeometryUpdateResult\.Applied",
            subsystem);
        Assert.Contains("foreach (MemoryRequestToken token in pendingRequests.Values)",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("foreach (Queue<BankRequest> queue in bankQueues)",
            subsystem, StringComparison.Ordinal);
        Assert.Contains("if (TRB.OutstandingCount != 0)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("foreach (bool active in bankOccupied)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("foreach (PortState port in _portStates)", subsystem,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CandidateIsFullyPreparedBeforePublicationAssignments()
    {
        string subsystem = Subsystem(FindRepositoryRoot());

        Order(
            subsystem,
            "TryPreparePhysicalBankTopologyCandidate(",
            "ulong nextGenerationRaw =",
            "PhysicalMemoryBankGeometry nextGeometry =",
            "_numBanks = bankCount;",
            "bankQueues = candidate.BankQueues;",
            "_publishedPhysicalBankGeometry = nextGeometry;",
            "_lastIssuedPhysicalBankGeometryGeneration = nextGenerationRaw;",
            "return MemoryBankGeometryUpdateResult.Applied;");
        Assert.Contains("if (bankCount > Array.MaxLength)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("catch (OutOfMemoryException)", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("catch (OverflowException)", subsystem,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LaterBindingStorageLeavesResolverWireAndInvalidBehaviorUnchanged()
    {
        string root = FindRepositoryRoot();
        string subsystem = Subsystem(root);
        string operations = Read(root, "HybridCPU_ISE", "CloseToHSL", "Memory",
            "Subsystem", "MemorySubsystem.Operations.cs");

        Assert.Contains(
            "CapturePublishedPhysicalMemoryBankBindingUnderControllerGate(",
            subsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalMemoryBankResolution", subsystem,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(operations,
            @"PhysicalMemoryBankBinding\s+physicalBankBinding\s*=").Count);
        Assert.Contains("int sanitized = Math.Max(1, value);", subsystem,
            StringComparison.Ordinal);
        Assert.Contains("_bankWidthBytes = value;", subsystem,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankGeometryGeneration", operations,
            StringComparison.Ordinal);
    }


    private static MemorySubsystem CreateMemory()
    {
        Processor processor = default;
        return new MemorySubsystem(ref processor);
    }

    private static void AssertRejected(
        MemoryBankGeometryUpdateResult result,
        MemoryBankGeometryUpdateRejectReason reason)
    {
        Assert.False(result.IsApplied);
        Assert.Equal(reason, result.RejectReason);
    }

    private static object Field(MemorySubsystem memory, string name) =>
        typeof(MemorySubsystem)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(memory)!;

    private static void SetField(
        MemorySubsystem memory,
        string name,
        object value) =>
        typeof(MemorySubsystem)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(memory, value);

    private static string Subsystem(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem",
        "MemorySubsystem.cs");

    private static string Controller(string root) => Read(root,
        "HybridCPU_ISE", "CloseToHSL", "Memory", "Timing",
        "MemoryCycleController.cs");

    private static string Paper(string root) => Read(root, "ResearchPaper",
        "section", "md base",
        "3_Architectural_Overview_and_Frontend_Contract.md");

    private static string Evidence(string root) => Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF12",
        "rf12.6ad-memory-bank-geometry-authoritative-replacement-valid-input-cutover.md");

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current, "ResearchPaper")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static void Order(string text, params string[] markers)
    {
        int cursor = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, cursor + 1, StringComparison.Ordinal);
            Assert.True(next > cursor,
                $"Expected marker after offset {cursor}: {marker}");
            cursor = next;
        }
    }
}
