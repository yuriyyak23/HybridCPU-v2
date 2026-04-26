using System;
using System.Linq;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;

namespace HybridCPU_ISE.Tests;

public sealed class Phase03PublicFacadeOpcodeSurfaceTests
{
    [Fact]
    public void PublicCompilerFacadeMethods_DoNotExposeInstructionsEnumParameters()
    {
        Type instructionsEnumType = typeof(Processor.CPU_Core.InstructionsEnum);

        Type[] facadeTypes =
        [
            typeof(IAppAsmFacade),
            typeof(IPlatformAsmFacade),
            typeof(IExpertBackendFacade),
            typeof(AppAsmFacade),
            typeof(PlatformAsmFacade),
            typeof(ExpertBackendFacade),
        ];

        string[] leakedMethods = facadeTypes
            .SelectMany(type => type.GetMethods())
            .Where(method => method.GetParameters().Any(parameter => parameter.ParameterType == instructionsEnumType))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .Distinct()
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(leakedMethods);
    }

    [Fact]
    public void PlatformAsmFacade_VectorHelpers_AcceptCanonicalIsaOpcodeAndEmitMatchingCarrier()
    {
        var context = new HybridCpuThreadCompilerContext(0);
        var facade = new PlatformAsmFacade(0, context);
        Processor.CPU_Core.IsaOpcode opcode = Processor.CPU_Core.InstructionsEnum.VADD;

        facade.VectorOp(
            opcode,
            DataTypeEnum.INT32,
            dest: 0x1000,
            src: 0x2000,
            streamLength: 8,
            stride: 4);

        Assert.Equal(1, context.InstructionCount);
        VLIW_Instruction instruction = context.GetCompiledInstructions()[0];

        Assert.Equal((uint)opcode, instruction.OpCode);
        Assert.Equal((byte)DataTypeEnum.INT32, instruction.DataType);
        Assert.Equal((ulong)0x1000, instruction.DestSrc1Pointer);
        Assert.Equal((ulong)0x2000, instruction.Src2Pointer);
        Assert.Equal((uint)8, instruction.StreamLength);
        Assert.Equal((ushort)4, instruction.Stride);
    }

    [Fact]
    public void PlatformAsmFacade_VectorImmediateHelpers_AcceptCanonicalIsaOpcodeAndEmitMatchingCarrier()
    {
        var context = new HybridCpuThreadCompilerContext(0);
        var facade = new PlatformAsmFacade(0, context);
        Processor.CPU_Core.IsaOpcode opcode = Processor.CPU_Core.InstructionsEnum.VSLL;

        facade.VectorOpImm(
            opcode,
            DataTypeEnum.UINT32,
            immediate: 3,
            dest: 0x3000,
            src: 0x4000,
            streamLength: 16,
            stride: 8);

        Assert.Equal(1, context.InstructionCount);
        VLIW_Instruction instruction = context.GetCompiledInstructions()[0];

        Assert.Equal((uint)opcode, instruction.OpCode);
        Assert.Equal((byte)DataTypeEnum.UINT32, instruction.DataType);
        Assert.Equal((ushort)3, instruction.Immediate);
        Assert.Equal((ulong)0x3000, instruction.DestSrc1Pointer);
        Assert.Equal((ulong)0x4000, instruction.Src2Pointer);
        Assert.Equal((uint)16, instruction.StreamLength);
        Assert.Equal((ushort)8, instruction.Stride);
    }

    [Fact]
    public void PlatformFacade_DoesNotExposeLegacyPowerStateHelper()
    {
        Assert.Null(typeof(IPlatformAsmFacade).GetMethod("PowerState"));
        Assert.Null(typeof(PlatformAsmFacade).GetMethod("PowerState"));
    }
}

