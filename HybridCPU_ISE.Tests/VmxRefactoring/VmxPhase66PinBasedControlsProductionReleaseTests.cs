using HybridCPU_ISE.Arch;
using System.Text.Json;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase66PinBasedControlsProductionReleaseTests
{
    private const CompatibilityEventRoutingPolicy FullPolicy =
        CompatibilityEventRoutingPolicy.RuntimeTrapPolicyRequired |
        CompatibilityEventRoutingPolicy.NeutralTrapResultRequired |
        CompatibilityEventRoutingPolicy.PublicationFenceRequired;

    [Fact]
    public void ExactImmutableConstruction_BindsAllFrozenDecisionsAndReviewedPhase65Subject()
    {
        PinBasedControlsVmReadProductionConstructionProfile profile =
            PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7);

        Assert.True(profile.IsConfigured);
        Assert.False(profile.RuntimeAuthorityGranted);
        Assert.Equal(7UL, profile.DomainIdentity);
        Assert.Equal("ABI-HV-VMX8-PIN-BASED-CONTROLS-0001",
            PinBasedControlsVmReadProductionConstructionProfile.FrozenAbiDecisionId);
        Assert.Equal("D2-HV-VMREAD-PIN-BASED-CONTROLS-0005",
            PinBasedControlsVmReadProductionConstructionProfile.ProjectionDecisionId);
        Assert.Equal("D2-HV-VMREAD-PIN-BASED-CONTROLS-SCALAR-0006",
            PinBasedControlsVmReadProductionConstructionProfile.ScalarDeliveryDecisionId);
        Assert.Equal("bcb10489762c4a560813be8efac299408d8f2906",
            PinBasedControlsVmReadProductionConstructionProfile.ReviewedImplementationSubjectSha);
        Assert.Equal("e5db97f9dcee4cab0d6114fcc85a00f305339011",
            PinBasedControlsVmReadProductionConstructionProfile.ReviewedImplementationSubjectTree);
    }

    [Fact]
    public void ForgedDecisionOrSubjectIdentity_IsRejectedBeforeCoreConstruction()
    {
        Assert.Throws<ArgumentException>(() => Profile(frozenAbi: "forged"));
        Assert.Throws<ArgumentException>(() => Profile(projection: "forged"));
        Assert.Throws<ArgumentException>(() => Profile(scalar: "forged"));
        Assert.Throws<ArgumentException>(() => Profile(subjectSha: new string('0', 40)));
        Assert.Throws<ArgumentException>(() => Profile(subjectTree: new string('0', 40)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PinBasedControlsVmReadProductionConstructionProfile.CreateExact(0));
    }

    [Fact]
    public void ProductionBootstrap_IsExplicitDefaultDisabledAndExactDomainBound()
    {
        var memory = Memory();
        CpuCorePlatformContext defaults = CpuCorePlatformContext.CreateFixed(
            memory,
            ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(7, FullPolicy));
        Assert.False(defaults.EnablePinBasedControlsVmRead);
        Assert.Null(defaults.PinBasedControlsVmReadProfile);

        CpuCorePlatformContext exact = CpuCorePlatformContext.CreateFixed(
            memory,
            ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(7, FullPolicy),
            pinBasedControlsVmReadProfile:
                PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7));
        Assert.True(exact.EnablePinBasedControlsVmRead);
        Assert.True(exact.PinBasedControlsVmReadProfile!.Value.IsConfigured);
        Assert.True(new Processor.CPU_Core(0, exact).HasActivePinBasedControlsVmReadProfile);

        Assert.Throws<ArgumentException>(() => CpuCorePlatformContext.CreateFixed(
            memory,
            ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(8, FullPolicy),
            pinBasedControlsVmReadProfile:
                PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7)));
    }

    [Fact]
    public void KillSwitch_IsGenerationBoundAndHotReactivationHasNoApi()
    {
        Processor.CPU_Core core = CreateReleasedCore();
        Assert.True(core.HasActivePinBasedControlsVmReadProfile);
        Assert.True(core.DisablePinBasedControlsVmReadProfile());
        Assert.False(core.HasActivePinBasedControlsVmReadProfile);
        Assert.True(core.DisablePinBasedControlsVmReadProfile());
        Assert.False(core.HasActivePinBasedControlsVmReadProfile);

        Assert.DoesNotContain(
            typeof(Processor.CPU_Core).GetMethods(),
            method => method.Name.Contains("EnablePinBasedControls", StringComparison.Ordinal));

        Processor.CPU_Core replacement = CreateReleasedCore();
        Assert.True(replacement.HasActivePinBasedControlsVmReadProfile);
        Assert.NotSame(core, replacement);
    }

    [Fact]
    public void ProductionSources_DoNotLoadReleaseEvidenceOrOpenAdjacentAuthority()
    {
        string platform = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/State/CpuCorePlatformContext.cs");
        string composition = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Runtime/Events/VmRead/PinBasedControlsVmReadScalarDeliveryCanonicalComposition.cs");
        string materialization = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToHSL/Core/Pipeline/ExecutionFlow/Materialization/CPU_Core.PipelineExecution.Materialization.cs");

        Assert.Contains("PinBasedControlsVmReadProductionConstructionProfile", platform);
        Assert.Contains("EnablePinBasedControlsVmRead = true", platform);
        Assert.DoesNotContain("ProductionReleaseRecord", platform + composition + materialization);
        Assert.DoesNotContain("File.ReadAllText", platform + composition + materialization);
        Assert.DoesNotContain("ProcBasedControls", composition + materialization);
        Assert.DoesNotContain("SecondaryProcControls", composition + materialization);
        Assert.DoesNotContain("VMWRITE", composition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VMCALL", composition, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseRecord_BindsReviewedImplementationAndSeparateCleanCandidate()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string plan = Path.Combine(root, "HybridCPU_ISE", "docs", "ref2",
            "VirtualizationActivationPlan");
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "PinBasedControlsExactProductionReleaseRecordV1.json")));
        using JsonDocument status = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            plan, "VirtualizationActivationStatusV1.json")));

        JsonElement release = record.RootElement;
        Assert.Equal("ImmutableReleaseEvidenceOnlyNeverRuntimeAuthority",
            release.GetProperty("RecordRole").GetString());
        Assert.Equal("bcb10489762c4a560813be8efac299408d8f2906",
            release.GetProperty("ReviewedImplementationSubjectSha").GetString());
        Assert.Equal("e5db97f9dcee4cab0d6114fcc85a00f305339011",
            release.GetProperty("ReviewedImplementationSubjectTree").GetString());
        Assert.Equal("b424a224afd07146080a0f174fc3e12eec0a043a",
            release.GetProperty("ReleaseCandidateSubjectSha").GetString());
        Assert.Equal("f12dbaa96e356531ea5840e31f7137767a3cae8a",
            release.GetProperty("ReleaseCandidateSubjectTree").GetString());
        Assert.All(release.GetProperty("Authority").EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.False),
            property => Assert.False(property.Value.GetBoolean()));
        Assert.Equal("DisabledAbsentProfile",
            release.GetProperty("Construction").GetProperty("Default").GetString());
        Assert.Equal("DeniedNewExplicitConstructionRequired",
            release.GetProperty("Rollback").GetProperty("HotReactivation").GetString());

        Assert.Equal("None", status.RootElement.GetProperty("NextOpenPool").GetString());
        Assert.Equal("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation",
            status.RootElement.GetProperty("NextCandidatePool").GetString());
        Assert.Equal("CompatibilityControlPolicyOwnerOnly",
            status.RootElement.GetProperty("Phase66PinBasedControlsExactProductionRelease")
                .GetProperty("RuntimeAuthority").GetString());
    }

    private static Processor.CPU_Core CreateReleasedCore() => new(
        0,
        CpuCorePlatformContext.CreateFixed(
            Memory(),
            ProcessorMode.Emulation,
            compatibilityControlPolicyProfile:
                new CompatibilityControlPolicyConstructionProfile(7, FullPolicy),
            pinBasedControlsVmReadProfile:
                PinBasedControlsVmReadProductionConstructionProfile.CreateExact(7)));

    private static PinBasedControlsVmReadProductionConstructionProfile Profile(
        string? frozenAbi = null,
        string? projection = null,
        string? scalar = null,
        string? subjectSha = null,
        string? subjectTree = null) => new(
            7,
            frozenAbi ?? PinBasedControlsVmReadProductionConstructionProfile.FrozenAbiDecisionId,
            projection ?? PinBasedControlsVmReadProductionConstructionProfile.ProjectionDecisionId,
            scalar ?? PinBasedControlsVmReadProductionConstructionProfile.ScalarDeliveryDecisionId,
            subjectSha ?? PinBasedControlsVmReadProductionConstructionProfile.ReviewedImplementationSubjectSha,
            subjectTree ?? PinBasedControlsVmReadProductionConstructionProfile.ReviewedImplementationSubjectTree);

    private static Processor.MultiBankMemoryArea Memory()
    {
        var memory = new Processor.MultiBankMemoryArea(1, 0x1000);
        memory.SetLength(0x1000);
        return memory;
    }
}
