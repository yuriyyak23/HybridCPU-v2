using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcConflictManagerTests
{
    [Fact]
    public void L7SdcConflictManager_TryReserveOwnsActiveFootprintTruthAndRejectsSecondWriter()
    {
        var manager = new ExternalAcceleratorConflictManager();
        var store = new AcceleratorTokenStore();

        AcceleratorTokenAdmissionResult first =
            AdmitToken(
                store,
                manager,
                new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        AcceleratorTokenAdmissionResult second =
            AdmitToken(
                store,
                manager,
                new[] { new AcceleratorMemoryRange(0x9020, 0x20) });

        Assert.True(first.IsAccepted, first.Message);
        Assert.Equal(1, manager.ActiveReservationCount);
        Assert.True(second.IsNonTrappingReject, second.Message);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, second.FaultCode);
        Assert.Equal(1, manager.ActiveReservationCount);
        Assert.Equal(1, store.Count);
        Assert.Equal(AcceleratorTokenState.Created, first.Token!.State);
        Assert.False(second.Handle.IsValid);
    }

    [Fact]
    public void L7SdcConflictManager_TryReserveIsIdempotentForSameGuardedToken()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorConflictDecision first =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);
        AcceleratorConflictDecision second =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);

        Assert.True(first.IsAccepted, first.Message);
        Assert.True(second.IsAccepted, second.Message);
        Assert.Equal(AcceleratorConflictClass.SubmitReservation, second.ConflictClass);
        Assert.Equal(1, manager.ActiveReservationCount);
    }

    [Fact]
    public void L7SdcConflictManager_OpaqueHandleCollisionIsNotConflictAuthority()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture active =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        L7SdcPhase07Fixture collidingHandle =
            L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                destinationRanges: new[] { new AcceleratorMemoryRange(0xA000, 0x40) });

        AcceleratorConflictDecision activeReservation =
            manager.TryReserveOnSubmit(active.Token, active.Evidence);
        AcceleratorConflictDecision collision =
            manager.TryReserveOnSubmit(collidingHandle.Token, collidingHandle.Evidence);

        Assert.True(activeReservation.IsAccepted, activeReservation.Message);
        Assert.True(collision.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, collision.TokenFaultCode);
        Assert.Equal(1, manager.ActiveReservationCount);
    }

    [Fact]
    public void L7SdcConflictManager_TokenStoreAdmissionWithManagerRejectsConflictingSubmitWithoutVisibleHandle()
    {
        var manager = new ExternalAcceleratorConflictManager();
        var store = new AcceleratorTokenStore();
        AcceleratorTokenAdmissionResult active =
            AdmitToken(
                store,
                manager,
                new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        Assert.True(active.IsAccepted, active.Message);

        AcceleratorTokenAdmissionResult admission =
            AdmitToken(
                store,
                manager,
                new[] { new AcceleratorMemoryRange(0x9030, 0x20) });

        Assert.True(admission.IsNonTrappingReject, admission.Message);
        Assert.Equal(AcceleratorTokenFaultCode.ConflictRejected, admission.FaultCode);
        Assert.False(admission.Handle.IsValid);
        Assert.Equal(1, store.Count);
        Assert.Equal(1, manager.ActiveReservationCount);
    }

    [Fact]
    public void L7SdcConflictManager_AbsentManagerDoesNotImplyGlobalLoadStoreOrdering()
    {
        var store = new AcceleratorTokenStore();

        AcceleratorTokenAdmissionResult first =
            AdmitToken(
                store,
                manager: null,
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9000, 0x40) });
        AcceleratorTokenAdmissionResult second =
            AdmitToken(
                store,
                manager: null,
                destinationRanges: new[] { new AcceleratorMemoryRange(0x9020, 0x20) });

        Assert.True(first.IsAccepted, first.Message);
        Assert.True(second.IsAccepted, second.Message);
        Assert.Equal(2, store.Count);
        Assert.True(first.Handle.IsValid);
        Assert.True(second.Handle.IsValid);
    }

    [Fact]
    public void L7SdcConflictManager_CpuLoadStoreOverlapDecisionsSerializeWithoutCommitAuthority()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        AcceleratorConflictDecision reservation =
            manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence);
        Assert.True(reservation.IsAccepted, reservation.Message);

        AcceleratorConflictDecision storeSource =
            manager.NotifyCpuStore(
                new AcceleratorMemoryRange(0x1008, 0x8),
                fixture.Evidence);
        AcceleratorConflictDecision storeDestination =
            manager.NotifyCpuStore(
                new AcceleratorMemoryRange(0x9010, 0x8),
                fixture.Evidence);
        AcceleratorConflictDecision loadDestination =
            manager.NotifyCpuLoad(
                new AcceleratorMemoryRange(0x9008, 0x8),
                fixture.Evidence);
        AcceleratorTokenLookupResult commitByHandle =
            fixture.Store.TryCommitPublication(fixture.Token.Handle, fixture.Evidence);

        Assert.True(storeSource.RequiresSerialization, storeSource.Message);
        Assert.Equal(AcceleratorConflictClass.CpuStoreOverlapsAcceleratorRead, storeSource.ConflictClass);
        Assert.True(storeDestination.RequiresSerialization, storeDestination.Message);
        Assert.Equal(AcceleratorConflictClass.CpuStoreOverlapsAcceleratorWrite, storeDestination.ConflictClass);
        Assert.True(loadDestination.RequiresSerialization, loadDestination.Message);
        Assert.Equal(AcceleratorConflictClass.CpuLoadOverlapsAcceleratorWrite, loadDestination.ConflictClass);
        Assert.True(commitByHandle.IsRejected);
        Assert.NotEqual(AcceleratorTokenFaultCode.None, commitByHandle.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcConflictManager_ValidateBeforeCommitIsNotCommitAuthority()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            var manager = new ExternalAcceleratorConflictManager();
            Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);
            MarkRunningAndComplete(fixture, staging);

            AcceleratorConflictDecision conflictCommit =
                manager.ValidateBeforeCommit(fixture.Token, fixture.Evidence);
            AcceleratorTokenLookupResult handleCommit =
                fixture.Store.TryCommitPublication(fixture.Token.Handle, fixture.Evidence);
            AcceleratorCommitResult coordinatorCommit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: false,
                    conflictManager: manager);

            Assert.True(conflictCommit.IsAccepted, conflictCommit.Message);
            Assert.False(conflictCommit.CanPublishArchitecturalMemory);
            Assert.True(handleCommit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, handleCommit.FaultCode);
            Assert.True(coordinatorCommit.Succeeded, coordinatorCommit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(0, manager.ActiveReservationCount);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcConflictManager_SerializingBoundaryAndMappingTransitionRejectActiveToken()
    {
        var manager = new ExternalAcceleratorConflictManager();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20);
        Assert.True(manager.TryReserveOnSubmit(fixture.Token, fixture.Evidence).IsAccepted);

        AcceleratorConflictDecision boundary =
            manager.NotifySerializingBoundary(fixture.Token, fixture.Evidence);
        AcceleratorConflictDecision mappingTransition =
            manager.NotifyVmDomainOrMappingTransition(
                fixture.Token,
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    fixture.Descriptor,
                    mappingEpoch: 11,
                    iommuDomainEpoch: 20));

        Assert.True(boundary.IsRejected);
        Assert.Equal(AcceleratorConflictClass.FenceOrSerializingBoundaryWhileTokenActive, boundary.ConflictClass);
        Assert.Equal(AcceleratorTokenFaultCode.FenceRejected, boundary.TokenFaultCode);
        Assert.True(mappingTransition.ShouldFaultToken);
        Assert.Equal(AcceleratorConflictClass.VmDomainMappingTransitionWhileTokenActive, mappingTransition.ConflictClass);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, mappingTransition.TokenFaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
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
                L7SdcPhase07TestFactory.Fill(0xD4, 0x40),
                fixture.Evidence);
        Assert.True(staged.IsAccepted, staged.Message);
        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }

    private static AcceleratorTokenAdmissionResult AdmitToken(
        AcceleratorTokenStore store,
        ExternalAcceleratorConflictManager? manager,
        AcceleratorMemoryRange[] destinationRanges)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor(
            destinationRanges: destinationRanges);
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(ownerBinding);
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseWithGuard(
                descriptorBytes,
                evidence).RequireDescriptor();
        var capability =
            L7SdcCapabilityRegistryTests.CreateRegistry().AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);

        return store.Create(
            descriptor,
            capability,
            evidence,
            conflictManager: manager);
    }
}
