using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Versioned binary canonical form for D2 v2 governance artifacts.
///
/// Envelope: ASCII "HCPUVD2\0", artifact-kind u8, encoding-version u16 BE.
/// Every field is emitted in the fixed order below as tag u16 BE, type u8,
/// payload-length u32 BE, payload. Integers and enum values are big-endian;
/// text is length-delimited UTF-8; SHA-1/SHA-256 values are raw 20/32 bytes.
/// Collections are sorted by their declared identity key before encoding.
/// </summary>
internal static class VirtualizationDecisionCanonicalEncoderV2
{
    internal const ushort EncodingVersion = 1;

    private const byte SpecArtifact = 1;
    private const byte AcceptanceArtifact = 2;
    private const byte RevocationArtifact = 3;
    private const byte SupersessionArtifact = 4;

    internal static ImmutableArray<byte> EncodeSpecPayload(VirtualizationDecisionSpecV2 spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var writer = new CanonicalWriter(SpecArtifact);
        WriteSpecFields(writer, spec);
        return writer.ToImmutable();
    }

    internal static ImmutableArray<byte> EncodeSpec(VirtualizationDecisionSpecV2 spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var writer = new CanonicalWriter(SpecArtifact);
        WriteSpecFields(writer, spec);
        writer.WriteSha256(44, spec.SpecDigest);
        return writer.ToImmutable();
    }

    internal static string ComputeSpecDigest(VirtualizationDecisionSpecV2 spec) =>
        ComputeSha256Hex(EncodeSpecPayload(spec));

    internal static ImmutableArray<byte> EncodeAcceptancePayload(
        VirtualizationDecisionAcceptanceRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(AcceptanceArtifact);
        WriteAcceptanceFields(writer, record);
        return writer.ToImmutable();
    }

    internal static ImmutableArray<byte> EncodeAcceptance(
        VirtualizationDecisionAcceptanceRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(AcceptanceArtifact);
        WriteAcceptanceFields(writer, record);
        writer.WriteSha256(14, record.AcceptanceDigest);
        return writer.ToImmutable();
    }

    internal static string ComputeAcceptanceDigest(
        VirtualizationDecisionAcceptanceRecordV2 record) =>
        ComputeSha256Hex(EncodeAcceptancePayload(record));

    internal static ImmutableArray<byte> EncodeRevocationPayload(
        VirtualizationDecisionRevocationRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(RevocationArtifact);
        WriteRevocationFields(writer, record);
        return writer.ToImmutable();
    }

    internal static ImmutableArray<byte> EncodeRevocation(
        VirtualizationDecisionRevocationRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(RevocationArtifact);
        WriteRevocationFields(writer, record);
        writer.WriteSha256(10, record.RevocationDigest);
        return writer.ToImmutable();
    }

    internal static string ComputeRevocationDigest(
        VirtualizationDecisionRevocationRecordV2 record) =>
        ComputeSha256Hex(EncodeRevocationPayload(record));

    internal static ImmutableArray<byte> EncodeSupersessionPayload(
        VirtualizationDecisionSupersessionRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(SupersessionArtifact);
        WriteSupersessionFields(writer, record);
        return writer.ToImmutable();
    }

    internal static ImmutableArray<byte> EncodeSupersession(
        VirtualizationDecisionSupersessionRecordV2 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var writer = new CanonicalWriter(SupersessionArtifact);
        WriteSupersessionFields(writer, record);
        writer.WriteSha256(11, record.SupersessionDigest);
        return writer.ToImmutable();
    }

    internal static string ComputeSupersessionDigest(
        VirtualizationDecisionSupersessionRecordV2 record) =>
        ComputeSha256Hex(EncodeSupersessionPayload(record));

