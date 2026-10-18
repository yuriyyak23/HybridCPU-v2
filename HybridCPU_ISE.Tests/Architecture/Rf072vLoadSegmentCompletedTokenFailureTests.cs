using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.Core;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf072vLoadSegmentCompletedTokenFailureTests
{
    [Fact]
    public void LoadSegmentCompletedFailedRead_ProducesExactPageFaultWithoutSuccess()
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(0, 0, 0, 0x1000UL, IOMMUAccessPermissions.ReadWrite);
            Processor processor = default;
            Processor.Memory = new MemorySubsystem(ref processor);

            var core = new Processor.CPU_Core(0);
            var microOp = new LoadSegmentMicroOp
            {
                Instruction = new VLIW_Instruction
                {
                    OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VLOAD,
                    DataTypeValue = DataTypeEnum.UINT32,
                    DestSrc1Pointer = 0x2000UL,
                    StreamLength = 2,
                    Stride = 4
                }
            };
            microOp.InitializeMetadata();

            Assert.False(microOp.Execute(ref core));
            Processor.Memory!.AdvanceCycles(2);

            PageFaultException fault = Assert.Throws<PageFaultException>(
                () => microOp.Execute(ref core));

            Assert.Equal(0x2000UL, fault.FaultAddress);
            Assert.False(fault.IsWrite);
            Assert.Contains("failed completed controller read", fault.Message, StringComparison.Ordinal);
            Assert.NotNull(microOp.GetLoadedBuffer());
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }
}
