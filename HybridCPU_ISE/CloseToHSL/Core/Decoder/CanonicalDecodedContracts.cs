using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using OpcodeValues = YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Stable reason code for expected decode rejection. RF-04 defines the carrier
/// only; RF-05 maps legacy decoder rejection sites to this taxonomy.
/// </summary>
public enum DecodeFailureCode : ushort
{
    Unknown = 0,
    UnknownOpcode = 1,
    ProhibitedOpcode = 2,
    ReservedEncoding = 3,
    OperandEncoding = 4,
    ExtensionPayload = 5,
    Sideband = 6,
    BundleShape = 7,
    UnsupportedOpcode = 8,
}

/// <summary>
/// Typed expected-illegal-input result. It is deliberately not an exception and
/// therefore cannot turn a programming/invariant failure into an ordinary stall.
/// </summary>
public sealed record DecodeFailure(
    DecodeFailureCode Code,
    int SlotIndex,
    string Field,
    string RawHash,
    string Message)
{
    public static DecodeFailure Create(
        DecodeFailureCode code,
        int slotIndex,
        string field,
        ReadOnlySpan<byte> rawBytes,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new DecodeFailure(code, slotIndex, field, CanonicalPayloadSnapshot.Hash(rawBytes), message);
    }
}

/// <summary>
/// Immutable deep-copy payload used at the canonical decode boundary. Content is
/// a UTF-8 JSON snapshot, never a reference to mutable decoded sideband state.
/// </summary>
public sealed class CanonicalPayloadSnapshot : IEquatable<CanonicalPayloadSnapshot>
{
    private readonly ImmutableArray<byte> _content;

    private CanonicalPayloadSnapshot(string kind, ImmutableArray<byte> content, string contentSha256)
    {
        Kind = kind;
        _content = content;
        ContentSha256 = contentSha256;
    }

    public string Kind { get; }

    public string ContentSha256 { get; }

    public ReadOnlyMemory<byte> Content => _content.AsMemory();

    public static CanonicalPayloadSnapshot FromObject(string kind, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        byte[] content = value is null
            ? "null"u8.ToArray()
            : JsonSerializer.SerializeToUtf8Bytes(value, value.GetType());
        return FromBytes(kind, content);
    }

    public static CanonicalPayloadSnapshot FromBytes(string kind, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        byte[] copy = content.ToArray();
        return new CanonicalPayloadSnapshot(kind, ImmutableArray.Create(copy), Hash(copy));
    }

    public static string Hash(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public T? Deserialize<T>() => JsonSerializer.Deserialize<T>(_content.AsSpan());

    public bool Equals(CanonicalPayloadSnapshot? other) =>
        other is not null &&
        string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
        string.Equals(ContentSha256, other.ContentSha256, StringComparison.Ordinal) &&
        _content.AsSpan().SequenceEqual(other._content.AsSpan());

    public override bool Equals(object? obj) => Equals(obj as CanonicalPayloadSnapshot);

    public override int GetHashCode() => HashCode.Combine(Kind, ContentSha256);
}

/// <summary>
/// Full non-dynamic context needed for replay identity. The default context is
/// explicit and non-replay-eligible: missing runtime evidence never aliases a
/// valid context or silently enables cache serving.
/// </summary>
public sealed record CanonicalDecodeContext
{
    public required string ManifestVersion { get; init; }
    public required string ManifestHash { get; init; }
    public required string ExtensionConfigurationFingerprint { get; init; }
    public required string DecoderEpoch { get; init; }
    public required string DecoderVersion { get; init; }
    public required string PrivilegeContext { get; init; }
    public required string DomainIdentity { get; init; }
    public required string AddressSpaceIdentity { get; init; }
    public required string VectorConfigurationFingerprint { get; init; }
    public required ulong ExecutableMemoryInvalidationEpoch { get; init; }
    public required ulong CodeGenerationEpoch { get; init; }
    public required bool IsReplayEligible { get; init; }

