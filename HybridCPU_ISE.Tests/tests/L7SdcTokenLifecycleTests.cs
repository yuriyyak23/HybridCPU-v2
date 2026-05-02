using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcTokenLifecycleTests
{
    [Fact]
    public void L7SdcTokenLifecycle_AcceptedSubmitCreatesOpaqueNonzeroToken()
    {
        TokenFixture fixture = CreateAcceptedToken();

        Assert.True(fixture.Admission.IsAccepted, fixture.Admission.Message);
        Assert.True(fixture.Admission.Handle.IsValid);
        Assert.NotEqual(0UL, fixture.Admission.Handle.Value);
        Assert.Equal(1, fixture.Store.Count);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
        Assert.False(fixture.Token.HasBackendExecution);
        Assert.False(fixture.Token.HasQueueExecution);
        Assert.False(fixture.Token.HasStagedWrites);
        Assert.False(fixture.Token.HasArchitecturalCommit);
        Assert.False(fixture.Token.UserVisiblePublicationAllowed);
    }

    [Fact]
    public void L7SdcTokenLifecycle_LegalModelTransitionsReachDeviceCompleteWithoutCommitAuthority()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorToken token = fixture.Token;

        Assert.True(token.MarkValidated(fixture.Evidence).Succeeded);
        Assert.True(token.MarkQueued(fixture.Evidence).Succeeded);
        Assert.True(token.MarkRunning(fixture.Evidence).Succeeded);
        Assert.True(token.MarkDeviceComplete(fixture.Evidence).Succeeded);
        AcceleratorTokenTransition commitPending =
            token.MarkCommitPending(fixture.Evidence);

        Assert.True(commitPending.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, commitPending.FaultCode);
        Assert.Equal(AcceleratorTokenState.DeviceComplete, token.State);
        Assert.False(token.HasBackendExecution);
        Assert.False(token.HasQueueExecution);
        Assert.False(token.HasStagedWrites);
        Assert.False(token.HasArchitecturalCommit);
    }

    [Fact]
    public void L7SdcTokenLifecycle_IllegalTransitionRejectsWithoutStateChange()
    {
        TokenFixture fixture = CreateAcceptedToken();

        AcceleratorTokenTransition transition =
            fixture.Token.MarkRunning(fixture.Evidence);
        AcceleratorTokenTransition commitPending =
            fixture.Token.MarkCommitPending(fixture.Evidence);
        AcceleratorTokenTransition committed =
            fixture.Token.MarkCommitted(fixture.Evidence);

        Assert.True(transition.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.IllegalTransition, transition.FaultCode);
        Assert.True(commitPending.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.IllegalTransition, commitPending.FaultCode);
        Assert.True(committed.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.IllegalTransition, committed.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcTokenLifecycle_TokenAloneCannotBecomeCommitted()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorToken token = fixture.Token;
        token.MarkValidated(fixture.Evidence);
        token.MarkQueued(fixture.Evidence);
        token.MarkRunning(fixture.Evidence);
        token.MarkDeviceComplete(fixture.Evidence);
        AcceleratorTokenTransition commitPending =
            token.MarkCommitPending(fixture.Evidence);

        AcceleratorTokenTransition transition =
            token.MarkCommitted(fixture.Evidence);
        AcceleratorTokenLookupResult commitPublication =
            fixture.Store.TryCommitPublication(token.Handle, fixture.Evidence);

        Assert.True(commitPending.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, commitPending.FaultCode);
        Assert.True(transition.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, transition.FaultCode);
        Assert.True(commitPublication.IsRejected);
        Assert.False(commitPublication.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.DeviceComplete, token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, token.State);
    }

    [Theory]
    [InlineData(AcceleratorTokenState.Faulted)]
    [InlineData(AcceleratorTokenState.Canceled)]
    [InlineData(AcceleratorTokenState.TimedOut)]
    [InlineData(AcceleratorTokenState.Abandoned)]
    public void L7SdcTokenLifecycle_TerminalTokensCannotBecomeCommitted(
        AcceleratorTokenState terminalState)
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorToken token = fixture.Token;

        switch (terminalState)
        {
            case AcceleratorTokenState.Faulted:
                token.MarkFaulted(
                    AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                    fixture.Evidence);
                break;
            case AcceleratorTokenState.Canceled:
                token.MarkCanceled(fixture.Evidence);
                break;
            case AcceleratorTokenState.TimedOut:
                AcceleratorTokenTransition timeout =
                    token.MarkTimedOut(fixture.Evidence);
                Assert.True(timeout.Succeeded, timeout.Message);
                Assert.Equal(AcceleratorTokenFaultCode.TimedOut, token.FaultCode);
                break;
            case AcceleratorTokenState.Abandoned:
                token.MarkAbandoned(fixture.Evidence);
                break;
        }

        AcceleratorTokenTransition transition =
            token.MarkCommitted(fixture.Evidence);
        AcceleratorTokenTransition commitPending =
            token.MarkCommitPending(fixture.Evidence);

        Assert.Equal(terminalState, token.State);
        Assert.True(transition.Rejected);
        Assert.True(commitPending.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.TerminalState, transition.FaultCode);
        Assert.Equal(AcceleratorTokenFaultCode.TerminalState, commitPending.FaultCode);
        Assert.NotEqual(AcceleratorTokenState.Committed, token.State);
    }

    [Fact]
    public void L7SdcTokenLifecycle_ZeroHandleIsInvalid()
    {
        TokenFixture fixture = CreateAcceptedToken();

        AcceleratorTokenLookupResult lookup =
            fixture.Store.TryLookup(AcceleratorTokenHandle.Invalid, fixture.Evidence);

        Assert.True(lookup.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.InvalidHandle, lookup.FaultCode);
    }

    [Fact]
    public void L7SdcTokenLifecycle_TokenCreationRequiresDescriptorCapabilityAndSubmitGuards()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCapabilityRegistry registry =
            L7SdcCapabilityRegistryTests.CreateRegistry();
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore();

        AcceleratorTokenAdmissionResult accepted =
            store.Create(
                descriptor,
                capabilityAcceptance,
                descriptor.OwnerGuardDecision.Evidence);
        AcceleratorTokenAdmissionResult missingSubmitGuard =
            new AcceleratorTokenStore().Create(
                descriptor,
                capabilityAcceptance,
                submitGuardEvidence: null);
        AcceleratorTokenAdmissionResult missingCapabilityGuard =
            new AcceleratorTokenStore().Create(
                descriptor,
                registry.AcceptCapability("matmul.fixture.v1", descriptor.OwnerBinding),
                descriptor.OwnerGuardDecision.Evidence);

        Assert.True(accepted.IsAccepted, accepted.Message);
        Assert.True(missingSubmitGuard.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, missingSubmitGuard.FaultCode);
        Assert.True(missingCapabilityGuard.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, missingCapabilityGuard.FaultCode);
    }

    private static TokenFixture CreateAcceptedToken()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            L7SdcCapabilityRegistryTests.CreateRegistry().AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore();
        AcceleratorGuardEvidence evidence = descriptor.OwnerGuardDecision.Evidence!;
        AcceleratorTokenAdmissionResult admission =
            store.Create(descriptor, capabilityAcceptance, evidence);

        Assert.True(admission.IsAccepted, admission.Message);
        return new TokenFixture(
            store,
            admission.Token!,
            admission,
            descriptor,
            capabilityAcceptance,
            evidence);
    }

    private sealed record TokenFixture(
        AcceleratorTokenStore Store,
        AcceleratorToken Token,
        AcceleratorTokenAdmissionResult Admission,
        AcceleratorCommandDescriptor Descriptor,
        AcceleratorCapabilityAcceptanceResult CapabilityAcceptance,
        AcceleratorGuardEvidence Evidence);
}
