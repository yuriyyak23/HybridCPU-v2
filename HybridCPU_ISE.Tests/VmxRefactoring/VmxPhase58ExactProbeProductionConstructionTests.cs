using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using System.Text.Json;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPhase58ExactProbeProductionConstructionTests
{
    [Fact]
    public void MachineCurrent_ClosesPoolWithoutOpeningReleaseOrVmRead()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string status = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "VirtualizationActivationStatusV1.json"));
        string phase = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "58_exact_probe_production_construction_reachability_and_rollback.md"));

        Assert.Contains("\"NextOpenPool\": \"None\"", status);
        Assert.Contains("NoneUntilSeparateOwnerAuthorizationForNextExactFieldOrOperation", status);
        Assert.Contains("CPUCoreInitializePipelineToExistingCanonicalMicroOpScheduler", status);
        Assert.Contains("VMREAD, VMWRITE, nested, IOMMU/DMA/device, SecureCompute or compiler", phase);
        Assert.Contains("does not release another profile", phase);
    }

    [Fact]
    public void LaterEvidence_BindsExactSubjectAndKeepsEveryLaterPoolExcluded()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string path = Path.Combine(
            root, "HybridCPU_ISE", "docs", "ref2", "VirtualizationActivationPlan",
            "evidence", "2026-08-13-phase58-exact-probe-production-construction-clean-evidence.json");
        using JsonDocument evidence = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement record = evidence.RootElement;

        Assert.Equal("e29d6b2150bf10c136ba3897eb17a9cab03c9967",
            record.GetProperty("SubjectCommitSha").GetString());
        Assert.Equal("b3cd41275e548dc39fc7c0c7c5d411c339a0f29e",
            record.GetProperty("SubjectTreeSha").GetString());
        Assert.Equal("LaterNonSelfReferentialEvidenceOnlyNeverRuntimeAuthority",
            record.GetProperty("RecordRole").GetString());
        Assert.Equal("None", record.GetProperty("NextOpenPool").GetString());
        Assert.Equal(7, record.GetProperty("Excluded").GetArrayLength());
        Assert.False(record.GetProperty("ForbiddenAuthority").EnumerateObject()
            .Any(property => property.Value.GetBoolean()));
    }

    [Fact]
    public void DefaultConstruction_RemainsUnboundAndFaultOnly()
    {
        var core = new Processor.CPU_Core(
            0,
            CpuCorePlatformContext.CreateFixed(
                new Processor.MainMemoryArea(),
                ProcessorMode.Emulation));

        core.InitializePipeline();

        Assert.False(core.HasActiveConfiguredExactProbeRuntimeProfile);
    }

    [Fact]
    public void ExplicitReviewedProfile_ReachesCanonicalProductionScheduler()
    {
        var profile = new CpuExactProbeRuntimeConstructionProfile(
            domainTag: 7,
            CpuExactProbeRuntimeConstructionProfile.ProvenContourSubjectCommitSha);
        var core = new Processor.CPU_Core(
            0,
            CpuCorePlatformContext.CreateFixed(
                new Processor.MainMemoryArea(),
                ProcessorMode.Emulation,
                exactProbeRuntimeProfile: profile));

        core.InitializePipeline();

        Assert.True(core.HasActiveConfiguredExactProbeRuntimeProfile);
    }

    [Fact]
    public void Construction_DeniesUnknownOrMalformedContourSha()
    {
        Assert.Throws<ArgumentException>(() =>
            new CpuExactProbeRuntimeConstructionProfile(7, new string('0', 40)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CpuExactProbeRuntimeConstructionProfile(
                0,
                CpuExactProbeRuntimeConstructionProfile.ProvenContourSubjectCommitSha));
    }

    [Fact]
    public void Reinitialization_DoesNotReplaceOrDuplicateLiveExactBinding()
    {
        var core = CreateExactCore();
        core.InitializePipeline();
        Assert.True(core.HasActiveConfiguredExactProbeRuntimeProfile);

        core.InitializePipeline();

        Assert.True(core.HasActiveConfiguredExactProbeRuntimeProfile);
    }

    [Fact]
    public void OperationalRollback_RestoresFaultOnlyAndCannotImplicitlyReactivate()
    {
        var core = CreateExactCore();
        core.InitializePipeline();

        Assert.True(core.DisableConfiguredExactProbeRuntimeProfile(TimeSpan.FromSeconds(5)));
        Assert.False(core.HasActiveConfiguredExactProbeRuntimeProfile);
        Assert.Throws<InvalidOperationException>(() => core.InitializePipeline());
        Assert.True(core.DisableConfiguredExactProbeRuntimeProfile(TimeSpan.Zero));
    }

    [Fact]
    public async Task InitializeVersusRollbackRace_NeverReactivatesAfterKillSwitch()
    {
        var core = CreateExactCore();
        core.InitializePipeline();

        Task initialize = Task.Run(() =>
        {
            try
            {
                core.InitializePipeline();
            }
            catch (InvalidOperationException)
            {
                // Rollback may linearize first; construction then fails closed.
            }
        });
        Task<bool> rollback = Task.Run(() =>
            core.DisableConfiguredExactProbeRuntimeProfile(TimeSpan.FromSeconds(5)));

        await Task.WhenAll(initialize, rollback);

        Assert.True(await rollback);
        Assert.False(core.HasActiveConfiguredExactProbeRuntimeProfile);
        Assert.Throws<InvalidOperationException>(() => core.InitializePipeline());
    }

    [Fact]
    public void ProductionSourceOwnsConstructionCallAndKeepsCompatibilityOut()
    {
        string root = VmxDocumentationMigrationClaimHygieneTests.FindRepositoryRoot();
        string pipeline = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Stages", "Issue",
            "CPU_Core.Pipeline.cs"));
        string retire = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.VmxRetire.cs"));
        string context = File.ReadAllText(Path.Combine(
            root, "HybridCPU_ISE", "CloseToHSL", "Core", "State",
            "CpuCorePlatformContext.cs"));
        int exactProfileStart = context.IndexOf(
            "public readonly record struct CpuExactProbeRuntimeConstructionProfile",
            StringComparison.Ordinal);
        int nextProfileStart = context.IndexOf(
            "/// Immutable construction-only binding for the exact released read-only",
            exactProfileStart,
            StringComparison.Ordinal);
        string exactConstructionContract = context[exactProfileStart..nextProfileStart];

        Assert.Contains("EnsureConfiguredExactProbeRuntimeProfile(scheduler);", pipeline);
        Assert.Contains("new Core.DomainHypercallExactRuntimeProfile(", retire);
        Assert.Contains("profile.KillSwitch(transitionTimeout)", retire);
        Assert.DoesNotContain("Vmcs", exactConstructionContract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VmRead", exactConstructionContract, StringComparison.OrdinalIgnoreCase);
    }

    private static Processor.CPU_Core CreateExactCore() => new(
        0,
        CpuCorePlatformContext.CreateFixed(
            new Processor.MainMemoryArea(),
            ProcessorMode.Emulation,
            exactProbeRuntimeProfile: new CpuExactProbeRuntimeConstructionProfile(
                7,
                CpuExactProbeRuntimeConstructionProfile.ProvenContourSubjectCommitSha)));
}
