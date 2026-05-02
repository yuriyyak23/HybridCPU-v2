using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcPollWaitCancelFenceTests
{
    [Fact]
    public void L7SdcPollWaitCancelFence_PollReturnsPackedStatusWithoutCommit()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x51, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xA7);

            AcceleratorTokenLookupResult poll =
                fixture.Store.TryPoll(fixture.Token.Handle, fixture.Evidence);
            AcceleratorTokenStatusWord unpacked =
                AcceleratorTokenStatusWord.Unpack(poll.PackedStatusWord);

            Assert.True(poll.IsAllowed, poll.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, unpacked.State);
            Assert.False(unpacked.IsTerminal);
            Assert.False(poll.UserVisiblePublicationAllowed);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, fixture.Token.State);
            Assert.NotEqual(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_PollRequiresFreshGuardBeforeStatusExposure()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20);
        AcceleratorGuardEvidence mappingDrift =
            L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                fixture.Descriptor,
                mappingEpoch: 11,
                iommuDomainEpoch: 20);

        AcceleratorTokenLookupResult missingGuard =
            fixture.Store.TryPoll(fixture.Token.Handle, currentGuardEvidence: null);
        AcceleratorTokenLookupResult staleGuard =
            fixture.Store.TryPoll(fixture.Token.Handle, mappingDrift);
        AcceleratorTokenLookupResult handleAlone =
            fixture.Store.TryPoll(
                fixture.Token.Handle,
                AcceleratorGuardEvidence.FromEvidencePlane(
                    AcceleratorGuardEvidenceSource.TokenHandle,
                    fixture.Descriptor.OwnerBinding,
                    evidenceIdentity: fixture.Token.Handle.Value));

        Assert.True(missingGuard.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, missingGuard.FaultCode);
        Assert.False(AcceleratorRegisterAbi.FromStatusLookup(missingGuard).WritesRegister);
        Assert.False(missingGuard.UserVisiblePublicationAllowed);
        Assert.True(staleGuard.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, staleGuard.FaultCode);
        Assert.False(AcceleratorRegisterAbi.FromStatusLookup(staleGuard).WritesRegister);
        Assert.False(staleGuard.UserVisiblePublicationAllowed);
        Assert.True(handleAlone.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, handleAlone.FaultCode);
        Assert.False(AcceleratorRegisterAbi.FromStatusLookup(handleAlone).WritesRegister);
        Assert.False(handleAlone.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_WaitRequiresFreshGuardBeforeStatusExposure()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20);
        AcceleratorGuardEvidence mappingDrift =
            L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                fixture.Descriptor,
                mappingEpoch: 11,
                iommuDomainEpoch: 20);

        AcceleratorTokenLookupResult wait =
            fixture.Store.TryWait(
                fixture.Token.Handle,
                mappingDrift,
                AcceleratorWaitPolicy.TimedOutAndMarkToken(timeoutTicks: 8));

        Assert.True(wait.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, wait.FaultCode);
        Assert.False(AcceleratorRegisterAbi.FromStatusLookup(wait).WritesRegister);
        Assert.False(wait.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_WaitObservesCompletionAndTimeoutWithoutPublication()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x32, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture completed =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var completedStaging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(completed, completedStaging, 0xB4);

            AcceleratorTokenLookupResult waitCompleted =
                completed.Store.TryWait(
                    completed.Token.Handle,
                    completed.Evidence,
                    AcceleratorWaitPolicy.ObserveOnly);

            Assert.True(waitCompleted.IsAllowed, waitCompleted.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, waitCompleted.StatusWord.State);
            Assert.Equal(AcceleratorTokenFaultCode.None, waitCompleted.StatusWord.FaultCode);
            Assert.False(waitCompleted.UserVisiblePublicationAllowed);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            L7SdcPhase07Fixture active =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            AcceleratorTokenLookupResult timeoutObserved =
                active.Store.TryWait(
                    active.Token.Handle,
                    active.Evidence,
                    AcceleratorWaitPolicy.TimedOutNoStateChange(timeoutTicks: 4));
            AcceleratorTokenLookupResult timeoutMarked =
                active.Store.TryWait(
                    active.Token.Handle,
                    active.Evidence,
                    AcceleratorWaitPolicy.TimedOutAndMarkToken(timeoutTicks: 5));

            Assert.True(timeoutObserved.IsAllowed, timeoutObserved.Message);
            Assert.Equal(AcceleratorTokenFaultCode.TimedOut, timeoutObserved.StatusWord.FaultCode);
            Assert.True(timeoutObserved.StatusWord.Flags.HasFlag(AcceleratorTokenStatusFlags.TimeoutObserved));
            Assert.Equal(AcceleratorTokenState.Created, timeoutObserved.StatusWord.State);
            Assert.Equal(AcceleratorTokenState.TimedOut, active.Token.State);
            Assert.True(timeoutMarked.StatusWord.IsTerminal);
            Assert.NotEqual(AcceleratorTokenState.Committed, active.Token.State);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Theory]
    [InlineData(AcceleratorTokenState.Faulted)]
    [InlineData(AcceleratorTokenState.Canceled)]
    [InlineData(AcceleratorTokenState.TimedOut)]
    [InlineData(AcceleratorTokenState.Abandoned)]
    public void L7SdcPollWaitCancelFence_WaitReturnsFinalTerminalStatusWithoutCommit(
        AcceleratorTokenState terminalState)
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        MarkTerminal(fixture, terminalState);

        AcceleratorTokenLookupResult wait =
            fixture.Store.TryWait(
                fixture.Token.Handle,
                fixture.Evidence,
                AcceleratorWaitPolicy.ObserveOnly);

        Assert.True(wait.IsAllowed, wait.Message);
        Assert.Equal(terminalState, wait.StatusWord.State);
        Assert.True(wait.StatusWord.IsTerminal);
        Assert.False(wait.UserVisiblePublicationAllowed);
        Assert.NotEqual(AcceleratorTokenState.Committed, fixture.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_CancelQueuedAndRunningTokensWithGuardedPolicy()
    {
        L7SdcPhase07Fixture queued =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        Assert.True(queued.Token.MarkValidated(queued.Evidence).Succeeded);
        Assert.True(queued.Token.MarkQueued(queued.Evidence).Succeeded);

        AcceleratorTokenLookupResult queuedCancel =
            queued.Store.TryCancel(
                queued.Token.Handle,
                queued.Evidence,
                AcceleratorCancelPolicy.Cooperative);

        Assert.True(queuedCancel.IsAllowed, queuedCancel.Message);
        Assert.Equal(AcceleratorTokenState.Canceled, queued.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, queued.Token.State);

        L7SdcPhase07Fixture cooperativeRunning =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        MarkRunning(cooperativeRunning);
        AcceleratorTokenLookupResult runningCancel =
            cooperativeRunning.Store.TryCancel(
                cooperativeRunning.Token.Handle,
                cooperativeRunning.Evidence,
                AcceleratorCancelPolicy.Cooperative);

        Assert.True(runningCancel.IsAllowed, runningCancel.Message);
        Assert.Equal(AcceleratorTokenState.Canceled, cooperativeRunning.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, cooperativeRunning.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_NonCooperativeRunningCancelRejectsOrFaultsNeverCommits()
    {
        L7SdcPhase07Fixture rejected =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        MarkRunning(rejected);
        L7SdcPhase07Fixture faulted =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        MarkRunning(faulted);

        AcceleratorTokenLookupResult reject =
            rejected.Store.TryCancel(
                rejected.Token.Handle,
                rejected.Evidence,
                AcceleratorCancelPolicy.RejectRunning);
        AcceleratorTokenLookupResult fault =
            faulted.Store.TryCancel(
                faulted.Token.Handle,
                faulted.Evidence,
                AcceleratorCancelPolicy.FaultRunning);

        Assert.True(reject.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.CancelRejected, reject.FaultCode);
        Assert.Equal(AcceleratorTokenState.Running, rejected.Token.State);
        Assert.True(fault.IsAllowed, fault.Message);
        Assert.Equal(AcceleratorTokenState.Faulted, faulted.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, rejected.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, faulted.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_CancelCannotAuthorizeCommitOrStagedPublication()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x75, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            MarkRunning(fixture);
            AcceleratorStagingResult staged =
                staging.StageWrite(
                    fixture.Token,
                    fixture.Descriptor.DestinationRanges[0],
                    L7SdcPhase07TestFactory.Fill(0xC9, 0x40),
                    fixture.Evidence);
            Assert.True(staged.IsAccepted, staged.Message);

            AcceleratorTokenLookupResult cancel =
                fixture.Store.TryCancel(
                    fixture.Token.Handle,
                    fixture.Evidence,
                    AcceleratorCancelPolicy.Cooperative);
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(cancel.IsAllowed, cancel.Message);
            Assert.Equal(AcceleratorTokenState.Canceled, fixture.Token.State);
            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.TerminalState, commit.FaultCode);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_FenceCommitsCompletedTokenOnlyThroughCommitCoordinator()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x23, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xD1);

            AcceleratorFenceResult observeOnly =
                new AcceleratorFenceCoordinator().TryFence(
                    fixture.Store,
                    AcceleratorFenceScope.ForToken(
                        fixture.Token.Handle,
                        commitCompletedTokens: false),
                    fixture.Evidence,
                    staging,
                    Processor.MainMemory);

            Assert.True(observeOnly.Succeeded, observeOnly.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, fixture.Token.State);
            Assert.False(observeOnly.CanPublishArchitecturalMemory);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            AcceleratorFenceResult commitFence =
                new AcceleratorFenceCoordinator().TryFence(
                    fixture.Store,
                    AcceleratorFenceScope.ForToken(
                        fixture.Token.Handle,
                        commitCompletedTokens: true),
                    fixture.Evidence,
                    staging,
                    Processor.MainMemory);

            Assert.True(commitFence.Succeeded, commitFence.Message);
            Assert.Equal(1, commitFence.CommittedCount);
            Assert.True(commitFence.CanPublishArchitecturalMemory);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(
                L7SdcPhase07TestFactory.Fill(0xD1, 0x40),
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, 0x40));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_ModelFenceSuccessDoesNotMakeAccelFenceExecutable()
    {
        AcceleratorFenceResult modelFence =
            new AcceleratorFenceCoordinator().TryFence(
                new AcceleratorTokenStore(),
                AcceleratorFenceScope.ForTokens(Array.Empty<AcceleratorTokenHandle>()),
                currentGuardEvidence: null);
        var carrier = new AcceleratorFenceMicroOp();
        var core = new Processor.CPU_Core(0);

        Assert.True(modelFence.Succeeded, modelFence.Message);
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => carrier.Execute(ref core));

        Assert.Contains("fail closed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("architectural rd writeback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_FenceRejectsCommitWhenConflictPlaceholderNotAccepted()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0xA9, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xBC);

            AcceleratorFenceResult fence =
                new AcceleratorFenceCoordinator().TryFence(
                    fixture.Store,
                    AcceleratorFenceScope.ForToken(
                        fixture.Token.Handle,
                        commitCompletedTokens: true,
                        commitConflictPlaceholderAccepted: false),
                    fixture.Evidence,
                    staging,
                    Processor.MainMemory);

            Assert.True(fence.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitConflictRejected, fence.FaultCode);
            Assert.Equal(0, fence.CommittedCount);
            Assert.False(fence.CanPublishArchitecturalMemory);
            Assert.Equal(AcceleratorTokenState.Faulted, fixture.Token.State);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_FenceRejectsOrCancelsScopedActiveTokensConservatively()
    {
        L7SdcPhase07Fixture rejected =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        Assert.True(rejected.Token.MarkValidated(rejected.Evidence).Succeeded);
        Assert.True(rejected.Token.MarkQueued(rejected.Evidence).Succeeded);
        L7SdcPhase07Fixture canceled =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        Assert.True(canceled.Token.MarkValidated(canceled.Evidence).Succeeded);
        Assert.True(canceled.Token.MarkQueued(canceled.Evidence).Succeeded);

        var coordinator = new AcceleratorFenceCoordinator();
        AcceleratorFenceResult rejectFence =
            coordinator.TryFence(
                rejected.Store,
                AcceleratorFenceScope.ForToken(rejected.Token.Handle),
                rejected.Evidence);
        AcceleratorFenceResult cancelFence =
            coordinator.TryFence(
                canceled.Store,
                AcceleratorFenceScope.ForToken(
                    canceled.Token.Handle,
                    activeTokenDisposition: AcceleratorFenceActiveTokenDisposition.Cancel),
                canceled.Evidence);

        Assert.True(rejectFence.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.FenceRejected, rejectFence.FaultCode);
        Assert.Equal(AcceleratorTokenState.Queued, rejected.Token.State);
        Assert.True(cancelFence.Succeeded, cancelFence.Message);
        Assert.Equal(AcceleratorTokenState.Canceled, canceled.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, rejected.Token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, canceled.Token.State);
    }

    [Fact]
    public void L7SdcPollWaitCancelFence_FenceCannotUseTokenHandleAloneAsAuthority()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorFenceResult result =
            new AcceleratorFenceCoordinator().TryFence(
                fixture.Store,
                AcceleratorFenceScope.ForToken(fixture.Token.Handle),
                AcceleratorGuardEvidence.FromEvidencePlane(
                    AcceleratorGuardEvidenceSource.TokenHandle,
                    fixture.Descriptor.OwnerBinding,
                    evidenceIdentity: fixture.Token.Handle.Value));

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, result.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    private static void StageAndCompleteDefault(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        byte value)
    {
        MarkRunning(fixture);
        AcceleratorStagingResult staged =
            staging.StageWrite(
                fixture.Token,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(value, 0x40),
                fixture.Evidence);
        Assert.True(staged.IsAccepted, staged.Message);
        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }

    private static void MarkRunning(L7SdcPhase07Fixture fixture)
    {
        Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
    }

    private static void MarkTerminal(
        L7SdcPhase07Fixture fixture,
        AcceleratorTokenState terminalState)
    {
        switch (terminalState)
        {
            case AcceleratorTokenState.Faulted:
                Assert.True(
                    fixture.Token.MarkFaulted(
                        AcceleratorTokenFaultCode.BackendRejected,
                        fixture.Evidence).Succeeded);
                break;
            case AcceleratorTokenState.Canceled:
                Assert.True(fixture.Token.MarkCanceled(fixture.Evidence).Succeeded);
                break;
            case AcceleratorTokenState.TimedOut:
                Assert.True(fixture.Token.MarkTimedOut(fixture.Evidence).Succeeded);
                break;
            case AcceleratorTokenState.Abandoned:
                Assert.True(fixture.Token.MarkAbandoned(fixture.Evidence).Succeeded);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminalState));
        }
    }
}
