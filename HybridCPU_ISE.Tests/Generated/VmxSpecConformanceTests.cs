using System;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Generated;

public sealed class VmxSpecConformanceTests
{
    [Fact]
    public void GeneratedVmx8OpcodeSpec_MatchesPublishedRegistryAndRuntimeTables()
    {
        foreach (VmxOpcodeSpec spec in VmxSpecTable.Vmx8Opcodes)
        {
            OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo(spec.Opcode));
            Assert.Equal(spec.Mnemonic, info.Mnemonic);
            Assert.Equal(spec.OperandCount, info.OperandCount);
            Assert.Equal(spec.Flags, info.Flags);
            Assert.Equal(spec.InstructionClass, info.InstructionClass);
            Assert.Equal(spec.SerializationClass, info.SerializationClass);
            Assert.Equal(spec.InternalOpKindName, InternalOpBuilder.MapToKind(spec.Opcode).ToString());
        }
    }

    [Fact]
    public void GeneratedVmx8VmcsFieldSpec_MatchesEnumAndExcludesHostEvidence()
    {
        string[] enumNames = Enum.GetNames<VmcsField>();
        Assert.Equal(enumNames.OrderBy(name => name), VmxSpecTable.Vmx8VmcsFields.Select(field => field.Name).OrderBy(name => name));

        foreach (VmcsFieldSpec spec in VmxSpecTable.Vmx8VmcsFields)
        {
            Assert.Equal(spec.Id, (ushort)Enum.Parse<VmcsField>(spec.Name));
            Assert.NotEqual(VmcsVisibilityPolicy.HostOwnedRuntimeEvidence, spec.VisibilityPolicy);
        }
    }

    [Fact]
    public void GeneratedVmx8CsrSpec_MatchesLegacyAliasAddressesAndRegistration()
    {
        var csr = new CsrFile();

        foreach (VmxCsrSpec spec in VmxSpecTable.Vmx8Csrs)
        {
            ushort address = (ushort)typeof(CsrAddresses).GetField(spec.Name)!.GetRawConstantValue()!;
            Assert.Equal(spec.Address, address);
            Assert.True(csr.IsRegistered(address));
        }
    }

    [Fact]
    public void GeneratedVmx8ExitReasonSpec_MatchesEnumValues()
    {
        string[] enumNames = Enum.GetNames<VmExitReason>();
        Assert.All(VmxSpecTable.Vmx8ExitReasons, spec => Assert.Contains(spec.Name, enumNames));

        foreach (VmExitReasonSpec spec in VmxSpecTable.Vmx8ExitReasons)
        {
            Assert.Equal(spec.Value, (uint)Enum.Parse<VmExitReason>(spec.Name));
        }
    }

    [Fact]
    public void GeneratedVmx8MicroOpSpec_PublishesLane7SystemSingletonAndNoScalarMemoryEffects()
    {
        foreach (VmxOpcodeSpec spec in VmxSpecTable.Vmx8Opcodes)
        {
            var microOp = Assert.IsType<VmxMicroOp>(InstructionRegistry.CreateMicroOp(
                spec.Opcode,
                new DecoderContext
                {
                    OpCode = spec.Opcode,
                    Reg1ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.WritesRd) ? (ushort)7 : (ushort)0,
                    Reg2ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs1) ||
                             spec.OperandForm == VmxOperandForm.ReservedIgnoredRegister
                        ? (ushort)2
                        : (ushort)0,
                    Reg3ID = spec.RegisterRoleMask.HasFlag(VmxRegisterRoleMask.ReadsRs2) ? (ushort)3 : (ushort)0,
                }));

            Assert.Equal(InstructionClass.Vmx, microOp.InstructionClass);
            Assert.Equal(SerializationClass.VmxSerial, microOp.SerializationClass);
            Assert.Equal(SlotClass.SystemSingleton, microOp.AdmissionMetadata.Placement.RequiredSlotClass);
            Assert.Equal(7, microOp.AdmissionMetadata.Placement.PinnedLaneId);
            Assert.False(microOp.IsMemoryOp);
        }
    }
}
