using System;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Memory
{
    /// <summary>
    /// Byte-addressable memory bus used by <see cref="MemoryUnit"/> for typed
    /// load/store operations. Decouples the execution-level memory unit from
    /// the concrete memory subsystem.
    /// </summary>
    public interface IMemoryBus
    {
        byte[] Read(ulong address, int length);

        void Write(ulong address, byte[] data);
    }

    /// <summary>
    /// Thrown when a memory access violates the ISA v4 alignment policy.
    /// </summary>
    public sealed class MemoryAlignmentException : Exception
    {
        public ulong Address { get; }

        public int RequiredAlignment { get; }

        public string InstructionMnemonic { get; }

        public MemoryAlignmentException(ulong address, int requiredAlignment, string instructionMnemonic)
            : base($"Misaligned {instructionMnemonic} access at 0x{address:X16} (required alignment: {requiredAlignment} bytes)")
        {
            Address = address;
            RequiredAlignment = requiredAlignment;
            InstructionMnemonic = instructionMnemonic;
        }
    }

    internal readonly struct ResolvedMemoryAccess
    {
        private ResolvedMemoryAccess(
            ulong effectiveAddress,
            bool hasRegisterWrite,
            ushort registerDestination,
            ulong registerWriteValue,
            bool hasStoreCommit,
            ulong storeData,
            byte accessSize)
        {
            EffectiveAddress = effectiveAddress;
            HasRegisterWrite = hasRegisterWrite;
            RegisterDestination = registerDestination;
            RegisterWriteValue = registerWriteValue;
            HasStoreCommit = hasStoreCommit;
            StoreData = storeData;
            AccessSize = accessSize;
        }

        public ulong EffectiveAddress { get; }

        public bool HasRegisterWrite { get; }

        public ushort RegisterDestination { get; }

        public ulong RegisterWriteValue { get; }

        public bool HasStoreCommit { get; }

        public ulong StoreData { get; }

        public byte AccessSize { get; }

        public static ResolvedMemoryAccess NoArchitecturalWrite(ulong effectiveAddress) =>
            new(effectiveAddress, false, 0, 0, false, 0, 0);

        public static ResolvedMemoryAccess RegisterWrite(
            ulong effectiveAddress,
            ushort registerDestination,
            ulong registerWriteValue) =>
            new(
                effectiveAddress,
                hasRegisterWrite: registerDestination != 0,
                registerDestination,
                registerWriteValue,
                hasStoreCommit: false,
                storeData: 0,
                accessSize: 0);

        public static ResolvedMemoryAccess StoreCommit(
            ulong effectiveAddress,
            ulong storeData,
            byte accessSize) =>
            new(
                effectiveAddress,
                hasRegisterWrite: false,
                registerDestination: 0,
                registerWriteValue: 0,
                hasStoreCommit: true,
                storeData,
                accessSize);
    }

    /// <summary>
    /// ISA v4 memory execution unit for typed scalar loads/stores plus the retained
    /// absolute legacy Load/Store contour.
    /// </summary>
    public sealed class MemoryUnit
    {
        private readonly IMemoryBus _bus;
        private readonly bool _trapOnMisalign;

        public MemoryUnit(IMemoryBus bus, bool trapOnMisalign = true)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _trapOnMisalign = trapOnMisalign;
        }

        /// <summary>
        /// Internal semantic-compatibility execution surface for typed loads/stores.
        /// For loads, writes the result directly to <paramref name="state"/> and
        /// for stores mutates the backing bus immediately, so this method is not
        /// the authoritative retire contract for memory-side follow-through.
        /// Callers that need an authoritative retire-window publication shape should
        /// resolve through <c>ExecutionDispatcherV4.CaptureRetireWindowPublications(...)</c>.
        /// Returns the effective address for pipeline tracking.
        /// </summary>
        internal ulong Execute(InstructionIR instr, ICanonicalCpuState state) =>
            Execute(instr, state, vtId: 0);

        /// <summary>
        /// Internal semantic-compatibility execution surface for typed loads/stores scoped
        /// to virtual thread <paramref name="vtId"/>.
        /// </summary>
        internal ulong Execute(InstructionIR instr, ICanonicalCpuState state, byte vtId)
        {
            (ulong rs1, ulong rs2) = ResolveOperandValues(instr, state, vtId);
            ResolvedMemoryAccess resolved = ResolveArchitecturalAccess(instr, rs1, rs2);

            if (resolved.HasRegisterWrite)
            {
                state.WriteRegister(vtId, resolved.RegisterDestination, resolved.RegisterWriteValue);
            }

            if (resolved.HasStoreCommit)
            {
                _bus.Write(
                    resolved.EffectiveAddress,
                    GetScalarStoreBytes(resolved.StoreData, resolved.AccessSize));
            }

            return resolved.EffectiveAddress;
        }

        /// <summary>
        /// Resolve a typed scalar memory instruction without mutating the
        /// architectural state or the backing bus.
        /// </summary>
        internal ResolvedMemoryAccess ResolveArchitecturalAccess(
            InstructionIR instr,
            ulong rs1Value,
            ulong rs2Value)
        {
            ushort opcode = instr.CanonicalOpcode.Value;
            ulong effectiveAddress = ResolveEffectiveAddress(instr, rs1Value);

            switch (opcode)
            {
                case IsaOpcodeValues.Load:
                {
                    byte[] raw = _bus.Read(effectiveAddress, 8);
                    ulong value = BitConverter.ToUInt64(raw, 0);
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LB:
                {
                    byte[] raw = _bus.Read(effectiveAddress, 1);
                    ulong value = unchecked((ulong)(long)(sbyte)raw[0]);
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LBU:
                {
                    byte[] raw = _bus.Read(effectiveAddress, 1);
                    ulong value = raw[0];
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LH:
                {
                    CheckAlignment(effectiveAddress, 2, "LH");
                    byte[] raw = _bus.Read(effectiveAddress, 2);
                    ulong value = unchecked((ulong)(long)(short)(raw[0] | (raw[1] << 8)));
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LHU:
                {
                    CheckAlignment(effectiveAddress, 2, "LHU");
                    byte[] raw = _bus.Read(effectiveAddress, 2);
                    ulong value = (ushort)(raw[0] | (raw[1] << 8));
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LW:
                {
                    CheckAlignment(effectiveAddress, 4, "LW");
                    byte[] raw = _bus.Read(effectiveAddress, 4);
                    int word = raw[0] | (raw[1] << 8) | (raw[2] << 16) | (raw[3] << 24);
                    ulong value = unchecked((ulong)(long)word);
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LWU:
                {
                    CheckAlignment(effectiveAddress, 4, "LWU");
                    byte[] raw = _bus.Read(effectiveAddress, 4);
                    ulong value = (uint)(raw[0] | (raw[1] << 8) | (raw[2] << 16) | (raw[3] << 24));
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.LD:
                {
                    CheckAlignment(effectiveAddress, 8, "LD");
                    byte[] raw = _bus.Read(effectiveAddress, 8);
                    ulong value = BitConverter.ToUInt64(raw, 0);
                    return instr.Rd != 0
                        ? ResolvedMemoryAccess.RegisterWrite(effectiveAddress, instr.Rd, value)
                        : ResolvedMemoryAccess.NoArchitecturalWrite(effectiveAddress);
                }

                case IsaOpcodeValues.SB:
                    return ResolvedMemoryAccess.StoreCommit(
                        effectiveAddress,
                        rs2Value,
                        accessSize: 1);

                case IsaOpcodeValues.SH:
                    CheckAlignment(effectiveAddress, 2, "SH");
                    return ResolvedMemoryAccess.StoreCommit(
                        effectiveAddress,
                        rs2Value,
                        accessSize: 2);

                case IsaOpcodeValues.SW:
                    CheckAlignment(effectiveAddress, 4, "SW");
                    return ResolvedMemoryAccess.StoreCommit(
                        effectiveAddress,
                        rs2Value,
                        accessSize: 4);

                case IsaOpcodeValues.SD:
                    CheckAlignment(effectiveAddress, 8, "SD");
                    return ResolvedMemoryAccess.StoreCommit(
                        effectiveAddress,
                        rs2Value,
                        accessSize: 8);

                case IsaOpcodeValues.Store:
                    return ResolvedMemoryAccess.StoreCommit(
                        effectiveAddress,
                        rs2Value,
                        accessSize: 8);

                default:
                    throw new InvalidOpcodeException(
                        $"Invalid opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)} in MemoryUnit",
                        OpcodeRegistry.GetMnemonicOrHex(opcode),
                        -1,
                        false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ResolveEffectiveAddress(InstructionIR instr, ulong rs1Value) =>
            instr.HasAbsoluteAddressing ? (ulong)instr.Imm : (ulong)((long)rs1Value + instr.Imm);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (ulong Rs1Value, ulong Rs2Value) ResolveOperandValues(
            InstructionIR instr,
            ICanonicalCpuState state,
            byte vtId)
        {
            ulong rs1Value = instr.HasAbsoluteAddressing
                ? 0UL
                : unchecked((ulong)state.ReadRegister(vtId, instr.Rs1));
            ulong rs2Value = RequiresStoreSourceValue(instr)
                ? unchecked((ulong)state.ReadRegister(vtId, instr.Rs2))
                : 0UL;
            return (rs1Value, rs2Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RequiresStoreSourceValue(InstructionIR instr) =>
            instr.CanonicalOpcode.Value is
                IsaOpcodeValues.SB or
                IsaOpcodeValues.SH or
                IsaOpcodeValues.SW or
                IsaOpcodeValues.SD or
                IsaOpcodeValues.Store;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAlignment(ulong address, int alignment, string mnemonic)
        {
            if (_trapOnMisalign && (address & (ulong)(alignment - 1)) != 0)
            {
                throw new MemoryAlignmentException(address, alignment, mnemonic);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] GetScalarStoreBytes(ulong storeData, byte accessSize)
        {
            return accessSize switch
            {
                1 => new[] { (byte)(storeData & 0xFF) },
                2 => new[]
                {
                    (byte)(storeData & 0xFF),
                    (byte)((storeData >> 8) & 0xFF)
                },
                4 => new[]
                {
                    (byte)(storeData & 0xFF),
                    (byte)((storeData >> 8) & 0xFF),
                    (byte)((storeData >> 16) & 0xFF),
                    (byte)((storeData >> 24) & 0xFF)
                },
                8 => BitConverter.GetBytes(storeData),
                _ => throw new InvalidOperationException(
                    $"Unsupported scalar memory access size {accessSize}.")
            };
        }
    }
}

