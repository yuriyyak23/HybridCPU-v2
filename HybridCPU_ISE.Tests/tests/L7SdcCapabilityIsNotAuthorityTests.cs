using System;
using System.Linq;
using System.Reflection;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

namespace HybridCPU_ISE.Tests.MemoryAccelerators;

public sealed class L7SdcCapabilityIsNotAuthorityTests
{
    [Fact]
    public void L7SdcCapabilityIsNotAuthority_RegistrationDoesNotCreateOpcodesOrDecodeAuthority()
    {
        InstructionRegistry.Clear();
        InstructionRegistry.Initialize();
        AcceleratorCapabilityRegistry registry = L7SdcCapabilityRegistryTests.CreateRegistry();

        Assert.Equal(1, registry.Count);
        Assert.False(InstructionRegistry.IsCustomAcceleratorOpcode(0xC000));
        Assert.False(InstructionRegistry.IsRegistered(0xC000));

        var decoder = new VliwDecoderV4();
        var instruction = new VLIW_Instruction
        {
            OpCode = 0xC000,
            DataTypeValue = DataTypeEnum.INT32,
            Word1 = VLIW_Instruction.PackArchRegs(1, 2, 3)
        };

        void Decode() => decoder.Decode(in instruction, slotIndex: 7);
        InvalidOpcodeException ex = Assert.Throws<InvalidOpcodeException>((Action)Decode);
        Assert.Contains("canonical ISA v4 opcode space", ex.Message);
    }

    [Fact]
    public void L7SdcCapabilityIsNotAuthority_RegistryCannotCreateMicroOpsOrSubmitCommands()
    {
        MethodInfo[] methods = typeof(AcceleratorCapabilityRegistry).GetMethods(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(methods, static method =>
            typeof(MicroOp).IsAssignableFrom(method.ReturnType) ||
            method.Name.Contains("Decode", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Submit", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Commit", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Authorize", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void L7SdcCapabilityIsNotAuthority_RegistryHasNoPlacementOrLaneBypassSurface()
    {
        MethodInfo[] methods = typeof(AcceleratorCapabilityRegistry).GetMethods(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(methods, static method =>
            method.ReturnType == typeof(SlotClass) ||
            method.GetParameters().Any(parameter => parameter.ParameterType == typeof(SlotClass)));
    }

    [Fact]
    public void L7SdcCapabilityIsNotAuthority_QueryResultCannotAuthorizeDecodeSubmitExecuteOrCommit()
    {
        AcceleratorCapabilityRegistry registry = L7SdcCapabilityRegistryTests.CreateRegistry();

        AcceleratorCapabilityQueryResult result = registry.Query("matmul.fixture.v1");

        Assert.True(result.IsMetadataAvailable);
        Assert.False(result.GrantsDecodeAuthority);
        Assert.False(result.GrantsCommandSubmissionAuthority);
        Assert.False(result.GrantsExecutionAuthority);
        Assert.False(result.GrantsCommitAuthority);
    }

    [Fact]
    public void L7SdcCapabilityIsNotAuthority_OperationSupportIsNotDescriptorOrOwnerDomainAcceptance()
    {
        AcceleratorCapabilityDescriptor descriptor =
            L7SdcCapabilityRegistryTests.CreateDescriptor();

        Assert.True(descriptor.TryGetOperation("matmul", out AcceleratorOperationCapability? operation));
        Assert.True(operation!.SupportsDatatype("f32"));
        Assert.True(operation.SupportsShape("matrix-2d", elementCount: 64, rank: 2));

        AcceleratorCapabilityQueryResult metadata =
            AcceleratorCapabilityQueryResult.MetadataAvailable(descriptor);
        Assert.False(metadata.GrantsCommandSubmissionAuthority);
        Assert.False(metadata.GrantsExecutionAuthority);
        Assert.False(metadata.GrantsCommitAuthority);
    }
}
