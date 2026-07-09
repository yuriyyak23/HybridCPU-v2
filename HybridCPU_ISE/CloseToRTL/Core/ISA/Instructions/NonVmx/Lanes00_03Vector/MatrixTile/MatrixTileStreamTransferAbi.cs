using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileStreamTransferDirection : byte
{
    None = 0,
    MemoryIngress = 1,
    TileEgress = 2,
}

public readonly record struct MatrixTileStreamWindowIdentity(
    ushort Row,
    ushort Window,
    ulong Address,
    uint ByteCount,
    ulong DataFingerprint);

public readonly record struct MatrixTileStreamTransferRecord(
    uint CoreId,
    int OwnerThreadId,
    uint Opcode,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileRuntimeResourceClass ResourceClass,
    SlotClass SlotClass,
    byte PhysicalLaneId,
    byte StreamEngineChannel,
    MatrixTileStreamTransferDirection Direction,
    ushort ExpectedRows,
    MatrixTileStreamWindowIdentity[] Windows,
    ulong TotalBytes,
    ulong TransferFingerprint,
    bool Completed)
{
    public bool IsEmpty =>
        ResourceClass == MatrixTileRuntimeResourceClass.None &&
        (Windows == null || Windows.Length == 0) &&
        TransferFingerprint == 0;

    public bool IsTypedTransport =>
        ResourceClass == MatrixTileRuntimeResourceClass.MatrixTileMemory &&
        SlotClass == SlotClass.MatrixTileStreamClass &&
        PhysicalLaneId == MatrixTileResourceContour.TileStreamLaneId &&
        StreamEngineChannel == MatrixTileResourceContour.StreamEngineChannel &&
        Direction != MatrixTileStreamTransferDirection.None &&
        TransferFingerprint != 0;

    public bool PublishesArchitecturalTileState => false;

    public bool MutatesMemoryBeforeRetire => false;

    public bool UsesDmaStreamComputeAuthority => false;

    public bool UsesGenericStreamExecutionAuthority => false;

    public bool UsesHostOwnedArchitecturalEvidence => false;

    public MatrixTileStreamTransferRecord DeepClone() =>
        this with
        {
            Windows = Windows is null
                ? Array.Empty<MatrixTileStreamWindowIdentity>()
                : (MatrixTileStreamWindowIdentity[])Windows.Clone()
        };
}

public sealed class MatrixTileStreamTransferSession
{
    private const int MaxSrfWindowBytes = 256;

    private readonly uint _coreId;
    private readonly int _ownerThreadId;
    private readonly uint _opcode;
    private readonly MatrixTileProjectedOperationKind _operationKind;
    private readonly MatrixTileStreamTransferDirection _direction;
    private readonly ushort _expectedRows;
    private readonly StreamRegisterFile _srf;
    private readonly Func<ulong, int, byte[]?>? _memoryReader;
    private readonly List<MatrixTileStreamWindowIdentity> _windows = new();
    private ushort _nextIngressRow;
    private ulong _totalBytes;
    private bool _transportFailed;

    internal MatrixTileStreamTransferSession(
        uint coreId,
        int ownerThreadId,
        uint opcode,
        MatrixTileProjectedOperationKind operationKind,
        MatrixTileStreamTransferDirection direction,
        ushort expectedRows,
        StreamRegisterFile srf,
        Func<ulong, int, byte[]?>? memoryReader)
    {
        if (ownerThreadId is < 0 or >= Processor.CPU_Core.SmtWays)
        {
            throw new ArgumentOutOfRangeException(nameof(ownerThreadId));
        }

        if (!MatrixTileResourceContour.IsMatrixTileMemoryOpcode(opcode) ||
            operationKind is not (
                MatrixTileProjectedOperationKind.Load or
                MatrixTileProjectedOperationKind.Store))
        {
            throw new InvalidOperationException(
                "MatrixTile stream transfer requires an MTILE_LOAD or MTILE_STORE identity.");
        }

        _coreId = coreId;
        _ownerThreadId = ownerThreadId;
        _opcode = opcode;
        _operationKind = operationKind;
        _direction = direction;
        _expectedRows = expectedRows;
        _srf = srf ?? throw new ArgumentNullException(nameof(srf));
        _memoryReader = memoryReader;
    }

    public byte[]? ReadIngress(ulong address, int length)
    {
        if (_operationKind != MatrixTileProjectedOperationKind.Load ||
            _direction != MatrixTileStreamTransferDirection.MemoryIngress ||
            _memoryReader == null ||
            length <= 0)
        {
            _transportFailed = true;
            return null;
        }

        byte[] row = new byte[length];
        if (!TryStageWindows(
                _nextIngressRow,
                address,
                row,
                readFromMemory: true))
        {
            _transportFailed = true;
            return null;
        }

        _nextIngressRow++;
        return row;
    }

