using System;

namespace HybridCPU_ISE.Legacy
{
    public enum LegacyMachineStateReadFailureKind
    {
        MainMemoryUnavailable = 0,
        AddressOutOfRange = 1,
        PartialReadRequiresCompatibilityPadding = 2,
        ReadFault = 3,
    }

    public sealed class LegacyMachineStateReadException : InvalidOperationException
    {
        public ulong Address { get; }
        public int Length { get; }
        public long MemoryLength { get; }
        public LegacyMachineStateReadFailureKind FailureKind { get; }

        public LegacyMachineStateReadException(
            ulong address,
            int length,
            long memoryLength,
            LegacyMachineStateReadFailureKind failureKind,
            Exception? innerException = null)
            : base(BuildMessage(address, length, memoryLength, failureKind), innerException)
        {
            Address = address;
            Length = length;
            MemoryLength = memoryLength;
            FailureKind = failureKind;
        }

        private static string BuildMessage(
            ulong address,
            int length,
            long memoryLength,
            LegacyMachineStateReadFailureKind failureKind)
        {
            string rangeText = $"IOVA 0x{address:X} for {length} byte(s)";
            string memoryText = memoryLength >= 0
                ? $"main memory length 0x{memoryLength:X}"
                : "uninitialized main memory";

            return failureKind switch
            {
                LegacyMachineStateReadFailureKind.MainMemoryUnavailable =>
                    $"LegacyGlobal machine-state source cannot read {rangeText} because the runtime has no initialized main memory.",
                LegacyMachineStateReadFailureKind.AddressOutOfRange =>
                    $"LegacyGlobal machine-state source cannot read {rangeText} because the request starts beyond {memoryText}.",
                LegacyMachineStateReadFailureKind.PartialReadRequiresCompatibilityPadding =>
                    $"LegacyGlobal machine-state source requires explicit compatibility padding to read {rangeText} against {memoryText}. Use CreateLegacyGlobalCompat(...) when zero-filled tail bytes are intended.",
                _ =>
                    $"LegacyGlobal machine-state source failed to read {rangeText} from {memoryText}.",
            };
        }
    }
}
