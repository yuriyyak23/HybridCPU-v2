using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
        internal const ulong RetiredPolicyGapMask = 1UL << 50;

        internal static string GetRetiredPolicyGapViolationMessage(int slotIndex = -1)
        {
            return slotIndex >= 0
                ? $"Instruction at slot {slotIndex} has word3[50] set. This retired legacy scheduling-policy bit must be zero in production decoded streams."
                : "Instruction has word3[50] set. This retired legacy scheduling-policy bit must be zero in production decoded streams.";
        }

        internal static ulong ValidateWord3ForProductionIngress(ulong rawWord3)
        {
            if ((rawWord3 & RetiredPolicyGapMask) == 0)
            {
                return rawWord3;
            }

            throw new InvalidOpcodeException(GetRetiredPolicyGapViolationMessage());
        }

        internal void ValidateRetiredPolicyGapBitForProductionIngress()
        {
            word3 = ValidateWord3ForProductionIngress(word3);
        }
    }
}
