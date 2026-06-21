using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcSrfCacheInvalidationTests
{
    [Fact]
    public void L7SdcSrfCacheInvalidation_CommitInvalidatesOverlappingSrfWindow()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x9000,
                L7SdcPhase07TestFactory.Fill(0x31, 0x40));
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xF1);

            var srf = new StreamRegisterFile();
            int register = srf.AllocateRegister(0x9000, elementSize: 1, elementCount: 0x40);
            Assert.True(register >= 0);
            Assert.True(srf.LoadRegister(
                register,
                L7SdcPhase07TestFactory.Fill(0x31, 0x40).AsSpan()));
            Assert.Equal(StreamRegisterFile.RegisterState.Valid, srf.GetRegisterState(register));

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
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(StreamRegisterFile.RegisterState.Invalid, srf.GetRegisterState(register));
            Assert.Equal(1, commit.InvalidationPlan.SrfInvalidationCount);
            Assert.True(commit.InvalidationPlan.HasInvalidationEvidence);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcSrfCacheInvalidation_CommitRecordsCacheOverlapEvidenceAtCoordinatorDecisionPoint()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x9000,
                L7SdcPhase07TestFactory.Fill(0x41, 0x40));
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xD1);

            AcceleratorCommitInvalidationPlan invalidationPlan =
                AcceleratorCommitInvalidationPlan.Observe(
                    cacheWindows: new[]
                    {
                        new AcceleratorMemoryRange(0x9000, 0x20),
                        new AcceleratorMemoryRange(0xA000, 0x20)
                    });
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    invalidationPlan,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(1, commit.InvalidationPlan.CacheInvalidationCount);
            Assert.Contains(
                commit.InvalidationPlan.Records,
                record => record.Target == AcceleratorCommitInvalidationTarget.CacheWindow &&
                          record.Overlapped &&
                          record.Invalidated &&
                          record.Window.Address == 0x9000);
            Assert.Contains(
                commit.InvalidationPlan.Records,
                record => record.Target == AcceleratorCommitInvalidationTarget.CacheWindow &&
                          !record.Overlapped &&
                          !record.Invalidated &&
                          record.Window.Address == 0xA000);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    private static void StageAndCompleteDefault(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        byte value)
    {
        Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
        AcceleratorStagingResult staged =
            staging.StageWrite(
                fixture.Token,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(value, 0x40),
                fixture.Evidence);
        Assert.True(staged.IsAccepted, staged.Message);
        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }
}
