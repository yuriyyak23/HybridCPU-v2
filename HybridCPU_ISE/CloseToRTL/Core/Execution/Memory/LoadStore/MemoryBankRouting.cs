using System;
using System.Threading;

namespace YAKSys_Hybrid_CPU.Core.Memory
{
    /// <summary>
    /// Single authority for scheduler-visible memory-bank routing.
    /// Returns an explicit invalid bank contour when the runtime memory
    /// subsystem has not been materialized yet, and records that use so the
    /// uninitialized path cannot remain silent or masquerade as real bank truth.
    /// </summary>
    internal static class MemoryBankRouting
    {
        internal const int UninitializedSchedulerVisibleBankId = -1;
        private const int DefaultNumBanks = 16;
        private const int DefaultBankWidthBytes = 4096;
        private static long _schedulerVisibleUninitializedUseCount;

        internal static ulong SchedulerVisibleUninitializedUseCount =>
            (ulong)Interlocked.Read(ref _schedulerVisibleUninitializedUseCount);

        internal static bool IsResolvedSchedulerVisibleBankId(int bankId) => bankId >= 0;

        internal static void ResetTelemetryForTesting()
        {
            Interlocked.Exchange(ref _schedulerVisibleUninitializedUseCount, 0);
        }

        public static int ResolveSchedulerVisibleBankId(ulong address)
        {
            if (Processor.Memory is { NumBanks: > 0, BankWidthBytes: > 0 } memory)
            {
                return ResolveBankId(address, memory.BankWidthBytes, memory.NumBanks);
            }

            Interlocked.Increment(ref _schedulerVisibleUninitializedUseCount);
            return UninitializedSchedulerVisibleBankId;
        }

        public static int ResolveBankId(ulong address, int bankWidthBytes, int numBanks)
        {
            int resolvedBankWidthBytes = bankWidthBytes > 0
                ? bankWidthBytes
                : DefaultBankWidthBytes;
            int resolvedNumBanks = numBanks > 0
                ? numBanks
                : DefaultNumBanks;

            return (int)((address / (ulong)resolvedBankWidthBytes) % (ulong)resolvedNumBanks);
        }
    }
}
