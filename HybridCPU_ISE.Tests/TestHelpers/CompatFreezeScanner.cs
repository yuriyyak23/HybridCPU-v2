using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class CompatFreezeScanner
{
    internal static string[] FindUnexpectedCallSites(
        string repoRoot,
        CompatFreezeGateCatalog.SymbolAllowance allowance)
    {
        return FindUnexpectedCallSites(
            repoRoot,
            allowance.Pattern,
            allowance.AllowedRelativePaths,
            allowance.ProductionRootRelativePaths);
    }

    internal static string[] FindUnexpectedCallSites(
        string repoRoot,
        string pattern,
        IReadOnlyCollection<string> allowedRelativePaths,
        IReadOnlyCollection<string>? productionRootRelativePaths = null)
    {
        HashSet<string> allowedRelativePathSet = allowedRelativePaths
            .Select(NormalizeRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unexpectedCallSites = new List<string>();

        foreach (string productionRoot in ResolveProductionRoots(repoRoot, productionRootRelativePaths))
        {
            foreach (string filePath in EnumerateSourceFiles(productionRoot))
            {
                if (IsGeneratedPath(filePath))
                {
                    continue;
                }

                string relativePath = NormalizeRelativePath(Path.GetRelativePath(repoRoot, filePath));
                if (allowedRelativePathSet.Contains(relativePath))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (lines[lineIndex].Contains(pattern, StringComparison.Ordinal))
                    {
                        unexpectedCallSites.Add($"{relativePath}:{lineIndex + 1}");
                    }
                }
            }
        }

        return unexpectedCallSites.ToArray();
    }

    internal static string[] ScanProductionFilesForPatterns(
        string repoRoot,
        IReadOnlyCollection<string> patterns,
        IReadOnlyCollection<string> allowedRelativePaths,
        IReadOnlyCollection<string>? productionRootRelativePaths = null)
    {
        HashSet<string> allowedRelativePathSet = allowedRelativePaths
            .Select(NormalizeRelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var violations = new List<string>();

        foreach (string productionRoot in ResolveProductionRoots(repoRoot, productionRootRelativePaths))
        {
            foreach (string filePath in EnumerateSourceFiles(productionRoot))
            {
                if (IsGeneratedPath(filePath))
                {
                    continue;
                }

                string relativePath = NormalizeRelativePath(Path.GetRelativePath(repoRoot, filePath));
                if (allowedRelativePathSet.Contains(relativePath))
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(filePath);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = StripLineComment(lines[lineIndex]);
                    if (patterns.Any(pattern => line.Contains(pattern, StringComparison.Ordinal)))
                    {
                        violations.Add($"{relativePath}:{lineIndex + 1}: {lines[lineIndex].Trim()}");
                    }
                }
            }
        }

        return violations.ToArray();
    }

    internal static bool IsGeneratedPath(string filePath) =>
        filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    internal static string FindRepoRoot()
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

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU ISE repository root from test output directory.");
    }

    private static IEnumerable<string> ResolveProductionRoots(
        string repoRoot,
        IReadOnlyCollection<string>? productionRootRelativePaths)
    {
        foreach (string relativeRoot in productionRootRelativePaths ?? CompatFreezeGateCatalog.ProductionRootRelativePaths)
        {
            yield return Path.GetFullPath(Path.Combine(repoRoot, relativeRoot));
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string productionRoot)
    {
        if (Directory.Exists(productionRoot))
        {
            return Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories);
        }

        if (File.Exists(productionRoot) &&
            string.Equals(Path.GetExtension(productionRoot), ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { productionRoot };
        }

        return Array.Empty<string>();
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static string StripLineComment(string line)
    {
        int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        return commentIndex >= 0 ? line[..commentIndex] : line;
    }
}
