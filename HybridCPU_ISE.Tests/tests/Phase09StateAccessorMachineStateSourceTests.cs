using System;
using HybridCPU_ISE;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09StateAccessorMachineStateSourceTests
{
    [Fact]
    public void GetCoreState_WhenMachineStateSourceIsInjected_UsesInjectedCoreAndCoreCount()
    {
        var injectedCore = CreateCoreWithLivePc(0x2222, activeVtId: 2);
        var service = new IseObservationService(
            new ObservationTestMachineStateSource(cores: new[] { injectedCore }),
            ObservationServiceTestFactory.SyncRoot);
        CoreStateSnapshot snapshot = service.GetCoreState(0);

        Assert.Equal(1, service.GetTotalCores());
        Assert.Equal(MachineStateSourceProvenance.LiveCore, service.SourceProvenance);
        Assert.Equal(0x2222UL, snapshot.LiveInstructionPointer);
        Assert.Equal(2, snapshot.ActiveVirtualThreadId);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetCoreState(1));
    }

    [Fact]
    public void ReadMemory_WhenMachineStateSourceProvidesMainMemory_UsesInjectedMemoryInsteadOfGlobalMemory()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var globalMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        var injectedMemory = new Processor.MultiBankMemoryArea(bankCount: 4, bankSize: 0x1000UL);
        byte[] globalBytes = BitConverter.GetBytes(0x1122_3344U);
        byte[] injectedBytes = BitConverter.GetBytes(0xAABB_CCDDU);

        try
        {
            Assert.True(globalMemory.TryWritePhysicalRange(0x40, globalBytes));
            Assert.True(injectedMemory.TryWritePhysicalRange(0x40, injectedBytes));
            Processor.MainMemory = globalMemory;

            var service = new IseObservationService(
                new ObservationTestMachineStateSource(mainMemory: injectedMemory),
                ObservationServiceTestFactory.SyncRoot);
            byte[] bytes = service.ReadMemory(0x40, injectedBytes.Length);

            Assert.Equal(injectedBytes, bytes);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    [Fact]
    public void GetPodSnapshot_WhenMachineStateSourceIsInstalled_UsesInjectedPodTopology()
    {
        PodController? originalPod = Processor.Pods[0];
        var injectedPod = new PodController(3, 4, new MicroOpScheduler())
        {
            DomainCertificate = 0x44
        };

        try
        {
            Processor.Pods[0] = null!;

            var service = new IseObservationService(
                new ObservationTestMachineStateSource(pods: new PodController?[] { injectedPod }),
                ObservationServiceTestFactory.SyncRoot);
            PodSnapshot snapshot = service.GetPodSnapshot(0);

            Assert.Equal(3, snapshot.PodX);
            Assert.Equal(4, snapshot.PodY);
            Assert.Equal(injectedPod.PodId, snapshot.PodId);
            Assert.Equal(0x44UL, snapshot.DomainCertificate);
        }
        finally
        {
            Processor.Pods[0] = originalPod!;
        }
    }

    private static Processor.CPU_Core CreateCoreWithLivePc(ulong livePc, int activeVtId)
    {
        var core = new Processor.CPU_Core(0);
        core.PrepareExecutionStart(livePc, activeVtId);
        core.ActiveVirtualThreadId = activeVtId;
        core.WriteVirtualThreadPipelineState((byte)activeVtId, PipelineState.Task);
        core.WriteActiveLivePc(livePc);
        return core;
    }
}
