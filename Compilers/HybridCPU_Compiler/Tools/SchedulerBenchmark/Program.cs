using System.Reflection;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;

// Tool-only measurement lane. It is not referenced by production compiler projects and
// its results are performance observations, never legality or publication evidence.
BenchmarkRunner.Run<SchedulerBenchmarks>();

[MemoryDiagnoser]
public class SchedulerBenchmarks
{
    private IrProgram _program = null!;

    [Params(64, 512, 2048)]
    public int OperationCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var assembly = new PersistedAssemblyBuilder(new AssemblyName("SchedulerBenchmarkFixture"), typeof(object).Assembly);
        TypeBuilder type = assembly.DefineDynamicModule("Main").DefineType("Fixture", TypeAttributes.Public);
        ILGenerator il = type.DefineMethod("Calculate", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), [typeof(int)]).GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        for (int index = 0; index < OperationCount; index++)
        {
            il.Emit(OpCodes.Ldc_I4, index | 1);
            il.Emit(index % 3 == 0 ? OpCodes.Xor : OpCodes.Add);
        }
        il.Emit(OpCodes.Ret);
        type.CreateType();
        using var stream = new MemoryStream();
        assembly.Save(stream);
        RestrictedCilImportResultV1 imported = new RestrictedCilImporterV1(
            mode: RestrictedCilImportModeV1.ScalarControlFlowV2).ImportImage(
                stream.ToArray(), new("Fixture", "Calculate"));
        _program = imported.Program ?? throw new InvalidOperationException(string.Join(';',
            imported.Diagnostics.Select(static diagnostic => diagnostic.Code + ":" + diagnostic.Message)));
    }

    [Benchmark]
    public IrProgramSchedule Schedule() => new HybridCpuLocalListScheduler().ScheduleProgram(_program);
}
