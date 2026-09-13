namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129oAcceleratorTokenHandleInventoryTests
{
    [Fact]
    public void NativeHandleHasOpaqueNonzeroIngressAndStoreAllocation()
    {
        string handle = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");
        Assert.Contains("AcceleratorTokenHandle Invalid => new(0)", handle, StringComparison.Ordinal);
        Assert.Contains("FromOpaqueValue", handle, StringComparison.Ordinal);
        Assert.Contains("AcceleratorTokenHandle handle = AllocateHandle", store, StringComparison.Ordinal);
        Assert.Contains("TryLookup(", store, StringComparison.Ordinal);
    }

    [Fact]
    public void Lane7VirtualizationDoesNotExposeNativeHandleAsGuestIdentity()
    {
        string evidence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7", "Lane7VirtualToken.Evidence.partial.cs");
        string state = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7", "Lane7StateBlock.cs");
        Assert.Contains("ExposesHostTokenHandle", evidence, StringComparison.Ordinal);
        Assert.Contains("false", evidence, StringComparison.Ordinal);
        Assert.Contains("hostHandle = AcceleratorTokenHandle.Invalid", state, StringComparison.Ordinal);
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
