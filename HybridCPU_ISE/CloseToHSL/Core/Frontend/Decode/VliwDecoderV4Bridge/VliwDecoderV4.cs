using HybridCPU_ISE.Arch;
using System.Globalization;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// HybridCPU ISA v4 native VLIW decoder public façade.
    /// It preserves <see cref="IDecoderFrontend"/> and legacy
    /// <see cref="InstructionIR"/> projection compatibility while delegating
    /// decode-owned static legality to the declarative canonical pipeline.
    /// </summary>
    public sealed class VliwDecoderV4 : IDecoderFrontend
    {
        /// <summary>
        /// Decode a single instruction from a VLIW bundle slot.
        /// </summary>
        /// <param name="instruction">Raw VLIW instruction from the bundle slot.</param>
        /// <param name="slotIndex">Physical slot index (0–7) within the bundle.</param>
        /// <returns>Canonical <see cref="InstructionIR"/> compatibility projection.</returns>
        /// <exception cref="InvalidOpcodeException">
        /// Thrown when a decode-owned static encoding is illegal. The internal
        /// <see cref="DecodeFailure"/> remains available only inside the
        /// declarative pipeline.
        /// </exception>
        public InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            // The historical per-slot API permits an out-of-window diagnostic
            // index in unit tooling. Keep that compatibility while bundle decode
            // remains strictly eight physical slots.
            int rawReaderIndex = (uint)slotIndex < BundleMetadata.BundleSlotCount
                ? slotIndex
                : 0;
            RawSlot rawSlot = RawSlotReader.Read(in instruction, rawReaderIndex) with
            {
                SlotIndex = slotIndex
            };
            if (!DeclarativeDecoderPipeline.TryDecodeOccupiedSlot(
                    in rawSlot,
                    InstructionSlotMetadata.Default,
                    out DeclarativeDecodedSlot? declarativeSlot,
                    out DecodeFailure? failure))
            {
                throw DecodeFailureCompatibilityAdapter.ToInvalidOpcodeException(failure!, in instruction);
            }

            RuntimeSidebandProjection runtimeSideband = DecoderRuntimeSidebandValidator.ValidateAndResolve(
                in instruction,
                slotIndex,
                InstructionSlotMetadata.Default);
            return CanonicalInstructionIrCompatibilityProjector.ProjectInstruction(
                declarativeSlot!.CanonicalInstruction,
                declarativeSlot,
                runtimeSideband);
        }

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// </summary>
        /// <param name="bundle">Span of raw VLIW instructions (one per slot).</param>
        /// <param name="bundleAnnotations">Frozen source-sideband annotations, when present.</param>
        /// <param name="bundleAddress">PC address of the first byte of the decoded bundle.</param>
        /// <param name="bundleSerial">Bundle serial used by downstream pipeline tracking.</param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with one logical slot per
        /// physical bundle slot and legacy <see cref="InstructionIR"/> adapters.
        /// </returns>
        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0,
            CanonicalDecodeContext? decodeContext = null)
        {
            if (!DeclarativeDecoderPipeline.TryDecodeBundle(
                    bundle,
                    bundleAnnotations,
                    bundleAddress,
                    bundleSerial,
                    decodeContext,
                    out DeclarativeDecodedBundle? declarativeBundle,
                    out DecodeFailure? failure))
            {
                throw DecodeFailureCompatibilityAdapter.ToInvalidOpcodeException(failure!, bundle);
            }

            return CanonicalInstructionIrCompatibilityProjector.ProjectBundle(
                bundle,
                bundleAnnotations,
                declarativeBundle!,
                bundleAddress,
                bundleSerial);
        }

        /// <summary>
        /// Decode one frontend bundle using neutral/default slot metadata when no
        /// sideband bundle annotations are available.
        /// </summary>
        public DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            ulong bundleAddress,
            ulong bundleSerial = 0)
        {
            return DecodeInstructionBundle(
                bundle,
                null,
                bundleAddress,
                bundleSerial);
        }

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="opcode"/> is listed as
        /// prohibited in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(Processor.CPU_Core.IsaOpcode opcode)
        {
            var name = opcode.ToString();
            return IsProhibited(opcode.Value, name);
        }

        /// <summary>
        /// Returns <see langword="true"/> if the string <paramref name="opcodeName"/> is
        /// listed in <see cref="IsaV4Surface.ProhibitedOpcodes"/>.
        /// </summary>
        public static bool IsProhibited(string opcodeName)
        {
            if (TryParseOpcodeIdentifier(opcodeName, out ushort opcode))
            {
                return IsProhibited(opcode, opcodeName);
            }

            return IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant());
        }

        private static bool IsProhibited(ushort opcode, string opcodeName)
        {
            if (IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName) ||
                IsaV4Surface.ProhibitedOpcodes.Contains(opcodeName.ToUpperInvariant()))
            {
                return true;
            }

            string decimalIdentifier = opcode.ToString(CultureInfo.InvariantCulture);
            if (IsaV4Surface.ProhibitedOpcodes.Contains(decimalIdentifier))
            {
                return true;
            }

            string hexIdentifier = $"0x{opcode:X}";
            return IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier) ||
                   IsaV4Surface.ProhibitedOpcodes.Contains(hexIdentifier.ToUpperInvariant());
        }

        private static bool TryParseOpcodeIdentifier(string opcodeIdentifier, out ushort opcode)
        {
            if (ushort.TryParse(
                opcodeIdentifier,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out opcode))
            {
                return true;
            }

            if (opcodeIdentifier.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                ushort.TryParse(
                    opcodeIdentifier[2..],
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out opcode))
            {
                return true;
            }

            opcode = default;
            return false;
        }
    }
}
