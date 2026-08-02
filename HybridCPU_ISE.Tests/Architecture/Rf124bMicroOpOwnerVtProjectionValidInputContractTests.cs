using System.Reflection;
using System.Text.RegularExpressions;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf124bMicroOpOwnerVtProjectionValidInputContractTests
{
    [Fact]
    public void ProjectionHasOneExactCheckedSignatureWhileRawStorageRemainsMutableInt()
    {
        MethodInfo? projection = typeof(MicroOp).GetMethod(
            nameof(MicroOp.TryGetArchitecturalOwnerVtId),
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(VtId).MakeByRefType()],
            modifiers: null);

        Assert.NotNull(projection);
        Assert.Equal(typeof(bool), projection!.ReturnType);
        Assert.False(projection.IsStatic);

        PropertyInfo owner = typeof(MicroOp).GetProperty(nameof(MicroOp.OwnerThreadId))!;
        Assert.Equal(typeof(int), owner.PropertyType);
        Assert.True(owner.CanRead);
        Assert.True(owner.CanWrite);

        PropertyInfo carrier = typeof(MicroOp).GetProperty(nameof(MicroOp.VirtualThreadId))!;
        Assert.Equal(typeof(int), carrier.PropertyType);
        Assert.True(carrier.CanRead);
        Assert.True(carrier.CanWrite);
    }

    [Fact]
    public void Vt0ThroughVt3ProjectWithoutChangingOwnerOrCarrierStorage()
    {
        for (int rawOwner = VtId.MinValue; rawOwner <= VtId.MaxValue; rawOwner++)
        {
            int independentCarrier = (rawOwner + 1) % VtId.SmtWayCount;
            var microOp = new NopMicroOp
            {
                OwnerThreadId = rawOwner,
                VirtualThreadId = independentCarrier
            };

            Assert.True(microOp.TryGetArchitecturalOwnerVtId(out VtId checkedOwner));
            Assert.Equal(rawOwner, checkedOwner.Value);
            Assert.Equal(rawOwner, microOp.OwnerThreadId);
            Assert.Equal(independentCarrier, microOp.VirtualThreadId);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidRawOwnerRemainsStoredAndProjectionDoesNotCreateAnAlias(int rawOwner)
    {
        var microOp = new NopMicroOp { OwnerThreadId = rawOwner };

        Assert.False(microOp.TryGetArchitecturalOwnerVtId(out VtId checkedOwner));
        Assert.Equal(default(VtId), checkedOwner);
        Assert.Equal(rawOwner, microOp.OwnerThreadId);
    }

    [Fact]
    public void ProductionProjectionHasOnlyAuthorizedAtomicAndScalarCallersAndNoWireCutover()
    {
        string root = FindRepositoryRoot();
        const string token = "TryGetArchitecturalOwnerVtId";

        Assert.Equal(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Control/MicroOp.Control.cs"] = 3,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Memory/MicroOp.LoadStore.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Types/MicroOp.Misc.cs"] = 1,
            ["HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/MicroOp.Compute.cs"] = 2
        }, FindOccurrences(root, "HybridCPU_ISE", token));
        Assert.Empty(FindOccurrences(root, "HybridCPU_Compiler", token));
        Assert.Empty(FindOccurrences(root, "TestAssemblerConsoleApps", token));

        string source = File.ReadAllText(Path.Combine(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types", "MicroOp.cs"));
        Assert.Contains("VtId.TryCreate(OwnerThreadId, out ownerVtId)", source,
            StringComparison.Ordinal);
        Assert.Contains("public int OwnerThreadId { get; set; } = 0;", source,
            StringComparison.Ordinal);
        Assert.Contains("public int VirtualThreadId { get; set; } = 0;", source,
            StringComparison.Ordinal);
    }

    private static Dictionary<string, int> FindOccurrences(
        string root, string sourceRoot, string token)
    {
        string absoluteRoot = Path.Combine(root, sourceRoot);
        return Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !HasPathSegment(path, "bin"))
            .Where(path => !HasPathSegment(path, "obj"))
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Count = Regex.Matches(File.ReadAllText(path), $@"\b{token}\b").Count
            })
            .Where(entry => entry.Count != 0)
            .ToDictionary(entry => entry.Path, entry => entry.Count,
                StringComparer.Ordinal);
    }

    private static bool HasPathSegment(string path, string segment) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
