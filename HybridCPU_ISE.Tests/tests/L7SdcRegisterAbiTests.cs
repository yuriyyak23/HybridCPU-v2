using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcRegisterAbiTests
{
    [Fact]
    public void L7SdcRegisterAbi_SubmitAcceptedWritesNonzeroOpaqueHandle()
    {
        TokenFixture fixture = CreateAcceptedToken();

        AcceleratorRegisterAbiResult abi =
            AcceleratorRegisterAbi.FromSubmitAdmission(fixture.Admission);

        Assert.True(abi.WritesRegister);
        Assert.Equal(fixture.Token.Handle.Value, abi.RegisterValue);
        Assert.NotEqual(0UL, abi.RegisterValue);
    }

    [Fact]
    public void L7SdcRegisterAbi_ModelWriteResultDoesNotImplyCarrierRdWriteback()
    {
        TokenFixture fixture = CreateAcceptedToken();

        AcceleratorRegisterAbiResult abi =
            AcceleratorRegisterAbi.FromSubmitAdmission(fixture.Admission);
        var carrier = new AcceleratorSubmitMicroOp(fixture.Token.Descriptor);

        Assert.True(abi.WritesRegister);
        Assert.NotEqual(0UL, abi.RegisterValue);
        Assert.False(carrier.WritesRegister);
        Assert.Empty(carrier.WriteRegisters);
    }

    [Fact]
    public void L7SdcRegisterAbi_SubmitRejectWritesZeroOrPreciseFaultNoWrite()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCapabilityRegistry registry =
            L7SdcCapabilityRegistryTests.CreateRegistry();
        AcceleratorCapabilityAcceptanceResult unguardedCapability =
            registry.AcceptCapability("matmul.fixture.v1", descriptor.OwnerBinding);

        AcceleratorTokenAdmissionResult nonTrappingReject =
            new AcceleratorTokenStore().Create(
                descriptor,
                unguardedCapability,
                descriptor.OwnerGuardDecision.Evidence);
        AcceleratorTokenAdmissionResult preciseFault =
            new AcceleratorTokenStore().Create(
                descriptor,
                unguardedCapability,
                descriptor.OwnerGuardDecision.Evidence,
                AcceleratorTokenAdmissionRejectPolicy.PreciseFault);

        AcceleratorRegisterAbiResult nonTrappingAbi =
            AcceleratorRegisterAbi.FromSubmitAdmission(nonTrappingReject);
        AcceleratorRegisterAbiResult preciseFaultAbi =
            AcceleratorRegisterAbi.FromSubmitAdmission(preciseFault);

        Assert.True(nonTrappingReject.IsNonTrappingReject);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, nonTrappingReject.FaultCode);
        Assert.True(nonTrappingAbi.WritesRegister);
        Assert.Equal(0UL, nonTrappingAbi.RegisterValue);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, nonTrappingAbi.FaultCode);

        Assert.True(preciseFault.RequiresPreciseFault);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, preciseFault.FaultCode);
        Assert.True(preciseFaultAbi.RequiresPreciseFault);
        Assert.False(preciseFaultAbi.WritesRegister);
        Assert.Equal(0UL, preciseFaultAbi.RegisterValue);
        Assert.Equal(AcceleratorTokenFaultCode.CapabilityRejected, preciseFaultAbi.FaultCode);
    }

    [Fact]
    public void L7SdcRegisterAbi_StatusWordPackUnpackIsStableAndReservedBitsAreZero()
    {
        var status = new AcceleratorTokenStatusWord(
            AcceleratorTokenState.Running,
            AcceleratorTokenFaultCode.None,
            AcceleratorTokenStatusFlags.ModelOnly,
            ImplementationStatusSequence: 0xA5A5_0102);

        ulong packed = status.Pack();
        AcceleratorTokenStatusWord unpacked =
            AcceleratorTokenStatusWord.Unpack(packed);
        AcceleratorTokenStatusWord stripped =
            AcceleratorTokenStatusWord.Unpack(packed | (0xFFUL << AcceleratorTokenStatusWord.ReservedZeroShift));

        Assert.False(AcceleratorTokenStatusWord.HasReservedBitsSet(packed));
        Assert.Equal(status, unpacked);
        Assert.Equal(0UL, (packed >> AcceleratorTokenStatusWord.ReservedZeroShift) & 0xFFUL);
        Assert.Equal(status, stripped with { ImplementationStatusSequence = status.ImplementationStatusSequence });
        Assert.False(AcceleratorTokenStatusWord.HasReservedBitsSet(stripped.Pack()));
    }

    [Fact]
    public void L7SdcRegisterAbi_PollWaitCancelFenceWriteStatusOnlyAfterGuardedLookup()
    {
        TokenFixture fixture = CreateAcceptedToken();
        fixture.Token.MarkValidated(fixture.Evidence);

        AcceleratorRegisterAbiResult poll =
            AcceleratorRegisterAbi.FromStatusLookup(
                fixture.Store.Poll(fixture.Token.Handle, fixture.Evidence));
        AcceleratorRegisterAbiResult wait =
            AcceleratorRegisterAbi.FromStatusLookup(
                fixture.Store.Wait(fixture.Token.Handle, fixture.Evidence));
        AcceleratorRegisterAbiResult fence =
            AcceleratorRegisterAbi.FromStatusLookup(
                fixture.Store.Fence(fixture.Token.Handle, fixture.Evidence));
        AcceleratorRegisterAbiResult cancel =
            AcceleratorRegisterAbi.FromStatusLookup(
                fixture.Store.Cancel(fixture.Token.Handle, fixture.Evidence));

        Assert.True(poll.WritesRegister);
        Assert.True(wait.WritesRegister);
        Assert.True(fence.WritesRegister);
        Assert.True(cancel.WritesRegister);

        Assert.Equal(
            AcceleratorTokenState.Validated,
            AcceleratorTokenStatusWord.Unpack(poll.RegisterValue).State);
        Assert.Equal(
            AcceleratorTokenState.Validated,
            AcceleratorTokenStatusWord.Unpack(wait.RegisterValue).State);
        Assert.Equal(
            AcceleratorTokenState.Validated,
            AcceleratorTokenStatusWord.Unpack(fence.RegisterValue).State);
        Assert.Equal(
            AcceleratorTokenState.Canceled,
            AcceleratorTokenStatusWord.Unpack(cancel.RegisterValue).State);
        Assert.Equal(AcceleratorTokenState.Canceled, fixture.Token.State);
    }

    [Fact]
    public void L7SdcRegisterAbi_QueryCapsRequiresGuardBackedCapabilityAcceptance()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCapabilityRegistry registry =
            L7SdcCapabilityRegistryTests.CreateRegistry();
        AcceleratorCapabilityQueryResult metadataOnly =
            registry.Query("matmul.fixture.v1");
        AcceleratorCapabilityAcceptanceResult unguarded =
            registry.AcceptCapability("matmul.fixture.v1", descriptor.OwnerBinding);
        AcceleratorCapabilityAcceptanceResult guarded =
            registry.AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);

        AcceleratorRegisterAbiResult unguardedAbi =
            AcceleratorRegisterAbi.FromCapabilityQuery(unguarded);
        AcceleratorRegisterAbiResult guardedAbi =
            AcceleratorRegisterAbi.FromCapabilityQuery(guarded);

        Assert.True(metadataOnly.IsMetadataAvailable);
        Assert.False(metadataOnly.GrantsCommandSubmissionAuthority);
        Assert.False(unguardedAbi.WritesRegister);
        Assert.True(guardedAbi.WritesRegister);
        Assert.NotEqual(0UL, guardedAbi.RegisterValue);
        Assert.Equal(1UL, guardedAbi.RegisterValue & 0x1UL);
    }

    private static TokenFixture CreateAcceptedToken()
    {
        AcceleratorCommandDescriptor descriptor =
            L7SdcTestDescriptorFactory.ParseValidDescriptor();
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance =
            L7SdcCapabilityRegistryTests.CreateRegistry().AcceptCapability(
                "matmul.fixture.v1",
                descriptor.OwnerBinding,
                descriptor.OwnerGuardDecision);
        var store = new AcceleratorTokenStore();
        AcceleratorGuardEvidence evidence = descriptor.OwnerGuardDecision.Evidence!;
        AcceleratorTokenAdmissionResult admission =
            store.Create(descriptor, capabilityAcceptance, evidence);

        Assert.True(admission.IsAccepted, admission.Message);
        return new TokenFixture(store, admission.Token!, admission, evidence);
    }

    private sealed record TokenFixture(
        AcceleratorTokenStore Store,
        AcceleratorToken Token,
        AcceleratorTokenAdmissionResult Admission,
        AcceleratorGuardEvidence Evidence);
}
