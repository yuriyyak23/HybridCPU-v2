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

    [Fact]
    public void ExplicitIndexedScatterOpcode_HasPublishedCarrierSet()
    {
        Assert.True(InstructionRegistry.IsRegistered((uint)Processor.CPU_Core.InstructionsEnum.VSCATTER));
    }
}
