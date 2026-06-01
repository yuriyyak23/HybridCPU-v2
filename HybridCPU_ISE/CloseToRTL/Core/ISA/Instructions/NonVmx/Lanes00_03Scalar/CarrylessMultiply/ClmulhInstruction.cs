// Production paths: CPU_Core.Enums opcode/IsaOpcodeValues; InstructionSupportStatus/IsaV4Surface;
// OpcodeInfo.Registry.Data.Scalar; InstructionEncoder.EncodeScalar register-register ABI with Immediate=0;
// VliwDecoderV4 canonical rd,rs1,rs2 rejection of immediate aliases; InstructionIR/DecodedBundleTransportProjector;
// InstructionRegistry scalar carry-less factory/materializer; InternalOpBuilder/InternalOpKind;
// ScalarALUMicroOp + ScalarAluOps + ExecutionDispatcherV4; retire x0/writeback and replay/rollback;
// Phase 04 golden/conformance tests; compiler no-emission/helper guardrails; generic VMX boundary only.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CarrylessMultiply;

public sealed partial class ClmulhInstruction
{
    public const string Mnemonic = "CLMULH";
    public const string OperandShape = "rd, rs1, rs2";
    public const string ParameterDescriptor = "rd receives carry-less product bits [127:64] for XLEN=64";
    public const string MicroOpShape = "ScalarCarryLessMultiply.HighWindow, retire-owned register writeback, no flags";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "ExecutableScalarAlu";
    public const string BitOrderPolicy = "Bit index 0 is the least-significant bit; windowing follows the closed CLMUL low-half convention.";
    public const string Operation = "carryless_product(rs1, rs2) >> 64";

    public const int XLen = 64;
    public const int ProductWindowStartBit = 64;

    public const bool RequiresBitOrderAbi = false;
    public const bool NoHiddenMultiOpEmission = true;
    public const bool RequiresDecoderEncoderAbi = false;
    public const bool RequiresInstructionIrProjection = false;
    public const bool RequiresRegistryMaterializer = false;
    public const bool RequiresRetireRegisterWriteback = false;
    public const bool RequiresReplayRollbackEvidence = false;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresVmxProjection = false;
    public const bool HasOpcodeAllocation = true;
    public const bool IsExecutable = true;
    public const bool CompilerHelperAllowed = false;

    public static ushort Opcode => (ushort)Processor.CPU_Core.InstructionsEnum.CLMULH;

    public static bool WritesScalarRegister => true;

    public static bool HasSideEffects => false;

    public static ulong Execute(ulong multiplicand, ulong multiplier) =>
        CarryLessMultiplyWindow64(multiplicand, multiplier, ProductWindowStartBit);

    public static ulong EvaluateXLen64(ulong multiplicand, ulong multiplier) =>
        Execute(multiplicand, multiplier);

    public static bool RetireWritesDestination(ushort destinationRegister) =>
        destinationRegister != 0;

    public static CarryLessGoldenVector[] GetLocalGoldenVectors() =>
        new CarryLessGoldenVector[]
        {
            new CarryLessGoldenVector(0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL, 0x0000_0000_0000_0000UL),
            new CarryLessGoldenVector(0xFFFF_FFFF_FFFF_FFFFUL, 0xFFFF_FFFF_FFFF_FFFFUL, 0x5555_5555_5555_5555UL),
            new CarryLessGoldenVector(0x8000_0000_0000_0000UL, 0x0000_0000_0000_0002UL, 0x0000_0000_0000_0001UL),
            new CarryLessGoldenVector(0x1234_5678_9ABC_DEF0UL, 0x0FED_CBA9_8765_4321UL, 0x00E0_38D8_6888_50B0UL),
        };

    private static ulong CarryLessMultiplyWindow64(ulong multiplicand, ulong multiplier, int startBit)
    {
        ulong result = 0UL;

        for (int multiplierBit = 0; multiplierBit < XLen; multiplierBit++)
        {
            if (((multiplier >> multiplierBit) & 1UL) == 0)
            {
                continue;
            }

            for (int multiplicandBit = 0; multiplicandBit < XLen; multiplicandBit++)
            {
                if (((multiplicand >> multiplicandBit) & 1UL) == 0)
                {
                    continue;
                }

                int resultBit = multiplierBit + multiplicandBit - startBit;
                if ((uint)resultBit < (uint)XLen)
                {
                    result ^= 1UL << resultBit;
                }
            }
        }

        return result;
    }

    public readonly struct CarryLessGoldenVector
    {
        public CarryLessGoldenVector(ulong multiplicand, ulong multiplier, ulong expected)
        {
            Multiplicand = multiplicand;
            Multiplier = multiplier;
            Expected = expected;
        }

        public ulong Multiplicand { get; }

        public ulong Multiplier { get; }

        public ulong Expected { get; }
    }
}
