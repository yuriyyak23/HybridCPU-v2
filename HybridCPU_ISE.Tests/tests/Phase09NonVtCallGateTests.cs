using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

/// <summary>
/// REF-05: CI gate preventing new non-VT read/write calls in the
/// execution/privileged contour.
///
/// All new register/PC reads in <c>Core/Pipeline/</c>, <c>Core/Execution/</c>,
/// <c>Core/System/</c>, and <c>Core/Memory/</c> must use the VT-scoped API:
/// <c>ReadRegister(byte vtId, int regId)</c>, <c>WriteRegister(byte vtId, int regId, ulong value)</c>,
/// <c>ReadPc(byte vtId)</c>, and <c>WritePc(byte vtId, ulong pc)</c>.
///
/// Legacy non-VT shims remain only as compatibility members on <c>ICpuState</c>
/// and on adapter/test doubles; they must not re-enter the production contour.
/// </summary>
public sealed class Phase09NonVtCallGateTests
{
    /// <summary>
    /// Execution contour directories where new non-VT calls are prohibited.
    /// </summary>
    private static readonly string[] ExecutionContourSubPaths =
    [
        Path.Combine("HybridCPU_ISE", "Core", "Pipeline"),
        Path.Combine("HybridCPU_ISE", "Core", "Execution"),
        Path.Combine("HybridCPU_ISE", "Core", "System"),
        Path.Combine("HybridCPU_ISE", "Core", "Memory"),
    ];

    /// <summary>
    /// Patterns that identify legacy non-VT shim calls on <c>ICpuState</c>.
    /// The leading dot ensures we match member calls, not interface declarations.
    /// </summary>
    private static readonly string[] NonVtCallPatterns =
    [
        ".ReadIntRegister(",
        ".GetInstructionPointer(",
        ".WriteIntRegister(",
        ".SetInstructionPointer(",
    ];

    [Fact]
    public void ReadIntRegister_NoNewCallersInExecutionContour()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanForNonVtCalls(repoRoot, ".ReadIntRegister(");

        Assert.True(
            violations.Length == 0,
            $"New non-VT ReadIntRegister calls found in execution contour. " +
            $"Use ReadRegister(byte vtId, int regId) instead.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void GetInstructionPointer_NoNewCallersInExecutionContour()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanForNonVtCalls(repoRoot, ".GetInstructionPointer(");

        Assert.True(
            violations.Length == 0,
            $"New non-VT GetInstructionPointer calls found in execution contour. " +
            $"Use ReadPc(byte vtId) instead.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void WriteIntRegister_NoNewCallersInExecutionContour()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanForNonVtCalls(repoRoot, ".WriteIntRegister(");

        Assert.True(
            violations.Length == 0,
            $"New non-VT WriteIntRegister calls found in execution contour. " +
            $"Use WriteRegister(byte vtId, int regId, ulong value) instead.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void SetInstructionPointer_NoNewCallersInExecutionContour()
    {
        string repoRoot = FindRepoRoot();
        string[] violations = ScanForNonVtCalls(repoRoot, ".SetInstructionPointer(");

        Assert.True(
            violations.Length == 0,
            $"New non-VT SetInstructionPointer calls found in execution contour. " +
            $"Use WritePc(byte vtId, ulong pc) instead.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void NoNonVtCallersRemainInCoreTree()
    {
        string repoRoot = FindRepoRoot();
        string coreRoot = Path.Combine(repoRoot, "HybridCPU_ISE", "Core");
        var violations = new List<string>();

        foreach (string filePath in Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedPath(filePath))
                continue;

            string relativePath = Path.GetRelativePath(repoRoot, filePath);
            string[] lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                foreach (string pattern in NonVtCallPatterns)
                {
                    if (line.Contains(pattern, StringComparison.Ordinal))
                    {
                        violations.Add($"{relativePath}:{i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Legacy non-VT calls found in Core/ tree. " +
            $"Use VT-scoped ReadRegister / WriteRegister / ReadPc / WritePc instead.\n" +
            string.Join("\n", violations));
    }

    private static string[] ScanForNonVtCalls(string repoRoot, string pattern)
    {
        var violations = new List<string>();

        foreach (string subPath in ExecutionContourSubPaths)
        {
            string directory = Path.Combine(repoRoot, subPath);
            if (!Directory.Exists(directory))
                continue;

            foreach (string filePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(filePath))
                    continue;

                string relativePath = Path.GetRelativePath(repoRoot, filePath);
                string[] lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(pattern, StringComparison.Ordinal))
                    {
                        violations.Add($"{relativePath}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        return violations.ToArray();
    }

    private static bool IsGeneratedPath(string filePath) =>
        filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate HybridCPU ISE repository root from test output directory.");
    }
}

