using HybridCPU_ISE.Arch;
using System;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Residual adapter/test-support helper that projects a decoder frontend's canonical
    /// bundle contract into occupied-slot <see cref="InstructionIR"/> values.
    /// This helper intentionally lives outside <see cref="IDecoderFrontend"/>, so the
    /// runtime-relevant decoder contract stays centered on <c>DecodeInstructionBundle(...)</c>.
    /// </summary>
    public static class DecoderFrontendOccupiedInstructionProjection
    {
        public static InstructionIR[] DecodeOccupiedInstructions(
            IDecoderFrontend frontend,
            ReadOnlySpan<VLIW_Instruction> bundle)
        {
            ArgumentNullException.ThrowIfNull(frontend);

            DecodedInstructionBundle decodedBundle =
                frontend.DecodeInstructionBundle(
                    bundle,
                    bundleAddress: 0,
                    bundleSerial: 0);
            var results = new InstructionIR[decodedBundle.OccupiedSlotCount];
            int resultIndex = 0;
            for (int slotIndex = 0; slotIndex < decodedBundle.SlotCount; slotIndex++)
            {
                DecodedInstruction slot = decodedBundle.GetDecodedSlot(slotIndex);
                if (!slot.IsOccupied)
                    continue;

                results[resultIndex++] = slot.RequireInstruction();
            }

            return results;
        }
    }
}

