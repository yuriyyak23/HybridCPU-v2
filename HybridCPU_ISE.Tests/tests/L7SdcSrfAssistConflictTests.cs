using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcSrfAssistConflictTests
{
    [Fact]
    public void L7SdcSrfAssistConflict_SrfWarmBeforeSubmitRejectsAcceleratorWriteOverlap()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });

        AcceleratorConflictDecision warm =
            manager.NotifySrfWarmWindow(
                new AcceleratorMemoryRange(0x9010, 0x10),
                fixture.Evidence);
        AcceleratorConflictDecision reservation =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);

        Assert.True(warm.IsAccepted, warm.Message);
        Assert.Equal(1, manager.SrfWarmWindowCount);
        Assert.True(reservation.IsRejected);
        Assert.Equal(
            AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
            reservation.ConflictClass);
        Assert.Equal(0, manager.ActiveReservationCount);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcSrfAssistConflict_SrfWarmAndAssistIngressAfterActiveWriteReject()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);

        AcceleratorConflictDecision srfWarm =
            manager.NotifySrfWarmWindow(
                new AcceleratorMemoryRange(0x9020, 0x10),
                fixture.Evidence);
        AcceleratorConflictDecision assistIngress =
            manager.NotifyAssistIngressWindow(
                new AcceleratorMemoryRange(0x9030, 0x8),
                fixture.Evidence);

        Assert.True(srfWarm.IsRejected);
        Assert.Equal(
            AcceleratorConflictClass.AcceleratorWriteOverlapsSrfWarmedWindow,
            srfWarm.ConflictClass);
        Assert.True(assistIngress.IsRejected);
        Assert.Equal(
            AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
            assistIngress.ConflictClass);
        Assert.Equal(0, manager.SrfWarmWindowCount);
        Assert.Equal(0, manager.AssistIngressWindowCount);
        Assert.Equal(1, manager.ActiveReservationCount);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcSrfAssistConflict_AssistIngressBeforeSubmitRejectsFutureAcceleratorWrite()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0xA000, 0x40) });

        AcceleratorConflictDecision assist =
            manager.NotifyAssistIngressWindow(
                new AcceleratorMemoryRange(0xA010, 0x10),
                fixture.Evidence);
        AcceleratorConflictDecision reservation =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);

        Assert.True(assist.IsAccepted, assist.Message);
        Assert.Equal(1, manager.AssistIngressWindowCount);
        Assert.True(reservation.IsRejected);
        Assert.Equal(
            AcceleratorConflictClass.AssistIngressOverlapsAcceleratorWrite,
            reservation.ConflictClass);
        Assert.Equal(0, manager.ActiveReservationCount);
    }

    [Fact]
    public void L7SdcSrfAssistConflict_CommitUsesPhase08InvalidationAndThenReleasesConflictEvidence()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x9000,
                L7SdcPhase07TestFactory.Fill(0x3A, 0x40));
            var manager = new ExternalAcceleratorConflictManager();
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                    destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
            Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);
            var staging = new AcceleratorStagingBuffer();
            MarkRunningAndComplete(fixture, staging);

            var srf = new StreamRegisterFile();
            int register = srf.AllocateRegister(0x9000, elementSize: 1, elementCount: 0x40);
            Assert.True(register >= 0);
            Assert.True(srf.LoadRegister(
                register,
                L7SdcPhase07TestFactory.Fill(0x3A, 0x40).AsSpan()));
            AcceleratorCommitInvalidationPlan invalidationPlan =
                AcceleratorCommitInvalidationPlan.Observe(
                    srfWindows: new[] { new AcceleratorMemoryRange(0x9000, 0x40) },
                    streamRegisterFile: srf);

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    invalidationPlan,
                    commitConflictPlaceholderAccepted: false,
                    conflictManager: manager);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(StreamRegisterFile.RegisterState.Invalid, srf.GetRegisterState(register));
            Assert.Equal(1, commit.InvalidationPlan.SrfInvalidationCount);
            Assert.Equal(0, manager.ActiveReservationCount);
            Assert.True(commit.CanPublishArchitecturalMemory);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    private static void MarkRunningAndComplete(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging)
    {
        Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
        AcceleratorStagingResult staged =
            staging.StageWrite(
                fixture.Token,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(0x74, 0x40),
                fixture.Evidence);
        Assert.True(staged.IsAccepted, staged.Message);
        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }
}
