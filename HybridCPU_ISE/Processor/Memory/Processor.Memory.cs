using HybridCPU_ISE.Arch;
using System;
using System.Collections.Generic;
using System.IO;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        // Multi-bank memory for parallel access (4 banks x 64MB = 256MB total)
        // Using MultiBankMemoryArea instead of MainMemoryArea for improved throughput
        public static MainMemoryArea MainMemory = new MultiBankMemoryArea(bankCount: 4, bankSize: 0x4000000UL);

        public class StackMemory : MainMemoryArea
        {
        }

        public class MainMemoryArea : System.IO.Stream
        {
            protected MemoryStream SystemMemory = new MemoryStream();
            private readonly Dictionary<ulong, VliwBundleAnnotations> _vliwBundleAnnotationsByAddress = new();

            [ThreadStatic]
            protected static bool _isPhysicalAccessContext = false;

            private ulong _currentVirtualPosition;
            private const ulong CPU_DEVICE_ID = 0; // Assuming CPU uses Device ID 0 for IOMMU interactions

            public MainMemoryArea()
            {
                // Initialize SystemMemory or other properties if needed
            }

            /// <summary>
            /// Read a 64-bit word directly from host physical memory, bypassing IOMMU.
            /// Used by hardware page table walkers (EPT, nested paging) that operate
            /// on host physical addresses and must not recurse through translation.
            /// </summary>
            /// <param name="hostPhysicalAddress">Host physical address to read from.</param>
            /// <returns>64-bit value at the given HPA, or 0 if out of range.</returns>
            public virtual ulong ReadPhysicalWord(ulong hostPhysicalAddress)
            {
                lock (this)
                {
                    if ((long)hostPhysicalAddress + 8 > SystemMemory.Length)
                        return 0;

                    SystemMemory.Position = (long)hostPhysicalAddress;
                    Span<byte> buf = stackalloc byte[8];
                    int read = SystemMemory.Read(buf);
                    if (read < 8) return 0;
                    return BitConverter.ToUInt64(buf);
                }
            }

            public virtual bool TryReadPhysicalRange(ulong physicalAddress, Span<byte> buffer)
            {
                if (buffer.Length == 0)
                {
                    return true;
                }

                bool originalContext = _isPhysicalAccessContext;
                _isPhysicalAccessContext = true;
                try
                {
                    ResetPhysicalSilentSquashTrackingIfNeeded();
                    Position = (long)physicalAddress;
                    int bytesRead = Read(buffer);
                    return bytesRead == buffer.Length &&
                           !ConsumePhysicalSilentSquashTrackingIfNeeded();
                }
                catch
                {
                    return false;
                }
                finally
                {
                    _isPhysicalAccessContext = originalContext;
                }
            }

            public virtual bool TryWritePhysicalRange(ulong physicalAddress, ReadOnlySpan<byte> buffer)
            {
                if (buffer.Length == 0)
                {
                    return true;
                }

                bool originalContext = _isPhysicalAccessContext;
                _isPhysicalAccessContext = true;
                try
                {
                    ResetPhysicalSilentSquashTrackingIfNeeded();
                    Position = (long)physicalAddress;
                    Write(buffer);
                    return !ConsumePhysicalSilentSquashTrackingIfNeeded();
                }
                catch
                {
                    return false;
                }
                finally
                {
                    _isPhysicalAccessContext = originalContext;
                }
            }

            public override long Position
            {
                get
                {
                    if (_isPhysicalAccessContext)
                    {
                        return SystemMemory.Position;
                    }
                    return (long)_currentVirtualPosition;
                }
                set
                {
                    if (_isPhysicalAccessContext)
                    {
                        SystemMemory.Position = value;
                    }
                    else
                    {
                        _currentVirtualPosition = (ulong)value;
                    }
                }
            }

            public override long Length
            {
                get
                {
                    // Assuming Length refers to the underlying physical memory size
                    // or the total size of the virtually mapped region for CPU_DEVICE_ID
                    // which for simplicity we tie to SystemMemory.Length.
                    // A more complex IOMMU model might have virtual lengths independent of physical.
                    lock (this)
                    {
                        return SystemMemory.Length;
                    }
                }
            }

            public virtual int Capacity // Made virtual if derived classes need different logic
            {
                get
                {
                    lock (this)
                    {
                        return SystemMemory.Capacity;
                    }
                }
                set
                {
                    lock (this)
                    {
                        SystemMemory.Capacity = value;
                    }
                }
            }

            public override bool CanRead
            {
                get
                {
                    // If in physical context, depends on SystemMemory.
                    // If in virtual context, depends on IOMMU mapping for CPU_DEVICE_ID.
                    // For simplicity, assume true if SystemMemory can be read.
                    lock (this)
                    {
                        return SystemMemory.CanRead;
                    }
                }
            }

            public override bool CanWrite
            {
                get
                {
                    lock (this)
                    {
                        return SystemMemory.CanWrite;
                    }
                }
            }

            public override bool CanSeek
            {
                get
                {
                    // Seeking is on the virtual position if not in physical context.
                    return true;
                }
            }

            public override void SetLength(long value)
            {
                // Operates on the physical SystemMemory.
                // IOMMU mappings would need to be aware of physical size changes.
                lock (this)
                {
                    SystemMemory.SetLength(value);
                }
            }

            public new void CopyTo(System.IO.Stream destination, int bufferSize)
            {
                // Overload provided by MemoryStream, implementing for Stream.
                // This needs to read through IOMMU if it's a virtual copy.
                if (destination == null) throw new ArgumentNullException(nameof(destination));
                if (!CanRead) throw new NotSupportedException("Stream does not support reading.");
                if (!destination.CanWrite) throw new NotSupportedException("Destination stream does not support writing.");
                if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

                byte[] buffer = new byte[bufferSize];
                int bytesRead;
                while ((bytesRead = Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, bytesRead);
                }
            }

            public byte[] ToArray()
            {
                // This should ideally read the entire mapped virtual space for CPU_DEVICE_ID.
                // For simplicity, if we assume CPU_DEVICE_ID has a 1:1 mapping to physical,
                // then returning SystemMemory.ToArray() is a physical dump.
                // A true virtual ToArray would be much more complex.
                lock (this)
                {
                    // If a virtual ToArray is needed, it would involve iterating through
                    // YAKSys_Hybrid_CPU.Memory.IOMMU.DMARead calls for the mapped range.
                    // This simplified version returns the physical memory content.
                    return SystemMemory.ToArray();
                }
            }

            public override void Flush()
            {
                lock (this)
                {
                    SystemMemory.Flush();
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (_isPhysicalAccessContext)
                {
                    return SystemMemory.Seek(offset, origin);
                }
                else
                {
                    ulong newVirtualPosition = _currentVirtualPosition;
                    switch (origin)
                    {
                        case SeekOrigin.Begin:
                            newVirtualPosition = (ulong)offset;
                            break;
                        case SeekOrigin.Current:
                            newVirtualPosition = (ulong)((long)_currentVirtualPosition + offset);
                            break;
                        case SeekOrigin.End:
                            // Length here refers to the virtual length, which we've tied to physical SystemMemory.Length.
                            newVirtualPosition = (ulong)((long)this.Length + offset);
                            break;
                    }
                    if (newVirtualPosition < 0) throw new IOException("Seek before beginning of stream."); // Should be ulong, always >=0
                    _currentVirtualPosition = newVirtualPosition;
                    return (long)_currentVirtualPosition;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");
                if (count == 0) return 0;

                if (_isPhysicalAccessContext)
                {
                    // Direct physical read (called by YAKSys_Hybrid_CPU.Memory.IOMMU.DMARead after setting physical position)
                    return SystemMemory.Read(buffer, offset, count);
                }
                else
                {
                    // Virtual read using IOMMU for CPU_DEVICE_ID
                    bool originalContext = _isPhysicalAccessContext;
                    _isPhysicalAccessContext = true;
                    try
                    {
                        // YAKSys_Hybrid_CPU.Memory.IOMMU.DMARead will set this.Position to physical and call this.Read again (which will hit the physical path)
                        int bytesRead = (int)YAKSys_Hybrid_CPU.Memory.IOMMU.DMARead(CPU_DEVICE_ID, _currentVirtualPosition, buffer, (ulong)offset, (ulong)count);
                        if (bytesRead == count)
                        {
                            _currentVirtualPosition += (ulong)bytesRead;
                            return bytesRead;
                        }

                        if (bytesRead == -1)
                        {
                            throw new IOException("IOMMU DMARead failed in virtual MainMemory.Read(...).");
                        }

                        throw CreateIncompleteVirtualReadException(_currentVirtualPosition, count, bytesRead);
                    }
                    finally
                    {
                        _isPhysicalAccessContext = originalContext;
                    }
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");

                if (_isPhysicalAccessContext)
                {
                    // Direct physical write (called by YAKSys_Hybrid_CPU.Memory.IOMMU.DMAWrite after setting physical position)
                    ulong physicalAddress = (ulong)Position;
                    SystemMemory.Write(buffer, offset, count);
                    YAKSys_Hybrid_CPU.Core.Memory.MainMemoryAtomicMemoryUnit.NotifyPhysicalWrite(
                        physicalAddress,
                        count);
                }
                else
                {
                    // Virtual write using IOMMU for CPU_DEVICE_ID
                    ulong ioVirtualAddress = _currentVirtualPosition;
                    bool originalContext = _isPhysicalAccessContext;
                    _isPhysicalAccessContext = true;
                    try
                    {
                        // YAKSys_Hybrid_CPU.Memory.IOMMU.DMAWrite will set this.Position to physical and call this.Write again (physical path)
                        long bytesWritten = (long)YAKSys_Hybrid_CPU.Memory.IOMMU.DMAWrite(CPU_DEVICE_ID, _currentVirtualPosition, buffer, (ulong)offset, (ulong)count);
                        if (bytesWritten == count)
                        {
                            _currentVirtualPosition += (ulong)bytesWritten;
                            InvalidateVliwBundleAnnotationsForRange(ioVirtualAddress, count);
                        }
                        else
                        {
                            throw CreateIncompleteVirtualWriteException(_currentVirtualPosition, count, bytesWritten);
                        }
                    }
                    finally
                    {
                        _isPhysicalAccessContext = originalContext;
                    }
                }
            }

            public override int ReadByte()
            {
                if (_isPhysicalAccessContext)
                {
                    return SystemMemory.ReadByte();
                }
                else
                {
                    byte[] buffer = new byte[1];
                    int bytesRead = this.Read(buffer, 0, 1); // Uses the IOMMU-aware Read
                    if (bytesRead == 0) return -1; // End of stream or error
                    return buffer[0];
                }
            }

            public override void WriteByte(byte value)
            {
                if (_isPhysicalAccessContext)
                {
                    ulong physicalAddress = (ulong)Position;
                    SystemMemory.WriteByte(value);
                    YAKSys_Hybrid_CPU.Core.Memory.MainMemoryAtomicMemoryUnit.NotifyPhysicalWrite(
                        physicalAddress,
                        1);
                }
                else
                {
                    byte[] buffer = new byte[1] { value };
                    this.Write(buffer, 0, 1); // Uses the IOMMU-aware Write
                }
            }

            // --- Custom methods from original class ---

            public byte[] ReadFromPosition(byte[] byteArray_Buffer, ulong ioVirtualAddress, ulong length)
            {
                // This method uses an explicit IOVA, so it should use IOMMU directly.
                // The _isPhysicalAccessContext flag is for the Stream methods that use implicit _currentVirtualPosition.
                if (length == 0) return new byte[0];
                if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(length), "Length exceeds int.MaxValue for DMARead.");

                if (byteArray_Buffer == null || (ulong)byteArray_Buffer.Length < length)
                {
                    byteArray_Buffer = new byte[length];
                }

                // Temporarily set context for IOMMU's call to Processor.MainMemory.Read/Write
                bool originalContext = _isPhysicalAccessContext;
                _isPhysicalAccessContext = true;
                long bytesRead;
                try
                {
                    bytesRead = (long)YAKSys_Hybrid_CPU.Memory.IOMMU.DMARead(CPU_DEVICE_ID, ioVirtualAddress, byteArray_Buffer, 0, (ulong)length);
                }
                finally
                {
                    _isPhysicalAccessContext = originalContext;
                }

                if (bytesRead == -1)
                {
                    throw new IOException("IOMMU DMARead failed in ReadFromPosition.");
                }

                if ((ulong)bytesRead != length)
                {
                    throw CreateIncompleteReadFromPositionException(ioVirtualAddress, (int)length, bytesRead);
                }
                return byteArray_Buffer;
            }

            public virtual void PublishVliwBundleAnnotations(
                ulong ioVirtualAddress,
                VliwBundleAnnotations bundleAnnotations)
            {
                ArgumentNullException.ThrowIfNull(bundleAnnotations);

                lock (_vliwBundleAnnotationsByAddress)
                {
                    _vliwBundleAnnotationsByAddress[ioVirtualAddress] = bundleAnnotations;
                }
            }

            public virtual bool TryReadVliwBundleAnnotations(
                ulong ioVirtualAddress,
                out VliwBundleAnnotations? bundleAnnotations)
            {
                lock (_vliwBundleAnnotationsByAddress)
                {
                    if (_vliwBundleAnnotationsByAddress.TryGetValue(
                        ioVirtualAddress,
                        out VliwBundleAnnotations? resolvedAnnotations))
                    {
                        bundleAnnotations = resolvedAnnotations;
                        return true;
                    }
                }

                bundleAnnotations = null;
                return false;
            }

            public virtual void ClearVliwBundleAnnotations(ulong ioVirtualAddress)
            {
                lock (_vliwBundleAnnotationsByAddress)
                {
                    _vliwBundleAnnotationsByAddress.Remove(ioVirtualAddress);
                }
            }

            public void WriteToPosition(byte[] value, ulong ioVirtualAddress)
            {
                // This method uses an explicit IOVA.
                if (value == null || value.Length == 0)
                {
                    return;
                }

                // Temporarily set context for IOMMU's call to Processor.MainMemory.Read/Write
                bool originalContext = _isPhysicalAccessContext;
                _isPhysicalAccessContext = true;
                int bytesWritten;
                try
                {
                    bytesWritten = (int)YAKSys_Hybrid_CPU.Memory.IOMMU.DMAWrite(CPU_DEVICE_ID, ioVirtualAddress, value, 0, (ulong)value.Length);
                }
                finally
                {
                    _isPhysicalAccessContext = originalContext;
                }

                if (bytesWritten == -1)
                {
                    throw new IOException("IOMMU DMAWrite failed in WriteToPosition.");
                }
                if ((ulong)bytesWritten != (ulong)value.Length)
                {
                    throw CreateIncompleteWriteToPositionException(ioVirtualAddress, value.Length, bytesWritten);
                }

                InvalidateVliwBundleAnnotationsForRange(ioVirtualAddress, value.Length);
            }

            private static IOException CreateIncompleteVirtualReadException(
                ulong ioVirtualAddress,
                int requestedBytes,
                int completedBytes)
            {
                return new IOException(
                    $"Virtual MainMemory.Read(...) at IOVA 0x{ioVirtualAddress:X} requested {requestedBytes} byte(s) but materialized {completedBytes} byte(s). " +
                    "Virtual reads must fail closed instead of advancing position or returning a partial/silently squashed memory image.");
            }

            private static IOException CreateIncompleteReadFromPositionException(
                ulong ioVirtualAddress,
                int requestedBytes,
                long completedBytes)
            {
                return new IOException(
                    $"ReadFromPosition(...) at IOVA 0x{ioVirtualAddress:X} requested {requestedBytes} byte(s) but materialized {completedBytes} byte(s). " +
                    "Explicit-position reads must fail closed instead of returning a truncated or silently squashed memory image.");
            }

            private static IOException CreateIncompleteVirtualWriteException(
                ulong ioVirtualAddress,
                int requestedBytes,
                long completedBytes)
            {
                return new IOException(
                    $"Virtual MainMemory write at IOVA 0x{ioVirtualAddress:X} requested {requestedBytes} byte(s) but materialized {completedBytes} byte(s). " +
                    "Direct virtual write surfaces must fail closed instead of reporting success after a silently squashed or partially materialized physical write.");
            }

            private static IOException CreateIncompleteWriteToPositionException(
                ulong ioVirtualAddress,
                int requestedBytes,
                int completedBytes)
            {
                return new IOException(
                    $"WriteToPosition(...) at IOVA 0x{ioVirtualAddress:X} requested {requestedBytes} byte(s) but materialized {completedBytes} byte(s). " +
                    "Explicit-position helper writes must fail closed instead of reporting success after a silently squashed or partially materialized physical write.");
            }

            private static void ResetPhysicalSilentSquashTrackingIfNeeded()
            {
                if (Processor.MainMemory is global::YAKSys_Hybrid_CPU.MultiBankMemoryArea)
                {
                    global::YAKSys_Hybrid_CPU.MultiBankMemoryArea.ResetLastAccessSilentSquashFlag();
                }
            }

            private static bool ConsumePhysicalSilentSquashTrackingIfNeeded()
            {
                return Processor.MainMemory is global::YAKSys_Hybrid_CPU.MultiBankMemoryArea &&
                       global::YAKSys_Hybrid_CPU.MultiBankMemoryArea.ConsumeLastAccessSilentSquashFlag();
            }

            public void AllocateMemory(ulong physicalPosition, ulong length)
            {
                // This is a physical memory operation, directly interacts with SystemMemory.
                // IOMMU would then be used to map IOVAs to this physical space.
                lock (this)
                {
                    ulong requiredLength = physicalPosition + length;
                    if ((ulong)SystemMemory.Length < requiredLength)
                    {
                        SystemMemory.SetLength((long)requiredLength);
                    }
                }
            }

            private void InvalidateVliwBundleAnnotationsForRange(
                ulong ioVirtualAddress,
                int byteCount)
            {
                if (byteCount <= 0)
                {
                    return;
                }

                ulong rangeEndExclusive = unchecked(ioVirtualAddress + (ulong)byteCount);
                List<ulong>? staleBundleAddresses = null;

                lock (_vliwBundleAnnotationsByAddress)
                {
                    foreach (KeyValuePair<ulong, VliwBundleAnnotations> entry in _vliwBundleAnnotationsByAddress)
                    {
                        ulong bundleAddress = entry.Key;
                        ulong bundleEndExclusive = unchecked(bundleAddress + 256UL);
                        if (bundleAddress < rangeEndExclusive && ioVirtualAddress < bundleEndExclusive)
                        {
                            staleBundleAddresses ??= new List<ulong>();
                            staleBundleAddresses.Add(bundleAddress);
                        }
                    }

                    if (staleBundleAddresses == null)
                    {
                        return;
                    }

                    for (int index = 0; index < staleBundleAddresses.Count; index++)
                    {
                        _vliwBundleAnnotationsByAddress.Remove(staleBundleAddresses[index]);
                    }
                }
            }
        }

        /// <summary>
        /// Multi-bank memory area for parallel access and improved throughput.
        /// Divides physical memory into multiple banks that can be accessed concurrently.
        /// </summary>
        public class MultiBankMemoryArea : MainMemoryArea
        {
            private readonly MemoryStream[] _banks;
            private readonly int _bankCount;
            private readonly ulong _bankSize; // Size of each bank in bytes

            /// <summary>
            /// Create a multi-bank memory area.
            /// </summary>
            /// <param name="bankCount">Number of memory banks (e.g., 4)</param>
            /// <param name="bankSize">Size of each bank in bytes (e.g., 64MB = 0x4000000)</param>
            public MultiBankMemoryArea(int bankCount, ulong bankSize)
            {
                _bankCount = bankCount;
                _bankSize = bankSize;
                _banks = new MemoryStream[_bankCount];

                // Initialize each bank with its own memory stream
                for (int i = 0; i < _bankCount; i++)
                {
                    _banks[i] = new MemoryStream(new byte[bankSize]);
                }
            }

            /// <summary>
            /// Select the appropriate bank and calculate offset for a physical address.
            /// Banks are interleaved based on address: addr % bankSize determines offset,
            /// (addr / bankSize) % bankCount determines bank index.
            /// </summary>
            private (MemoryStream bank, ulong offset) GetBank(ulong physicalAddress)
            {
                int bankIndex = (int)((physicalAddress / _bankSize) % (ulong)_bankCount);
                ulong offset = physicalAddress % _bankSize;
                return (_banks[bankIndex], offset);
            }

            /// <summary>
            /// Override Read to use bank selection for physical access.
            /// Virtual access still goes through IOMMU.
            /// </summary>
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");

                if (_isPhysicalAccessContext)
                {
                    // Physical read: use bank selection
                    var (bank, bankOffset) = GetBank((ulong)Position);
                    bank.Position = (long)bankOffset;
                    return bank.Read(buffer, offset, count);
                }
                else
                {
                    // Virtual read: use IOMMU (inherited behavior)
                    return base.Read(buffer, offset, count);
                }
            }

            /// <summary>
            /// Override Write to use bank selection for physical access.
            /// Virtual access still goes through IOMMU.
            /// </summary>
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (buffer.Length - offset < count) throw new ArgumentException("Invalid offset length.");

                if (_isPhysicalAccessContext)
                {
                    // Physical write: use bank selection
                    ulong physicalAddress = (ulong)Position;
                    var (bank, bankOffset) = GetBank((ulong)Position);
                    bank.Position = (long)bankOffset;
                    bank.Write(buffer, offset, count);
                    YAKSys_Hybrid_CPU.Core.Memory.MainMemoryAtomicMemoryUnit.NotifyPhysicalWrite(
                        physicalAddress,
                        count);
                }
                else
                {
                    // Virtual write: use IOMMU (inherited behavior)
                    base.Write(buffer, offset, count);
                }
            }

            /// <summary>
            /// Override Length to return total capacity across all banks.
            /// </summary>
            public override long Length
            {
                get
                {
                    return (long)(_bankSize * (ulong)_bankCount);
                }
            }

            /// <summary>
            /// Get bank count for diagnostic purposes.
            /// </summary>
            public int BankCount => _bankCount;

            /// <summary>
            /// Get size of each bank for diagnostic purposes.
            /// </summary>
            public ulong BankSize => _bankSize;
        }
    }
}
