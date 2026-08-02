using System;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Registers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-05 public-facade compatibility matrix. Static expected-illegal outcomes
/// use the typed internal carrier, while runtime owner/domain evidence remains
/// outside that carrier and still fails closed at the public boundary.
/// </summary>
public sealed class DecoderFacadeCompatibilityTests
{
    [Theory]
    [InlineData(DecodeFailureCode.Unknown)]
    [InlineData(DecodeFailureCode.UnknownOpcode)]
    [InlineData(DecodeFailureCode.ProhibitedOpcode)]
    [InlineData(DecodeFailureCode.ReservedEncoding)]
    [InlineData(DecodeFailureCode.OperandEncoding)]
    [InlineData(DecodeFailureCode.ExtensionPayload)]
    [InlineData(DecodeFailureCode.Sideband)]
    [InlineData(DecodeFailureCode.BundleShape)]
    [InlineData(DecodeFailureCode.UnsupportedOpcode)]
    public void EveryDecodeFailureCode_MapsToTheStablePublicExceptionCarrier(DecodeFailureCode code)
    {
        var raw = new VLIW_Instruction { OpCode = IsaOpcodeValues.ADD };
        DecodeFailure failure = DecodeFailure.Create(code, 2, "matrix", new byte[] { 1, 2, 3 }, "typed matrix failure");

        InvalidOpcodeException exception = DecodeFailureCompatibilityAdapter.ToInvalidOpcodeException(failure, in raw);

        Assert.Equal(2, exception.SlotIndex);
        Assert.Equal("ADD", exception.OpcodeIdentifier);
        Assert.Equal(code == DecodeFailureCode.ProhibitedOpcode, exception.IsProhibited);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void DmaOwnerDomainReject_IsPostDecodeRuntimeAuthority_NotDecodeFailure()
    {
        DmaStreamComputeDescriptor rejectedDescriptor = DmaStreamComputeTestDescriptorFactory.CreateDescriptor() with
        {
            OwnerGuardDecision = default,
        };
        var slots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        slots[6] = new VLIW_Instruction { OpCode = IsaOpcodeValues.DmaStreamCompute };
        var metadata = new InstructionSlotMetadata[BundleMetadata.BundleSlotCount];
        Array.Fill(metadata, InstructionSlotMetadata.Default);
        metadata[6] = new InstructionSlotMetadata(VtId.Create(0), SlotMetadata.NotStealable)
        {
            DmaStreamComputeDescriptor = rejectedDescriptor,
        };
        var annotations = new VliwBundleAnnotations(metadata);

        Assert.True(DeclarativeDecoderPipeline.TryDecodeBundle(
            slots,
            annotations,
            bundleAddress: 0x6500,
            bundleSerial: 9,
            out DeclarativeDecodedBundle? declarative,
            out DecodeFailure? decodeFailure));
        Assert.NotNull(declarative);
        Assert.Null(decodeFailure);

        InvalidOpcodeException exception = Assert.Throws<InvalidOpcodeException>(() =>
            new VliwDecoderV4().DecodeInstructionBundle(slots, annotations, 0x6500, 9));
        Assert.Equal(6, exception.SlotIndex);
        Assert.False(exception.IsProhibited);
        Assert.Contains("owner/domain guard", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
