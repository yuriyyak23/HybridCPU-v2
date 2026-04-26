using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using HybridCPU.Compiler.Core.IR;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase12;

/// <summary>
/// REF-C3: promote safety-mask naming to a canonical diagnostic layer while
/// retaining the older compatibility aliases for one release window.
/// </summary>
public sealed class Phase12SafetyMaskDiagnosticRenameTests
{
    [Fact]
    public void SafetyMaskCompatibilityAliases_AreMarkedObsolete()
    {
        Assembly compilerAssembly = typeof(SafetyMaskDiagnosticChecker).Assembly;
        Type[] compatibilityTypes =
        [
            compilerAssembly.GetType("HybridCPU.Compiler.Core.IR.SafetyMaskCompatibilityChecker", throwOnError: true)!,
            compilerAssembly.GetType("HybridCPU.Compiler.Core.IR.SafetyMaskCompatibilityResult", throwOnError: true)!,
        ];

        foreach (Type compatibilityType in compatibilityTypes)
        {
            var attribute = compatibilityType.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(attribute);
            Assert.Contains("Compatibility alias (REF-C3)", attribute.Message, StringComparison.Ordinal);
        }

        PropertyInfo safetyMaskResultProperty = typeof(IrBundleAdmissionResult).GetProperty(nameof(IrBundleAdmissionResult.SafetyMaskResult))!;
        var propertyAttribute = safetyMaskResultProperty.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(propertyAttribute);
        Assert.Contains("Compatibility alias (REF-C3)", propertyAttribute.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SafetyMaskCompatibilityAliases_AreHiddenFromEditorBrowsing()
    {
        Assembly compilerAssembly = typeof(SafetyMaskDiagnosticChecker).Assembly;
        Type[] compatibilityTypes =
        [
            compilerAssembly.GetType("HybridCPU.Compiler.Core.IR.SafetyMaskCompatibilityChecker", throwOnError: true)!,
            compilerAssembly.GetType("HybridCPU.Compiler.Core.IR.SafetyMaskCompatibilityResult", throwOnError: true)!,
        ];

        foreach (Type compatibilityType in compatibilityTypes)
        {
            var attribute = compatibilityType.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(EditorBrowsableState.Never, attribute.State);
        }

        PropertyInfo safetyMaskResultProperty = typeof(IrBundleAdmissionResult).GetProperty(nameof(IrBundleAdmissionResult.SafetyMaskResult))!;
        var propertyAttribute = safetyMaskResultProperty.GetCustomAttribute<EditorBrowsableAttribute>();
        Assert.NotNull(propertyAttribute);
        Assert.Equal(EditorBrowsableState.Never, propertyAttribute.State);
    }

    [Fact]
    public void IrBundleAdmissionResult_ExposesCanonicalSafetyMaskDiagnosticAlias()
    {
#pragma warning disable CS0618
        var compatibilityResult = new SafetyMaskCompatibilityResult(
            IsCompatible: true,
            AggregateMask: SafetyMask128.Zero,
            Conflicts: Array.Empty<SafetyMaskConflict>());

        var admissionResult = new IrBundleAdmissionResult(
            BundleCycle: 7,
            Classification: AdmissibilityClassification.StructurallyAdmissible,
            SafetyMaskResult: compatibilityResult,
            StealVerdicts: Array.Empty<StealabilityVerdict>());
#pragma warning restore CS0618

        Assert.Same(compatibilityResult, admissionResult.SafetyMaskDiagnostic);
        Assert.True(admissionResult.SafetyMaskDiagnostic.IsCompatible);
        Assert.Equal(0, admissionResult.SafetyMaskDiagnostic.ConflictCount);
    }

    [Fact]
    public void LegacySafetyMaskNames_NoNewProductionMentionsOutsideCompatAllowList()
    {
        string repoRoot = FindRepoRoot();

        Assert.Empty(FindUnexpectedCallSites(
            repoRoot,
            "SafetyMaskCompatibilityChecker",
            [
                Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Admission", "SafetyMaskDiagnosticChecker.cs"),
            ]));

        Assert.Empty(FindUnexpectedCallSites(
            repoRoot,
            "SafetyMaskCompatibilityResult",
            [
                Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Admission", "SafetyMaskDiagnosticChecker.cs"),
                Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Model", "IrBundleAdmissionResult.cs"),
            ]));

        Assert.Empty(FindUnexpectedCallSites(
            repoRoot,
            "SafetyMaskResult",
            [
                Path.Combine(repoRoot, "HybridCPU_Compiler", "Core", "IR", "Model", "IrBundleAdmissionResult.cs"),
            ]));
    }

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
