using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase64PinBasedControlsE0D2Tests
{
    private const CompatibilityEventRoutingPolicy FullPolicy =
        CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
        CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
        CompatibilityEventRoutingPolicy.PublicationFenceRequired;

    [Fact]
    public void E0_ProductionConstructionConsumerAndFreshnessGraphIsReal()
    {
        string platform = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/State/CpuCorePlatformContext.cs");
        string construction = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.StateData.cs");
        string consumer = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/ExceptionsAndTraps/CPU_Core.InterruptDispatch.cs");
        string restore = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Architecture/State/Architectural/CPU_Core.State.cs");

        Assert.Contains("CompatibilityControlPolicyConstructionProfile?", platform);
        Assert.Contains("new CompatibilityControlPolicyOwner(compatibilityPolicyProfile)", construction);
        Assert.Contains("InterruptRoutingConsumer.Validate", consumer);
        Assert.Contains("InvalidateAfterRestore", restore);
        Assert.DoesNotContain("VmcsField", construction + consumer + restore);
        Assert.DoesNotContain("VmRead", consumer + restore);

        Processor.CPU_Core core = CreateCore(6401, FullPolicy);
        CompatibilityControlPolicySnapshot snapshot = Assert.IsType<CompatibilityControlPolicySnapshot>(
            core.CaptureCompatibilityControlPolicy());
        Assert.True(core.CompatibilityControlPolicyOwner!.IsCurrent(snapshot));
        Assert.Equal(6401UL, snapshot.Identity.DomainIdentity);
        Assert.NotEqual(0UL, snapshot.Identity.PolicyGeneration);

        byte calls = 0;
        core.TestSetInterruptDispatcher((_, _, _) => { calls++; return 1; });
        Assert.Equal((byte)1, core.DispatchInterrupt(default, 1));
        Assert.Equal((byte)1, calls);
    }

    [Fact]
    public void E0_FrozenDecisionHasExactWholeFieldSemanticCoverage()
    {
        using JsonDocument decision = ReadDecision();
        JsonElement root = decision.RootElement;
        JsonElement[] bits = root.GetProperty("Bits").EnumerateArray().ToArray();
        Assert.Equal(3, bits.Length);
        Assert.Equal("3..63MustReadZeroAndCannotBeInferred",
            root.GetProperty("UnsupportedBits").GetString());

        for (ulong mask = 1; mask <= 7; mask++)
        {
            CompatibilityEventRoutingPolicy policy = (CompatibilityEventRoutingPolicy)mask;
            Processor.CPU_Core core = CreateCore(6500 + mask, policy);
            CompatibilityControlPolicySnapshot snapshot = core.CaptureCompatibilityControlPolicy()!;

            ulong projected = 0;
            if ((snapshot.EventRoutingPolicy & CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired) != 0)
                projected |= 1UL << 0;
            if ((snapshot.EventRoutingPolicy & CompatibilityEventRoutingPolicy.NeutralTrapResultRequired) != 0)
                projected |= 1UL << 1;
            if ((snapshot.EventRoutingPolicy & CompatibilityEventRoutingPolicy.PublicationFenceRequired) != 0)
                projected |= 1UL << 2;

            Assert.Equal(mask, projected);
            Assert.Equal(0UL, projected & ~0x7UL);
        }
    }

    [Fact]
    public void E0_AbsentStaleForeignAndCrossDomainSourcesDenyWithoutZeroFallback()
    {
        var first = new CompatibilityControlPolicyOwner(
            new CompatibilityControlPolicyConstructionProfile(6601, FullPolicy));
        var second = new CompatibilityControlPolicyOwner(
            new CompatibilityControlPolicyConstructionProfile(6602, FullPolicy));
        CompatibilityControlPolicySnapshot stale = first.CaptureCurrent();
        first.InvalidateAfterRestore();

        Assert.False(first.IsCurrent(null));
        Assert.False(first.IsCurrent(stale));
        Assert.False(second.IsCurrent(first.CaptureCurrent()));
        Assert.NotEqual(first.CaptureCurrent().Identity.DomainIdentity,
            second.CaptureCurrent().Identity.DomainIdentity);

        string projection = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");
        Assert.DoesNotContain("VmcsField.PinBasedControls =>", projection);
        Assert.Contains("CompatibilityControlValueProjectionDenied", projection);
    }

    [Fact]
    public void D2_MapsOnlyExactCurrentOwnerSnapshotAndPreservesAbsentVersusZero()
    {
        for (ulong mask = 1; mask <= 7; mask++)
        {
            var owner = new CompatibilityControlPolicyOwner(
                new CompatibilityControlPolicyConstructionProfile(
                    6700 + mask,
                    (CompatibilityEventRoutingPolicy)mask));
            PinBasedControlsProjectionResult result =
                Phase64PinBasedControlsVmReadProjectionD2.Project(
                    owner, owner.CaptureCurrent());

            Assert.True(result.IsGranted);
            Assert.True(result.Value.HasValue);
            Assert.Equal(mask, result.Value.Value);
            Assert.Equal(0UL, result.Value.Value &
                Phase64PinBasedControlsVmReadProjectionD2.UnsupportedMask);
        }

        PinBasedControlsProjectionResult missing =
            Phase64PinBasedControlsVmReadProjectionD2.Project(null, null);
        Assert.False(missing.IsGranted);
        Assert.Null(missing.Value);
        Assert.Equal(PinBasedControlsProjectionDecision.DeniedMissingOwnerOrSnapshot,
            missing.Decision);
    }

    [Fact]
    public void D2_DeniesStaleForeignCrossDomainAndHasNoProductionComposition()
    {
        var owner = new CompatibilityControlPolicyOwner(
            new CompatibilityControlPolicyConstructionProfile(6801, FullPolicy));
        var foreign = new CompatibilityControlPolicyOwner(
            new CompatibilityControlPolicyConstructionProfile(6802, FullPolicy));
        CompatibilityControlPolicySnapshot stale = owner.CaptureCurrent();
        owner.InvalidateAfterRestore();

        PinBasedControlsProjectionResult staleResult =
            Phase64PinBasedControlsVmReadProjectionD2.Project(owner, stale);
        PinBasedControlsProjectionResult foreignResult =
            Phase64PinBasedControlsVmReadProjectionD2.Project(
                foreign, owner.CaptureCurrent());
        Assert.False(staleResult.IsGranted);
        Assert.Null(staleResult.Value);
        Assert.False(foreignResult.IsGranted);
        Assert.Null(foreignResult.Value);
        Assert.False(Phase64PinBasedControlsVmReadProjectionD2.RuntimeAuthorityGranted);
        Assert.False(Phase64PinBasedControlsVmReadProjectionD2.ProductionCompositionAuthorized);
        Assert.Equal(VmcsField.PinBasedControls,
            Phase64PinBasedControlsVmReadProjectionD2.ExactField);

        string frontend = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs");
        Assert.DoesNotContain("Phase64PinBasedControlsVmReadProjectionD2", frontend);
        Assert.DoesNotContain("VmcsField.PinBasedControls =>", frontend);
    }

    [Fact]
    public void D2_ArtifactsAreDigestBoundAndDenyDeliveryActivationAndAdjacentFields()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan");
        string specPath = Path.Combine(plan, "PinBasedControlsVmReadDecisionSpecV2.json");
        using JsonDocument spec = JsonDocument.Parse(File.ReadAllText(specPath));
        using JsonDocument acceptance = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "PinBasedControlsVmReadAcceptanceRecordV2.json")));

        string canonicalSpec = File.ReadAllText(specPath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonicalSpec))).ToLowerInvariant();
        Assert.Equal(digest,
            acceptance.RootElement.GetProperty("DecisionSpecCanonicalLfSha256").GetString());
        Assert.Equal("D2-HV-VMREAD-PIN-BASED-CONTROLS-0005",
            spec.RootElement.GetProperty("DecisionId").GetString());
        Assert.Equal("NotAuthorized",
            acceptance.RootElement.GetProperty("ProductionComposition").GetString());
        Assert.Equal("NotAuthorized",
            acceptance.RootElement.GetProperty("ScalarDelivery").GetString());
        Assert.Equal("Denied",
            acceptance.RootElement.GetProperty("AdjacentFields").GetString());
    }

    private static Processor.CPU_Core CreateCore(
        ulong domainIdentity,
        CompatibilityEventRoutingPolicy policy) =>
        new(
            0,
            CpuCorePlatformContext.CreateFixed(
                new Processor.MainMemoryArea(),
                ProcessorMode.Emulation,
                compatibilityControlPolicyProfile:
                    new CompatibilityControlPolicyConstructionProfile(domainIdentity, policy)));

    private static JsonDocument ReadDecision()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "PinBasedControlsFrozenAbiDecisionV1.json")));
    }
}
