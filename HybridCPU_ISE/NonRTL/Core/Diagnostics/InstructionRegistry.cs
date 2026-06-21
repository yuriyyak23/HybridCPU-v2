
using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Centralized registry for instruction-to-MicroOp mapping.
    ///
    /// Purpose: Eliminate large switch-case statements in decoder by using registration pattern.
    /// Benefits:
    /// - Easier to add new instructions (register once, use everywhere)
    /// - Clear separation between instruction metadata and execution logic
    /// - Supports runtime extension (can register new instructions dynamically)
    /// - Facilitates testing (can mock/override specific instructions)
    ///
    /// Usage:
    ///   1. At startup: InstructionRegistry.Initialize()
    ///   2. To decode: MicroOp uop = InstructionRegistry.CreateMicroOp(opcode, context)
    ///   3. To add a new instruction: InstructionRegistry.RegisterSemanticFactory(opcode, factory)
    ///      plus InstructionRegistry.RegisterOpAttributes(opcode, descriptor)
    /// </summary>
    public static partial class InstructionRegistry
    {
        // Reserved high-word bits used only as a structural backstop when legacy
        // producer metadata would otherwise emit a zero SafetyMask. These bits keep
        // mask-era compatibility code honest without reintroducing policy into the
        // bundle encoding or widening the mask format.
        private const ulong StructuralFallbackSerializedOpBitHigh = 1UL << 61;
        private const ulong StructuralFallbackDmaStreamLaneBitHigh = 1UL << 62;
        private const ulong StructuralFallbackLane7SingletonBitHigh = 1UL << 63;

        private static Dictionary<uint, MicroOpFactory> _factories = new Dictionary<uint, MicroOpFactory>();
        private static Dictionary<uint, MicroOpDescriptor> _descriptors = new Dictionary<uint, MicroOpDescriptor>();
        private static Dictionary<string, ICustomAccelerator> _customAccelerators = new Dictionary<string, ICustomAccelerator>();
        private static HashSet<uint> _customAcceleratorOpcodes = new HashSet<uint>();
        private static bool _initialized = false;
    }
}
