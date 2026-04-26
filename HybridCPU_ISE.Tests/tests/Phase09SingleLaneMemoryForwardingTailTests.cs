using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09SingleLaneMemoryForwardingTailTests
{
    [Fact]
    public void SingleLaneMemory_WhenLoadPublishesMemResult_ThenForwardMemCarriesLoadedValueWithoutTimingMetadata()
    {
        const ushort destinationRegister = 10;
        const ulong address = 0x380UL;
        const ulong loadedValue = 0x0102_0304_0506_0708UL;
        const ulong pc = 0x8E00UL;

        InitializeCpuMainMemoryIdentityMap();
        WriteBytes(address, BitConverter.GetBytes(loadedValue));

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
            isMemoryOp: true,
            isLoad: true,
            writesRegister: true,
            destRegId: destinationRegister,
            memoryAddress: address,
            memoryAccessSize: 8,
            pc: pc,
            opCode: (uint)InstructionsEnum.Load);

        core.TestRunMemoryStageFromCurrentExecuteState();

        ForwardingPath forwardMem = core.TestGetMemoryForwardingPath();
        PipelineControl control = core.GetPipelineControl();

        Assert.True(forwardMem.Valid);
        Assert.Equal(destinationRegister, forwardMem.DestRegID);
        Assert.Equal(loadedValue, forwardMem.ForwardedValue);
        Assert.Equal(0L, forwardMem.AvailableCycle);
        Assert.Equal(PipelineStage.None, forwardMem.SourceStage);
        Assert.Equal(1UL, control.ForwardingEvents);
    }

    [Fact]
    public void SingleLaneMemory_WhenStoreDoesNotWriteRegister_ThenLeavesForwardMemClear()
    {
        const ulong address = 0x3C0UL;
        const ulong storeValue = 0x8877_6655_4433_2211UL;
        const ulong pc = 0x8E80UL;

        InitializeCpuMainMemoryIdentityMap();

        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.TestSeedSingleLaneExecuteForMemoryFollowThrough(
            isMemoryOp: true,
            isLoad: false,
            writesRegister: false,
            destRegId: 0,
            memoryAddress: address,
            memoryData: storeValue,
            memoryAccessSize: 8,
            pc: pc,
            opCode: (uint)InstructionsEnum.Store);

        core.TestRunMemoryStageFromCurrentExecuteState();

        ForwardingPath forwardMem = core.TestGetMemoryForwardingPath();
        PipelineControl control = core.GetPipelineControl();

        Assert.False(forwardMem.Valid);
        Assert.Equal(0UL, control.ForwardingEvents);
    }

    private static void WriteBytes(ulong address, byte[] bytes)
    {
        Processor.MainMemory.WriteToPosition(bytes, address);
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        Processor.MainMemory = new YAKSys_Hybrid_CPU.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
        YAKSys_Hybrid_CPU.MultiBankMemoryArea.SetAccessDomainTag(0);
    }
}