    public static CanonicalDecodeContext Unbound { get; } = new()
    {
        ManifestVersion = GeneratedIsaCatalog.ManifestVersion,
        ManifestHash = GeneratedIsaCatalog.ManifestSha256,
        ExtensionConfigurationFingerprint = "unbound",
        DecoderEpoch = "rf04-shadow",
        DecoderVersion = "VliwDecoderV4-legacy",
        PrivilegeContext = "unbound",
        DomainIdentity = "unbound",
        AddressSpaceIdentity = "unbound",
        VectorConfigurationFingerprint = "unbound",
        ExecutableMemoryInvalidationEpoch = 0,
        CodeGenerationEpoch = 0,
        IsReplayEligible = false,
    };
}

/// <summary>
/// Immutable identity of decoded semantics. It intentionally has no admission,
/// issue, attempt, or <c>VliwOperationId</c> field. Equality compares raw bytes,
/// not only their digest, so a hash collision cannot create a replay identity hit.
/// </summary>
public readonly struct SemanticInstructionKey : IEquatable<SemanticInstructionKey>
{
    private readonly ImmutableArray<byte> _rawBundleBytes;

    private SemanticInstructionKey(
        ImmutableArray<byte> rawBundleBytes,
        string rawBundleSha256,
        string annotationsSha256,
        CanonicalDecodeContext context)
    {
        _rawBundleBytes = rawBundleBytes;
        RawBundleSha256 = rawBundleSha256;
        AnnotationsSha256 = annotationsSha256;
        ManifestVersion = context.ManifestVersion;
        ManifestHash = context.ManifestHash;
        ExtensionConfigurationFingerprint = context.ExtensionConfigurationFingerprint;
        DecoderEpoch = context.DecoderEpoch;
        DecoderVersion = context.DecoderVersion;
        PrivilegeContext = context.PrivilegeContext;
        DomainIdentity = context.DomainIdentity;
        AddressSpaceIdentity = context.AddressSpaceIdentity;
        VectorConfigurationFingerprint = context.VectorConfigurationFingerprint;
        ExecutableMemoryInvalidationEpoch = context.ExecutableMemoryInvalidationEpoch;
        CodeGenerationEpoch = context.CodeGenerationEpoch;
        IsReplayEligible = context.IsReplayEligible;
    }

    public string RawBundleSha256 { get; }
    public string AnnotationsSha256 { get; }
    public string ManifestVersion { get; }
    public string ManifestHash { get; }
    public string ExtensionConfigurationFingerprint { get; }
    public string DecoderEpoch { get; }
    public string DecoderVersion { get; }
    public string PrivilegeContext { get; }
    public string DomainIdentity { get; }
    public string AddressSpaceIdentity { get; }
    public string VectorConfigurationFingerprint { get; }
    public ulong ExecutableMemoryInvalidationEpoch { get; }
    public ulong CodeGenerationEpoch { get; }
    public bool IsReplayEligible { get; }
    public ReadOnlyMemory<byte> RawBundleBytes => _rawBundleBytes.AsMemory();

    public static SemanticInstructionKey Create(
        ReadOnlySpan<byte> rawBundleBytes,
        string annotationsSha256,
        CanonicalDecodeContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(annotationsSha256);
        ArgumentNullException.ThrowIfNull(context);
        ValidateContext(context);

        byte[] rawCopy = rawBundleBytes.ToArray();
        return new SemanticInstructionKey(
            ImmutableArray.Create(rawCopy),
            CanonicalPayloadSnapshot.Hash(rawCopy),
            annotationsSha256,
            context);
    }

    public bool Equals(SemanticInstructionKey other) =>
        RawBundleSha256 == other.RawBundleSha256 &&
        AnnotationsSha256 == other.AnnotationsSha256 &&
        ManifestVersion == other.ManifestVersion &&
        ManifestHash == other.ManifestHash &&
        ExtensionConfigurationFingerprint == other.ExtensionConfigurationFingerprint &&
        DecoderEpoch == other.DecoderEpoch &&
        DecoderVersion == other.DecoderVersion &&
        PrivilegeContext == other.PrivilegeContext &&
        DomainIdentity == other.DomainIdentity &&
        AddressSpaceIdentity == other.AddressSpaceIdentity &&
        VectorConfigurationFingerprint == other.VectorConfigurationFingerprint &&
        ExecutableMemoryInvalidationEpoch == other.ExecutableMemoryInvalidationEpoch &&
        CodeGenerationEpoch == other.CodeGenerationEpoch &&
        IsReplayEligible == other.IsReplayEligible &&
        _rawBundleBytes.AsSpan().SequenceEqual(other._rawBundleBytes.AsSpan());

    public override bool Equals(object? obj) => obj is SemanticInstructionKey other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RawBundleSha256, StringComparer.Ordinal);
        hash.Add(AnnotationsSha256, StringComparer.Ordinal);
        hash.Add(ManifestVersion, StringComparer.Ordinal);
        hash.Add(ManifestHash, StringComparer.Ordinal);
        hash.Add(ExtensionConfigurationFingerprint, StringComparer.Ordinal);
        hash.Add(DecoderEpoch, StringComparer.Ordinal);
        hash.Add(DecoderVersion, StringComparer.Ordinal);
        hash.Add(PrivilegeContext, StringComparer.Ordinal);
        hash.Add(DomainIdentity, StringComparer.Ordinal);
        hash.Add(AddressSpaceIdentity, StringComparer.Ordinal);
        hash.Add(VectorConfigurationFingerprint, StringComparer.Ordinal);
        hash.Add(ExecutableMemoryInvalidationEpoch);
        hash.Add(CodeGenerationEpoch);
        hash.Add(IsReplayEligible);
        return hash.ToHashCode();
    }

    public static bool operator ==(SemanticInstructionKey left, SemanticInstructionKey right) => left.Equals(right);
    public static bool operator !=(SemanticInstructionKey left, SemanticInstructionKey right) => !left.Equals(right);

    private static void ValidateContext(CanonicalDecodeContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ManifestVersion) ||
            string.IsNullOrWhiteSpace(context.ManifestHash) ||
            string.IsNullOrWhiteSpace(context.ExtensionConfigurationFingerprint) ||
            string.IsNullOrWhiteSpace(context.DecoderEpoch) ||
            string.IsNullOrWhiteSpace(context.DecoderVersion) ||
            string.IsNullOrWhiteSpace(context.PrivilegeContext) ||
            string.IsNullOrWhiteSpace(context.DomainIdentity) ||
            string.IsNullOrWhiteSpace(context.AddressSpaceIdentity) ||
            string.IsNullOrWhiteSpace(context.VectorConfigurationFingerprint))
        {
            throw new ArgumentException("Every SemanticInstructionKey context factor must be explicit.", nameof(context));
        }
    }
}

