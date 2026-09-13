using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcFaultPublicationTests
{
    [Fact]
    public void L7SdcFaultPublication_ValidGuardPublishesPackedFaultStatus()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorFaultPublicationResult result =
            new AcceleratorExceptionPublication().TryPublish(
                fixture.Token,
                AcceleratorTokenFaultCode.BackendRejected,
                fixture.Evidence);

        Assert.True(result.IsPublished, result.Message);
        Assert.True(result.UserVisiblePublished);
        Assert.True(result.HasUserVisibleStatusWord);
        Assert.Equal(AcceleratorTokenState.Faulted, fixture.Token.State);
        Assert.Equal(AcceleratorTokenState.Faulted, result.StatusWord.State);
        Assert.Equal(AcceleratorTokenFaultCode.BackendRejected, result.StatusWord.FaultCode);
        Assert.False(result.CanAuthorizeCommit);
        Assert.False(result.CanAuthorizeCancel);
        Assert.False(result.CanAuthorizeFence);
    }

    [Fact]
    public void L7SdcFaultPublication_AlreadyFaultedTokenPublishesExistingFaultConsistently()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();
        Assert.True(
            fixture.Token.MarkFaulted(
                AcceleratorTokenFaultCode.DirectWriteViolation,
                fixture.Evidence).Succeeded);

        AcceleratorFaultPublicationResult result =
            new AcceleratorExceptionPublication().TryPublish(
                fixture.Token,
                AcceleratorTokenFaultCode.BackendRejected,
                fixture.Evidence);

        Assert.True(result.IsPublished, result.Message);
        Assert.Equal(AcceleratorTokenFaultCode.DirectWriteViolation, result.FaultCode);
        Assert.Equal(result.FaultCode, result.StatusWord.FaultCode);
        Assert.Equal(result.PackedStatusWord, result.StatusWord.Pack());
        Assert.Equal(AcceleratorTokenState.Faulted, result.StatusWord.State);
    }

    [Fact]
    public void L7SdcFaultPublication_InvalidOwnerBlocksUserVisibleFaultStatus()
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

        AcceleratorFaultPublicationResult result =
            new AcceleratorExceptionPublication().TryPublish(
                fixture.Token,
                AcceleratorTokenFaultCode.BackendRejected,
                mappingDrift,
                recordPrivilegedDiagnostic: true);

        Assert.False(result.UserVisiblePublished);
        Assert.False(result.HasUserVisibleStatusWord);
        Assert.Equal(0UL, result.PackedStatusWord);
        Assert.True(result.PrivilegedDiagnosticRecorded);
        Assert.Equal(AcceleratorFaultPublicationDisposition.DiagnosticOnly, result.Disposition);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, result.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcFaultPublication_PrivilegedDiagnosticsAreNonAuthoritative()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination = L7SdcPhase07TestFactory.Fill(0x46, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken(
                    mappingEpoch: 10,
                    iommuDomainEpoch: 20);
            AcceleratorGuardEvidence mappingDrift =
                L7SdcPhase07TestFactory.CreateMappingDriftEvidence(
                    fixture.Descriptor,
                    mappingEpoch: 11,
                    iommuDomainEpoch: 20);
            var staging = new AcceleratorStagingBuffer();

            AcceleratorFaultPublicationResult diagnostic =
                new AcceleratorExceptionPublication().TryPublish(
                    fixture.Token,
                    AcceleratorTokenFaultCode.BackendRejected,
                    mappingDrift,
                    recordPrivilegedDiagnostic: true);
            AcceleratorTokenLookupResult cancel =
                fixture.Store.TryCancel(
                    fixture.Token.Handle,
                    mappingDrift,
                    AcceleratorCancelPolicy.Cooperative);
            AcceleratorFenceResult fence =
                new AcceleratorFenceCoordinator().TryFence(
                    fixture.Store,
                    AcceleratorFenceScope.ForToken(
                        fixture.Token.Handle,
                        activeTokenDisposition: AcceleratorFenceActiveTokenDisposition.Cancel),
                    mappingDrift);
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator().TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    mappingDrift,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);

            Assert.True(diagnostic.PrivilegedDiagnosticRecorded);
            Assert.False(diagnostic.CanAuthorizeCommit);
            Assert.False(diagnostic.CanAuthorizeCancel);
            Assert.False(diagnostic.CanAuthorizeFence);
            Assert.True(cancel.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, cancel.FaultCode);
            Assert.True(fence.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, fence.FaultCode);
            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.IllegalTransition, commit.FaultCode);
            Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
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
    public void L7SdcFaultPublication_MissingGuardRejectsUserVisiblePublication()
    {
        L7SdcPhase07Fixture fixture =
            L7SdcPhase07TestFactory.CreateAcceptedToken();

        AcceleratorFaultPublicationResult result =
            new AcceleratorExceptionPublication().TryPublish(
                fixture.Token,
                AcceleratorTokenFaultCode.BackendRejected,
                currentGuardEvidence: null);

        Assert.False(result.UserVisiblePublished);
        Assert.False(result.HasUserVisibleStatusWord);
        Assert.False(result.PrivilegedDiagnosticRecorded);
        Assert.Equal(AcceleratorFaultPublicationDisposition.BlockedInvalidOwner, result.Disposition);
        Assert.Equal(AcceleratorTokenFaultCode.MissingGuardEvidence, result.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }
}
