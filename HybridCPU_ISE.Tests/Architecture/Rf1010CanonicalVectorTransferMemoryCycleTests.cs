using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using HybridCPU_ISE.CloseToHSL.Memory.Timing;
using HybridCPU_ISE.Core;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Memory;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf1010CanonicalVectorTransferMemoryCycleTests
{
    [Fact]
    public void CanonicalTransferSharesReadFifoAndConsumesOneControllerServiceDecision()
    {
        WithMappedMemory((unusedMainMemory, memory) =>
        {
            MemoryAdmissionResult scalar =
                memory.CycleController.TryAcceptExplicitPacketScalarLoad(0, 0x100, 4);
            MemoryAdmissionResult transfer =
                memory.CycleController.TryAcceptCanonicalVectorTransfer(
                    Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                    0,
                    0x200,
                    0x300,
                    2,
                    4,
                    4);
            MemoryAdmissionResult segment =
                memory.CycleController.TryAcceptVectorSegmentLoad(0, 0x400, 8);

            Assert.Equal(MemoryAdmissionStatus.Accepted, scalar.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, transfer.Status);
            Assert.Equal(MemoryAdmissionStatus.Accepted, segment.Status);

            memory.AdvanceCycles(1);
            Assert.False(memory.CycleController.TryTakeCompletion(scalar.RequestId, out _));
            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(scalar.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(transfer.RequestId, out _));
            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(transfer.RequestId, out _));
            Assert.False(memory.CycleController.TryTakeCompletion(segment.RequestId, out _));
            memory.AdvanceCycles(1);
            Assert.True(memory.CycleController.TryTakeCompletion(segment.RequestId, out _));
        });
    }

    [Theory]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VLOAD)]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VSTORE)]
    public void BoundTransferPublishesImmutableServicedBytesOnlyAtSelectedRetire(
        Processor.CPU_Core.InstructionsEnum opcode)
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong destSrc1 = 0x600;
            const ulong src2 = 0x700;
            byte[] serviced = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
            byte[] later = Enumerable.Repeat((byte)0xEE, serviced.Length).ToArray();
            byte[] destinationSeed = Enumerable.Repeat((byte)0xA5, serviced.Length).ToArray();
            ulong source = opcode == Processor.CPU_Core.InstructionsEnum.VLOAD ? src2 : destSrc1;
            ulong destination = opcode == Processor.CPU_Core.InstructionsEnum.VLOAD ? destSrc1 : src2;
            Assert.True(mainMemory.TryWritePhysicalRange(source, serviced));
            Assert.True(mainMemory.TryWritePhysicalRange(destination, destinationSeed));

            Processor.CPU_Core core = CreateBoundCore();
            VLIW_Instruction instruction = CreateInstruction(opcode, destSrc1, src2);
            VectorTransferMicroOp transfer = CreateTransfer(instruction);

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                transfer,
                isVectorOp: true,
                pc: 0xA710);
            Assert.False(core.TestReadExecuteStageStatus().ResultReady);
            Assert.True(transfer.OwnsPendingMemoryCompletion);
            Assert.Equal(destinationSeed, Read(mainMemory, destination, destinationSeed.Length));

            memory.AdvanceCycles(1);
            Assert.True(mainMemory.TryWritePhysicalRange(source, later));
            memory.AdvanceCycles(1);

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                transfer,
                isVectorOp: true,
                pc: 0xA710);
            Assert.True(core.TestReadExecuteStageStatus().ResultReady);
            Assert.True(transfer.HasControllerRetireEffect);
            Assert.Equal(destinationSeed, Read(mainMemory, destination, destinationSeed.Length));

            core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState();

            Assert.Equal(serviced, Read(mainMemory, destination, serviced.Length));
            Assert.Equal(later, Read(mainMemory, source, later.Length));
            Assert.Equal(0, memory.CycleController.OutstandingCanonicalVectorTransfers);
        });
    }

    [Fact]
    public void CompleteRetireBatchPrevalidationPreventsPartialDestinationPublication()
    {
        WithMappedMemory((mainMemory, memory) =>
        {
            const ulong source = 0x180;
            const ulong destination = 0x7FFC;
            byte[] sourceBytes = { 1, 2, 3, 4, 5, 6, 7, 8 };
            byte[] firstDestinationSeed = { 0xCC, 0xCC, 0xCC, 0xCC };
            Assert.True(mainMemory.TryWritePhysicalRange(source, sourceBytes));
            Assert.True(mainMemory.TryWritePhysicalRange(destination, firstDestinationSeed));

            Processor.CPU_Core core = CreateBoundCore();
            VLIW_Instruction instruction = CreateInstruction(
                Processor.CPU_Core.InstructionsEnum.VLOAD,
                destination,
                source,
                streamLength: 2);
            VectorTransferMicroOp transfer = CreateTransfer(instruction);

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                transfer,
                isVectorOp: true,
                pc: 0xA720);
            memory.AdvanceCycles(2);
            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                transfer,
                isVectorOp: true,
                pc: 0xA720);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                core.TestRunMemoryAndWriteBackStagesFromCurrentExecuteState);

            Assert.Contains("retire prevalidation", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                firstDestinationSeed,
                Read(mainMemory, destination, firstDestinationSeed.Length));
        });
    }

    [Fact]
    public void BackpressureAllocatesNoIdentityAndProjectsNoEffectRetry()
    {
        WithMappedMemory((_, memory) =>
        {
            var accepted = new List<MemoryRequestId>();
            for (int index = 0; index < MemoryCycleController.CanonicalVectorTransferCapacity; index++)
            {
                MemoryAdmissionResult admission =
                    memory.CycleController.TryAcceptCanonicalVectorTransfer(
                        Processor.CPU_Core.IsaOpcodeValues.VLOAD,
                        0,
                        0x100UL + (ulong)(index * 16),
                        0x800UL + (ulong)(index * 16),
                        2,
                        4,
                        4);
                Assert.Equal(MemoryAdmissionStatus.Accepted, admission.Status);
                accepted.Add(admission.RequestId);
            }

            Processor.CPU_Core core = CreateBoundCore();
            VectorTransferMicroOp transfer = CreateTransfer(CreateInstruction(
                Processor.CPU_Core.InstructionsEnum.VLOAD,
                0xC00,
                0xB00));
            Assert.False(transfer.Execute(ref core));
            Assert.True(transfer.HasControllerAdmissionBackpressure);
            Assert.False(transfer.OwnsPendingMemoryCompletion);

            ExecutionOutcome outcome =
                Processor.CPU_Core.ProjectSingleLaneVectorTransferAdmissionBackpressureOutcome(
                    transfer,
                    legacySuccess: false);
            Assert.Equal(ExecutionOutcomeKind.Retryable, outcome.Kind);
            Assert.False(outcome.HasArchitecturalEffects);
            Assert.Null(outcome.Result);

            foreach (MemoryRequestId requestId in accepted)
            {
                Assert.True(memory.CycleController.TryCancel(requestId));
            }
        });
    }

    [Fact]
    public void PipelineFlushTerminallyCancelsAcceptedCanonicalTransfer()
    {
        WithMappedMemory((_, memory) =>
        {
            Processor.CPU_Core core = CreateBoundCore();
            VLIW_Instruction instruction = CreateInstruction(
                Processor.CPU_Core.InstructionsEnum.VSTORE,
                0x500,
                0x900);
            VectorTransferMicroOp transfer = CreateTransfer(instruction);

            core.TestRunExecuteStageWithDecodedInstruction(
                instruction,
                transfer,
                isVectorOp: true,
                pc: 0xA730);
            Assert.True(transfer.OwnsPendingMemoryCompletion);
            Assert.Equal(1, memory.CycleController.OutstandingCanonicalVectorTransfers);

            core.FlushPipeline();

            Assert.False(transfer.OwnsPendingMemoryCompletion);
            Assert.Equal(0, memory.CycleController.OutstandingCanonicalVectorTransfers);
            memory.AdvanceCycles(2);
            Assert.Equal(0, memory.CycleController.OutstandingCanonicalVectorTransfers);
        });
    }

    [Fact]
    public void SourceAndAuthorityGuardFreezeTheExactRf1010Cutover()
    {
        string root = FindRepositoryRoot();
        string controller = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Memory/Timing/MemoryCycleController.cs");
        string vector = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Data.cs");
        string retire = ReadText(root,
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/Evidence/CPU_Core.PipelineExecution.Retire.cs");
        string paper = ReadText(root,
            "ResearchPaper/section/md base/7_Replay_Stable_Placement_Replay_Tokens_and_Execution_Boundaries.md");
        string status = ReadText(root,
            "Documentation/ArchitectureAuthorityRefactor/09_RF10/00_CURRENT_STATUS_AND_LEDGER.md");

        Assert.Contains("TryAcceptCanonicalVectorTransfer", controller, StringComparison.Ordinal);
        Assert.Contains("CanonicalVectorTransferCapacity = 8", controller, StringComparison.Ordinal);
        Assert.Contains("HasControllerRetireEffect", vector, StringComparison.Ordinal);
        Assert.Contains("CaptureVectorTransferEffect", retire, StringComparison.Ordinal);
        Assert.Contains("PrevalidateVectorTransferEffect", retire, StringComparison.Ordinal);
        Assert.Contains("RF-10.10 authorizes", paper, StringComparison.Ordinal);
        Assert.Contains("RF-10.10 | closed", status, StringComparison.Ordinal);
        Assert.Contains("RF-10 overall | closed", status, StringComparison.Ordinal);
    }

    private static VectorTransferMicroOp CreateTransfer(VLIW_Instruction instruction)
    {
        var transfer = new VectorTransferMicroOp
        {
            OpCode = instruction.OpCode,
            Instruction = instruction,
            VirtualThreadId = instruction.VirtualThreadId,
            OwnerThreadId = instruction.VirtualThreadId,
            OwnerContextId = instruction.VirtualThreadId,
        };
        transfer.InitializeMetadata();
        return transfer;
    }

    private static VLIW_Instruction CreateInstruction(
        Processor.CPU_Core.InstructionsEnum opcode,
        ulong destSrc1,
        ulong src2,
        uint streamLength = 4) =>
        new()
        {
            OpCode = (uint)opcode,
            DestSrc1Pointer = destSrc1,
            Src2Pointer = src2,
            StreamLength = streamLength,
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

    private static byte[] Read(Processor.MainMemoryArea memory, ulong address, int length)
    {
        byte[] bytes = new byte[length];
        Assert.True(memory.TryReadPhysicalRange(address, bytes));
        return bytes;
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
