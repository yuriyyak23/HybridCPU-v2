using System;
using System.Threading;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.TestHelpers;

/// <summary>
/// Test-only compat ingress adapter for legacy payload verification.
/// Production runtime must not ship repair/sanitization ingress helpers.
/// </summary>
internal static class VliwCompatIngressTestAdapter
{
    private static long _policyGapBitSanitizedCount;

    internal static ulong PolicyGapBitSanitizedCount =>
        (ulong)Interlocked.Read(ref _policyGapBitSanitizedCount);

    internal static void ResetTelemetryForTesting()
    {
        Interlocked.Exchange(ref _policyGapBitSanitizedCount, 0);
    }

    internal static ulong SanitizeWord3ForCompatIngress(ulong rawWord3)
    {
        if ((rawWord3 & VLIW_Instruction.RetiredPolicyGapMask) == 0)
        {
            return rawWord3;
        }

        Interlocked.Increment(ref _policyGapBitSanitizedCount);
        return rawWord3 & ~VLIW_Instruction.RetiredPolicyGapMask;
    }

    internal static bool TryReadInstructionForCompatIngress(
        ReadOnlySpan<byte> source,
        out VLIW_Instruction instruction,
        int offset = 0)
    {
        instruction = default;
        if (source.Length - offset < 32)
        {
            return false;
        }

        instruction.Word0 = BitConverter.ToUInt64(source.Slice(offset, 8));
        instruction.Word1 = BitConverter.ToUInt64(source.Slice(offset + 8, 8));
        instruction.Word2 = BitConverter.ToUInt64(source.Slice(offset + 16, 8));
        instruction.Word3 = SanitizeWord3ForCompatIngress(
            BitConverter.ToUInt64(source.Slice(offset + 24, 8)));
        return true;
    }

    internal static bool TryReadBundleForCompatIngress(
        ReadOnlySpan<byte> source,
        out VLIW_Bundle bundle,
        int offset = 0)
    {
        bundle = default;
        if (source.Length - offset < 256)
        {
            return false;
        }

        for (int slotIndex = 0; slotIndex < 8; slotIndex++)
        {
            if (!TryReadInstructionForCompatIngress(
                source,
                out VLIW_Instruction instruction,
                offset + (slotIndex * 32)))
            {
                bundle = default;
                return false;
            }

            bundle.SetInstruction(slotIndex, instruction);
        }

        return true;
    }

    internal static void SetInstructionForCompatIngress(
        ref VLIW_Bundle bundle,
        int index,
        VLIW_Instruction instruction)
    {
        instruction.Word3 = SanitizeWord3ForCompatIngress(instruction.Word3);
        bundle.SetInstruction(index, instruction);
    }
}
