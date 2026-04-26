using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09IommuAndDmaMainMemoryBindingSeamTests
{
    private sealed class SwappingMainMemoryArea : Processor.MainMemoryArea
    {
        private readonly Processor.MainMemoryArea _replacementMemory;
        private readonly bool _swapOnFirstRead;
        private readonly bool _swapOnFirstWrite;
        private bool _swappedOnRead;
        private bool _swappedOnWrite;

        public SwappingMainMemoryArea(
            ulong length,
            Processor.MainMemoryArea replacementMemory,
            bool swapOnFirstRead,
            bool swapOnFirstWrite)
        {
            SetLength((long)length);
            _replacementMemory = replacementMemory;
            _swapOnFirstRead = swapOnFirstRead;
            _swapOnFirstWrite = swapOnFirstWrite;
        }

        public override bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
        {
            bool result = base.TryReadPhysicalRange(physicalAddress, buffer);
            if (result && _swapOnFirstRead && !_swappedOnRead)
            {
                _swappedOnRead = true;
                Processor.MainMemory = _replacementMemory;
            }

            return result;
        }

        public override bool TryWritePhysicalRange(ulong physicalAddress, ReadOnlySpan<byte> buffer)
        {
            bool result = base.TryWritePhysicalRange(physicalAddress, buffer);
            if (result && _swapOnFirstWrite && !_swappedOnWrite)
            {
                _swappedOnWrite = true;
                Processor.MainMemory = _replacementMemory;
            }

            return result;
        }
    }

    [Fact]
    public void IommuReadBurst_WhenGlobalMainMemoryIsReplacedAfterFirstChunk_UsesSeededMemorySurface()
    {
        const ulong address = 0xFF0UL;
        const ulong memoryLength = 0x4000UL;
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        bool originalNullDomainGuardEnabled = IOMMU.NullDomainGuardEnabled;
        Processor.MainMemoryArea replacementMemory = CreateMainMemory(length: memoryLength);
        var seededMemory = new SwappingMainMemoryArea(
            length: memoryLength,
            replacementMemory,
            swapOnFirstRead: true,
            swapOnFirstWrite: false);
        byte[] expected = CreatePattern(length: 32, start: 0x10);
        byte[] replacementSeed = CreatePattern(length: 32, start: 0x90);

        try
        {
            Processor.MainMemory = seededMemory;
            SeedIommuIdentityMap(size: 0x2000UL);
            Assert.True(seededMemory.TryWritePhysicalRange(address, expected));
            Assert.True(replacementMemory.TryWritePhysicalRange(address, replacementSeed));

            byte[] buffer = new byte[expected.Length];

            Assert.True(IOMMU.ReadBurst(deviceID: 0UL, ioVirtualAddress: address, buffer));
            Assert.Equal(expected, buffer);

            byte[] replacementObserved = new byte[replacementSeed.Length];
            Assert.True(replacementMemory.TryReadPhysicalRange(address, replacementObserved));
            Assert.Equal(replacementSeed, replacementObserved);
        }
        finally
        {
            IOMMU.NullDomainGuardEnabled = originalNullDomainGuardEnabled;
            Processor.MainMemory = originalMainMemory;
        }
    }

    [Fact]
    public void IommuWriteBurst_WhenGlobalMainMemoryIsReplacedAfterFirstChunk_UsesSeededMemorySurface()
    {
        const ulong address = 0xFF0UL;
        const ulong memoryLength = 0x4000UL;
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        bool originalNullDomainGuardEnabled = IOMMU.NullDomainGuardEnabled;
        Processor.MainMemoryArea replacementMemory = CreateMainMemory(length: memoryLength);
        var seededMemory = new SwappingMainMemoryArea(
            length: memoryLength,
            replacementMemory,
            swapOnFirstRead: false,
            swapOnFirstWrite: true);
        byte[] payload = CreatePattern(length: 32, start: 0x20);
        byte[] replacementBaseline = CreatePattern(length: 32, start: 0xC0);

        try
        {
            Processor.MainMemory = seededMemory;
            SeedIommuIdentityMap(size: 0x2000UL);
            Assert.True(replacementMemory.TryWritePhysicalRange(address, replacementBaseline));

            Assert.True(IOMMU.WriteBurst(deviceID: 0UL, ioVirtualAddress: address, payload));

            byte[] seededObserved = new byte[payload.Length];
            byte[] replacementObserved = new byte[replacementBaseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(address, seededObserved));
            Assert.True(replacementMemory.TryReadPhysicalRange(address, replacementObserved));
            Assert.Equal(payload, seededObserved);
            Assert.Equal(replacementBaseline, replacementObserved);
        }
        finally
        {
            IOMMU.NullDomainGuardEnabled = originalNullDomainGuardEnabled;
            Processor.MainMemory = originalMainMemory;
        }
    }

    [Fact]
    public void DmaControllerNonIommuBurst_WhenGlobalMainMemoryIsReplacedAfterSourceRead_UsesSeededMemorySurface()
    {
        const ulong sourceAddress = 0x100UL;
        const ulong destinationAddress = 0x180UL;
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        Processor.MainMemoryArea replacementMemory = CreateMainMemory(length: 0x4000UL);
        var seededMemory = new SwappingMainMemoryArea(
            length: 0x4000UL,
            replacementMemory,
            swapOnFirstRead: true,
            swapOnFirstWrite: false);
        byte[] payload = CreatePattern(length: 8, start: 0x30);
        byte[] replacementBaseline = CreatePattern(length: 8, start: 0xE0);

        try
        {
            Processor.MainMemory = seededMemory;
            Assert.True(seededMemory.TryWritePhysicalRange(sourceAddress, payload));
            Assert.True(replacementMemory.TryWritePhysicalRange(destinationAddress, replacementBaseline));

            Processor proc = default;
            var dma = new DMAController(ref proc);
            Assert.True(dma.ConfigureTransfer(new DMAController.TransferDescriptor
            {
                SourceAddress = sourceAddress,
                DestAddress = destinationAddress,
                TransferSize = (uint)payload.Length,
                ElementSize = 1,
                ChannelID = 0,
                Priority = 128,
                UseIOMMU = false
            }));
            Assert.True(dma.StartTransfer(0));

            dma.ExecuteCycle();

            var (transferred, total) = dma.GetChannelProgress(0);
            Assert.Equal((uint)payload.Length, transferred);
            Assert.Equal((uint)payload.Length, total);

            byte[] seededObserved = new byte[payload.Length];
            byte[] replacementObserved = new byte[replacementBaseline.Length];
            Assert.True(seededMemory.TryReadPhysicalRange(destinationAddress, seededObserved));
            Assert.True(replacementMemory.TryReadPhysicalRange(destinationAddress, replacementObserved));
            Assert.Equal(payload, seededObserved);
            Assert.Equal(replacementBaseline, replacementObserved);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
        }
    }

    private static void SeedIommuIdentityMap(ulong size)
    {
        IOMMU.Initialize();
        IOMMU.NullDomainGuardEnabled = false;
        IOMMU.RegisterDevice(0);
        Assert.True(IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: size,
            permissions: IOMMUAccessPermissions.ReadWrite));
    }

    private static Processor.MainMemoryArea CreateMainMemory(ulong length)
    {
        var memory = new Processor.MainMemoryArea();
        memory.SetLength((long)length);
        return memory;
    }

    private static byte[] CreatePattern(int length, byte start)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = unchecked((byte)(start + i));
        }

        return data;
    }
}
