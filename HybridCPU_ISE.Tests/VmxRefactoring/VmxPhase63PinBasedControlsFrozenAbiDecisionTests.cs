using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase63PinBasedControlsFrozenAbiDecisionTests
{
    [Fact]
    public void FrozenDecision_CoversEveryBitWithExactPredicateOrUnsupportedZero()
    {
        using JsonDocument document = ReadDecision();
        JsonElement root = document.RootElement;

        Assert.Equal("ABI-HV-VMX8-PIN-BASED-CONTROLS-0001",
            root.GetProperty("DecisionId").GetString());
        Assert.Equal("VmcsField.PinBasedControls",
            root.GetProperty("ExactField").GetString());
        Assert.Equal("HybridCPU VMX8", root.GetProperty("CompatibilityAbi").GetString());
        Assert.Equal("CompatibilityControlPolicyOwner",
            root.GetProperty("NeutralOwner").GetString());

        JsonElement[] bits = root.GetProperty("Bits").EnumerateArray().ToArray();
        Assert.Equal(new[] { 0, 1, 2 },
            bits.Select(bit => bit.GetProperty("Bit").GetInt32()).ToArray());
        Assert.Equal(new[]
        {
            "CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired",
            "CompatibilityEventRoutingPolicy.NeutralTrapResultRequired",
            "CompatibilityEventRoutingPolicy.PublicationFenceRequired"
        }, bits.Select(bit => bit.GetProperty("NeutralPredicate").GetString()).ToArray());
        Assert.All(bits, bit => Assert.StartsWith("Legal:",
            bit.GetProperty("Zero").GetString(), StringComparison.Ordinal));

        Assert.Equal("3..63MustReadZeroAndCannotBeInferred",
            root.GetProperty("UnsupportedBits").GetString());
        Assert.Equal("Bits0To2ExactNeutralPredicatesBits3To63UnsupportedZero",
            root.GetProperty("WholeFieldCoverage").GetString());
        Assert.Equal("ExplicitDenialNoZeroFallback",
            root.GetProperty("AbsentOrStaleOwnerSnapshot").GetString());
    }

    [Fact]
    public void FrozenDecision_IsGovernanceOnlyAndDoesNotOpenD2OrComposition()
    {
        using JsonDocument document = ReadDecision();
        JsonElement root = document.RootElement;

        Assert.Equal("GovernanceOnlyFrozenCompatibilityMappingNeverRuntimeAuthority",
            root.GetProperty("DecisionRole").GetString());
        Assert.Equal("None", root.GetProperty("RuntimeAuthority").GetString());
        Assert.Equal("NotMaterializedUntilRepeatedE0Green",
            root.GetProperty("D2").GetString());
        Assert.Equal("NotAuthorized",
            root.GetProperty("ProductionVmReadComposition").GetString());
        Assert.Equal("Denied", root.GetProperty("AdjacentFields").GetString());

        string production = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");
        Assert.DoesNotContain("VmcsField.PinBasedControls =>", production);
        Assert.DoesNotContain("PinBasedControlsFrozenAbiDecision", production);
    }

    private static JsonDocument ReadDecision()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root,
            "HybridCPU_ISE",
            "docs",
            "ref2",
            "VirtualizationActivationPlan",
            "PinBasedControlsFrozenAbiDecisionV1.json")));
    }
}
