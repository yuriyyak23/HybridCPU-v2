using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Resources;

internal static class AllocatorCancellationSmoke
{
    public static void Run()
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("AllocatorCancellation"), typeof(object).Assembly);
        var type = assembly.DefineDynamicModule("fixture").DefineType("Fixture", TypeAttributes.Public);
        var method = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Neg); il.Emit(OpCodes.Ret);
        type.CreateType(); using var stream = new MemoryStream(); assembly.Save(stream);
        var imported = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportImage(stream.ToArray(), new("Fixture", "Read"));
        var program = imported.Program ?? throw new Exception(string.Join(';', imported.Diagnostics));
        var schedule = new HybridCpuLocalListScheduler().ScheduleProgram(program);
        var bundles = new HybridCpuBundleFormer().BundleProgram(schedule);
        var resource = HybridCpuMiiResourceModelV1.Create(new HybridCpuMachineTopologyV1(64, 64, 4, 8, 2, 16), 8);
        var allocator = new HybridCpuScheduleAwareRegisterAllocatorV1();
        var options = HybridCpuRegisterAllocationOptionsV1.Qualification;
        var baseline = allocator.Allocate(schedule, bundles, resourceModel: resource, options: options);
        var none = allocator.Allocate(CancellationToken.None, schedule, bundles, resourceModel: resource, options: options);
        void Equal(IrRegisterAllocationResultV1 value)
        {
            if (baseline.Witness is null || value.Witness?.WitnessDigest != baseline.Witness.WitnessDigest ||
                !HybridCpuIrProgramCompiler.CompileAllocated(baseline).ProgramImage.SequenceEqual(
                    HybridCpuIrProgramCompiler.CompileAllocated(value).ProgramImage))
                throw new Exception("Allocator None/reuse witness and physical image drift.");
        }
        Equal(none);
        using var completion = new CancellationTokenSource();
        byte[] completedImage = HybridCpuIrProgramCompiler.CompileAllocated(baseline).ProgramImage;
        if (!completedImage.SequenceEqual(HybridCpuIrProgramCompiler.CompileAllocated(CancellationToken.None, baseline).ProgramImage) ||
            !completedImage.SequenceEqual(HybridCpuIrProgramCompiler.CompileAllocated(completion.Token, baseline).ProgramImage))
            throw new Exception("Allocated completion token changed physical bytes.");
        completion.Cancel();
        Cancel(() => HybridCpuIrProgramCompiler.CompileAllocated(completion.Token, baseline), completion.Token);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Cancel(() => allocator.Allocate(cancelled.Token, schedule, bundles, resourceModel: resource, options: options), cancelled.Token);
        try { allocator.Allocate(cancelled.Token, null!, bundles); throw new Exception("Null schedule admitted."); }
        catch (ArgumentNullException e) when (e.ParamName == "selectedSchedule") { }
        using var during = new CancellationTokenSource();
        var plans = new IrSelectedAllocationPlansV1(new CancellingPlans(during), [], [], []);
        Cancel(() => allocator.Allocate(during.Token, schedule, bundles, plans, resource, options), during.Token);
        if (!during.IsCancellationRequested) throw new Exception("Plan digest read not reached.");
        Equal(allocator.Allocate(schedule, bundles, resourceModel: resource, options: options));
        var budget = HybridCpuRegisterAllocationOptionsV1.Create(true, true, options.Budgets with { MaximumInstructions = 1 });
        var oldRejected = allocator.Allocate(schedule, bundles, resourceModel: resource, options: budget);
        var newRejected = allocator.Allocate(CancellationToken.None, schedule, bundles, resourceModel: resource, options: budget);
        if (oldRejected.Status != IrRegisterAllocationStatusV1.BudgetExhausted ||
            oldRejected.ResultDigest != newRejected.ResultDigest || oldRejected.Reason != newRejected.Reason ||
            !oldRejected.Diagnostics.SequenceEqual(newRejected.Diagnostics) || !newRejected.UsedExactFallback)
            throw new Exception("Budget diagnostic/default precedence drift.");
        using var active = new CancellationTokenSource();
        var orchestrated = HybridCpuProgramBundler.CompileWithAllocation(active.Token, program, _ => schedule,
            new HybridCpuBundleFormer(), (s, b) => allocator.Allocate(active.Token, s, b, resourceModel: resource, options: options));
        Equal(orchestrated.Allocation!);
        var rebuild = typeof(HybridCpuScheduleAwareRegisterAllocatorV1).GetMethod("RebuildAllProofs",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var topologyCancellation = new CancellationTokenSource();
        var ranks = new CancellingRanks(baseline.FinalSchedule.Program.Instructions
            .ToDictionary(instruction => instruction.Index, instruction => (long)instruction.Index), topologyCancellation);
        try
        {
            rebuild.Invoke(allocator, [baseline.FinalSchedule.Program,
                baseline.OriginalSchedule.Program.Contract.DerivedFacts.ProgramMutation,
                resource, false, ranks, topologyCancellation.Token]);
            throw new Exception("Cancelled proof rebuild returned.");
        }
        catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException cancelledRebuild &&
            cancelledRebuild.CancellationToken == topologyCancellation.Token) { }
        if (!ranks.Reached) throw new Exception("Physical topology rank read was not reached.");
        var frontier = typeof(HybridCpuScheduleAwareRegisterAllocatorV1).GetMethod(
            "BuildRegisterGroupSerializationFrontier", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var frontierCancellation = new CancellationTokenSource();
        var masks = new CancellingMasks(frontierCancellation);
        try
        {
            frontier.Invoke(null, [masks, new ushort[] { 0, 1 }, new int[] { 0, 1 }, frontierCancellation.Token]);
            throw new Exception("Cancelled frontier returned partial edges.");
        }
        catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException cancelledFrontier &&
            cancelledFrontier.CancellationToken == frontierCancellation.Token) { }
        if (masks.Reads != 1) throw new Exception("Frontier did not stop at the next ordinal checkpoint.");
        Equal(allocator.Allocate(schedule, bundles, resourceModel: resource, options: options));
        Console.WriteLine("PASS allocator None witness/physical image, pre/digest/topology/frontier cancellation escape, null/budget precedence, reuse and shared token consumer");
    }

    private static void Cancel(Action action, CancellationToken token)
    {
        try { action(); throw new Exception("Cancelled allocation returned result/witness."); }
        catch (OperationCanceledException e) when (e.CancellationToken == token) { }
    }
    private sealed class CancellingPlans(CancellationTokenSource source) : IReadOnlyList<string>
    {
        public int Count => 1;
        public string this[int index] { get { source.Cancel(); return new string('a', 64); } }
        public IEnumerator<string> GetEnumerator() { source.Cancel(); yield return new string('a', 64); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CancellingRanks(IReadOnlyDictionary<int, long> values, CancellationTokenSource source)
        : IReadOnlyDictionary<int, long>
    {
        public bool Reached { get; private set; }
        public long this[int key] { get { Reached = true; source.Cancel(); return values[key]; } }
        public int Count => values.Count;
        public IEnumerable<int> Keys => values.Keys;
        public IEnumerable<long> Values => values.Values;
        public bool ContainsKey(int key) => values.ContainsKey(key);
        public bool TryGetValue(int key, out long value) => values.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<int, long>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private sealed class CancellingMasks(CancellationTokenSource source) : IReadOnlyList<ushort>
    {
        public int Reads { get; private set; }
        public int Count => 2;
        public ushort this[int index] { get { Reads++; source.Cancel(); return 1; } }
        public IEnumerator<ushort> GetEnumerator() { yield return this[0]; yield return this[1]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
