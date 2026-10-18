using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Banked Scratch Buffer Controller with crossbar interconnect.
    ///
    /// Models a 4-bank interleaved SRAM with 2R+1W access per cycle.
    /// Each bank is True Dual-Port BRAM (1R+1W simultaneous).
    /// Crossbar routes element addresses to banks by addr[1:0].
    ///
    /// <para><b>Conflict detection:</b>
    /// If two reads or a read+write hit the same bank in one cycle → 1-cycle stall.
    /// Stall model is cycle-accurate for timing simulation.</para>
    ///
    /// <para><b>Bank mapping (low-bit interleaving):</b></para>
    /// <list type="bullet">
    ///   <item>Address[1:0] = 00 → Bank 0</item>
    ///   <item>Address[1:0] = 01 → Bank 1</item>
    ///   <item>Address[1:0] = 10 → Bank 2</item>
    ///   <item>Address[1:0] = 11 → Bank 3</item>
    /// </list>
    ///
    /// <para><b>HLS:</b> 4 × BRAM18 (64 bytes each) + 4×3 crossbar mux (~60 LUT6).</para>
    /// </summary>
    public struct ScratchBankController
    {
        private const int BANK_COUNT = 4;
        private const int ELEMENTS_PER_BANK = 8;  // 32 elements / 4 banks
        private const int MAX_ELEMENT_SIZE = 8;    // 64-bit elements
        private const int BANK_SIZE = ELEMENTS_PER_BANK * MAX_ELEMENT_SIZE; // 64 bytes

        /// <summary>4 independent SRAM banks.</summary>
        private byte[][] _banks;

        /// <summary>True when <see cref="Initialize"/> has been called.</summary>
        public readonly bool IsInitialized => _banks != null;

        /// <summary>Bank conflicts detected (2+ accesses hit same bank in one cycle).</summary>
        public ulong BankConflicts { get; private set; }

        /// <summary>Total element accesses.</summary>
        public ulong TotalAccesses { get; private set; }

        /// <summary>Initialize banked storage (call once during core init).</summary>
        public void Initialize()
        {
            _banks = new byte[BANK_COUNT][];
            for (int i = 0; i < BANK_COUNT; i++)
                _banks[i] = new byte[BANK_SIZE];
            BankConflicts = 0;
            TotalAccesses = 0;
        }

        /// <summary>
        /// Map element index to (bank, byte offset) using low-bit interleaving.
        /// HLS: wire select, zero combinational logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int bank, int offset) MapAddress(int elementIndex, int elementSize)
        {
            int bank = elementIndex & (BANK_COUNT - 1);   // bits [1:0]
            int bankElement = elementIndex >> 2;           // bits [N:2]
            return (bank, bankElement * elementSize);
        }

        /// <summary>
        /// Read a single element from banked storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadElement(int elementIndex, int elementSize, Span<byte> dest)
        {
            var (bank, offset) = MapAddress(elementIndex, elementSize);
            _banks[bank].AsSpan(offset, elementSize).CopyTo(dest);
            TotalAccesses++;
        }

        /// <summary>
        /// Write a single element to banked storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteElement(int elementIndex, int elementSize, ReadOnlySpan<byte> src)
        {
            var (bank, offset) = MapAddress(elementIndex, elementSize);
            src.CopyTo(_banks[bank].AsSpan(offset, elementSize));
            TotalAccesses++;
        }

        /// <summary>
        /// Check if three simultaneous accesses (2R + 1W) have bank conflicts.
        /// Returns stall cycles: 0 = no conflict, 1 = one-cycle stall.
        ///
        /// HLS: 3 bank-index comparators → single-cycle combinational logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CheckConflict(int readAIndex, int readBIndex, int writeDstIndex)
        {
            int bankA = readAIndex & (BANK_COUNT - 1);
            int bankB = readBIndex & (BANK_COUNT - 1);
            int bankDst = writeDstIndex & (BANK_COUNT - 1);

            bool conflict = (bankA == bankB) || (bankA == bankDst) || (bankB == bankDst);
            if (conflict)
            {
                BankConflicts++;
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Execute a 2R+1W vector triple access with conflict detection.
        /// Reads elements from bank A and B, writes result to bank Dst.
        /// If conflict detected: data is still transferred (sequentialized),
        /// return value indicates stall penalty.
        /// </summary>
        /// <param name="readAElement">Element index for read operand A.</param>
        /// <param name="readBElement">Element index for read operand B.</param>
        /// <param name="writeDstElement">Element index for write destination.</param>
        /// <param name="elementSize">Element size in bytes.</param>
        /// <param name="destA">Buffer to receive operand A data.</param>
        /// <param name="destB">Buffer to receive operand B data.</param>
        /// <param name="srcDst">Source data to write to destination.</param>
        /// <returns>Number of stall cycles (0 or 1).</returns>
        public int ExecuteVectorTripleAccess(
            int readAElement, int readBElement, int writeDstElement,
            int elementSize,
            Span<byte> destA, Span<byte> destB, ReadOnlySpan<byte> srcDst)
        {
            int stallCycles = CheckConflict(readAElement, readBElement, writeDstElement);

            ReadElement(readAElement, elementSize, destA);
            ReadElement(readBElement, elementSize, destB);
            WriteElement(writeDstElement, elementSize, srcDst);

            return stallCycles;
        }

        /// <summary>
        /// Bulk load from contiguous external memory into banked scratch.
        /// Elements are distributed across banks by interleaving.
        /// </summary>
        /// <param name="source">Source memory span.</param>
        /// <param name="elementSize">Element size in bytes.</param>
        /// <param name="elementCount">Number of elements to load.</param>
        public void LoadFromMemory(ReadOnlySpan<byte> source, int elementSize, int elementCount)
        {
            for (int i = 0; i < elementCount && (i + 1) * elementSize <= source.Length; i++)
            {
                var element = source.Slice(i * elementSize, elementSize);
                WriteElement(i, elementSize, element);
            }
        }

        /// <summary>
        /// Bulk store from banked scratch to contiguous external memory.
        /// </summary>
        /// <param name="dest">Destination memory span.</param>
        /// <param name="elementSize">Element size in bytes.</param>
        /// <param name="elementCount">Number of elements to store.</param>
        public void StoreToMemory(Span<byte> dest, int elementSize, int elementCount)
        {
            for (int i = 0; i < elementCount && (i + 1) * elementSize <= dest.Length; i++)
            {
                var element = dest.Slice(i * elementSize, elementSize);
                ReadElement(i, elementSize, element);
            }
        }
    }
}
