using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcDirectWriteViolationTests
{
    [Fact]
    public void L7SdcDirectWriteViolation_DetectedFaultedAndCannotMutateArchitecturalMemory()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x5A, 64);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue();
            AcceleratorQueueAdmissionResult admission =
                queue.TryEnqueue(
                    fixture.CreateQueueAdmissionRequest(),
                    fixture.Evidence);
            var backend = new DirectWriteViolationBackend();

            AcceleratorBackendResult violation =
                backend.AttemptDirectWriteForTest(
                    admission.Command!,
                    fixture.Evidence,
                    address: 0x9000,
                    attemptedData: L7SdcPhase07TestFactory.Fill(0xE7, 64));
            AcceleratorTokenLookupResult commit =
                fixture.Store.TryCommitPublication(fixture.Token.Handle, fixture.Evidence);

            Assert.True(admission.IsAccepted, admission.Message);
            Assert.True(backend.IsTestOnly);
            Assert.True(violation.IsFaulted, violation.Message);
            Assert.True(violation.DirectWriteViolationDetected);
            Assert.Equal(AcceleratorTokenFaultCode.DirectWriteViolation, violation.FaultCode);
            Assert.Equal(AcceleratorTokenState.Faulted, fixture.Token.State);
            Assert.False(violation.CanPublishArchitecturalMemory);
            Assert.False(violation.CanPublishException);
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.True(commit.IsRejected);
            Assert.False(commit.UserVisiblePublicationAllowed);
            Assert.NotEqual(AcceleratorTokenState.Committed, fixture.Token.State);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcDirectWriteViolation_StaleGuardRejectsBeforeFaultSideEffect()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue();
        AcceleratorQueueAdmissionResult admission =
            queue.TryEnqueue(
                fixture.CreateQueueAdmissionRequest(),
                fixture.Evidence);
        var backend = new DirectWriteViolationBackend();

        AcceleratorBackendResult violation =
            backend.AttemptDirectWriteForTest(
                admission.Command!,
                L7SdcPhase07TestFactory.CreateOwnerDriftEvidence(fixture.Descriptor),
                address: 0x9000,
                attemptedData: L7SdcPhase07TestFactory.Fill(0xE7, 16));

        Assert.True(admission.IsAccepted, admission.Message);
        Assert.True(violation.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.OwnerDomainRejected, violation.FaultCode);
        Assert.False(violation.DirectWriteViolationDetected);
        Assert.Equal(AcceleratorTokenState.Queued, fixture.Token.State);
    }
}
