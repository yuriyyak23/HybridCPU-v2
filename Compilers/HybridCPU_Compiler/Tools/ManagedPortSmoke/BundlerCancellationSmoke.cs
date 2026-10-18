using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;

internal static class BundlerCancellationSmoke
{
    public static void Run()
    {
        var words = Enumerable.Repeat(new HybridCpuInstructionWord {
            OpCode = (uint)HybridCpuOpcode.ADDI, DataTypeValue = HybridCpuDataType.INT32,
            PredicateMask = byte.MaxValue,
            Word1 = HybridCpuInstructionWord.PackArchRegs(10, 10, HybridCpuInstructionWord.NoArchReg),
            Src2Pointer = 1, VirtualThreadId = 0
        }, 128).ToArray();
        foreach (int size in new[] { 1, 16, 128 })
        {
            var program = new HybridCpuIrBuilder().BuildProgram(0, words[..size]);
            var schedule = new HybridCpuLocalListScheduler().ScheduleProgram(program);
            var former = new HybridCpuBundleFormer();
            string Projection(IrProgramBundlingResult value)
            {
                var (_, annotations, image) = HybridCpuPostBundleCompiler.Compile(value, true, true);
                return Convert.ToHexString(SHA256.HashData(image)) + JsonSerializer.Serialize(annotations);
            }
            string baseline = Projection(former.BundleProgram(schedule));
            if (baseline != Projection(former.BundleProgram(CancellationToken.None, schedule)))
                throw new Exception("Bundler None artifact drift.");
            var (sameSchedule, activeBundles) = HybridCpuProgramBundler.Compile(CancellationToken.None, program, _ => schedule, former);
            if (!ReferenceEquals(schedule, sameSchedule) || baseline != Projection(activeBundles))
                throw new Exception("Shared bundler None drift.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Cancel(() => former.BundleProgram(cancelled.Token, schedule), cancelled.Token);
            Cancel(() => former.BundleBlock(cancelled.Token, schedule.BlockSchedules[0]), cancelled.Token);
            Cancel(() => HybridCpuIrProgramCompiler.Compile(cancelled.Token, program, _ => schedule, former), cancelled.Token);
            using var compiling = new CancellationTokenSource();
            Cancel(() => HybridCpuIrProgramCompiler.Compile(compiling.Token, program, _ => schedule, former,
                (phase, _) => { if (phase == "BundleMaterializationStarting") compiling.Cancel(); }), compiling.Token);
            using var during = new CancellationTokenSource();
            var original = schedule.BlockSchedules[0];
            var rows = new CancellingRows<IrScheduleCycleGroup>(original.CycleGroups, during);
            var wrapped = new IrBasicBlockSchedule(original.Block, original.Dag, rows, original.ScheduledInstructions);
            // Reads during materialization cancel deterministically, independent of timer speed.
            Cancel(() => former.BundleBlock(during.Token, wrapped), during.Token);
            if (!during.IsCancellationRequested) throw new Exception("Materialization read not reached.");
            using var inProgram = new CancellationTokenSource();
            var programRows = new CancellingRows<IrScheduleCycleGroup>(original.CycleGroups, inProgram) { Armed = false };
            var programBlock = new IrBasicBlockSchedule(original.Block, original.Dag, programRows, original.ScheduledInstructions);
            var wrappedProgram = new IrProgramSchedule(program, schedule.DependencyGraph, [programBlock]);
            programRows.Armed = true;
            Cancel(() => former.BundleProgram(inProgram.Token, wrappedProgram), inProgram.Token);
            if (!inProgram.IsCancellationRequested) throw new Exception("Program materialization read not reached.");
            using var observer = new CancellationTokenSource();
            Cancel(() => HybridCpuProgramBundler.Compile(observer.Token, program, _ => schedule, former,
                bundlingStarting: observer.Cancel), observer.Token);
            if (baseline != Projection(former.BundleProgram(schedule))) throw new Exception("Bundler reuse drift.");
        }
        Console.WriteLine("PASS bundler global/local None bytes+annotations, pre/mid cancellation, shared callback cancellation and reuse");
    }

    private static void Cancel(Action action, CancellationToken token)
    {
        try { action(); throw new Exception("Partial bundling success after cancellation."); }
        catch (OperationCanceledException e) when (e.CancellationToken == token) { }
    }

    private sealed class CancellingRows<T>(IReadOnlyList<T> values, CancellationTokenSource source) : IReadOnlyList<T>
    {
        public bool Armed { get; set; } = true;
        public int Count => values.Count;
        public T this[int index] { get { if (Armed) source.Cancel(); return values[index]; } }
        public IEnumerator<T> GetEnumerator() { foreach (T value in values) { if (Armed) source.Cancel(); yield return value; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
