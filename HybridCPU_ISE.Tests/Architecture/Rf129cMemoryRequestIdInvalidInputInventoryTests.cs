namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129cMemoryRequestIdInvalidInputInventoryTests
{
    [Fact]
    public void ControllerOwnsZeroAndUnknownRequestDisposition()
    {
        string controller = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");

        Assert.Contains("public bool IsValid => Value != 0", controller, StringComparison.Ordinal);
        Assert.Contains("public bool TryTakeCompletion(MemoryRequestId requestId", controller, StringComparison.Ordinal);
        Assert.Contains("public bool TryCancel(MemoryRequestId requestId)", controller, StringComparison.Ordinal);
        Assert.Contains("_outstanding.Remove(requestId", controller, StringComparison.Ordinal);
    }


    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    private static string Root()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) && Directory.Exists(Path.Combine(current.FullName, "ResearchPaper"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
