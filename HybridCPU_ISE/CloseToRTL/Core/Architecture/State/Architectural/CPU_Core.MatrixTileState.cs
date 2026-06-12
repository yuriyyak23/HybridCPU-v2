using System;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private MatrixTileArchitecturalTileRegisterFile? _matrixTileRegisterFile;
            private StreamRegisterFile? _matrixTileStreamRegisterFile;
            private ulong _matrixTileStreamInvalidationCount;

            internal MatrixTileArchitecturalTileRegisterFile GetMatrixTileRegisterFile() =>
                _matrixTileRegisterFile ??= new MatrixTileArchitecturalTileRegisterFile();

            internal StreamRegisterFile GetMatrixTileStreamRegisterFile() =>
                _matrixTileStreamRegisterFile ??= new StreamRegisterFile();

            internal ulong MatrixTileStreamInvalidationCount =>
                _matrixTileStreamInvalidationCount;

            internal void SeedMatrixTileForRuntime(
                int ownerThreadId,
                ushort tileId,
                MatrixTileCanonicalDescriptorAbi descriptor,
                ReadOnlySpan<byte> packedData)
            {
                GetMatrixTileRegisterFile().WriteTileForRuntimeSeed(
                    ownerThreadId,
                    tileId,
                    descriptor,
                    packedData);
            }

            internal bool TryCaptureMatrixTileSnapshot(
                int ownerThreadId,
                ushort tileId,
                MatrixTileCanonicalDescriptorAbi expectedDescriptor,
                out MatrixTileTileImage snapshot)
            {
                return GetMatrixTileRegisterFile().TryCaptureSnapshot(
                    ownerThreadId,
                    tileId,
                    expectedDescriptor,
                    out snapshot);
            }

            internal bool TryCaptureAnyMatrixTileSnapshot(
                int ownerThreadId,
                ushort tileId,
                out MatrixTileTileImage snapshot)
            {
                return GetMatrixTileRegisterFile().TryCaptureAnySnapshot(
                    ownerThreadId,
                    tileId,
                    out snapshot);
            }

            internal void PublishRetiredMatrixTile(
                int ownerThreadId,
                MatrixTileTileImage image)
            {
                if (ownerThreadId is < 0 or >= SmtWays)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(ownerThreadId),
                        ownerThreadId,
                        $"MTILE retire owner must be in the range [0, {SmtWays - 1}].");
                }

                if (!image.IsCanonicalPacked)
                {
                    throw new InvalidOperationException(
                        "MTILE retire publication requires a canonical packed tile image.");
                }

                GetMatrixTileRegisterFile().WriteTileForRuntimeSeed(
                    ownerThreadId,
                    image.TileId,
                    image.Descriptor,
                    image.Data);
            }

            internal void RestoreRetiredMatrixTileCheckpoint(
                int ownerThreadId,
                ushort tileId,
                bool hadTileCheckpoint,
                MatrixTileTileImage tileCheckpoint)
            {
                if (hadTileCheckpoint)
                {
                    if (!tileCheckpoint.IsCanonicalPacked ||
                        tileCheckpoint.TileId != tileId)
                    {
                        throw new InvalidOperationException(
                            "MTILE rollback requires the exact canonical pre-retire tile checkpoint.");
                    }

                    PublishRetiredMatrixTile(ownerThreadId, tileCheckpoint);
                    return;
                }

                GetMatrixTileRegisterFile().RemoveTile(ownerThreadId, tileId);
            }

            internal bool TryReadMatrixTileMemoryExact(
                ulong address,
                byte[] buffer)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                return HasExactBoundMainMemoryRange(address, buffer.Length) &&
                       GetBoundMainMemory().TryReadPhysicalRange(address, buffer);
            }

            internal bool TryCommitRetiredMatrixTileStoreAllOrNone(
                MatrixTileCapturedMemoryWrite[] writes,
                out string failureMessage)
            {
                ArgumentNullException.ThrowIfNull(writes);
                if (!TryCaptureMatrixTileStoreRollbackImage(
                        writes,
                        out MatrixTileCapturedMemoryWrite[] backups,
                        out failureMessage))
                {
                    return false;
                }

                for (int index = 0; index < writes.Length; index++)
                {
                    MatrixTileCapturedMemoryWrite write = writes[index];
                    if (GetBoundMainMemory().TryWritePhysicalRange(write.Address, write.Data))
                    {
                        continue;
                    }

                    for (int rollbackIndex = index; rollbackIndex >= 0; rollbackIndex--)
                    {
                        MatrixTileCapturedMemoryWrite backup = backups[rollbackIndex];
                        if (!GetBoundMainMemory().TryWritePhysicalRange(
                                backup.Address,
                                backup.Data))
                        {
                            throw new InvalidOperationException(
                                $"MTILE_STORE retire rollback failed for row {backup.Row} at 0x{backup.Address:X}; all-or-none architectural visibility cannot be preserved.");
                        }
                    }

                    failureMessage =
                        $"MTILE_STORE retire rolled back all staged rows after row {write.Row} commit failed.";
                    InvalidateMatrixTileStreamWindows(writes);
                    return false;
                }

                InvalidateMatrixTileStreamWindows(writes);
                return true;
            }

            internal bool TryCaptureMatrixTileStoreRollbackImage(
                MatrixTileCapturedMemoryWrite[] writes,
                out MatrixTileCapturedMemoryWrite[] backups,
                out string failureMessage)
            {
                ArgumentNullException.ThrowIfNull(writes);
                failureMessage = string.Empty;
                backups = new MatrixTileCapturedMemoryWrite[writes.Length];
                for (int index = 0; index < writes.Length; index++)
                {
                    MatrixTileCapturedMemoryWrite write = writes[index];
                    if (write.Data == null ||
                        !HasExactBoundMainMemoryRange(write.Address, write.Data.Length))
                    {
                        failureMessage =
                            $"MTILE_STORE checkpoint preflight rejected row {write.Row} at 0x{write.Address:X}.";
                        backups = Array.Empty<MatrixTileCapturedMemoryWrite>();
                        return false;
                    }

                    byte[] backup = new byte[write.Data.Length];
                    if (!GetBoundMainMemory().TryReadPhysicalRange(write.Address, backup))
                    {
                        failureMessage =
                            $"MTILE_STORE checkpoint could not snapshot row {write.Row}.";
                        backups = Array.Empty<MatrixTileCapturedMemoryWrite>();
                        return false;
                    }

                    backups[index] = new MatrixTileCapturedMemoryWrite(
                        write.Row,
                        write.Address,
                        backup);
                }

                return true;
            }

            internal bool MatrixTileMemoryRowsMatch(
                MatrixTileCapturedMemoryWrite[] expectedRows)
            {
                ArgumentNullException.ThrowIfNull(expectedRows);
                for (int index = 0; index < expectedRows.Length; index++)
                {
                    MatrixTileCapturedMemoryWrite expected = expectedRows[index];
                    if (expected.Data == null ||
                        !HasExactBoundMainMemoryRange(
                            expected.Address,
                            expected.Data.Length))
                    {
                        return false;
                    }

                    byte[] current = new byte[expected.Data.Length];
                    if (!GetBoundMainMemory().TryReadPhysicalRange(
                            expected.Address,
                            current) ||
                        !current.AsSpan().SequenceEqual(expected.Data))
                    {
                        return false;
                    }
                }

                return true;
            }

            internal void RestoreRetiredMatrixTileStoreAllOrNone(
                MatrixTileCapturedMemoryWrite[] checkpointRows)
            {
                ArgumentNullException.ThrowIfNull(checkpointRows);
                if (!TryCaptureMatrixTileStoreRollbackImage(
                        checkpointRows,
                        out MatrixTileCapturedMemoryWrite[] currentRows,
                        out string failureMessage))
                {
                    throw new InvalidOperationException(failureMessage);
                }

                for (int index = 0; index < checkpointRows.Length; index++)
                {
                    MatrixTileCapturedMemoryWrite checkpoint = checkpointRows[index];
                    if (GetBoundMainMemory().TryWritePhysicalRange(
                            checkpoint.Address,
                            checkpoint.Data))
                    {
                        continue;
                    }

                    for (int rollbackIndex = index; rollbackIndex >= 0; rollbackIndex--)
                    {
                        MatrixTileCapturedMemoryWrite current =
                            currentRows[rollbackIndex];
                        if (!GetBoundMainMemory().TryWritePhysicalRange(
                                current.Address,
                                current.Data))
                        {
                            throw new InvalidOperationException(
                                $"MTILE_STORE replay rollback recovery failed for row {current.Row} at 0x{current.Address:X}.");
                        }
                    }

                    throw new InvalidOperationException(
                        $"MTILE_STORE rollback failed for row {checkpoint.Row} at 0x{checkpoint.Address:X}; the original committed image was restored.");
                }

                InvalidateMatrixTileStreamWindows(checkpointRows);
            }

            private void InvalidateMatrixTileStreamWindows(
                MatrixTileCapturedMemoryWrite[] rows)
            {
                StreamRegisterFile srf = GetMatrixTileStreamRegisterFile();
                for (int index = 0; index < rows.Length; index++)
                {
                    MatrixTileCapturedMemoryWrite row = rows[index];
                    if (row.Data == null)
                    {
                        continue;
                    }

                    _matrixTileStreamInvalidationCount += checked((ulong)
                        srf.InvalidateOverlappingRangeAndCount(
                            row.Address,
                            checked((ulong)row.Data.Length)));
                }
            }
        }
    }
}
