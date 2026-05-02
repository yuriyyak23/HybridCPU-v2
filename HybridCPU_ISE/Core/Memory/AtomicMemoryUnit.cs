using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core.Memory
{
    public static class AtomicMemoryUnitExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MainMemoryAtomicMemoryUnit RequireMainMemoryAtomicMemoryUnit(
            IAtomicMemoryUnit atomicMemoryUnit,
            string operation)
        {
            ArgumentNullException.ThrowIfNull(atomicMemoryUnit);

            if (atomicMemoryUnit is MainMemoryAtomicMemoryUnit mainMemoryAtomicMemoryUnit)
            {
                return mainMemoryAtomicMemoryUnit;
            }

            throw new InvalidOperationException(
                $"{operation} requires a core-owned {nameof(MainMemoryAtomicMemoryUnit)} implementation, but received {atomicMemoryUnit.GetType().FullName ?? atomicMemoryUnit.GetType().Name}.");
        }

        /// <summary>
        /// Resolve validated atomic retire intent without mutating memory.
        /// The returned effect is a retire-side carrier; reservation checks and memory writes
        /// happen only when <see cref="ApplyRetireEffect(IAtomicMemoryUnit, in AtomicRetireEffect)"/>
        /// is invoked on the authoritative retire path.
        /// </summary>
        public static AtomicRetireEffect ResolveRetireEffect(
            this IAtomicMemoryUnit atomicMemoryUnit,
            ushort opcode,
            ushort destinationRegister,
            ulong address,
            ulong sourceValue,
            int coreId,
            int virtualThreadId)
        {
            MainMemoryAtomicMemoryUnit mainMemoryAtomicMemoryUnit =
                RequireMainMemoryAtomicMemoryUnit(
                    atomicMemoryUnit,
                    "Atomic retire-effect resolution");
            byte accessSize = MainMemoryAtomicMemoryUnit.ResolveAccessSize(opcode);
            mainMemoryAtomicMemoryUnit.ValidateAccess(opcode, address, accessSize);
            return AtomicRetireEffect.Create(
                opcode,
                accessSize,
                address,
                sourceValue,
                destinationRegister,
                unchecked((ushort)coreId),
                virtualThreadId);
        }

        /// <summary>
        /// Apply a previously resolved atomic retire effect against the bound main-memory surface.
        /// The returned outcome reports retire-time memory mutation and any architectural
        /// destination-register writeback payload as separate facts.
        /// </summary>
        public static AtomicRetireOutcome ApplyRetireEffect(
            this IAtomicMemoryUnit atomicMemoryUnit,
            in AtomicRetireEffect effect)
        {
            if (!effect.IsValid)
            {
                return default;
            }

            return RequireMainMemoryAtomicMemoryUnit(
                atomicMemoryUnit,
                "Atomic retire/apply").ApplyResolvedRetireEffect(effect);
        }
    }

    internal static class AtomicReservationRegistry
    {
        private readonly struct Reservation
        {
            public Reservation(ulong address, byte accessSize)
            {
                Address = address;
                AccessSize = accessSize;
            }

            public ulong Address { get; }

            public byte AccessSize { get; }
        }

        private static readonly object Sync = new();
        private static readonly Dictionary<(int CoreId, int VirtualThreadId), Reservation> Reservations = new();

        public static void RegisterReservation(int coreId, int virtualThreadId, ulong address, byte accessSize)
        {
            lock (Sync)
            {
                Reservations[(coreId, virtualThreadId)] = new Reservation(address, accessSize);
            }
        }

        public static bool ConsumeReservation(
            int coreId,
            int virtualThreadId,
            ulong address,
            byte accessSize)
        {
            lock (Sync)
            {
                (int CoreId, int VirtualThreadId) key = (coreId, virtualThreadId);
                if (!Reservations.TryGetValue(key, out Reservation reservation))
                {
                    return false;
                }

                Reservations.Remove(key);
                return reservation.Address == address &&
                    reservation.AccessSize == accessSize;
            }
        }

        public static void ClearReservation(int coreId, int virtualThreadId)
        {
            lock (Sync)
            {
                Reservations.Remove((coreId, virtualThreadId));
            }
        }

        public static void InvalidateOverlapping(ulong address, int byteCount)
        {
            if (byteCount <= 0)
            {
                return;
            }

            ulong length = (ulong)byteCount;
            lock (Sync)
            {
                List<(int CoreId, int VirtualThreadId)> staleReservations = new();
                foreach (KeyValuePair<(int CoreId, int VirtualThreadId), Reservation> entry in Reservations)
                {
                    if (MemoryRangeOverlap.RangesOverlap(
                        address,
                        length,
                        entry.Value.Address,
                        entry.Value.AccessSize))
                    {
                        staleReservations.Add(entry.Key);
                    }
                }

                for (int i = 0; i < staleReservations.Count; i++)
                {
                    Reservations.Remove(staleReservations[i]);
                }
            }
        }

    }

    public sealed class MainMemoryAtomicMemoryUnit : IAtomicMemoryUnit
    {
        private readonly Processor.MainMemoryArea _mainMemory;

        [Obsolete("Use MainMemoryAtomicMemoryUnit(Processor.MainMemoryArea mainMemory) with explicit binding. Implicit Processor.MainMemory fallback is disabled.")]
        public MainMemoryAtomicMemoryUnit()
        {
            throw new MainMemoryBindingUnavailableException(
                nameof(MainMemoryAtomicMemoryUnit),
                "parameterless construction");
        }

        public MainMemoryAtomicMemoryUnit(Processor.MainMemoryArea mainMemory)
        {
            ArgumentNullException.ThrowIfNull(mainMemory);
            _mainMemory = mainMemory;
        }

        public int LoadReserved32(ulong address)
        {
            return LoadReserved32(address, coreId: 0, virtualThreadId: 0);
        }

        public int StoreConditional32(ulong address, int value)
        {
            return StoreConditional32(address, value, coreId: 0, virtualThreadId: 0);
        }

        public long LoadReserved64(ulong address)
        {
            return LoadReserved64(address, coreId: 0, virtualThreadId: 0);
        }

        public long StoreConditional64(ulong address, long value)
        {
            return StoreConditional64(address, value, coreId: 0, virtualThreadId: 0);
        }

        public int AtomicSwap32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(address, unchecked((uint)value), static (_, source) => source));

        public int AtomicAdd32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(address, unchecked((uint)value), static (previous, source) => unchecked(previous + source)));

        public int AtomicXor32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(address, unchecked((uint)value), static (previous, source) => previous ^ source));

        public int AtomicAnd32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(address, unchecked((uint)value), static (previous, source) => previous & source));

        public int AtomicOr32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(address, unchecked((uint)value), static (previous, source) => previous | source));

        public int AtomicMinSigned32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(
                address,
                unchecked((uint)value),
                static (previous, source) => unchecked((uint)Math.Min((int)previous, (int)source))));

        public int AtomicMaxSigned32(ulong address, int value) =>
            unchecked((int)ApplyWordAtomic(
                address,
                unchecked((uint)value),
                static (previous, source) => unchecked((uint)Math.Max((int)previous, (int)source))));

        public uint AtomicMinUnsigned32(ulong address, uint value) =>
            ApplyWordAtomic(address, value, static (previous, source) => Math.Min(previous, source));

        public uint AtomicMaxUnsigned32(ulong address, uint value) =>
            ApplyWordAtomic(address, value, static (previous, source) => Math.Max(previous, source));

        public long AtomicSwap64(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(address, unchecked((ulong)value), static (_, source) => source));

        public long AtomicAdd64(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(address, unchecked((ulong)value), static (previous, source) => unchecked(previous + source)));

        public long AtomicXor64(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(address, unchecked((ulong)value), static (previous, source) => previous ^ source));

        public long AtomicAnd64(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(address, unchecked((ulong)value), static (previous, source) => previous & source));

        public long AtomicOr64(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(address, unchecked((ulong)value), static (previous, source) => previous | source));

        public long AtomicMin64Signed(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(
                address,
                unchecked((ulong)value),
                static (previous, source) => unchecked((ulong)Math.Min((long)previous, (long)source))));

        public long AtomicMax64Signed(ulong address, long value) =>
            unchecked((long)ApplyDoublewordAtomic(
                address,
                unchecked((ulong)value),
                static (previous, source) => unchecked((ulong)Math.Max((long)previous, (long)source))));

        public ulong AtomicMin64Unsigned(ulong address, ulong value) =>
            ApplyDoublewordAtomic(address, value, static (previous, source) => Math.Min(previous, source));

        public ulong AtomicMax64Unsigned(ulong address, ulong value) =>
            ApplyDoublewordAtomic(address, value, static (previous, source) => Math.Max(previous, source));

        internal AtomicRetireOutcome ApplyResolvedRetireEffect(in AtomicRetireEffect effect)
        {
            byte[] rawValue = ReadMemory(effect.Address, effect.AccessSize);
            ulong previousValue = effect.AccessSize == 4
                ? BitConverter.ToUInt32(rawValue, 0)
                : BitConverter.ToUInt64(rawValue, 0);

            // Retire-time apply resolves reservation and memory visibility here.
            // Architectural register publication is returned to the caller as part of
            // AtomicRetireOutcome and is committed separately through the retire path.
            switch (effect.Opcode)
            {
                case IsaOpcodeValues.LR_W:
                case IsaOpcodeValues.LR_D:
                    AtomicReservationRegistry.RegisterReservation(
                        effect.CoreId,
                        effect.VirtualThreadId,
                        effect.Address,
                        effect.AccessSize);
                    return CreateOutcome(effect, previousValue, memoryMutated: false);

                case IsaOpcodeValues.SC_W:
                case IsaOpcodeValues.SC_D:
                {
                    bool reservationMatched = AtomicReservationRegistry.ConsumeReservation(
                        effect.CoreId,
                        effect.VirtualThreadId,
                        effect.Address,
                        effect.AccessSize);
                    if (!reservationMatched)
                    {
                        return AtomicRetireOutcome.Create(
                            effect,
                            registerWritebackValue: 1,
                            hasRegisterWriteback: effect.HasRegisterDestination,
                            memoryMutated: false);
                    }

                    WriteMemory(
                        effect.Address,
                        effect.SourceValue,
                        effect.AccessSize);
                    return AtomicRetireOutcome.Create(
                        effect,
                        registerWritebackValue: 0,
                        hasRegisterWriteback: effect.HasRegisterDestination,
                        memoryMutated: true);
                }

                default:
                {
                    ulong newValue = ComputeAmoWriteValue(
                        effect.Opcode,
                        previousValue,
                        effect.SourceValue,
                        effect.AccessSize);
                    WriteMemory(effect.Address, newValue, effect.AccessSize);
                    return CreateOutcome(effect, previousValue, memoryMutated: true);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void NotifyPhysicalWrite(ulong address, int byteCount)
        {
            AtomicReservationRegistry.InvalidateOverlapping(address, byteCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AtomicRetireOutcome CreateOutcome(
            in AtomicRetireEffect effect,
            ulong previousValue,
            bool memoryMutated)
        {
            ulong registerValue = effect.AccessSize == 4
                ? SignExtendWord(previousValue)
                : previousValue;
            return AtomicRetireOutcome.Create(
                effect,
                registerValue,
                effect.HasRegisterDestination,
                memoryMutated);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ValidateAccess(
            ushort opcode,
            ulong address,
            byte accessSize)
        {
            if ((address & (ulong)(accessSize - 1)) != 0)
            {
                throw new MemoryAlignmentException(address, accessSize, OpcodeRegistry.GetMnemonicOrHex(opcode));
            }

            ulong mainMemoryLength = (ulong)_mainMemory.Length;
            if (accessSize > mainMemoryLength || address > mainMemoryLength - accessSize)
            {
                throw new PageFaultException(address, IsWriteLikeOpcode(opcode));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte ResolveAccessSize(ushort opcode)
        {
            return opcode switch
            {
                IsaOpcodeValues.LR_W or
                IsaOpcodeValues.SC_W or
                IsaOpcodeValues.AMOSWAP_W or
                IsaOpcodeValues.AMOADD_W or
                IsaOpcodeValues.AMOXOR_W or
                IsaOpcodeValues.AMOAND_W or
                IsaOpcodeValues.AMOOR_W or
                IsaOpcodeValues.AMOMIN_W or
                IsaOpcodeValues.AMOMAX_W or
                IsaOpcodeValues.AMOMINU_W or
                IsaOpcodeValues.AMOMAXU_W => 4,
                _ => 8
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWriteLikeOpcode(ushort opcode)
        {
            return opcode is not (IsaOpcodeValues.LR_W or IsaOpcodeValues.LR_D);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LoadReserved32(ulong address, int coreId, int virtualThreadId)
        {
            ValidateAccess(IsaOpcodeValues.LR_W, address, accessSize: 4);
            AtomicReservationRegistry.RegisterReservation(coreId, virtualThreadId, address, accessSize: 4);
            return unchecked((int)BitConverter.ToUInt32(ReadMemory(address, accessSize: 4), 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int StoreConditional32(ulong address, int value, int coreId, int virtualThreadId)
        {
            ValidateAccess(IsaOpcodeValues.SC_W, address, accessSize: 4);
            if (!AtomicReservationRegistry.ConsumeReservation(coreId, virtualThreadId, address, accessSize: 4))
            {
                return 1;
            }

            WriteMemory(address, unchecked((uint)value), accessSize: 4);
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long LoadReserved64(ulong address, int coreId, int virtualThreadId)
        {
            ValidateAccess(IsaOpcodeValues.LR_D, address, accessSize: 8);
            AtomicReservationRegistry.RegisterReservation(coreId, virtualThreadId, address, accessSize: 8);
            return unchecked((long)BitConverter.ToUInt64(ReadMemory(address, accessSize: 8), 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long StoreConditional64(ulong address, long value, int coreId, int virtualThreadId)
        {
            ValidateAccess(IsaOpcodeValues.SC_D, address, accessSize: 8);
            if (!AtomicReservationRegistry.ConsumeReservation(coreId, virtualThreadId, address, accessSize: 8))
            {
                return 1;
            }

            WriteMemory(address, unchecked((ulong)value), accessSize: 8);
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ApplyWordAtomic(
            ulong address,
            uint value,
            Func<uint, uint, uint> operation)
        {
            ValidateAccess(IsaOpcodeValues.AMOSWAP_W, address, accessSize: 4);
            uint previousValue = BitConverter.ToUInt32(ReadMemory(address, accessSize: 4), 0);
            uint newValue = operation(previousValue, value);
            WriteMemory(address, newValue, accessSize: 4);
            return previousValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong ApplyDoublewordAtomic(
            ulong address,
            ulong value,
            Func<ulong, ulong, ulong> operation)
        {
            ValidateAccess(IsaOpcodeValues.AMOSWAP_D, address, accessSize: 8);
            ulong previousValue = BitConverter.ToUInt64(ReadMemory(address, accessSize: 8), 0);
            ulong newValue = operation(previousValue, value);
            WriteMemory(address, newValue, accessSize: 8);
            return previousValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] ReadMemory(ulong address, byte accessSize)
        {
            byte[] buffer = new byte[accessSize];
            if (!_mainMemory.TryReadPhysicalRange(address, buffer))
            {
                throw new IOException(
                    $"Atomic main-memory read failed at physical address 0x{address:X} for {accessSize} byte(s).");
            }

            return buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMemory(
            ulong address,
            ulong value,
            byte accessSize)
        {
            byte[] bytes = accessSize switch
            {
                4 => BitConverter.GetBytes((uint)value),
                8 => BitConverter.GetBytes(value),
                _ => throw new InvalidOperationException($"Unsupported atomic access size {accessSize}.")
            };
            if (!_mainMemory.TryWritePhysicalRange(address, bytes))
            {
                throw new IOException(
                    $"Atomic main-memory write failed at physical address 0x{address:X} for {accessSize} byte(s).");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ComputeAmoWriteValue(
            ushort opcode,
            ulong previousValue,
            ulong sourceValue,
            byte accessSize)
        {
            if (accessSize == 4)
            {
                uint previousWord = unchecked((uint)previousValue);
                uint sourceWord = unchecked((uint)sourceValue);
                return opcode switch
                {
                    IsaOpcodeValues.AMOSWAP_W => sourceWord,
                    IsaOpcodeValues.AMOADD_W => unchecked((uint)(previousWord + sourceWord)),
                    IsaOpcodeValues.AMOXOR_W => previousWord ^ sourceWord,
                    IsaOpcodeValues.AMOAND_W => previousWord & sourceWord,
                    IsaOpcodeValues.AMOOR_W => previousWord | sourceWord,
                    IsaOpcodeValues.AMOMIN_W => unchecked((uint)Math.Min((int)previousWord, (int)sourceWord)),
                    IsaOpcodeValues.AMOMAX_W => unchecked((uint)Math.Max((int)previousWord, (int)sourceWord)),
                    IsaOpcodeValues.AMOMINU_W => Math.Min(previousWord, sourceWord),
                    IsaOpcodeValues.AMOMAXU_W => Math.Max(previousWord, sourceWord),
                    _ => throw new InvalidOperationException($"Unsupported word atomic opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)}.")
                };
            }

            return opcode switch
            {
                IsaOpcodeValues.AMOSWAP_D => sourceValue,
                IsaOpcodeValues.AMOADD_D => unchecked(previousValue + sourceValue),
                IsaOpcodeValues.AMOXOR_D => previousValue ^ sourceValue,
                IsaOpcodeValues.AMOAND_D => previousValue & sourceValue,
                IsaOpcodeValues.AMOOR_D => previousValue | sourceValue,
                IsaOpcodeValues.AMOMIN_D => unchecked((ulong)Math.Min((long)previousValue, (long)sourceValue)),
                IsaOpcodeValues.AMOMAX_D => unchecked((ulong)Math.Max((long)previousValue, (long)sourceValue)),
                IsaOpcodeValues.AMOMINU_D => Math.Min(previousValue, sourceValue),
                IsaOpcodeValues.AMOMAXU_D => Math.Max(previousValue, sourceValue),
                _ => throw new InvalidOperationException($"Unsupported doubleword atomic opcode {OpcodeRegistry.GetMnemonicOrHex(opcode)}.")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SignExtendWord(ulong value)
        {
            return unchecked((ulong)(long)(int)(uint)value);
        }
    }
}
