using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class ExplicitPacketStoreRetireFollowThroughTests
    {
        [Fact]
        public void ExplicitPacketStoreRetireThroughWriteBack_Lane4_DefersPhysicalWriteUntilRetire()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            const ulong pc = 0x2800;
            const ulong address = 0x1880;
            const ulong data = 0x00000000_A1B2C3D4UL;

            byte[] baseline = { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };
            WriteBytes(address, baseline);

            var core = new Processor.CPU_Core(0);
            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 4,
                pc,
                address,
                data,
                accessSize: 4,
                vtId: 1);

            Assert.Equal(baseline, ReadBytes(address, baseline.Length));

            core.TestRunWriteBackStage();

            Assert.Equal(
                new byte[] { 0xD4, 0xC3, 0xB2, 0xA1, 0xCC, 0xCC, 0xCC, 0xCC },
                ReadBytes(address, baseline.Length));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ExplicitPacketStoreRetireThroughWriteBack_Lane5_ZeroAccessSizeDefaultsToEightBytes()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            const ulong pc = 0x2C00;
            const ulong address = 0x18C0;
            const ulong data = 0x88776655_44332211UL;

            WriteBytes(address, new byte[8]);

            var core = new Processor.CPU_Core(0);
            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 5,
                pc,
                address,
                data,
                accessSize: 0,
                vtId: 2);

            Assert.Equal(new byte[8], ReadBytes(address, 8));

            core.TestRunWriteBackStage();

            Assert.Equal(
                new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 },
                ReadBytes(address, 8));

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
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

        private static void InitializeMemorySubsystem()
        {
            Processor proc = default;
            Processor.Memory = new MemorySubsystem(ref proc);
        }
    }
}
