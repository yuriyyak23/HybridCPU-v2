using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests;

public sealed class Phase14TestingBoundaryTripwireTests
{
    [Fact]
    public void HybridCpuIseProject_RejectsReleaseBuildsWithInternalTestHooks()
    {
        string repoRoot = FindRepoRoot();
        string projectPath = Path.Combine(repoRoot, "HybridCPU_ISE", "HybridCPU_ISE.csproj");
        string projectSource = File.ReadAllText(projectPath);

        Assert.Contains(
            "<EnableInternalTestHooks Condition=\"'$(EnableInternalTestHooks)' == ''\">$(DefineTestSupport)</EnableInternalTestHooks>",
            projectSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Target Name=\"RejectReleaseBuildWithInternalTestHooks\" BeforeTargets=\"CoreCompile\">",
            projectSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Condition=\"'$(Configuration)' == 'Release' and '$(EnableInternalTestHooks)' == 'true'\"",
            projectSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Compile Remove=\"$(BaseIntermediateOutputPath)**\\*.cs\" />",
            projectSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "<GenerateAssemblyInfo>false</GenerateAssemblyInfo>",
            projectSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>",
            projectSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TestSupportPartials_RemainQuarantinedBehindTestingSymbol()
    {
        string repoRoot = FindRepoRoot();
        string cpuCoreTestSupportPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Core",
            "CPU_Core.TestSupport.cs");
        string schedulerTestSupportPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Pipeline",
            "Scheduling",
            "MicroOpScheduler.TestSupport.cs");

        string cpuCoreTestSupport = File.ReadAllText(cpuCoreTestSupportPath);
        string schedulerTestSupport = File.ReadAllText(schedulerTestSupportPath);

        Assert.StartsWith("#if TESTING", cpuCoreTestSupport, StringComparison.Ordinal);
        Assert.StartsWith("#if TESTING", schedulerTestSupport, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Could not locate repository root for testing boundary assertions.");
    }
}
