using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcQueueAdmissionTests
{
    [Fact]
    public void L7SdcQueueAdmission_RequiresGuardBackedDescriptorCapabilityTokenAndCurrentGuard()
    {
        L7SdcPhase07Fixture accepted =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue();

        AcceleratorQueueAdmissionResult valid =
            queue.TryEnqueue(
                accepted.CreateQueueAdmissionRequest(),
                accepted.Evidence);

        L7SdcPhase07Fixture descriptorFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCommandDescriptor unguardedDescriptor =
            descriptorFixture.Descriptor with
            {
                OwnerGuardDecision = default
            };
        AcceleratorQueueAdmissionResult descriptorReject =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                new AcceleratorQueueAdmissionRequest
                {
                    Descriptor = unguardedDescriptor,
                    CapabilityAcceptance = descriptorFixture.CapabilityAcceptance,
                    TokenAdmission = descriptorFixture.Admission
                },
                descriptorFixture.Evidence);

        L7SdcPhase07Fixture capabilityFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorCapabilityAcceptanceResult unguardedCapability =
            L7SdcCapabilityRegistryTests.CreateRegistry().AcceptCapability(
                "matmul.fixture.v1",
                capabilityFixture.Descriptor.OwnerBinding);
        AcceleratorQueueAdmissionResult capabilityReject =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                new AcceleratorQueueAdmissionRequest
                {
                    Descriptor = capabilityFixture.Descriptor,
                    CapabilityAcceptance = unguardedCapability,
                    TokenAdmission = capabilityFixture.Admission
                },
                capabilityFixture.Evidence);

        L7SdcPhase07Fixture tokenFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorTokenAdmissionResult tokenRejectEvidence =
            AcceleratorTokenAdmissionResult.Reject(
                AcceleratorTokenFaultCode.SubmitAdmissionRejected,
                "synthetic rejected token admission");
        AcceleratorQueueAdmissionResult tokenReject =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                new AcceleratorQueueAdmissionRequest
                {
                    Descriptor = tokenFixture.Descriptor,
                    CapabilityAcceptance = tokenFixture.CapabilityAcceptance,
                    TokenAdmission = tokenRejectEvidence
                },
                tokenFixture.Evidence);

        L7SdcPhase07Fixture currentGuardFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorQueueAdmissionResult currentGuardReject =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                currentGuardFixture.CreateQueueAdmissionRequest(),
                currentGuardEvidence: null);

        Assert.True(valid.IsAccepted, valid.Message);
        Assert.Equal(AcceleratorTokenState.Queued, accepted.Token.State);
        Assert.False(valid.CanPublishArchitecturalMemory);
        Assert.False(valid.CanPublishException);

        Assert.True(descriptorReject.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.DescriptorNotGuardBacked, descriptorReject.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, descriptorFixture.Token.State);

        Assert.True(capabilityReject.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, capabilityReject.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, capabilityFixture.Token.State);

        Assert.True(tokenReject.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.SubmitAdmissionRejected, tokenReject.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, tokenFixture.Token.State);

        Assert.True(currentGuardReject.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, currentGuardReject.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, currentGuardFixture.Token.State);
    }

    [Fact]
    public void L7SdcQueueAdmission_QueueFullRejectsWithModelEvidenceCounter()
    {
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue(capacity: 1);
        L7SdcPhase07Fixture first =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        L7SdcPhase07Fixture second =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorQueueAdmissionResult firstResult =
            queue.TryEnqueue(first.CreateQueueAdmissionRequest(), first.Evidence);
        AcceleratorQueueAdmissionResult secondResult =
            queue.TryEnqueue(second.CreateQueueAdmissionRequest(), second.Evidence);

        Assert.True(firstResult.IsAccepted, firstResult.Message);
        Assert.True(secondResult.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.QueueFull, secondResult.FaultCode);
        Assert.Equal(1, queue.QueueFullRejectCount);
        Assert.Equal(1, secondResult.QueueFullRejectCount);
        Assert.Equal(AcceleratorTokenState.Created, second.Token.State);
    }

    [Fact]
    public void L7SdcQueueAdmission_DeviceBusyRejectsWithModelEvidenceCounter()
    {
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue(deviceAvailable: false);
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorQueueAdmissionResult result =
            queue.TryEnqueue(fixture.CreateQueueAdmissionRequest(), fixture.Evidence);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.DeviceBusy, result.FaultCode);
        Assert.Equal(1, queue.DeviceBusyRejectCount);
        Assert.Equal(1, result.DeviceBusyRejectCount);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcQueueAdmission_ConflictPlaceholderRejectionIsModelOnlyAndNonPublishing()
    {
        AcceleratorCommandQueue queue =
            L7SdcPhase07TestFactory.CreateQueue();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorQueueAdmissionResult result =
            queue.TryEnqueue(
                fixture.CreateQueueAdmissionRequest(
                    conflictAccepted: false,
                    conflictEvidenceMessage:
                    "Phase 07 explicit placeholder conflict rejection."),
                fixture.Evidence);

        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, result.FaultCode);
        Assert.False(result.CanPublishArchitecturalMemory);
        Assert.False(result.CanPublishException);
        Assert.False(result.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void L7SdcQueueAdmission_DoesNotRevealQueueFullOrDeviceBusyBeforeCurrentGuard()
    {
        AcceleratorCommandQueue busyQueue =
            L7SdcPhase07TestFactory.CreateQueue(deviceAvailable: false);
        L7SdcPhase07Fixture busyFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorQueueAdmissionResult busyWithoutGuard =
            busyQueue.TryEnqueue(
                busyFixture.CreateQueueAdmissionRequest(),
                currentGuardEvidence: null);

        AcceleratorCommandQueue fullQueue =
            L7SdcPhase07TestFactory.CreateQueue(capacity: 1);
        L7SdcPhase07Fixture first =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        L7SdcPhase07Fixture second =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorQueueAdmissionResult firstAccepted =
            fullQueue.TryEnqueue(first.CreateQueueAdmissionRequest(), first.Evidence);
        AcceleratorQueueAdmissionResult fullWithoutGuard =
            fullQueue.TryEnqueue(
                second.CreateQueueAdmissionRequest(),
                currentGuardEvidence: null);

        Assert.True(busyWithoutGuard.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, busyWithoutGuard.FaultCode);
        Assert.Equal(0, busyQueue.DeviceBusyRejectCount);
        Assert.Equal(AcceleratorTokenState.Created, busyFixture.Token.State);

        Assert.True(firstAccepted.IsAccepted, firstAccepted.Message);
        Assert.True(fullWithoutGuard.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, fullWithoutGuard.FaultCode);
        Assert.Equal(0, fullQueue.QueueFullRejectCount);
        Assert.Equal(AcceleratorTokenState.Created, second.Token.State);
    }

    [Fact]
    public void L7SdcQueueAdmission_OwnerDomainAndEpochDriftBlockSubmitSideEffects()
    {
        L7SdcPhase07Fixture ownerDriftFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        L7SdcPhase07Fixture mappingDriftFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20);
        L7SdcPhase07Fixture iommuDriftFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20);

        AcceleratorQueueAdmissionResult ownerDrift =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                ownerDriftFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateOwnerDriftEvidence(ownerDriftFixture.Descriptor));
        AcceleratorQueueAdmissionResult mappingDrift =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                mappingDriftFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    mappingDriftFixture.Descriptor,
                    mappingEpoch: 11,
                    iommuDomainEpoch: 20));
        AcceleratorQueueAdmissionResult iommuDrift =
            L7SdcPhase07TestFactory.CreateQueue().TryEnqueue(
                iommuDriftFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    iommuDriftFixture.Descriptor,
                    mappingEpoch: 10,
                    iommuDomainEpoch: 21));

        Assert.True(ownerDrift.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.OwnerDomainRejected, ownerDrift.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, ownerDriftFixture.Token.State);

        Assert.True(mappingDrift.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, mappingDrift.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, mappingDriftFixture.Token.State);

        Assert.True(iommuDrift.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.IommuDomainEpochDrift, iommuDrift.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, iommuDriftFixture.Token.State);
    }
}
