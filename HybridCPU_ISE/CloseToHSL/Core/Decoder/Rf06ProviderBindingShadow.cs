using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using YAKSys_Hybrid_CPU.Arch.Generated;

namespace YAKSys_Hybrid_CPU.Core.Decoder;

/// <summary>Failure classes owned by the RF-06.1 generated-binding shadow.</summary>
public enum ProviderBindingShadowFailureKind : byte
{
    Missing = 0,
    Duplicate = 1,
    Inactive = 2,
    Unreferenced = 3,
    SchemaMismatch = 4,
}

/// <summary>One immutable shadow entry; it is not a runtime provider instance.</summary>
public sealed record GeneratedProviderBindingShadowEntry
{
    public const string ExpectedSchemaVersion = "rf06.generated-provider-binding.v1";

    public GeneratedProviderBindingShadowEntry(
        GeneratedStaticBinding binding,
        string schemaVersion = ExpectedSchemaVersion,
        bool isActive = true)
    {
        Binding = binding;
        SchemaVersion = RequireSchema(schemaVersion);
        IsActive = isActive;
    }

    public GeneratedStaticBinding Binding { get; }
    public string SchemaVersion { get; }
    public bool IsActive { get; }

    private static string RequireSchema(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}

/// <summary>
/// Deterministic generated-binding shadow. RF-06.1 validates this shadow only;
/// it does not register or resolve a production execution provider.
/// </summary>
public sealed class GeneratedProviderBindingShadowRegistry
{
    public GeneratedProviderBindingShadowRegistry(IEnumerable<GeneratedProviderBindingShadowEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries.ToImmutableArray();
        if (Entries.Any(entry => entry is null))
        {
            throw new ArgumentException("A generated provider-binding shadow cannot contain null entries.", nameof(entries));
        }
    }

    public ImmutableArray<GeneratedProviderBindingShadowEntry> Entries { get; }

    public static GeneratedProviderBindingShadowRegistry FromGeneratedCatalog()
    {
        var uniqueBindings = new HashSet<GeneratedStaticBinding>();
        var entries = new List<GeneratedProviderBindingShadowEntry>();
        foreach (GeneratedIsaDescriptor descriptor in GeneratedIsaCatalog.Descriptors)
        {
            GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
            if (uniqueBindings.Add(binding))
            {
                entries.Add(new GeneratedProviderBindingShadowEntry(binding));
            }
        }

        return new GeneratedProviderBindingShadowRegistry(entries);
    }

    public GeneratedProviderBindingShadowRegistry ReorderedForTest()
    {
        return new GeneratedProviderBindingShadowRegistry(Entries.Reverse());
    }

    public string CanonicalFingerprint()
    {
        return string.Join(
            "|",
            Entries
                .OrderBy(entry => entry.Binding.Opcode)
                .ThenBy(entry => entry.Binding.MaterializerId.Value, StringComparer.Ordinal)
                .ThenBy(entry => entry.Binding.RuntimeExecutionProviderId.Value, StringComparer.Ordinal)
                .ThenBy(entry => entry.Binding.LatencyModelId.Value, StringComparer.Ordinal)
                .Select(entry => string.Join(
                    ":",
                    entry.Binding.Opcode,
                    entry.Binding.MaterializerId.Value,
                    entry.Binding.RuntimeExecutionProviderId.Value,
                    entry.Binding.LatencyModelId.Value,
                    entry.SchemaVersion,
                    entry.IsActive ? "active" : "inactive")));
    }
}

public sealed record ProviderBindingShadowFailure(
    ProviderBindingShadowFailureKind Kind,
    string Identity,
    string Detail);

public sealed record ProviderBindingShadowValidationReport(
    bool IsValid,
    ImmutableArray<ProviderBindingShadowFailure> Failures)
{
    public static ProviderBindingShadowValidationReport Valid { get; } =
        new(true, ImmutableArray<ProviderBindingShadowFailure>.Empty);
}

/// <summary>
/// Compares generated static references with the deterministic shadow registry.
/// Ordering is deliberately excluded from validity and canonical identity.
/// </summary>
public static class GeneratedProviderBindingShadowValidator
{
    public static ProviderBindingShadowValidationReport Validate(
        GeneratedProviderBindingShadowRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var expected = new Dictionary<GeneratedStaticBinding, int>();
        foreach (GeneratedIsaDescriptor descriptor in GeneratedIsaCatalog.Descriptors)
        {
            GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
            expected[binding] = expected.TryGetValue(binding, out int count) ? count + 1 : 1;
        }

        var failures = ImmutableArray.CreateBuilder<ProviderBindingShadowFailure>();
        var grouped = registry.Entries
            .GroupBy(entry => entry.Binding)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach ((GeneratedStaticBinding binding, int referenceCount) in expected)
        {
            if (!grouped.TryGetValue(binding, out GeneratedProviderBindingShadowEntry[]? candidates))
            {
                failures.Add(new(
                    ProviderBindingShadowFailureKind.Missing,
                    Describe(binding),
                    $"Generated catalog references the binding {referenceCount} time(s), but the shadow has no entry."));
                continue;
            }

            if (candidates.Length != 1)
            {
                failures.Add(new(
                    ProviderBindingShadowFailureKind.Duplicate,
                    Describe(binding),
                    $"Expected exactly one shadow entry, found {candidates.Length}."));
            }

            foreach (GeneratedProviderBindingShadowEntry candidate in candidates)
            {
                if (!string.Equals(candidate.SchemaVersion, GeneratedProviderBindingShadowEntry.ExpectedSchemaVersion, StringComparison.Ordinal))
                {
                    failures.Add(new(
                        ProviderBindingShadowFailureKind.SchemaMismatch,
                        Describe(binding),
                        $"Expected schema '{GeneratedProviderBindingShadowEntry.ExpectedSchemaVersion}', got '{candidate.SchemaVersion}'."));
                }

                if (!candidate.IsActive)
                {
                    failures.Add(new(
                        ProviderBindingShadowFailureKind.Inactive,
                        Describe(binding),
                        "The generated binding is present but inactive."));
                }
            }
        }

        foreach (GeneratedProviderBindingShadowEntry entry in registry.Entries)
        {
            if (!expected.ContainsKey(entry.Binding))
            {
                failures.Add(new(
                    ProviderBindingShadowFailureKind.Unreferenced,
                    Describe(entry.Binding),
                    "The shadow entry is not referenced by the generated catalog."));
            }
        }

        return failures.Count == 0
            ? ProviderBindingShadowValidationReport.Valid
            : new ProviderBindingShadowValidationReport(false, failures.ToImmutable());
    }

    private static string Describe(GeneratedStaticBinding binding) =>
        $"opcode={binding.Opcode};materializer={binding.MaterializerId.Value};provider={binding.RuntimeExecutionProviderId.Value};latency={binding.LatencyModelId.Value}";
}
