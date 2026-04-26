using System;
using System.IO;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09RegisterLegacySurfaceClosureTests
{
    [Fact]
    public void RegisterLayer_NoLongerCarriesRemovedLegacyStaticAndDuplicateSurfaces()
    {
        string repoRoot = FindRepoRoot();
        string registerSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "Registers",
            "CPU_Core.Registers.cs"));
        string stateDataSource = File.ReadAllText(Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "Core",
            "State",
            "CPU_Core.StateData.cs"));

        Assert.DoesNotContain("public static IntRegister[] IntRegisters", registerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public static FlagsRegister Flags_Register", registerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool[] Shared_Flags", registerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public enum Flags", registerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IntRegisters_LocalCore_GlobalIndexRelation", stateDataSource, StringComparison.Ordinal);
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