/// <summary>Immutable semantic projection for one fixed physical VLIW slot.</summary>
public sealed record CanonicalDecodedInstruction(
    int SlotIndex,
    bool IsOccupied,
    uint Opcode,
    InstructionClass? InstructionClass,
    SerializationClass? SerializationClass,
    byte Rd,
    byte Rs1,
    byte Rs2,
    long Immediate,
    ushort? CsrAddress,
    bool AcquireOrdering,
    bool ReleaseOrdering,
    CanonicalPayloadSnapshot RawSlot,
    CanonicalPayloadSnapshot InstructionPayload,
    CanonicalPayloadSnapshot SlotSideband)
{
    /// <summary>
    /// The generated static binding captured once at the canonical decode boundary.
    /// Empty slots have no binding; occupied generated slots must carry one.
    /// </summary>
    public GeneratedStaticBinding? StaticBinding { get; init; }

    /// <summary>
    /// Decoder-owned static scalar-load addressing shape. This deliberately
    /// contains no resolved address, footprint, bank or runtime memory state.
    /// </summary>
    public CanonicalScalarLoadAddressPlan? ScalarLoadAddressPlan { get; init; }
}

/// <summary>
/// Immutable static shape of a scalar load address calculation. It is not a
/// <see cref="MemoryCapability"/> and must never be used as a resolved memory
/// request, scheduler bank identity, footprint or completion token.
/// </summary>
public sealed class CanonicalScalarLoadAddressPlan
{
    private CanonicalScalarLoadAddressPlan(
        GeneratedStaticBinding generatedBinding,
        byte destinationRegisterId,
        byte baseRegisterId,
        long signedDisplacement,
        byte accessSize)
    {
        GeneratedBinding = generatedBinding;
        DestinationRegisterId = destinationRegisterId;
        BaseRegisterId = baseRegisterId;
        SignedDisplacement = signedDisplacement;
        AccessSize = accessSize;
    }

