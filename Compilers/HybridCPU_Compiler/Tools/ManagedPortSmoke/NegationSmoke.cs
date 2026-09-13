using System.Reflection;
using System.Reflection.Emit;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;

internal static class NegationSmoke
{
    public static void Run()
    {
        byte[] pe = File.ReadAllBytes(typeof(NegationFixture).Assembly.Location);
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2);
        foreach (string method in new[] { nameof(NegationFixture.Word), nameof(NegationFixture.UnsignedWord), nameof(NegationFixture.Wide) })
        {
            var selector = new RestrictedCilMethodSelectorV1(typeof(NegationFixture).FullName!, method);
            var imported = importer.ImportImage(pe, selector);
            if (imported.Status != RestrictedCilImportStatusV1.Success || !imported.Program!.Instructions.Any(i =>
                    i.Opcode == HybridCpuOpcode.SUB && i.Operands[0].Kind == IrOperandKind.ArchitecturalRegister && i.Operands[0].Value == 0))
                throw new Exception(method + ": " + string.Join(';', imported.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "negation-smoke", [selector], []));
            if (graph.Status != RestrictedCilImportStatusV1.Success ||
                new ScalarControlFlowV2ObjectLinkerV1().Link(graph, graph.Graph!.RootIdentities.Single()).Status != ScalarControlFlowV2LinkStatusV1.Success)
                throw new Exception(method + " backend/link failed");
        }

        foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
            if (NegationFixture.Word(value) != unchecked(-value)) throw new Exception("I4 neg parity");
        foreach (uint value in new[] { uint.MinValue, 1u, 0x7fffffffu, 0x80000000u, uint.MaxValue })
            if (NegationFixture.UnsignedWord(value) != unchecked(-(int)value)) throw new Exception("UInt32 I4 carrier neg parity");
        foreach (long value in new[] { long.MinValue, -1L, 0L, 1L, long.MaxValue })
            if (NegationFixture.Wide(value) != unchecked(-value)) throw new Exception("I8 neg parity");

        var invalidAssembly = new PersistedAssemblyBuilder(new AssemblyName("NegationInvalidFixture"), typeof(object).Assembly);
        var invalidType = invalidAssembly.DefineDynamicModule("Invalid").DefineType("Invalid", TypeAttributes.Public);
        var wrongType = invalidType.DefineMethod("WrongType", MethodAttributes.Public | MethodAttributes.Static, typeof(object), [typeof(object)]).GetILGenerator();
        wrongType.Emit(OpCodes.Ldarg_0); wrongType.Emit(OpCodes.Neg); wrongType.Emit(OpCodes.Ret);
        var underflow = invalidType.DefineMethod("Underflow", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes).GetILGenerator();
        underflow.Emit(OpCodes.Neg); underflow.Emit(OpCodes.Ret);
        invalidType.CreateType();
        using var invalidPe = new MemoryStream(); invalidAssembly.Save(invalidPe);
        if (!importer.ImportImage(invalidPe.ToArray(), new("Invalid", "WrongType")).Diagnostics.Any(d => d.Code == "HCCIL1872") ||
            !importer.ImportImage(invalidPe.ToArray(), new("Invalid", "Underflow")).Diagnostics.Any(d => d.Code == "HCCIL1873"))
            throw new Exception("neg fail-closed diagnostics");

        Console.WriteLine("PASS integer negation wrapping boundaries, exact type negatives and backend/link");
    }
}

public static class NegationFixture
{
    public static int Word(int value) => -value;
    public static int UnsignedWord(uint value) => unchecked(-(int)value);
    public static long Wide(long value) => -value;
}
