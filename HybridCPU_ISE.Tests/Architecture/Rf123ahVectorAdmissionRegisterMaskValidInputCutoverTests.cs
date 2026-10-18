using System.Collections;
using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123ahVectorAdmissionRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void PaperAuthorityAllowsOnlyTwoValidInputSelectionsWithRawFallbacks()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "#### 3.7.15 Common VectorMicroOp admission-register fold boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("Each already-participating value in `0..31`\nmay use the distinctly named checked `ArchRegId` entry point",
            paper, StringComparison.Ordinal);
        Assert.Contains("every other\n`int` must use the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("whole-list null substitution", paper,
            StringComparison.Ordinal);
        Assert.Contains("The distinct\n`VConfigMicroOp.InitializeMetadata` register loops",
            paper, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EveryArchitecturalRegisterKeepsExactMaskListsAndMemoryProfile(
        bool readsMemory,
        bool writesMemory)
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(register));

            int[] reads = [raw];
            int[] writes = [raw];
            var probe = new VectorAdmissionProbe();
            probe.SetRegisters(reads, writes);
            probe.Refresh(readsMemory, writesMemory);

            ResourceBitset expected =
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw);
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
            Assert.Equal(
                MicroOpAdmissionMetadata.BuildRegisterHazardMask(reads, writes),
                probe.AdmissionMetadata.RegisterHazardMask);
        }
    }

    [Fact]
    public void EveryNonrepresentableUshortKeepsExactRawFallback()
    {
        int[] reads = [0];
        int[] writes = [0];
        var probe = new VectorAdmissionProbe();
        probe.SetRegisters(reads, writes);

        for (int raw = ArchRegId.MaxValue + 1; raw <= ushort.MaxValue; raw++)
        {
            Assert.False(ArchRegId.TryCreate(raw, out _));
            reads[0] = raw;
            writes[0] = raw;
            probe.Refresh(readsMemory: false, writesMemory: false);

            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                probe.ResourceMask);
        }

        foreach (int raw in new[]
                 {
                     int.MinValue, int.MinValue + 1, -65536, -65535, -65, -64,
                     -33, -32, -5, -4, -3, -2, -1,
                     ushort.MaxValue + 1, 1_000_000, int.MaxValue - 1,
                     int.MaxValue
                 })
        {
            Assert.False(ArchRegId.TryCreate(raw, out _));
            reads[0] = raw;
            writes[0] = raw;
            probe.Refresh(readsMemory: false, writesMemory: false);

            Assert.Equal(
                ResourceMaskBuilder.ForRegisterRead(raw) |
                ResourceMaskBuilder.ForRegisterWrite(raw),
                probe.ResourceMask);
        }

        Assert.Same(reads, probe.ReadRegisters);
        Assert.Same(writes, probe.WriteRegisters);
    }

    [Fact]
    public void CheckedSelectionPreservesOneFoldReadPlusOneAdmissionReadPerElement()
    {
        var reads = new CountingReadOnlyList(0, 31, -1, int.MaxValue);
        var writes = new CountingReadOnlyList(31, 0, int.MaxValue, -1);
        var probe = new VectorAdmissionProbe();
        probe.SetRegisters(reads, writes);
        probe.Refresh(readsMemory: true, writesMemory: true);

            // Each element is read once by the register-resource fold and once
            // by the existing admission hazard-mask pass.
            Assert.Equal(8, reads.IndexerReadCount);
            Assert.Equal(8, writes.IndexerReadCount);
        Assert.Equal(ResourceMaskBuilder.ForRegisterRead(0) |
                     ResourceMaskBuilder.ForRegisterRead(31) |
                     ResourceMaskBuilder.ForRegisterRead(-1) |
                     ResourceMaskBuilder.ForRegisterRead(int.MaxValue) |
                     ResourceMaskBuilder.ForRegisterWrite(31) |
                     ResourceMaskBuilder.ForRegisterWrite(0) |
                     ResourceMaskBuilder.ForRegisterWrite(int.MaxValue) |
                     ResourceMaskBuilder.ForRegisterWrite(-1) |
                     ResourceMaskBuilder.ForStreamEngine(0) |
                     ResourceMaskBuilder.ForLoad() |
                     ResourceMaskBuilder.ForStore(),
            probe.ResourceMask);
    }

    [Fact]
    public void CanonicalVpopcX0ThroughX15KeepsWritebackAndRetireFacts()
    {
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
            Assert.Equal(
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(
                    ArchRegId.Create(destination)),
                microOp.ResourceMask);
        }
    }

    [Fact]
    public void SignaturesCallersNullAndUnrelatedFamiliesRemainUnchanged()
    {
        Type type = typeof(VectorMicroOp);
        MethodInfo refresh = type.GetMethod("RefreshVectorAdmissionMetadata",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(type.FullName,
                "RefreshVectorAdmissionMetadata");
        Assert.True(refresh.IsFamily);
        Assert.Equal(typeof(void), refresh.ReturnType);
        Assert.Equal([typeof(bool), typeof(bool)],
            refresh.GetParameters().Select(parameter => parameter.ParameterType));

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

        var probe = new VectorAdmissionProbe { IsMemoryOp = false };
        probe.SetRegisters(null, null);
        probe.Refresh(readsMemory: false, writesMemory: false);
        Assert.Null(probe.ReadRegisters);
        Assert.Null(probe.WriteRegisters);
        Assert.Equal(ResourceBitset.Zero, probe.ResourceMask);

        string method = ExtractBalanced(ReadVector("VectorMicroOps.cs"),
            "protected void RefreshVectorAdmissionMetadata(");
        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "ForMemoryBank", "ForDMAChannel",
                     "DomainId", "DomainTag", "Token", "Generation",
                     "LaneId", "SlotId", "PinnedLane", "VConfigMicroOp"
                 })
        {
            Assert.DoesNotContain(unrelated, method, StringComparison.Ordinal);
        }

        string vconfig = ExtractBalanced(ReadVector("VectorMicroOps.Data.cs"),
            "public class VConfigMicroOp");
        Assert.Equal(1, Count(vconfig,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(vconfig,
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)"));
        Assert.Equal(2, Count(vconfig, "ArchRegId.TryCreate"));
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

    private sealed class CountingReadOnlyList(params int[] values)
        : IReadOnlyList<int>
    {
        public int IndexerReadCount { get; private set; }
        public int Count => values.Length;

        public int this[int index]
        {
            get
            {
                IndexerReadCount++;
                return values[index];
            }
        }

        public IEnumerator<int> GetEnumerator() =>
            ((IEnumerable<int>)values).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static string ReadVector(string fileName) =>
        Read(FindRepositoryRoot(), "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Vector", fileName);

    private static string ReadSourceTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
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
