using System.Reflection;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf114TelemetryStateExtractionTests
{
    private const BindingFlags InstanceDeclared =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void RuntimeContainsExactlyOneTelemetryDomain()
    {
        Type root = RequiredType("YAKSys_Hybrid_CPU.Core.CoreRuntimeState");
        Type telemetry = RequiredType("YAKSys_Hybrid_CPU.Core.TelemetryState");

        FieldInfo contained = Assert.Single(
            root.GetFields(InstanceDeclared),
            field => field.FieldType == telemetry);
        Assert.Equal(telemetry, contained.FieldType);
        Assert.NotEmpty(telemetry.GetFields(InstanceDeclared));
        Assert.DoesNotContain(telemetry.GetMethods(InstanceDeclared), method =>
            new[] { "Commit", "Rollback", "Publish", "Execute", "Migrate" }
                .Any(name => method.Name.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void PipelineControlRetainsFunctionalFieldsButCounterStorageMoved()
    {
        Type control = typeof(Processor.CPU_Core.PipelineControl);
        string[] fields = control.GetFields(InstanceDeclared)
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("Enabled", fields);
        Assert.Contains("Stalled", fields);
        Assert.Contains("StallReason", fields);
        Assert.Contains("ClusterPreparedModeEnabled", fields);
        Assert.Contains("_telemetry", fields);
        Assert.DoesNotContain("CycleCount", fields);
        Assert.DoesNotContain("InstructionsRetired", fields);
        Assert.DoesNotContain("ScalarIssueWidthHistogram", fields);

        Assert.Equal(typeof(ulong), control.GetProperty("CycleCount")?.PropertyType);
        Assert.Equal(typeof(ulong), control.GetProperty("InstructionsRetired")?.PropertyType);
        Assert.Equal(typeof(ulong[]), control.GetProperty("ScalarIssueWidthHistogram")?.PropertyType);
    }

    [Fact]
    public void TransitionalCoreCopyAliasesTelemetryButSnapshotIsDetached()
    {
#pragma warning disable CS0618
        var core = new Processor.CPU_Core(0);
#pragma warning restore CS0618
        Processor.CPU_Core copy = core;

        // RF-11 TEST-ONLY mutation: proves containment identity, not a production contract.
        core.Runtime.Telemetry.CycleCount = 7;
        Assert.Equal(7UL, copy.Runtime.Telemetry.CycleCount);

        var control = new Processor.CPU_Core.PipelineControl();
        control.Clear();
        control.CycleCount = 11;
        control.ScalarIssueWidthHistogram[0] = 3;
        Processor.CPU_Core.PipelineControl snapshot = control.CreateSnapshot();
        control.CycleCount = 12;
        control.ScalarIssueWidthHistogram[0] = 4;

        Assert.Equal(11UL, snapshot.CycleCount);
        Assert.Equal(3UL, snapshot.ScalarIssueWidthHistogram[0]);
    }

    [Fact]
    public void LegacyCoreTelemetryFieldsAndLiveSnapshotAliasingAreAbsent()
    {
        Type core = typeof(Processor.CPU_Core);
        string[] removed =
        [
            "_assistLaunchCount",
            "_assistCompletedCount",
            "_assistKilledCount",
            "_assistInvalidationCount",
            "_testReferenceRawFallbackCount"
        ];
        foreach (string field in removed)
            Assert.Null(core.GetField(field, InstanceDeclared));

        string root = FindRepositoryRoot();
        string pipeline = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Stages", "Issue", "CPU_Core.Pipeline.cs");
        string observation = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.Pipeline.Helpers.PipelineObservation.cs");
        string diagnostic = Read(root, "TestAssemblerConsoleApps", "SimpleAsmApp.cs");
        Assert.Contains("return pipeCtrl.CreateSnapshot();", pipeline, StringComparison.Ordinal);
        Assert.Contains("pipelineControl: pipeCtrl.CreateSnapshot()", observation, StringComparison.Ordinal);
        Assert.Contains("PipelineControl).GetProperties", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void OutOfScopeOwnerTelemetryRemainsOutsideCoreTelemetryState()
    {
        Type telemetry = RequiredType("YAKSys_Hybrid_CPU.Core.TelemetryState");
        string[] fieldNames = telemetry.GetFields(InstanceDeclared)
            .Select(field => field.Name)
            .ToArray();

        Assert.DoesNotContain(fieldNames, name => name.Contains("MatrixTile", StringComparison.Ordinal));
        Assert.DoesNotContain(fieldNames, name => name.Contains("DmaStream", StringComparison.Ordinal));
        Assert.DoesNotContain(fieldNames, name => name.Contains("L7", StringComparison.Ordinal));
        Assert.DoesNotContain(fieldNames, name => name.Contains("Vmx", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fieldNames, name => name.Contains("MemoryCycle", StringComparison.Ordinal));
    }

    [Fact]
    public void LedgerAndEvidenceCloseOnlyTelemetryState()
    {
        string root = FindRepositoryRoot();
        string ledger = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "10_RF11",
            "00_CURRENT_STATUS_AND_LEDGER.md");
        string evidence = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "Evidence", "RF11",
            "rf11.4-telemetry-state-extraction.md");

        Assert.Contains("RF-11.4 | closed TelemetryState", ledger, StringComparison.Ordinal);
        Assert.Contains("RF-11.5 FrontendState", ledger, StringComparison.Ordinal);
        Assert.Contains("one state group", evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no timing remediation", evidence, StringComparison.OrdinalIgnoreCase);
    }

    private static Type RequiredType(string name) =>
        typeof(Processor.CPU_Core).Assembly.GetType(name) ??
        throw new InvalidOperationException($"Required type '{name}' was not found.");

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
