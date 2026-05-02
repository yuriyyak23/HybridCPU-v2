using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Backends;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Queues;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcBackendTests
{
    [Fact]
    public void L7SdcBackend_NullBackendRejectsSubmitWithoutMemoryWrite()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] original = L7SdcPhase07TestFactory.Fill(0xA5, 64);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, original);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();

            var backend = new NullExternalAcceleratorBackend();
            AcceleratorBackendResult result =
                backend.TrySubmit(
                    fixture.CreateQueueAdmissionRequest(),
                    L7SdcPhase07TestFactory.CreateQueue(),
                    fixture.Evidence);

            Assert.True(result.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.BackendExecutionUnavailable, result.FaultCode);
            Assert.False(result.CanPublishArchitecturalMemory);
            Assert.False(result.CanPublishException);
            Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
            AcceleratorStagingReadResult staged =
                new AcceleratorStagingBuffer().GetStagedWriteSet(
                    fixture.Token,
                    fixture.Evidence);
            Assert.True(staged.IsAccepted, staged.Message);
            Assert.Empty(staged.StagedWrites);
            Assert.Equal(original, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, original.Length));
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcBackend_NullBackendFaultPolicyRequiresTokenBoundRequestEvidence()
    {
        L7SdcPhase07Fixture tokenFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        L7SdcPhase07Fixture descriptorFixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        var backend = new NullExternalAcceleratorBackend(
            NullExternalAcceleratorBackendPolicy.FaultToken);

        AcceleratorBackendResult mismatched =
            backend.TrySubmit(
                new AcceleratorQueueAdmissionRequest
                {
                    Descriptor = descriptorFixture.Descriptor,
                    CapabilityAcceptance = descriptorFixture.CapabilityAcceptance,
                    TokenAdmission = tokenFixture.Admission
                },
                L7SdcPhase07TestFactory.CreateQueue(),
                tokenFixture.Evidence);
        Assert.True(mismatched.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.QueueAdmissionRejected, mismatched.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, tokenFixture.Token.State);

        AcceleratorBackendResult acceptedFault =
            backend.TrySubmit(
                tokenFixture.CreateQueueAdmissionRequest(),
                L7SdcPhase07TestFactory.CreateQueue(),
                tokenFixture.Evidence);

        Assert.True(acceptedFault.IsFaulted, acceptedFault.Message);
        Assert.Equal(AcceleratorTokenFaultCode.BackendExecutionUnavailable, acceptedFault.FaultCode);
        Assert.Equal(AcceleratorTokenState.Faulted, tokenFixture.Token.State);
        Assert.False(acceptedFault.CanPublishArchitecturalMemory);
        Assert.False(acceptedFault.CanPublishException);
    }

    [Fact]
    public void L7SdcBackend_FakeBackendStagesWritesOnlyAndDeviceCompleteIsNotCommit()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                L7SdcPhase07TestFactory.Fill(0x11, 0x40));
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0xCC, 0x40);
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
            AcceleratorTokenLookupResult commit =
                fixture.Store.TryCommitPublication(fixture.Token.Handle, fixture.Evidence);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(backend.IsTestOnly);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, fixture.Token.State);
            Assert.True(tick.IsAccepted, tick.Message);
            Assert.Equal(AcceleratorBackendResultKind.DeviceCompleted, tick.Kind);
            Assert.True(tick.BytesRead > 0);
            Assert.Equal(64UL, tick.BytesStaged);
            AcceleratorStagingReadResult staged =
                staging.GetStagedWriteSet(fixture.Token, fixture.Evidence);
            AcceleratorStagingReadResult stagedWithoutGuard =
                staging.GetStagedWriteSet(fixture.Token, currentGuardEvidence: null);
            Assert.True(staged.IsAccepted, staged.Message);
            Assert.Single(staged.StagedWrites);
            Assert.True(stagedWithoutGuard.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, stagedWithoutGuard.FaultCode);
            Assert.Equal(originalDestination, L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));
            Assert.False(tick.CanPublishArchitecturalMemory);
            Assert.False(tick.UserVisiblePublicationAllowed);
            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, commit.FaultCode);
            Assert.Equal(AcceleratorTokenState.DeviceComplete, fixture.Token.State);
            Assert.NotEqual(AcceleratorTokenState.Committed, fixture.Token.State);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcBackend_SourceReadAndStagingRequireRunningTokenBoundBackendContour()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                L7SdcPhase07TestFactory.Fill(0x44, 0x40));
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var portal = new MainMemoryReadOnlyAcceleratorMemoryPortal(Processor.MainMemory);
            var staging = new AcceleratorStagingBuffer();

            AcceleratorMemoryPortalReadResult readBeforeBackend =
                portal.ReadSourceRanges(
                    fixture.Token,
                    fixture.Descriptor,
                    fixture.Evidence);
            AcceleratorStagingResult stageBeforeBackend =
                staging.StageWrite(
                    fixture.Token,
                    fixture.Descriptor.DestinationRanges[0],
                    L7SdcPhase07TestFactory.Fill(0x55, 64),
                    fixture.Evidence);

            Assert.True(readBeforeBackend.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.SourceReadRejected, readBeforeBackend.FaultCode);
            Assert.True(stageBeforeBackend.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.StagingRejected, stageBeforeBackend.FaultCode);
            AcceleratorStagingReadResult staged =
                staging.GetStagedWriteSet(fixture.Token, fixture.Evidence);
            Assert.True(staged.IsAccepted, staged.Message);
            Assert.Empty(staged.StagedWrites);
            Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcBackend_DriftBlocksTickCancelObservationAndCompletionSideEffects()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            L7SdcPhase07TestFactory.WriteMainMemory(
                0x1000,
                L7SdcPhase07TestFactory.Fill(0x22, 0x40));
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken(
                    mappingEpoch: 10,
                    iommuDomainEpoch: 20);
            AcceleratorCommandQueue queue =
                L7SdcPhase07TestFactory.CreateQueue();
            var backend = new FakeExternalAcceleratorBackend();
            var staging = new AcceleratorStagingBuffer();
            AcceleratorGuardEvidence mappingDrift =
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    fixture.Descriptor,
                    mappingEpoch: 11,
                    iommuDomainEpoch: 20);

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
                    mappingDrift);
            AcceleratorBackendResult cancel =
                backend.TryCancel(
                    fixture.Store,
                    fixture.Token.Handle,
                    mappingDrift);
            AcceleratorTokenLookupResult observation =
                fixture.Store.Poll(fixture.Token.Handle, mappingDrift);

            Assert.True(submit.IsAccepted, submit.Message);
            Assert.True(tick.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, tick.FaultCode);
            Assert.True(cancel.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, cancel.FaultCode);
            Assert.True(observation.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, observation.FaultCode);
            Assert.Equal(AcceleratorTokenState.Queued, fixture.Token.State);
            Assert.Equal(1, queue.Count);
            AcceleratorStagingReadResult staleStaged =
                staging.GetStagedWriteSet(fixture.Token, mappingDrift);
            Assert.True(staleStaged.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, staleStaged.FaultCode);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }

    [Fact]
    public void L7SdcBackend_NoFallbackAndDirectSystemDeviceMicroOpExecuteRemainsFailClosed()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            if (Processor.MainMemory is null)
            {
                L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            }

            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken();
            var core = new Processor.CPU_Core(0);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => new AcceleratorSubmitMicroOp(fixture.Descriptor).Execute(ref core));

            Assert.Contains("direct execution is unsupported", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }

        string externalAcceleratorSource = string.Empty;
        foreach (string sourcePath in System.IO.Directory.EnumerateFiles(
                     System.IO.Path.Combine(
                         L7SdcPhase07TestFactory.ResolveRepoRoot(),
                         "HybridCPU_ISE",
                         "Core",
                         "Execution",
                         "ExternalAccelerators"),
                     "*.cs",
                     System.IO.SearchOption.AllDirectories))
        {
            if (sourcePath.Contains(
                    $"{System.IO.Path.DirectorySeparatorChar}Conflicts{System.IO.Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                continue;
            }

            externalAcceleratorSource += System.IO.File.ReadAllText(sourcePath);
        }

        Assert.DoesNotContain("new DmaStreamComputeMicroOp", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaStreamComputeMicroOp(", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileDmaStreamCompute", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StreamEngine", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VectorALU", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GenericMicroOp", externalAcceleratorSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ICustomAccelerator", externalAcceleratorSource, StringComparison.Ordinal);
    }
}
