using System.Reflection;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;

namespace HybridCPU_ISE.Tests.Phase12;

[CollectionDefinition("Phase12 Live Compat Ingress Boundary", DisableParallelization = true)]
public sealed class Phase12LiveCompatIngressBoundaryCollection;

[Collection("Phase12 Live Compat Ingress Boundary")]
public sealed class Phase12LiveCompatIngressBoundaryTests
{
    [Fact]
    public void StreamExecutionRequest_CreateValidatedCompatIngress_IgnoresCompatOnlyVirtualThreadHint()
    {
        var baseline = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xA5,
            DestSrc1Pointer = 0x240UL,
            Src2Pointer = 0x340UL,
            Immediate = 13,
            StreamLength = 4,
            Stride = 4,
            RowStride = 64,
            Indexed = true,
            Is2D = true,
            TailAgnostic = true
        };

        VLIW_Instruction hinted = baseline;
        hinted.VirtualThreadId = 3;

        StreamExecutionRequest baselineRequest =
            StreamExecutionRequest.CreateValidatedCompatIngress(in baseline);
        StreamExecutionRequest hintedRequest =
            StreamExecutionRequest.CreateValidatedCompatIngress(in hinted);

        Assert.Equal(baselineRequest.OpCode, hintedRequest.OpCode);
        Assert.Equal(baselineRequest.DataTypeValue, hintedRequest.DataTypeValue);
        Assert.Equal(baselineRequest.PredicateMask, hintedRequest.PredicateMask);
        Assert.Equal(baselineRequest.DestSrc1Pointer, hintedRequest.DestSrc1Pointer);
        Assert.Equal(baselineRequest.Src2Pointer, hintedRequest.Src2Pointer);
        Assert.Equal(baselineRequest.Immediate, hintedRequest.Immediate);
        Assert.Equal(baselineRequest.StreamLength, hintedRequest.StreamLength);
        Assert.Equal(baselineRequest.Stride, hintedRequest.Stride);
        Assert.Equal(baselineRequest.RowStride, hintedRequest.RowStride);
        Assert.Equal(baselineRequest.Indexed, hintedRequest.Indexed);
        Assert.Equal(baselineRequest.Is2D, hintedRequest.Is2D);
        Assert.Equal(baselineRequest.TailAgnostic, hintedRequest.TailAgnostic);
        Assert.Equal(baselineRequest.MaskAgnostic, hintedRequest.MaskAgnostic);
        Assert.Null(typeof(StreamExecutionRequest).GetProperty(
            "VirtualThreadId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Null(typeof(StreamExecutionRequest).GetProperty(
            "Word3",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    [Fact]
    public void StreamExecutionRequest_CreateValidatedCompatIngress_RejectsRetiredPolicyGapBit()
    {
        var invalid = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xA5,
            DestSrc1Pointer = 0x240UL,
            Src2Pointer = 0x340UL,
            Immediate = 13,
            StreamLength = 4,
            Stride = 4,
            RowStride = 64,
            Indexed = true,
            Is2D = true,
            TailAgnostic = true
        };
        invalid.Word3 |= 1UL << 50;

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => StreamExecutionRequest.CreateValidatedCompatIngress(in invalid));

        Assert.Equal(
            VLIW_Instruction.GetRetiredPolicyGapViolationMessage(),
            ex.Message);
    }

    [Fact]
    public void ExecuteStreamInstruction_WhenVirtualThreadHintDiffers_ThenRuntimeResultMatchesCanonicalIngress()
    {
        static Processor.CPU_Core CreateSeededCore()
        {
            var core = new Processor.CPU_Core(0)
            {
                ActiveVirtualThreadId = 0
            };

            core.WriteCommittedArch(0, 1, 7UL);
            core.WriteCommittedArch(0, 2, 5UL);
            core.WriteCommittedArch(0, 9, 1UL);
            return core;
        }

        var baseline = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
            StreamLength = 1
        };
        VLIW_Instruction hinted = baseline;
        hinted.VirtualThreadId = 3;

        Processor.CPU_Core baselineCore = CreateSeededCore();
        Processor.CPU_Core hintedCore = CreateSeededCore();

        baselineCore.ExecuteDirectStreamCompat(baseline);
        hintedCore.ExecuteDirectStreamCompat(hinted);

        Assert.Equal(baselineCore.ReadArch(0, 9), hintedCore.ReadArch(0, 9));
        Assert.Equal(baselineCore.ReadActiveVirtualThreadId(), hintedCore.ReadActiveVirtualThreadId());
    }

    [Fact]
    public void ExecuteStreamInstruction_WhenRetiredPolicyGapBitIsSet_ThenFailsClosed()
    {
        var core = new Processor.CPU_Core(0)
        {
            ActiveVirtualThreadId = 0
        };

        var invalid = new VLIW_Instruction
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Addition,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = VLIW_Instruction.PackArchRegs(9, 1, 2),
            StreamLength = 1
        };
        invalid.Word3 |= 1UL << 50;

        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>(
            () => core.ExecuteDirectStreamCompat(invalid));

        Assert.Equal(
            VLIW_Instruction.GetRetiredPolicyGapViolationMessage(),
            ex.Message);
    }
}
