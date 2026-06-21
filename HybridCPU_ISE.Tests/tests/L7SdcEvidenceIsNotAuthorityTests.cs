using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Telemetry;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcEvidenceIsNotAuthorityTests
{
    [Fact]
    public void L7SdcEvidenceIsNotAuthority_TelemetrySnapshotIsNotAnyGuardOrCommitCredential()
    {
        var telemetry = new AcceleratorTelemetry();
        telemetry.RecordCapabilityQuery(accepted: true, "metadata observed");
        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        object snapshotObject = snapshot;

        Assert.False(snapshotObject is AcceleratorGuardEvidence);
        Assert.False(snapshotObject is AcceleratorGuardDecision);
        Assert.False(snapshotObject is AcceleratorCapabilityAcceptanceResult);
        Assert.False(snapshotObject is AcceleratorTokenAdmissionResult);
        Assert.False(snapshotObject is AcceleratorCommitResult);

        Type snapshotType = typeof(AcceleratorTelemetrySnapshot);
        Type[] authorityTypes =
        {
            typeof(AcceleratorGuardEvidence),
            typeof(AcceleratorGuardDecision),
            typeof(AcceleratorCapabilityAcceptanceResult),
            typeof(AcceleratorTokenAdmissionResult),
            typeof(AcceleratorCommitResult)
        };

        Assert.DoesNotContain(snapshotType, authorityTypes);
        Assert.Empty(
            typeof(AcceleratorOwnerDomainGuard)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.ParameterType == snapshotType));
    }

    [Theory]
    [InlineData(AcceleratorGuardEvidenceSource.Telemetry)]
    [InlineData(AcceleratorGuardEvidenceSource.ReplayCertificateIdentity)]
    [InlineData(AcceleratorGuardEvidenceSource.TokenHandle)]
    public void L7SdcEvidenceIsNotAuthority_EvidencePlaneSourcesCannotAuthorizeDescriptorOrSubmit(
        AcceleratorGuardEvidenceSource source)
    {
        var telemetry = new AcceleratorTelemetry();
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidencePlane =
            AcceleratorGuardEvidence.FromEvidencePlane(
                source,
                ownerBinding,
                evidenceIdentity: 0xA11CE);

        AcceleratorDescriptorValidationResult descriptorResult =
            AcceleratorDescriptorParser.Parse(
                descriptorBytes,
                evidencePlane,
                L7SdcTestDescriptorFactory.CreateReference(descriptorBytes),
                telemetry);

        Assert.False(descriptorResult.IsValid);
        Assert.Equal(AcceleratorDescriptorFault.OwnerDomainFault, descriptorResult.Fault);

        L7SdcPhase07Fixture accepted =
            L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
        AcceleratorTokenAdmissionResult submit =
            new AcceleratorTokenStore(telemetry).Create(
                accepted.Descriptor,
                accepted.CapabilityAcceptance,
                evidencePlane);

        Assert.False(submit.IsAccepted);
        Assert.Null(submit.Token);
        Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, submit.FaultCode);
        Assert.Equal(AcceleratorTokenState.Created, accepted.Token.State);

        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.True(snapshot.DescriptorRejected >= 1);
        Assert.True(snapshot.SubmitRejected >= 1);
        Assert.Equal(0, snapshot.Lifecycle.Committed);
    }

    [Fact]
    public void L7SdcEvidenceIsNotAuthority_CapabilityMetadataAndTelemetryCannotAcceptCapability()
    {
        var telemetry = new AcceleratorTelemetry();
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence telemetryEvidence =
            AcceleratorGuardEvidence.FromEvidencePlane(
                AcceleratorGuardEvidenceSource.Telemetry,
                ownerBinding,
                evidenceIdentity: 0x7E1E);
        AcceleratorGuardDecision telemetryDecision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeDescriptorAcceptance(
                ownerBinding,
                telemetryEvidence);
        var registry = new AcceleratorCapabilityRegistry(telemetry);
        registry.RegisterProvider(new MatMulCapabilityProvider());

        AcceleratorCapabilityAcceptanceResult noGuard =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                ownerBinding);
        AcceleratorCapabilityAcceptanceResult telemetryGuard =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                ownerBinding,
                telemetryDecision);

        Assert.True(noGuard.IsRejected);
        Assert.True(telemetryGuard.IsRejected);
        Assert.False(noGuard.GrantsCommandSubmissionAuthority);
        Assert.False(telemetryGuard.GrantsExecutionAuthority);

        AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
        Assert.Equal(2, snapshot.CapabilityQueryAttempts);
        Assert.Equal(2, snapshot.CapabilityQuerySuccess);
        Assert.Equal(0, snapshot.SubmitAccepted);
    }

    [Fact]
    public void L7SdcEvidenceIsNotAuthority_TelemetryCannotAuthorizeCommitOrExceptionPublication()
    {
        Processor.MainMemoryArea previousMemory = Processor.MainMemory;
        try
        {
            L7SdcPhase07TestFactory.InitializeMainMemory(0x10000);
            byte[] originalDestination =
                L7SdcPhase07TestFactory.Fill(0x44, 0x40);
            L7SdcPhase07TestFactory.WriteMainMemory(0x9000, originalDestination);

            var telemetry = new AcceleratorTelemetry();
            L7SdcPhase07Fixture fixture =
                L7SdcPhase07TestFactory.CreateAcceptedToken(telemetry: telemetry);
            Assert.True(fixture.Token.MarkValidated(fixture.Evidence).Succeeded);
            Assert.True(fixture.Token.MarkQueued(fixture.Evidence).Succeeded);
            Assert.True(fixture.Token.MarkRunning(fixture.Evidence).Succeeded);
            var staging = new AcceleratorStagingBuffer();
            Assert.True(staging.StageWrite(
                fixture.Token,
                new AcceleratorMemoryRange(0x9000, 0x40),
                L7SdcPhase07TestFactory.Fill(0xE1, 0x40),
                fixture.Evidence).IsAccepted);
            Assert.True(fixture.Token.MarkDeviceComplete(fixture.Evidence).Succeeded);

            AcceleratorGuardEvidence telemetryEvidence =
                AcceleratorGuardEvidence.FromEvidencePlane(
                    AcceleratorGuardEvidenceSource.Telemetry,
                    fixture.Descriptor.OwnerBinding,
                    evidenceIdentity: (ulong)telemetry.Snapshot().EvidenceRecords.Count);
            AcceleratorCommitResult commit =
                new AcceleratorCommitCoordinator(telemetry).TryCommit(
                    fixture.Token,
                    fixture.Descriptor,
                    staging,
                    Processor.MainMemory,
                    telemetryEvidence,
                    AcceleratorCommitInvalidationPlan.None,
                    commitConflictPlaceholderAccepted: true);
            AcceleratorGuardDecision exceptionPublication =
                AcceleratorOwnerDomainGuard.Default.EnsureBeforeExceptionPublication(
                    fixture.Descriptor,
                    telemetryEvidence);

            Assert.True(commit.IsRejected);
            Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, commit.FaultCode);
            Assert.False(commit.RequiresRetireExceptionPublication);
            Assert.False(exceptionPublication.IsAllowed);
            Assert.Equal(AcceleratorGuardFault.EvidenceSourceNotAuthority, exceptionPublication.Fault);
            Assert.Equal(
                originalDestination,
                L7SdcPhase07TestFactory.ReadMainMemory(0x9000, originalDestination.Length));

            AcceleratorTelemetrySnapshot snapshot = telemetry.Snapshot();
            Assert.Equal(0, snapshot.Lifecycle.Committed);
            Assert.Equal(0UL, snapshot.Bytes.BytesCommitted);
        }
        finally
        {
            Processor.MainMemory = previousMemory;
            Processor.Memory = null;
        }
    }
}
