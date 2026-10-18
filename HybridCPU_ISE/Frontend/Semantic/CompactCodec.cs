using System.IO.Compression;
using System.Text;

namespace HybridCpu.ShadowFrontend;

// v1 is a bounded lossless snapshot codec, not an opcode interpreter. All eight
// slots (including NOP bytes/policies) are retained. Brotli is a host prototype;
// it is not claimed to be an RTL one-cycle banked operand format.
public static class CompactCodec
{
    public const int MaxDecodedBytes = 131072;
    public const int MaxEncodedBytes = 131088;
    private const uint Magic = 0x31434244;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static byte[]? Encode(DecodeResult result)
    {
        if (result.Schema != 1 || result.Failure?.Code > 8) return null;
        // Bound BEFORE allocating the encoding buffer, including opaque extensions.
        try { if (BodySize(result) > MaxDecodedBytes) return null; }
        catch (EncoderFallbackException) { return null; } // Expected unsupported text contour, not a programming exception.
        using var body = new MemoryStream();
        using (var w = new BinaryWriter(body, Encoding.UTF8, true))
        {
            w.Write(result.IsSuccess); Blob(w, result.Raw);
            if (result.Failure is { } f)
            { w.Write(f.Code); w.Write(f.SlotIndex); Text(w, f.Field); Text(w, f.RawHash); Text(w, f.Message); }
            else
            {
                Snap(w, result.BundleSideband!);
                byte mask = 0;
                foreach (var s in result.Slots) if (s.IsOccupied) mask |= (byte)(1 << s.SlotIndex);
                w.Write(mask);
                foreach (var s in result.Slots)
                {
                    w.Write((byte)s.SlotIndex); w.Write(s.Opcode); Optional(w, s.InstructionClass);
                    Optional(w, s.SerializationClass); w.Write(s.Rd); w.Write(s.Rs1); w.Write(s.Rs2);
                    w.Write(s.Immediate); Optional(w, s.CsrAddress); w.Write(s.AcquireOrdering); w.Write(s.ReleaseOrdering);
                    Snap(w, s.RawSlot); Snap(w, s.InstructionPayload); Snap(w, s.SlotSideband);
                    Snap(w, s.StaticBinding); Snap(w, s.ScalarLoadAddressPlan);
                }
            }
        }
        if (body.Length > MaxDecodedBytes) return null;
        using var output = new MemoryStream();
        using (var h = new BinaryWriter(output, Encoding.UTF8, true))
        { h.Write(Magic); h.Write((ushort)1); h.Write((ushort)0); h.Write((int)body.Length); }
        using (var compressor = new BrotliStream(output, CompressionLevel.SmallestSize, true))
            compressor.Write(body.GetBuffer().AsSpan(0, (int)body.Length));
        return output.Length <= MaxEncodedBytes ? output.ToArray() : null;
    }
    public static bool TryDecode(ReadOnlySpan<byte> encoded, out DecodeResult? result)
    {
        result = null;
        if (encoded.Length is < 12 or > MaxEncodedBytes) return false;
        using var input = new MemoryStream(encoded.ToArray(), false);
        using var reader = new BinaryReader(input);
        if (reader.ReadUInt32() != Magic || reader.ReadUInt16() != 1 || reader.ReadUInt16() != 0) return false;
        int size = reader.ReadInt32();
        if (size is < 261 or > MaxDecodedBytes) return false;
        // BrotliDecoder exposes exact consumed/written counts: reject trailing streams,
        // truncation and expansion bombs without unbounded allocation.
        byte[] body = new byte[size];
        using var decoder = new BrotliDecoder();
        var status = decoder.Decompress(encoded[12..], body, out int used, out int written);
        if (status != System.Buffers.OperationStatus.Done || used != encoded.Length - 12 || written != size) return false;
        try
        {
            using var stream = new MemoryStream(body, false);
            using var r = new BinaryReader(stream, new UTF8Encoding(false, true));
            bool success = Boolean(r); var raw = ReadBlob(r);
            if (raw.Length != 256) return false;
            if (!success)
            {
                ushort code = r.ReadUInt16(); int slot = r.ReadInt32();
                if (code > 8 || slot is < -1 or > 7) return false;
                result = DecodeResult.Illegal(raw, new(code, slot, ReadText(r), ReadText(r), ReadText(r)));
            }
            else
            {
                Snapshot sideband = ReadSnap(r); byte mask = r.ReadByte(); var slots = new SemanticSlot[8];
                for (int i = 0; i < 8; i++)
                {
                    if (r.ReadByte() != i) return false;
                    uint opcode = r.ReadUInt32(); int? cls = ReadOptional(r), ser = ReadOptional(r);
                    byte rd = r.ReadByte(), rs1 = r.ReadByte(), rs2 = r.ReadByte(); long imm = r.ReadInt64();
                    int? csr = ReadOptional(r); if (csr is < 0 or > ushort.MaxValue) return false;
                    bool aq = Boolean(r), rl = Boolean(r);
                    slots[i] = new(i, (mask & (1 << i)) != 0, opcode, cls, ser, rd, rs1, rs2, imm,
                        (ushort?)csr, aq, rl, ReadSnap(r), ReadSnap(r), ReadSnap(r), ReadSnap(r), ReadSnap(r));
                    if (slots[i].RawSlot.Content.Length != 32 ||
                        !slots[i].RawSlot.Content.Span.SequenceEqual(raw.Span.Slice(i * 32, 32))) return false;
                }
                result = DecodeResult.Success(raw, slots, sideband);
            }
            if (stream.Position != stream.Length) { result = null; return false; }
            return true;
        }
        catch (EndOfStreamException) { result = null; return false; }
        catch (InvalidDataException) { result = null; return false; }
        catch (DecoderFallbackException) { result = null; return false; }
    }
    private static bool Boolean(BinaryReader r) => r.ReadByte() switch { 0 => false, 1 => true, _ => throw new InvalidDataException() };
    private static long TextSize(string value) => 4L + StrictUtf8.GetByteCount(value);
    private static long SnapshotSize(Snapshot value) => TextSize(value.Kind) + 4L + value.Content.Length;
    private static long BodySize(DecodeResult value)
    {
        long size = 1L + 4 + value.Raw.Length;
        if (value.Failure is { } f) return size + 6 + TextSize(f.Field) + TextSize(f.RawHash) + TextSize(f.Message);
        size += SnapshotSize(value.BundleSideband!) + 1;
        foreach (var s in value.Slots)
            size += 21 + (s.InstructionClass.HasValue ? 4 : 0) + (s.SerializationClass.HasValue ? 4 : 0) +
                (s.CsrAddress.HasValue ? 4 : 0) + SnapshotSize(s.RawSlot) + SnapshotSize(s.InstructionPayload) +
                SnapshotSize(s.SlotSideband) + SnapshotSize(s.StaticBinding) + SnapshotSize(s.ScalarLoadAddressPlan);
        return size;
    }
    private static void Optional(BinaryWriter w, int? v) { w.Write(v.HasValue); if (v.HasValue) w.Write(v.Value); }
    private static int? ReadOptional(BinaryReader r) => Boolean(r) ? r.ReadInt32() : null;
    private static void Blob(BinaryWriter w, FrozenBytes b) { w.Write(b.Length); w.Write(b.Span); }
    private static FrozenBytes ReadBlob(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > MaxDecodedBytes || n > r.BaseStream.Length - r.BaseStream.Position) throw new InvalidDataException();
        return new(r.ReadBytes(n));
    }
    private static void Text(BinaryWriter w, string s) => Blob(w, new(StrictUtf8.GetBytes(s)));
    private static string ReadText(BinaryReader r) => new UTF8Encoding(false, true).GetString(ReadBlob(r).Span);
    private static void Snap(BinaryWriter w, Snapshot s) { Text(w, s.Kind); Blob(w, s.Content); }
    private static Snapshot ReadSnap(BinaryReader r) => new(ReadText(r), ReadBlob(r));
}
