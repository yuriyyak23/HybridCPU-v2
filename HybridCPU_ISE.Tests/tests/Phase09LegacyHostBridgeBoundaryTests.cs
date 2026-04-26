using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09LegacyHostBridgeBoundaryTests
{
    [Fact]
    public void LegacyHostCoreStateBridge_ProductionCallersStayInsideEnvGuiHostBridge()
    {
        string repoRoot = FindRepoRoot();
        string bridgePath = Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs");
        string[] allowedCallers =
        [
            bridgePath,
            Path.Combine(repoRoot, "CpuInterfaceBridge", "LegacyEmulatorService.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "LegacyHostCoreStateBridge.",
            allowedCallers);

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void HybridCpuEvents_ProductionCallersStayInsideLegacyUiContractsOrEnvGuiHost()
    {
        string repoRoot = FindRepoRoot();
        string contractsPath = Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyUiContracts.cs");
        string stateBridgePath = Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs");
        string[] allowedCallers =
        [
            contractsPath,
            stateBridgePath,
            Path.Combine(repoRoot, "CpuInterfaceBridge", "LegacyEmulatorService.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Core.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "HybridCpuEvents.",
            allowedCallers);

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void CoreInitialization_NoLongerPublishesLegacyHostEventBridgeFromRuntime()
    {
        string repoRoot = FindRepoRoot();
        string eventBridgePath = Path.Combine(repoRoot, "HybridCPU_ISE", "CompilerEnv", "LegacyHostEventBridge.cs");
        string registersPath = Path.Combine(repoRoot, "HybridCPU_ISE", "Core", "Registers", "CPU_Core.Registers.cs");
        string oldContractsPath = Path.Combine(repoRoot, "HybridCPU_ISE", "CompilerEnv", "LegacyUiContracts.cs");
        string formCorePath = Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Core.cs");
        string formInitPath = Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs");

        // Legacy files must not exist at old paths
        Assert.False(File.Exists(eventBridgePath), "LegacyHostEventBridge.cs must not exist at old CompilerEnv path");
        Assert.False(File.Exists(oldContractsPath), "LegacyUiContracts.cs must not exist at old CompilerEnv path (moved to CpuInterfaceBridge/Legacy/)");
        Assert.DoesNotContain("NotifyCoreInitialized(", File.ReadAllText(registersPath), StringComparison.Ordinal);
        Assert.Contains("SyncInitializedCoresFromRuntime();", File.ReadAllText(formCorePath), StringComparison.Ordinal);
        Assert.Contains("SyncInitializedCoresFromRuntime();", File.ReadAllText(formInitPath), StringComparison.Ordinal);
    }

    [Fact]
    public void TestAssemblerConsoleApps_NoMemLegacyHarnessSurfaceRemains()
    {
        string repoRoot = FindRepoRoot();
        string harnessRoot = Path.Combine(repoRoot, "TestAssemblerConsoleApps");
        string[] unexpectedMentions = FindUnexpectedTokenMentions(
            harnessRoot,
            ["mem-legacy", "MemLegacyProbe"]);

        Assert.Empty(unexpectedMentions);
    }

    private static string[] FindUnexpectedCallSites(
        string repoRoot,
        string pattern,
        IReadOnlyCollection<string> allowedPaths)
    {
        var unexpectedCallSites = new List<string>();
        foreach (string productionRoot in EnumerateProductionRoots(repoRoot))
        {
            foreach (string filePath in Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (allowedPaths.Contains(filePath, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lines[lineIndex].Contains(pattern, StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add($"{Path.GetRelativePath(repoRoot, filePath)}:{lineIndex + 1}");
                    }
                }
            }
        }

        return unexpectedCallSites.ToArray();
    }

    private static string[] FindUnexpectedTokenMentions(
        string rootPath,
        IReadOnlyCollection<string> tokens)
    {
        var unexpectedMentions = new List<string>();
        foreach (string filePath in Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories))
        {
            if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(filePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                foreach (string token in tokens)
                {
                    if (lines[lineIndex].Contains(token, StringComparison.Ordinal))
                    {
                        unexpectedMentions.Add($"{Path.GetRelativePath(rootPath, filePath)}:{lineIndex + 1}");
                    }
                }
            }
        }

        return unexpectedMentions.ToArray();
    }

    private static IEnumerable<string> EnumerateProductionRoots(string repoRoot)
    {
        yield return Path.Combine(repoRoot, "HybridCPU_ISE");
        yield return Path.Combine(repoRoot, "HybridCPU_Compiler");
        yield return Path.Combine(repoRoot, "HybridCPU_EnvGUI");
        yield return Path.Combine(repoRoot, "CpuInterfaceBridge");
        yield return Path.Combine(repoRoot, "forms");
        yield return Path.Combine(repoRoot, "TestAssemblerConsoleApps");
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            bool hasRepoLayout =
                Directory.Exists(Path.Combine(current.FullName, "Documentation")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests"));
            if (hasRepoLayout)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate HybridCPU ISE repository root from test output directory.");
    }
}
