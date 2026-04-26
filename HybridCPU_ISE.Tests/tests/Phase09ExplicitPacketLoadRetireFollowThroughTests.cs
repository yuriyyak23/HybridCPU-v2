using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09
{
    public sealed class ExplicitPacketLoadRetireFollowThroughTests
    {
        [Fact]
        public void ExplicitPacketLoadRetireThroughWriteBack_Lane4_WritesLoadedValueOnlyAtRetire()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            const int vtId = 1;
            const ulong pc = 0x2840;
            const ulong address = 0x1880;
            const ushort destinationRegister = 9;
            const ulong originalDestinationValue = 0xDEAD_BEEF_CAFE_BABEUL;
            const ulong loadedValue = 0x0102_0304_0506_0708UL;

            WriteBytes(address, BitConverter.GetBytes(loadedValue));

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 4,
                pc,
                address,
                destinationRegister,
                accessSize: 8,
                vtId);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            Assert.Equal(loadedValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
            Assert.Equal(pc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
        }

        [Fact]
        public void ExplicitPacketLoadRetireThroughWriteBack_Lane5_ZeroAccessSizeDefaultsToEightBytes()
        {
            InitializeCpuMainMemoryIdentityMap();
            InitializeMemorySubsystem();

            const int vtId = 2;
            const ulong pc = 0x2C40;
            const ulong address = 0x18C0;
            const ushort destinationRegister = 11;
            const ulong originalDestinationValue = 0x1111_2222_3333_4444UL;
            const ulong loadedValue = 0x8877_6655_4433_2211UL;

            WriteBytes(address, BitConverter.GetBytes(loadedValue));

            var core = new Processor.CPU_Core(0);
            core.PrepareExecutionStart(pc, activeVtId: vtId);
            core.WriteCommittedPc(vtId, pc);
            core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

            core.TestPrepareExplicitPacketLoadForWriteBack(
                laneIndex: 5,
                pc,
                address,
                destinationRegister,
                accessSize: 0,
                vtId);

            Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));

            core.TestRunWriteBackStage();

            Assert.Equal(loadedValue, core.ReadArch(vtId, destinationRegister));
            Assert.Equal(pc, core.ReadCommittedPc(vtId));
            Assert.Equal(pc, core.ReadActiveLivePc());

            var control = core.GetPipelineControl();
            Assert.Equal(1UL, control.InstructionsRetired);
            Assert.Equal(0UL, control.ScalarLanesRetired);
            Assert.Equal(1UL, control.NonScalarLanesRetired);
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
