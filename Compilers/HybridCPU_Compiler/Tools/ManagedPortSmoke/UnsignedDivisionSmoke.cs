using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target.Managed;
using System.Reflection;
using System.Reflection.Emit;
using HybridCPU.ManagedRuntime;

internal static class UnsignedDivisionSmoke
{
    public static void Run()
    {
        byte[] pe = File.ReadAllBytes(typeof(UnsignedDivisionFixture).Assembly.Location);
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2);
        foreach (var (name, opcode) in new[] { ("Word", HybridCpuOpcode.DIVUW), ("Wide", HybridCpuOpcode.DIVU),
            ("HighWord", HybridCpuOpcode.DIVUW) })
        {
            var selector = new RestrictedCilMethodSelectorV1(typeof(UnsignedDivisionFixture).FullName!, name);
            var result = importer.ImportImage(pe, selector);
            if (result.Status != RestrictedCilImportStatusV1.Success || !result.Program!.Instructions.Any(i => i.Opcode == opcode))
                throw new Exception(name + ": " + string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "division-smoke", [selector], []));
            if (graph.Status != RestrictedCilImportStatusV1.Success) throw new Exception("division graph: " + string.Join(";", graph.Diagnostics.Select(d => d.Message)));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(graph, graph.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success)
                throw new Exception(name + " backend: " + string.Join(";", linked.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var division = result.Program.Instructions.Single(i => i.Opcode == opcode);
            // Evaluate the emitted arithmetic fragment, including actual materialized constant
            // bits. This is an IR semantic check, not a claim of native guest execution.
            foreach (ulong input in new ulong[] { 0, 1, 17, 18, 19, 0x7fffffff, 0x80000000, uint.MaxValue, 0xa5a5a5a5ffffffff, ulong.MaxValue })
            {
                ulong Eval(IrOperand operand)
                {
                    if (operand.Kind == IrOperandKind.Constant) return operand.Value;
                    if (operand.Kind == IrOperandKind.ArchitecturalRegister) return operand.Value == 0 ? 0 : input;
                    if (operand.Name.EndsWith(":arg:0", StringComparison.Ordinal)) return input;
                    var producer = result.Program.Instructions.Single(i => i.Annotation.Defs.Any(d => d.Kind == operand.Kind && d.Name == operand.Name));
                    ulong a = Eval(producer.Operands[0]), b = Eval(producer.Operands[1]);
                    return producer.Opcode switch
                    {
                        HybridCpuOpcode.ADDI or HybridCpuOpcode.ADD => unchecked(a + b),
                        HybridCpuOpcode.SLLI => a << (int)b,
                        HybridCpuOpcode.ORI => a | b,
                        _ => throw new Exception("Unexpected materialization opcode " + producer.Opcode)
                    };
                }
                ulong left = Eval(division.Operands[0]), right = Eval(division.Operands[1]);
                ulong actual = opcode == HybridCpuOpcode.DIVUW ? (uint)left / (uint)right : left / right;
                ulong expected = name == "Wide" ? UnsignedDivisionFixture.Wide(input) : name == "HighWord"
                    ? UnsignedDivisionFixture.HighWord((uint)input) : UnsignedDivisionFixture.Word((uint)input);
                if (actual != expected) throw new Exception(name + " emitted arithmetic differs from CoreCLR");
            }
        }
        var dynamicResult = importer.ImportImage(pe, new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.Dynamic)));
        if (dynamicResult.Status != RestrictedCilImportStatusV1.Success ||
            !dynamicResult.Program!.Instructions.Any(i => i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_divide_u4_checked") ||
            dynamicResult.Program.Instructions.Any(i => i.Opcode is HybridCpuOpcode.DIVU or HybridCpuOpcode.DIVUW))
            throw new Exception("Dynamic divisor must use only the exact managed checked helper");
        var zeroAssembly = new PersistedAssemblyBuilder(new AssemblyName("DivisionZeroFixture"), typeof(object).Assembly);
        var zeroType = zeroAssembly.DefineDynamicModule("Zero").DefineType("Zero", TypeAttributes.Public);
        var zero = zeroType.DefineMethod("Divide", MethodAttributes.Public | MethodAttributes.Static, typeof(uint), [typeof(uint)]).GetILGenerator();
        zero.Emit(OpCodes.Ldarg_0); zero.Emit(OpCodes.Ldc_I4_0); zero.Emit(OpCodes.Div_Un); zero.Emit(OpCodes.Ret);
        zeroType.CreateType();
        using var zeroPe = new MemoryStream(); zeroAssembly.Save(zeroPe);
        var zeroResult = importer.ImportImage(zeroPe.ToArray(), new("Zero", "Divide"));
        if (zeroResult.Status != RestrictedCilImportStatusV1.Success ||
            !zeroResult.Program!.Instructions.Any(i => i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_divide_u4_checked") ||
            zeroResult.Program.Instructions.Any(i => i.Opcode is HybridCpuOpcode.DIVU or HybridCpuOpcode.DIVUW))
            throw new Exception("A literal zero must lower to the managed checked helper, never native DIVU/DIVUW");
        // Reference values exercise unsigned boundaries; native ISA operand width is checked
        // above by the actual emitted opcode, not by substituting signed division.
        foreach (uint value in new uint[] { 0, 1, 17, 18, 19, 0x7fffffff, 0x80000000, uint.MaxValue })
        {
            if (UnsignedDivisionFixture.Word(value) != (uint)((ulong)value / 18) ||
                UnsignedDivisionFixture.HighWord(value) != (uint)((ulong)value / 0x80000001)) throw new Exception("CoreCLR I4 parity");
        }
        foreach (ulong value in new ulong[] { 0, 1, 18, 0x7fffffffffffffff, 0x8000000000000000, ulong.MaxValue })
            if (UnsignedDivisionFixture.Wide(value) != value / 0x100000001UL) throw new Exception("CoreCLR I8 parity");
        try { UnsignedDivisionFixture.Dynamic(1, 0); throw new Exception("CoreCLR zero division must throw"); }
        catch (DivideByZeroException) { }
        if (ManagedCheckedArithmeticRuntimeV1.DivideUInt32(uint.MaxValue, 3) != uint.MaxValue / 3)
            throw new Exception("Managed checked division runtime result mismatch");
        try { ManagedCheckedArithmeticRuntimeV1.DivideUInt32(1, 0); throw new Exception("Runtime helper must throw DivideByZeroException"); }
        catch (DivideByZeroException) { }
        HybridCpuRuntimeHelperV1? divideAbi = HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(
            HybridCpuManagedDivideUInt32EmitterV1.Symbol);
        var divideObject = HybridCpuManagedDivideUInt32EmitterV1.EmitObject();
        if (divideAbi is not { GcTransition: HybridCpuRuntimeHelperGcTransitionV1.MaySafepoint,
                MayThrow: true, Support: HybridCpuManagedAbiSupportV1.Supported } ||
            divideObject.Status != HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success ||
            !divideObject.Symbols.Any(symbol => symbol.Name == HybridCpuManagedDivideUInt32EmitterV1.Symbol &&
                symbol.IsDefinition))
            throw new Exception("Checked UInt32 division must bind an exact conditionally-allocating CPU thunk");
        TestSigned(importer, pe);
        TestRemainder(importer, pe);
        Console.WriteLine("PASS signed/unsigned division width, exception proof gates, CoreCLR boundaries and backend/link");
    }

    private static void TestSigned(RestrictedCilImporterV1 importer, byte[] pe)
    {
        foreach (var (name, opcode) in new[]
        {
            (nameof(UnsignedDivisionFixture.SignedWord), HybridCpuOpcode.DIVW),
            (nameof(UnsignedDivisionFixture.SignedNegativeDivisor), HybridCpuOpcode.DIVW),
            (nameof(UnsignedDivisionFixture.SignedWide), HybridCpuOpcode.DIV)
        })
        {
            var selector = new RestrictedCilMethodSelectorV1(typeof(UnsignedDivisionFixture).FullName!, name);
            var result = importer.ImportImage(pe, selector);
            if (result.Status != RestrictedCilImportStatusV1.Success || !result.Program!.Instructions.Any(i => i.Opcode == opcode))
                throw new Exception(name + ": " + string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "signed-division-smoke", [selector], []));
            if (graph.Status != RestrictedCilImportStatusV1.Success) throw new Exception(name + " graph: " + string.Join(";", graph.Diagnostics.Select(d => d.Message)));
            var linked = new ScalarControlFlowV2ObjectLinkerV1().Link(graph, graph.Graph!.RootIdentities.Single());
            if (linked.Status != ScalarControlFlowV2LinkStatusV1.Success)
                throw new Exception(name + " backend: " + string.Join(";", linked.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        }

        var dynamicSigned = importer.ImportImage(pe,
            new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicSigned)));
        if (dynamicSigned.Status != RestrictedCilImportStatusV1.Success ||
            !dynamicSigned.Program!.Instructions.Any(i => i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_divide_i4_checked") ||
            dynamicSigned.Program.Instructions.Any(i => i.Opcode is HybridCpuOpcode.DIV or HybridCpuOpcode.DIVW))
            throw new Exception("Dynamic signed divisor must use the managed zero/overflow helper");

        var dynamicSignedWide = importer.ImportImage(pe,
            new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicSignedWide)));
        if (dynamicSignedWide.Status != RestrictedCilImportStatusV1.Success ||
            !dynamicSignedWide.Program!.Instructions.Any(i => i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_divide_i8_checked") ||
            dynamicSignedWide.Program.Instructions.Any(i => i.Opcode is HybridCpuOpcode.DIV or HybridCpuOpcode.DIVW))
            throw new Exception("Dynamic signed I8 divisor must use the managed zero/overflow helper");

        var unsafeAssembly = new PersistedAssemblyBuilder(new AssemblyName("SignedDivisionUnsafeFixture"), typeof(object).Assembly);
        var unsafeType = unsafeAssembly.DefineDynamicModule("Unsafe").DefineType("Unsafe", TypeAttributes.Public);
        var minusOne = unsafeType.DefineMethod("MinusOne", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]).GetILGenerator();
        minusOne.Emit(OpCodes.Ldarg_0); minusOne.Emit(OpCodes.Ldc_I4_M1); minusOne.Emit(OpCodes.Div); minusOne.Emit(OpCodes.Ret);
        var zero = unsafeType.DefineMethod("Zero", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int)]).GetILGenerator();
        zero.Emit(OpCodes.Ldarg_0); zero.Emit(OpCodes.Ldc_I4_0); zero.Emit(OpCodes.Div); zero.Emit(OpCodes.Ret);
        unsafeType.CreateType();
        using var unsafePe = new MemoryStream(); unsafeAssembly.Save(unsafePe);
        foreach (string method in new[] { "MinusOne", "Zero" })
            if (importer.ImportImage(unsafePe.ToArray(), new("Unsafe", method)).Program?.Instructions.Any(i =>
                    i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_divide_i4_checked") != true)
                throw new Exception(method + " must retain managed signed-division exception semantics");

        foreach (int value in new[] { int.MinValue, -1025, -17, -1, 0, 1, 17, 1025, int.MaxValue })
        {
            if (UnsignedDivisionFixture.SignedWord(value) != value / 16 ||
                UnsignedDivisionFixture.SignedNegativeDivisor(value) != value / -2)
                throw new Exception("CoreCLR signed I4 parity");
        }
        foreach (long value in new[] { long.MinValue, -1025L, -17L, -1L, 0L, 1L, 17L, 1025L, long.MaxValue })
            if (UnsignedDivisionFixture.SignedWide(value) != value / 0x100000001L) throw new Exception("CoreCLR signed I8 parity");
        try { UnsignedDivisionFixture.DynamicSigned(1, 0); throw new Exception("CoreCLR signed zero division must throw"); }
        catch (DivideByZeroException) { }
        try { UnsignedDivisionFixture.DynamicSigned(int.MinValue, -1); throw new Exception("CoreCLR signed overflow must throw"); }
        catch (OverflowException) { }
        if (ManagedCheckedArithmeticRuntimeV1.DivideInt32(int.MaxValue, 3) != int.MaxValue / 3)
            throw new Exception("Signed checked helper result mismatch");
        try { ManagedCheckedArithmeticRuntimeV1.DivideInt32(1, 0); throw new Exception("signed helper zero"); }
        catch (DivideByZeroException) { }
        try { ManagedCheckedArithmeticRuntimeV1.DivideInt32(int.MinValue, -1); throw new Exception("signed helper overflow"); }
        catch (OverflowException) { }
        var divideInt32Object = HybridCpuManagedDivideInt32EmitterV1.EmitObject();
        if (HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(HybridCpuManagedDivideInt32EmitterV1.Symbol) is not
                { GcTransition: HybridCpuRuntimeHelperGcTransitionV1.MaySafepoint, MayThrow: true,
                  Support: HybridCpuManagedAbiSupportV1.Supported } ||
            divideInt32Object.Status != HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success ||
            !divideInt32Object.Symbols.Any(symbol => symbol.Name == HybridCpuManagedDivideInt32EmitterV1.Symbol && symbol.IsDefinition))
            throw new Exception("checked signed I4 division exact ABI/HCO");
        if (ManagedCheckedArithmeticRuntimeV1.DivideInt64(long.MaxValue, 3) != long.MaxValue / 3)
            throw new Exception("Signed checked I8 helper result mismatch");
        try { ManagedCheckedArithmeticRuntimeV1.DivideInt64(1, 0); throw new Exception("signed I8 helper zero"); }
        catch (DivideByZeroException) { }
        try { ManagedCheckedArithmeticRuntimeV1.DivideInt64(long.MinValue, -1); throw new Exception("signed I8 helper overflow"); }
        catch (OverflowException) { }
        var divideInt64Object = HybridCpuManagedDivideInt64EmitterV1.EmitObject();
        if (HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(HybridCpuManagedDivideInt64EmitterV1.Symbol) is not
                { GcTransition: HybridCpuRuntimeHelperGcTransitionV1.MaySafepoint, MayThrow: true,
                  Support: HybridCpuManagedAbiSupportV1.Supported } ||
            divideInt64Object.Status != HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success ||
            !divideInt64Object.Symbols.Any(symbol => symbol.Name == HybridCpuManagedDivideInt64EmitterV1.Symbol && symbol.IsDefinition))
            throw new Exception("checked signed I8 division exact ABI/HCO");
    }

    private static void TestRemainder(RestrictedCilImporterV1 importer, byte[] pe)
    {
        foreach (var (name, opcode) in new[]
        {
            (nameof(UnsignedDivisionFixture.SignedRemainderWord), HybridCpuOpcode.REMW),
            (nameof(UnsignedDivisionFixture.UnsignedRemainderWord), HybridCpuOpcode.REMUW),
            (nameof(UnsignedDivisionFixture.SignedRemainderWide), HybridCpuOpcode.REM),
            (nameof(UnsignedDivisionFixture.SignedRemainderWideTen), HybridCpuOpcode.REM),
            (nameof(UnsignedDivisionFixture.UnsignedRemainderWide), HybridCpuOpcode.REMU)
        })
        {
            var selector = new RestrictedCilMethodSelectorV1(typeof(UnsignedDivisionFixture).FullName!, name);
            var result = importer.ImportImage(pe, selector);
            if (result.Status != RestrictedCilImportStatusV1.Success || !result.Program!.Instructions.Any(i => i.Opcode == opcode))
                throw new Exception(name + ": " + string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
                "remainder-smoke", [selector], []));
            var linked = graph.Status == RestrictedCilImportStatusV1.Success
                ? new ScalarControlFlowV2ObjectLinkerV1().Link(graph, graph.Graph!.RootIdentities.Single()) : null;
            if (linked?.Status != ScalarControlFlowV2LinkStatusV1.Success)
                throw new Exception(name + " remainder backend/link failed");
        }
        var dynamicRemainder = importer.ImportImage(pe,
            new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicRemainder)));
        if (dynamicRemainder.Status != RestrictedCilImportStatusV1.Success ||
            !dynamicRemainder.Program!.Instructions.Any(i => i.Annotation.BranchTargetSymbolName == "__hybridcpu_managed_remainder_i4_checked") ||
            dynamicRemainder.Program.Instructions.Any(i => i.Opcode == HybridCpuOpcode.REMW) ||
            !importer.ImportImage(pe, new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicUnsignedRemainder)))
                .Diagnostics.Any(d => d.Code == "HCCIL1830"))
            throw new Exception("dynamic remainder needs managed zero/overflow paths");
        if (!importer.ImportImage(pe, new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicRemainderWide)))
                .Diagnostics.Any(d => d.Code == "HCCIL1832"))
            throw new Exception("dynamic I8 remainder must remain fail-closed without its own checked helper");
        var remainderObject = HybridCpuManagedRemainderInt32EmitterV1.EmitObject();
        if (HybridCpuManagedAbiFamilyV1.SchemaMinor != 61 ||
            HybridCpuManagedAbiFamilyV1.Default.ResolveRuntimeHelper(HybridCpuManagedRemainderInt32EmitterV1.Symbol) is not
                { Support: HybridCpuManagedAbiSupportV1.Supported, MayThrow: true,
                  GcTransition: HybridCpuRuntimeHelperGcTransitionV1.MaySafepoint } ||
            remainderObject.Status != HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success ||
            !remainderObject.Symbols.Any(symbol => symbol.Name == HybridCpuManagedRemainderInt32EmitterV1.Symbol && symbol.IsDefinition))
            throw new Exception("checked signed remainder helper ABI contract");
        var dynamicRemainderGraph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe,
            "dynamic-remainder-smoke", [new(typeof(UnsignedDivisionFixture).FullName!, nameof(UnsignedDivisionFixture.DynamicRemainder))], []));
        if (dynamicRemainderGraph.Status != RestrictedCilImportStatusV1.Success)
            throw new Exception("checked signed remainder graph failed");
        var dynamicRemainderObject = (ScalarControlFlowV2MethodObjectV1)typeof(ScalarControlFlowV2ObjectLinkerV1)
            .GetMethod("CompileMethod", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [dynamicRemainderGraph.Methods.Single(), 0])!;
        if (dynamicRemainderObject.ObjectArtifact.Status != HybridCPU.Compiler.Core.Target.Object.HybridCpuObjectStatusV1.Success ||
            !dynamicRemainderObject.ObjectArtifact.Relocations.Any(r =>
                r.TargetSymbol == "__hybridcpu_managed_remainder_i4_checked"))
            throw new Exception("checked signed remainder HCO/relocation failed");
        if (UnsignedDivisionFixture.SignedRemainderWord(int.MinValue) != int.MinValue % 35 ||
            UnsignedDivisionFixture.SignedRemainderWide(long.MinValue) != long.MinValue % 0x100000001L)
            throw new Exception("signed remainder boundary parity");
        try { UnsignedDivisionFixture.DynamicRemainder(int.MinValue, -1); throw new Exception("CoreCLR remainder overflow must throw"); }
        catch (OverflowException) { }
        if (ManagedCheckedArithmeticRuntimeV1.RemainderInt32(-17, 5) != -17 % 5)
            throw new Exception("Signed checked remainder helper result mismatch");
        try { ManagedCheckedArithmeticRuntimeV1.RemainderInt32(1, 0); throw new Exception("remainder helper zero"); }
        catch (DivideByZeroException) { }
        try { ManagedCheckedArithmeticRuntimeV1.RemainderInt32(int.MinValue, -1); throw new Exception("remainder helper overflow"); }
        catch (OverflowException) { }
    }
}

