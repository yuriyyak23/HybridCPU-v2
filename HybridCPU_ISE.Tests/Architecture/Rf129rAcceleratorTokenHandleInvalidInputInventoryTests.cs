namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129rAcceleratorTokenHandleInvalidInputInventoryTests
{
    [Fact]
    public void DefaultAndForgedHandlesHaveOwnerLocalInvalidOutcomes()
    {
        string handle = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");
        Assert.Contains("if (value == 0)", handle, StringComparison.Ordinal);
        Assert.Contains("throw new ArgumentOutOfRangeException", handle, StringComparison.Ordinal);
        Assert.Contains("if (!handle.IsValid)", store, StringComparison.Ordinal);
        Assert.Contains("AcceleratorTokenFaultCode.InvalidHandle", store, StringComparison.Ordinal);
        Assert.Contains("unknown, stale, or forged", store, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardEvidenceMismatchStaysSeparateFromInvalidHandle()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");
        string state = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7", "Lane7StateBlock.cs");
        Assert.Contains("ValidateMappingEpoch(", store, StringComparison.Ordinal);
        Assert.Contains("AcceleratorTokenFaultCode.OwnerDomainRejected", store, StringComparison.Ordinal);
        Assert.Contains("!handle.IsValid", state, StringComparison.Ordinal);
        Assert.Contains("_handlesByValue.ContainsKey(handle.Value)", state, StringComparison.Ordinal);
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
