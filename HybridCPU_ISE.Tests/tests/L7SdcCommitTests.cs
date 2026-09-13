using System;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcCommitTests
{
    [Fact]
    public void L7SdcCommit_StagedWritesInvisibleUntilGuardedCoordinatorCommit()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                L7SdcPhase07TestFactory.Fill(0x21, 0x40));
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0xC1, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue();
            var backend = new FakeExternalAcceleratorBackend();
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
            AcceleratorStagingReadResult stagedBeforeCommit =
                staging.GetStagedWriteSet(fixture.Token, fixture.Evidence);
            AcceleratorTokenLookupResult handleCommit =
                fixture.Store.TryCommitPublication(fixture.Token.Handle, fixture.Evidence);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsAccepted, tick.Message);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, fixture.Token.State);
            Assert.True(stagedBeforeCommit.IsAccepted, stagedBeforeCommit.Message);
            Assert.Single(stagedBeforeCommit.StagedWrites);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.True(handleCommit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, handleCommit.FaultCode);

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.True(commit.CanPublishArchitecturalMemory);
            Assert.True(commit.UserVisiblePublicationAllowed);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.True(fixture.Token.HasArchitecturalCommit);
            Assert.Equal(64UL, commit.BytesCommitted);
            Assert.Equal(
                stagedBeforeCommit.StagedWrites[0].Data.ToArray(),
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcCommit_ExactSplitCoverageCommitsThroughCoordinator()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x11, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndComplete(
                fixture,
                staging,
                new AcceleratorMemoryRange(0x9000, 0x20),
                L7SdcPhase07TestFactory.Fill(0xA1, 0x20),
                new AcceleratorMemoryRange(0x9020, 0x20),
                L7SdcPhase07TestFactory.Fill(0xB2, 0x20));

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            byte[] expected = new byte[0x40];
            Array.Fill<byte>(expected, 0xA1, 0, 0x20);
            Array.Fill<byte>(expected, 0xB2, 0x20, 0x20);
            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(expected, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, expected.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcCommit_MissingStagedByteFaultsWithoutMemoryPublication()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x22, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndComplete(
                fixture,
                staging,
                new AcceleratorMemoryRange(0x9000, 0x3F),
                L7SdcPhase07TestFactory.Fill(0xE1, 0x3F));

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.StagedCoverageMismatch, commit.FaultCode);
            Assert.Equal(AcceleratorTokenState.Faulted, fixture.Token.State);
            Assert.False(commit.CanPublishArchitecturalMemory);
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
    public void L7SdcCommit_OverlappingStagedRangesRejectWithoutHidingAsExactCoverage()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x33, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndComplete(
                fixture,
                staging,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(0xC3, 0x40),
                new AcceleratorMemoryRange(0x9020, 0x10),
                L7SdcPhase07TestFactory.Fill(0xD4, 0x10));

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.StagedCoverageMismatch, commit.FaultCode);
            Assert.Contains("overlap", commit.Message, StringComparison.OrdinalIgnoreCase);
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
    public void L7SdcCommit_OwnerDomainAndEpochDriftBlockPublicationBeforeStatePromotion()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x44, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

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
            var ownerStaging = new AcceleratorStagingBuffer();
            var mappingStaging = new AcceleratorStagingBuffer();
            var iommuStaging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(ownerDriftFixture, ownerStaging, 0x55);
            StageAndCompleteDefault(mappingDriftFixture, mappingStaging, 0x66);
            StageAndCompleteDefault(iommuDriftFixture, iommuStaging, 0x77);

            var coordinator = new AcceleratorCommitCoordinator();
            AcceleratorCommitResult ownerCommit =
                coordinator.TryCommit(
                    ownerDriftFixture.Token,
                    ownerDriftFixture.Descriptor,
                    ownerStaging,
                    Processor.MainMemory,
                    L7SdcPhase07TestFactory.CreateOwnerDriftEvidence(ownerDriftFixture.Descriptor),
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);
            AcceleratorCommitResult mappingCommit =
                coordinator.TryCommit(
                    mappingDriftFixture.Token,
                    mappingDriftFixture.Descriptor,
                    mappingStaging,
                    Processor.MainMemory,
                    L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                        mappingDriftFixture.Descriptor,
                        mappingEpoch: 11,
                        iommuDomainEpoch: 20),
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);
            AcceleratorCommitResult iommuCommit =
                coordinator.TryCommit(
                    iommuDriftFixture.Token,
                    iommuDriftFixture.Descriptor,
                    iommuStaging,
                    Processor.MainMemory,
                    L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                        iommuDriftFixture.Descriptor,
                        mappingEpoch: 10,
                        iommuDomainEpoch: 21),
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.Equal(AcceleratorTokenFaultCode.OwnerDomainRejected, ownerCommit.FaultCode);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, mappingCommit.FaultCode);
            Assert.Equal(AcceleratorTokenFaultCode.IommuDomainEpochDrift, iommuCommit.FaultCode);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, ownerDriftFixture.Token.State);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, mappingDriftFixture.Token.State);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, iommuDriftFixture.Token.State);
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
    public void L7SdcCommit_DescriptorAndFootprintIdentityMismatchFaultBeforePublication()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x88, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture descriptorFixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            L7SdcPhase07Fixture footprintFixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var descriptorStaging = new AcceleratorStagingBuffer();
            var footprintStaging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(descriptorFixture, descriptorStaging, 0x99);
            StageAndCompleteDefault(footprintFixture, footprintStaging, 0xAA);

            AcceleratorCommandDescriptor descriptorIdentityMismatch =
                descriptorFixture.Descriptor with
                {
                    Identity = descriptorFixture.Descriptor.Identity with
                    {
                        DescriptorIdentityHash =
                            descriptorFixture.Descriptor.Identity.DescriptorIdentityHash ^ 0x10UL
                    }
                };
            AcceleratorCommandDescriptor footprintMismatch =
                footprintFixture.Descriptor with
                {
                    Identity = footprintFixture.Descriptor.Identity with
                    {
                        NormalizedFootprintHash =
                            footprintFixture.Descriptor.Identity.NormalizedFootprintHash ^ 0x20UL
                    },
                    NormalizedFootprint = footprintFixture.Descriptor.NormalizedFootprint with
                    {
                        Hash = footprintFixture.Descriptor.NormalizedFootprint.Hash ^ 0x20UL
                    }
                };

            var coordinator = new AcceleratorCommitCoordinator();
            AcceleratorCommitResult descriptorCommit =
                coordinator.TryCommit(
                    descriptorFixture.Token,
                    descriptorIdentityMismatch,
                    descriptorStaging,
                    Processor.MainMemory,
                    descriptorFixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);
            AcceleratorCommitResult footprintCommit =
                coordinator.TryCommit(
                    footprintFixture.Token,
                    footprintMismatch,
                    footprintStaging,
                    Processor.MainMemory,
                    footprintFixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.Equal(AcceleratorTokenFaultCode.DescriptorIdentityMismatch, descriptorCommit.FaultCode);
            Assert.Equal(AcceleratorTokenFaultCode.NormalizedFootprintMismatch, footprintCommit.FaultCode);
            Assert.Equal(AcceleratorTokenState.Faulted, descriptorFixture.Token.State);
            Assert.Equal(AcceleratorTokenState.Faulted, footprintFixture.Token.State);
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
    public void L7SdcCommit_UsesTokenBoundFootprintWhenCallerDescriptorRangesAreTampered()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalTokenDestination =
                L7SdcPhase07TestFactory.Fill(0x41, 0x40);
            byte[] originalTamperedDestination =
                L7SdcPhase07TestFactory.Fill(0x52, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalTokenDestination);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9100, originalTamperedDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xD7);
            AcceleratorCommandDescriptor tamperedDescriptor =
                fixture.Descriptor with
                {
                    NormalizedFootprint = fixture.Descriptor.NormalizedFootprint with
                    {
                        DestinationRanges = new[]
                        {
                            new AcceleratorMemoryRange(0x9100, 0x40)
                        }
                    },
                    DestinationRanges = new[]
                    {
                        new AcceleratorMemoryRange(0x9100, 0x40)
                    }
                };

            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    tamperedDescriptor,
                    staging,
                    Processor.MainMemory,
                    fixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(commit.Succeeded, commit.Message);
            Assert.Equal(AcceleratorTokenState.Committed, fixture.Token.State);
            Assert.Equal(
                L7SdcPhase07TestFactory.Fill(0xD7, 0x40),
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, 0x40));
            Assert.Equal(
                originalTamperedDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9100, 0x40));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcCommit_DirectWriteViolationAndConflictRejectionCannotAuthorizeCommit()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0xA0, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture directWriteFixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            L7SdcPhase07Fixture conflictFixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var directStaging = new AcceleratorStagingBuffer();
            var conflictStaging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(directWriteFixture, directStaging, 0xB0);
            StageAndCompleteDefault(conflictFixture, conflictStaging, 0xC0);

            var coordinator = new AcceleratorCommitCoordinator();
            AcceleratorCommitResult directCommit =
                coordinator.TryCommit(
                    directWriteFixture.Token,
                    directWriteFixture.Descriptor,
                    directStaging,
                    Processor.MainMemory,
                    directWriteFixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true,
                    directWriteViolationDetected: true);
            AcceleratorCommitResult conflictCommit =
                coordinator.TryCommit(
                    conflictFixture.Token,
                    conflictFixture.Descriptor,
                    conflictStaging,
                    Processor.MainMemory,
                    conflictFixture.Evidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: false);

            Assert.Equal(AcceleratorTokenFaultCode.DirectWriteViolation, directCommit.FaultCode);
            Assert.Equal(AcceleratorTokenFaultCode.CommitConflictRejected, conflictCommit.FaultCode);
            Assert.Equal(AcceleratorTokenState.Faulted, directWriteFixture.Token.State);
            Assert.Equal(AcceleratorTokenState.Faulted, conflictFixture.Token.State);
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
    public void L7SdcCommit_PublicCommitHelpersRequireFreshGuardBeforePublicationOrInvalidation()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x71, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var staging = new AcceleratorStagingBuffer();
            StageAndCompleteDefault(fixture, staging, 0xE8);
            PromoteToCommitPendingForHelperTest(fixture.Token, fixture.Evidence);
            AcceleratorStagingReadResult stagedRead =
                staging.GetStagedWriteSet(fixture.Token, fixture.Evidence);
            Assert.True(stagedRead.IsAccepted, stagedRead.Message);
            var stagedSet = AcceleratorStagedWriteSet.FromStagingReadResult(stagedRead);

            var srf = new YAKSys_Hybrid_CPU.Memory.StreamRegisterFile();
            int register = srf.AllocateRegister(0x9000, elementSize: 1, elementCount: 0x20);
            Assert.True(register >= 0);
            Assert.True(srf.LoadRegister(
                register,
                L7SdcPhase07TestFactory.Fill(0x71, 0x20).AsSpan()));

            var coordinator = new AcceleratorCommitCoordinator();
            bool applied =
                coordinator.ApplyAllOrNone(
                    fixture.Token,
                    fixture.Descriptor,
                    Processor.MainMemory,
                    stagedSet,
                    currentGuardEvidence: null,
                    out AcceleratorRollbackRecord rollback,
                    out AcceleratorCommitFault? applyFault,
                    out ulong bytesCommitted);
            bool invalidated =
                coordinator.InvalidateSrfAndCache(
                    fixture.Token,
                    fixture.Descriptor,
                    stagedSet,
                    AcceleratorCommitInvalidationPlan.Observe(
                        srfWindows: new[] { new AcceleratorMemoryRange(0x9000, 0x20) },
                        streamRegisterFile: srf),
                    currentGuardEvidence: null,
                    out AcceleratorCommitInvalidationPlan invalidationPlan,
                    out AcceleratorCommitFault? invalidationFault);

            Assert.False(applied);
            Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, applyFault!.FaultCode);
            Assert.False(rollback.RollbackAttempted);
            Assert.Equal(0UL, bytesCommitted);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.False(invalidated);
            Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, invalidationFault!.FaultCode);
            Assert.Equal(0, invalidationPlan.SrfInvalidationCount);
            Assert.Equal(
                YAKSys_Hybrid_CPU.Memory.StreamRegisterFile.RegisterState.Valid,
                srf.GetRegisterState(register));

            AcceleratorCommandDescriptor descriptorIdentityDrift =
                fixture.Descriptor with
                {
                    Identity = fixture.Descriptor.Identity with
                    {
                        DescriptorIdentityHash =
                            fixture.Descriptor.Identity.DescriptorIdentityHash ^ 0x100UL
                    }
                };
            bool descriptorApplied =
                coordinator.ApplyAllOrNone(
                    fixture.Token,
                    descriptorIdentityDrift,
                    Processor.MainMemory,
                    stagedSet,
                    fixture.Evidence,
                    out AcceleratorRollbackRecord descriptorRollback,
                    out AcceleratorCommitFault? descriptorApplyFault,
                    out ulong descriptorBytesCommitted);
            bool descriptorInvalidated =
                coordinator.InvalidateSrfAndCache(
                    fixture.Token,
                    descriptorIdentityDrift,
                    stagedSet,
                    AcceleratorCommitInvalidationPlan.Observe(
                        srfWindows: new[] { new AcceleratorMemoryRange(0x9000, 0x20) },
                        streamRegisterFile: srf),
                    fixture.Evidence,
                    out AcceleratorCommitInvalidationPlan descriptorInvalidationPlan,
                    out AcceleratorCommitFault? descriptorInvalidationFault);

            Assert.False(descriptorApplied);
            Assert.Equal(
                AcceleratorTokenFaultCode.DescriptorIdentityMismatch,
                descriptorApplyFault!.FaultCode);
            Assert.False(descriptorRollback.RollbackAttempted);
            Assert.Equal(0UL, descriptorBytesCommitted);
            Assert.False(descriptorInvalidated);
            Assert.Equal(
                AcceleratorTokenFaultCode.DescriptorIdentityMismatch,
                descriptorInvalidationFault!.FaultCode);
            Assert.Equal(0, descriptorInvalidationPlan.SrfInvalidationCount);
            Assert.Equal(AcceleratorTokenState.CommitPending, fixture.Token.State);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.Equal(
                YAKSys_Hybrid_CPU.Memory.StreamRegisterFile.RegisterState.Valid,
                srf.GetRegisterState(register));
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
    public void L7SdcCommit_TerminalTokensCannotCommit(
        AcceleratorTokenState terminalState)
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        switch (terminalState)
        {
            case AcceleratorTokenState.Faulted:
                fixture.Token.MarkFaulted(
                    AcceleratorTokenFaultCode.BackendExecutionUnavailable,
                    fixture.Evidence);
                break;
            case AcceleratorTokenState.Canceled:
                fixture.Token.MarkCanceled(fixture.Evidence);
                break;
            case AcceleratorTokenState.TimedOut:
                fixture.Token.MarkTimedOut(fixture.Evidence);
                break;
            case AcceleratorTokenState.Abandoned:
                fixture.Token.MarkAbandoned(fixture.Evidence);
                break;
        }

        AcceleratorCommitResult commit =
            new AcceleratorCommitCoordinator().TryCommit(
                fixture.Token,
                fixture.Descriptor,
                new AcceleratorStagingBuffer(),
                new Processor.MultiBankMemoryArea(1, 0x10000),
                fixture.Evidence,
                AcceleratorCommitInvalidationPlan.None,
                commitConflictPlaceholderAccepted: true);

        Assert.True(commit.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.TerminalState, commit.FaultCode);
        Assert.Equal(terminalState, fixture.Token.State);
    }

    private static void StageAndCompleteDefault(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        byte value)
    {
        StageAndComplete(
            fixture,
            staging,
            new AcceleratorMemoryRange(0x9000, 0x40),
            L7SdcPhase07TestFactory.Fill(value, 0x40));
    }

    private static void StageAndComplete(
        L7SdcPhase07Fixture fixture,
        AcceleratorStagingBuffer staging,
        AcceleratorMemoryRange firstRange,
        byte[] firstData,
        AcceleratorMemoryRange? secondRange = null,
        byte[]? secondData = null)
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

        if (secondRange.HasValue)
        {
            AcceleratorStagingResult secondStage =
                staging.StageWrite(
                    fixture.Token,
                    secondRange.Value,
                    secondData!,
                    fixture.Evidence);
            Assert.True(secondStage.IsAccepted, secondStage.Message);
        }

        Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);
    }

    private static void PromoteToCommitPendingForHelperTest(
        AcceleratorToken token,
        AcceleratorGuardEvidence evidence)
    {
        MethodInfo method =
            typeof(AcceleratorToken).GetMethod(
                "MarkCommitPendingFromCommitCoordinator",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                nameof(AcceleratorToken),
                "MarkCommitPendingFromCommitCoordinator");
        var transition =
            (AcceleratorTokenTransition)method.Invoke(
                token,
                new object[] { evidence })!;
        Assert.True(transition.Succeeded, transition.Message);
        Assert.Equal(AcceleratorTokenState.CommitPending, token.State);
    }
}
