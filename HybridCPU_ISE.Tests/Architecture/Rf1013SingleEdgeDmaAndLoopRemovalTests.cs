using HybridCPU_ISE.CloseToHSL.Memory.DMA;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1013SingleEdgeDmaAndLoopRemovalTests
{
    [Fact]
    public void BoundDmaAdvancesExactlyOncePerMemoryControllerEdge()
    {
        WithBoundDma((_, memory, dma) =>
        {
            StartTransfer(dma, source: 0x1000, destination: 0x3000, byteCount: 512);

            Assert.Equal((0U, 512U), dma.GetChannelProgress(0));
            memory.AdvanceCycles(1);
            Assert.Equal((256U, 512U), dma.GetChannelProgress(0));
            memory.AdvanceCycles(1);
            Assert.Equal((512U, 512U), dma.GetChannelProgress(0));
        });
    }

    [Fact]
    public void DirectLargeMemorySubsystemAccessCreatesNoCallerLocalDmaProgress()
    {
        WithBoundDma((mainMemory, memory, dma) =>
        {
            byte[] source = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(0x1000, source));
            StartTransfer(dma, source: 0x1000, destination: 0x3000, byteCount: 512);

            byte[] read = new byte[source.Length];
            Assert.True(memory.Read(0, 0x1000, read));
            Assert.Equal(source, read);
            Assert.Equal((0U, 512U), dma.GetChannelProgress(0));

            byte[] write = Enumerable.Range(0, 2048).Select(i => (byte)(255 - i)).ToArray();
            Assert.True(memory.Write(0, 0x5000, write));
            Assert.Equal(write, Read(mainMemory, 0x5000, write.Length));
            Assert.Equal((0U, 512U), dma.GetChannelProgress(0));
        });
    }

    [Fact]
    public void LargeStreamBurstAdaptersCreateNoCallerLocalDmaProgress()
    {
        WithBoundDma((mainMemory, _, dma) =>
        {
            byte[] source = Enumerable.Range(0, 8192).Select(i => (byte)i).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(0x1000, source));
            StartTransfer(dma, source: 0x1000, destination: 0x7000, byteCount: 512);

            byte[] read = new byte[source.Length];
            Assert.Equal(8192UL, BurstIO.BurstRead(0x1000, read, 8192, 1, 1));
            Assert.Equal(source, read);
            Assert.Equal((0U, 512U), dma.GetChannelProgress(0));

            byte[] write = Enumerable.Range(0, 8192).Select(i => (byte)(i * 3)).ToArray();
            Assert.Equal(8192UL, BurstIO.BurstWrite(0x9000, write, 8192, 1, 1));
            Assert.Equal(write, Read(mainMemory, 0x9000, write.Length));
            Assert.Equal((0U, 512U), dma.GetChannelProgress(0));
        }, memorySize: 0xD000);
    }

    [Fact]
    public void SourceGuardProvesAllFourNestedLoopsAndThresholdIngressAreGone()
    {
        string root = FindRepositoryRoot();
        string operations = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.Operations.cs");
        string subsystem = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.cs");
        string helpers = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Subsystem/MemorySubsystem.Helpers.cs");
        string burst = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Core/Execution/StreamEngine/BurstIO/StreamEngine.BurstIO.cs");
        string controller = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");

        Assert.DoesNotContain("ReadViaDMA", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteViaDMA", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstReadViaDMA", burst, StringComparison.Ordinal);
        Assert.DoesNotContain("BurstWriteViaDMA", burst, StringComparison.Ordinal);
        Assert.DoesNotContain("maxCycles = 10000", operations, StringComparison.Ordinal);
        Assert.DoesNotContain("maxCycles = 10000", burst, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaThresholdBytes && dma", subsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("DmaThresholdBytes && dma", helpers, StringComparison.Ordinal);
        Assert.Contains("_memorySubsystem.AdvanceBoundDmaAgentOneCycle();", controller, StringComparison.Ordinal);
        Assert.Single(
            Directory.EnumerateFiles(
                Path.Combine(root, "HybridCPU_ISE", "CloseToHSL"), "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains("dma?.ExecuteCycle();", StringComparison.Ordinal));
    }

    private static void StartTransfer(DMAController dma, ulong source, ulong destination, uint byteCount)
    {
        Assert.True(dma.ConfigureTransfer(new DMAController.TransferDescriptor
        {
            SourceAddress = source,
            DestAddress = destination,
            TransferSize = byteCount,
            ElementSize = 1,
            UseIOMMU = true,
            ChannelID = 0,
            Priority = 128,
        }));
        Assert.True(dma.StartTransfer(0));
    }

    private static byte[] Read(Processor.MainMemoryArea memory, ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(memory.TryReadPhysicalRange(address, bytes));
        return bytes;
    }

    private static void WithBoundDma(
        Action<Processor.MainMemoryArea, MemorySubsystem, DMAController> body,
        ulong memorySize = 0x8000)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        DMAController? originalDma = Processor.DMAController;
        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(4, memorySize);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(0, 0, 0, memorySize, IOMMUAccessPermissions.ReadWrite));
            Processor processor = default;
            var dma = new DMAController(ref processor);
            var memory = new MemorySubsystem(ref processor, dma);
            Processor.DMAController = dma;
            Processor.Memory = memory;
            body(mainMemory, memory, dma);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
            Processor.DMAController = originalDma;
        }
    }

    private static string ReadText(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
