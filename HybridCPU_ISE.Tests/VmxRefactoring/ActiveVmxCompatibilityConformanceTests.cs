using Xunit;

namespace HybridCPU_ISE.Tests;

public sealed class VmxProjectionSchemaAndQuarantineTests
{
    [Fact]
    public void ActiveQuarantineFence_KeepsNeutralTrapResultSplitAndRemovedManagersAbsent()
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        string[] forbiddenFileNames =
        {
            "VmxExecutionUnit.cs",
            "VmcsManager.cs",
            "IVmcsManager.cs",
        };

        foreach (string fileName in forbiddenFileNames)
        {
            string[] matches = Directory.GetFiles(projectRoot, fileName, SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}CloseToRTL{Path.DirectorySeparatorChar}Core{Path.DirectorySeparatorChar}Virtualization{Path.DirectorySeparatorChar}Conformance{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.False(matches.Any(), string.Join(Environment.NewLine, matches));
        }

        string runtimeTrapSource = ReadProjectSource(
            "CloseToRTL/Core/Runtime/Events/Traps/NeutralTrapResult.cs",
            "CloseToRTL/Core/Runtime/Events/Traps/TrapPolicyBitmap.cs",
            "CloseToRTL/Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs");

        Assert.Contains("NeutralTrapResult", runtimeTrapSource);
        Assert.DoesNotContain("VmExitReason", runtimeTrapSource);
        Assert.DoesNotContain("TrapDecision", runtimeTrapSource);
    }

    private static string ReadProjectSource(params string[] relativePaths) =>
        ActiveVmxConformanceHelpers.ReadProjectSource(relativePaths);

    private static string FindRepositoryRoot() =>
        ActiveVmxConformanceHelpers.FindRepositoryRoot();
}

public sealed class VmxCompatibilityProjectionInventoryTests
{
    [Fact]
    public void ActiveProjectionInventory_CoversMapperAndRejectsRuntimeAuthorityMarkers()
    {
        string projectRoot = Path.Combine(ActiveVmxConformanceHelpers.FindRepositoryRoot(), "HybridCPU_ISE");
        string[] sources = EnumerateScopedProjectionSources(projectRoot);

        Assert.Equal(32, sources.Length);
        Assert.Contains(
            "Frontend/Projection/Events/VmxTrapProjectionMapper.cs",
            sources);
        Assert.Contains(
            "Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs",
            sources);

        foreach (string sourcePath in sources)
        {
            string source = File.ReadAllText(Path.Combine(
                projectRoot,
                "CloseToRTL/Core/Virtualization/Compatibility",
                sourcePath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.DoesNotContain("VmxExecutionUnit", source);
            Assert.DoesNotContain("VmcsManager", source);
            Assert.DoesNotContain("IVmcsManager", source);
            Assert.DoesNotContain("VmcsManagerAdapter", source);
            Assert.DoesNotContain("VmxRuntimeManager", source);
            Assert.DoesNotContain("VmcsProjectionRuntimeManager", source);
            Assert.DoesNotContain("VmcsV2RuntimeManager", source);
            Assert.DoesNotContain("ReadFieldValue(", source);
            Assert.DoesNotContain("WriteFieldValue(", source);
            Assert.DoesNotContain("HardwareWrite(", source);
            Assert.DoesNotContain("DirectWrite(", source);
        }
    }

    private static string[] EnumerateScopedProjectionSources(string projectRoot)
    {
        string compatibilityRoot = Path.Combine(
            projectRoot,
            "CloseToRTL",
            "Core",
            "Virtualization",
            "Compatibility");
        string[] roots =
        {
            Path.Combine(compatibilityRoot, "Generated"),
            Path.Combine(compatibilityRoot, "Frontend", "Projection"),
        };

        return roots
            .SelectMany(static root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(compatibilityRoot, path).Replace('\\', '/'))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class VmxFirstAdmittedCompatibilityPathTests
{
    [Fact]
    public void ActiveVmreadPath_RemainsAdmittedDeniedAndDoesNotCreateBackendSuccess()
    {
        string source = ActiveVmxConformanceHelpers.ReadProjectSource(
            "CloseToRTL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs");

        Assert.Contains("RuntimeBoundaryAdmissionService", source);
        Assert.Contains("DomainRuntimeOperationKind.ReadCompatibilityProjection", source);
        Assert.Contains("EvidenceVisibilityClass.CompatibilityAlias", source);
        Assert.Contains("VmcsReadOnlyValueProjectionService", source);
        Assert.Contains("ReadOnlyValueProjected", source);
        Assert.Contains("ReadOnlyProjectionDenied", source);
        Assert.DoesNotContain("TryReadScalarField", source);
        Assert.DoesNotContain("VmxExecutionUnit", source);
        Assert.DoesNotContain("VmcsManager", source);
        Assert.DoesNotContain("VmxRetireEffect.VmcsRead", source);
        Assert.DoesNotContain("VmxRetireEffect.InterceptExit", source);
    }
}

internal static class ActiveVmxConformanceHelpers
{
    public static string ReadProjectSource(params string[] relativePaths)
    {
        string projectRoot = Path.Combine(FindRepositoryRoot(), "HybridCPU_ISE");
        return string.Concat(relativePaths.Select(path => File.ReadAllText(Path.Combine(
            projectRoot,
            path.Replace('/', Path.DirectorySeparatorChar)))));
    }

    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE.sln")) ||
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate HybridCPU ISE repository root.");
    }
}
