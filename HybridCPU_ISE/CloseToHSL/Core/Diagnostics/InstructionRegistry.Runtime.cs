
using System;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        /// <summary>
        /// Phase-1 registration: store only the <see cref="MicroOpFactory"/> for
        /// <paramref name="opCode"/>. Call <see cref="RegisterOpAttributes"/> separately
        /// (or not at all - a default <see cref="MicroOpDescriptor"/> is used until then).
        ///
        /// Separating factory registration from attribute registration allows deferred or
        /// table-driven descriptor assignment without modifying factory lambdas.
        ///
        /// Blueprint section 4: split registration into semantic-factory and descriptor phases.
        /// </summary>
        public static void RegisterSemanticFactory(uint opCode, MicroOpFactory factory)
        {
            _factories[opCode] = factory;
            if (!_descriptors.ContainsKey(opCode))
            {
                _descriptors[opCode] = new MicroOpDescriptor();
            }
        }

        /// <summary>
        /// Phase-2 registration: store (or replace) the <see cref="MicroOpDescriptor"/>
        /// for <paramref name="opCode"/>. The descriptor is immutable after this call.
        /// May be called before or after <see cref="RegisterSemanticFactory"/>.
        ///
        /// Blueprint section 4: descriptors are treated as immutable after creation.
        /// </summary>
        public static void RegisterOpAttributes(uint opCode, MicroOpDescriptor descriptor)
        {
            _descriptors[opCode] = descriptor;
        }

        /// <summary>
        /// Get the descriptor for a registered opcode.
        /// </summary>
        /// <param name="opCode">Instruction opcode</param>
        /// <returns>Descriptor if registered, null otherwise</returns>
        public static MicroOpDescriptor GetDescriptor(uint opCode)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _descriptors.TryGetValue(opCode, out var desc) ? desc : null;
        }

        /// <summary>
        /// Create a MicroOp from a decoder context.
        /// </summary>
        public static MicroOp CreateMicroOp(uint opCode, DecoderContext context)
        {
            if (!_initialized)
            {
                Initialize();
            }

            if (IsCustomAcceleratorOpcode(opCode))
            {
                throw CreateUnsupportedCustomAcceleratorException(opCode);
            }

            if (_factories.TryGetValue(opCode, out MicroOpFactory factory))
            {
                MicroOp microOp = factory(context);

                // Apply descriptor flag overrides (Blueprint §2/§4: factory creates the MicroOp;
                // the descriptor provides semantic flags so factories need not set them directly).
                if (microOp != null && _descriptors.TryGetValue(opCode, out var desc))
                {
                    bool prevWrites = microOp.WritesRegister;
                    if (desc.WritesRegister.HasValue)
                    {
                        microOp.WritesRegister = desc.WritesRegister.Value;
                    }

                    if (desc.IsMemoryOp.HasValue)
                    {
                        microOp.IsMemoryOp = desc.IsMemoryOp.Value;
                    }

                    // Blueprint Phase 4 fix: if the descriptor promoted WritesRegister from
                    // false to true, the factory may have already called InitializeMetadata()
                    // while WritesRegister was still false — leaving WriteRegisters empty and
                    // ResourceMask without the write-resource bits. Allow the MicroOp to
                    // re-synchronise its write metadata now that the flag is correct.
                    if (!prevWrites && microOp.WritesRegister)
                    {
                        microOp.RefreshWriteMetadata();
                    }
                }

                microOp?.ValidatePublishedWriteRegisterContract(
                    "InstructionRegistry.CreateMicroOp");

                return microOp;
            }

            // Unknown opcode: always throw IllegalInstruction — no NOP fallback (v6 checklist A3).
            throw new InvalidOperationException($"Unsupported instruction opcode: 0x{opCode:X} ({(Processor.CPU_Core.InstructionsEnum)opCode})");
        }

        /// <summary>
        /// Check if an opcode is registered.
        /// </summary>
        public static bool IsRegistered(uint opCode)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _factories.ContainsKey(opCode);
        }
    }
}
