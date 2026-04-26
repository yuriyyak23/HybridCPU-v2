using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09FetchStageMainMemoryBindingSeamTests
{
    /// <summary>
    /// Proves the early guard in PipelineStage_Fetch uses GetBoundMainMemoryLength()
    /// (the core-owned seam) instead of the mutable global Processor.MainMemory.Length.
    ///
    /// Strategy: seed a small memory, construct the core (binds), swap the global
    /// to a much larger memory, set PC beyond the seeded total size, and verify
    /// the fetch stage rejects.
    ///
    /// MultiBankMemoryArea(bankCount, bankSize) → total Length = bankCount × bankSize.
    /// </summary>
    [Fact]
    public void FetchGuard_WhenGlobalMainMemoryIsReplacedAfterCoreConstruction_UsesBoundLength()
    {
        // 4 banks × 0x80 = total bound length 0x200
        const int bankCount = 4;
        const ulong seededBankSize = 0x80UL;
        // PC at 0x200 = exactly at bound edge → guard rejects (fetchPC >= boundLength)
        const ulong pcAtBoundEdge = (ulong)bankCount * seededBankSize;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount, seededBankSize);
        var replacementMemory = new Processor.MultiBankMemoryArea(bankCount, 0x10000UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(pcAtBoundEdge, activeVtId: 0);

            Processor.MainMemory = replacementMemory;

            core.ExecutePipelineCycle();

            Processor.CPU_Core.FetchStage fetchStage = core.GetFetchStage();
            Assert.False(fetchStage.Valid,
                "Fetch guard should reject when PC is at/beyond the bound memory length " +
                $"(bound={bankCount * seededBankSize}, PC={pcAtBoundEdge}), " +
                "even though the swapped global memory is large enough.");
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    /// <summary>
    /// Proves the early guard in PipelineStage_Fetch uses GetBoundMainMemoryLength()
    /// with a PC far beyond the bound — verifies the seam, not an off-by-one.
    /// </summary>
    [Fact]
    public void FetchGuard_WhenPcFarBeyondBoundLength_RejectsEvenWithLargeGlobalMemory()
    {
        const int bankCount = 4;
        const ulong seededBankSize = 0x80UL;
        // PC far beyond bound total length (0x200)
        const ulong pcFarBeyondBound = 0x8000UL;

        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        ProcessorMode originalMode = Processor.CurrentProcessorMode;

        var seededMemory = new Processor.MultiBankMemoryArea(bankCount, seededBankSize);
        var replacementMemory = new Processor.MultiBankMemoryArea(bankCount, 0x10000UL);

        try
        {
            Processor.MainMemory = seededMemory;
            Processor.Memory = null;
            Processor.CurrentProcessorMode = ProcessorMode.Emulation;

            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(pcFarBeyondBound, activeVtId: 0);

            Processor.MainMemory = replacementMemory;

            core.ExecutePipelineCycle();

            Processor.CPU_Core.FetchStage fetchStage = core.GetFetchStage();
            Assert.False(fetchStage.Valid,
                "Fetch guard should reject when PC is far beyond the bound memory length.");
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.CurrentProcessorMode = originalMode;
        }
    }
}
