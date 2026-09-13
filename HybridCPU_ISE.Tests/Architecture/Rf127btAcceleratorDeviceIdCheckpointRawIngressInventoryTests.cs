namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bt AcceleratorDeviceId checkpoint raw-re-entry closure inventory.</summary>
public sealed class Rf127btAcceleratorDeviceIdCheckpointRawIngressInventoryTests
{
    [Fact]
    public void ParserIsTheOnlyProductionRawCastAndBothLane7OwnerBoundariesRevalidate()
    {
        string root = Root();
        string production = ReadAll(root, "HybridCPU_ISE");
        string checkpoint = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.Checkpoint.partial.cs");
        string lane7 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.cs");

        Assert.Equal(1, Count(production, "(AcceleratorDeviceId)ReadUInt16"));
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), handle.AcceleratorId)", checkpoint,
            StringComparison.Ordinal);
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), acceleratorId)", lane7,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize<AcceleratorDeviceId>", production, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<AcceleratorDeviceId>", production, StringComparison.Ordinal);
    }


    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadAll(string root, string directory) => string.Join("\n",
        Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

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
