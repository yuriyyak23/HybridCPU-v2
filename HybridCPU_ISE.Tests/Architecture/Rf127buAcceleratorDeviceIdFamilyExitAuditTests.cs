using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Descriptors;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bu final AcceleratorDeviceId closed-world exit audit.</summary>
public sealed class Rf127buAcceleratorDeviceIdFamilyExitAuditTests
{
    [Fact]
    public void DeclaredFamilyAndLane7OwnerBoundariesAreClosed()
    {
        AcceleratorDeviceId[] declared = Enum.GetValues<AcceleratorDeviceId>();
        Assert.Equal(6, declared.Length);
        Assert.All(declared, id => Assert.True(Enum.IsDefined(id)));
        Assert.DoesNotContain((AcceleratorDeviceId)0, declared);

        string root = Root();
        string lane7 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.cs");
        string checkpoint = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.Checkpoint.partial.cs");
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), acceleratorId)", lane7,
            StringComparison.Ordinal);
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), handle.AcceleratorId)", checkpoint,
            StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\b(?:IommuDeviceId|BurstIoEndpointId|DomainId|TokenId|LaneId|SlotId)\b"), lane7);
    }

    [Fact]
    public void RetainedWireAndTestOnlyForgeryDoNotCreateASecondAuthority()
    {
        string root = Root();
        string production = ReadAll(root, "HybridCPU_ISE");
        string tests = ReadAll(root, "HybridCPU_ISE.Tests");

        Assert.Equal(1, Count(production, "(AcceleratorDeviceId)ReadUInt16"));
        Assert.DoesNotContain("JsonSerializer.Serialize<AcceleratorDeviceId>", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<AcceleratorDeviceId>", production, StringComparison.Ordinal);
        Assert.Contains("typeof(Lane7Checkpoint).GetConstructors(", tests, StringComparison.Ordinal);
    }

    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
    private static string ReadAll(string root, string directory) => string.Join("\n", Directory.EnumerateFiles(
        Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine([root, .. parts]));
    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
