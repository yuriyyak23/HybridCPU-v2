namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf129TokenGenerationInventoryTests
{
    [Fact]
    public void OwnerSpecificTokenAndGenerationDeclarationsRemainSeparate()
    {
        string memory = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        string accelerator = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string dma = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeTokenStore.cs");
        string generation = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem", "MemoryBankGeometryGeneration.cs");

        Assert.Contains("record struct MemoryRequestId(ulong Value)", memory, StringComparison.Ordinal);
        Assert.Contains("record struct AcceleratorTokenHandle(ulong Value)", accelerator, StringComparison.Ordinal);
        Assert.Contains("record struct DmaStreamComputeTokenHandle", dma, StringComparison.Ordinal);
        Assert.Contains("record struct MemoryBankGeometryGeneration", generation, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroAndInvalidFormsStayWithTheirAllocatingOwners()
    {
        string memory = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Timing", "MemoryCycleController.cs");
        string accelerator = Read("HybridCPU_ISE", "CloseToHSL", "Core", "Execution", "ExternalAccelerators", "Tokens", "AcceleratorTokenHandle.cs");
        string generation = Read("HybridCPU_ISE", "CloseToHSL", "Memory", "Subsystem", "MemoryBankGeometryGeneration.cs");

        Assert.Contains("public bool IsValid => Value != 0", memory, StringComparison.Ordinal);
        Assert.Contains("public static AcceleratorTokenHandle Invalid => new(0)", accelerator, StringComparison.Ordinal);
        Assert.Contains("public bool IsIssued => IsRepresentable(Value)", generation, StringComparison.Ordinal);
        Assert.Contains("generation = default", generation, StringComparison.Ordinal);
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
