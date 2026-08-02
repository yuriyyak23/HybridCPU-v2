using System.Collections.Immutable;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf06ProviderBindingShadowTests
{
    [Fact]
    public void GeneratedCatalogShadow_CoversEveryDescriptorExactlyOnce()
    {
        GeneratedProviderBindingShadowRegistry registry =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();

        Assert.Equal(GeneratedIsaCatalog.Descriptors.Length, registry.Entries.Length);
        Assert.Equal(
            registry.Entries.Length,
            registry.Entries.Select(entry => entry.Binding).Distinct().Count());

        ProviderBindingShadowValidationReport report =
            GeneratedProviderBindingShadowValidator.Validate(registry);

        Assert.True(report.IsValid, FormatFailures(report));
        Assert.Empty(report.Failures);
    }

    [Fact]
    public void Shadow_MissingEntry_IsRejected()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        var mutated = baseline.Entries.Skip(1).ToImmutableArray();

        ProviderBindingShadowValidationReport report = Validate(mutated);

        Assert.Contains(report.Failures, failure =>
            failure.Kind == ProviderBindingShadowFailureKind.Missing);
    }

    [Fact]
    public void Shadow_DuplicateEntry_IsRejected()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        var mutated = baseline.Entries.Add(baseline.Entries[0]);

        ProviderBindingShadowValidationReport report = Validate(mutated);

        Assert.Contains(report.Failures, failure =>
            failure.Kind == ProviderBindingShadowFailureKind.Duplicate);
    }

    [Fact]
    public void Shadow_InactiveEntry_IsRejected()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        GeneratedProviderBindingShadowEntry inactive = new(
            baseline.Entries[0].Binding,
            baseline.Entries[0].SchemaVersion,
            isActive: false);
        var mutated = baseline.Entries.SetItem(0, inactive);

        ProviderBindingShadowValidationReport report = Validate(mutated);

        Assert.Contains(report.Failures, failure =>
            failure.Kind == ProviderBindingShadowFailureKind.Inactive);
    }

    [Fact]
    public void Shadow_UnreferencedEntry_IsRejected()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        GeneratedStaticBinding unreferenced = new(
            0xFFFF_FFFEu,
            new MaterializerId("rf06.unreferenced.materializer"),
            new RuntimeExecutionProviderId("rf06.unreferenced.provider"),
            new LatencyModelId("rf06.unreferenced.latency"),
            GeneratedIsaCatalog.CatalogVersion,
            GeneratedIsaCatalog.CatalogSha256,
            "rf06-unreferenced");
        var mutated = baseline.Entries.Add(new GeneratedProviderBindingShadowEntry(unreferenced));

        ProviderBindingShadowValidationReport report = Validate(mutated);

        Assert.Contains(report.Failures, failure =>
            failure.Kind == ProviderBindingShadowFailureKind.Unreferenced);
    }

    [Fact]
    public void Shadow_SchemaMismatch_IsRejected()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        GeneratedProviderBindingShadowEntry mismatched = new(
            baseline.Entries[0].Binding,
            "rf06.generated-provider-binding.invalid",
            isActive: true);
        var mutated = baseline.Entries.SetItem(0, mismatched);

        ProviderBindingShadowValidationReport report = Validate(mutated);

        Assert.Contains(report.Failures, failure =>
            failure.Kind == ProviderBindingShadowFailureKind.SchemaMismatch);
    }

    [Fact]
    public void Shadow_RegistryOrderMutation_IsBehaviorNeutral()
    {
        GeneratedProviderBindingShadowRegistry baseline =
            GeneratedProviderBindingShadowRegistry.FromGeneratedCatalog();
        GeneratedProviderBindingShadowRegistry reordered = baseline.ReorderedForTest();

        ProviderBindingShadowValidationReport report =
            GeneratedProviderBindingShadowValidator.Validate(reordered);

        Assert.True(report.IsValid, FormatFailures(report));
        Assert.Equal(baseline.CanonicalFingerprint(), reordered.CanonicalFingerprint());
    }

    private static ProviderBindingShadowValidationReport Validate(
        ImmutableArray<GeneratedProviderBindingShadowEntry> entries) =>
        GeneratedProviderBindingShadowValidator.Validate(
            new GeneratedProviderBindingShadowRegistry(entries));

    private static string FormatFailures(ProviderBindingShadowValidationReport report) =>
        string.Join("; ", report.Failures.Select(failure =>
            $"{failure.Kind}:{failure.Identity}:{failure.Detail}"));
}
