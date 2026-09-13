using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Rf123abVmxMicroOpRegisterMaskValidInputCutoverTests
{
    [Fact]
    public void PaperAuthorizesOnlyCheckedValidBranchesWithExactRawFallbacks()
    {
        string paper = Read(FindRepositoryRoot(), "ResearchPaper", "section",
            "md base", "3_Architectural_Overview_and_Frontend_Contract.md");

        Assert.Contains(
            "A later valid-input-only cutover may branch independently at the six existing",
            paper, StringComparison.Ordinal);
        Assert.Contains("source folds and the one destination fold", paper,
            StringComparison.Ordinal);
        Assert.Contains("every participating raw byte 32..254 must use the exact raw helper",
            paper, StringComparison.Ordinal);
        Assert.Contains("The opcode and participation predicates, ordered duplicates",
            paper, StringComparison.Ordinal);
        Assert.Contains("Stage A/B, FSP,\nexecution, fault, replay, effect, retire",
            paper, StringComparison.Ordinal);
    }

    [Fact]
    public void SevenExistingFoldsSelectCheckedPathsAndRetainRawFallbacks()
    {
        string source = Read(FindRepositoryRoot(), "HybridCPU_ISE",
            "CloseToHSL", "Core", "Pipeline", "MicroOps", "Types",
            "MicroOp.IO.cs");
        string carrier = ExtractBalanced(source,
            "public sealed class VmxMicroOp");
        string initialize = ExtractBalanced(carrier,
            "private void InitializeMetadata()");

        Assert.Equal(7, Count(initialize, "ArchRegId.TryCreate("));
        Assert.Equal(6, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForArchitecturalRegisterWrite("));
        Assert.Equal(6, Count(initialize,
            "ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(initialize,
            "ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Equal(4, Count(initialize,
            "ArchRegId.TryCreate(Rs1, out ArchRegId rs1)"));
        Assert.Equal(2, Count(initialize,
            "ArchRegId.TryCreate(Rs2, out ArchRegId rs2)"));
        Assert.Equal(1, Count(initialize,
            "ArchRegId.TryCreate(Rd, out ArchRegId destinationRegister)"));

        Assert.Equal(7, Count(initialize,
            "? ResourceMaskBuilder.ForArchitecturalRegister"));
        Assert.Equal(6, Count(initialize,
            ": ResourceMaskBuilder.ForRegisterRead("));
        Assert.Equal(1, Count(initialize,
            ": ResourceMaskBuilder.ForRegisterWrite("));
        Assert.Contains("if (HasArchitecturalRegister(Rs1))", initialize,
            StringComparison.Ordinal);
        Assert.Contains("if (HasArchitecturalRegister(Rs2))", initialize,
            StringComparison.Ordinal);
        Assert.Contains("if (WritesRegister)", initialize,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CheckedEntryPointsMatchRawMasksForEveryParticipatingValidRegister()
    {
        for (int raw = 1; raw <= ArchRegId.MaxValue; raw++)
        {
            Assert.True(ArchRegId.TryCreate(raw, out ArchRegId register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterRead(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterRead(register));
            Assert.Equal(ResourceMaskBuilder.ForRegisterWrite(raw),
                ResourceMaskBuilder.ForArchitecturalRegisterWrite(register));
        }
    }

    [Fact]
    public void EveryBytePreservesIndependentOpcodeRoleListsAndRawMaskBehavior()
    {
        foreach ((ushort opcode, int readCount, bool writes) in RoleCases())
        {
            for (int raw = byte.MinValue; raw <= byte.MaxValue; raw++)
            {
                byte value = (byte)raw;
                bool participates = value is not 0 and not byte.MaxValue;
                var carrier = new VmxMicroOp
                {
                    OpCode = opcode,
                    Rd = value,
                    Rs1 = value,
                    Rs2 = value
                };

                carrier.RefreshWriteMetadata();

                Assert.Equal(participates
                    ? Enumerable.Repeat(raw, readCount).ToArray()
                    : [], carrier.ReadRegisters);
                Assert.Equal(writes && participates ? [raw] : [],
                    carrier.WriteRegisters);
                Assert.Equal(writes && participates, carrier.WritesRegister);

                ResourceBitset expected = ResourceBitset.Zero;
                if (participates && readCount != 0)
                    expected |= ResourceMaskBuilder.ForRegisterRead(raw);
                if (participates && writes)
                    expected |= ResourceMaskBuilder.ForRegisterWrite(raw);
                Assert.Equal(expected, carrier.ResourceMask);
            }
        }
    }

    [Fact]
    public void IndependentRolesPreserveOrderingDuplicatesAndSentinelSuppression()
    {
        var mixed = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMWRITE,
            Rs1 = 31,
            Rs2 = 254
        };
        mixed.RefreshWriteMetadata();
        Assert.Equal([31, 254], mixed.ReadRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForArchitecturalRegisterRead(
                ArchRegId.FromRawValue(31)) |
            ResourceMaskBuilder.ForRegisterRead(254),
            mixed.ResourceMask);

        var duplicate = new VmxMicroOp
        {
            OpCode = IsaOpcodeValues.VMFUNC,
            Rd = 7,
            Rs1 = 7,
            Rs2 = 7
        };
        duplicate.RefreshWriteMetadata();
        Assert.Equal([7, 7], duplicate.ReadRegisters);
        Assert.Equal([7], duplicate.WriteRegisters);
        Assert.Equal(
            ResourceMaskBuilder.ForArchitecturalRegisterRead(
                ArchRegId.FromRawValue(7)) |
            ResourceMaskBuilder.ForArchitecturalRegisterWrite(
                ArchRegId.FromRawValue(7)),
            duplicate.ResourceMask);

        foreach (byte absent in new[] { (byte)0, byte.MaxValue })
        {
            var suppressed = new VmxMicroOp
            {
                OpCode = IsaOpcodeValues.VMFUNC,
                Rd = absent,
                Rs1 = absent,
                Rs2 = absent
            };
            suppressed.RefreshWriteMetadata();
            Assert.Empty(suppressed.ReadRegisters);
            Assert.Empty(suppressed.WriteRegisters);
            Assert.False(suppressed.WritesRegister);
            Assert.Equal(ResourceBitset.Zero, suppressed.ResourceMask);
        }
    }

    [Fact]
    public void WirePlacementExecutionEffectAndUnrelatedOwnersRemainUnchanged()
    {
        string root = FindRepositoryRoot();
        string carrierSource = Read(root, "HybridCPU_ISE", "CloseToHSL",
            "Core", "Pipeline", "MicroOps", "Types", "MicroOp.IO.cs");
        string carrier = ExtractBalanced(carrierSource,
            "public sealed class VmxMicroOp");
        string registry = Read(root, "HybridCPU_ISE", "CloseToHSL", "Core",
            "Diagnostics", "InstructionRegistry.Helpers.Core.cs");

        Assert.Contains("registerId == VLIW_Instruction.NoArchReg",
            carrier, StringComparison.Ordinal);
        Assert.Contains("? (byte)0", carrier, StringComparison.Ordinal);
        Assert.Contains("Instruction ?? BuildInstructionIR()", carrier,
            StringComparison.Ordinal);
        Assert.Contains("VmxRetireEffect.Fault(", carrier,
            StringComparison.Ordinal);
        Assert.Contains("VmExitReason.SecurityPolicyViolation", carrier,
            StringComparison.Ordinal);
        Assert.Contains("SetHardPinnedPlacement(SlotClass.SystemSingleton, 7)",
            carrier, StringComparison.Ordinal);
        Assert.Contains("TryDecodeCanonicalOrUnpackedNoArchRegister",
            registry, StringComparison.Ordinal);
        Assert.Contains("throw new DecodeProjectionFaultException",
            registry, StringComparison.Ordinal);

        foreach (string unrelated in new[]
                 {
                     "MemoryBankId", "AcceleratorTokenHandle", "ChannelId",
                     "DomainId", "TokenId", "SlotId"
                 })
        {
            Assert.DoesNotContain(unrelated, carrier,
                StringComparison.Ordinal);
        }
    }

    private static IEnumerable<(ushort Opcode, int ReadCount, bool Writes)>
        RoleCases()
    {
        yield return (IsaOpcodeValues.VMREAD, 1, true);
        yield return (IsaOpcodeValues.VMWRITE, 2, false);
        yield return (IsaOpcodeValues.VMCLEAR, 1, false);
        yield return (IsaOpcodeValues.VMPTRLD, 1, false);
        yield return (IsaOpcodeValues.VMCALL, 2, false);
        yield return (IsaOpcodeValues.INVEPT, 2, false);
        yield return (IsaOpcodeValues.INVVPID, 2, false);
        yield return (IsaOpcodeValues.VMFUNC, 2, true);
        yield return (IsaOpcodeValues.VMSAVEX, 2, false);
        yield return (IsaOpcodeValues.VMRESTX, 2, false);
        yield return (IsaOpcodeValues.VMPTRST, 0, true);
        yield return (IsaOpcodeValues.VMXON, 0, false);
        yield return (0, 0, false);
        yield return (ushort.MaxValue, 0, false);
    }

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
