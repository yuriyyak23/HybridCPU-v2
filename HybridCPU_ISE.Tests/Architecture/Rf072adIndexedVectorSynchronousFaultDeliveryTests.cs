using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToHSL.Memory.MMU;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-07.2ad characterizes the real production single-lane delivery of the
/// synchronous indexed gather/scatter fallback boundary. The fallback is not a
/// PageFault surface: it stays fail-closed InvalidInternalOp until a distinct
/// architectural memory-fault contract authorizes otherwise.
/// </summary>
public sealed class Rf072adIndexedVectorSynchronousFaultDeliveryTests
{
    [Fact]
    public void GatherOutOfRangeSynchronousFallback_FailClosesProductionExecuteWithoutPublication()
    {
        WithBoundMainMemory((core, memory) =>
        {
            const ulong descriptorAddress = 0x100UL;
            const ulong indexBase = 0x200UL;
            const ulong destinationBase = 0x400UL;
            const ulong sourceBase = 0x0FFF_FFFCUL;
            WriteWords(memory, destinationBase, 7U, 8U);
            WriteWords(memory, indexBase, 0U, 1U);
            WriteDescriptor(memory, descriptorAddress, sourceBase, indexBase);

            VLIW_Instruction instruction = CreateInstruction(InstructionsEnum.VGATHER, destinationBase, descriptorAddress);
            var gather = new GatherMicroOp { OpCode = instruction.OpCode, Instruction = instruction };
            gather.InitializeMetadata();
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                core.TestRunExecuteStageWithDecodedInstruction(
                    instruction, gather, isVectorOp: true, isMemoryOp: true, pc: 0x8AD0UL));

            Assert.False(ExecutionFaultContract.TryGetCategory(exception, out _));
            ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(exception);
            Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.UnknownException, outcome.Diagnostic!.Code);
            Assert.Contains("Vector opcode 0xD5", exception.Message, StringComparison.Ordinal);
            Assert.Contains("MicroOp failure", exception.Message, StringComparison.Ordinal);
            Assert.False(core.GetExecuteStage().Valid);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
            Assert.Equal(new[] { 7U, 8U }, ReadWords(memory, destinationBase, 2));
        });
    }

    [Fact]
    public void ScatterOutOfRangeSynchronousFallback_FailClosesProductionExecuteWithoutPublication()
    {
        WithBoundMainMemory((core, memory) =>
        {
            const ulong descriptorAddress = 0x100UL;
            const ulong indexBase = 0x200UL;
            const ulong sourceBase = 0x400UL;
            const ulong targetBase = 0x0FFF_FFFCUL;
            WriteWords(memory, sourceBase, 11U, 22U);
            WriteWords(memory, indexBase, 0U, 1U);
            WriteDescriptor(memory, descriptorAddress, targetBase, indexBase);
            memory.WriteToPosition(BitConverter.GetBytes(0xAAAA_AAAAU), targetBase);

            VLIW_Instruction instruction = CreateInstruction(InstructionsEnum.VSCATTER, sourceBase, descriptorAddress);
            var scatter = new StoreScatterMicroOp { OpCode = instruction.OpCode, Instruction = instruction };
            scatter.InitializeMetadata();
            ulong retiredBefore = core.GetPipelineControl().InstructionsRetired;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                core.TestRunExecuteStageWithDecodedInstruction(
                    instruction, scatter, isVectorOp: true, isMemoryOp: true, pc: 0x8AD8UL));

            Assert.False(ExecutionFaultContract.TryGetCategory(exception, out _));
            ExecutionOutcome outcome = Rf07LegacyOutcomeProjection.ProjectException(exception);
            Assert.Equal(ExecutionOutcomeKind.FatalInvariantViolation, outcome.Kind);
            Assert.Equal(ExecutionDiagnosticCode.UnknownException, outcome.Diagnostic!.Code);
            Assert.Contains("Vector opcode 0xD6", exception.Message, StringComparison.Ordinal);
            Assert.Contains("MicroOp failure", exception.Message, StringComparison.Ordinal);
            Assert.False(core.GetExecuteStage().Valid);
            Assert.False(core.TestGetExecuteForwardingPath().Valid);
            Assert.Equal(retiredBefore, core.GetPipelineControl().InstructionsRetired);
            Assert.Equal(0UL, core.TestGetReferenceRawFallbackCount());
            Assert.Equal(0xAAAA_AAAAU, ReadWords(memory, targetBase, 1)[0]);
        });
    }

    private static void WithBoundMainMemory(Action<Processor.CPU_Core, Processor.MainMemoryArea> body)
    {
        Processor.MainMemoryArea originalMainMemory = Processor.MainMemory;
        MemorySubsystem? originalMemorySubsystem = Processor.Memory;
        try
        {
            var memory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
            Processor.MainMemory = memory;
            IOMMU.Initialize();
            IOMMU.RegisterDevice(0);
            IOMMU.Map(
                deviceID: 0,
                ioVirtualAddress: 0,
                physicalAddress: 0,
                size: 0x10000000UL,
                permissions: IOMMUAccessPermissions.ReadWrite);
            Processor processor = default;
            Processor.Memory = new MemorySubsystem(ref processor);
            var core = new Processor.CPU_Core(0);
            core.InitializePipeline();
            core.PrepareExecutionStart(0x8AD0UL, activeVtId: 0);
            body(core, memory);
        }
        finally
        {
            Processor.MainMemory = originalMainMemory;
            Processor.Memory = originalMemorySubsystem;
        }
    }

    private static VLIW_Instruction CreateInstruction(
        InstructionsEnum opcode,
        ulong primaryPointer,
        ulong descriptorAddress)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVectorIndexed(
            (uint)opcode,
            DataTypeEnum.UINT32,
            primaryPointer,
            descriptorAddress,
            streamLength: 2,
            predicateMask: 0);
        instruction.Stride = 4;
        return instruction;
    }

    private static void WriteDescriptor(
        Processor.MainMemoryArea memory,
        ulong descriptorAddress,
        ulong baseAddress,
        ulong indexBase)
    {
        byte[] descriptor = new byte[32];
        BitConverter.GetBytes(baseAddress).CopyTo(descriptor, 0);
        BitConverter.GetBytes(indexBase).CopyTo(descriptor, 8);
        BitConverter.GetBytes((ushort)4).CopyTo(descriptor, 16);
        descriptor[18] = 0;
        descriptor[19] = 0;
        memory.WriteToPosition(descriptor, descriptorAddress);
    }

    private static void WriteWords(Processor.MainMemoryArea memory, ulong address, params uint[] values)
    {
        byte[] bytes = values.SelectMany(BitConverter.GetBytes).ToArray();
        memory.WriteToPosition(bytes, address);
    }

    private static uint[] ReadWords(Processor.MainMemoryArea memory, ulong address, int count)
    {
        byte[] bytes = new byte[count * sizeof(uint)];
        Assert.True(memory.TryReadPhysicalRange(address, bytes));
        return Enumerable.Range(0, count).Select(index => BitConverter.ToUInt32(bytes, index * sizeof(uint))).ToArray();
    }
}
