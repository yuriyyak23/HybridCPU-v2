using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf106VectorSegmentLoadMemoryCycleTests
{
    [Fact]
    public void ReadFifoArbitratesScalarAndVectorClassesInAcceptanceOrderAtOneTotalPerTick()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            MemoryAdmissionResult explicitScalar =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 4);
            MemoryAdmissionResult vector =
                memory.CycleController.TryAcceptVectorSegmentLoad(0, 0x200, 16);
            MemoryAdmissionResult singleScalar =
                memory.CycleController.TryAcceptSingleLaneScalarLoad(0, 0x300, 8);

            Assert.Equal(MemoryAdmissionStatus.Accepted, explicitScalar.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, vector.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, singleScalar.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(explicitScalar.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(explicitScalar.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(vector.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(vector.RequestId, out MemoryCompletion? vectorCompletion));
            Assert.NotNull(vectorCompletion);
            Assert.Equal(16, vectorCompletion.Data.Length);
            Assert.False(memory.CycleController.TryTakeCompletion(singleScalar.RequestId, out _));

            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(singleScalar.RequestId, out _));
        });
    }

    [Fact]
    public void LoadSegmentConsumesImmutableNextLatchPayloadWithoutLegacyQueueAdmission()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong address = 0x480;
            byte[] servicedBytes = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
            byte[] laterBytes = Enumerable.Repeat((byte)0xEE, servicedBytes.Length).ToArray();
            Assert.True(mainMemory.TryWritePhysicalRange(address, servicedBytes));

            Processor.CPU_Core core = CreateBoundCore();
            LoadSegmentMicroOp load = CreateLoad(address);
            int legacyQueuedBefore = memory.CurrentQueuedRequests;

            Assert.False(load.Execute(ref core));
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingVectorSegmentLoads);
            Assert.Equal(legacyQueuedBefore, memory.CurrentQueuedRequests);

            memory.AdvanceCycles(1);
            Assert.False(load.Execute(ref core));
            Assert.True(mainMemory.TryWritePhysicalRange(address, laterBytes));

            memory.AdvanceCycles(1);
            Assert.True(load.Execute(ref core));
            Assert.Equal(servicedBytes, load.GetLoadedBuffer());
            Assert.Equal(0, memory.CycleController.OutstandingVectorSegmentLoads);
        });
    }

    [Fact]
    public void VectorIngressBackpressureAllocatesNoIdAndProjectsSeparateNoEffectRetry()
    {
        WithMappedMemory((_, memory) =>
        {
            var accepted = new List<MemoryRequestId>();
            for (int request = 0; request < MemoryCycleController.VectorSegmentLoadCapacity; request++)
            {
                MemoryAdmissionResult admission =
                    memory.CycleController.TryAcceptVectorSegmentLoad(
                        0,
                        0x100UL + (ulong)(request * 16),
                        16);
                Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
                accepted.Add(admission.RequestId);
            }

            Processor.CPU_Core core = CreateBoundCore();
            LoadSegmentMicroOp load = CreateLoad(0x700);
            Assert.False(load.Execute(ref core));
            Assert.True(load.HasControllerAdmissionBackpressure);
            Assert.False(load.OwnsPendingMemoryCompletion);
            Assert.Equal(
                MemoryCycleController.VectorSegmentLoadCapacity,
                memory.CycleController.OutstandingVectorSegmentLoads);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneLoadSegmentAdmissionBackpressureOutcome(
                    load,
                    legacySuccess: false);
            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.ResourceWait, outcome.Diagnostic!.Code);
            Assert.Null(outcome.Result);
            Assert.False(outcome.HasArchitecturalEffects);

            foreach (MemoryRequestId requestId in accepted)
            {
                Assert.True(memory.CycleController.TryCancel(requestId));
            }
        });
    }

    [Fact]
    public void PipelineFlushTerminallyCancelsAcceptedVectorSegmentRead()
    {
        WithMappedMemory((_, memory) =>
        {
            Processor.CPU_Core core = CreateBoundCore();
            VLIW_Instruction instruction = CreateInstruction(0x600);
            var load = new LoadSegmentMicroOp { Instruction = instruction };
            load.InitializeMetadata();

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                load,
                isVectorOp: true,
                isMemoryOp: true,
                pc: 0xA600);
            Assert.True(load.OwnsPendingMemoryCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingVectorSegmentLoads);

            core.FlushPipeline();

            Assert.False(load.OwnsPendingMemoryCompletion);
            Assert.Equal(0, memory.CycleController.OutstandingVectorSegmentLoads);
            memory.AdvanceCycles(2);
            Assert.Equal(0, memory.CycleController.OutstandingVectorSegmentLoads);
        });
    }

    [Fact]
    public void SourceAndAuthorityCloseOnlyBoundSubsystemLoadSegment()
    {
        string root = FindRepositoryRoot();
        string vector = Read(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs");
        string paper = Read(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = Read(root,
            "Documentation/Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        int loadStart = vector.IndexOf("public class LoadSegmentMicroOp", StringComparison.Ordinal);
        int loadEnd = vector.IndexOf("public class Load2DMicroOp", loadStart, StringComparison.Ordinal);
        int storeStart = vector.IndexOf("public class StoreSegmentMicroOp", StringComparison.Ordinal);
        Assert.True(loadStart >= 0 && loadEnd > loadStart && storeStart > loadEnd);
        string loadSurface = vector[loadStart..loadEnd];
        string storeSurface = vector[storeStart..];

        Assert.Contains("TryAcceptVectorSegmentLoad", loadSurface, StringComparison.Ordinal);
        Assert.Contains("TryTakeCompletion", loadSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryRequestToken", loadSurface, StringComparison.Ordinal);
        Assert.Contains("MemoryRequestToken", storeSurface, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 authorizes exactly `LoadSegmentMicroOp.Execute`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.6 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10.7 | closed inventory/blocker", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static LoadSegmentMicroOp CreateLoad(ulong address)
    {
        var load = new LoadSegmentMicroOp { Instruction = CreateInstruction(address) };
        load.InitializeMetadata();
        return load;
    }

    private static VLIW_Instruction CreateInstruction(ulong address) =>
        new()
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VLOAD,
            DestSrc1Pointer = address,
            StreamLength = 4,
            DataTypeValue = DataTypeEnum.UINT32,
            Stride = 4,
            VirtualThreadId = 0,
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
            var mainMemory = new Processor.MultiBankMemoryArea(4, 0x2000UL);
            Processor.MainMemory = mainMemory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            Assert.True(IOMMU.Map(0, 0, 0, 0x2000UL, IOMMUAccessPermissions.ReadWrite));
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
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
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
