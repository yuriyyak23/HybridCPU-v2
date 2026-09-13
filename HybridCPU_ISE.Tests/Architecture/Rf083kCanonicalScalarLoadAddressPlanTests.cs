using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3k: canonical load facts are immutable addressing shape only; they
/// cannot become a resolved memory capability or a live scheduler input.
/// </summary>
public sealed class Rf083kCanonicalScalarLoadAddressPlanTests
{
    [Fact]
    public void ScalarLoadPlanRetainsTheSameFrozenBindingAndOnlyStaticAddressShape()
    {
        CanonicalDecodedInstruction canonical = CreateLoad(
            opcode: IsaOpcodeValues.LW,
            destination: 9,
            baseRegister: 3,
            displacement: -48,
            out GeneratedStaticBinding binding);

        Assert.True(CanonicalScalarLoadAddressPlan.TryCreate(canonical, out CanonicalScalarLoadAddressPlan? plan));
        plan = Assert.IsType<CanonicalScalarLoadAddressPlan>(plan);
        Assert.Same(binding, plan.GeneratedBinding);
        Assert.Equal((byte)9, plan.DestinationRegisterId);
        Assert.Equal((byte)3, plan.BaseRegisterId);
        Assert.Equal(-48, plan.SignedDisplacement);
        Assert.Equal((byte)4, plan.AccessSize);

        string[] forbidden = ["Address", "Bank", "Footprint", "Token", "Mshr", "Completion", "Retry", "Fault"];
        foreach (string fragment in forbidden)
        {
            Assert.DoesNotContain(typeof(CanonicalScalarLoadAddressPlan).GetProperties(), property =>
                property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData((uint)IsaOpcodeValues.LB, 1)]
    [InlineData((uint)IsaOpcodeValues.LHU, 2)]
    [InlineData((uint)IsaOpcodeValues.LWU, 4)]
    [InlineData((uint)IsaOpcodeValues.LD, 8)]
    public void SupportedScalarLoadWidthsAreDecoderStatic(uint opcode, byte expectedAccessSize)
    {
        CanonicalDecodedInstruction canonical = CreateLoad(opcode, 7, 2, 16, out _);

        Assert.True(CanonicalScalarLoadAddressPlan.TryCreate(canonical, out CanonicalScalarLoadAddressPlan? plan));
        Assert.Equal(expectedAccessSize, Assert.IsType<CanonicalScalarLoadAddressPlan>(plan).AccessSize);
    }

    [Fact]
    public void PlanRejectsNonLoadAndMismatchedCanonicalBinding()
    {
        CanonicalDecodedInstruction nonLoad = CreateLoad(IsaOpcodeValues.ADD, 3, 1, 0, out _,
            instructionClass: InstructionClass.ScalarAlu);
        Assert.False(CanonicalScalarLoadAddressPlan.TryCreate(nonLoad, out _));

        CanonicalDecodedInstruction load = CreateLoad(IsaOpcodeValues.LD, 3, 1, 8, out _);
        GeneratedStaticBinding wrongBinding = BindingFor(IsaOpcodeValues.LW);
        Assert.False(CanonicalScalarLoadAddressPlan.TryCreate(
            load with { StaticBinding = wrongBinding }, out _));
    }

    [Fact]
    public void AddressPlanIsNotWiredIntoMemoryCapabilityOrLiveFspIngress()
    {
        string root = FindRepositoryRoot();
        string canonical = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "CanonicalDecodedContracts.cs");
        string contracts = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Decoder", "Rf06ExecutionContracts.cs");
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");

        Assert.Contains("CanonicalScalarLoadAddressPlan", canonical, StringComparison.Ordinal);
        Assert.Contains("A memory capability must declare its frozen footprint.", contracts, StringComparison.Ordinal);
        Assert.Contains("A memory capability must declare its typed bank identity.", contracts, StringComparison.Ordinal);
        int capabilityStart = contracts.IndexOf("public sealed record MemoryCapability", StringComparison.Ordinal);
        int companionStart = contracts.IndexOf("public sealed class StaticMemoryAccessPlan", StringComparison.Ordinal);
        Assert.True(capabilityStart >= 0 && companionStart > capabilityStart);
        Assert.DoesNotContain(
            "CanonicalScalarLoadAddressPlan",
            contracts[capabilityStart..companionStart],
            StringComparison.Ordinal);
        Assert.Contains("public sealed class StaticMemoryAccessPlan", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("CanonicalScalarLoadAddressPlan", fsp, StringComparison.Ordinal);
        Assert.Contains("candidate is not Core.ScalarALUMicroOp", fsp, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndAdrLimitThePlanToUnresolvedDecoderMetadata()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation", "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-009_VLIW_Retirement.md");

        Assert.Contains("RF-08.3k authorised canonical scalar-load unresolved address plan", paper, StringComparison.Ordinal);
        Assert.Contains("is not a `MemoryCapability`, cannot satisfy the existing concrete", paper, StringComparison.Ordinal);
        Assert.Contains("an `AdmissionRecord` or a `PostStageBIssuedAttempt`", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.3k canonical scalar-load unresolved address plan", adr, StringComparison.Ordinal);
        Assert.Contains("`MemoryCapability`, an `AdmissionRecord`, a scheduler input or an issued", adr, StringComparison.Ordinal);
    }

    private static CanonicalDecodedInstruction CreateLoad(
        uint opcode,
        byte destination,
        byte baseRegister,
        long displacement,
        out GeneratedStaticBinding binding,
        InstructionClass instructionClass = InstructionClass.Memory)
    {
        binding = BindingFor(opcode);
        return new CanonicalDecodedInstruction(
            SlotIndex: 4,
            IsOccupied: true,
            Opcode: opcode,
            InstructionClass: instructionClass,
            SerializationClass: SerializationClass.Free,
            Rd: destination,
            Rs1: baseRegister,
            Rs2: 0,
            Immediate: displacement,
            CsrAddress: null,
            AcquireOrdering: false,
            ReleaseOrdering: false,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("test", [1]),
            InstructionPayload: CanonicalPayloadSnapshot.FromBytes("test", [2]),
            SlotSideband: CanonicalPayloadSnapshot.FromBytes("test", [3]))
        {
            StaticBinding = binding,
        };
    }

    private static GeneratedStaticBinding BindingFor(uint opcode)
    {
        Assert.True(GeneratedIsaCatalog.TryGetDescriptor(opcode, out GeneratedIsaDescriptor descriptor));
        return GeneratedStaticBinding.FromDescriptor(in descriptor);
    }

    private static string Read(string root, params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(segments).ToArray()));

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE")) &&
                Directory.Exists(Path.Combine(current.FullName, "HybridCPU_ISE.Tests")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
