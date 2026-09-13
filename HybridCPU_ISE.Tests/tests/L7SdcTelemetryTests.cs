using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcTelemetryTests
{
    [Fact]
    public void L7SdcTelemetry_AcceptedLifecycleBytesCommitAndExport_AreEvidenceOnly()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                L7SdcPhase07TestFactory.Fill(0x21, 0x40));
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x9000,
                L7SdcPhase07TestFactory.Fill(0x11, 0x40));

            var telemetry = new AcceleratorTelemetry();
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry);
            var backend = new FakeExternalAcceleratorBackend(telemetry: telemetry);
            var staging = new AcceleratorStagingBuffer();

            AcceleratorBackendResult submit =
                backend.TrySubmit(
                    fixture.CreateQueueAdmissionRequest(),
                    queue,
                    fixture.Evidence);
            AcceleratorBackendResult tick =
                backend.Tick(
                    queue,
                    new MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemory),
                    staging,
                    fixture.Evidence);
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator(telemetry).TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.Observe(
                        srfWindows: new[] { new AcceleratorMemoryRange(0x9000, 0x40) },
                        cacheWindows: new[] { new AcceleratorMemoryRange(0x9000, 0x40) }),
                    commitConflictPlaceholderAccepted: true);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsAccepted, tick.Message);
            Assert.True(commit.Succeeded, commit.Message);

            AcceleratorTelemetrySnapshot snapshot =
                TelemetryExporter.ExportAcceleratorTelemetry(telemetry);
            Assert.Equal(1, snapshot.DescriptorParseAttempts);
            Assert.Equal(1, snapshot.DescriptorAccepted);
            Assert.Equal(1, snapshot.CapabilityQueryAttempts);
            Assert.Equal(1, snapshot.CapabilityQuerySuccess);
            Assert.Equal(1, snapshot.SubmitAttempts);
            Assert.Equal(1, snapshot.SubmitAccepted);
            Assert.Equal(1, snapshot.Lifecycle.Created);
            Assert.Equal(1, snapshot.Lifecycle.Validated);
            Assert.Equal(1, snapshot.Lifecycle.Queued);
            Assert.Equal(1, snapshot.Lifecycle.Running);
            Assert.Equal(1, snapshot.Lifecycle.DeviceCompleted);
            Assert.Equal(1, snapshot.Lifecycle.CommitPending);
            Assert.Equal(1, snapshot.Lifecycle.Committed);
            Assert.Equal(64UL, snapshot.Bytes.BytesRead);
            Assert.Equal(64UL, snapshot.Bytes.BytesStaged);
            Assert.Equal(64UL, snapshot.Bytes.BytesCommitted);
            Assert.Equal(1UL, snapshot.OperationCount);
            Assert.Equal(1UL, snapshot.LatencyCycles);
            Assert.Equal(1, snapshot.Conflicts.SrfInvalidations);
            Assert.Equal(1, snapshot.Conflicts.CacheInvalidations);
            object snapshotObject = snapshot;
            Assert.False(snapshotObject is AcceleratorGuardEvidence);

            TypedSlotTelemetryProfile profile =
                TelemetryExporter.BuildProfile(
                    new MicroOpScheduler(),
                    "l7-sdc-telemetry",
                    snapshot);
            string json = TelemetryExporter.SerializeToJson(profile);
            TypedSlotTelemetryProfile? roundTripped =
                TelemetryExporter.DeserializeFromJson(json);

            Assert.NotNull(roundTripped);
            Assert.NotNull(roundTripped!.AcceleratorTelemetry);
            Assert.Equal(1, roundTripped.AcceleratorTelemetry!.Lifecycle.Committed);
            Assert.Null(
                TelemetryExporter.BuildProfile(
                    new MicroOpScheduler(),
                    "l7-sdc-absent").AcceleratorTelemetry);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcTelemetry_RejectPathsIncrementEvidenceWithoutChangingState()
    {
        var telemetry = new AcceleratorTelemetry();

        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence domainRejectedEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                ownerBinding,
                activeDomainCertificate: 0x2);
        AcceleratorDescriptorValidationResult descriptorReject =
            AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                domainRejectedEvidence,
                L7SdcTestDescriptorFactory.CreateReference(descriptorBytes),
                telemetry);

        L7SdcPhase07Fixture busyFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
        AcceleratorQueueAdmissionResult busyReject =
            L7SdcPhase07TestFactory.CreateQueue(
                deviceAvailable: false,
                telemetry: telemetry).TryEnqueue(
                    busyFixture.CreateQueueAdmissionRequest(),
                    busyFixture.Evidence);

        AcceleratorCommandQueue fullQueue =
            L7SdcPhase07TestFactory.CreateQueue(capacity: 1, telemetry: telemetry);
        L7SdcPhase07Fixture first =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
        L7SdcPhase07Fixture second =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
        Assert.True(fullQueue.TryEnqueue(first.CreateQueueAdmissionRequest(), first.Evidence).IsAccepted);
        AcceleratorQueueAdmissionResult fullReject =
            fullQueue.TryEnqueue(second.CreateQueueAdmissionRequest(), second.Evidence);

        L7SdcPhase07Fixture ownerDriftFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
        AcceleratorQueueAdmissionResult ownerDrift =
            L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry).TryEnqueue(
                ownerDriftFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateOwnerDriftEvidence(ownerDriftFixture.Descriptor));

        L7SdcPhase07Fixture mappingFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20,
                telemetry: telemetry);
        AcceleratorQueueAdmissionResult mappingDrift =
            L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry).TryEnqueue(
                mappingFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    mappingFixture.Descriptor,
                    mappingEpoch: 11,
                    iommuDomainEpoch: 20));

        L7SdcPhase07Fixture iommuFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 10,
                iommuDomainEpoch: 20,
                telemetry: telemetry);
        AcceleratorQueueAdmissionResult iommuDrift =
            L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry).TryEnqueue(
                iommuFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    iommuFixture.Descriptor,
                    mappingEpoch: 10,
                    iommuDomainEpoch: 21));

        var throttle = new AcceleratorLane7PressureThrottle(
            maxSubmitPollPerWindow: 1,
            telemetry: telemetry);
        Assert.True(throttle.TryAdmit(SystemDeviceCommandKind.Submit).Accepted);
        AcceleratorLane7PressureResult throttleReject =
            throttle.TryAdmit(SystemDeviceCommandKind.Poll);

        Assert.False(descriptorReject.IsValid);
        Assert.True(busyReject.IsRejected);
        Assert.True(fullReject.IsRejected);
        Assert.True(ownerDrift.IsRejected);
        Assert.True(mappingDrift.IsRejected);
        Assert.True(iommuDrift.IsRejected);
        Assert.True(throttleReject.Rejected);

        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.True(snapshot.Rejects.DomainRejects >= 1);
        Assert.Equal(1, snapshot.Rejects.DeviceBusyRejects);
        Assert.Equal(1, snapshot.Rejects.QueueFullRejects);
        Assert.True(snapshot.Rejects.OwnerDriftRejects >= 1);
        Assert.True(snapshot.Rejects.MappingEpochDriftRejects >= 1);
        Assert.True(snapshot.Rejects.IommuDomainEpochDriftRejects >= 1);
        Assert.Equal(1, snapshot.Rejects.Lane7SubmitPollThrottleRejects);
        Assert.Equal(AcceleratorTokenState.Created, busyFixture.Token.State);
        Assert.Equal(AcceleratorTokenState.Created, second.Token.State);
    }

    [Fact]
    public void L7SdcTelemetry_PostSubmitGuardAndConflictRejectsRemainEvidenceOnly()
    {
        var telemetry = new AcceleratorTelemetry();
        L7SdcPhase07Fixture lookupFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 7,
                iommuDomainEpoch: 9,
                telemetry: telemetry);

        AcceleratorTokenLookupResult lookupDrift =
            lookupFixture.Store.TryPoll(
                lookupFixture.Token.Handle,
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    lookupFixture.Descriptor,
                    mappingEpoch: 8,
                    iommuDomainEpoch: 9));

        var conflictManager = new ExternalAcceleratorConflictManager(telemetry);
        L7SdcPhase07Fixture active =
            L7SdcPhase07TestFactory.CreateAcceptedToken(
                mappingEpoch: 3,
                iommuDomainEpoch: 4,
                telemetry: telemetry);
        Assert.True(conflictManager.TryReserveOnSubmit(active.Token, active.Evidence).IsAccepted);

        AcceleratorConflictDecision serializingReject =
            conflictManager.NotifySerializingBoundary(
                active.Token,
                active.Evidence);
        AcceleratorConflictDecision releaseDrift =
            conflictManager.ReleaseTokenFootprint(
                active.Token,
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    active.Descriptor,
                    mappingEpoch: 4,
                    iommuDomainEpoch: 4));
        AcceleratorConflictDecision vmDrift =
            conflictManager.NotifyVmDomainOrMappingTransition(
                active.Token,
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    active.Descriptor,
                    mappingEpoch: 3,
                    iommuDomainEpoch: 5));

        Assert.True(lookupDrift.IsRejected);
        Assert.True(serializingReject.IsRejected);
        Assert.True(releaseDrift.IsRejected);
        Assert.True(vmDrift.IsRejected);

        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.True(snapshot.Rejects.MappingEpochDriftRejects >= 2);
        Assert.True(snapshot.Rejects.IommuDomainEpochDriftRejects >= 1);
        Assert.True(snapshot.Conflicts.FootprintConflictRejects >= 3);
        Assert.Equal(AcceleratorTokenState.Created, lookupFixture.Token.State);
        Assert.Equal(AcceleratorTokenState.Created, active.Token.State);
    }

    [Fact]
    public void L7SdcTelemetry_BackendNegativeControlsRecordEvidenceOnly()
    {
        var telemetry = new AcceleratorTelemetry();
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);

        AcceleratorBackendResult directReject =
            new DirectWriteViolationBackend(telemetry: telemetry).TrySubmit(
                fixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry),
                fixture.Evidence);
        AcceleratorBackendResult nullReject =
            new NullExternalAcceleratorBackend(telemetry: telemetry).TrySubmit(
                fixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry),
                fixture.Evidence);

        Assert.True(directReject.IsRejected);
        Assert.True(nullReject.IsRejected);

        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(1, snapshot.Rejects.DirectWriteViolationRejects);
        Assert.Equal(2, snapshot.Rejects.BackendRejects);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcTelemetry_ConflictDirectWriteAndRollbackCountersAreEvidenceOnly()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            var telemetry = new AcceleratorTelemetry();
            var conflictManager = new ExternalAcceleratorConflictManager(telemetry);
            L7SdcPhase07Fixture active =
                L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
            AcceleratorConflictDecision reserved =
                conflictManager.TryReserveOnSubmit(active.Token, active.Evidence);
            AcceleratorConflictDecision dmaReject =
                conflictManager.NotifyDmaStreamComputeAdmission(
                    new[] { new AcceleratorMemoryRange(0x9000, 0x10) },
                    new[] { new AcceleratorMemoryRange(0xA000, 0x10) },
                    active.Evidence);

            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x5A, 64);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture direct =
                L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue(telemetry: telemetry);
            AcceleratorQueueAdmissionResult queued =
                queue.TryEnqueue(direct.CreateQueueAdmissionRequest(), direct.Evidence);
            AcceleratorBackendResult directViolation =
                new DirectWriteViolationBackend(telemetry: telemetry).AttemptDirectWriteForTest(
                    queued.Command!,
                    direct.Evidence,
                    address: 0x9000,
                    attemptedData: L7SdcPhase07TestFactory.Fill(0xE7, 64));

            var memory = new FailingWriteMemoryArea
            {
                FailAddress = 0x9020,
                MutateBeforeReturningFailure = true
            };
            memory.AllocateMemory(0, 0x10000);
            Processor.MainMemory = memory;
            Assert.True(memory.TryWritePhysicalRange(0x9000, L7SdcPhase07TestFactory.Fill(0x10, 16)));
            Assert.True(memory.TryWritePhysicalRange(0x9020, L7SdcPhase07TestFactory.Fill(0x20, 16)));

            L7SdcPhase07Fixture rollbackFixture =
                L7SdcPhase07TestFactory.CreateAcceptedTokenForDescriptor(
                    destinationRanges: new[]
                    {
                        new AcceleratorMemoryRange(0x9000, 16),
                        new AcceleratorMemoryRange(0x9020, 16)
                    },
                    telemetry: telemetry);
            var staging = new AcceleratorStagingBuffer();
            Assert.True(rollbackFixture.Token.MarkValidated(rollbackFixture.Evidence).Succeeded);
            Assert.True(rollbackFixture.Token.MarkQueued(rollbackFixture.Evidence).Succeeded);
            Assert.True(rollbackFixture.Token.MarkRunning(rollbackFixture.Evidence).Succeeded);
            Assert.True(staging.StageWrite(
                rollbackFixture.Token,
                new AcceleratorMemoryRange(0x9000, 16),
                L7SdcPhase07TestFactory.Fill(0xA1, 16),
                rollbackFixture.Evidence).IsAccepted);
            Assert.True(staging.StageWrite(
                rollbackFixture.Token,
                new AcceleratorMemoryRange(0x9020, 16),
                L7SdcPhase07TestFactory.Fill(0xB2, 16),
                rollbackFixture.Evidence).IsAccepted);
            Assert.True(rollbackFixture.Token.MarkDeviceComplete(rollbackFixture.Evidence).Succeeded);
            memory.FailureEnabled = true;
            AcceleratorCommitResult rollbackCommit =
                new AcceleratorCommitCoordinator(telemetry).TryCommit(
                    rollbackFixture.Token,
                    rollbackFixture.Descriptor,
                    staging,
                    memory,
                    rollbackFixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(reserved.IsAccepted, reserved.Message);
            Assert.True(dmaReject.IsRejected);
            Assert.True(directViolation.IsFaulted, directViolation.Message);
            Assert.True(rollbackCommit.IsRejected);
            Assert.True(rollbackCommit.Rollback.RollbackAttempted);

            AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
            Assert.Equal(1, snapshot.Conflicts.DmaStreamComputeConflictRejects);
            Assert.Equal(1, snapshot.Conflicts.DirectWriteViolationRejects);
            Assert.Equal(1, snapshot.Conflicts.CommitRollbackCount);
            Assert.Equal(0UL, rollbackCommit.BytesCommitted);
            Assert.NotEqual(AcceleratorTokenState.Committed, direct.Token.State);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    private sealed class FailingWriteMemoryArea : Processor.MainMemoryArea
    {
        public bool FailureEnabled { get; set; }

        public ulong FailAddress { get; init; }

        public bool MutateBeforeReturningFailure { get; init; }

        public override bool TryWritePhysicalRange(
            ulong physicalAddress,
            ReadOnlySpan<byte> buffer)
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
