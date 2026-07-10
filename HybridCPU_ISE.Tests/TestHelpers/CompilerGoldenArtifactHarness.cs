using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU.Compiler.Core.IR.Intent;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal sealed class CompilerGoldenArtifactManifest
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("entries")]
    public List<CompilerGoldenArtifactEntry> Entries { get; set; } = [];
}

internal sealed class CompilerGoldenArtifactEntry
{
    [JsonPropertyName("artifact_id")]
    public string ArtifactId { get; set; } = string.Empty;

    [JsonPropertyName("source_test")]
    public string SourceTest { get; set; } = string.Empty;

    [JsonPropertyName("contour")]
    public string Contour { get; set; } = string.Empty;

    [JsonPropertyName("intent_kind")]
    public string IntentKind { get; set; } = string.Empty;

    [JsonPropertyName("opcode_or_opcode_family")]
    public string OpcodeOrOpcodeFamily { get; set; } = string.Empty;

    [JsonPropertyName("producer_surface")]
    public string ProducerSurface { get; set; } = string.Empty;

    [JsonPropertyName("production_gate_id")]
    public string ProductionGateId { get; set; } = string.Empty;

    [JsonPropertyName("carrier_words_or_bytes_hash")]
    public string CarrierWordsOrBytesHash { get; set; } = string.Empty;

    [JsonPropertyName("descriptor_hash")]
    public string DescriptorHash { get; set; } = string.Empty;

    [JsonPropertyName("sideband_hash")]
    public string SidebandHash { get; set; } = string.Empty;

    [JsonPropertyName("typed_slot_fact_hash")]
    public string TypedSlotFactHash { get; set; } = string.Empty;

    [JsonPropertyName("evidence_hash")]
    public string EvidenceHash { get; set; } = string.Empty;

    [JsonPropertyName("runtime_bridge_hash")]
    public string RuntimeBridgeHash { get; set; } = string.Empty;

    [JsonPropertyName("telemetry_hash")]
    public string TelemetryHash { get; set; } = string.Empty;

    [JsonPropertyName("no_fallback_proof_id")]
    public string NoFallbackProofId { get; set; } = string.Empty;

    [JsonPropertyName("runtime_authority_dependency")]
    public string RuntimeAuthorityDependency { get; set; } = string.Empty;

    [JsonPropertyName("explicit_non_claims")]
    public string ExplicitNonClaims { get; set; } = string.Empty;

    [JsonPropertyName("ise_decode_parity_status")]
    public string IseDecodeParityStatus { get; set; } = string.Empty;

    [JsonPropertyName("expected_outcome")]
    public string ExpectedOutcome { get; set; } = string.Empty;
}

internal sealed record CompilerGoldenArtifactSpec(
    string ArtifactId,
    string SourceTest,
    SemanticIntentKind IntentKind,
    ExecutionContourKind Contour,
    string OpcodeOrOpcodeFamily,
    string ProducerSurface,
    string ProductionGateId,
    string NoFallbackProofId,
    string IseDecodeParityStatus = "DeferredToPhase05ParityHarness",
    string ExpectedOutcome = "compatibility/helper artifact; runtime authority pending");

/// <summary>
/// Test-only Phase 02 harness. It snapshots separated compiler artifacts and
/// intentionally ignores package GUIDs and other non-contract identity noise.
/// </summary>
internal static class CompilerGoldenArtifactHarness
{
    internal const int SchemaVersion = 1;
    internal const string ExplicitNonClaims =
        "not execution; not publication; not commit; not retire; not final runtime legality";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    internal static CompilerGoldenArtifactManifest LoadManifest(
        string repoRoot,
        string relativePath)
    {
        string path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        CompilerGoldenArtifactManifest? manifest =
            JsonSerializer.Deserialize<CompilerGoldenArtifactManifest>(File.ReadAllText(path));
        if (manifest is null)
        {
            throw new InvalidDataException($"Golden artifact manifest is empty: {relativePath}");
        }

        return manifest;
    }

    internal static CompilerGoldenArtifactEntry Snapshot(
        CompilerEmissionPackage package,
        CompilerGoldenArtifactSpec spec)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(spec);

