using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private Processor.MainMemoryArea? _mainMemory;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static Processor.MainMemoryArea CaptureDefaultMainMemory() => Processor.MainMemory;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Processor.MainMemoryArea GetBoundMainMemory() =>
                _mainMemory ??= CaptureDefaultMainMemory();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ulong GetBoundMainMemoryLength() => (ulong)GetBoundMainMemory().Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool HasExactBoundMainMemoryRange(ulong address, int size)
            {
                if (size <= 0)
                {
                    return false;
                }

                ulong memoryLength = GetBoundMainMemoryLength();
                ulong requestSize = (ulong)size;
                return requestSize <= memoryLength &&
                       address <= memoryLength - requestSize;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ThrowIfBoundMainMemoryRangeUnavailable(
                ulong address,
                int size,
                string executionSurface)
            {
                if (HasExactBoundMainMemoryRange(address, size))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"{executionSurface} reached synchronous MainMemory fallback at 0x{address:X} for {size} byte(s) without a fully materializable in-range surface. " +
                    "The authoritative fallback must fail closed instead of decoding partial bytes, reusing zeros, or reporting a boundary-crossing memory success.");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private byte[] ReadBoundMainMemory(
                ulong address,
                byte[] buffer,
                byte accessSize,
                string operation)
            {
                ArgumentNullException.ThrowIfNull(buffer);

                if (buffer.Length < accessSize)
                {
                    throw new ArgumentException(
                        $"Bound main-memory read buffer was smaller than the requested access size {accessSize}.",
                        nameof(buffer));
                }

                if (!GetBoundMainMemory().TryReadPhysicalRange(address, buffer.AsSpan(0, accessSize)))
                {
                    throw new IOException(
                        $"{operation} failed to read {accessSize} byte(s) from bound main memory at physical address 0x{address:X}.");
                }

                return buffer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void ReadBoundMainMemoryExact(
                ulong address,
                byte[] buffer,
                string executionSurface)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                ThrowIfBoundMainMemoryRangeUnavailable(address, buffer.Length, executionSurface);
                ReadBoundMainMemory(address, buffer, checked((byte)buffer.Length), executionSurface);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteBoundMainMemory(
                ulong address,
                byte[] buffer,
                string operation)
            {
                ArgumentNullException.ThrowIfNull(buffer);

                if (!GetBoundMainMemory().TryWritePhysicalRange(address, buffer))
                {
                    throw new IOException(
                        $"{operation} failed to write {buffer.Length} byte(s) to bound main memory at physical address 0x{address:X}.");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void WriteBoundMainMemoryExact(
                ulong address,
                byte[] buffer,
                string executionSurface)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                ThrowIfBoundMainMemoryRangeUnavailable(address, buffer.Length, executionSurface);
                WriteBoundMainMemory(address, buffer, executionSurface);
            }
        }
    }
}
