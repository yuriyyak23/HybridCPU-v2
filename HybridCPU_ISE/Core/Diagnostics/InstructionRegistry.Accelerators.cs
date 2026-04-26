
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        /// <summary>
        /// Clear all registered factories for testing.
        /// </summary>
        public static void Clear()
        {
            _factories.Clear();
            _descriptors.Clear();
            _customAccelerators.Clear();
            _customAcceleratorOpcodes.Clear();
            _initialized = false;
        }

        /// <summary>
        /// Get the count of registered instructions.
        /// </summary>
        public static int GetRegisteredCount()
        {
            if (!_initialized)
            {
                Initialize();
            }

            return _factories.Count;
        }

        internal static IReadOnlyList<uint> GetRegisteredOpcodes()
        {
            if (!_initialized)
            {
                Initialize();
            }

            uint[] opcodes = new uint[_factories.Count];
            _factories.Keys.CopyTo(opcodes, 0);
            Array.Sort(opcodes);
            return opcodes;
        }

        /// <summary>
        /// Register a custom HLS accelerator descriptor surface.
        /// The current runtime tracks the opcode family but still fails closed for
        /// canonical decode, direct factory publication, and execution until a truthful
        /// accelerator carrier contract exists.
        /// </summary>
        /// <param name="accelerator">Custom accelerator implementation</param>
        public static void RegisterAccelerator(ICustomAccelerator accelerator)
        {
            ArgumentNullException.ThrowIfNull(accelerator);

            _customAccelerators[accelerator.Name] = accelerator;

            foreach (uint opcode in accelerator.SupportedOpcodes)
            {
                _customAcceleratorOpcodes.Add(opcode);
            }
        }

        public static bool IsCustomAcceleratorOpcode(uint opCode)
        {
            return _customAcceleratorOpcodes.Contains(opCode);
        }

        internal static InvalidOpcodeException CreateUnsupportedCustomAcceleratorException(
            uint opcode,
            string opcodeIdentifier,
            int slotIndex)
        {
            return new InvalidOpcodeException(
                $"Opcode '{opcodeIdentifier}' (slot {slotIndex}) targets a registered custom accelerator contour, " +
                "but the current runtime has no truthful canonical publication, operand ABI, placement, DMA, " +
                "or replay/retire follow-through for custom accelerators. Decode must fail closed instead of " +
                "projecting scalar/system success.",
                opcodeIdentifier,
                slotIndex,
                isProhibited: false);
        }

        internal static InvalidOpcodeException CreateUnsupportedCustomAcceleratorException(uint opcode)
        {
            return new InvalidOpcodeException(
                $"Opcode 0x{opcode:X} reached the registered custom accelerator runtime surface, but the current " +
                "runtime has no truthful canonical publication, operand ABI, placement, DMA, or replay/retire " +
                "follow-through for custom accelerators. Direct/manual publication must fail closed.");
        }

        /// <summary>
        /// Get a registered accelerator by name.
        /// </summary>
        public static ICustomAccelerator? GetAccelerator(string name)
        {
            _customAccelerators.TryGetValue(name, out var accelerator);
            return accelerator;
        }

        /// <summary>
        /// Get all registered accelerators.
        /// </summary>
        public static IReadOnlyDictionary<string, ICustomAccelerator> GetAllAccelerators()
        {
            return _customAccelerators;
        }
    }
}
