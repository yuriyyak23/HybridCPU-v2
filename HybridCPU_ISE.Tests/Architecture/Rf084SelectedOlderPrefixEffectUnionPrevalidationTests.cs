using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf084SelectedOlderPrefixEffectUnionPrevalidationTests
{
    [Fact]
    public void InvalidLaterEffectRejectsWholeSelectedBatchBeforeOlderStoreVisibility()
    {
        const ulong address = 0x280;
        const ulong initial = 0x1111_2222_3333_4444UL;
        const ulong selectedOlderStore = 0xABCD_EF01_2345_6789UL;

        InitializeMemory();
        Processor.MainMemory.WriteToPosition(BitConverter.GetBytes(initial), address);
        var core = new Processor.CPU_Core(0);

        Span<RetireRecord> records = stackalloc RetireRecord[4];
        Span<Processor.CPU_Core.RetireWindowEffect> effects =
            stackalloc Processor.CPU_Core.RetireWindowEffect[4];
        PipelineEvent?[] pipelineEvents = new PipelineEvent?[2];
        var batch = new Processor.CPU_Core.RetireWindowBatch(records, effects, pipelineEvents);

        batch.CaptureRetireWindowScalarMemoryStore(address, selectedOlderStore, 8);
        batch.CaptureRetireWindowAtomicEffect(default);
        Assert.Equal(2, batch.Effects.Length);
        Assert.False(batch.Effects[1].AtomicEffect.IsValid);

        InvalidOperationException? thrown = null;
        try
        {
            core.ApplyCapturedRetireWindowBatch(ref batch, countRetireCycle: true);
        }
        catch (InvalidOperationException exception)
        {
            thrown = exception;
        }

        Assert.NotNull(thrown);
        Assert.Contains("invalid atomic effect", thrown!.Message, StringComparison.Ordinal);
        Assert.Equal(initial, ReadDoubleword(address));
        Assert.Equal(0UL, core.GetPipelineControl().RetireCycleCount);
    }

    [Fact]
    public void LiveWriteBackPrevalidatesCompleteBatchBeforeFinalizeCountersAndPublication()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "ExecutionFlow", "StageFlow",
            "CPU_Core.PipelineExecution.cs"));
        string retireSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs"));

        int capture = source.IndexOf("CaptureRetiredWriteBackLaneEffects(ref retireBatch, laneIndex, lane)", StringComparison.Ordinal);
        int prevalidate = source.IndexOf("PrevalidateRetireWindowBatchForPublication(", StringComparison.Ordinal);
        int finalize = source.IndexOf("FinalizeRetiredWriteBackLane(ref retireBatch, laneIndex, lane)", StringComparison.Ordinal);
        int apply = source.IndexOf("ApplyRetireBatchImmediateEffects(", StringComparison.Ordinal);

        Assert.True(capture >= 0 && prevalidate > capture && finalize > prevalidate && apply > finalize);
        Assert.Contains("RetireCoordinator.Prevalidate(retireBatch.RetireRecords)", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.DeferredStoreCommit", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.Csr", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.VectorConfig", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.Atomic", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.System", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.PipelineEvent", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.Vmx", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.ScalarMemoryStore", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.PredicateState", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.VectorStreamDirty", retireSource, StringComparison.Ordinal);
        Assert.Contains("case RetireWindowEffectKind.SerializingBoundary", retireSource, StringComparison.Ordinal);
    }

    private static void InitializeMemory()
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);

        Processor processor = default;
        Processor.Memory = new MemorySubsystem(ref processor);
    }

    private static ulong ReadDoubleword(ulong address) =>
        BitConverter.ToUInt64(Processor.MainMemory.ReadFromPosition(new byte[8], address, 8), 0);

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
