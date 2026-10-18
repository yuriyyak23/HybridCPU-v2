using System.Reflection;
using System.Reflection.Emit;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Resources;

internal static class AllocationOrchestrationSmoke
{
    public static void Run()
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("AllocationOrchestration"), typeof(object).Assembly);
        var type = assembly.DefineDynamicModule("fixture").DefineType("Fixture", TypeAttributes.Public);
        var method = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]);
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Neg); il.Emit(OpCodes.Ret);
        type.CreateType(); using var stream = new MemoryStream(); assembly.Save(stream);
        var imported = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2)
            .ImportImage(stream.ToArray(), new("Fixture", "Read"));
        var program = imported.Program ?? throw new Exception(string.Join(';', imported.Diagnostics));
        var schedule = new HybridCpuLocalListScheduler().ScheduleProgram(program);
        var former = new HybridCpuBundleFormer();
        var resource = HybridCpuMiiResourceModelV1.Create(new HybridCpuMachineTopologyV1(64, 64, 4, 8, 2, 16), 8);
        IrRegisterAllocationResultV1 Allocate(IrProgramSchedule s, IrProgramBundlingResult b) =>
            new HybridCpuScheduleAwareRegisterAllocatorV1().Allocate(s, b, resourceModel: resource,
                options: HybridCpuRegisterAllocationOptionsV1.Qualification);
        var legacy = HybridCpuProgramBundler.CompileWithAllocation(program, _ => schedule, former, Allocate);
        var current = HybridCpuProgramBundler.CompileWithAllocation(CancellationToken.None, program, _ => schedule, former, Allocate);
        if (legacy.Allocation?.Witness is null || current.Allocation?.Witness?.WitnessDigest != legacy.Allocation.Witness.WitnessDigest ||
            !ReferenceEquals(current.Bundles, current.Allocation.FinalBundles) ||
            !HybridCpuIrProgramCompiler.CompileAllocated(legacy.Allocation).ProgramImage.SequenceEqual(
                HybridCpuIrProgramCompiler.CompileAllocated(current.Allocation).ProgramImage))
            throw new Exception("Allocated None witness/final bundles/image drift.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Cancel(() => HybridCpuProgramBundler.CompileWithAllocation(cancelled.Token, program,
            _ => throw new Exception("Pre-cancel schedule invoked"), former, Allocate), cancelled.Token);
        using var scheduled = new CancellationTokenSource();
        Cancel(() => HybridCpuProgramBundler.CompileWithAllocation(scheduled.Token, program,
            _ => { scheduled.Cancel(); return schedule; }, former,
            (_, _) => throw new Exception("Allocation invoked after cancelled schedule")), scheduled.Token);
        using var allocated = new CancellationTokenSource();
        bool reached = false;
        Cancel(() => HybridCpuProgramBundler.CompileWithAllocation(allocated.Token, program, _ => schedule, former,
            (s, b) => { reached = true; var result = Allocate(s, b); allocated.Cancel(); return result; }), allocated.Token);
        if (!reached) throw new Exception("Allocation cancellation callback not reached.");
        var absent = HybridCpuProgramBundler.CompileWithAllocation(CancellationToken.None, program, _ => schedule, former, null);
        if (absent.Allocation is not null || !ReferenceEquals(absent.Bundles.ProgramSchedule, schedule))
            throw new Exception("Absent allocator semantics changed.");
        var unwitnessed = HybridCpuProgramBundler.CompileWithAllocation(CancellationToken.None, program, _ => schedule, former,
            (_, _) => legacy.Allocation with { Witness = null });
        if (ReferenceEquals(unwitnessed.Bundles, legacy.Allocation.FinalBundles) || unwitnessed.Allocation?.Witness is not null)
            throw new Exception("Unwitnessed allocation replaced original layout.");
        var rejected = HybridCpuProgramBundler.CompileWithAllocation(CancellationToken.None, program, _ => schedule, former,
            (s, b) => new HybridCpuScheduleAwareRegisterAllocatorV1().Allocate(s, b, resourceModel: resource,
                options: HybridCpuRegisterAllocationOptionsV1.Create(true, true,
                    HybridCpuRegisterAllocationOptionsV1.Qualification.Budgets with { MaximumInstructions = 1 })));
        if (rejected.Allocation?.Status != IrRegisterAllocationStatusV1.BudgetExhausted ||
            !ReferenceEquals(rejected.Bundles, rejected.Allocation.OriginalBundles))
            throw new Exception("Rejected allocation diagnostic/layout changed.");
        var reused = HybridCpuProgramBundler.CompileWithAllocation(program, _ => schedule, former, Allocate);
        if (reused.Allocation?.Witness?.WitnessDigest != legacy.Allocation.Witness.WitnessDigest)
            throw new Exception("Allocation callback cancellation changed reuse.");
        Console.WriteLine("PASS allocation orchestration None witness/image, pre/schedule/callback cancellation, absent/unwitnessed/budget rejection and reuse");
    }

    private static void Cancel(Action action, CancellationToken token)
    {
        try { action(); throw new Exception("Partial allocated result after cancellation."); }
        catch (OperationCanceledException e) when (e.CancellationToken == token) { }
    }
}
