namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129qAcceleratorTokenHandleValidInputParityTests
{
    [Fact]
    public void NativeHandleRemainsTheTypedStoreAndFenceConsumerSignature()
    {
        string store = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenStore.cs");
        string fence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Fences", "AcceleratorFenceModel.cs");
        Assert.Contains("AcceleratorTokenHandle handle = AllocateHandle", store, StringComparison.Ordinal);
        Assert.Contains("_tokens.Add(handle.Value, token)", store, StringComparison.Ordinal);
        Assert.Contains("public AcceleratorTokenLookupResult TryPoll(", store, StringComparison.Ordinal);
        Assert.Contains("public AcceleratorTokenLookupResult TryWait(", store, StringComparison.Ordinal);
        Assert.Contains("public AcceleratorTokenLookupResult TryCancel(", store, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<AcceleratorTokenHandle> tokenHandles", fence, StringComparison.Ordinal);
        Assert.Contains("AcceleratorTokenHandle handle = scope.TokenHandles[index]", fence, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidNativeHandleDoesNotBecomeGuestWireOrVirtualTokenIdentity()
    {
        string evidence = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Runtime", "Lanes", "Lane7", "Lane7VirtualToken.Evidence.partial.cs");
        Assert.Contains("ExposesHostTokenHandle(AcceleratorTokenHandle hostHandle) =>",
            evidence, StringComparison.Ordinal);
        Assert.Contains("false;", evidence, StringComparison.Ordinal);
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
