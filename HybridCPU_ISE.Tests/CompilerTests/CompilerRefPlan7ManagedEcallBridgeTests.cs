using HybridCPU.ManagedRuntime;
using HybridCPU.Platform.Contracts;
using HybridCPU.RuntimeKernel;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerRefPlan7ManagedEcallBridgeTests
{
    [Fact]
    public void RetirementHandler_TransfersReceiverResultAndAdvancesOneBundle()
    {
        DeterministicRuntimeKernelV1 kernel = KernelWithContext(0);
        ulong observedReceiver = 0;
        var bridge = new HybridCpuIseManagedEcallBridgeV1(kernel, receiver =>
        {
            observedReceiver = receiver;
            return new(HybridCpuManagedShapeStatusV1.Success, string.Empty, 0x3000_0040);
        });
        var state = Envelope(pc: 0x0010_0000, receiver: 0x3000_0010);
        var handler = new PipelineFsmEventHandler(new CsrFile(), ecallBridge: bridge);

        PipelineState result = handler.Handle(Ecall(), PipelineState.Task, state, PrivilegeLevel.User);

        Assert.Equal(PipelineState.Task, result);
        Assert.Equal(0x3000_0010UL, observedReceiver);
        Assert.Equal(0x3000_0040UL, Read(state, HybridCpuExternalServiceEcallContractV1.ResultValueRegister));
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.Success,
            Read(state, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));
        Assert.Equal(0x0010_0100UL, state.ReadPc(0));
    }

    [Fact]
    public void InvalidEnvelope_IsContainedWithoutCallingRuntime()
    {
        int calls = 0;
        var bridge = new HybridCpuIseManagedEcallBridgeV1(KernelWithContext(0), _ =>
        {
            calls++;
            return new(HybridCpuManagedShapeStatusV1.Success, string.Empty, 1);
        });
        var state = Envelope(0x1000, 0x3000_0010);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ArgumentCountRegister, 2);

        Assert.True(bridge.TryHandle(Ecall(), state, PrivilegeLevel.User));
        Assert.Equal(0, calls);
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.InvalidRequest,
            Read(state, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));
        Assert.Equal(0x1100UL, state.ReadPc(0));
    }

    [Fact]
    public void UnknownServiceAndUnrepresentableNextPc_FallThroughToTrapPath()
    {
        var bridge = new HybridCpuIseManagedEcallBridgeV1(KernelWithContext(0), _ =>
            new(HybridCpuManagedShapeStatusV1.Success, string.Empty, 1));
        var unknown = Envelope(0x1000, 1);
        unknown.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ServiceRegister,
            (ulong)HybridCpuHostServiceV1.Console);
        Assert.False(bridge.TryHandle(Ecall(), unknown, PrivilegeLevel.User));

        var overflow = Envelope(ulong.MaxValue - 255, 1);
        Assert.False(bridge.TryHandle(Ecall(), overflow, PrivilegeLevel.User));
        Assert.Equal(ulong.MaxValue - 255, overflow.ReadPc(0));
    }

    [Fact]
    public void NullReferenceAllocation_RequiresZeroArgumentsAndReturnsRootedObject()
    {
        int calls = 0;
        var bridge = new HybridCpuIseManagedEcallBridgeV1(KernelWithContext(0),
            allocateNullReferenceException: () =>
            {
                calls++;
                return new(HybridCpuManagedShapeStatusV1.Success, string.Empty, 0x3000_0080);
            });
        var state = Envelope(0x1000, 0);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.OperationRegister,
            HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ArgumentCountRegister, 0);

        Assert.True(bridge.TryHandle(Ecall(), state, PrivilegeLevel.User));
        Assert.Equal(1, calls);
        Assert.Equal(0x3000_0080UL, Read(state, HybridCpuExternalServiceEcallContractV1.ResultValueRegister));
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.Success,
            Read(state, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));
    }

    [Fact]
    public void NullReferenceAllocation_RejectsMissingObjectAndWrongArgumentCount()
    {
        int calls = 0;
        var bridge = new HybridCpuIseManagedEcallBridgeV1(KernelWithContext(0),
            allocateNullReferenceException: () =>
            {
                calls++;
                return new(HybridCpuManagedShapeStatusV1.Success, string.Empty, 0);
            });
        var missing = Envelope(0x1000, 0);
        missing.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.OperationRegister,
            HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation);
        missing.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ArgumentCountRegister, 0);
        Assert.True(bridge.TryHandle(Ecall(), missing, PrivilegeLevel.User));
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.ProviderFailure,
            Read(missing, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));

        var wrongCount = Envelope(0x2000, 0);
        wrongCount.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.OperationRegister,
            HybridCpuManagedRuntimeEcallContractV1.AllocateNullReferenceExceptionOperation);
        Assert.True(bridge.TryHandle(Ecall(), wrongCount, PrivilegeLevel.User));
        Assert.Equal(1, calls);
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.InvalidRequest,
            Read(wrongCount, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));
    }

    [Fact]
    public void ObjectAllocation_RecordsExactProviderFailureReason()
    {
        const string reason = "Allocation requires an exact runtime-owned class TypeDescriptor handle and digest.";
        var bridge = new HybridCpuIseManagedEcallBridgeV1(KernelWithContext(0),
            allocateObject: _ => new(false, 0, reason));
        var state = Envelope(0x1000, 230);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.OperationRegister,
            HybridCpuManagedRuntimeEcallContractV1.AllocateObjectOperation);

        Assert.True(bridge.TryHandle(Ecall(), state, PrivilegeLevel.User));
        Assert.Equal((ulong)HybridCpuExternalServiceStatusV1.ProviderFailure,
            Read(state, HybridCpuExternalServiceEcallContractV1.ResultStatusRegister));
        Assert.Equal(5UL, Read(state, HybridCpuExternalServiceEcallContractV1.ResultErrorRegister));
        Assert.Equal(reason, bridge.LastManagedReason);
    }

    private static FsmCpuState Envelope(ulong pc, ulong receiver)
    {
        var state = new FsmCpuState();
        state.WritePc(0, pc);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ServiceRegister,
            (ulong)HybridCpuHostServiceV1.ManagedRuntime);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.OperationRegister,
            HybridCpuManagedRuntimeEcallContractV1.ArgumentExceptionGetMessageOperation);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.BufferAddressRegister, 0);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.BufferLengthRegister, 0);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.BufferAccessRegister,
            (ulong)HybridCpuHostBufferAccessV1.None);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.ArgumentCountRegister, 1);
        state.WriteRegister(0, HybridCpuExternalServiceEcallContractV1.FirstArgumentRegister, receiver);
        return state;
    }

    private static EcallEvent Ecall() => new()
    {
        VtId = 0, BundleSerial = 1,
        EcallCode = unchecked((long)HybridCpuExternalServiceEcallContractV1.EcallNumber)
    };

    private static ulong Read(FsmCpuState state, int register) =>
        unchecked((ulong)state.ReadRegister(0, register));

    private static DeterministicRuntimeKernelV1 KernelWithContext(int carrier)
    {
        var kernel = new DeterministicRuntimeKernelV1();
        Assert.True(kernel.Boot(new(HybridCpuPlatformContractV1.ContractDigest, new string('a', 64),
            0x0010_0000, 4096, 0x0010_0000, 0x0020_0000, 4096, 0)).IsSuccess);
        return kernel;
    }
}
