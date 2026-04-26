using System;
using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09RawVectorCarrierCoverageTests
{
    public static IEnumerable<object[]> RawCapableVectorOpcodesWithPublishedCarrier()
    {
        foreach (Processor.CPU_Core.InstructionsEnum opcode in Enum.GetValues<Processor.CPU_Core.InstructionsEnum>())
        {
            OpcodeInfo? info = OpcodeRegistry.GetInfo((uint)opcode);
            if (!info.HasValue || !info.Value.IsVector)
            {
                continue;
            }

            if (opcode is Processor.CPU_Core.InstructionsEnum.VGATHER or Processor.CPU_Core.InstructionsEnum.VSCATTER)
            {
                continue;
            }

            yield return new object[] { opcode };
        }
    }

    [Theory]
    [MemberData(nameof(RawCapableVectorOpcodesWithPublishedCarrier))]
    public void RawCapableVectorOpcodeFamilies_HavePublishedInstructionRegistryCarrier(
        Processor.CPU_Core.InstructionsEnum opcode)
    {
        Assert.True(
            InstructionRegistry.IsRegistered((uint)opcode),
            $"Expected raw-capable vector opcode {(uint)opcode} ({opcode}) to keep a published InstructionRegistry carrier.");
    }

    [Theory]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VGATHER)]
    [InlineData(Processor.CPU_Core.InstructionsEnum.VSCATTER)]
    public void ExplicitFailClosedIndexedTransferOpcodes_RemainOutsidePublishedCarrierSet(
        Processor.CPU_Core.InstructionsEnum opcode)
    {
        Assert.False(InstructionRegistry.IsRegistered((uint)opcode));
    }
}
