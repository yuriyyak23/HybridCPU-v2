using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;

namespace YAKSys_Hybrid_CPU.Core.Decoder
{
    /// <summary>
    /// Canonical contract for the HybridCPU ISA v4 VLIW decoder front-end.
    /// <para>
    /// All ISA v4 decoder implementations must implement this interface.
    /// Production runtime uses exactly one active decode path: the native canonical
    /// VLIW path exposed by <see cref="VliwDecoderV4"/>.
    /// Explicit compat implementations may still exist for retained wrapper ingress,
    /// offline tooling, and tests, but they must not redefine the production decoder
    /// authority contour.
    /// </para>
    /// <para>
    /// Production implementor: <see cref="VliwDecoderV4"/>.
    /// </para>
    /// </summary>
    public interface IDecoderFrontend
    {
        /// <summary>
        /// Decode a single instruction from a VLIW bundle slot and produce a canonical
        /// <see cref="InstructionIR"/> record.
        /// </summary>
        /// <param name="instruction">
        /// Raw 256-bit VLIW instruction word as read from the bundle slot.
        /// </param>
        /// <param name="slotIndex">
        /// Physical slot index (0–7) within the containing bundle.
        /// Used for diagnostics and exception reporting only — not for decode logic.
        /// </param>
        /// <returns>
        /// Canonical <see cref="InstructionIR"/> record with <c>CanonicalOpcode</c>,
        /// <c>Class</c>, <c>SerializationClass</c>, register fields, and <c>Imm</c>
        /// populated. Producer-side admission facts travel through sideband slot metadata,
        /// not through legacy safety-mask fields on the IR surface.
        /// </returns>
        /// <exception cref="Arch.InvalidOpcodeException">
        /// Thrown when the opcode is listed in
        /// <see cref="Arch.IsaV4Surface.ProhibitedOpcodes"/> (pseudo-ops, hints,
        /// compiler wrappers) or is otherwise outside the canonical ISA v4 opcode space.
        /// </exception>
        InstructionIR Decode(
            in VLIW_Instruction instruction,
            int slotIndex);

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// </summary>
        /// <param name="bundle">
        /// Read-only span of VLIW instructions, one per physical bundle slot.
        /// Typically 8 elements for an 8-slot bundle.
        /// </param>
        /// <param name="bundleAddress">
        /// Programme-counter address of the first byte of the decoded bundle.
        /// </param>
        /// <param name="bundleSerial">
        /// Bundle serial number used by downstream pipeline tracking.
        /// </param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with per-slot occupancy
        /// preserved and semantic content represented only as <see cref="InstructionIR"/>.
        /// </returns>
        /// <exception cref="Arch.InvalidOpcodeException">
        /// Thrown (and propagated) if any non-NOP slot contains a prohibited opcode.
        /// </exception>
        DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            VliwBundleAnnotations? bundleAnnotations,
            ulong bundleAddress,
            ulong bundleSerial = 0);

        /// <summary>
        /// Decode one frontend bundle into the canonical Phase 03 bundle contract.
        /// Uses neutral/default slot metadata when no sideband bundle annotations are available.
        /// </summary>
        /// <param name="bundle">
        /// Read-only span of VLIW instructions, one per physical bundle slot.
        /// Typically 8 elements for an 8-slot bundle.
        /// </param>
        /// <param name="bundleAddress">
        /// Programme-counter address of the first byte of the decoded bundle.
        /// </param>
        /// <param name="bundleSerial">
        /// Bundle serial number used by downstream pipeline tracking.
        /// </param>
        /// <returns>
        /// Canonical <see cref="DecodedInstructionBundle"/> with per-slot occupancy
        /// preserved and semantic content represented only as <see cref="InstructionIR"/>.
        /// </returns>
        /// <exception cref="Arch.InvalidOpcodeException">
        /// Thrown (and propagated) if any non-NOP slot contains a prohibited opcode.
        /// </exception>
        DecodedInstructionBundle DecodeInstructionBundle(
            System.ReadOnlySpan<VLIW_Instruction> bundle,
            ulong bundleAddress,
            ulong bundleSerial = 0);

    }
}

