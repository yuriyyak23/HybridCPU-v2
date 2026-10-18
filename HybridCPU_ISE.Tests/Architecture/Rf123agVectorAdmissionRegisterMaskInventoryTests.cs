using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123agVectorAdmissionRegisterMaskInventoryTests
{
    [Fact]
    public void PaperDefinesCommonVectorFoldDomainAbsenceMutationAndLaterCutover()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.15 Common VectorMicroOp admission-register fold boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("twenty-six\nderived calls", paper, StringComparison.Ordinal);
        Assert.Contains("Empty or null is therefore\nwhole-list absence", paper,
            StringComparison.Ordinal);
        Assert.Contains("including x0,\nnegative values, `NoArchReg=255`, `NoReg=65535`",
            paper, StringComparison.Ordinal);
        Assert.Contains("only\n`VectorMaskPopCountMicroOp` replaces a register list",
            paper, StringComparison.Ordinal);
        Assert.Contains("nineteen read/write, three read-only,\ntwo write-only and two register-only",
            paper, StringComparison.Ordinal);
        Assert.Contains("prior safety and\ncached admission snapshots remain as they were",
            paper, StringComparison.Ordinal);
        Assert.Contains("every other\n`int` must use the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("The distinct\n`VConfigMicroOp.InitializeMetadata` register loops",
            paper, StringComparison.Ordinal);
        Assert.Contains("There is no bank identifier, bank resolver, unresolved-bank sentinel or\ninvalid-bank fallback",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void ClosedWorldDeclarationAndCallerManifestRemainsExact()
    {
        string root = FindRepositoryRoot();

        Assert.Equal(
        [
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Compute.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Data.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.DotWide.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Memory.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Permute2.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Saturating.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Scan.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.SlideOne.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.Transpose.cs",
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector/VectorMicroOps.cs"
        ], FindFilesContaining(root, "HybridCPU_ISE",
            "RefreshVectorAdmissionMetadata"));
        Assert.Empty(FindFilesContaining(root, "HybridCPU_Compiler",
            "RefreshVectorAdmissionMetadata"));
        Assert.Empty(FindFilesContaining(root, "TestAssemblerConsoleApps",
            "RefreshVectorAdmissionMetadata"));
        Assert.Empty(FindFilesContaining(root, "HybridCPU_ISE.Tests",
            "RefreshVectorAdmissionMetadata"));

        string production = ReadSourceTree(root, "HybridCPU_ISE");
        Assert.Equal(27, Count(production, "RefreshVectorAdmissionMetadata("));
        Assert.Equal(1, Count(production,
            "protected void RefreshVectorAdmissionMetadata("));
    }

    [Fact]
    public void TwentySixDerivedCallersAndMemoryProfilesRemainExact()
    {
        string vector = ReadSourceTree(FindRepositoryRoot(),
            "HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Vector");

        Assert.Equal(26, Count(vector, "base.InitializeMetadata();"));
        Assert.Equal(19, Count(vector,
            "RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: true)"));
        Assert.Equal(3, Count(vector,
            "RefreshVectorAdmissionMetadata(readsMemory: true, writesMemory: false)"));
        Assert.Equal(2, Count(vector,
            "RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: true)"));
        Assert.Equal(2, Count(vector,
            "RefreshVectorAdmissionMetadata(readsMemory: false, writesMemory: false)"));

        Assert.Equal(8, Count(ReadVector("VectorMicroOps.Compute.cs"),
            "RefreshVectorAdmissionMetadata("));
        Assert.Equal(5, Count(ReadVector("VectorMicroOps.Data.cs"),
            "RefreshVectorAdmissionMetadata("));
        Assert.Equal(6, Count(ReadVector("VectorMicroOps.Memory.cs"),
            "RefreshVectorAdmissionMetadata("));
        Assert.Equal(2, Count(ReadVector("VectorMicroOps.SlideOne.cs"),
            "RefreshVectorAdmissionMetadata("));
    }


    [Fact]
    public void CanonicalProducerIsEmptyExceptMaskPopCountDestinationNibble()
    {
        string source = ReadVector("VectorMicroOps.cs");
        string baseInitialize = ExtractBalanced(source,
            "public virtual void InitializeMetadata()");
        Assert.Contains("ReadRegisters = Array.Empty<int>();", baseInitialize,
            StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = Array.Empty<int>();", baseInitialize,
            StringComparison.Ordinal);

        string compute = ReadVector("VectorMicroOps.Compute.cs");
        string maskPop = ExtractBalanced(compute,
            "public sealed class VectorMaskPopCountMicroOp");
        Assert.Contains(
            "ushort destRegId = (ushort)((Instruction.Immediate >> 8) & 0x0F);",
            maskPop, StringComparison.Ordinal);
        Assert.Contains("WriteRegisters = new[] { (int)destRegId };", maskPop,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ReadRegisters =", maskPop, StringComparison.Ordinal);

        string data = ReadVector("VectorMicroOps.Data.cs");
        string vconfig = ExtractBalanced(data, "public class VConfigMicroOp");
        Assert.DoesNotContain("RefreshVectorAdmissionMetadata(", vconfig,
            StringComparison.Ordinal);
        Assert.DoesNotContain(": VectorMicroOp", vconfig, StringComparison.Ordinal);

        for (ushort destination = 0; destination <= 15; destination++)
        {
            var microOp = new VectorMaskPopCountMicroOp
            {
                Instruction = new VLIW_Instruction
                {
                    Immediate = (ushort)(destination << 8)
                }
            };
            microOp.InitializeMetadata();

            Assert.True(microOp.WritesRegister);
            Assert.Equal(destination, microOp.DestRegID);
            Assert.Equal([destination], microOp.WriteRegisters);
            Assert.Empty(microOp.ReadRegisters);
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(destination),
                microOp.ResourceMask);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ExtensibleRawIntDomainKeepsListsMasksAndMemoryProfile(
        bool readsMemory,
        bool writesMemory)
    {
        int[] reads =
        [
            int.MinValue, -65, -4, -1, 0, 1, 31, 32, 255, 65535, int.MaxValue
        ];
        int[] writes =
        [
            int.MaxValue, 65535, 255, 32, 31, 1, 0, -1, -4, -65, int.MinValue
        ];
        var probe = new VectorAdmissionProbe();
        probe.SetRegisters(reads, writes);
        probe.Refresh(readsMemory, writesMemory);

        ResourceBitset expected = ResourceBitset.Zero;
        foreach (int value in reads)
            expected |= ResourceMaskBuilder.ForRegisterRead(value);
        foreach (int value in writes)
            expected |= ResourceMaskBuilder.ForRegisterWrite(value);
        if (readsMemory || writesMemory)
            expected |= ResourceMaskBuilder.ForStreamEngine(0);
        if (readsMemory)
            expected |= ResourceMaskBuilder.ForLoad();
        if (writesMemory)
            expected |= ResourceMaskBuilder.ForStore();

        Assert.Same(reads, probe.ReadRegisters);
        Assert.Same(writes, probe.WriteRegisters);
        Assert.Equal(expected, probe.ResourceMask);
        Assert.Equal(new SafetyMask128(expected.Low, expected.High),
            probe.SafetyMask);
        MicroOpAdmissionMetadata admission = probe.AdmissionMetadata;
        Assert.Same(reads, admission.ReadRegisters);
        Assert.Same(writes, admission.WriteRegisters);
        Assert.Equal(
            MicroOpAdmissionMetadata.BuildRegisterHazardMask(reads, writes),
            admission.RegisterHazardMask);
    }

    [Fact]
    public void WholeListNullAndThrowingIndexerMutationWinnersRemainFrozen()
    {
        var probe = new VectorAdmissionProbe();
        probe.IsMemoryOp = false;
        probe.SetRegisters(null, null);
        probe.Refresh(readsMemory: false, writesMemory: false);

        Assert.Null(probe.ReadRegisters);
        Assert.Null(probe.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, probe.ResourceMask);
        Assert.Empty(probe.AdmissionMetadata.ReadRegisters);
        Assert.Empty(probe.AdmissionMetadata.WriteRegisters);

        var missingStructuralMask = new VectorAdmissionProbe();
        missingStructuralMask.SetRegisters(null, null);
        InvalidOperationException missingMask = Assert.Throws<InvalidOperationException>(
            () => missingStructuralMask.Refresh(
                readsMemory: false,
                writesMemory: false));
        Assert.Contains("without an explicit structural safety mask",
            missingMask.Message, StringComparison.Ordinal);
        Assert.Equal(ResourceBitset.Zero, missingStructuralMask.ResourceMask);
        Assert.Equal(SafetyMask128.Zero, missingStructuralMask.SafetyMask);

        int[] priorReads = [1];
        int[] priorWrites = [2];
        probe.SetRegisters(priorReads, priorWrites);
        probe.Refresh(readsMemory: true, writesMemory: true);
        SafetyMask128 priorSafety = probe.SafetyMask;
        MicroOpAdmissionMetadata priorAdmission = probe.AdmissionMetadata;

        probe.SetRegisters(new ThrowingReadOnlyList(4, 8), [12]);
        Assert.Throws<InventoryListException>(
            () => probe.Refresh(readsMemory: true, writesMemory: true));

        Assert.Equal(ResourceMaskBuilder.ForRegisterRead(4), probe.ResourceMask);
        Assert.Equal(priorSafety, probe.SafetyMask);
        Assert.Same(priorAdmission.ReadRegisters,
            probe.AdmissionMetadata.ReadRegisters);
        Assert.Same(priorAdmission.WriteRegisters,
            probe.AdmissionMetadata.WriteRegisters);
    }

    [Fact]
    public void ConstructionPlacementAndMutationSeamsStayUncheckedAndUnconflated()
    {
        Type type = typeof(VectorMicroOp);
        Assert.True(type.IsAbstract);
        ConstructorInfo constructor = Assert.Single(type.GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(constructor.GetParameters());

        MethodInfo refresh = type.GetMethod("RefreshVectorAdmissionMetadata",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName,
                "RefreshVectorAdmissionMetadata");
        Assert.True(refresh.IsFamily);
        Assert.Equal([typeof(bool), typeof(bool)],
            refresh.GetParameters().Select(parameter => parameter.ParameterType));

        Assert.True(typeof(MicroOp).GetProperty(nameof(MicroOp.ReadRegisters))!
            .GetSetMethod(nonPublic: true)!.IsFamily);
        Assert.True(typeof(MicroOp).GetProperty(nameof(MicroOp.WriteRegisters))!
            .GetSetMethod(nonPublic: true)!.IsFamily);

        var probe = new VectorAdmissionProbe();
        Assert.Equal(SlotClass.AluClass, probe.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, probe.Placement.PinningKind);
        Assert.Equal(0, probe.Placement.PinnedLaneId);

        string method = ExtractBalanced(ReadVector("VectorMicroOps.cs"),
            "protected void RefreshVectorAdmissionMetadata(");
        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "ForMemoryBank", "ForDMAChannel",
                     "DomainId", "DomainTag", "Token", "Generation",
                     "LaneId", "SlotId", "PinnedLane"
                 })
        {
            Assert.DoesNotContain(unrelated, method, StringComparison.Ordinal);
        }
    }

    private sealed class VectorAdmissionProbe : VectorMicroOp
    {
        public void SetRegisters(
            IReadOnlyList<int>? reads,
            IReadOnlyList<int>? writes)
        {
            ReadRegisters = reads!;
            WriteRegisters = writes!;
        }

        public void Refresh(bool readsMemory, bool writesMemory) =>
            RefreshVectorAdmissionMetadata(readsMemory, writesMemory);

        public override bool Execute(ref Processor.CPU_Core core) => true;
    }

    private sealed class ThrowingReadOnlyList(params int[] values)
        : IReadOnlyList<int>
    {
        public int Count => values.Length;

        public int this[int index] =>
            index == 1
                ? throw new InventoryListException()
                : values[index];

        public IEnumerator<int> GetEnumerator() =>
            ((IEnumerable<int>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class InventoryListException : Exception;

    private static string ReadVector(string fileName) =>
        Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", fileName);

    private static string[] FindFilesContaining(
        string root,
        string relativeRoot,
        string token)
    {
        string directory = Path.Combine(root, relativeRoot);
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
                Path.GetFileName(path) !=
                    "Rf123agVectorAdmissionRegisterMaskInventoryTests.cs" &&
                Path.GetFileName(path) !=
                    "Rf123ahVectorAdmissionRegisterMaskValidInputCutoverTests.cs")
            .Where(path => File.ReadAllText(path).Contains(token,
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path)
                .Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ReadSourceTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
                Path.GetFileName(path) !=
                    "Rf123agVectorAdmissionRegisterMaskInventoryTests.cs" &&
                Path.GetFileName(path) !=
                    "Rf123ahVectorAdmissionRegisterMaskValidInputCutoverTests.cs")
            .Select(File.ReadAllText));

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        int depth = 0;
        for (int index = brace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException($"'{signature}' was not closed.");
    }

    private static void AssertOrdered(string source, params string[] markers)
    {
        int previous = -1;
        foreach (string marker in markers)
        {
            int index = source.IndexOf(marker, previous + 1,
                StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase) ||
        path.Contains(
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

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
