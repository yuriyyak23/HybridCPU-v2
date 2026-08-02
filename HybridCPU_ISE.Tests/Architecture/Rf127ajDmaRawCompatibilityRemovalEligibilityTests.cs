using System.Reflection;
using HybridCPU_ISE.CloseToHSL.Memory.DMA;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>RF-12.7aj DmaChannelId raw compatibility removal-eligibility decision.</summary>
public sealed class Rf127ajDmaRawCompatibilityRemovalEligibilityTests
{
    [Fact]
    public void PublicRawCompatibilityContractsRemainDiscoverable()
    {
        Type controller = typeof(DMAController);
        string[] byteEntryPoints =
        [
            nameof(DMAController.StartTransfer),
            nameof(DMAController.GetChannelState),
            nameof(DMAController.GetChannelProgress),
            nameof(DMAController.ResetChannel),
            nameof(DMAController.GetChannelAddresses),
            nameof(DMAController.PauseTransfer),
            nameof(DMAController.ResumeTransfer),
            nameof(DMAController.CancelTransfer)
        ];

        foreach (string entryPoint in byteEntryPoints)
        {
            Assert.NotNull(controller.GetMethod(entryPoint, BindingFlags.Public | BindingFlags.Instance,
                [typeof(byte)]));
        }

        Assert.Equal(typeof(byte), typeof(DMAController.TransferDescriptor).GetField(
            nameof(DMAController.TransferDescriptor.ChannelID))!.FieldType);
        Assert.Equal(typeof(byte), typeof(DMAController.TransferCompletedEventArgs).GetProperty(
            nameof(DMAController.TransferCompletedEventArgs.ChannelID))!.PropertyType);
        Assert.Equal(typeof(byte), typeof(DMAController.TransferCompletionCallback).GetMethod("Invoke")!.GetParameters()[0].ParameterType);
        Assert.NotNull(controller.GetMethod(nameof(DMAController.ConfigureTransfer),
            BindingFlags.Public | BindingFlags.Instance, [typeof(DMAController.TransferDescriptor), typeof(DMAController.TransferCompletionCallback)]));
    }

    [Fact]
    public void RawPublicSurfaceHasNonzeroCallerInventory()
    {
        string tests = Path.Combine(Root(), "HybridCPU_ISE.Tests");
        string[] byteEntryPoints =
        [
            "ConfigureTransfer(", "StartTransfer(", "GetChannelState(", "GetChannelProgress(",
            "ResetChannel(", "GetChannelAddresses(", "PauseTransfer(", "ResumeTransfer(", "CancelTransfer("
        ];

        foreach (string entryPoint in byteEntryPoints)
        {
            Assert.NotEmpty(FindCallers(tests, entryPoint));
        }

        Assert.NotEmpty(FindCallers(tests, "TransferCompleted +="));
        Assert.NotEmpty(FindCallers(tests, "TransferCompletionCallback"));
        Assert.NotEmpty(FindCallers(tests, "TransferDescriptor"));
    }

    [Fact]
    public void PrivateRawPublicationCarrierIsNotAnAuthorizationToRemovePublicWireForms()
    {
        string source = File.ReadAllText(Path.Combine(Root(), "HybridCPU_ISE", "CloseToHSL", "Memory",
            "DMA", "DMAController.cs"));

        Assert.Contains("private void OnTransferCompleted(byte channelID", source, StringComparison.Ordinal);
        Assert.Contains("OnTransferCompleted(channelID", source, StringComparison.Ordinal);
        Assert.Contains("byte channelID = channel;", source, StringComparison.Ordinal);
    }

    private static IEnumerable<string> FindCallers(string directory, string text) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("Rf127ajDmaRawCompatibilityRemovalEligibilityTests.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(text, StringComparison.Ordinal));

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
