using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123gScalarLoadRegisterMaskInventoryTests
{
    [Fact]
    public void PaperAuthorizesOnlyIndependentValidInputCutoverAndRetainsInvalidBehavior()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base",
            "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains("#### 3.7.2 RF-08 scalar-load static-plan register-mask boundary",
            paper, StringComparison.Ordinal);
        Assert.Contains("each field is evaluated independently", paper,
            StringComparison.Ordinal);
        Assert.Contains("Architectural register x0 is valid in either role", paper,
            StringComparison.Ordinal);
        Assert.Contains("per-role raw fallback", paper, StringComparison.Ordinal);
        Assert.Contains("this subsection does not decide whether absence is",
            paper, StringComparison.Ordinal);
        Assert.Contains("legal for every scalar-load opcode",
            paper, StringComparison.Ordinal);
        Assert.Contains("open scalar-load FSP membership", paper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentPlanAndRawMaskBehaviorRemainFrozenForEveryByteInEachRole()
    {
        for (int rawValue = byte.MinValue; rawValue <= byte.MaxValue; rawValue++)
        {
            byte value = (byte)rawValue;
            AssertPlanAndContract(value, 0);
            AssertPlanAndContract(0, value);
        }
    }

    [Fact]
    public void PackedLoadWireRetainsNoArchRegBaseAndRejectsNonCanonicalRegisterEncoding()
    {
        var raw = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        raw[0] = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.LD,
            Word1 = VLIW_Instruction.PackArchRegs(
                7,
                VLIW_Instruction.NoArchReg,
                VLIW_Instruction.NoArchReg),
        };

        CanonicalBundle bundle = Assert.IsType<CanonicalBundle>(
            new VliwDecoderV4().DecodeInstructionBundle(raw, 0x4000, 1).CanonicalBundle);
        CanonicalScalarLoadAddressPlan plan = Assert.IsType<CanonicalScalarLoadAddressPlan>(
            bundle.GetSlot(0).ScalarLoadAddressPlan);
        Assert.Equal((byte)7, plan.DestinationRegisterId);
        Assert.Equal(VLIW_Instruction.NoArchReg, plan.BaseRegisterId);

        var malformed = new VLIW_Instruction
        {
            OpCode = IsaOpcodeValues.LD,
            Word1 = 32,
        };
        RawSlot malformedSlot = RawSlotReader.Read(in malformed, 0);
        Assert.True(OpcodeDescriptorLookup.TryLookup(in malformedSlot, out var descriptor, out _));
        Assert.False(OperandDecoder.TryDecode(
            in malformedSlot,
            in descriptor,
            out DecodedOperandFields operands,
            out DecodeFailure? failure));
        Assert.Equal(default, operands);
        Assert.NotNull(failure);
        Assert.Equal(DecodeFailureCode.OperandEncoding, failure!.Code);
        Assert.Equal("Word1", failure.Field);
    }

    [Fact]
    public void StorageCallersReflectionAndTestSupportSeamsRemainFrozenAfterRf123hCutover()
    {
        Assert.Empty(typeof(CanonicalScalarLoadAddressPlan)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        PropertyInfo destination = Assert.Single(typeof(CanonicalScalarLoadAddressPlan)
            .GetProperties().Where(property => property.Name == "DestinationRegisterId"));
        PropertyInfo @base = Assert.Single(typeof(CanonicalScalarLoadAddressPlan)
            .GetProperties().Where(property => property.Name == "BaseRegisterId"));
        Assert.Equal(typeof(byte), destination.PropertyType);
        Assert.Equal(typeof(byte), @base.PropertyType);
        Assert.False(destination.CanWrite);
        Assert.False(@base.CanWrite);

        string root = FindRepositoryRoot();
        string projection = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "Rf08ScalarLoadContractProjection.cs");
        string canonical = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder",
            "CanonicalDecodedContracts.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Scheduling", "Rf06ScalarSchedulerRouting.cs");
        string testSupport = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline",
            "Core", "CPU_Core.TestSupport.cs");
        string callableSources = ReadTree(root, "HybridCPU_ISE") +
                                 ReadTree(root, "HybridCPU_Compiler") +
                                 ReadTree(root, "TestAssemblerConsoleApps");

        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForRegisterRead(plan.BaseRegisterId)"));
        Assert.Equal(1, Count(projection,
            "ResourceMaskBuilder.ForRegisterWrite(plan.DestinationRegisterId)"));
        Assert.Equal(1, Count(projection, "ForArchitecturalRegisterRead"));
        Assert.Equal(1, Count(projection, "ForArchitecturalRegisterWrite"));
        Assert.Equal(2, Count(projection, "ArchRegId.TryCreate"));
        Assert.Equal(0, Count(callableSources,
            "Rf08ScalarLoadContractProjection.CreateContract("));

        Assert.Contains("public byte DestinationRegisterId { get; }", canonical,
            StringComparison.Ordinal);
        Assert.Contains("public byte BaseRegisterId { get; }", canonical,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ArchRegId.IsRepresentable", canonical,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Rf08ScalarLoadContractProjection", fsp,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Rf08ScalarLoadContractProjection", routing,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalScalarLoadAddressPlan", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Rf08ScalarLoadContractProjection", testSupport,
            StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", projection, StringComparison.Ordinal);
    }

    private static void AssertPlanAndContract(byte destinationRegisterId, byte baseRegisterId)
    {
        CanonicalDecodedInstruction slot = CreateCanonicalLoad(
            destinationRegisterId,
            baseRegisterId);
        Assert.True(CanonicalScalarLoadAddressPlan.TryCreate(
            slot,
            out CanonicalScalarLoadAddressPlan? plan));
        plan = Assert.IsType<CanonicalScalarLoadAddressPlan>(plan);
        CanonicalDecodedInstruction canonical = slot with { ScalarLoadAddressPlan = plan };

        ExecutionContract contract = Rf08ScalarLoadContractProjection.CreateContract(canonical);
        ResourceBitset expected =
            ResourceMaskBuilder.ForRegisterRead(baseRegisterId) |
            ResourceMaskBuilder.ForRegisterWrite(destinationRegisterId) |
            ResourceMaskBuilder.ForLoad();

        Assert.Equal(destinationRegisterId, plan.DestinationRegisterId);
        Assert.Equal(baseRegisterId, plan.BaseRegisterId);
        Assert.Equal(new[] { (int)baseRegisterId }, contract.ReadRegisters);
        Assert.Equal(new[] { (int)destinationRegisterId }, contract.WriteRegisters);
        Assert.Equal(expected, contract.ResourceMask);
    }

    private static CanonicalDecodedInstruction CreateCanonicalLoad(
        byte destinationRegisterId,
        byte baseRegisterId)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(
            IsaOpcodeValues.LD,
            out GeneratedIsaDescriptor descriptor));
        GeneratedStaticBinding binding = GeneratedStaticBinding.FromDescriptor(in descriptor);
        return new CanonicalDecodedInstruction(
            SlotIndex: 0,
            IsOccupied: true,
            Opcode: IsaOpcodeValues.LD,
            InstructionClass: InstructionClass.Memory,
            SerializationClass: SerializationClass.Free,
            Rd: destinationRegisterId,
            Rs1: baseRegisterId,
            Rs2: VLIW_Instruction.NoArchReg,
            Immediate: 16,
            CsrAddress: null,
            AcquireOrdering: false,
            ReleaseOrdering: false,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("rf12.3g", [1]),
            InstructionPayload: CanonicalPayloadSnapshot.FromBytes("rf12.3g", [2]),
            SlotSideband: CanonicalPayloadSnapshot.FromBytes("rf12.3g", [3]))
        {
            StaticBinding = binding,
        };
    }

    private static string ReadTree(string root, string relativeRoot) =>
        string.Join("\n", Directory.EnumerateFiles(
                Path.Combine(root, relativeRoot), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
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
}
