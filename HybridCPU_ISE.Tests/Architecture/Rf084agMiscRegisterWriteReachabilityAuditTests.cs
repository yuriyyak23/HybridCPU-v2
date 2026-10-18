namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.4ag freezes production reachability for the three RegisterWrite
/// constructor sources retained in MicroOp.Misc.cs. It changes no runtime.
/// </summary>
public sealed class Rf084agMiscRegisterWriteReachabilityAuditTests
{
    [Fact]
    public void CustomAcceleratorAndIncrDecrHaveNoProductionConstructorCaller()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string miscPath = Path.Combine(
            coreRoot, "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");

        string allOtherCoreSources = string.Join(
            "\n",
            Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetFullPath(path).Equals(
                    Path.GetFullPath(miscPath),
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain(
            "new CustomAcceleratorMicroOp",
            allOtherCoreSources,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new IncrDecrMicroOp",
            allOtherCoreSources,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedMoveFactoriesAreUnregisteredAndHaveNoOtherProductionCaller()
    {
        string root = FindRepositoryRoot();
        string coreRoot = Path.Combine(root, "HybridCPU_ISE", "CloseToHSL", "Core");
        string helperPath = Path.Combine(
            coreRoot, "Diagnostics", "InstructionRegistry.Helpers.Vector.cs");
        string helper = File.ReadAllText(helperPath);

        Assert.Equal(2, CountOccurrences(helper, "new MoveMicroOp"));

        string allOtherCoreSources = string.Join(
            "\n",
            Directory.EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !Path.GetFullPath(path).Equals(
                    Path.GetFullPath(helperPath),
                    StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("new MoveMicroOp", allOtherCoreSources, StringComparison.Ordinal);

        string allCoreSources = helper + "\n" + allOtherCoreSources;
        Assert.Equal(1, CountOccurrences(allCoreSources, "RegisterRetainedMoveOp("));
        Assert.Equal(1, CountOccurrences(allCoreSources, "RegisterRetainedMoveNumOp("));
    }

    [Fact]
    public void CustomAcceleratorPublicationFailsClosedBeforeCarrierConstruction()
    {
        string root = FindRepositoryRoot();
        string runtime = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Runtime.cs");
        string misc = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");

        Assert.Contains("if (IsCustomAcceleratorOpcode(opCode))", runtime, StringComparison.Ordinal);
        Assert.Contains("throw CreateUnsupportedCustomAcceleratorException(opCode)", runtime, StringComparison.Ordinal);
        Assert.Contains(
            "public class CustomAcceleratorMicroOp",
            misc,
            StringComparison.Ordinal);
        Assert.Contains(
            "throw InstructionRegistry.CreateUnsupportedCustomAcceleratorException(OpCode)",
            misc,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MiscWritersDoNotAcquireTypedFspExactAttemptCarrier()
    {
        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string misc = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");

        Assert.Contains(
            "if (candidate is not Core.ScalarALUMicroOp)",
            fsp,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PostStageBIssuedAttempt", misc, StringComparison.Ordinal);
        Assert.Contains(
            "RetireRecord.RegisterWrite(vtId, DestRegID",
            misc,
            StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "ResearchPaper")) &&
                Directory.Exists(Path.Combine(directory.FullName, "HybridCPU_ISE")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
