using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf103SingleLaneScalarLoadMemoryCycleTests
{
    [Fact]
    public void ScalarReadFifo_ArbitratesBothClassesInAcceptanceOrderAtOneTotalPerTick()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong explicitAddress = 0x180;
            const ulong singleLaneAddress = 0x188;
            Assert.True(mainMemory.TryWritePhysicalRange(
                explicitAddress,
                BitConverter.GetBytes(0x1111_2222_3333_4444UL)));
            Assert.True(mainMemory.TryWritePhysicalRange(
                singleLaneAddress,
                BitConverter.GetBytes(0x5555_6666_7777_8888UL)));

            MemoryAdmissionResult explicitAdmission =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(
                    0,
                    explicitAddress,
                    8);
            MemoryAdmissionResult singleLaneAdmission =
                memory.CycleController.TryAcceptSingleLaneScalarLoad(
                    0,
                    singleLaneAddress,
                    8);
            Assert.Equal(MemoryAdmissionStatus.Accepted, explicitAdmission.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, singleLaneAdmission.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(explicitAdmission.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(singleLaneAdmission.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(
                explicitAdmission.RequestId,
                out MemoryCompletion? explicitCompletion));
            Assert.True(explicitCompletion!.Succeeded);
            Assert.False(memory.CycleController.TryTakeCompletion(singleLaneAdmission.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(
                singleLaneAdmission.RequestId,
                out MemoryCompletion? singleLaneCompletion));
            Assert.True(singleLaneCompletion!.Succeeded);
            Assert.Equal(0, memory.CycleController.OutstandingExplicitPacketScalarLoads);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarLoads);
        });
    }

    [Fact]
    public void LoadMicroOp_UsesControllerNextLatchAndPreservesDecodedResult()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong address = 0x280;
            const ulong expected = 0x1122_3344_5566_7788UL;
            Assert.True(mainMemory.TryWritePhysicalRange(address, BitConverter.GetBytes(expected)));
            Processor.CPU_Core core = CreateBoundCore();
            var load = CreateLoad(address);
            int legacyQueuedBefore = memory.CurrentQueuedRequests;

            Assert.False(load.Execute(ref core));
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.Equal(legacyQueuedBefore, memory.CurrentQueuedRequests);
            Assert.Equal(1, memory.CycleController.OutstandingSingleLaneScalarLoads);

            memory.AdvanceCycles(1);
            Assert.False(load.Execute(ref core));
            Assert.True(load.OwnsPendingMemoryCompletion);

            memory.AdvanceCycles(1);
            Assert.True(load.Execute(ref core));
            Assert.False(load.OwnsPendingMemoryCompletion);
            Assert.True(load.TryGetPrimaryWriteBackResult(out ulong actual));
            Assert.Equal(expected, actual);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarLoads);
        });
    }

    [Fact]
    public void BackpressureAllocatesNoIdentityAndProjectsASeparateNoEffectRetry()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            var acceptedIds = new List<MemoryRequestId>();
            for (int index = 0; index < MemoryCycleController.SingleLaneScalarLoadCapacity; index++)
            {
                MemoryAdmissionResult admission =
                    memory.CycleController.TryAcceptSingleLaneScalarLoad(
                        0,
                        (ulong)(index * 8),
                        8);
                Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
                acceptedIds.Add(admission.RequestId);
            }

            Processor.CPU_Core core = CreateBoundCore();
            var load = CreateLoad(0x300);
            Assert.False(load.Execute(ref core));
            Assert.True(load.HasControllerAdmissionBackpressure);
            Assert.False(load.OwnsPendingMemoryCompletion);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneScalarLoadAdmissionBackpressureOutcome(
                    load,
                    legacySuccess: false);
            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.Equal(
                MemoryCycleController.SingleLaneScalarLoadCapacity,
                memory.CycleController.OutstandingSingleLaneScalarLoads);

            Assert.True(memory.CycleController.TryCancel(acceptedIds[0]));
            Assert.False(load.Execute(ref core));
            Assert.False(load.HasControllerAdmissionBackpressure);
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.True(load.CancelPendingControllerRequest());
            foreach (MemoryRequestId requestId in acceptedIds.Skip(1))
            {
                Assert.True(memory.CycleController.TryCancel(requestId));
            }
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarLoads);
        });
    }

    [Fact]
    public void PipelineFlushTerminallyCancelsAcceptedSingleLaneRequest()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            _ = unusedMainMemory;
            Processor.CPU_Core core = CreateBoundCore();
            var load = CreateLoad(0x380);
            var instruction = new VLIW_Instruction
            {
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
                DataTypeValue = DataTypeEnum.UINT64,
                PredicateMask = 0xFF,
                DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 0),
            };

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                load,
                isMemoryOp: true,
                writesRegister: true,
                reg1Id: instruction.Reg1ID,
                reg2Id: instruction.Reg2ID,
                reg3Id: instruction.Reg3ID,
                pc: 0x9100UL);
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingSingleLaneScalarLoads);

            core.FlushPipeline();

            Assert.False(load.OwnsPendingMemoryCompletion);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarLoads);
            memory.AdvanceCycles(2);
            Assert.Equal(0, memory.CycleController.OutstandingSingleLaneScalarLoads);
        });
    }

    [Fact]
    public void ProductionCutover_PreservesLoadMicroOpAfterStoreMicroOpMigration()
    {
        string root = FindRepositoryRoot();
        string loadStore = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Memory",
            "MicroOp.LoadStore.cs"));
        string vectorMemory = File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Pipeline",
            "MicroOps",
            "Vector",
            "VectorMicroOps.Memory.cs"));
        int storeStart = loadStore.IndexOf("public class StoreMicroOp", StringComparison.Ordinal);
        Assert.True(storeStart > 0);
        string loadSurface = loadStore[..storeStart];
        string storeSurface = loadStore[storeStart..];

        Assert.Contains("TryAcceptSingleLaneScalarLoad", loadSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueRead(", loadSurface, StringComparison.Ordinal);
        Assert.Contains("TryAcceptSingleLaneScalarStore", storeSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("EnqueueWrite(", storeSurface, StringComparison.Ordinal);
        Assert.Contains("MemoryRequestToken", vectorMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorityAndCurrentLedger_PreserveRf103AfterRf104Closure()
    {
        string root = FindRepositoryRoot();
        string paper = File.ReadAllText(Path.Combine(
            root,
            "ResearchPaper",
            "section",
            "md base",
            "7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md"));
        string status = File.ReadAllText(Path.Combine(
            root,
            "Documentation",
            "Documentation", "ArchitectureAuthorityRefactor",
            "09_RF10",
            "00_CURRENT_STATUS_AND_LEDGER.md"));

        Assert.Contains(
            "RF-10.3 authorizes exactly the bound-`MemorySubsystem` single-lane scalar",
            paper,
            StringComparison.Ordinal);
        Assert.Contains("RF-10.3 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.4 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.5 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static LoadMicroOp CreateLoad(ulong address)
    {
        var load = new LoadMicroOp
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.LD,
            Address = address,
            Size = 8,
            DestRegID = 9,
            BaseRegID = 1,
            WritesRegister = true,
        };
        load.InitializeMetadata();
        return load;
    }

    private static Processor.CPU_Core CreateBoundCore()
    {
        var core = new Processor.CPU_Core(0);
        core.InitializePipeline();
        core.PrepareExecutionStart(0x9100UL, activeVtId: 0);
        return core;
    }

    private static void WithMappedMemory(Action<Processor.MainMemoryArea, MemorySubsystem> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            var mainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x2000UL,
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
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
