using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09BurstIoDmaMainMemoryBindingSeamTests
{
    [Fact]
    public void BurstRead_WhenDmaCompletionSwapsGlobalMainMemory_UsesSeededMemorySurface()
    {
        const ulong address = 0x2000UL;
        const int elementCount = 2048;
        const int elementSize = sizeof(uint);
        const ushort stride = elementSize;
        int byteCount = elementCount * elementSize;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        DMAController? originalDmaController = Processor.DMAController;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x4000UL);
        var replacementMemory = new Processor.MultiBankMemoryArea(4, 0x4000UL);

        Processor proc = default;
        DMAController dmaController = new DMAController(ref proc);
        EventHandler<DMAController.TransferCompletedEventArgs>? handler = null;

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Processor.DMAController = dmaController;

            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x4000,
                permissions: IOMMUAccessPermissions.ReadWrite));

            byte[] expected = new byte[byteCount];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = (byte)(i & 0xFF);
            }

            Assert.True(seededMemory.TryWritePhysicalRange(address, expected));
            bool swapped = false;
            handler = (_, args) =>
            {
                if (args.ChannelID == 0 && !swapped)
                {
                    Processor.MainMemory = replacementMemory;
                    swapped = true;
                }
            };
            dmaController.TransferCompleted += handler;

            byte[] destination = new byte[byteCount];
            ulong completed = BurstIO.BurstRead(
                address,
                destination,
                (ulong)elementCount,
                elementSize,
                stride);

            Assert.True(swapped);
            Assert.Equal((ulong)elementCount, completed);
            Assert.Equal(expected, destination);
        }
        finally
        {
            if (handler != null)
            {
                dmaController.TransferCompleted -= handler;
            }

            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.DMAController = originalDmaController;
        }
    }
}
