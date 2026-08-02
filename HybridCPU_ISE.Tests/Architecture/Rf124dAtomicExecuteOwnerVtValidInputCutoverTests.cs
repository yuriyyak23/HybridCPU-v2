using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124dAtomicExecuteOwnerVtValidInputCutoverTests
{
    [Fact]
    public void AtomicExecuteUsesCheckedProjectionOnlyForRepresentableOwners()
    {
        string carrier = ExtractBalanced(Read(FindRepositoryRoot(),
            "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "MicroOps",
            "Types", "MicroOp.Misc.cs"), "public sealed class AtomicMicroOp");

        Assert.Contains(
            "int vtId = TryGetArchitecturalOwnerVtId(out VtId checkedOwner)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("? checkedOwner.Value", carrier,
            StringComparison.Ordinal);
        Assert.Contains(": NormalizeExecutionVtId(OwnerThreadId);", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "int vtId = NormalizeExecutionVtId(OwnerThreadId);",
            carrier, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(carrier,
            @"\bTryGetArchitecturalOwnerVtId\s*\(").Count);
        Assert.Equal(1, Regex.Matches(carrier,
            @"\bNormalizeExecutionVtId\s*\(\s*OwnerThreadId\s*\)").Count);
    }

    [Fact]
    public void Vt0ThroughVt3PreserveOperandSelectionAndCapturedEffectOwner()
    {
        var core = new Processor.CPU_Core(0);

        for (int rawOwner = VtId.MinValue;
             rawOwner <= VtId.MaxValue;
             rawOwner++)
        {
            ulong address = 0x480UL + (ulong)(rawOwner * 0x10);
            ulong source = 0x10UL + (ulong)rawOwner;
            core.WriteCommittedArch(rawOwner, 1, address);
            core.WriteCommittedArch(rawOwner, 2, source);

            var operation = new AtomicMicroOp
            {
                OwnerThreadId = rawOwner,
                VirtualThreadId = (rawOwner + 1) % VtId.SmtWayCount,
                OpCode = (uint)Processor.CPU_Core.IsaOpcodeValues.AMOADD_W,
                BaseRegID = 1,
                SrcRegID = 2,
                DestRegID = 9,
                Size = 4,
                WritesRegister = true
            };

            Assert.True(operation.Execute(ref core));

            AtomicRetireEffect effect = operation.CreateRetireEffect();
            Assert.True(effect.IsValid);
            Assert.Equal(rawOwner, effect.VirtualThreadId);
            Assert.Equal(address, effect.Address);
            Assert.Equal(source, effect.SourceValue);
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
    public void InvalidOwnerRetainsExactThrowBeforeCoreAddressOrEffectMutation(
        int rawOwner)
    {
        const ulong originalAddress = 0xCAFE_BABEUL;
        var operation = new AtomicMicroOp
        {
            OwnerThreadId = rawOwner,
            Address = originalAddress,
            BaseRegID = 1,
            SrcRegID = 2
        };
        Processor.CPU_Core core = null!;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => operation.Execute(ref core));

        Assert.Equal("ownerThreadId", exception.ParamName);
        Assert.Equal(rawOwner, exception.ActualValue);
        Assert.Equal(originalAddress, operation.Address);
        Assert.False(operation.CreateRetireEffect().IsValid);
    }

    [Fact]
    public void SignatureStorageCallerAndEffectPublicationBoundariesStayFrozen()
    {
        MethodInfo execute = typeof(AtomicMicroOp).GetMethod(
            nameof(AtomicMicroOp.Execute),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(Processor.CPU_Core).MakeByRefType()],
            modifiers: null)!;
        Assert.Equal(typeof(bool), execute.ReturnType);

        PropertyInfo owner = typeof(MicroOp).GetProperty(
            nameof(MicroOp.OwnerThreadId))!;
        Assert.Equal(typeof(int), owner.PropertyType);
        Assert.True(owner.CanRead);
        Assert.True(owner.CanWrite);

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

        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        Order(retire,
            "PrevalidateAtomicEffect(retireEffect.AtomicEffect)",
            "ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)");
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

    private static void Order(string text, params string[] markers)
    {
        int offset = -1;
        foreach (string marker in markers)
        {
            int next = text.IndexOf(marker, offset + 1, StringComparison.Ordinal);
            Assert.True(next > offset, $"Missing or out-of-order marker: {marker}");
            offset = next;
        }
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
            depth += source[index] == '{' ? 1 : source[index] == '}' ? -1 : 0;
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

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
