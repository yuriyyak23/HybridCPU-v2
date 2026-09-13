using System.Reflection;
using HybridCPU.Compiler.Cil;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Target.Object;

internal static class NativeConstantSmoke
{
    public static void Run()
    {
        foreach (ulong expected in new ulong[]
                 {
                     0, 1, 32767, 32768, unchecked((ulong)-32768L), unchecked((ulong)-32769L),
                     0x0000008000000000, 0x123456789abcdef0, ulong.MaxValue
                 })
        {
            ulong actual = 0;
            foreach (ExactNativeConstantStepV1 step in ExactNativeConstantPlanV1.Create(expected))
                actual = step.Opcode switch
                {
                    HybridCpuOpcode.ADDI => unchecked((ulong)(long)(short)step.Immediate),
                    HybridCpuOpcode.SLLI => actual << (short)step.Immediate,
                    HybridCpuOpcode.ORI => actual | unchecked((ulong)(long)(short)step.Immediate),
                    _ => throw new Exception("Unexpected exact constant plan opcode")
                };
            if (actual != expected) throw new Exception($"Exact constant plan produced {actual:x16}, expected {expected:x16}");
        }
        byte[] pe = File.ReadAllBytes(typeof(NativeConstantFixture).Assembly.Location);
        var importer = new RestrictedCilImporterV1(mode: RestrictedCilImportModeV1.ScalarControlFlowV2);
        foreach (var method in typeof(NativeConstantFixture).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var graph = importer.ImportBodyWorld(new(ManagedBodyWorldModeV1.StandaloneRestrictedModule, pe, "native-constant-smoke",
                [new(typeof(NativeConstantFixture).FullName!, method.Name)], []));
            if (graph.Status != RestrictedCilImportStatusV1.Success)
                throw new Exception(method.Name + ": " + string.Join(';', graph.Diagnostics.Select(d => d.Code + ":" + d.Message)));
            var obj = (ScalarControlFlowV2MethodObjectV1)typeof(ScalarControlFlowV2ObjectLinkerV1)
                .GetMethod("CompileMethod", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { graph.Methods.Single(), 0 })!;
            if (obj.ObjectArtifact.Status != HybridCpuObjectStatusV1.Success) throw new Exception("Constant HCO failed");
            byte[] code = obj.ObjectArtifact.Sections.Single(s => s.Name == ".text").Data;
            foreach (ulong input in new ulong[] { 0, 1, 0x7fffffff, 0x80000000, uint.MaxValue, 0x123456789abcdef0, ulong.MaxValue })
            {
                object?[] parameters = method.GetParameters().Length == 0 ? [] :
                    [method.GetParameters()[0].ParameterType == typeof(uint) ? (object)(uint)input : input];
                object expected = method.Invoke(null, parameters)!;
                ulong actual = Evaluate(code, input);
                bool equal = expected switch { int value => (uint)actual == unchecked((uint)value), uint value => (uint)actual == value, ulong value => actual == value, _ => false };
                if (!equal) throw new Exception($"{method.Name}: encoded words returned {actual:x16}, CoreCLR returned {expected}");
            }
        }
        Console.WriteLine("PASS encoded native constant words: signed I4, full I8, arithmetic/masks and DIVU/DIVUW vs CoreCLR");
    }

