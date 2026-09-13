using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcRollbackTests
{
    [Fact]
    public void L7SdcRollback_PartialArchitecturalWriteRollsBackAllOrNoneAndFaultsToken()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            var memory = new FailingWriteMemoryArea
            {
                FailAddress = 0x9020,
                MutateBeforeReturningFailure = true
            };
            memory.AllocateMemory(0, 0x10000);
            Processor.MainMemory = memory;
            Processor.Memory = null;

            byte[] originalFirst = L7SdcPhase07TestFactory.Fill(0x10, 16);
            byte[] originalSecond = L7SdcPhase07TestFactory.Fill(0x20, 16);
            Assert.True(memory.TryWritePhysicalRange(0x9000, originalFirst));
            Assert.True(memory.TryWritePhysicalRange(0x9020, originalSecond));

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                    destinationRanges: new[]
                    {
                        new AcceleratorMemoryRange(0x9000, 16),
                        new AcceleratorMemoryRange(0x9020, 16)
                    });
            var staging = new AcceleratorStagingBuffer();
            StageAndComplete(
                fixture,
                staging,
                new AcceleratorMemoryRange(0x9000, 16),
                L7SdcPhase07TestFactory.Fill(0xA1, 16));
            AcceleratorStagingResult secondStage =
                staging.StageWrite(
                    fixture.Token,
                    new AcceleratorMemoryRange(0x9020, 16),
                    L7SdcPhase07TestFactory.Fill(0xB2, 16),
                    fixture.Evidence);
            Assert.True(secondStage.IsAccepted, secondStage.Message);
            Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);

            memory.FailureEnabled = true;
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    memory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitMemoryFault, commit.FaultCode);
            Assert.True(commit.Rollback.RollbackAttempted);
            Assert.True(commit.Rollback.RollbackSucceeded);
            Assert.Equal(1, commit.Rollback.WriteFailureIndex);
            Assert.Equal(AcceleratorTokenState.Faulted, fixture.Token.State);
            Assert.Equal(originalFirst, Read(memory, 0x9000, 16));
            Assert.Equal(originalSecond, Read(memory, 0x9020, 16));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcRollback_PublicRollbackRequiresTokenBoundExactBackupEvidence()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x31, 0x40);
            byte[] protectedBytes =
                L7SdcPhase07TestFactory.Fill(0x42, 0x20);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07TestFactory.WriteMainMemory(0xA000, protectedBytes);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
            Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
            Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
            Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
            PromoteToCommitPendingForRollbackHelper(fixture.Token, fixture.Evidence);

            var forgedBackups = new[]
            {
                new AcceleratorStagedWrite(
                    fixture.Token.Handle,
                    0xA000,
                    L7SdcPhase07TestFactory.Fill(0xEF, 0x20))
            };
            var forgedAttemptedWrites = new[]
            {
                new AcceleratorStagedWrite(
                    fixture.Token.Handle,
                    0xA000,
                    L7SdcPhase07TestFactory.Fill(0xCC, 0x20))
            };

            AcceleratorRollbackRecord rollback =
                new AcceleratorCommitCoordinator().Rollback(
                    fixture.Token,
                    fixture.Descriptor,
                    Processor.MainMemory,
                    forgedBackups,
                    forgedAttemptedWrites,
                    writeFailureIndex: 0,
                    fixture.Evidence);

            Assert.True(rollback.RollbackAttempted);
            Assert.False(rollback.RollbackSucceeded);
            Assert.Equal(AcceleratorTokenState.CommitPending, fixture.Token.State);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.Equal(
                protectedBytes,
                L7SdcPhase07TestFactory.ReadMainMemory(0xA000, protectedBytes.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    private static void StageAndComplete(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        AcceleratorMemoryRange firstRange,
        byte[] firstData)
    {
        Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
        AcceleratorStagingResult firstStage =
            staging.StageWrite(
                fixture.Token,
                firstRange,
                firstData,
                fixture.Evidence);
        Assert.True(firstStage.IsAccepted, firstStage.Message);
    }

    private static void PromoteToCommitPendingForRollbackHelper(
        AcceleratorToken token,
        AcceleratorGuardEvidence evidence)
    {
        MethodInfo? method = typeof(AcceleratorToken).GetMethod(
            "MarkCommitPendingFromCommitCoordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        AcceleratorTokenTransition transition =
            (AcceleratorTokenTransition)method.Invoke(
                token,
                new object[] { evidence })!;
        Assert.True(transition.Succeeded, transition.Message);
    }

    private static byte[] Read(
        Processor.MainMemoryArea memory,
        ulong address,
        int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(memory.TryReadPhysicalRange(address, bytes));
        return bytes;
    }

    private sealed class FailingWriteMemoryArea : Processor.MainMemoryArea
    {
        public bool FailureEnabled { get; set; }

        public ulong FailAddress { get; init; }

        public bool MutateBeforeReturningFailure { get; init; }

        public override bool TryWritePhysicalRange(
            ulong physicalAddress,
            System.ReadOnlySpan<byte> buffer)
        {
            if (FailureEnabled && physicalAddress == FailAddress)
            {
                if (MutateBeforeReturningFailure)
                {
                    _ = base.TryWritePhysicalRange(physicalAddress, buffer);
                }

                FailureEnabled = false;
                return false;
            }

            return base.TryWritePhysicalRange(physicalAddress, buffer);
        }
    }
}
