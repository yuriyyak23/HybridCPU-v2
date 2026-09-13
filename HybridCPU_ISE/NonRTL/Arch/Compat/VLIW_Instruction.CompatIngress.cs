using YAKSys_Hybrid_CPU.Arch;

namespace HybridCPU_ISE.Arch
{
    public partial struct VLIW_Instruction
    {
        internal const ulong ReservedWord0Mask = 0x0000FF0000000000UL;
        internal const ulong RetiredPolicyGapMask = 1UL << 50;

        internal static string GetReservedWord0ViolationMessage(int slotIndex = -1)
        {
            return slotIndex >= 0
                ? $"Instruction at slot {slotIndex} has word0[47:40] set. This reserved field must be zero in production decoded streams."
                : "Instruction has word0[47:40] set. This reserved field must be zero in production decoded streams.";
        }

        internal static string GetRetiredPolicyGapViolationMessage(int slotIndex = -1)
        {
            return slotIndex >= 0
                ? $"Instruction at slot {slotIndex} has word3[50] set. This retired legacy scheduling-policy bit must be zero in production decoded streams."
                : "Instruction has word3[50] set. This retired legacy scheduling-policy bit must be zero in production decoded streams.";
        }

        internal static ulong ValidateWord0ForProductionIngress(ulong rawWord0)
        {
            if ((rawWord0 & ReservedWord0Mask) == 0)
            {
                return rawWord0;
            }

            throw new InvalidOpcodeException(GetReservedWord0ViolationMessage());
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
            word0 = ValidateWord0ForProductionIngress(word0);
            word3 = ValidateWord3ForProductionIngress(word3);
        }
    }
}