    public MatrixTileTileImage StageEgress(
        MatrixTileTileImage sourceSnapshot,
        MatrixTileMemoryShapeValidationResult? memoryValidation)
    {
        if (_operationKind != MatrixTileProjectedOperationKind.Store ||
            _direction != MatrixTileStreamTransferDirection.TileEgress ||
            !sourceSnapshot.IsCanonicalPacked ||
            !memoryValidation.HasValue ||
            !memoryValidation.Value.IsMemoryShapeAbiAccepted)
        {
            _transportFailed = true;
            return sourceSnapshot;
        }

        MatrixTileCanonicalDescriptorAbi descriptor = sourceSnapshot.Descriptor;
        int rowByteCount = checked(descriptor.Columns * descriptor.ElementSizeBytes);
        byte[] staged = new byte[sourceSnapshot.Data.Length];
        for (ushort row = 0; row < descriptor.Rows; row++)
        {
            int packedOffset = checked(row * rowByteCount);
            byte[] rowData = sourceSnapshot.Data.AsSpan(packedOffset, rowByteCount).ToArray();
            ulong address = checked(
                memoryValidation.Value.FirstByteAddress +
                ((ulong)row * descriptor.StrideBytes));
            if (!TryStageWindows(row, address, rowData, readFromMemory: false))
            {
                _transportFailed = true;
                return default;
            }

            rowData.CopyTo(staged, packedOffset);
        }

        return MatrixTileTileImage.Create(
            sourceSnapshot.TileId,
            sourceSnapshot.Descriptor,
            staged);
    }

    public MatrixTileStreamTransferRecord Complete(bool captureHasFault)
    {
        MatrixTileStreamWindowIdentity[] windows = _windows.ToArray();
        bool completed =
            !captureHasFault &&
            !_transportFailed &&
            CountCompletedRows(windows) == _expectedRows;
        ulong fingerprint = ComputeFingerprint(
            _coreId,
            _ownerThreadId,
            _opcode,
            _operationKind,
            _direction,
            _expectedRows,
            windows,
            _totalBytes,
            completed);

        return new MatrixTileStreamTransferRecord(
            _coreId,
            _ownerThreadId,
            _opcode,
            _operationKind,
            MatrixTileRuntimeResourceClass.MatrixTileMemory,
            SlotClass.MatrixTileStreamClass,
            MatrixTileResourceContour.TileStreamLaneId,
            MatrixTileResourceContour.StreamEngineChannel,
            _direction,
            _expectedRows,
            windows,
            _totalBytes,
            fingerprint,
            completed);
    }

    private bool TryStageWindows(
        ushort row,
        ulong rowAddress,
        byte[] rowData,
        bool readFromMemory)
    {
        int copied = 0;
        ushort window = 0;
        while (copied < rowData.Length)
        {
            int chunkLength = Math.Min(MaxSrfWindowBytes, rowData.Length - copied);
            ulong chunkAddress = checked(rowAddress + (ulong)copied);
            byte[] source;
            if (readFromMemory)
            {
                source = _memoryReader!(chunkAddress, chunkLength) ?? Array.Empty<byte>();
                if (source.Length != chunkLength)
                {
                    return false;
                }
            }
            else
            {
                source = rowData.AsSpan(copied, chunkLength).ToArray();
            }

            int registerIndex = _srf.AllocateRegister(
                chunkAddress,
                elementSize: 1,
                checked((uint)chunkLength));
            if (registerIndex < 0 ||
                !_srf.LoadRegister(registerIndex, source.AsSpan()))
            {
                return false;
            }

            Span<byte> stagedChunk = rowData.AsSpan(copied, chunkLength);
            if (!_srf.TryReadPrefetchedChunk(
                    chunkAddress,
                    elementSize: 1,
                    checked((uint)chunkLength),
                    stagedChunk))
            {
                return false;
            }

            _windows.Add(new MatrixTileStreamWindowIdentity(
                row,
                window,
                chunkAddress,
                checked((uint)chunkLength),
                ComputeDataFingerprint(stagedChunk)));
            _totalBytes += checked((uint)chunkLength);
            copied += chunkLength;
            window++;
        }

        return true;
    }

    private static int CountCompletedRows(
        MatrixTileStreamWindowIdentity[] windows)
    {
        if (windows.Length == 0)
        {
            return 0;
        }

        int rows = 1;
        ushort lastRow = windows[0].Row;
        for (int index = 1; index < windows.Length; index++)
        {
            if (windows[index].Row != lastRow)
            {
                rows++;
                lastRow = windows[index].Row;
            }
        }

        return rows;
    }

    private static ulong ComputeDataFingerprint(ReadOnlySpan<byte> bytes)
    {
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < bytes.Length; index++)
        {
            hash ^= bytes[index];
            hash *= 1099511628211UL;
        }

