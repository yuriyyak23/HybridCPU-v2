using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf125hInvalidPinnedLaneStageBTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(255)]
    public void InvalidHardPinIsRejectedBeforeShiftAndDoesNotSelectOrIssueALane(byte rawLane)
    {
        var scheduler = new MicroOpScheduler();
        MicroOp candidate = MicroOpTestHelper.CreateScalarALU(0, destReg: 3, src1Reg: 1, src2Reg: 2);
        candidate.Placement = new SlotPlacementMetadata
        {
            RequiredSlotClass = SlotClass.AluClass,
            PinningKind = SlotPinningKind.HardPinned,
            PinnedLaneId = rawLane,
            DomainTag = 0
        };

        bool materialized = scheduler.TestTryMaterializeLane(
            candidate, bundleOccupancy: 0, out int selectedLane, out TypedSlotRejectReason reason);

        Assert.False(materialized);
        Assert.Equal(-1, selectedLane);
        Assert.Equal(TypedSlotRejectReason.InvalidPinnedLane, reason);
        Assert.Null(candidate.PostStageBIssuedAttempt);
    }

    [Fact]
    public void InvalidPinHasItsOwnDiagnosticReasonAndDoesNotAliasLaneConflict()
    {
        var scheduler = new MicroOpScheduler();

        scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.InvalidPinnedLane);

        Assert.Equal(1, scheduler.InvalidPinnedLaneRejects);
        Assert.Equal(0, scheduler.PinnedLaneConflicts);
        Assert.Equal(0, scheduler.LaneConflictRejects);
    }

    [Fact]
    public void SchedulerChecksLaneIdBeforeTheOnlyHardPinShiftAndBothOuterPathsContinue()
    {
        string root = FindRepositoryRoot();
        string admission = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Admission", "MicroOpScheduler.Admission.cs");
        string smt = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Smt", "MicroOpScheduler.SMT.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "MicroOpScheduler.FSPPipeline.cs");

        int validate = admission.IndexOf("LaneId.TryCreate(candidate.Placement.PinnedLaneId", StringComparison.Ordinal);
        int reject = admission.IndexOf("TypedSlotRejectReason.InvalidPinnedLane", StringComparison.Ordinal);
        int shift = admission.IndexOf("1 << lane", StringComparison.Ordinal);
        Assert.True(validate >= 0 && reject > validate && shift > reject);
        Assert.Contains("RecordTypedSlotReject(rejectB, candidate);\n                        continue;", smt, StringComparison.Ordinal);
        Assert.Contains("RecordTypedSlotReject(rejectB, candidate);\n                        continue;", fsp, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] path) => File.ReadAllText(Path.Combine(new[] { root }.Concat(path).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException();
    }
}
