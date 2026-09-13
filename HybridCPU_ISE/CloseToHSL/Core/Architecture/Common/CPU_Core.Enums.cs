namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public sealed partial class CPU_Core
        {

            // Public literal fields are emitted by HybridCPU.IsaGen from the validated compatibility map.
            public static partial class IsaOpcodeValues
            {
            }

            /// <summary>
            /// Dedicated canonical opcode identity carried by decode/runtime IR.
            /// This separates canonical opcode storage from the mixed legacy
            /// <see cref="InstructionsEnum"/> plane while migration is still in progress.
            /// </summary>
            public readonly record struct IsaOpcode(ushort Value)
            {
                public static IsaOpcode FromInstructionsEnum(InstructionsEnum opcode) =>
                    new((ushort)opcode);

                public static IsaOpcode FromRawValue(uint rawOpcode)
                {
                    if (rawOpcode > ushort.MaxValue)
                    {
                        throw new System.ArgumentOutOfRangeException(
                            nameof(rawOpcode),
                            rawOpcode,
                            $"Opcode value must fit in {nameof(UInt16)}.");
                    }

                    return new((ushort)rawOpcode);
                }

                public InstructionsEnum ToInstructionsEnum() => (InstructionsEnum)Value;

                public override string ToString() => Arch.OpcodeRegistry.GetMnemonicOrHex(Value);

                public static implicit operator IsaOpcode(InstructionsEnum opcode) =>
                    FromInstructionsEnum(opcode);

                public static explicit operator InstructionsEnum(IsaOpcode opcode) =>
                    opcode.ToInstructionsEnum();

                public static explicit operator ushort(IsaOpcode opcode) => opcode.Value;

                public static explicit operator uint(IsaOpcode opcode) => opcode.Value;
            }
        }
    }
}
