using HybridCPU.Compiler.Core.IR;

namespace HybridCPU.Compiler.Cil;

public sealed record ExactNativeConstantStepV1(HybridCpuOpcode Opcode, ushort Immediate);

/// <summary>Exact register materialization for one native-width bit pattern.</summary>
public static class ExactNativeConstantPlanV1
{
    public static IReadOnlyList<ExactNativeConstantStepV1> Create(ulong value)
    {
        if (FitsSignedImmediate(value))
            return [new(HybridCpuOpcode.ADDI, unchecked((ushort)(short)(long)value))];

        int first = 7;
        while (first > 0 && (byte)(value >> (8 * first)) == 0) first--;
        var result = new List<ExactNativeConstantStepV1>
        {
            new(HybridCpuOpcode.ADDI, (byte)(value >> (8 * first)))
        };
        for (int part = first - 1; part >= 0; part--)
        {
            result.Add(new(HybridCpuOpcode.SLLI, 8));
            byte next = (byte)(value >> (8 * part));
            if (next != 0) result.Add(new(HybridCpuOpcode.ORI, next));
        }
        return result;
    }

    public static bool FitsSignedImmediate(ulong value) =>
        unchecked((long)value) is >= short.MinValue and <= short.MaxValue;
}
