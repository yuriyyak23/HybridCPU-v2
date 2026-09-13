using System;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class Vmx8AbiSnapshotTests
{
    [Fact]
    public void Vmx8OpcodeValues_AreFrozenCompatibilityBaseline()
    {
        (InstructionsEnum Opcode, ushort Value)[] expected =
        [
            (InstructionsEnum.VMXON, 250),
            (InstructionsEnum.VMXOFF, 251),
            (InstructionsEnum.VMLAUNCH, 252),
            (InstructionsEnum.VMRESUME, 253),
            (InstructionsEnum.VMREAD, 254),
            (InstructionsEnum.VMWRITE, 255),
            (InstructionsEnum.VMCLEAR, 256),
            (InstructionsEnum.VMPTRLD, 257),
        ];

        Assert.Equal(expected.Length, VmxSpecTable.Vmx8Opcodes.Count);
        Assert.Equal(expected.Select(item => item.Value), VmxSpecTable.Vmx8Opcodes.Select(item => item.Opcode));

        foreach ((InstructionsEnum opcode, ushort value) in expected)
        {
            Assert.Equal(value, (ushort)opcode);
            Assert.Equal(value, (ushort)Enum.Parse<InstructionsEnum>(opcode.ToString()));
            Assert.Equal(value, (ushort)typeof(IsaOpcodeValues).GetField(opcode.ToString())!.GetRawConstantValue()!);
        }
    }

    [Fact]
    public void Vmx8VmcsFieldIds_AreFrozenCompatibilityAliases()
    {
        Assert.All(VmxSpecTable.Vmx8VmcsFields, spec =>
        {
            var enumValue = Enum.Parse<VmcsField>(spec.Name);
            Assert.Equal(spec.Id, (ushort)enumValue);
            Assert.Contains("compatibility alias", spec.CompatibilityNote, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(VmcsVisibilityPolicy.HostOwnedRuntimeEvidence, spec.VisibilityPolicy);
        });
    }

    [Fact]
    public void Vmx8CsrAliases_RemainRegisteredAtLegacyAddresses()
    {
        var csr = new CsrFile();

        Assert.All(VmxSpecTable.Vmx8Csrs, spec =>
        {
            ushort address = (ushort)typeof(CsrAddresses).GetField(spec.Name)!.GetRawConstantValue()!;
            Assert.Equal(spec.Address, address);
            Assert.True(csr.IsRegistered(address));
        });
    }

    [Fact]
    public void Vmx8ExitReasonValues_AreFrozenCompatibilitySnapshot()
    {
        Assert.All(VmxSpecTable.Vmx8ExitReasons, spec =>
        {
            var enumValue = Enum.Parse<VmExitReason>(spec.Name);
            Assert.Equal(spec.Value, (uint)enumValue);
        });
    }

    [Fact]
    public void VmxV2Drafts_AreNotCompatibilityTargetsInPhase00()
    {
        Assert.Equal("VMX8", VmxSpecTable.FrozenCompatibilityBaseline);
        Assert.False(VmxSpecTable.VmxV2DraftsAreCompatibilityTargets);
    }
}
