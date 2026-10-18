using System;
using System.IO;
using System.Text.Json;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-05 aggregate evidence gate. It joins the committed C#-driven corpus
/// locks; it is not a second decoder authority and it does not approve facade
/// cutover by itself.
/// </summary>
public sealed class DecoderRf05AggregateCoverageTests
{
    private sealed record AggregateReport(
        int SchemaVersion,
        string Scope,
        int GeneratedDescriptorCount,
        int GeneratedVectorPayloadDescriptorCount,
        int StaticConstraintFamilyCount,
        int StaticAcceptedMutations,
        int StaticRejectedMutations,
        int SidebandAcceptedBundles,
        int SidebandRejectedBundles,
        int KnownLegacyRejectFamiliesWithoutOwner,
        int SemanticProjectionDifferences);

    [Fact]
    public void AggregateReport_ClosesTheKnownStaticCoverageAccountingWithoutGrantingFacadeCutover()
    {
        AggregateReport? report = JsonSerializer.Deserialize<AggregateReport>(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "Documentation",
                "Documentation",
                "ArchitectureAuthorityRefactor",
                "Evidence",
                "RF05",
                "rf05-aggregate-differential-report.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(report);
        Assert.Equal(1, report!.SchemaVersion);
        Assert.Equal("known static encoding legality and canonical projection evidence", report.Scope);
        Assert.Equal(250, report.GeneratedDescriptorCount);
        Assert.Equal(71, report.GeneratedVectorPayloadDescriptorCount);
        Assert.Equal(
            EncodingConstraintValidator.RegisteredRawFormConstraintIds.Count +
            EncodingConstraintValidator.RegisteredConstraintIds.Count,
            report.StaticConstraintFamilyCount);
        Assert.Equal(20, report.StaticAcceptedMutations);
        Assert.Equal(26, report.StaticRejectedMutations);
        Assert.Equal(5, report.SidebandAcceptedBundles);
        Assert.Equal(13, report.SidebandRejectedBundles);
        Assert.Equal(0, report.KnownLegacyRejectFamiliesWithoutOwner);
        Assert.Equal(0, report.SemanticProjectionDifferences);
    }

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