    internal static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsCanonicalCommitSha(string? value) =>
        value is { Length: 40 } &&
        value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void WriteSpecFields(CanonicalWriter writer, VirtualizationDecisionSpecV2 spec)
    {
        writer.WriteUInt32(1, spec.SchemaVersion);
        writer.WriteString(2, spec.DecisionId);
        writer.WriteString(3, spec.OperationNamespace);
        writer.WriteUInt16(4, spec.LeafWidth);
        writer.WriteUInt16(5, spec.InvalidLeaf);
        writer.WriteUInt16(6, spec.NumericLeaf);
        writer.WriteString(7, spec.OperationId);
        writer.WriteEnum(8, spec.OwnerClass);
        writer.WriteUInt64(9, spec.OwnerId);
        writer.WriteUInt32(10, spec.OwnerPolicyVersion);
        writer.WriteUInt32(11, spec.OwnerEpoch);
        writer.WriteUInt32(12, spec.OperandAbiVersion);
        writer.WriteString(13, spec.Rs1Contract);
        writer.WriteString(14, spec.Rs2Contract);
        writer.WriteString(15, spec.RdContract);
        writer.WriteEnum(16, spec.ResultAbi);
        writer.WriteEnum(17, spec.EffectClass);
        writer.WriteEnum(18, spec.CapabilityRequirement);
        writer.WriteUInt64(19, spec.CapabilityMask);
        writer.WriteBoolean(20, spec.RequiresTypedGrant);
        writer.WriteEnum(21, spec.DelegationPolicy);
        writer.WriteEnum(22, spec.RevocationPolicy);
        writer.WriteEnum(23, spec.CapabilityMigrationClass);
        writer.WriteEnum(24, spec.EvidenceVisibility);
        writer.WriteEnum(25, spec.FrontendProjectionPolicy);
        writer.WriteEnum(26, spec.ExecutionEvidenceRequirement);
        writer.WriteEnum(27, spec.DomainRequirement);
        writer.WriteBoolean(28, spec.RequireNonZeroDomainTag);
        writer.WriteBoolean(29, spec.RequiresMemoryDomain);
        writer.WriteBoolean(30, spec.RequiresIoDomain);
        writer.WriteEnum(31, spec.AddressSpaceRequirement);
        writer.WriteEnum(32, spec.SecureDomainPolicy);
        writer.WriteEnum(33, spec.CancellationPolicy);
        writer.WriteEnum(34, spec.ReplayPolicy);
        writer.WriteEnum(35, spec.OperationMigrationPolicy);
        writer.WriteEnum(36, spec.CompletionEvidenceClass);
        writer.WriteEnum(37, spec.CompletionMigrationClass);
        writer.WriteEnum(38, spec.CompletionProjectionPolicy);
        writer.WriteEnum(39, spec.CompletionPolicy);
        writer.WriteEnum(40, spec.RetirePolicy);
        writer.WriteEnum(41, spec.AdjacentLeafPolicy);
        writer.WriteEnum(42, spec.CrossNamespacePolicy);
        writer.WriteOwnerMap(43, spec.OwnerMap);

        // Phase 38 predates projection profiles. Keeping this suffix absent for
        // its default values preserves the already accepted canonical bytes.
        bool hasProjectionProfile =
            spec.OperationClass != VirtualizationDecisionOperationClassV2.Unspecified ||
            spec.AuthorityPlane != VirtualizationDecisionAuthorityPlaneV2.Unspecified ||
            spec.ExactFieldIds is not null ||
            spec.MutationClass != VirtualizationDecisionMutationClassV2.Unspecified ||
            !string.IsNullOrEmpty(spec.DependencyContract) ||
            spec.VmcsMetadataOnly ||
            spec.RequiresConformanceProof;
        if (hasProjectionProfile)
        {
            writer.WriteEnum(45, spec.OperationClass);
            writer.WriteEnum(46, spec.AuthorityPlane);
            writer.WriteUInt16Collection(47, spec.ExactFieldIds ?? []);
            writer.WriteEnum(48, spec.MutationClass);
            writer.WriteString(49, spec.DependencyContract);
            writer.WriteBoolean(50, spec.VmcsMetadataOnly);
            writer.WriteBoolean(51, spec.RequiresConformanceProof);
        }
    }

    private static void WriteAcceptanceFields(
        CanonicalWriter writer,
        VirtualizationDecisionAcceptanceRecordV2 record)
    {
        writer.WriteUInt32(1, record.SchemaVersion);
        writer.WriteString(2, record.DecisionId);
        writer.WriteSha256(3, record.SpecDigest);
        writer.WriteCommitSha(4, record.SpecCommitSha);
        writer.WriteEnum(5, record.AcceptanceState);
        writer.WriteString(6, record.AcceptedBy);
        writer.WriteUInt32(7, record.AcceptancePolicyVersion);
        writer.WriteReviewEvidence(8, record.OwnerReviewEvidence);
        writer.WriteReviewEvidence(9, record.ArchitectureReviewEvidence);
        writer.WriteCommitSha(10, record.CodeOwnersBlobSha);
        writer.WriteOptionalString(11, record.SupersedesDecisionId);
        writer.WriteOptionalSha256(12, record.SupersedesAcceptanceDigest);
    }

