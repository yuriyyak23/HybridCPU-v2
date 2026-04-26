using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CpuInterfaceBridge.Legacy;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase11;

/// <summary>
/// REF-11: CI gate enforcing the freeze of legacy UI contracts.
///
/// <list type="bullet">
///   <item><see cref="CoreStatInfo"/> — frozen struct; no new fields allowed.</item>
///   <item><see cref="HybridCpuEvents"/> — frozen event hub; no new events or methods allowed.</item>
///   <item><see cref="LegacyHostCoreStateBridge"/> — frozen bridge; no new callers allowed.</item>
/// </list>
///
/// All new UI integrations must go through <c>CpuInterfaceBridge</c> and
/// <c>CoreStateSnapshot</c>.
/// </summary>
public sealed class Phase11LegacyUiContractsFreezeTests
{
    /// <summary>
    /// Frozen field count of <see cref="CoreStatInfo"/>.
    /// Adding a field breaks this test — use <c>CoreStateSnapshot</c> instead.
    /// </summary>
    private const int ExpectedCoreStatInfoFieldCount = 46;

    /// <summary>
    /// Frozen event count of <see cref="HybridCpuEvents"/>.
    /// </summary>
    private const int ExpectedHybridCpuEventsEventCount = 2;

    /// <summary>
    /// Frozen user-defined method count of <see cref="HybridCpuEvents"/>
    /// (excludes compiler-generated event accessors).
    /// </summary>
    private const int ExpectedHybridCpuEventsMethodCount = 2;

    // ------------------------------------------------------------------
    // Reflection-based shape freeze
    // ------------------------------------------------------------------

    [Fact]
    public void CoreStatInfo_FieldCountIsFrozen()
    {
        FieldInfo[] fields = typeof(CoreStatInfo)
            .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Equal(ExpectedCoreStatInfoFieldCount, fields.Length);
    }

    [Fact]
    public void HybridCpuEvents_EventCountIsFrozen()
    {
        EventInfo[] events = typeof(HybridCpuEvents)
            .GetEvents(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        Assert.Equal(ExpectedHybridCpuEventsEventCount, events.Length);
    }

    [Fact]
    public void HybridCpuEvents_MethodCountIsFrozen()
    {
        MethodInfo[] userMethods = typeof(HybridCpuEvents)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        Assert.Equal(ExpectedHybridCpuEventsMethodCount, userMethods.Length);
    }

    [Fact]
    public void CoreStatInfo_IsMarkedObsolete()
    {
        Assert.NotNull(typeof(CoreStatInfo).GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void HybridCpuEvents_IsMarkedObsolete()
    {
        Assert.NotNull(typeof(HybridCpuEvents).GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void LegacyHostCoreStateBridge_IsMarkedObsolete()
    {
        Assert.NotNull(typeof(LegacyHostCoreStateBridge).GetCustomAttribute<ObsoleteAttribute>());
    }

    // ------------------------------------------------------------------
    // File-scan allow-list enforcement
    // ------------------------------------------------------------------

    [Fact]
    public void CoreStatInfo_NoNewProductionCallers()
    {
        string repoRoot = FindRepoRoot();
        string[] allowedCallers =
        [
            Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyUiContracts.cs"),
            Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs"),
            Path.Combine(repoRoot, "CpuInterfaceBridge", "LegacyEmulatorService.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Core.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "CoreStatInfo",
            allowedCallers);

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void HybridCpuEvents_NoNewProductionCallers()
    {
        string repoRoot = FindRepoRoot();
        string[] allowedCallers =
        [
            Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyUiContracts.cs"),
            Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs"),
            Path.Combine(repoRoot, "CpuInterfaceBridge", "LegacyEmulatorService.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Core.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "HybridCpuEvents",
            allowedCallers);

        Assert.Empty(unexpectedCallSites);
    }

    [Fact]
    public void LegacyHostCoreStateBridge_NoNewProductionSubscribers()
    {
        string repoRoot = FindRepoRoot();
        string[] allowedCallers =
        [
            Path.Combine(repoRoot, "CpuInterfaceBridge", "Legacy", "LegacyHostCoreStateBridge.cs"),
            Path.Combine(repoRoot, "CpuInterfaceBridge", "LegacyEmulatorService.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.ExternalModulesBridge.cs"),
            Path.Combine(repoRoot, "HybridCPU_EnvGUI", "Form_Main.Init.cs"),
        ];

        string[] unexpectedCallSites = FindUnexpectedCallSites(
            repoRoot,
            "LegacyHostCoreStateBridge",
            allowedCallers);

        Assert.Empty(unexpectedCallSites);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string[] FindUnexpectedCallSites(
        string repoRoot,
        string pattern,
        IReadOnlyCollection<string> allowedPaths)
    {
        var unexpectedCallSites = new List<string>();
        foreach (string productionRoot in EnumerateProductionRoots(repoRoot))
        {
            if (!Directory.Exists(productionRoot))
            {
                continue;
            }

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
