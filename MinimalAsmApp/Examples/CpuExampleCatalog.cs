using MinimalAsmApp.Examples.Abstractions;
using MinimalAsmApp.Examples.Arithmetic;
using MinimalAsmApp.Examples.Branching;
using MinimalAsmApp.Examples.DebugTrace;
using MinimalAsmApp.Examples.Flags;
using MinimalAsmApp.Examples.HelloCpu;
using MinimalAsmApp.Examples.Loops;
using MinimalAsmApp.Examples.Memory;
using MinimalAsmApp.Examples.Programs;
using MinimalAsmApp.Examples.Registers;
using MinimalAsmApp.Examples.StackNotSupported;
using MinimalAsmApp.Examples.Stream;
using MinimalAsmApp.Examples.Smt;
using MinimalAsmApp.Examples.Vector;

namespace MinimalAsmApp.Examples;

public static class CpuExampleCatalog
{
    public static CpuExampleRunner CreateRunner()
    {
        ICpuExample[] examples =
        [
            new HelloCpuExample(),
            new LoadImmediateExample(),
            new RegisterMoveExample(),
            new AddSubExample(),
            new MulDivExample(),
            new MemoryLoadStoreExample(),
            new MemoryCopyExample(),
            new CompareSetExample(),
            new ConditionalBranchNotTakenExample(),
            new JumpNotSupportedExample(),
            new CounterLoopNotSupportedExample(),
            new SumLoopNotSupportedExample(),
            new StackNotSupportedExample(),
            new ZeroFlagAlternativeExample(),
            new CarryFlagNotSupportedExample(),
            new SumOneToNExample(),
            new FactorialExample(),
            new FibonacciExample(),
            new ArraySumExample(),
            new MaxOfTwoNotSupportedExample(),
            new StepByStepTraceExample(),
            new RegisterDumpExample(),
            new MemoryDumpExample(),
            new VectorAddExample(),
            new VectorMulExample(),
            new VectorMinMaxExample(),
            new VectorReductionSumExample(),
            new VectorCompareMaskExample(),
            new Vector2DAddressingExample(),
            new VectorTransferEncodingExample(),
            new StreamControlEncodingExample(),
            new StreamControlNotSupportedExample(),
            new DmaStreamComputeNotSupportedExample(),
            new FourWayVliwSmtLayoutExample(),
            new VirtualThreadIdRangeExample(),
            new FspStealableSlotsExample(),
            new FspProtectedSlotsExample(),
            new FspDonorVtHintExample(),
            new FspBoundaryMetadataExample(),
            new BundleMixedPolicyExample(),
            new SmtSyncOpcodeEncodingExample(),
            new SmtObservationSurfaceExample(),
            new ExecutableSmtProgramNotSupportedExample()
        ];

        return new CpuExampleRunner(examples);
    }
}
