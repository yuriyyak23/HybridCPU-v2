using System.Text.Json;
using Xunit;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase40VmReadStatusTests
{
    [Fact]
    public void CurrentStatus_RecordsMachineAcceptedGovernanceWithoutOpeningImplementation()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan", "VirtualizationActivationStatusV1.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement vmread = document.RootElement.GetProperty("VmReadD2");

        Assert.Equal("MachineAcceptedGovernanceOnly", vmread.GetProperty("State").GetString());
        Assert.Equal("D2-HV-VMREAD-PROJECTION-V1-GUEST-CR0-CR4-0001",
            vmread.GetProperty("DecisionId").GetString());
        Assert.Equal("4d3b5b97c22661652c94357319e2e6b16615cceb",
            vmread.GetProperty("SpecCommitSha").GetString());
        Assert.Equal("5c1956f9ea580265f3f5a96afeaf3378a7dda6c6",
            vmread.GetProperty("AcceptanceCommitSha").GetString());
        Assert.Equal(new[] { "VmcsField.GuestCr0", "VmcsField.GuestCr4" },
            vmread.GetProperty("ExactFields").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("None", vmread.GetProperty("CapabilityRequirement").GetString());
        Assert.Equal("ReadOnly", vmread.GetProperty("MutationClass").GetString());
        Assert.Equal("None", vmread.GetProperty("RuntimeAuthority").GetString());
        Assert.Equal("NotAuthorized", vmread.GetProperty("ProductionImplementation").GetString());
        Assert.Equal("Denied", vmread.GetProperty("AdjacentFields").GetString());
        Assert.Equal("NoneUntilSeparateNeutralCpuTranslationFaultCompletionProducerAuthorization",
            document.RootElement.GetProperty("NextCandidatePool").GetString());
    }
}
