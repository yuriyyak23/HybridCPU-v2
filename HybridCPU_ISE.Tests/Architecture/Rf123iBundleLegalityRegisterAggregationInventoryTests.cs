using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123iBundleLegalityRegisterAggregationInventoryTests
{
    [Fact]
    public void PaperDefinesDependencyParticipationWithoutInventingAbsenceOrAuthority()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("#### 3.7.3 Bundle-legality architectural-register aggregation boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("dependency inputs only", paper, StringComparison.Ordinal);
        Assert.Contains("x0 remains a valid identity", paper, StringComparison.Ordinal);
        Assert.Contains("producer-side dependency-participation", paper,
            StringComparison.Ordinal);
        Assert.Contains("accumulator input list", paper, StringComparison.Ordinal);
        Assert.Contains("`32..63` set their existing 64-bit dependency bit",
            paper, StringComparison.Ordinal);
        Assert.Contains("fallback. It must not change list storage", paper,
            StringComparison.Ordinal);
        Assert.Contains("hardening of public constructors", paper, StringComparison.Ordinal);
    }

    [Fact]
    public void PrivateAccumulatorAndItsClosedWorldCallerShapeRemainFrozenAfterRf123jCutover()
    {
        MethodInfo accumulator = Assert.Single(typeof(BundleLegalityAnalyzer)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == "AccumulateDependencyInputs"));
        Assert.True(accumulator.IsPrivate);
        ParameterInfo[] parameters = accumulator.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal(typeof(DecodedBundleSlotDescriptor).MakeByRefType(),
            parameters[0].ParameterType);
        Assert.True(parameters[0].IsIn);
        Assert.Equal(typeof(ulong).MakeByRefType(), parameters[1].ParameterType);
        Assert.Equal(typeof(ulong).MakeByRefType(), parameters[2].ParameterType);
        Assert.Equal(typeof(ResourceBitset).MakeByRefType(), parameters[3].ParameterType);

        string root = FindRepositoryRoot();
        string analyzer = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Legality",
            "BundleLegalityAnalyzer.cs");
        string production = ReadTree(root, "HybridCPU_ISE");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string assembler = ReadTree(root, "TestAssemblerConsoleApps");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");

        Assert.Equal(2, Count(analyzer, "AccumulateDependencyInputs"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForRegisterRead(registerId)"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForRegisterWrite(registerId)"));
        Assert.Equal(2, Count(analyzer, "ArchRegId.TryCreate("));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead(architecturalRegisterId)"));
        Assert.Equal(1, Count(analyzer,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite(architecturalRegisterId)"));
        Assert.Equal(1, Count(production,
            "new Core.Legality.BundleLegalityAnalyzer().Analyze(canonicalBundle)"));
        Assert.Equal(0, Count(compiler, "BundleLegalityAnalyzer"));
        Assert.Equal(0, Count(assembler, "BundleLegalityAnalyzer"));
        Assert.DoesNotContain("AccumulateDependencyInputs", testSupport,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DirectInstructionIrCompatibilityBypassPreservesEveryByteResultPerRole()
    {
        BundleLegalityDescriptor baseline = Analyze(0, 0, 0);
        DecodedBundleDependencySummary baselineSummary =
            Assert.IsType<DecodedBundleDependencySummary>(baseline.DependencySummary);

        for (int rawValue = byte.MinValue; rawValue <= byte.MaxValue; rawValue++)
        {
            byte value = (byte)rawValue;
            AssertDirectRole(value, RegisterRole.Rd, baselineSummary);
            AssertDirectRole(value, RegisterRole.Rs1, baselineSummary);
            AssertDirectRole(value, RegisterRole.Rs2, baselineSummary);
        }
    }

    [Fact]
    public void ReflectionFreezesRawIntShiftClampAndNegativeAliasBehavior()
    {
        foreach (int registerId in new[]
                 {
                     int.MinValue, -257, -65, -5, -4, -1, 0, 1, 31, 32, 63, 64, 254,
                     255, 256, 1024, int.MaxValue,
                 })
        {
            AccumulatedMasks read = InvokeAccumulator([registerId], []);
            Assert.Equal((uint)registerId < ArchRegId.DependencyMaskBitCount
                ? 1UL << registerId
                : 0UL, read.ReadMask);
            Assert.Equal(0UL, read.WriteMask);
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(registerId) |
                         StructuralBaseline(), read.ResourceMask);

            AccumulatedMasks write = InvokeAccumulator([], [registerId]);
            Assert.Equal(0UL, write.ReadMask);
            Assert.Equal((uint)registerId < ArchRegId.DependencyMaskBitCount
                ? 1UL << registerId
                : 0UL, write.WriteMask);
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(registerId) |
                         StructuralBaseline(), write.ResourceMask);
        }

        Assert.Equal(ResourceMaskBuilder.ForRegisterRead(0),
            ResourceMaskBuilder.ForRegisterRead(-1));
        Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(0),
            ResourceMaskBuilder.ForRegisterWrite(-1));
    }

    [Fact]
    public void UncheckedDescriptorStorageAndNoSerializationMutationSeamsRemainExplicit()
    {
        ConstructorInfo constructor = Assert.Single(typeof(DecodedBundleSlotDescriptor)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        ParameterInfo readRegisters = Assert.Single(constructor.GetParameters()
            .Where(parameter => parameter.Name == "readRegisters"));
        ParameterInfo writeRegisters = Assert.Single(constructor.GetParameters()
            .Where(parameter => parameter.Name == "writeRegisters"));
        Assert.Equal(typeof(IReadOnlyList<int>), readRegisters.ParameterType);
        Assert.Equal(typeof(IReadOnlyList<int>), writeRegisters.ParameterType);

        int[] reads = [-1, 0, 32, 255];
        int[] writes = [int.MaxValue];
        DecodedBundleSlotDescriptor descriptor = CreateDescriptor(reads, writes);
        Assert.Same(reads, descriptor.ReadRegisters);
        Assert.Same(writes, descriptor.WriteRegisters);

        string root = FindRepositoryRoot();
        string production = ReadTree(root, "HybridCPU_ISE");
        string compiler = ReadTree(root, "HybridCPU_Compiler");
        string assembler = ReadTree(root, "TestAssemblerConsoleApps");
        string combined = production + compiler + assembler;
        Assert.DoesNotContain("JsonSerializer.Serialize(dependencySummary", combined,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Deserialize<DecodedBundleDependencySummary>",
            combined, StringComparison.Ordinal);
        Assert.DoesNotContain("GetMethod(\"AccumulateDependencyInputs\"", combined,
            StringComparison.Ordinal);
    }

    private static void AssertDirectRole(
        byte value,
        RegisterRole role,
        DecodedBundleDependencySummary baseline)
    {
        byte rd = role == RegisterRole.Rd ? value : (byte)0;
        byte rs1 = role == RegisterRole.Rs1 ? value : (byte)0;
        byte rs2 = role == RegisterRole.Rs2 ? value : (byte)0;
        DecodedBundleDependencySummary actual = Assert.IsType<DecodedBundleDependencySummary>(
            Analyze(rd, rs1, rs2).DependencySummary);

        bool participates = value != 0 &&
            value != ArchRegisterTripletEncoding.NoArchReg;
        ulong expectedDependencyMask = participates && value < ArchRegId.DependencyMaskBitCount
            ? 1UL << value
            : 0UL;
        ResourceBitset expectedResourceMask = baseline.AggregateResourceMask;
        if (participates)
        {
            expectedResourceMask |= role == RegisterRole.Rd
                ? ResourceMaskBuilder.ForRegisterWrite(value)
                : ResourceMaskBuilder.ForRegisterRead(value);
        }

        Assert.Equal(role == RegisterRole.Rd ? 0UL : expectedDependencyMask,
            actual.ReadRegisterMask);
        Assert.Equal(role == RegisterRole.Rd ? expectedDependencyMask : 0UL,
            actual.WriteRegisterMask);
        Assert.Equal(expectedResourceMask, actual.AggregateResourceMask);
    }

    private static BundleLegalityDescriptor Analyze(byte rd, byte rs1, byte rs2)
    {
        var instruction = new InstructionIR
        {
            CanonicalOpcode = InstructionsEnum.ADD,
            Class = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = rd,
            Rs1 = rs1,
            Rs2 = rs2,
            Imm = 0,
        };
        var bundle = new DecodedInstructionBundle(
            bundleAddress: 0x1234,
            bundleSerial: 1,
            slots: [DecodedInstruction.CreateOccupied(0, instruction)]);
        return new BundleLegalityAnalyzer().Analyze(bundle);
    }

    private static AccumulatedMasks InvokeAccumulator(
        IReadOnlyList<int> reads,
        IReadOnlyList<int> writes)
    {
        MethodInfo accumulator = Assert.Single(typeof(BundleLegalityAnalyzer)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name == "AccumulateDependencyInputs"));
        object?[] arguments =
        [
            CreateDescriptor(reads, writes),
            0UL,
            0UL,
            ResourceBitset.Zero,
        ];
        accumulator.Invoke(null, arguments);
        return new AccumulatedMasks(
            Assert.IsType<ulong>(arguments[1]),
            Assert.IsType<ulong>(arguments[2]),
            Assert.IsType<ResourceBitset>(arguments[3]));
    }

    private static ResourceBitset StructuralBaseline() =>
        InvokeAccumulator([], []).ResourceMask;

    private static DecodedBundleSlotDescriptor CreateDescriptor(
        IReadOnlyList<int> reads,
        IReadOnlyList<int> writes) =>
        new(
            microOp: null!,
            slotIndex: 0,
            virtualThreadId: 0,
            ownerThreadId: 0,
            opCode: (uint)InstructionsEnum.ADD,
            readRegisters: reads,
            writeRegisters: writes,
            writesRegister: writes.Count != 0,
            isMemoryOp: false,
            isControlFlow: false,
            placement: BundleLegalityAnalyzer.BuildCanonicalPlacement(
                InstructionClass.ScalarAlu),
            memoryBankIntent: -1,
            isFspInjected: false,
            isEmptyOrNop: false,
            isVectorOp: false);

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith(
                "Rf123iBundleLegalityRegisterAggregationInventoryTests.cs",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    private static string Read(string root, params string[] components) =>
        File.ReadAllText(components.Aggregate(root, Path.Combine));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
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

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }

    private enum RegisterRole
    {
        Rd,
        Rs1,
        Rs2,
    }

    private readonly record struct AccumulatedMasks(
        ulong ReadMask,
        ulong WriteMask,
        ResourceBitset ResourceMask);
}
