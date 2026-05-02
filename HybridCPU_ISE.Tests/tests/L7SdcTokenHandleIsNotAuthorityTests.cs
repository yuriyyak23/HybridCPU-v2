using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcTokenHandleIsNotAuthorityTests
{
    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_HandleArithmeticAndForgedHandlesReject()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorTokenHandle forged =
            AcceleratorTokenHandle.FromOpaqueValue(fixture.Token.Handle.Value + 1);

        AcceleratorTokenLookupResult lookup =
            fixture.Store.Poll(forged, fixture.Evidence);

        Assert.True(lookup.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.InvalidHandle, lookup.FaultCode);
        Assert.Null(lookup.Token);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_TokenHandleEvidenceCannotSatisfyGuard()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorGuardEvidence tokenHandleEvidence =
            AcceleratorGuardEvidence.FromEvidencePlane(
                AcceleratorGuardEvidenceSource.TokenHandle,
                fixture.Descriptor.OwnerBinding,
                evidenceIdentity: fixture.Token.Handle.Value);

        AcceleratorTokenLookupResult lookup =
            fixture.Store.Poll(fixture.Token.Handle, tokenHandleEvidence);

        Assert.True(lookup.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.TokenHandleNotAuthority, lookup.FaultCode);
        Assert.False(lookup.SideEffectsAllowed);
        Assert.Equal(AcceleratorTokenState.Created, fixture.Token.State);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_DescriptorWithoutGuardCannotCreateToken()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorCommandDescriptor unguardedDescriptor = fixture.Descriptor with
        {
            OwnerGuardDecision = default
        };

        AcceleratorTokenAdmissionResult result =
            new AcceleratorTokenStore().Create(
                unguardedDescriptor,
                fixture.CapabilityAcceptance,
                fixture.Evidence);

        Assert.True(result.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.DescriptorNotGuardBacked, result.FaultCode);
        Assert.Equal(AcceleratorTokenHandle.Invalid, result.Handle);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_OwnerDriftBlocksLookupSideEffects()
    {
        TokenFixture fixture = CreateAcceptedToken();
        fixture.Token.MarkValidated(fixture.Evidence);
        AcceleratorOwnerBinding driftedOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerContextId: 0xBAD);
        AcceleratorGuardEvidence driftedEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                driftedOwner,
                activeDomainCertificate: fixture.Descriptor.OwnerBinding.DomainTag);

        AcceleratorTokenLookupResult cancel =
            fixture.Store.Cancel(fixture.Token.Handle, driftedEvidence);

        Assert.True(cancel.IsRejected);
        Assert.False(cancel.SideEffectsAllowed);
        Assert.Equal(AcceleratorTokenFaultCode.OwnerDomainRejected, cancel.FaultCode);
        Assert.Equal(AcceleratorTokenState.Validated, cancel.StatusWord.State);
        Assert.Equal(AcceleratorTokenFaultCode.OwnerDomainRejected, cancel.StatusWord.FaultCode);
        Assert.Equal(AcceleratorTokenState.Validated, fixture.Token.State);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_MappingEpochDriftBlocksObservationAndCommitPublication()
    {
        TokenFixture fixture = CreateAcceptedToken(mappingEpoch: 10, iommuDomainEpoch: 20);
        fixture.Token.MarkValidated(fixture.Evidence);
        AcceleratorGuardEvidence mappingDrift =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                fixture.Descriptor.OwnerBinding,
                activeDomainCertificate: fixture.Descriptor.OwnerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(11),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(20));
        AcceleratorGuardEvidence iommuDrift =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                fixture.Descriptor.OwnerBinding,
                activeDomainCertificate: fixture.Descriptor.OwnerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(10),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(21));

        AcceleratorTokenLookupResult poll =
            fixture.Store.Poll(fixture.Token.Handle, mappingDrift);
        AcceleratorTokenLookupResult commit =
            fixture.Store.TryCommitPublication(fixture.Token.Handle, iommuDrift);

        Assert.True(poll.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.MappingEpochDrift, poll.FaultCode);
        Assert.Equal(AcceleratorTokenState.Validated, poll.StatusWord.State);
        Assert.True(commit.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.IommuDomainEpochDrift, commit.FaultCode);
        Assert.Equal(AcceleratorTokenState.Validated, commit.StatusWord.State);
        Assert.False(commit.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.Validated, fixture.Token.State);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_CommitPublicationIsForbiddenEvenWithCurrentGuard()
    {
        TokenFixture fixture = CreateAcceptedToken();
        AcceleratorToken token = fixture.Token;
        token.MarkValidated(fixture.Evidence);
        token.MarkQueued(fixture.Evidence);
        token.MarkRunning(fixture.Evidence);
        token.MarkDeviceComplete(fixture.Evidence);
        AcceleratorTokenTransition commitPending =
            token.MarkCommitPending(fixture.Evidence);

        AcceleratorTokenLookupResult result =
            fixture.Store.TryCommitPublication(token.Handle, fixture.Evidence);

        Assert.True(commitPending.Rejected);
        Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, commitPending.FaultCode);
        Assert.True(result.IsRejected);
        Assert.Equal(AcceleratorTokenFaultCode.CommitCoordinatorRequired, result.FaultCode);
        Assert.False(result.UserVisiblePublicationAllowed);
        Assert.Equal(AcceleratorTokenState.DeviceComplete, token.State);
        Assert.NotEqual(AcceleratorTokenState.Committed, token.State);
    }

    [Fact]
    public void L7SdcTokenHandleIsNotAuthority_ModelOperationsDoNotWriteMemoryOrFallback()
    {
        TokenFixture fixture = CreateAcceptedToken();
        Processor.MainMemoryArea previousMainMemory = Processor.MainMemory;
        var previousMemorySubsystem = Processor.Memory;
        Processor.MainMemory = new Processor.MultiBankMemoryArea(1, 0x1000);
        Processor.Memory = null;
        byte[] original = { 0x33, 0x44, 0x55, 0x66 };

        try
        {
            Assert.True(Processor.MainMemory.TryWritePhysicalRange(0x100, original));

            fixture.Store.Poll(fixture.Token.Handle, fixture.Evidence);
            fixture.Store.Wait(fixture.Token.Handle, fixture.Evidence);
            fixture.Store.Fence(fixture.Token.Handle, fixture.Evidence);
            fixture.Store.Cancel(fixture.Token.Handle, fixture.Evidence);

            byte[] observed = new byte[original.Length];
            Assert.True(Processor.MainMemory.TryReadPhysicalRange(0x100, observed));
            Assert.Equal(original, observed);
            Assert.False(fixture.Token.HasBackendExecution);
            Assert.False(fixture.Token.HasQueueExecution);
            Assert.False(fixture.Token.HasStagedWrites);
            Assert.False(fixture.Token.HasArchitecturalCommit);
            var core = new Processor.CPU_Core(0);
            Assert.Throws<InvalidOperationException>(
                () => new AcceleratorSubmitMicroOp(fixture.Descriptor).Execute(ref core));
        }
        finally
        {
            Processor.MainMemory = previousMainMemory;
            Processor.Memory = previousMemorySubsystem;
        }
    }

    private static TokenFixture CreateAcceptedToken(
        ulong mappingEpoch = 0,
        ulong iommuDomainEpoch = 0)
    {
        byte[] descriptorBytes = L7SdcTestDescriptorFactory.BuildDescriptor();
        AcceleratorOwnerBinding ownerBinding =
            L7SdcTestDescriptorFactory.ReadOwnerBinding(descriptorBytes);
        AcceleratorGuardEvidence evidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                ownerBinding,
                activeDomainCertificate: ownerBinding.DomainTag,
                mappingEpoch: new AcceleratorMappingEpoch(mappingEpoch),
                iommuDomainEpoch: new AcceleratorIommuDomainEpoch(iommuDomainEpoch));
        AcceleratorDescriptorValidationResult descriptorResult =
            L7SdcTestDescriptorFactory.ParseWithGuard(descriptorBytes, evidence);
        Assert.True(descriptorResult.IsValid, descriptorResult.Message);
        AcceleratorCommandDescriptor descriptor = descriptorResult.RequireDescriptor();
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            L7SdcCapabilityRegistryTests.CreateRegistry().AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore();
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
