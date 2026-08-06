using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using System.Reflection;

namespace HybridCPU_ISE.Tests.Architecture;

public sealed class GeneratedIsaCatalogAuthorityTests
{
    [Fact]
    public void GeneratedCatalog_RuntimeFacadeKeepsIndependentIdentityAndCompleteValueParity()
    {
        Assert.NotSame(GeneratedIsaCatalog.Opcodes, OpcodeRegistry.Opcodes);
        Assert.Equal(GeneratedIsaCatalog.Opcodes, OpcodeRegistry.Opcodes);
    }

    [Fact]
    public void EveryGeneratedDescriptor_MatchesItsStaticOpcodeInfoParityMirror()
    {
        Assert.Equal(GeneratedIsaCatalog.Descriptors.Length, OpcodeRegistry.Opcodes.Length);
        var opcodesByValue = OpcodeRegistry.Opcodes.ToDictionary(opcode => opcode.OpCode);
        foreach (var descriptor in GeneratedIsaCatalog.Descriptors)
        {
            var opcode = opcodesByValue[descriptor.Opcode];
            Assert.Equal(descriptor.Opcode, opcode.OpCode);
            Assert.Equal(descriptor.Mnemonic, opcode.Mnemonic);
            Assert.Equal(descriptor.OpcodeCategory, opcode.Category);
            Assert.Equal(
                descriptor.OperandSchema == "operands-0"
                    ? (byte)0
                    : byte.Parse(descriptor.OperandSchema.AsSpan(9)),
                opcode.OperandCount);
            Assert.Equal(descriptor.InstructionFlags, opcode.Flags);
            Assert.Equal(descriptor.ExecutionLatency, opcode.ExecutionLatency);
            Assert.Equal(descriptor.MemoryBandwidth, opcode.MemoryBandwidth);
            Assert.Equal(descriptor.StaticClass, opcode.InstructionClass);
            Assert.Equal(descriptor.Serialization, opcode.SerializationClass);
        }
    }

    [Fact]
    public void InstructionClassifier_UsesGeneratedStaticClassAndSerializationForEveryDeclaredOpcode()
    {
        foreach (var descriptor in GeneratedIsaCatalog.Descriptors)
        {
            Assert.True(GeneratedIsaCatalog.TryGetDescriptor(descriptor.Opcode, out var resolved));
            Assert.Equal(descriptor, resolved);
            Assert.Equal(descriptor.StaticClass, InstructionClassifier.GetClass((ushort)descriptor.Opcode));
            Assert.Equal(descriptor.Serialization, InstructionClassifier.GetSerializationClass((ushort)descriptor.Opcode));
        }

        Assert.False(GeneratedIsaCatalog.TryGetDescriptor(uint.MaxValue, out _));
    }

    [Fact]
    public void InstructionClassifier_UnknownRawUshortPreservesTheLegacyCompatibilityFallback()
    {
        const ushort UnknownOpcode = ushort.MaxValue;

        Assert.False(GeneratedIsaCatalog.TryGetDescriptor(UnknownOpcode, out _));
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(UnknownOpcode));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(UnknownOpcode));
        Assert.Equal(
            (InstructionClass.ScalarAlu, SerializationClass.Free),
            InstructionClassifier.Classify(UnknownOpcode));
    }

    [Fact]
    public void OpcodeRegistry_UnknownAndZeroOpcodesPreserveAbsenceAndDiagnosticFallbacks()
    {
        const uint ZeroOpcode = 0;
        const uint InvalidOpcode = uint.MaxValue;

        foreach (uint opcode in new[] { ZeroOpcode, InvalidOpcode })
        {
            Assert.Null(OpcodeRegistry.GetInfo(opcode));
            Assert.False(OpcodeRegistry.TryGetMnemonic(opcode, out string mnemonic));
            Assert.Equal(string.Empty, mnemonic);
            Assert.Equal($"0x{opcode:X}", OpcodeRegistry.GetMnemonicOrHex(opcode));
            Assert.False(OpcodeRegistry.TryGetPublishedSemantics(opcode, out InstructionClass instructionClass, out SerializationClass serializationClass));
            Assert.Equal(default, instructionClass);
            Assert.Equal(default, serializationClass);
        }
    }

    [Fact]
    public void IsaV4Surface_ProjectsEveryStaticPolicyToTheGeneratedCatalog()
    {
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("MandatoryCoreClasses"), IsaV4Surface.MandatoryCoreClasses);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("MandatoryCoreOpcodes"), IsaV4Surface.MandatoryCoreOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("SystemDeviceCommandOpcodes"), IsaV4Surface.SystemDeviceCommandOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("CarrierOnlyOpcodes"), IsaV4Surface.CarrierOnlyOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("MandatoryInteger64RepairOpcodes"), IsaV4Surface.MandatoryInteger64RepairOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("DescriptorOnlyOpcodes"), IsaV4Surface.DescriptorOnlyOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("ParserOnlyOpcodes"), IsaV4Surface.ParserOnlyOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("OptionalEnabledOpcodes"), IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("OptionalDisabledOpcodes"), IsaV4Surface.OptionalDisabledOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("ReservedOpcodes"), IsaV4Surface.ReservedOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("ProhibitedOpcodes"), IsaV4Surface.ProhibitedOpcodes);
        Assert.Same(GeneratedIsaCatalog.GetStaticPolicy("OptionalExtensions"), IsaV4Surface.OptionalExtensions);
        Assert.Same(GeneratedIsaCatalog.PipelineClassMap, IsaV4Surface.PipelineClassMap);
    }

    [Fact]
    public void GeneratedStaticPolicy_UnknownIdentifierFailsClosedRatherThanReturningAnEmptyPolicy()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => GeneratedIsaCatalog.GetStaticPolicy("RF13-unknown-static-policy"));
    }

    [Fact]
    public void GeneratedCompatibilityFacade_ProvidesTheRetainedPublicLiteralAbi()
    {
        var retained = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(ushort))
            .ToDictionary(field => field.Name, field => Assert.IsType<ushort>(field.GetRawConstantValue()));
        var enumValues = Enum.GetValues<YAKSys_Hybrid_CPU.Processor.CPU_Core.InstructionsEnum>()
            .ToDictionary(opcode => opcode.ToString(), opcode => (ushort)opcode);

        Assert.Equal(enumValues, retained);
        Assert.Equal((ushort)0, retained["Nope"]);
    }

}
