// CRC64 is a scalar CRC negative-decision gate: no opcode/materializer/helper authority
// until polynomial/reflection/seed/final-xor/endian ABI is explicit. VMX stays generic.
namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC;

public sealed partial class Crc64Instruction
{
    public const string Mnemonic = "CRC64";
    public const string OperandShape = "rd, rs_seed, rs_data";
    public const string ParameterDescriptor = "rd receives a 64-bit CRC result once polynomial/reflection ABI is frozen";
    public const string MicroOpShape = "ScalarCrcMicroOp candidate; no MicroOp is published in this template";
    public const string ExecutionLaneBinding = "Lanes00_03Scalar";
    public const string EvidenceBoundary = "CrcPolynomialAbiDeferredNoEmission";
    public const string AbiDecision = "NoAllocationUntilPolynomialReflectionSeedFinalXorEndianAbi";
    public const string PolynomialPolicy = "Unspecified: CRC64 does not imply ECMA, ISO, Jones, XZ, or any other polynomial.";
    public const string ReflectionPolicy = "Unspecified: input/output bit reflection must be explicit before execution.";
    public const string SeedFinalXorPolicy = "Unspecified: seed initialization and final XOR must be explicit before execution.";
    public const string EndianPolicy = "Unspecified: byte/word ingestion order must be explicit before execution.";
    public const bool RequiresPolynomialAbi = true;
    public const bool RequiresReflectionAbi = true;
    public const bool RequiresSeedFinalXorAbi = true;
    public const bool RequiresEndianPolicyAbi = true;
    public const bool RejectImplicitPolynomialSelection = true;
    public const bool NoHiddenMultiOpEmission = true;
    public const bool RequiresDecoderEncoderAbi = true;
    public const bool RequiresInstructionIrProjection = true;
    public const bool RequiresRegistryMaterializer = true;
    public const bool RequiresRetireRegisterWriteback = true;
    public const bool RequiresReplayRollbackEvidence = true;
    public const bool NoVmxFrontendIntegrationRequired = true;
    public const bool RequiresVmxProjection = false;
    public const bool HasOpcodeAllocation = false;
    public const bool IsExecutable = false;
    public const bool CompilerHelperAllowed = false;
}
