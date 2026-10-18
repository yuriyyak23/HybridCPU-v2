using System.Text.Json;
using Xunit;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase40VmReadD2VerificationRecordTests
{
    [Fact]
    public void VerificationRecord_ClosesOnlyE0D2GovernanceAndNamesExactEarlierProvenance()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "evidence",
            "2026-08-11-phase40-guest-cr0-cr4-d2-verification.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = document.RootElement;
        JsonElement spec = record.GetProperty("spec_provenance");
        JsonElement acceptance = record.GetProperty("acceptance_provenance");
        JsonElement closure = record.GetProperty("closure");

        Assert.Equal("4d3b5b97c22661652c94357319e2e6b16615cceb",
            spec.GetProperty("commit_sha").GetString());
        Assert.Equal("0cc6c74cb985b5455d05de89b8b4b6ab28483122",
            spec.GetProperty("tree_sha").GetString());
        Assert.Equal("5c1956f9ea580265f3f5a96afeaf3378a7dda6c6",
            acceptance.GetProperty("commit_sha").GetString());
        Assert.Equal("f83f9f7576f458e08ac475d9580c48ec7e52d138",
            acceptance.GetProperty("tree_sha").GetString());
        Assert.True(acceptance.GetProperty("spec_commit_is_earlier").GetBoolean());
        Assert.False(acceptance.GetProperty("acceptance_record_contains_own_commit_sha").GetBoolean());
        Assert.Equal("Closed", closure.GetProperty("e0").GetString());
        Assert.Equal("MachineAcceptedGovernanceOnly", closure.GetProperty("d2").GetString());
        Assert.True(closure.GetProperty("one_d2_for_both_fields").GetBoolean());
        Assert.Equal("NotAuthorized",
            closure.GetProperty("production_vmread_implementation").GetString());

        foreach (string denied in new[]
        {
            "runtime_authority_granted",
            "projection_value_available_from_governance",
            "capability_granted",
            "compatibility_frontend_authority_granted",
            "backend_execution_authorized",
            "authoritative_state_mutation_authorized",
            "completion_publication_authorized",
            "retire_publication_authorized",
            "vmwrite_authorized",
            "probe_no_state_v1_authority_reused",
            "adjacent_fields_authorized",
        })
        {
            Assert.False(closure.GetProperty(denied).GetBoolean(), denied);
        }

        Assert.Contains(record.GetProperty("commands_and_results").EnumerateArray(), command =>
            command.TryGetProperty("result", out JsonElement result) &&
            result.GetString() == "passed 70/70 checks");
        Assert.Equal("not_claimed", record.GetProperty("baseline_provenance_note")
            .GetProperty("full_repository_clean_checkout_build").GetString());
        Assert.Equal("separately authorized production implementation/projection pool only",
            record.GetProperty("next_pool").GetString());
    }
}
