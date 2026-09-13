using System;

namespace YAKSys_Hybrid_CPU.Arch
{
    /// <summary>
    /// Thrown when the ISA v4 decoder encounters an opcode that is either:
    /// <list type="bullet">
    ///   <item>Listed in <see cref="IsaV4Surface.ProhibitedOpcodes"/> — assembler pseudo-ops,
    ///     hint opcodes, or compiler/emulator wrappers that must not appear in the hardware
    ///     instruction stream.</item>
    ///   <item>Outside the canonical ISA v4 opcode space and therefore has no known
    ///     <see cref="InstructionClass"/>/<see cref="SerializationClass"/> classification.</item>
    /// </list>
    /// </summary>
    public sealed class InvalidOpcodeException : Exception
    {
        /// <summary>Opcode name or numeric value that caused the fault.</summary>
        public string? OpcodeIdentifier { get; }

        /// <summary>Physical bundle slot index (0–7) in which the fault occurred.</summary>
        public int SlotIndex { get; }

        /// <summary>
        /// Whether the opcode is specifically in <see cref="IsaV4Surface.ProhibitedOpcodes"/>
        /// (as opposed to simply being unknown / unrecognised).
        /// </summary>
        public bool IsProhibited { get; }

        /// <summary>
        /// Initializes a new <see cref="InvalidOpcodeException"/> for a prohibited opcode.
        /// </summary>
        public InvalidOpcodeException(
            string message,
            string opcodeIdentifier,
            int slotIndex,
            bool isProhibited = true)
            : base(message)
        {
            OpcodeIdentifier = opcodeIdentifier;
            SlotIndex = slotIndex;
            IsProhibited = isProhibited;
        }

        /// <summary>
        /// Initializes a new <see cref="InvalidOpcodeException"/> without slot context.
        /// Used by static classifier paths.
        /// </summary>
        public InvalidOpcodeException(string message)
            : base(message)
        {
            OpcodeIdentifier = null;
            SlotIndex = -1;
            IsProhibited = false;
        }

        /// <summary>
        /// Initializes a new <see cref="InvalidOpcodeException"/> with an inner exception.
        /// </summary>
        public InvalidOpcodeException(string message, Exception inner)
            : base(message, inner)
        {
            OpcodeIdentifier = null;
            SlotIndex = -1;
            IsProhibited = false;
        }
    }
}
