using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072xStoreSegmentCompletedTokenFailureTests
{
    [Fact]
    public void StoreSegmentCompletedFailedWrite_ProducesExactPageFaultWithoutPhysicalWrite()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
            byte[] physicalBaseline =
                BitConverter.GetBytes(0xAABB_CCDDU)
                    .Concat(BitConverter.GetBytes(0xEEFF_0011U))
                    .ToArray();
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(0, 0x2000UL, 0x2000UL, checked((ulong)physicalBaseline.Length), IOMMUAccessPermissions.ReadWrite);
            Processor.MainMemory.WriteToPosition(physicalBaseline, 0x2000UL);

            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(0, 0, 0, 0x1000UL, IOMMUAccessPermissions.ReadWrite);
            Processor processor = default;
            Processor.Memory = new MemorySubsystem(ref processor);

            var core = new Processor.CPU_Core(0);
            var microOp = new StoreSegmentMicroOp
            {
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VSTORE,
                    DataTypeValue = DataTypeEnum.UINT32,
                    DestSrc1Pointer = 0x2000UL,
                    StreamLength = 2,
                    Stride = 4
                }
            };
            microOp.SetStoreBuffer(
                BitConverter.GetBytes(0x1122_3344U)
                    .Concat(BitConverter.GetBytes(0x5566_7788U))
                    .ToArray());
            microOp.InitializeMetadata();

            Assert.False(microOp.Execute(ref core));
            for (int cycle = 0; cycle < 64; cycle++)
            {
                Processor.Memory!.AdvanceCycles(1);
            }

            PageFaultException fault = Assert.Throws<PageFaultException>(
                () => microOp.Execute(ref core));

            Assert.Equal(0x2000UL, fault.FaultAddress);
            Assert.True(fault.IsWrite);
            Assert.Contains("failed completed write token", fault.Message, StringComparison.Ordinal);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(0, 0x2000UL, 0x2000UL, checked((ulong)physicalBaseline.Length), IOMMUAccessPermissions.ReadWrite);
            Assert.Equal(
                physicalBaseline,
                Processor.MainMemory.ReadFromPosition(
                    new byte[physicalBaseline.Length],
                    0x2000UL,
                    checked((ulong)physicalBaseline.Length)));
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }
}
