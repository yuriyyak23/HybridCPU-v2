using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class LegacyStoreRetireFollowThroughTests
    {
        [Fact]
        public void LegacyScalarStoreRetireThroughWriteBack_WritesRequestedAccessSizeOnly_AndCountsRetire()
        {
            InitializeCpuMainMemoryIdentityMap();

            const ulong pc = 0x2000;
            const ulong address = 0x1800;
            const ulong data = 0x00000000_11223344UL;

            byte[] baseline = { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
            WriteBytes(address, baseline);

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);

            Assert.Equal(baseline, ReadBytes(address, baseline.Length));

            core.TestRetireLegacyScalarStoreThroughWriteBack(
                pc,
                address,
                data,
                accessSize: 4,
                vtId: 0);

            Assert.Equal(
                new byte[] { 0x44, 0x33, 0x22, 0x11, 0xAA, 0xAA, 0xAA, 0xAA },
                ReadBytes(address, baseline.Length));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
        }

        [Fact]
        public void LegacyScalarStoreRetireThroughWriteBack_ZeroAccessSizeDefaultsToEightBytes()
        {
            InitializeCpuMainMemoryIdentityMap();

            const ulong pc = 0x2400;
            const ulong address = 0x1840;
            const ulong data = 0x88776655_44332211UL;

            WriteBytes(address, new byte[8]);

            var core = new YAKSys_Hybrid_CPU.Processor.CPU_Core(0);
            core.TestRetireLegacyScalarStoreThroughWriteBack(
                pc,
                address,
                data,
                accessSize: 0,
                vtId: 2);

            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 },
                ReadBytes(address, 8));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(1UL, control.ScalarLanesRetired);
        }

        private static byte[] ReadBytes(ulong address, int length)
        {
            return Processor.MainMemory.ReadFromPosition(new byte[length], address, (ulong)length);
        }

        private static void WriteBytes(ulong address, byte[] bytes)
        {
            Processor.MainMemory.WriteToPosition(bytes, address);
        }

        private static void InitializeCpuMainMemoryIdentityMap()
        {
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x100000000UL,
                permissions: IOMMUAccessPermissions.ReadWrite);
        }
    }
}
