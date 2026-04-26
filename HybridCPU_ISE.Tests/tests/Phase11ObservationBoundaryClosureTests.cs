using System;
using System.IO;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase11;

[Trait("Category", CompatFreezeGateCatalog.Category)]
public sealed class Phase11ObservationBoundaryClosureTests
{
    [Fact]
    public void ProductionCode_NoDirectStaticIseStateAccessorCallersRemain()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            CompatFreezeGateCatalog.ForbiddenStaticAccessorPatterns,
            allowedRelativePaths: Array.Empty<string>());

        Assert.True(
            violations.Length == 0,
            "Production code still references the removed static ISE_StateAccessor facade.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void StaticIseStateAccessorFacade_FileIsRemoved()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string filePath = Path.Combine(repoRoot, "HybridCPU_ISE", "ISE_StateAccessor.Compat.cs");

        Assert.False(File.Exists(filePath), $"Compatibility facade file should be absent: {filePath}");
    }

    [Fact]
    public void StaticIseStateAccessorFacade_SymbolIsAbsentFromHybridCpuAssembly()
    {
        Type? facadeType = typeof(IseObservationService).Assembly.GetType(
            "HybridCPU_ISE.ISE_StateAccessor",
            throwOnError: false,
            ignoreCase: false);

        Assert.Null(facadeType);
    }

    [Fact]
    public void ObservationBridges_NoDirectProcessorAccessorsRemain()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] observationRoots =
        [
            Path.Combine("forms", "Form_Main.ObservationBridge", "Form_Hybrid_CPU.ObservationBridge.cs"),
            Path.Combine("HybridCPU_EnvGUI", "Form_Main.ObservationBridge.cs"),
            Path.Combine("CpuInterfaceBridge", "CpuInterfaceBridge.cs"),
            Path.Combine("CpuInterfaceBridge", "IseCoreStateService.cs"),
            Path.Combine("CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs"),
            Path.Combine("TestAssemblerConsoleApps", "SimpleAsmApp.Progress.cs"),
        ];

        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            ["Processor."],
            allowedRelativePaths: Array.Empty<string>(),
            productionRootRelativePaths: observationRoots);

        Assert.True(
            violations.Length == 0,
            "Observation/GUI bridge paths must not reference Processor.* directly.\n" +
            string.Join("\n", violations));
    }
}
