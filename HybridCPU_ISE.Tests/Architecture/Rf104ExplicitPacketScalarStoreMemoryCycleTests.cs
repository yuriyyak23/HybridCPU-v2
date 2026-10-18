using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf104ExplicitPacketScalarStoreMemoryCycleTests
{
    [Fact]
    public void AdmissionSnapshotsExactBytesAndBackpressureAllocatesNoIdentity()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            var acceptedIds = new List<MemoryRequestId>();
            byte[] firstData = BitConverter.GetBytes(0x1122_3344_5566_7788UL);

            for (int index = 0; index < MemoryCycleController.ExplicitPacketScalarStoreCapacity; index++)
            {
                byte[] data = index == 0 ? firstData : BitConverter.GetBytes((ulong)index);
                MemoryAdmissionResult admission =
                    memory.CycleController.TryAcceptExplicitPacketScalarStore(
                        0,
                        (ulong)(0x100 + index * 8),
                        8,
                        data);
                Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
                Assert.True(admission.RequestId.IsValid);
                acceptedIds.Add(admission.RequestId);
            }

            byte[] capturedFirstData = (byte[])firstData.Clone();
            firstData[0] ^= 0xFF;
            Assert.True(memory.CycleController.OwnsOutstandingExplicitPacketScalarStore(
                acceptedIds[0], 0, 0x100, 8, capturedFirstData));
            Assert.False(memory.CycleController.OwnsOutstandingExplicitPacketScalarStore(
                acceptedIds[0], 0, 0x100, 8, firstData));

            MemoryAdmissionResult backpressured =
                memory.CycleController.TryAcceptExplicitPacketScalarStore(
                    0, 0x200, 8, new byte[8]);
            Assert.Equal(MemoryAdmissionStatus.Backpressured, backpressured.Status);
            Assert.False(backpressured.RequestId.IsValid);

            MemoryAdmissionResult rejectedSize =
                memory.CycleController.TryAcceptExplicitPacketScalarStore(
                    0, 0x200, 3, new byte[3]);
            Assert.Equal(MemoryAdmissionStatus.Rejected, rejectedSize.Status);
            Assert.False(rejectedSize.RequestId.IsValid);

            foreach (MemoryRequestId requestId in acceptedIds)
            {
                Assert.True(memory.CycleController.TryCancel(requestId));
            }
            Assert.Equal(0, memory.CycleController.OutstandingExplicitPacketScalarStores);
        });
    }

    [Fact]
    public void ReadAndStoreReadinessCanStageTogetherButStoreNeverMutatesMemory()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong readAddress = 0x280;
            const ulong storeAddress = 0x300;
            byte[] readData = BitConverter.GetBytes(0xAABB_CCDD_EEFF_0011UL);
            byte[] baseline = Enumerable.Repeat((byte)0xCC, 8).ToArray();
            byte[] storeData = BitConverter.GetBytes(0x1122_3344_5566_7788UL);
            Assert.True(mainMemory.TryWritePhysicalRange(readAddress, readData));
            Assert.True(mainMemory.TryWritePhysicalRange(storeAddress, baseline));

            MemoryAdmissionResult read =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, readAddress, 8);
            MemoryAdmissionResult store =
                memory.CycleController.TryAcceptExplicitPacketScalarStore(0, storeAddress, 8, storeData);
            Assert.Equal(MemoryAdmissionStatus.Accepted, read.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, store.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(read.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(store.RequestId, out _));
            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[8], storeAddress, 8));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(read.RequestId, out MemoryCompletion? readCompletion));
            Assert.True(memory.CycleController.TryTakeCompletion(store.RequestId, out MemoryCompletion? storeCompletion));
            Assert.Equal(readData, readCompletion!.Data.ToArray());
            Assert.True(storeCompletion!.Succeeded);
            Assert.Empty(storeCompletion.Data.ToArray());
            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[8], storeAddress, 8));
            Assert.Equal(0, memory.CycleController.OutstandingExplicitPacketScalarStores);
        });
    }

    [Fact]
    public void ExplicitPacketStorePublishesOnlyAtExistingSelectedRetireBoundary()
    {
        WithMappedMemory((mainMemory, unusedMemory) =>
        {
            _ = unusedMemory;
            const ulong address = 0x380;
            const ulong data = 0x0000_0000_A1B2_C3D4UL;
            byte[] baseline = Enumerable.Repeat((byte)0xCC, 8).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(address, baseline));

            var core = new Processor.CPU_Core(0);
            core.TestPrepareExplicitPacketStoreForWriteBack(
                laneIndex: 4,
                pc: 0xA400,
                address,
                data,
                accessSize: 4,
                vtId: 1);

            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[8], address, 8));
            core.TestRunWriteBackStage();
            Assert.Equal(
                new byte[] { 0xD4, 0xC3, 0xB2, 0xA1, 0xCC, 0xCC, 0xCC, 0xCC },
                mainMemory.ReadFromPosition(new byte[8], address, 8));
        });
    }

    [Fact]
    public void SourceEnvelopePreservesRf104AfterStoreMicroOpMigration()
    {
        string root = FindRepositoryRoot();
        string explicitMemory = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Stages/Memory/CPU_Core.PipelineExecution.Memory.cs");
        string controller = Read(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");
        string loadStore = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");
        string vector = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string retire = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");

        Assert.Contains("TryAcceptExplicitPacketScalarStore", explicitMemory, StringComparison.Ordinal);
        Assert.DoesNotContain("memSub.EnqueueWrite(", explicitMemory, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteBoundMainMemory", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("IOMMU.WriteBurst", controller, StringComparison.Ordinal);
        Assert.Contains("public class StoreMicroOp", loadStore, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarStore", loadStore, StringComparison.Ordinal);
        Assert.DoesNotContain("_requestToken = memSub.EnqueueWrite", loadStore, StringComparison.Ordinal);
        Assert.Contains("_requestToken = memSub.EnqueueWrite", vector, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredScalarStoreCommit(", retire, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorityAndLedgerCloseOnlyRf104AndNameRf105()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        Assert.Contains("RF-10.4 authorizes exactly the explicit-packet scalar-store contour", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.4 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.5 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static string Read(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static void WithMappedMemory(Action<Processor.MainMemoryArea, MemorySubsystem> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(4, 0x1000UL);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x1000UL,
                permissions: IOMMUAccessPermissions.ReadWrite));
            Processor processor = default;
            var memory = new MemorySubsystem(ref processor);
            Processor.Memory = memory;
            body(mainMemory, memory);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "HybridCPU v2.slnx")))
                return directory;
            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
