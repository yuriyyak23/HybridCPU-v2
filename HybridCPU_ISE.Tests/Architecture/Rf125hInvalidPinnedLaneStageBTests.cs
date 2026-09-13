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

}
