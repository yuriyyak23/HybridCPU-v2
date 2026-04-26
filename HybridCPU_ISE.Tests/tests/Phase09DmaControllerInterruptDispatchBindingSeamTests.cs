using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// Proves that <see cref="DMAController.RaiseInterrupt"/> routes interrupt dispatch
/// through the captured delegate (D3-I binding seam) instead of the mutable global
/// <see cref="Processor.InterruptData.CallInterrupt"/>.
///
/// Strategy: construct DMAController with an explicit interrupt-dispatch delegate,
/// run a small transfer to completion, verify the delegate received the call.
/// </summary>
public sealed class Phase09DmaControllerInterruptDispatchBindingSeamTests
{
    private static void InitializeTestEnvironment()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
    }

    /// <summary>
    /// Proves that a DMAController constructed with an explicit interrupt-dispatch delegate
    /// routes the completion interrupt through that delegate, not the global
    /// Processor.InterruptData.CallInterrupt.
    /// </summary>
    [Fact]
    public void CompleteChannel_WhenInterruptDispatchDelegateProvided_RoutesToBoundDelegate()
    {
        InitializeTestEnvironment();

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;

        var capturedCalls = new List<(Processor.DeviceType Device, ushort InterruptId, ulong CoreId)>();
        Action<Processor.DeviceType, ushort, ulong> boundDispatch = (device, interruptId, coreId) =>
        {
            capturedCalls.Add((device, interruptId, coreId));
        };

        // Seed a small memory so PerformBurst can do a direct read/write
        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x4000);

        try
        {
            Processor.MainMemory = seededMemory;

            Processor proc = default;
            var dma = new DMAController(ref proc, interruptDispatch: boundDispatch);

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x0000,
                DestAddress = 0x1000,
                TransferSize = 64,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            };

            bool configured = dma.ConfigureTransfer(descriptor);
            Assert.True(configured);

            bool started = dma.StartTransfer(0);
            Assert.True(started);

            // Run enough cycles for the small transfer to complete
            for (int cycle = 0; cycle < 10; cycle++)
            {
                dma.ExecuteCycle();
            }

            Assert.Equal(DMAController.ChannelState.Completed, dma.GetChannelState(0));

            Assert.True(capturedCalls.Count > 0,
                "Bound interrupt-dispatch delegate should have been invoked on DMA transfer completion.");

            // Completion interrupt for channel 0: base 0x90 + channel 0 = 0x90
            Assert.Contains(capturedCalls, c =>
                c.Device == Processor.DeviceType.DMAController &&
                c.InterruptId == 0x90 &&
                c.CoreId == 0);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    /// <summary>
    /// Proves that a DMAController constructed without an interrupt-dispatch delegate
    /// (null default) falls back to the global Processor.InterruptData.CallInterrupt
    /// without crashing.
    ///
    /// No interrupt handler is registered for the DMA completion interrupt (0x90),
    /// so CallInterrupt returns 0xFE ("no handler") early — this avoids entering
    /// the interrupt handler which would attempt a VLIW bundle prefetch through the
    /// IOMMU virtual path. The test still exercises the full fallback dispatch path.
    /// </summary>
    [Fact]
    public void CompleteChannel_WhenInterruptDispatchIsNull_FallsBackToGlobalWithoutCrash()
    {
        InitializeTestEnvironment();

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        Processor.CPU_Core[] originalCores = Processor.CPU_Cores;

        var seededMemory = new Processor.MainMemoryArea();
        seededMemory.SetLength(0x10000);

        try
        {
            Processor.MainMemory = seededMemory;

            // Ensure at least one core exists so CallInterrupt can index CPU_Cores[0]
            Processor.CPU_Cores = new Processor.CPU_Core[1];
            Processor.CPU_Cores[0] = new Processor.CPU_Core(0);

            // Do NOT register a handler at 0x90 — CallInterrupt will hit the
            // "no handler registered" early return (handlerAddress == 0 → 0xFE),
            // proving the global fallback path is reachable without side-effects.
            Processor.InterruptData.UnregisterHandler(0x90);

            Processor proc = default;
            var dma = new DMAController(ref proc); // no interruptDispatch → null fallback

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = 0x3000,
                DestAddress = 0x4000,
                TransferSize = 64,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            };

            bool configured = dma.ConfigureTransfer(descriptor);
            Assert.True(configured);

            bool started = dma.StartTransfer(0);
            Assert.True(started);

            // Run enough cycles for the small transfer to complete — should not throw
            for (int cycle = 0; cycle < 10; cycle++)
            {
                dma.ExecuteCycle();
            }

            Assert.Equal(DMAController.ChannelState.Completed, dma.GetChannelState(0));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.CPU_Cores = originalCores;
        }
    }
}
