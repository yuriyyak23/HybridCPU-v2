using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcPhase15RollbackControlsTests
{
    [Fact]
    public void L7SdcPhase15RollbackSwitch_DisablesSubmitAfterGuardWithoutCreatingToken()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        var registry = new AcceleratorCapabilityRegistry();
        registry.RegisterProvider(new MatMulCapabilityProvider());
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore(
            featureSwitch: ExternalAcceleratorFeatureSwitch.SubmitAndBackendExecutionDisabled);

        AcceleratorTokenAdmissionResult admission =
            store.Create(
                descriptor,
                capabilityAcceptance,
                descriptor.OwnerGuardDecision.Evidence);

        Assert.True(capabilityAcceptance.IsAccepted);
        Assert.True(admission.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.SubmitAdmissionRejected, admission.FaultCode);
        Assert.Contains("rollback feature switch", admission.Message);
        Assert.NotNull(admission.GuardDecision);
        Assert.Equal(0, store.Count);
        Assert.False(admission.Handle.IsValid);
    }

    [Fact]
    public void L7SdcPhase15RollbackSwitch_DisablesBackendSubmitBeforeQueueOrStaging()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue();
        var backend = new FakeExternalAcceleratorBackend(
            featureSwitch: ExternalAcceleratorFeatureSwitch.BackendExecutionDisabled);

        AcceleratorBackendResult submit =
            backend.TrySubmit(
                fixture.CreateQueueAdmissionRequest(),
                queue,
                fixture.Evidence);

        Assert.True(submit.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.BackendExecutionUnavailable, submit.FaultCode);
        Assert.Contains("rollback feature switch", submit.Message);
        Assert.Equal(0, queue.Count);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
        Assert.False(submit.CanPublishArchitecturalMemory);
        Assert.False(submit.CanPublishException);
    }

    [Fact]
    public void L7SdcPhase15RollbackSwitch_DisablesBackendTickWithoutDrainingQueuedCommand()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue();
            var enabledBackend = new FakeExternalAcceleratorBackend();
            AcceleratorBackendResult submit =
                enabledBackend.TrySubmit(
                    fixture.CreateQueueAdmissionRequest(),
                    queue,
                    fixture.Evidence);
            var staging = new AcceleratorStagingBuffer();
            var disabledBackend = new FakeExternalAcceleratorBackend(
                featureSwitch: ExternalAcceleratorFeatureSwitch.BackendExecutionDisabled);

            AcceleratorBackendResult tick =
                disabledBackend.Tick(
                    queue,
                    new MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemory),
                    staging,
                    fixture.Evidence);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.BackendExecutionUnavailable, tick.FaultCode);
            Assert.Contains("rollback feature switch", tick.Message);
            Assert.Equal(1, queue.Count);
            Assert.Equal(0, staging.TotalStagedWriteCount);
            Assert.Equal(AcceleratorTokenState.Queued, fixture.Token.State);
            Assert.False(tick.CanPublishArchitecturalMemory);
            Assert.False(tick.CanPublishException);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }
}