        return new CompilerGoldenArtifactEntry
        {
            ArtifactId = spec.ArtifactId,
            SourceTest = spec.SourceTest,
            Contour = spec.Contour.ToString(),
            IntentKind = spec.IntentKind.ToString(),
            OpcodeOrOpcodeFamily = spec.OpcodeOrOpcodeFamily,
            ProducerSurface = spec.ProducerSurface,
            ProductionGateId = spec.ProductionGateId,
            CarrierWordsOrBytesHash = package.Carrier is null
                ? "not-present"
                : HashBytes(package.Carrier.Image.SerializedImage),
            DescriptorHash = package.Descriptor is null
                ? "not-present"
                : HashCanonical(package.Descriptor),
            SidebandHash = package.Sideband is null
                ? "not-present"
                : HashSideband(package.Sideband),
            TypedSlotFactHash = package.TypedSlotFacts is null
                ? "not-present"
                : HashCanonical(package.TypedSlotFacts),
            EvidenceHash = HashCanonical(package.Evidence),
            RuntimeBridgeHash = package.RuntimeBridgeInput is null
                ? "not-present"
                : HashCanonical(package.RuntimeBridgeInput),
            TelemetryHash = "not-present-in-compatibility-package",
            NoFallbackProofId = spec.NoFallbackProofId,
            RuntimeAuthorityDependency = package.Header.RuntimeAuthorityDependency.ToString(),
            ExplicitNonClaims = ExplicitNonClaims,
            IseDecodeParityStatus = spec.IseDecodeParityStatus,
            ExpectedOutcome = spec.ExpectedOutcome
        };
    }

    internal static string Render(CompilerGoldenArtifactManifest manifest) =>
        JsonSerializer.Serialize(manifest, JsonOptions);

    internal static void AssertManifestShape(
        CompilerGoldenArtifactManifest manifest,
        IReadOnlyCollection<string> expectedArtifactIds)
    {
        if (manifest.SchemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                $"Expected golden artifact schema {SchemaVersion}, got {manifest.SchemaVersion}.");
        }

        string[] actualIds = manifest.Entries
            .Select(static entry => entry.ArtifactId)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds = expectedArtifactIds
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Golden artifact ids differ. Expected: {string.Join(", ", expectedIds)}; " +
                $"actual: {string.Join(", ", actualIds)}.");
        }

        foreach (CompilerGoldenArtifactEntry entry in manifest.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.CarrierWordsOrBytesHash) ||
                string.IsNullOrWhiteSpace(entry.NoFallbackProofId) ||
                !string.Equals(entry.ExplicitNonClaims, ExplicitNonClaims, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Golden artifact `{entry.ArtifactId}` is missing separated artifact or non-claim fields.");
            }
        }
    }

    private static string HashSideband(CompilerSidebandEnvelope sideband)
    {
        var rows = new List<string>
        {
            sideband.Requirement.ToString(),
            sideband.PreservationClass.ToString(),
            sideband.IsEmptyCompatibilitySideband.ToString()
        };

        for (int bundleIndex = 0; bundleIndex < sideband.BundleAnnotations.Count; bundleIndex++)
        {
            VliwBundleAnnotations annotations = sideband.BundleAnnotations[bundleIndex];
            rows.Add($"bundle:{bundleIndex}:{Canonicalize(annotations.BundleMetadata)}:{annotations.Count}");
            for (int slotIndex = 0; slotIndex < annotations.Count; slotIndex++)
            {
                if (!annotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata))
                {
                    rows.Add($"slot:{slotIndex}:missing");
                    continue;
                }

                rows.Add($"slot:{slotIndex}:{Canonicalize(metadata)}");
            }
        }

        return HashCanonical(rows);
    }

    private static string HashCanonical(object? value) =>
        HashBytes(Encoding.UTF8.GetBytes(Canonicalize(value)));

    private static string HashBytes(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string Canonicalize(object? value) =>
        Canonicalize(value, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static string Canonicalize(
        object? value,
        int depth,
        ISet<object> active)
    {
        if (value is null)
        {
            return "null";
        }

        if (depth > 10)
        {
            return "<depth-limit>";
        }

        if (value is string text)
        {
            return JsonSerializer.Serialize(text);
        }

        if (value is byte[] bytes)
        {
            return $"bytes:{Convert.ToHexString(bytes)}";
        }

        Type type = value.GetType();
        if (type.IsEnum ||
            type.IsPrimitive ||
            value is decimal or DateTime or DateTimeOffset or Guid)
        {
            return $"{type.FullName}:{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}";
        }

        if (value is IEnumerable enumerable)
        {
            var items = new List<string>();
            foreach (object? item in enumerable)
            {
                items.Add(Canonicalize(item, depth + 1, active));
            }

            return $"{type.FullName}:[{string.Join(",", items)}]";
        }

        if (!type.IsValueType && !active.Add(value))
        {
            return $"{type.FullName}:<cycle>";
        }

        try
        {
            var properties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
                .OrderBy(static property => property.Name, StringComparer.Ordinal);
            var fields = new List<string>();
            foreach (PropertyInfo property in properties)
            {
                object? propertyValue;
                try
                {
                    propertyValue = property.GetValue(value);
                }
                catch (Exception exception) when (exception is InvalidOperationException or TargetInvocationException)
                {
                    propertyValue = $"<getter:{exception.GetType().Name}>";
                }

                fields.Add(
                    $"{property.Name}={Canonicalize(propertyValue, depth + 1, active)}");
            }

            return $"{type.FullName}{{{string.Join(";", fields)}}}";
        }
        finally
        {
            if (!type.IsValueType)
            {
                active.Remove(value);
            }
        }
    }
}
