using System;
using System.Text;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>
/// Immutable validation witness for one frozen semantic replay entry.
/// The digest is an integrity witness over canonical decoded content; it is
/// never sufficient for lookup equality and carries no scheduler decision or
/// issued-attempt identity.
/// </summary>
public readonly record struct ReplayEntryValidationFingerprint(
    ushort SchemaVersion,
    string CanonicalContentSha256)
{
    public const ushort CurrentSchemaVersion = 1;

    public bool IsValid =>
        SchemaVersion == CurrentSchemaVersion &&
        CanonicalContentSha256 is { Length: 64 };
}

/// <summary>
/// Frozen cacheable decode result for RF-09 semantic replay.
///
/// This contract is deliberately not a serving cache. It owns only immutable
/// canonical decode data and semantic lookup identity. Replay lookup,
/// nomination and Stage A must not allocate a VLIW operation identity; each
/// successful attempt remains responsible for its own post-Stage-B identity.
/// </summary>
public sealed class ReplayEntry
{
    private ReplayEntry(
        SemanticInstructionKey semanticKey,
        CanonicalBundle canonicalBundle,
        ReplayEntryValidationFingerprint validationFingerprint)
    {
        SemanticKey = semanticKey;
        CanonicalBundle = canonicalBundle;
        ValidationFingerprint = validationFingerprint;
    }

    public SemanticInstructionKey SemanticKey { get; }

    public CanonicalBundle CanonicalBundle { get; }

    public ReplayEntryValidationFingerprint ValidationFingerprint { get; }

    /// <summary>
    /// Freezes an explicitly replay-eligible canonical bundle.
    /// Unbound decode context is rejected rather than silently becoming a
    /// cache-serving identity.
    /// </summary>
    public static ReplayEntry Create(CanonicalBundle canonicalBundle)
    {
        ArgumentNullException.ThrowIfNull(canonicalBundle);
        if (!canonicalBundle.IsReplayEligible)
        {
            throw new InvalidOperationException(
                "ReplayEntry requires an explicitly replay-eligible CanonicalDecodeContext.");
        }

        SemanticInstructionKey semanticKey = canonicalBundle.SemanticKey;
        return new ReplayEntry(
            semanticKey,
            canonicalBundle,
            ComputeValidationFingerprint(canonicalBundle));
    }

    /// <summary>
    /// Revalidates the frozen entry without treating its digest as lookup
    /// equality. Semantic key equality remains the authoritative comparison.
    /// </summary>
    public bool HasValidFrozenContent()
    {
        return ValidationFingerprint.IsValid &&
            SemanticKey.Equals(CanonicalBundle.SemanticKey) &&
            ValidationFingerprint.Equals(ComputeValidationFingerprint(CanonicalBundle));
    }

    /// <summary>
    /// Compares full frozen semantic content after authoritative semantic-key
    /// equality. Bundle address and transport serial are intentionally excluded:
    /// they are placement/transport facts, not decoded semantic identity.
    /// </summary>
    public bool HasSameFrozenSemanticContent(ReplayEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!SemanticKey.Equals(other.SemanticKey) ||
            !HasValidFrozenContent() ||
            !other.HasValidFrozenContent() ||
            !CanonicalBundle.BundleSideband.Equals(other.CanonicalBundle.BundleSideband) ||
            CanonicalBundle.Slots.Length != other.CanonicalBundle.Slots.Length)
        {
            return false;
        }

        for (int slotIndex = 0; slotIndex < CanonicalBundle.Slots.Length; slotIndex++)
        {
            if (!CanonicalBundle.Slots[slotIndex].Equals(
                    other.CanonicalBundle.Slots[slotIndex]))
            {
                return false;
            }
        }

        return true;
    }

    private static ReplayEntryValidationFingerprint ComputeValidationFingerprint(
        CanonicalBundle canonicalBundle)
    {
        var canonical = new StringBuilder();
        SemanticInstructionKey semanticKey = canonicalBundle.SemanticKey;
        Append(canonical, "rf09-replay-entry-v1");
        Append(canonical, semanticKey.RawBundleSha256);
        Append(canonical, semanticKey.AnnotationsSha256);
        Append(canonical, semanticKey.ManifestVersion);
        Append(canonical, semanticKey.ManifestHash);
        Append(canonical, semanticKey.ExtensionConfigurationFingerprint);
        Append(canonical, semanticKey.DecoderEpoch);
        Append(canonical, semanticKey.DecoderVersion);
        Append(canonical, semanticKey.PrivilegeContext);
        Append(canonical, semanticKey.DomainIdentity);
        Append(canonical, semanticKey.AddressSpaceIdentity);
        Append(canonical, semanticKey.VectorConfigurationFingerprint);
        Append(canonical, semanticKey.ExecutableMemoryInvalidationEpoch.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, semanticKey.CodeGenerationEpoch.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        Append(canonical, semanticKey.IsReplayEligible ? "1" : "0");
        Append(canonical, canonicalBundle.BundleSideband.Kind);
        Append(canonical, canonicalBundle.BundleSideband.ContentSha256);

        for (int slotIndex = 0; slotIndex < canonicalBundle.Slots.Length; slotIndex++)
        {
            CanonicalDecodedInstruction slot = canonicalBundle.Slots[slotIndex];
            Append(canonical, slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(canonical, slot.RawSlot.Kind);
            Append(canonical, slot.RawSlot.ContentSha256);
            Append(canonical, slot.InstructionPayload.Kind);
            Append(canonical, slot.InstructionPayload.ContentSha256);
            Append(canonical, slot.SlotSideband.Kind);
            Append(canonical, slot.SlotSideband.ContentSha256);
        }

        return new ReplayEntryValidationFingerprint(
            ReplayEntryValidationFingerprint.CurrentSchemaVersion,
            CanonicalPayloadSnapshot.Hash(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
