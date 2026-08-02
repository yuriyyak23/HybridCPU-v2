using System.Reflection;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123zAtomicMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void AtomicUsesThreeIndependentCheckedPathsWithExactRawFallbacks()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs");
        string carrier = ExtractBalanced(source,
            "public sealed class AtomicMicroOp");
        string initialize = ExtractBalanced(carrier,
            "public void InitializeMetadata()");

        Assert.Equal(3, Count(initialize, "ArchRegId.TryCreate("));
        AssertCheckedFold(initialize, "BaseRegID", "baseRegister", "Read");
        AssertCheckedFold(initialize, "SrcRegID", "sourceRegister", "Read");
        AssertCheckedFold(initialize, "DestRegID", "destinationRegister",
            "Write");
        Assert.DoesNotContain("ArchRegId.Create(", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Clamp", initialize,
            StringComparison.Ordinal);
        Assert.DoesNotContain("%", initialize, StringComparison.Ordinal);

        AssertOrdered(initialize,
            "ResourceMask = ResourceBitset.Zero;",
            "ArchRegId.TryCreate(BaseRegID, out ArchRegId baseRegister)",
            "ResourceMaskBuilder.ForRegisterRead(BaseRegID)",
            "ArchRegId.TryCreate(SrcRegID, out ArchRegId sourceRegister)",
            "ResourceMaskBuilder.ForRegisterRead(SrcRegID)",
            "ArchRegId.TryCreate(DestRegID, out ArchRegId destinationRegister)",
            "ResourceMaskBuilder.ForRegisterWrite(DestRegID)",
            "ResourceMaskBuilder.ForAtomic()",
            "ResourceMaskBuilder.ForMemoryDomain(OwnerThreadId)",
            "PublishExplicitStructuralSafetyMask();",
            "RefreshAdmissionMetadata(this);");
    }

    [Fact]
    public void EveryRepresentableRegisterPreservesPerRoleCheckedHelperParity()
    {
        for (int raw = ArchRegId.MinValue; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(checkedId));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(checkedId));

            ushort value = (ushort)raw;
            Assert.Equal(Expected(value, VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, IsaOpcodeValues.LR_W, false),
                Create(value, VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, IsaOpcodeValues.LR_W, false)
                    .ResourceMask);
            Assert.Equal(Expected(VLIW_Instruction.NoReg, value,
                    VLIW_Instruction.NoReg, IsaOpcodeValues.SC_W, false),
                Create(VLIW_Instruction.NoReg, value,
                    VLIW_Instruction.NoReg, IsaOpcodeValues.SC_W, false)
                    .ResourceMask);

            bool writes = raw != 0;
            Assert.Equal(Expected(VLIW_Instruction.NoReg,
                    VLIW_Instruction.NoReg, value,
                    IsaOpcodeValues.AMOADD_W, writes),
                Create(VLIW_Instruction.NoReg, VLIW_Instruction.NoReg,
                    value, IsaOpcodeValues.AMOADD_W, writes).ResourceMask);
        }
    }

    [Fact]
    public void EveryUshortPreservesIndependentRoleListsAndRawMaskBehavior()
    {
        const ushort noReg = VLIW_Instruction.NoReg;
        for (int raw = ushort.MinValue; raw <= ushort.MaxValue; raw++)
        {
            ushort value = (ushort)raw;

            AtomicMicroOp baseRole = Create(
                value, noReg, noReg, IsaOpcodeValues.LR_W, false);
            Assert.Equal(value == noReg ? [] : [raw],
                baseRole.ReadRegisters);
            Assert.Empty(baseRole.WriteRegisters);
            Assert.Equal(Expected(
                value, noReg, noReg, IsaOpcodeValues.LR_W, false),
                baseRole.ResourceMask);

            AtomicMicroOp sourceRole = Create(
                noReg, value, noReg, IsaOpcodeValues.SC_W, false);
            Assert.Equal(value == noReg ? [] : [raw],
                sourceRole.ReadRegisters);
            Assert.Empty(sourceRole.WriteRegisters);
            Assert.Equal(Expected(
                noReg, value, noReg, IsaOpcodeValues.SC_W, false),
                sourceRole.ResourceMask);

            AtomicMicroOp destinationRole = Create(
                noReg, noReg, value, IsaOpcodeValues.AMOADD_W, true);
            bool destinationParticipates = value is not 0 and not noReg;
            Assert.Empty(destinationRole.ReadRegisters);
            Assert.Equal(destinationParticipates ? [raw] : [],
                destinationRole.WriteRegisters);
            Assert.Equal(Expected(
                noReg, noReg, value, IsaOpcodeValues.AMOADD_W, true),
                destinationRole.ResourceMask);
        }
    }

    [Fact]
    public void SentinelLrPredicateDuplicatesFactoryAndMutationSeamsRemainFrozen()
    {
        const ushort noReg = VLIW_Instruction.NoReg;

        AtomicMicroOp defaultCarrier = new();
        defaultCarrier.InitializeMetadata();
        Assert.Equal([0, 0], defaultCarrier.ReadRegisters);
        Assert.Empty(defaultCarrier.WriteRegisters);
        Assert.Equal(Expected(0, 0, 0, 0, false),
            defaultCarrier.ResourceMask);

        AtomicMicroOp lr = Create(7, 65534, 0,
            IsaOpcodeValues.LR_D, false);
        Assert.Equal([7], lr.ReadRegisters);
        Assert.Equal(Expected(7, 65534, 0,
            IsaOpcodeValues.LR_D, false), lr.ResourceMask);

        AtomicMicroOp duplicate = Create(7, 7, 9,
            IsaOpcodeValues.AMOADD_W, true);
        Assert.Equal([7, 7], duplicate.ReadRegisters);
        Assert.Equal([9], duplicate.WriteRegisters);

        List<int> reads = Assert.IsType<List<int>>(duplicate.ReadRegisters);
        ResourceBitset cached = duplicate.AdmissionMetadata.RegisterHazardMask;
        reads[0] = 31;
        Assert.Equal(31, duplicate.AdmissionMetadata.ReadRegisters[0]);
        Assert.Equal(cached, duplicate.AdmissionMetadata.RegisterHazardMask);

        PropertyInfo property = typeof(AtomicMicroOp).GetProperty(
            nameof(AtomicMicroOp.BaseRegID)) ??
            throw new MissingMemberException();
        property.SetValue(duplicate, (ushort)12);
        duplicate.RefreshWriteMetadata();
        Assert.Equal([12, 7], duplicate.ReadRegisters);

        uint opcode = (uint)InstructionsEnum.AMOADD_W;
        AtomicMicroOp rawFactory = Assert.IsType<AtomicMicroOp>(
            InstructionRegistry.CreateMicroOp(opcode, new DecoderContext
            {
                OpCode = opcode,
                Reg1ID = 65534,
                Reg2ID = 65533,
                Reg3ID = 65532,
                OwnerThreadId = 0
            }));
        Assert.True(rawFactory.WritesRegister);
        Assert.Equal([65533, 65532], rawFactory.ReadRegisters);
        Assert.Equal([65534], rawFactory.WriteRegisters);
        Assert.Equal(Expected(65533, 65532, 65534,
            IsaOpcodeValues.AMOADD_W, true), rawFactory.ResourceMask);

        AtomicMicroOp sentinel = Create(noReg, noReg, noReg,
            IsaOpcodeValues.SC_W, true);
        Assert.Empty(sentinel.ReadRegisters);
        Assert.Empty(sentinel.WriteRegisters);
    }

    [Fact]
    public void OwnerExecutionEffectAndRetireOwnersRemainOutsideMaskTyping()
    {
        AtomicMicroOp domainFifteen = Create(
            1, VLIW_Instruction.NoReg, 0,
            IsaOpcodeValues.LR_W, false, ownerThreadId: 15);
        Assert.Equal(Expected(1, VLIW_Instruction.NoReg, 0,
            IsaOpcodeValues.LR_W, false, ownerThreadId: 15),
            domainFifteen.ResourceMask);

        foreach (int invalidDomain in new[] { -1, 16 })
        {
            var invalid = new AtomicMicroOp { OwnerThreadId = invalidDomain };
            Assert.Throws<ArgumentOutOfRangeException>(
                invalid.InitializeMetadata);
        }

        string root = FindRepositoryRoot();
        string carrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.Misc.cs");
        string baseCarrier = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "MicroOps", "Types", "MicroOp.cs");
        string retire = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Pipeline", "Retire", "Evidence",
            "CPU_Core.PipelineExecution.Retire.cs");
        string atomicMemory = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Execution", "Memory", "AtomicMemory",
            "AtomicMemoryUnit.cs");

        Assert.Contains("int vtId = NormalizeExecutionVtId(OwnerThreadId);",
            carrier, StringComparison.Ordinal);
        Assert.Contains("ReadUnifiedScalarSourceOperand(ref core, vtId, BaseRegID)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("TryNormalizeFlatArchRegId(rawRegId",
            baseCarrier, StringComparison.Ordinal);
        Assert.Contains("ResolveRetireEffect(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("PrevalidateAtomicEffect(retireEffect.AtomicEffect)",
            retire, StringComparison.Ordinal);
        Assert.Contains("ApplyRetiredAtomicEffect(retireEffect.AtomicEffect)",
            retire, StringComparison.Ordinal);
        Assert.Contains("internal AtomicRetireOutcome ApplyResolvedRetireEffect",
            atomicMemory, StringComparison.Ordinal);
    }

    [Fact]
    public void SignaturesWireFspBankAndOtherIdentifierFamiliesRemainIsolated()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(AtomicMicroOp).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.DeclaredOnly));
        Assert.Empty(constructor.GetParameters());
        Assert.Equal(typeof(ushort),
            typeof(AtomicMicroOp).GetProperty(nameof(AtomicMicroOp.BaseRegID))!
                .PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(AtomicMicroOp).GetProperty(nameof(AtomicMicroOp.SrcRegID))!
                .PropertyType);
        Assert.Equal(typeof(ushort),
            typeof(AtomicMicroOp).GetProperty(nameof(MicroOp.DestRegID))!
                .PropertyType);

        string root = FindRepositoryRoot();
        string carrier = ExtractBalanced(Read(root, "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.Misc.cs"), "public sealed class AtomicMicroOp");
        string projector = Read(root, "HybridCPU_ISE", "Legacy",
            "CloseToHSL", "Core", "Decoder",
            "DecodedBundleTransportProjector.cs");

        Assert.Contains("SetClassFlexiblePlacement(SlotClass.LsuClass)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("case AtomicMicroOp atomicMicroOp:",
            projector, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryBankId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AcceleratorTokenHandle", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ChannelId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DomainId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("TokenId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("LaneId", carrier,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SlotId", carrier,
            StringComparison.Ordinal);
    }

    private static void AssertCheckedFold(
        string source,
        string rawName,
        string checkedName,
        string access)
    {
        Assert.Equal(1, Count(source,
            $"ArchRegId.TryCreate({rawName}, out ArchRegId {checkedName})"));
        Assert.Equal(1, Count(source,
            $"ResourceMaskBuilder.ForArchitecturalRegister{access}({checkedName})"));
        Assert.Equal(1, Count(source,
            $"ResourceMaskBuilder.ForRegister{access}({rawName})"));
    }

    private static AtomicMicroOp Create(
        ushort baseRegister,
        ushort sourceRegister,
        ushort destinationRegister,
        uint opcode,
        bool writesRegister,
        int ownerThreadId = 0)
    {
        var operation = new AtomicMicroOp
        {
            BaseRegID = baseRegister,
            SrcRegID = sourceRegister,
            DestRegID = destinationRegister,
            OpCode = opcode,
            WritesRegister = writesRegister,
            OwnerThreadId = ownerThreadId,
            Address = 0xFFFF000000001000UL,
            Size = 8
        };
        operation.InitializeMetadata();
        return operation;
    }

    private static ResourceBitset Expected(
        ushort baseRegister,
        ushort sourceRegister,
        ushort destinationRegister,
        uint opcode,
        bool writesRegister,
        int ownerThreadId = 0)
    {
        ResourceBitset result =
            ResourceMaskBuilder.ForAtomic() |
            ResourceMaskBuilder.ForMemoryDomain(ownerThreadId);
        if (baseRegister != VLIW_Instruction.NoReg)
            result |= ResourceMaskBuilder.ForRegisterRead(baseRegister);
        if (opcode is not (IsaOpcodeValues.LR_W or IsaOpcodeValues.LR_D) &&
            sourceRegister != VLIW_Instruction.NoReg)
        {
            result |= ResourceMaskBuilder.ForRegisterRead(sourceRegister);
        }
        if (writesRegister &&
            destinationRegister is not 0 and not VLIW_Instruction.NoReg)
        {
            result |= ResourceMaskBuilder.ForRegisterWrite(
                destinationRegister);
        }
        return result;
    }

    private static string ExtractBalanced(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signature}' was not found.");
        int brace = source.IndexOf('{', start);
        Assert.True(brace > start);
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
            int index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous,
                $"Marker '{marker}' was missing or out of order.");
            previous = index;
        }
    }

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
