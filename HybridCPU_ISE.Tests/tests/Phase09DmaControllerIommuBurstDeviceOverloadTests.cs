using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09DmaControllerIommuBurstDeviceOverloadTests
{
    [Fact]
    public void DmaController_WhenUseIommuBurstTransferWithoutThreadDomain_ThenCompletesThroughDeviceOverload()
    {
        const ulong sourceAddress = 0x2000UL;
        const ulong destinationAddress = 0x0000UL;
        byte[] expected = new byte[1024];
        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = (byte)(i & 0xFF);
        }

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        var seededMemory = new Processor.MultiBankMemoryArea(4, 0x4000UL);
        Processor proc = default;
        DMAController dmaController = new DMAController(ref proc);
        EventHandler<DMAController.TransferCompletedEventArgs>? handler = null;

        try
        {
            Processor.MainMemory = seededMemory;

            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x4000,
                permissions: IOMMUAccessPermissions.ReadWrite));

            Assert.True(seededMemory.TryWritePhysicalRange(sourceAddress, expected));

            bool completed = false;
            bool success = false;
            handler = (_, args) =>
            {
                if (args.ChannelID == 0)
                {
                    completed = true;
                    success = !args.IsError;
                }
            };
            dmaController.TransferCompleted += handler;

            var descriptor = new DMAController.TransferDescriptor
            {
                SourceAddress = sourceAddress,
                DestAddress = destinationAddress,
                TransferSize = (uint)expected.Length,
                SourceStride = 0,
                DestStride = 0,
                ElementSize = 1,
                UseIOMMU = true,
                NextDescriptor = 0,
                ChannelID = 0,
                Priority = 128
            };

            Assert.True(dmaController.ConfigureTransfer(descriptor));
            Assert.True(dmaController.StartTransfer(0));

            for (int i = 0; i < 16 && !completed; i++)
            {
                dmaController.ExecuteCycle();
            }

            Assert.True(completed);
            Assert.True(success);
            byte[] materialized = new byte[expected.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(destinationAddress, materialized));
            Assert.Equal(expected, materialized);
        }
        finally
        {
            if (handler != null)
            {
                dmaController.TransferCompleted -= handler;
            }

            Processor.MainMemory = originalMainMemory;
        }
    }
}