    public GeneratedStaticBinding GeneratedBinding { get; }
    public byte DestinationRegisterId { get; }
    public byte BaseRegisterId { get; }
    public long SignedDisplacement { get; }
    public byte AccessSize { get; }

    public static bool TryCreate(
        CanonicalDecodedInstruction canonical,
        out CanonicalScalarLoadAddressPlan? plan)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        GeneratedStaticBinding? binding = canonical.StaticBinding;
        if (!canonical.IsOccupied ||
            canonical.InstructionClass != InstructionClass.Memory ||
            canonical.SerializationClass != SerializationClass.Free ||
            binding is null ||
            binding.Opcode != canonical.Opcode ||
            !TryResolveAccessSize(canonical.Opcode, out byte accessSize))
        {
            plan = null;
            return false;
        }

        plan = new CanonicalScalarLoadAddressPlan(
            binding,
            canonical.Rd,
            canonical.Rs1,
            canonical.Immediate,
            accessSize);
        return true;
    }

    internal void EnsureMatches(CanonicalDecodedInstruction canonical)
    {
        if (!TryCreate(canonical, out CanonicalScalarLoadAddressPlan? expected) ||
            expected is null ||
            !ReferenceEquals(GeneratedBinding, expected.GeneratedBinding) ||
            DestinationRegisterId != expected.DestinationRegisterId ||
            BaseRegisterId != expected.BaseRegisterId ||
            SignedDisplacement != expected.SignedDisplacement ||
            AccessSize != expected.AccessSize)
        {
            throw new InvalidOperationException(
                "Canonical scalar-load address plan does not match its frozen canonical slot and binding.");
        }
    }

    private static bool TryResolveAccessSize(uint opcode, out byte accessSize)
    {
        accessSize = opcode switch
        {
            OpcodeValues.LB or OpcodeValues.LBU => 1,
            OpcodeValues.LH or OpcodeValues.LHU => 2,
            OpcodeValues.LW or OpcodeValues.LWU => 4,
            OpcodeValues.LD => 8,
            _ => 0,
        };
        return accessSize != 0;
    }
}

/// <summary>
/// Frozen canonical decoded bundle. This is additive shadow output in RF-04; the
/// legacy <see cref="DecodedInstructionBundle"/> remains the active public adapter.
/// </summary>
public sealed class CanonicalBundle
{
    private CanonicalBundle(
        ulong bundleAddress,
        ulong bundleSerial,
        ImmutableArray<CanonicalDecodedInstruction> slots,
        CanonicalPayloadSnapshot bundleSideband,
        SemanticInstructionKey semanticKey)
    {
        BundleAddress = bundleAddress;
        BundleSerial = bundleSerial;
        Slots = slots;
        BundleSideband = bundleSideband;
        SemanticKey = semanticKey;
    }

