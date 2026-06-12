using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.Core
{
    public static partial class InstructionRegistry
    {
        private static void RegisterMatrixTileInstructions()
        {
            RegisterMatrixTileMicroOp((uint)InstructionsEnum.MTILE_LOAD);
            RegisterMatrixTileMicroOp((uint)InstructionsEnum.MTILE_STORE);
            RegisterMatrixTileMicroOp((uint)InstructionsEnum.MTILE_MACC);
            RegisterMatrixTileMicroOp((uint)InstructionsEnum.MTRANSPOSE);
        }

        public static bool IsMatrixTileMaterializerRegistered(uint opCode)
        {
            if (opCode > ushort.MaxValue ||
                !System.Enum.IsDefined(typeof(InstructionsEnum), (ushort)opCode))
            {
                return false;
            }

            return MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode((InstructionsEnum)opCode);
        }

        public static bool TryCreateMatrixTileRuntimeObject(
            InstructionIR instruction,
            out MatrixTileMaterializedInstruction materializedInstruction,
            out MatrixTileIrProjectionFaultKind faultKind)
        {
            InstructionsEnum opcode = instruction.CanonicalOpcode.ToInstructionsEnum();
            if (!IsMatrixTileMaterializerRegistered((uint)opcode))
            {
                materializedInstruction = default;
                faultKind = MatrixTileIrProjectionFaultKind.NonMatrixTileOpcode;
                return false;
            }

            return MatrixTileIrProjectionAndMaterializer.TryMaterialize(
                instruction,
                out materializedInstruction,
                out faultKind);
        }

        public static bool IsMatrixTileMicroOpRegistered(uint opCode)
        {
            return IsRegistered(opCode) && IsMatrixTileMaterializerRegistered(opCode);
        }

        private static void RegisterMatrixTileMicroOp(uint opCode)
        {
            RegisterSemanticFactory(opCode, ctx =>
            {
                MatrixTileMaterializedInstruction materializedInstruction =
                    CreateRequiredMatrixTileRuntimeObject(in ctx);
                MatrixTileMicroOp microOp = CreateTypedMatrixTileMicroOp(materializedInstruction);
                microOp.InitializeMetadata();
                return microOp;
            });

            OpcodeInfo opcodeInfo = RequirePublishedOpcodeInfo(opCode);
            RegisterOpAttributes(opCode, new MicroOpDescriptor
            {
                Latency = opcodeInfo.ExecutionLatency,
                MemFootprintClass = opCode is (uint)InstructionsEnum.MTILE_LOAD or (uint)InstructionsEnum.MTILE_STORE
                    ? 3
                    : 0,
                WritesRegister = false,
                IsMemoryOp = opCode is (uint)InstructionsEnum.MTILE_LOAD or (uint)InstructionsEnum.MTILE_STORE
            });
        }

        private static MatrixTileMaterializedInstruction CreateRequiredMatrixTileRuntimeObject(
            in DecoderContext ctx)
        {
            if (ctx.OpCode > ushort.MaxValue ||
                !System.Enum.IsDefined(typeof(InstructionsEnum), (ushort)ctx.OpCode))
            {
                throw new DecodeProjectionFaultException(
                    $"Opcode 0x{ctx.OpCode:X} reached the MTILE Phase08 MicroOp factory without canonical opcode identity.");
            }

            InstructionsEnum opcode = (InstructionsEnum)ctx.OpCode;
            if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode))
            {
                throw new DecodeProjectionFaultException(
                    $"Opcode {opcode} reached the MTILE Phase08 MicroOp factory without a runtime-owned VLM matrix row.");
            }

            VectorInstructionPayload payload = GetRequiredProjectedMatrixTilePayload(in ctx, opcode);
            MatrixTileInstructionIrProjection projection =
                MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                    opcode,
                    payload,
                    ctx.HasImmediate ? ctx.Immediate : 0);

            if (!MatrixTileIrProjectionAndMaterializer.TryMaterialize(
                    projection,
                    out MatrixTileMaterializedInstruction materializedInstruction,
                    out MatrixTileIrProjectionFaultKind faultKind))
            {
                throw new DecodeProjectionFaultException(
                    $"{opcode} Phase08 MicroOp factory rejected illegal MTILE IR projection: {faultKind}.");
            }

            return materializedInstruction;
        }

        private static VectorInstructionPayload GetRequiredProjectedMatrixTilePayload(
            in DecoderContext ctx,
            InstructionsEnum opcode)
        {
            if (!ctx.HasVectorPayload)
            {
                throw new DecodeProjectionFaultException(
                    $"{opcode} requires projected matrix/tile vector-payload handoff before typed MicroOp materialization.");
            }

            return new VectorInstructionPayload(
                ctx.VectorPrimaryPointer,
                ctx.VectorSecondaryPointer,
                ctx.VectorStreamLength,
                ctx.VectorStride,
                ctx.VectorRowStride,
                ctx.HasVectorAddressingContour && ctx.IndexedAddressing,
                ctx.HasVectorAddressingContour && ctx.Is2DAddressing,
                ctx.TailAgnostic,
                ctx.MaskAgnostic,
                ctx.Saturating,
                checked((byte)ctx.PredicateMask),
                GetRequiredDecoderDataType(
                    in ctx,
                    $"Matrix/tile opcode {opcode}"));
        }

        private static MatrixTileMicroOp CreateTypedMatrixTileMicroOp(
            MatrixTileMaterializedInstruction materializedInstruction)
        {
            return materializedInstruction.OperationKind switch
            {
                MatrixTileProjectedOperationKind.Load => new MtileLoadMicroOp(materializedInstruction),
                MatrixTileProjectedOperationKind.Store => new MtileStoreMicroOp(materializedInstruction),
                MatrixTileProjectedOperationKind.Macc => new MtileMaccMicroOp(materializedInstruction),
                MatrixTileProjectedOperationKind.Transpose => new MtransposeMicroOp(materializedInstruction),
                _ => throw new DecodeProjectionFaultException(
                    $"Unsupported MTILE Phase08 materialized operation kind {materializedInstruction.OperationKind}.")
            };
        }
    }
}