public static class UnsignedDivisionFixture
{
    public static uint Word(uint numerator) => numerator / 18u;
    public static uint HighWord(uint numerator) => numerator / 0x80000001u;
    public static ulong Wide(ulong numerator) => numerator / 0x100000001UL;
    public static uint Dynamic(uint numerator, uint denominator) => numerator / denominator;
    public static int SignedWord(int numerator) => numerator / 16;
    public static int SignedNegativeDivisor(int numerator) => numerator / -2;
    public static long SignedWide(long numerator) => numerator / 0x100000001L;
    public static int DynamicSigned(int numerator, int denominator) => numerator / denominator;
    public static long DynamicSignedWide(long numerator, long denominator) => numerator / denominator;
    public static int SignedRemainderWord(int numerator) => numerator % 35;
    public static uint UnsignedRemainderWord(uint numerator) => numerator % 35u;
    public static long SignedRemainderWide(long numerator) => numerator % 0x100000001L;
    public static long SignedRemainderWideTen(long numerator) => numerator % 10;
    public static ulong UnsignedRemainderWide(ulong numerator) => numerator % 0x100000001UL;
    public static int DynamicRemainder(int numerator, int denominator) => numerator % denominator;
    public static uint DynamicUnsignedRemainder(uint numerator, uint denominator) => numerator % denominator;
    public static long DynamicRemainderWide(long numerator, long denominator) => numerator % denominator;
}
