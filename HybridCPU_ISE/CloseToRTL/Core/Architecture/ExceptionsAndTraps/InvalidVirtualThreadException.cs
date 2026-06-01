using System;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Thrown when live architectural state is addressed with a VT identifier outside
    /// the canonical 4-way SMT ownership range.
    /// </summary>
    public sealed class InvalidVirtualThreadException : Exception
    {
        public string Operation { get; }
        public int VtId { get; }
        public int VtCount { get; }
        public ExecutionFaultCategory Category => ExecutionFaultCategory.InvalidVirtualThread;

        public InvalidVirtualThreadException(
            string operation,
            int vtId,
            int vtCount)
            : base(
                ExecutionFaultContract.FormatMessage(
                    ExecutionFaultCategory.InvalidVirtualThread,
                    FormatDetail(operation, vtId, vtCount)))
        {
            Operation = NormalizeOperation(operation);
            VtId = vtId;
            VtCount = vtCount;
            ExecutionFaultContract.Stamp(this, Category);
        }

        private static string FormatDetail(
            string operation,
            int vtId,
            int vtCount)
        {
            string normalizedOperation = NormalizeOperation(operation);
            string validRange = vtCount > 0
                ? $"0..{vtCount - 1}"
                : "<none>";

            return
                $"{normalizedOperation} attempted to access invalid virtual thread {vtId}. " +
                $"Valid VT range is {validRange}.";
        }

        private static string NormalizeOperation(string operation)
            => string.IsNullOrWhiteSpace(operation)
                ? "UnknownStateOperation"
                : operation;
    }

    internal static class VirtualThreadIdResolver
    {
        public static int ResolveOrThrow(
            int vtId,
            int vtCount,
            string operation)
        {
            if (vtCount > 0 &&
                (uint)vtId < (uint)vtCount &&
                VtId.TryCreate(vtId, out VtId resolvedVtId))
            {
                return resolvedVtId.Value;
            }

            throw new InvalidVirtualThreadException(operation, vtId, vtCount);
        }
    }
}