    // Evaluate actual HCO .text words, not IR Constant operands. Bundle operands are
    // read from a pre-bundle snapshot. This is an ISA-word semantic test, NOT ISE execution.
    internal static ulong Evaluate(byte[] code, ulong input, ulong input2 = 0)
    {
        var registers = new ulong[256]; registers[10] = input; registers[11] = input2; registers[2] = 0x100000;
        var memory = new Dictionary<ulong, byte>();
        for (int offset = 0; offset < code.Length; offset += HybridCpuBundleSerializer.BundleSizeBytes)
        {
            var bundle = new HybridCpuInstructionBundle();
            if (!bundle.TryReadBytes(code, offset)) throw new Exception("Malformed native bundle");
            var before = registers.ToArray();
            bool returning = false;
            for (int slot = 0; slot < HybridCpuInstructionBundle.SlotCount; slot++)
            {
                var word = bundle.GetInstruction(slot);
                var opcode = (HybridCpuOpcode)word.OpCode;
                if (opcode == HybridCpuOpcode.Nope) continue;
                if (!HybridCpuInstructionWord.TryUnpackArchRegs(word.Word1, out byte rd, out byte rs1, out byte rs2))
                    throw new Exception("Non-register native operand payload");
                ulong a = before[rs1], b = before[rs2];
                long immediate = (short)word.Immediate;
                if (opcode == HybridCpuOpcode.JALR) { returning = true; continue; }
                if (opcode == HybridCpuOpcode.SD)
                {
                    for (int i = 0; i < 8; i++) memory[a + (ulong)i] = (byte)(b >> (8 * i));
                    continue;
                }
                ulong result = opcode switch
                {
                    HybridCpuOpcode.ADDI => unchecked(a + (ulong)immediate),
                    HybridCpuOpcode.ADD => unchecked(a + b),
                    HybridCpuOpcode.SUB => unchecked(a - b),
                    HybridCpuOpcode.SLLI => a << ((int)immediate & 63),
                    HybridCpuOpcode.SRAI => unchecked((ulong)((long)a >> ((int)immediate & 63))),
                    HybridCpuOpcode.SLL => a << ((int)b & 63),
                    HybridCpuOpcode.SLLW => unchecked((ulong)(long)(int)((uint)a << ((int)b & 31))),
                    HybridCpuOpcode.SRAW => unchecked((ulong)(long)((int)a >> ((int)b & 31))),
                    HybridCpuOpcode.SRLW => (uint)a >> ((int)b & 31),
                    HybridCpuOpcode.SRA => unchecked((ulong)((long)a >> ((int)b & 63))),
                    HybridCpuOpcode.SRL => a >> ((int)b & 63),
                    HybridCpuOpcode.ORI => a | unchecked((ulong)immediate),
                    HybridCpuOpcode.OR => a | b,
                    HybridCpuOpcode.AND => a & b,
                    HybridCpuOpcode.XORI => a ^ unchecked((ulong)immediate),
                    HybridCpuOpcode.XOR => a ^ b,
                    HybridCpuOpcode.DIVU when b != 0 => a / b,
                    HybridCpuOpcode.DIVUW when (uint)b != 0 => unchecked((ulong)(long)(int)((uint)a / (uint)b)),
                    HybridCpuOpcode.LD => Enumerable.Range(0, 8).Aggregate(0UL, (value, i) => value | (ulong)memory[a + (ulong)i] << (8 * i)),
                    _ => throw new Exception("Unexpected native constant opcode " + opcode)
                };
                if (rd == HybridCpuInstructionWord.NoArchReg) throw new Exception("Native result has no register");
                if (rd != 0) registers[rd] = result;
            }
            registers[0] = 0;
            if (returning) return registers[10];
        }
        throw new Exception("Native constant fragment did not return");
    }
}

public static class NativeConstantFixture
{
    public static int MinI4() => int.MinValue;
    public static int MaxI4() => int.MaxValue;
    public static ulong FullI8() => 0xfedcba9876543210UL;
    public static ulong AddWide(ulong value) => value + 0x123456789abcdef0UL;
    public static ulong Mask(ulong value) => value & 0xff00ff00ff00ff00UL;
    public static ulong OrWide(ulong value) => value | 0xff00ff00ff00ff00UL;
    public static ulong XorWide(ulong value) => value ^ 0xff00ff00ff00ff00UL;
    public static int Signed16(ulong value) => (short)value;
    public static ulong DivideWide(ulong value) => value / 0x100000001UL;
    public static uint DivideWord(uint value) => value / 0x80000001U;
    public static uint Shift16(uint value) => value << 16;
}
