using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-08.3l proves that static load metadata can enter an immutable contract
/// without being mistaken for concrete memory admission or a live transport.
/// </summary>
public sealed class Rf083lScalarLoadContractSourceTests
{
    [Fact]
    public void ContractRetainsTheSameCanonicalPlanAndBindingWithoutConcreteMemoryCapability()
    {
        CanonicalDecodedInstruction canonical = CreateCanonicalLoad(IsaOpcodeValues.LD, 11, 4, -32);
        ExecutionContract contract = Rf08ScalarLoadContractProjection.CreateContract(canonical);

        StaticMemoryAccessPlan staticPlan = Assert.IsType<StaticMemoryAccessPlan>(contract.StaticMemoryPlan);
        CanonicalScalarLoadAddressPlan canonicalPlan = Assert.IsType<CanonicalScalarLoadAddressPlan>(canonical.ScalarLoadAddressPlan);
        Assert.Same(canonical.StaticBinding, contract.GeneratedBinding);
        Assert.Same(canonicalPlan, staticPlan.ScalarLoadPlan);
        Assert.Same(canonical.StaticBinding, staticPlan.GeneratedBinding);
        Assert.Equal(MemoryCapabilityKind.NoMemory, contract.Memory.Kind);
        Assert.Equal(MemoryAccessDirection.Read, staticPlan.Direction);
        Assert.Equal(InstructionClass.Memory, contract.InstructionClass);
        Assert.Equal(SlotClass.LsuClass, contract.Placement.RequiredSlotClass);
        Assert.Equal(Rf08ScalarLoadContractProjection.PayloadSchema, contract.RuntimeProvider.PayloadSchema);
        Assert.Equal(new[] { 4 }, contract.ReadRegisters);
        Assert.Equal(new[] { 11 }, contract.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForRegisterRead(4) |
            ResourceMaskBuilder.ForRegisterWrite(11) |
            ResourceMaskBuilder.ForLoad(),
            contract.ResourceMask);
    }

    [Fact]
    public void ContractFailsClosedForMissingOrMismatchedCanonicalPlan()
    {
        CanonicalDecodedInstruction canonical = CreateCanonicalLoad(IsaOpcodeValues.LW, 6, 2, 12);
        Assert.Throws<InvalidOperationException>(() =>
            Rf08ScalarLoadContractProjection.CreateContract(canonical with { ScalarLoadAddressPlan = null }));

        GeneratedStaticBinding wrongBinding = BindingFor(IsaOpcodeValues.LD);
        Assert.Throws<InvalidOperationException>(() =>
            Rf08ScalarLoadContractProjection.CreateContract(canonical with { StaticBinding = wrongBinding }));
    }

    [Fact]
    public void StaticPlanCannotBeMixedWithConcreteCapabilityOrUsedByLiveIngress()
    {
        CanonicalDecodedInstruction canonical = CreateCanonicalLoad(IsaOpcodeValues.LB, 3, 1, 0);
        StaticMemoryAccessPlan staticPlan = StaticMemoryAccessPlan.UnresolvedScalarLoad(
            Assert.IsType<CanonicalScalarLoadAddressPlan>(canonical.ScalarLoadAddressPlan));

        Assert.Throws<ArgumentException>(() => ExecutionContract.Create(
            Assert.IsType<GeneratedStaticBinding>(canonical.StaticBinding),
            new RuntimeExecutionProviderBinding(canonical.StaticBinding!.RuntimeExecutionProviderId, "test"),
            InstructionClass.Memory,
            SerializationClass.Free,
            ExecutionPlacement.Create(SlotClass.LsuClass, SlotPinningKind.ClassFlexible),
            "RegisterWrite",
            MemoryCapability.Create(MemoryCapabilityKind.Load, [new FrozenMemoryRange(0x100, 1)], new MemoryBankId(0)),
            staticMemoryPlan: staticPlan));

        string root = FindRepositoryRoot();
        string fsp = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Fsp", "CPU_Core.PipelineExecution.Fsp.cs");
        string routing = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core", "Pipeline", "Scheduling", "Rf06ScalarSchedulerRouting.cs");
        Assert.DoesNotContain("Rf08ScalarLoadContractProjection", fsp, StringComparison.Ordinal);
        Assert.DoesNotContain("StaticMemoryAccessPlan", fsp, StringComparison.Ordinal);
        Assert.Contains("contract.StaticMemoryPlan is null", routing, StringComparison.Ordinal);
    }

    [Fact]
    public void PaperAndAdrKeepContractSourceSeparateFromLoadTransport()
    {
        string root = FindRepositoryRoot();
        string paper = Read(root, "ResearchPaper", "section", "md base", "5_Two_Stage_Admission_and_Bundle_Compositional_SMT_Packing.md");
        string adr = Read(root, "Documentation", "ArchitectureAuthorityRefactor", "02_Authority", "ADR-009_VLIW_Retirement.md");

        Assert.Contains("RF-08.3l authorised scalar-load static contract source", paper, StringComparison.Ordinal);
        Assert.Contains("not an `AdmissionRecord` or a scheduler input", paper, StringComparison.Ordinal);
        Assert.Contains("RF-08.3l scalar-load static contract source", adr, StringComparison.Ordinal);
        Assert.Contains("does not open the RF-08.3g transport", adr, StringComparison.Ordinal);
    }

    private static CanonicalDecodedInstruction CreateCanonicalLoad(uint opcode, byte rd, byte rs1, long immediate)
    {
        GeneratedStaticBinding binding = BindingFor(opcode);
        var slot = new CanonicalDecodedInstruction(
            SlotIndex: 4,
            IsOccupied: true,
            Opcode: opcode,
            InstructionClass: InstructionClass.Memory,
            SerializationClass: SerializationClass.Free,
            Rd: rd,
            Rs1: rs1,
            Rs2: 0,
            Immediate: immediate,
            CsrAddress: null,
            AcquireOrdering: false,
            ReleaseOrdering: false,
            RawSlot: CanonicalPayloadSnapshot.FromBytes("test", [1]),
            InstructionPayload: CanonicalPayloadSnapshot.FromBytes("test", [2]),
            SlotSideband: CanonicalPayloadSnapshot.FromBytes("test", [3]))
        {
            StaticBinding = binding,
        };
        Assert.True(CanonicalScalarLoadAddressPlan.TryCreate(slot, out CanonicalScalarLoadAddressPlan? plan));
        return slot with { ScalarLoadAddressPlan = plan };
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
