using System.Buffers.Binary;

internal static class ObservationFrameTransport
{
    public const int MaximumPayloadBytes = 1024 * 1024;

    // Wire format: unsigned little-endian byte length followed by exactly that payload.
    // The caller supplies its bounded request/session deadline through cancellation.
    public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (length == 0 || length > MaximumPayloadBytes)
            throw new InvalidDataException("Observation frame length is outside 1..1048576 bytes.");
        byte[] payload = new byte[(int)length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    // Only one owner may write a connection stream. Queue limits are enforced by that owner.
    public static async Task WriteAsync(Stream stream, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        if (payload.Length == 0 || payload.Length > MaximumPayloadBytes)
            throw new InvalidDataException("Observation frame length is outside 1..1048576 bytes.");
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }
}
