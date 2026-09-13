using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase61PinBasedControlsVmReadE0Tests
{
    [Fact]
    public void MachineCurrent_ClosesOnlyExactPinBasedControlsE0AsBlocked()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan");
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "VirtualizationActivationStatusV1.json")));
        JsonElement phase = status.RootElement.GetProperty("Phase61PinBasedControlsVmReadE0");

        Assert.Equal(
            "BlockedE0NoProductionConstructionOrExactBitSemanticCoverage",
            phase.GetProperty("State").GetString());
        Assert.Equal("VmcsField.PinBasedControls", phase.GetProperty("ExactField").GetString());
        Assert.Equal("CompatibilityControlDescriptor", phase.GetProperty("CandidateNeutralOwner").GetString());
        Assert.Equal("MaterializedCompatibilityEventRoutingPolicy", phase.GetProperty("CandidateValueSource").GetString());
        Assert.Equal("Absent", phase.GetProperty("ProductionCaller").GetString());
        Assert.Equal("Absent", phase.GetProperty("ExactPerBitSemanticMap").GetString());
        Assert.Equal("NotMaterialized", phase.GetProperty("D2").GetString());
        Assert.Equal("Denied", phase.GetProperty("ProductionComposition").GetString());
        Assert.Equal("None", status.RootElement.GetProperty("NextOpenPool").GetString());
        Assert.Equal(
            "NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.RootElement.GetProperty("NextCandidatePool").GetString());
    }

    [Fact]
    public void CanonicalProductionGraph_UsesOwnerIssuedDescriptorAndNeutralConsumer()
    {
        string root = Path.Combine(
            VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL");
        string descriptorPath = Path.Combine(
            root, "Core", "Runtime", "Capabilities", "CompatibilityControls",
            "CompatibilityControlDescriptor.cs");
        string ownerPath = Path.Combine(
            root, "Core", "Runtime", "Capabilities", "CompatibilityControls",
            "CompatibilityControlPolicyOwner.cs");
        string constructionPath = Path.Combine(
            root, "Core", "Architecture", "State", "Architectural",
            "CPU_Core.StateData.cs");
        string consumerPath = Path.Combine(
            root, "Core", "Architecture", "ExceptionsAndTraps",
            "CPU_Core.InterruptDispatch.cs");

        string[] descriptorReferences = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "CompatibilityControlDescriptor", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Contains(Path.GetFullPath(descriptorPath), descriptorReferences);
        Assert.Contains(Path.GetFullPath(ownerPath), descriptorReferences);

        string[] policyReferences = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "CompatibilityEventRoutingPolicy", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .ToArray();
        Assert.Contains(Path.GetFullPath(descriptorPath), policyReferences);
        Assert.Contains(Path.GetFullPath(ownerPath), policyReferences);

        string owner = File.ReadAllText(ownerPath);
        string construction = File.ReadAllText(constructionPath);
        string consumer = File.ReadAllText(consumerPath);
        Assert.Contains("CompatibilityControlDescriptor.FromNeutralSemantics", owner);
        Assert.Contains("new CompatibilityControlPolicyOwner(compatibilityPolicyProfile)", construction);
        Assert.Contains("InterruptRoutingConsumer.Validate", consumer);
        Assert.DoesNotContain("VmcsField", owner + consumer);
        Assert.DoesNotContain("VmRead", owner + consumer);
    }

    [Fact]
    public void NeutralPolicy_DoesNotDefineFrozenPinBasedControlBitsOrZeroFallback()
    {
        string descriptor = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs");
        string projection = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");

        Assert.Contains("RuntimeTrapPolicyRequired", descriptor);
        Assert.Contains("NeutralTrapResultRequired", descriptor);
        Assert.Contains("PublicationFenceRequired", descriptor);
        Assert.DoesNotContain("VmcsField", descriptor);
        Assert.DoesNotContain("PinBasedControls", descriptor);
        Assert.DoesNotContain("ProjectPinBased", descriptor + projection);
        Assert.Contains("CompatibilityControlValueProjectionDenied", projection);
        Assert.Contains("no frozen VMX control-bit value projection contract", projection);
        Assert.DoesNotContain("VmcsField.PinBasedControls =>", projection);
    }

    [Fact]
    public void BlockedRecord_DeniesD2CompositionAdjacentFieldsAndSurrogateAuthority()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan");
        string document = File.ReadAllText(Path.Combine(
            plan, "61_pin_based_controls_vmread_e0_blocked.md"));
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "PinBasedControlsVmReadE0BlockedRecordV1.json")));

        Assert.Contains("BLOCKED E0 / NO D2 / NO PRODUCTION COMPOSITION", document);
        Assert.Contains("schema membership", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zero fallback", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No adjacent field opens", document);
        Assert.Equal("Blocked", record.RootElement.GetProperty("E0").GetProperty("Verdict").GetString());
        Assert.Equal("NotMaterialized", record.RootElement.GetProperty("D2").GetProperty("State").GetString());
        Assert.Equal("Denied", record.RootElement.GetProperty("D2").GetProperty("ProductionComposition").GetString());
        Assert.Equal("NoneAdded", record.RootElement.GetProperty("RuntimeAuthority").GetString());
        Assert.Contains(record.RootElement.GetProperty("Excluded").EnumerateArray(),
            item => item.GetString()!.Contains("SecondaryProcControls", StringComparison.Ordinal));
    }
}
