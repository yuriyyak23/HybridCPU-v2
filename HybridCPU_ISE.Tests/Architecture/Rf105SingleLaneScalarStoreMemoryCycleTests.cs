using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf105SingleLaneScalarStoreMemoryCycleTests
{
    [Fact]
    public void ScalarStoreFifoArbitratesBothClassesInAcceptanceOrderAtOneTotalPerTick()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong explicitAddress = 0x180;
            const ulong singleLaneAddress = 0x188;
            byte[] baseline = Enumerable.Repeat((byte)0xCC, 16).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(explicitAddress, baseline));

            MemoryAdmissionResult explicitStore =
                memory.CycleController.TryAcceptExplicitPacketScalarStore(
                    0, explicitAddress, 8, BitConverter.GetBytes(0x1111UL));
            MemoryAdmissionResult singleLaneStore =
                memory.CycleController.TryAcceptSingleLaneScalarStore(
                    0, singleLaneAddress, 8, BitConverter.GetBytes(0x2222UL));
            Assert.Equal(MemoryAdmissionStatus.Accepted, explicitStore.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, singleLaneStore.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(explicitStore.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(singleLaneStore.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(explicitStore.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(singleLaneStore.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(singleLaneStore.RequestId, out _));
            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[16], explicitAddress, 16));
        });
    }

    [Fact]
    public void StoreMicroOpCompletesReadinessWithoutMutationThenExistingRetirePublishes()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong address = 0x280;
            const ulong value = 0x1122_3344_5566_7788UL;
            byte[] baseline = Enumerable.Repeat((byte)0xA5, 8).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(address, baseline));
            Processor.CPU_Core core = CreateBoundCore();
            StoreMicroOp store = CreateStore(address, value);

            Assert.False(store.Execute(ref core));
            Assert.True(store.OwnsPendingWriteCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingSingleLaneScalarStores);
            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[8], address, 8));

            memory.AdvanceCycles(1);
            Assert.False(store.Execute(ref core));
            memory.AdvanceCycles(1);
            Assert.True(store.Execute(ref core));
            Assert.Equal(baseline, mainMemory.ReadFromPosition(new byte[8], address, 8));

            core.TestRetireLegacyScalarStoreThroughWriteBack(
                pc: 0xA500,
                address,
                data: value,
                accessSize: 8,
                vtId: 0);
            Assert.Equal(BitConverter.GetBytes(value), mainMemory.ReadFromPosition(new byte[8], address, 8));
        });
    }

    [Fact]
    public void BackpressureHasNoIdentityAndProjectsSeparateNoEffectRetry()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            var acceptedIds = new List<MemoryRequestId>();
            for (int index = 0; index < MemoryCycleController.SingleLaneScalarStoreCapacity; index++)
            {
                MemoryAdmissionResult admission =
                    memory.CycleController.TryAcceptSingleLaneScalarStore(
                        0, (ulong)(0x300 + index * 8), 8, BitConverter.GetBytes((ulong)index));
                Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
                acceptedIds.Add(admission.RequestId);
            }

            Processor.CPU_Core core = CreateBoundCore();
            StoreMicroOp store = CreateStore(0x400, 0xCAFEUL);
            Assert.False(store.Execute(ref core));
            Assert.True(store.HasControllerAdmissionBackpressure);
            Assert.False(store.OwnsPendingWriteCompletion);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarStoreAdmissionBackpressureOutcome(
                    store,
                    legacySuccess: false);
            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.False(outcome.HasArchitecturalEffects);

            Assert.True(memory.CycleController.TryCancel(acceptedIds[0]));
            Assert.False(store.Execute(ref core));
            Assert.True(store.OwnsPendingWriteCompletion);
            Assert.True(store.CancelPendingControllerRequest());
            foreach (MemoryRequestId requestId in acceptedIds.Skip(1))
                Assert.True(memory.CycleController.TryCancel(requestId));
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarStores);
        });
    }

    [Fact]
    public void PipelineFlushTerminallyCancelsAcceptedSingleLaneStore()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            Processor.CPU_Core core = CreateBoundCore();
            StoreMicroOp store = CreateStore(0x480, 0x1234UL);
            VLIW_Instruction instruction = CreateStoreInstruction();

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                store,
                isMemoryOp: true,
                writesRegister: false,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0xA510);
            Assert.True(store.OwnsPendingWriteCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingSingleLaneScalarStores);

            core.FlushPipeline();
            Assert.False(store.OwnsPendingWriteCompletion);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarStores);
            memory.AdvanceCycles(2);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarStores);
        });
    }

    [Fact]
    public void SourceAndAuthorityCloseOnlyBoundSubsystemStoreMicroOp()
    {
        string root = FindRepositoryRoot();
        string loadStore = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs");
        string vector = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string controller = Read(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");
        string paper = Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        int storeStart = loadStore.IndexOf("public class StoreMicroOp", StringComparison.Ordinal);
        Assert.True(storeStart > 0);
        string storeSurface = loadStore[storeStart..];
        Assert.Contains("TryAcceptSingleLaneScalarStore", storeSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueWrite(", storeSurface, StringComparison.Ordinal);
        Assert.Contains("EnqueueWrite(", vector, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteBoundMainMemory", controller, StringComparison.Ordinal);
        Assert.Contains("RF-10.5 authorizes exactly `StoreMicroOp.Execute`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.5 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static StoreMicroOp CreateStore(ulong address, ulong value)
    {
        var store = new StoreMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            Address = address,
            Value = value,
            Size = 8,
            SrcRegID = 2,
            BaseRegID = 1,
        };
        store.InitializeMetadata();
        return store;
    }

    private static VLIW_Instruction CreateStoreInstruction() =>
        new()
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.SD,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(1, 2, 0),
        };

    private static Processor.CPU_Core CreateBoundCore()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(0, activeVtId: 0);
        return core;
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
            Assert.True(IOMMU.Map(0, 0, 0, 0x1000UL, IOMMUAccessPermissions.ReadWrite));
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
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