    public ulong BundleAddress { get; }
    public ulong BundleSerial { get; }
    public ImmutableArray<CanonicalDecodedInstruction> Slots { get; }
    public CanonicalPayloadSnapshot BundleSideband { get; }
    public SemanticInstructionKey SemanticKey { get; }
    public bool IsReplayEligible => SemanticKey.IsReplayEligible;

    public CanonicalDecodedInstruction GetSlot(int slotIndex) =>
        (uint)slotIndex < (uint)Slots.Length
            ? Slots[slotIndex]
            : throw new ArgumentOutOfRangeException(nameof(slotIndex));

    public static CanonicalBundle Create(
        ReadOnlySpan<VLIW_Instruction> rawSlots,
        IReadOnlyList<DecodedInstruction> decodedSlots,
        BundleMetadata bundleMetadata,
        ulong bundleAddress,
        ulong bundleSerial,
        CanonicalDecodeContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(decodedSlots);
        ArgumentNullException.ThrowIfNull(bundleMetadata);
        if (decodedSlots.Count != BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentException("Canonical bundles require exactly eight normalized decoded slots.", nameof(decodedSlots));
        }

        var rawBundle = new byte[BundleMetadata.BundleSlotCount * 32];
        var slots = ImmutableArray.CreateBuilder<CanonicalDecodedInstruction>(BundleMetadata.BundleSlotCount);
        for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            VLIW_Instruction rawSlot = slotIndex < rawSlots.Length ? rawSlots[slotIndex] : default;
            if (!rawSlot.TryWriteBytes(rawBundle.AsSpan(slotIndex * 32, 32)))
            {
                throw new InvalidOperationException("Fixed-width VLIW raw-slot serialization unexpectedly failed.");
            }

            DecodedInstruction decoded = decodedSlots[slotIndex] ?? throw new ArgumentException("Decoded slots cannot contain null.", nameof(decodedSlots));
            if (decoded.SlotIndex != slotIndex)
            {
                throw new ArgumentException("Decoded slots must be normalized into physical slot order.", nameof(decodedSlots));
            }

            InstructionIR? instruction = decoded.Instruction;
            CanonicalDecodedInstruction canonicalSlot = new CanonicalDecodedInstruction(
                SlotIndex: slotIndex,
                IsOccupied: instruction is not null,
                Opcode: instruction is null ? 0u : (uint)instruction.CanonicalOpcode,
                InstructionClass: instruction?.Class,
                SerializationClass: instruction?.SerializationClass,
                Rd: instruction?.Rd ?? 0,
                Rs1: instruction?.Rs1 ?? 0,
                Rs2: instruction?.Rs2 ?? 0,
                Immediate: instruction?.Imm ?? 0,
                CsrAddress: instruction?.CsrAddress,
                AcquireOrdering: instruction?.AcquireOrdering ?? false,
                ReleaseOrdering: instruction?.ReleaseOrdering ?? false,
                RawSlot: CanonicalPayloadSnapshot.FromBytes("VLIW_Instruction", rawBundle.AsSpan(slotIndex * 32, 32)),
                InstructionPayload: CanonicalPayloadSnapshot.FromObject("InstructionIR", instruction),
                SlotSideband: CanonicalPayloadSnapshot.FromObject("InstructionSlotMetadata", decoded.SlotMetadata));
            if (instruction is not null &&
                GeneratedStaticBinding.TryFromOpcode((uint)instruction.CanonicalOpcode, out GeneratedStaticBinding binding))
            {
                canonicalSlot = canonicalSlot with { StaticBinding = binding };
                if (CanonicalScalarLoadAddressPlan.TryCreate(canonicalSlot, out CanonicalScalarLoadAddressPlan? loadPlan))
                {
                    canonicalSlot = canonicalSlot with { ScalarLoadAddressPlan = loadPlan };
                }
            }

            slots.Add(canonicalSlot);
        }

