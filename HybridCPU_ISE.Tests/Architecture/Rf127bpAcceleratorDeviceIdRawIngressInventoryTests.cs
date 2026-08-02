namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7bp AcceleratorDeviceId raw-ingress and compatibility inventory.</summary>
public sealed class Rf127bpAcceleratorDeviceIdRawIngressInventoryTests
{

    [Fact]
    public void CheckpointRestoreIsTheOnlyProductionRawStateReentryAndRevalidatesBeforeOwnerKeyInsertion()
    {
        string root = Root();
        string checkpoint = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.Checkpoint.partial.cs");
        string lane7 = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7",
            "Lane7StateBlock.cs");

        Assert.Contains("Lane7VirtualHandle handle = checkpoint.VirtualHandles[index]", checkpoint,
            StringComparison.Ordinal);
        Assert.Contains("_handleByOwner[(handle.ExecutionDomainTag, handle.OwnerVirtualThreadId, handle.AcceleratorId)]",
            checkpoint, StringComparison.Ordinal);
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), handle.AcceleratorId)", checkpoint,
            StringComparison.Ordinal);
        Assert.Contains("Enum.IsDefined(typeof(AcceleratorDeviceId), acceleratorId)", lane7,
            StringComparison.Ordinal);
    }


    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;

    private static string ReadAll(string root, string directory) => string.Join("\n",
        Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

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
