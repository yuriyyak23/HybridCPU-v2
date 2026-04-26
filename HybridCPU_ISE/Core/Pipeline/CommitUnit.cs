using System;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core.Pipeline
{
    /// <summary>
     /// CommitUnit emits typed retire records in program order.
     /// Architectural state is updated by <see cref="RetireCoordinator"/>.
     /// </summary>
    public sealed class CommitUnit
    {
        private readonly RetireCoordinator _retireCoordinator;

        /// <summary>
        /// Authoritative phase-04 factory for production commit flow.
        /// Architectural retirement is driven directly from the unified retire
        /// coordinator rather than from compatibility VT-bank shells.
        /// </summary>
        private CommitUnit(RetireCoordinator retireCoordinator) =>
            _retireCoordinator = retireCoordinator ?? throw new ArgumentNullException(nameof(retireCoordinator));

        public static CommitUnit FromRetireCoordinator(RetireCoordinator retireCoordinator) =>
            new(retireCoordinator);

        public void Commit(ReadOnlySpan<RetireRecord> records, byte vtId)
        {
            ValidateVirtualThreadId(vtId);

            for (int i = 0; i < records.Length; i++)
            {
                RetireRecord record = records[i];
                if (record.VtId != vtId)
                {
                    throw new InvalidOperationException(
                        $"CommitUnit retire batch for VT{vtId} cannot include record[{i}] owned by VT{record.VtId}.");
                }
            }

            _retireCoordinator.Retire(records);
        }

        private void ValidateVirtualThreadId(byte vtId)
        {
            if (vtId >= _retireCoordinator.VtCount)
            {
                int maxVtId = Math.Max(_retireCoordinator.VtCount - 1, 0);
                throw new ArgumentOutOfRangeException(
                    nameof(vtId),
                    vtId,
                    $"VT identifier {vtId} is outside the range [0, {maxVtId}].");
            }
        }
    }
}