        CanonicalPayloadSnapshot bundleSideband = CanonicalPayloadSnapshot.FromObject("BundleMetadata", bundleMetadata);
        string annotationHash = ComputeAnnotationsHash(bundleSideband, slots);
        SemanticInstructionKey key = SemanticInstructionKey.Create(
            rawBundle,
            annotationHash,
            context ?? CanonicalDecodeContext.Unbound);
        return new CanonicalBundle(bundleAddress, bundleSerial, slots.MoveToImmutable(), bundleSideband, key);
    }

    /// <summary>
    /// Creates a frozen canonical bundle directly from the RF-05 immutable slot
    /// contracts. Unlike the legacy-adapter overload this path never observes an
    /// <see cref="InstructionIR"/> and therefore cannot reconstruct mutable IR.
    /// </summary>
    public static CanonicalBundle CreateFromCanonicalSlots(
        ReadOnlySpan<VLIW_Instruction> rawSlots,
        IReadOnlyList<CanonicalDecodedInstruction> canonicalSlots,
        BundleMetadata bundleMetadata,
        ulong bundleAddress,
        ulong bundleSerial,
        CanonicalDecodeContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(canonicalSlots);
        ArgumentNullException.ThrowIfNull(bundleMetadata);
        if (rawSlots.Length != BundleMetadata.BundleSlotCount ||
            canonicalSlots.Count != BundleMetadata.BundleSlotCount)
        {
            throw new ArgumentException(
                "Canonical bundles require exactly eight raw and decoded slots.");
        }

        var rawBundle = new byte[BundleMetadata.BundleSlotCount * 32];
        var slots = ImmutableArray.CreateBuilder<CanonicalDecodedInstruction>(BundleMetadata.BundleSlotCount);
        for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            VLIW_Instruction rawSlot = rawSlots[slotIndex];
            if (!rawSlot.TryWriteBytes(rawBundle.AsSpan(slotIndex * 32, 32)))
            {
                throw new InvalidOperationException("Fixed-width VLIW raw-slot serialization unexpectedly failed.");
            }

            CanonicalDecodedInstruction slot = canonicalSlots[slotIndex] ??
                throw new ArgumentException("Canonical slots cannot contain null.", nameof(canonicalSlots));
            if (slot.SlotIndex != slotIndex)
            {
                throw new ArgumentException(
                    "Canonical slots must be normalized into physical slot order.",
                    nameof(canonicalSlots));
            }

            if (CanonicalScalarLoadAddressPlan.TryCreate(slot, out CanonicalScalarLoadAddressPlan? derivedLoadPlan))
            {
                if (slot.ScalarLoadAddressPlan is null)
                {
                    slot = slot with { ScalarLoadAddressPlan = derivedLoadPlan };
                }
                else
                {
                    slot.ScalarLoadAddressPlan.EnsureMatches(slot);
                }
            }
            else if (slot.ScalarLoadAddressPlan is not null)
            {
                throw new InvalidOperationException(
                    "A canonical non-load slot cannot carry a scalar-load address plan.");
            }

            slots.Add(slot);
        }

        CanonicalPayloadSnapshot bundleSideband = CanonicalPayloadSnapshot.FromObject("BundleMetadata", bundleMetadata);
        string annotationHash = ComputeAnnotationsHash(bundleSideband, slots);
        SemanticInstructionKey key = SemanticInstructionKey.Create(
            rawBundle,
            annotationHash,
            context ?? CanonicalDecodeContext.Unbound);
        return new CanonicalBundle(bundleAddress, bundleSerial, slots.MoveToImmutable(), bundleSideband, key);
    }

    private static string ComputeAnnotationsHash(
        CanonicalPayloadSnapshot bundleSideband,
        ImmutableArray<CanonicalDecodedInstruction>.Builder slots)
    {
        var text = new StringBuilder(bundleSideband.ContentSha256);
        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            text.Append('|');
            text.Append(slots[slotIndex].SlotSideband.ContentSha256);
        }

        return CanonicalPayloadSnapshot.Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }
}
