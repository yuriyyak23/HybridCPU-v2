using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124fScalarAluExecuteOwnerVtValidInputCutoverTests
{
    [Fact]
    public void ScalarExecuteUsesCheckedProjectionOnlyForRepresentableOwners()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Vector", "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");
        string execute = Slice(carrier,
            "public override bool Execute(ref Processor.CPU_Core core)",
            "public override void EmitWriteBackRetireRecords(");

        Assert.Contains(
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            execute, StringComparison.Ordinal);
        Assert.Contains("? checkedOwner.Value", execute,
            StringComparison.Ordinal);
        Assert.Contains(": NormalizeExecutionVtId(OwnerThreadId);", execute,
            StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(execute,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.Equal(1, Regex.Matches(execute,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
    }

    [Fact]
    public void Vt0ThroughVt3PreserveOwnerOperandAndResultParity()
    {
        var core = new Processor.CPU_Core(0);

        for (int rawOwner = VtId.MinValue;
             rawOwner <= VtId.MaxValue;
             rawOwner++)
        {
            ulong left = 0x20UL + (ulong)rawOwner;
            ulong right = 0x40UL + (ulong)(rawOwner * 2);
            core.WriteCommittedArch(rawOwner, 1, left);
            core.WriteCommittedArch(rawOwner, 2, right);

            var operation = new ScalarALUMicroOp
            {
                OwnerThreadId = rawOwner,
                VirtualThreadId = (rawOwner + 1) % VtId.SmtWayCount,
                OpCode = (uint)Processor.CPU_Core.InstructionsEnum.ADD,
                Src1RegID = 1,
                Src2RegID = 2,
                WritesRegister = true
            };

            Assert.True(operation.Execute(ref core));
            Assert.True(operation.TryGetPrimaryWriteBackResult(
                out ulong result));
            Assert.Equal(left + right, result);
            Assert.Equal(rawOwner, operation.OwnerThreadId);
            Assert.Equal((rawOwner + 1) % VtId.SmtWayCount,
                operation.VirtualThreadId);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidOwnerRetainsThrowBeforeCoreReadPcOrResultMutation(
        int rawOwner)
    {
        const ulong priorResult = 0xCAFE_BABEUL;
        var operation = new ScalarALUMicroOp
        {
            OwnerThreadId = rawOwner,
            Src1RegID = 1,
            Src2RegID = 2,
            WritesRegister = true
        };
        operation.CapturePrimaryWriteBackResult(priorResult);
        Processor.CPU_Core core = null!;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => operation.Execute(ref core));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.True(operation.TryGetPrimaryWriteBackResult(out ulong result));
        Assert.Equal(priorResult, result);
    }

    [Fact]
    public void SignatureStorageAndSeparateRetireOwnerReadStayFrozen()
    {
        MethodInfo execute = typeof(ScalarALUMicroOp).GetMethod(
            nameof(ScalarALUMicroOp.Execute),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(Processor.CPU_Core).MakeByRefType()],
            modifiers: null)!;
        Assert.Equal(typeof(bool), execute.ReturnType);
        Assert.False(typeof(ScalarALUMicroOp).IsSealed);
        Assert.Equal(typeof(int), typeof(MicroOp).GetProperty(
            nameof(MicroOp.OwnerThreadId))!.PropertyType);

        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Vector", "MicroOp.Compute.cs"), "public class ScalarALUMicroOp");
        string retire = Slice(carrier,
            "public override void EmitWriteBackRetireRecords(",
            "public override bool TryGetPrimaryWriteBackResult(");
        Assert.Contains(
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            retire, StringComparison.Ordinal);
        Assert.Contains(": NormalizeExecutionVtId(OwnerThreadId);", retire,
            StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(carrier,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
    }

    [Fact]
    public void ProjectionCallerInventoryContainsOnlyAtomicAndScalarCutovers()
    {
        string root = FindRepositoryRoot();
        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs"] = 3,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/MicroOp.Compute.cs"] = 2
        }, FindOccurrences(root, "HybridCPU_ISE",
            "TryGetArchitecturalOwnerVtId"));
        Assert.Empty(FindOccurrences(root, "HybridCPU_Compiler",
            "TryGetArchitecturalOwnerVtId"));
        Assert.Empty(FindOccurrences(root, "TestAssemblerConsoleApps",
            "TryGetArchitecturalOwnerVtId"));
    }

    private static Dictionary<string, int> FindOccurrences(
        string root, string sourceRoot, string token) =>
        Directory.EnumerateFiles(Path.Combine(root, sourceRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin") &&
                           !HasPathSegment(path, "obj") &&
                           !HasPathSegment(path, "Legacy"))
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Count = Regex.Matches(File.ReadAllText(path),
                    $@"\b{token}\b").Count
            })
            .Where(entry => entry.Count != 0)
            .ToDictionary(entry => entry.Path, entry => entry.Count,
                StringComparer.Ordinal);

    private static string Slice(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        int endIndex = text.IndexOf(end, startIndex + start.Length,
            StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return text[startIndex..endIndex];
    }

    private static string ExtractBalanced(string source, string marker)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0);
        int open = source.IndexOf('{', markerIndex);
        Assert.True(open >= 0);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            depth += source[index] == '{' ? 1 :
                source[index] == '}' ? -1 : 0;
            if (depth == 0)
                return source[markerIndex..(index + 1)];
        }

        throw new InvalidOperationException("Unbalanced source contour.");
    }

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException(
            "HybridCPU repository root was not found.");
    }
}