        return hash == 0 ? 1UL : hash;
    }

    private static ulong ComputeFingerprint(
        uint coreId,
        int ownerThreadId,
        uint opcode,
        MatrixTileProjectedOperationKind operationKind,
        MatrixTileStreamTransferDirection direction,
        ushort expectedRows,
        MatrixTileStreamWindowIdentity[] windows,
        ulong totalBytes,
        bool completed)
    {
        ulong hash = 14695981039346656037UL;
        Add(ref hash, coreId);
        Add(ref hash, unchecked((uint)ownerThreadId));
        Add(ref hash, opcode);
        Add(ref hash, (byte)operationKind);
        Add(ref hash, (byte)direction);
        Add(ref hash, expectedRows);
        Add(ref hash, totalBytes);
        Add(ref hash, completed ? (byte)1 : (byte)0);
        for (int index = 0; index < windows.Length; index++)
        {
            MatrixTileStreamWindowIdentity window = windows[index];
            Add(ref hash, window.Row);
            Add(ref hash, window.Window);
            Add(ref hash, window.Address);
            Add(ref hash, window.ByteCount);
            Add(ref hash, window.DataFingerprint);
        }

        return hash == 0 ? 1UL : hash;
    }

    private static void Add(ref ulong hash, byte value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static void Add(ref ulong hash, ushort value)
    {
        Add(ref hash, (byte)value);
        Add(ref hash, (byte)(value >> 8));
    }

    private static void Add(ref ulong hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            Add(ref hash, (byte)(value >> shift));
        }
    }

    private static void Add(ref ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            Add(ref hash, (byte)(value >> shift));
        }
    }
}

public static class MatrixTileStreamTransferAbi
{
    public const string TransferDecision = "TypedMatrixTileStreamEngineSrfTransport";
    public const string AuthorityDecision = "TransportOnlyRetireRemainsArchitecturalAuthority";
    public const string DscBoundaryDecision = "NoDmaStreamComputeDescriptorTokenOrQueueAuthority";
    public const string StoreBoundaryDecision = "NoMemoryMutationBeforeRetireCommit";

    public static MatrixTileStreamTransferSession BeginLoad(
        ref Processor.CPU_Core core,
        int ownerThreadId,
        uint opcode,
        ushort expectedRows)
    {
        StreamRegisterFile srf = core.GetMatrixTileStreamRegisterFile();
        Processor.CPU_Core coreCopy = core;
        return new MatrixTileStreamTransferSession(
            core.CoreID,
            ownerThreadId,
            opcode,
            MatrixTileProjectedOperationKind.Load,
            MatrixTileStreamTransferDirection.MemoryIngress,
            expectedRows,
            srf,
            (address, length) =>
            {
                byte[] buffer = new byte[length];
                return coreCopy.TryReadMatrixTileMemoryExact(address, buffer)
                    ? buffer
                    : null;
            });
    }

    public static MatrixTileStreamTransferSession BeginStore(
        ref Processor.CPU_Core core,
        int ownerThreadId,
        uint opcode,
        ushort expectedRows)
    {
        return new MatrixTileStreamTransferSession(
            core.CoreID,
            ownerThreadId,
            opcode,
            MatrixTileProjectedOperationKind.Store,
            MatrixTileStreamTransferDirection.TileEgress,
            expectedRows,
            core.GetMatrixTileStreamRegisterFile(),
            memoryReader: null);
    }

    internal static void ValidateCapture(
        in MatrixTileExecutionCaptureRecord capture)
    {
        MatrixTileStreamTransferRecord transfer = capture.StreamTransfer;
        bool memoryOperation = capture.OperationKind is
            MatrixTileProjectedOperationKind.Load or
            MatrixTileProjectedOperationKind.Store;
        if (!memoryOperation)
        {
            if (!transfer.IsEmpty)
            {
                throw new MatrixTileRetireValidationException(
                    $"{capture.Mnemonic} compute capture cannot carry MatrixTile stream ownership.");
            }

            return;
        }

        bool faultedBeforeTransport =
            capture.HasFault &&
            transfer.IsEmpty &&
            capture.MemoryFaultKind != MatrixTileMemoryFaultKind.PartialMemoryFault;
        if (faultedBeforeTransport)
        {
            return;
        }

        if (!transfer.IsTypedTransport ||
            transfer.OwnerThreadId != capture.CaptureIdentity.OwnerThreadId ||
            transfer.CoreId != capture.CaptureIdentity.CoreId ||
            transfer.Opcode != capture.CaptureIdentity.Opcode ||
            transfer.OperationKind != capture.OperationKind)
        {
            throw new MatrixTileRetireValidationException(
                $"{capture.Mnemonic} retire rejected missing or mismatched typed MatrixTile stream transfer identity.");
        }

        MatrixTileStreamTransferDirection expectedDirection =
            capture.OperationKind == MatrixTileProjectedOperationKind.Load
                ? MatrixTileStreamTransferDirection.MemoryIngress
                : MatrixTileStreamTransferDirection.TileEgress;
        if (transfer.Direction != expectedDirection ||
            (!capture.HasFault && !transfer.Completed) ||
            transfer.PublishesArchitecturalTileState ||
            transfer.MutatesMemoryBeforeRetire ||
            transfer.UsesDmaStreamComputeAuthority ||
            transfer.UsesGenericStreamExecutionAuthority ||
            transfer.UsesHostOwnedArchitecturalEvidence)
        {
            throw new MatrixTileRetireValidationException(
                $"{capture.Mnemonic} retire rejected an invalid MatrixTile stream transport envelope.");
        }
    }
}
