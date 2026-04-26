using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;

namespace HybridCPU_ISE.Tests.Phase12;

/// <summary>
/// REF-B2: quarantine the facade family as a deprecated compatibility surface.
/// New compiler integrations should use canonical compiler/context APIs directly.
/// </summary>
public sealed class Phase12FacadeFamilyDeprecationTests
{
    [Fact]
    public void FacadeFamily_IsMarkedObsolete()
    {
        Type[] facadeTypes =
        [
            typeof(IAppAsmFacade),
            typeof(IPlatformAsmFacade),
            typeof(IExpertBackendFacade),
            typeof(AppAsmFacade),
            typeof(PlatformAsmFacade),
            typeof(ExpertBackendFacade),
        ];

        foreach (Type facadeType in facadeTypes)
        {
            var attribute = facadeType.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(attribute);
            Assert.Contains("Compatibility facade surface", attribute.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FacadeFamily_IsHiddenFromEditorBrowsing()
    {
        Type[] facadeTypes =
        [
            typeof(IAppAsmFacade),
            typeof(IPlatformAsmFacade),
            typeof(IExpertBackendFacade),
            typeof(AppAsmFacade),
            typeof(PlatformAsmFacade),
            typeof(ExpertBackendFacade),
        ];

        foreach (Type facadeType in facadeTypes)
        {
            var attribute = facadeType.GetCustomAttribute<EditorBrowsableAttribute>();
            Assert.NotNull(attribute);
            Assert.Equal(EditorBrowsableState.Never, attribute.State);
        }
    }

    [Fact]
    public void FacadeInterfaces_NoNewProductionMentionsOutsideFacadeBoundary()
    {
        string repoRoot = FindRepoRoot();
        string[] allowedPaths =
        [
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "IAppAsmFacade.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "IPlatformAsmFacade.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "IExpertBackendFacade.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "AppAsmFacade.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "PlatformAsmFacade.cs"),
            Path.Combine(repoRoot, "HybridCPU_Compiler", "API", "Facade", "ExpertBackendFacade.cs"),
        ];

        string[] unexpectedIAppCallSites = FindUnexpectedCallSites(repoRoot, "IAppAsmFacade", allowedPaths);
        string[] unexpectedIPlatformCallSites = FindUnexpectedCallSites(repoRoot, "IPlatformAsmFacade", allowedPaths);
        string[] unexpectedIExpertCallSites = FindUnexpectedCallSites(repoRoot, "IExpertBackendFacade", allowedPaths);

        Assert.Empty(unexpectedIAppCallSites);
        Assert.Empty(unexpectedIPlatformCallSites);
        Assert.Empty(unexpectedIExpertCallSites);
    }

    [Fact]
    public void FacadeImplementations_NoNewProductionConstructionCallSites()
    {
        string repoRoot = FindRepoRoot();

        Assert.Empty(FindUnexpectedCallSites(repoRoot, "new AppAsmFacade(", Array.Empty<string>()));
        Assert.Empty(FindUnexpectedCallSites(repoRoot, "new PlatformAsmFacade(", Array.Empty<string>()));
        Assert.Empty(FindUnexpectedCallSites(repoRoot, "new ExpertBackendFacade(", Array.Empty<string>()));
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
