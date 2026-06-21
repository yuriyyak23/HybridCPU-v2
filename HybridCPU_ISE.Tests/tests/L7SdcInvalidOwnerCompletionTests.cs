using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcInvalidOwnerCompletionTests
{
    [Fact]
    public void InvalidOwnerCompletion_ForbidsUserVisiblePublicationAndRecordsPrivilegedDiagnostics()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorOwnerBinding staleOwner =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(ownerContextId: 0xBAD);
        AcceleratorGuardEvidence staleEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                staleOwner,
                activeDomainCertificate: descriptor.OwnerBinding.DomainTag);

        AcceleratorInvalidOwnerCompletionResult completion =
            AcceleratorOwnerDomainGuard.Default.MarkAbandonedOnInvalidOwner(
                descriptor,
                staleEvidence,
                recordPrivilegedDiagnostic: true);

        Assert.False(completion.UserVisiblePublicationAllowed);
        Assert.True(completion.PrivilegedDiagnosticRecorded);
        Assert.Equal(AcceleratorGuardFault.OwnerMismatch, completion.GuardDecision.Fault);
        Assert.Equal(AcceleratorInvalidOwnerCompletionDisposition.Abandoned, completion.Disposition);
        Assert.Contains("forbidden", completion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExceptionPublicationToInvalidOwner_RequiresGuardAndRejects()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorOwnerBinding wrongDomain =
            L7SdcTestDescriptorFactory.CreateOwnerBinding(domainTag: 0x4000);
        AcceleratorGuardEvidence wrongDomainEvidence =
            L7SdcTestDescriptorFactory.CreateGuardEvidence(
                wrongDomain,
                activeDomainCertificate: wrongDomain.DomainTag);

        AcceleratorGuardDecision decision =
            AcceleratorOwnerDomainGuard.Default.EnsureBeforeExceptionPublication(
                descriptor,
                wrongDomainEvidence);

        Assert.False(decision.IsAllowed);
        Assert.Equal(AcceleratorGuardFault.DomainMismatch, decision.Fault);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.RejectKind.DomainMismatch, decision.LegalityDecision.RejectKind);
    }

    [Fact]
    public void GuardStillValidCompletion_RemainsModelEvidenceAndDoesNotCreateCommitPath()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorGuardEvidence currentEvidence =
            descriptor.OwnerGuardDecision.Evidence!;

        AcceleratorInvalidOwnerCompletionResult completion =
            AcceleratorOwnerDomainGuard.Default.MarkAbandonedOnInvalidOwner(
                descriptor,
                currentEvidence,
                recordPrivilegedDiagnostic: true);

        Assert.False(completion.UserVisiblePublicationAllowed);
        Assert.False(completion.PrivilegedDiagnosticRecorded);
        Assert.Equal(AcceleratorInvalidOwnerCompletionDisposition.None, completion.Disposition);
        Assert.True(completion.GuardDecision.IsAllowed, completion.GuardDecision.Message);
        Assert.Contains("no user-visible commit", completion.Message, StringComparison.OrdinalIgnoreCase);
    }
}
