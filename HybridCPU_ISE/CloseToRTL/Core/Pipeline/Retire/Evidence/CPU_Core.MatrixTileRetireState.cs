using System.Collections.Generic;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private ulong _nextMatrixTileCaptureOrdinal;
            private ulong _nextMatrixTileReplayCheckpointOrdinal;
            private Dictionary<ulong, MatrixTileReplayRollbackJournal>? _matrixTileReplayJournals;

            internal ulong AllocateMatrixTileCaptureOrdinal() =>
                checked(++_nextMatrixTileCaptureOrdinal);

            internal ulong AllocateMatrixTileReplayCheckpointOrdinal() =>
                checked(++_nextMatrixTileReplayCheckpointOrdinal);

            internal void RegisterMatrixTileReplayJournal(
                MatrixTileReplayRollbackJournal journal)
            {
                _matrixTileReplayJournals ??=
                    new Dictionary<ulong, MatrixTileReplayRollbackJournal>();
                ulong checkpointOrdinal = journal.ReplayIdentity.CheckpointOrdinal;
                if (!_matrixTileReplayJournals.TryAdd(checkpointOrdinal, journal))
                {
                    throw new MatrixTileReplayRollbackValidationException(
                        $"MTILE replay checkpoint ordinal {checkpointOrdinal} is already registered.");
                }
            }

            internal readonly bool OwnsMatrixTileReplayJournal(
                MatrixTileReplayRollbackJournal journal)
            {
                return _matrixTileReplayJournals != null &&
                       _matrixTileReplayJournals.TryGetValue(
                           journal.ReplayIdentity.CheckpointOrdinal,
                           out MatrixTileReplayRollbackJournal? registered) &&
                       ReferenceEquals(registered, journal);
            }

            internal void ReleaseMatrixTileReplayJournal(
                MatrixTileReplayRollbackJournal journal)
            {
                if (!OwnsMatrixTileReplayJournal(journal) ||
                    !_matrixTileReplayJournals!.Remove(
                        journal.ReplayIdentity.CheckpointOrdinal))
                {
                    throw new MatrixTileReplayRollbackValidationException(
                        "MTILE replay journal release requires the registered core-owned journal.");
                }
            }
        }
    }
}