    private static void WriteRevocationFields(
        CanonicalWriter writer,
        VirtualizationDecisionRevocationRecordV2 record)
    {
        writer.WriteUInt32(1, record.SchemaVersion);
        writer.WriteString(2, record.RevocationId);
        writer.WriteString(3, record.DecisionId);
        writer.WriteSha256(4, record.AcceptanceDigest);
        writer.WriteEnum(5, record.State);
        writer.WriteString(6, record.RevokedBy);
        writer.WriteString(7, record.Reason);
        writer.WriteUInt64(8, record.Sequence);
    }

    private static void WriteSupersessionFields(
        CanonicalWriter writer,
        VirtualizationDecisionSupersessionRecordV2 record)
    {
        writer.WriteUInt32(1, record.SchemaVersion);
        writer.WriteString(2, record.SupersessionId);
        writer.WriteString(3, record.SupersededDecisionId);
        writer.WriteSha256(4, record.SupersededAcceptanceDigest);
        writer.WriteString(5, record.SupersedingDecisionId);
        writer.WriteSha256(6, record.SupersedingAcceptanceDigest);
        writer.WriteEnum(7, record.State);
        writer.WriteString(8, record.SupersededBy);
        writer.WriteUInt64(9, record.Sequence);
    }

    private static string ComputeSha256Hex(ImmutableArray<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes.AsSpan())).ToLowerInvariant();

    private sealed class CanonicalWriter
    {
        private const byte TypeUInt16 = 1;
        private const byte TypeUInt32 = 2;
        private const byte TypeUInt64 = 3;
        private const byte TypeBoolean = 4;
        private const byte TypeEnum = 5;
        private const byte TypeUtf8 = 6;
        private const byte TypeSha1 = 7;
        private const byte TypeSha256 = 8;
        private const byte TypeCollection = 9;
        private const byte TypeOptional = 10;

        private readonly ArrayBufferWriter<byte> _buffer = new();

        internal CanonicalWriter(byte artifactKind)
        {
            WriteRaw(Encoding.ASCII.GetBytes("HCPUVD2\0"));
            WriteRaw([artifactKind]);
            Span<byte> version = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(version, EncodingVersion);
            WriteRaw(version);
        }

        internal void WriteUInt16(ushort tag, ushort value)
        {
            Span<byte> payload = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(payload, value);
            WriteField(tag, TypeUInt16, payload);
        }

        internal void WriteUInt32(ushort tag, uint value)
        {
            Span<byte> payload = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(payload, value);
            WriteField(tag, TypeUInt32, payload);
        }

        internal void WriteUInt64(ushort tag, ulong value)
        {
            Span<byte> payload = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(payload, value);
            WriteField(tag, TypeUInt64, payload);
        }

        internal void WriteUInt16Collection(ushort tag, ImmutableArray<ushort> values)
        {
            if (values.IsDefault)
                throw new ArgumentException("Canonical UInt16 collection cannot be default.", nameof(values));

            ushort[] ordered = values.Order().ToArray();
            var payload = new ArrayBufferWriter<byte>();
            WriteUInt32Raw(payload, checked((uint)ordered.Length));
            foreach (ushort value in ordered)
            {
                Span<byte> destination = payload.GetSpan(2);
                BinaryPrimitives.WriteUInt16BigEndian(destination, value);
                payload.Advance(2);
            }

            WriteField(tag, TypeCollection, payload.WrittenSpan);
        }

        internal void WriteBoolean(ushort tag, bool value) =>
            WriteField(tag, TypeBoolean, [value ? (byte)1 : (byte)0]);

        internal void WriteEnum<TEnum>(ushort tag, TEnum value) where TEnum : struct, Enum
        {
            Span<byte> payload = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(payload, Convert.ToUInt32(value));
            WriteField(tag, TypeEnum, payload);
        }

        internal void WriteString(ushort tag, string? value) =>
            WriteField(tag, TypeUtf8, Encoding.UTF8.GetBytes(value ?? string.Empty));

        internal void WriteCommitSha(ushort tag, string value) =>
            WriteField(tag, TypeSha1, Convert.FromHexString(RequireCommitSha(value)));

        internal void WriteSha256(ushort tag, string value) =>
            WriteField(tag, TypeSha256, Convert.FromHexString(RequireSha256(value)));

        internal void WriteOptionalString(ushort tag, string? value)
        {
            byte[] text = value is null ? [] : Encoding.UTF8.GetBytes(value);
            var payload = new byte[text.Length + 1];
            payload[0] = value is null ? (byte)0 : (byte)1;
            text.CopyTo(payload, 1);
            WriteField(tag, TypeOptional, payload);
        }

        internal void WriteOptionalSha256(ushort tag, string? value)
        {
            byte[] digest = value is null ? [] : Convert.FromHexString(RequireSha256(value));
            var payload = new byte[digest.Length + 1];
            payload[0] = value is null ? (byte)0 : (byte)1;
            digest.CopyTo(payload, 1);
            WriteField(tag, TypeOptional, payload);
        }

        internal void WriteOwnerMap(
            ushort tag,
            ImmutableArray<VirtualizationDecisionOwnerMapEntryV2> ownerMap)
        {
            var payload = new ArrayBufferWriter<byte>();
            VirtualizationDecisionOwnerMapEntryV2[] ordered = ownerMap.IsDefault
                ? []
                : ownerMap.OrderBy(entry => entry.FieldOrOperation, StringComparer.Ordinal).ToArray();
            WriteUInt32Raw(payload, (uint)ordered.Length);
            foreach (VirtualizationDecisionOwnerMapEntryV2 entry in ordered)
            {
                WriteStringRaw(payload, entry.FieldOrOperation);
                WriteStringRaw(payload, entry.Owner);
                WriteStringRaw(payload, entry.ValueSource);
                WriteStringRaw(payload, entry.CapabilityPolicy);
                WriteStringRaw(payload, entry.EvidenceClass);
                WriteStringRaw(payload, entry.MigrationClass);
                WriteStringRaw(payload, entry.DenialReason);
            }

            WriteField(tag, TypeCollection, payload.WrittenSpan);
        }

        internal void WriteReviewEvidence(
            ushort tag,
            VirtualizationDecisionReviewEvidenceV2 evidence)
        {
            ArgumentNullException.ThrowIfNull(evidence);
            var payload = new ArrayBufferWriter<byte>();
            WriteUInt32Raw(payload, Convert.ToUInt32(evidence.Role));
            WriteUInt32Raw(payload, Convert.ToUInt32(evidence.AuthorityPlane));
            WriteUInt32Raw(payload, Convert.ToUInt32(evidence.State));
            WriteStringRaw(payload, evidence.Principal);
            WriteStringRaw(payload, evidence.ReviewedDecisionId);
            WriteRawWithLength(payload, Convert.FromHexString(RequireSha256(evidence.ReviewedSpecDigest)));
            WriteRawWithLength(payload, Convert.FromHexString(RequireCommitSha(evidence.ReviewedSpecCommitSha)));
            WriteStringRaw(payload, evidence.EvidenceId);
            WriteField(tag, TypeCollection, payload.WrittenSpan);
        }

        internal ImmutableArray<byte> ToImmutable() =>
            ImmutableArray.CreateRange(_buffer.WrittenSpan.ToArray());

        private void WriteField(ushort tag, byte type, ReadOnlySpan<byte> payload)
        {
            Span<byte> header = stackalloc byte[7];
            BinaryPrimitives.WriteUInt16BigEndian(header, tag);
            header[2] = type;
            BinaryPrimitives.WriteUInt32BigEndian(header[3..], checked((uint)payload.Length));
            WriteRaw(header);
            WriteRaw(payload);
        }

        private void WriteRaw(ReadOnlySpan<byte> value)
        {
            Span<byte> destination = _buffer.GetSpan(value.Length);
            value.CopyTo(destination);
            _buffer.Advance(value.Length);
        }

        private static void WriteStringRaw(ArrayBufferWriter<byte> writer, string? value) =>
            WriteRawWithLength(writer, Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static void WriteRawWithLength(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> value)
        {
            WriteUInt32Raw(writer, checked((uint)value.Length));
            Span<byte> destination = writer.GetSpan(value.Length);
            value.CopyTo(destination);
            writer.Advance(value.Length);
        }

        private static void WriteUInt32Raw(ArrayBufferWriter<byte> writer, uint value)
        {
            Span<byte> destination = writer.GetSpan(4);
            BinaryPrimitives.WriteUInt32BigEndian(destination, value);
            writer.Advance(4);
        }

        private static string RequireSha256(string? value) =>
            IsCanonicalSha256(value)
                ? value!
                : throw new FormatException("SHA-256 must be 64 lowercase hexadecimal characters.");

        private static string RequireCommitSha(string? value) =>
            IsCanonicalCommitSha(value)
                ? value!
                : throw new FormatException("Commit SHA must be 40 lowercase hexadecimal characters.");
    }
}
