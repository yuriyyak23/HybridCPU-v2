using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR;

internal static class CompilerMatrixTileEmissionLowerer
{
    public static CompilerMatrixTileEmissionPlan Lower(CompilerMatrixTileEmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        VLIW_Instruction instruction = BuildInstruction(request);
        return CreatePlan(request, instruction);
    }

    public static bool TryRecoverFromInstruction(
        InstructionsEnum opcode,
        in VLIW_Instruction instruction,
        MatrixTileNumericPolicy? matrixTileNumericPolicy,
        MatrixTileLayoutPolicy? matrixTileLayoutPolicy,
        out CompilerMatrixTileEmissionPlan? plan)
    {
        plan = null;
        if (!CompilerMatrixTilePositiveEmissionAbiContract.IsMatrixTilePositiveOpcode(opcode))
        {
            return false;
        }

        MatrixTileInstructionIrProjection projection =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                opcode,
                CreatePayload(
                    in instruction,
                    matrixTileNumericPolicy,
                    matrixTileLayoutPolicy),
                instruction.Immediate,
                requireExplicitNumericPolicy: true);
        if (projection.FaultKind != MatrixTileIrProjectionFaultKind.None)
        {
            return false;
        }

        CompilerMatrixTileEmissionRequest request =
            RecoverRequest(opcode, in instruction, projection);
        plan = CreatePlan(request, instruction);
        return true;
    }

    private static CompilerMatrixTileEmissionPlan CreatePlan(
        CompilerMatrixTileEmissionRequest request,
        in VLIW_Instruction instruction)
    {
        CompilerMatrixTilePositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(request.Mnemonic);
        MatrixTileCompilerEmissionHandoffRow handoffRow =
            MatrixTileCompilerEmissionHandoffPackage.GetRow(request.Mnemonic);

        if (handoffRow.Opcode != request.Opcode)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission request does not match the Phase 13 runtime opcode authority.");
        }

        VectorInstructionPayload payload = CreatePayload(
            in instruction,
            request.MatrixTileNumericPolicy,
            request.MatrixTileLayoutPolicy);
        MatrixTileInstructionIrProjection projection =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                request.Opcode,
                payload,
                instruction.Immediate,
                requireExplicitNumericPolicy: true);

        if (!MatrixTileIrProjectionAndMaterializer.TryMaterialize(
                projection,
                out MatrixTileMaterializedInstruction materializedInstruction,
                out MatrixTileIrProjectionFaultKind faultKind))
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission rejected by runtime-owned MTILE legality: {faultKind}.");
        }

        ValidateProjectionMatchesTypedRequest(request, projection);

        return new CompilerMatrixTileEmissionPlan(
            request,
            handoffRow,
            instruction,
            projection,
            materializedInstruction,
            UsesFallbackPath: false,
            UsesAliasPromotion: false,
            UsesScalarVectorDotOrBackendFallback: false);
    }

    private static void ValidateRequest(CompilerMatrixTileEmissionRequest request)
    {
        request.Descriptor.Validate(nameof(request.Descriptor));
        if (request.Opcode != CompilerMatrixTilePositiveEmissionAbiContract.GetOpcode(request.Kind))
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission request has inconsistent opcode identity.");
        }

        CompilerMatrixTilePositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(request.Mnemonic);
        ValidateCarrierShape(request.Descriptor.CanonicalDescriptor);

        switch (request.Kind)
        {
            case CompilerMatrixTilePositiveEmissionKind.MtileLoad:
                ValidateMemoryRequest(request, MatrixTileMemoryOperationKind.Load);
                break;
            case CompilerMatrixTilePositiveEmissionKind.MtileStore:
                ValidateMemoryRequest(request, MatrixTileMemoryOperationKind.Store);
                break;
            case CompilerMatrixTilePositiveEmissionKind.MtileMacc:
                ValidateMaccRequest(request);
                break;
            case CompilerMatrixTilePositiveEmissionKind.Mtranspose:
                ValidateTransposeRequest(request);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "Unknown MTILE compiler helper kind.");
        }
    }

    private static VLIW_Instruction BuildInstruction(CompilerMatrixTileEmissionRequest request)
    {
        MatrixTileCanonicalDescriptorAbi descriptor = request.Descriptor.CanonicalDescriptor;
        ulong primary = request.Kind switch
        {
            CompilerMatrixTilePositiveEmissionKind.MtileLoad => request.MemoryFaultAbi!.Value.BaseAddress,
            CompilerMatrixTilePositiveEmissionKind.MtileStore => request.MemoryFaultAbi!.Value.BaseAddress,
            CompilerMatrixTilePositiveEmissionKind.MtileMacc => request.PrimaryTile.TileId,
            CompilerMatrixTilePositiveEmissionKind.Mtranspose => request.PrimaryTile.TileId,
            _ => throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "Unknown MTILE compiler helper kind.")
        };
        ulong secondary = request.Kind switch
        {
            CompilerMatrixTilePositiveEmissionKind.MtileLoad => request.DestinationTile.TileId,
            CompilerMatrixTilePositiveEmissionKind.MtileStore => request.PrimaryTile.TileId,
            CompilerMatrixTilePositiveEmissionKind.MtileMacc =>
                ((ulong)request.DestinationTile.TileId << 16) | request.SecondaryTile.TileId,
            CompilerMatrixTilePositiveEmissionKind.Mtranspose => request.DestinationTile.TileId,
            _ => throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "Unknown MTILE compiler helper kind.")
        };

        return new VLIW_Instruction
        {
            OpCode = (uint)request.Opcode,
            DataTypeValue = request.Descriptor.ElementType,
            PredicateMask = 0,
            Immediate = descriptor.Columns,
            DestSrc1Pointer = primary,
            Src2Pointer = secondary,
            StreamLength = checked((uint)descriptor.Rows * descriptor.Columns),
            Stride = descriptor.ElementSizeBytes,
            RowStride = checked((ushort)descriptor.StrideBytes),
            Is2D = true,
            Indexed = false,
            Reduction = false,
            TailAgnostic = false,
            MaskAgnostic = false,
            VirtualThreadId = 0
        };
    }

    private static CompilerMatrixTileEmissionRequest RecoverRequest(
        InstructionsEnum opcode,
        in VLIW_Instruction instruction,
        MatrixTileInstructionIrProjection projection)
    {
        CompilerMatrixTileDescriptorAbi descriptor =
            new(projection.TileDescriptor, instruction.DataTypeValue);

        return opcode switch
        {
            InstructionsEnum.MTILE_LOAD => CompilerMatrixTileEmissionRequest.MtileLoad(
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.Src2Pointer)),
                descriptor,
                CompilerMatrixTileMemoryFaultAbiInputs.Create(instruction.DestSrc1Pointer)),
            InstructionsEnum.MTILE_STORE => CompilerMatrixTileEmissionRequest.MtileStore(
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.Src2Pointer)),
                descriptor,
                CompilerMatrixTileMemoryFaultAbiInputs.Create(instruction.DestSrc1Pointer)),
            InstructionsEnum.MTILE_MACC => CompilerMatrixTileEmissionRequest.MtileMacc(
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.DestSrc1Pointer)),
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.Src2Pointer)),
                CompilerMatrixTileTileOperand.Create(GetHighTileIdOrDefault(
                    instruction.Src2Pointer,
                    GetTileId(instruction.Src2Pointer))),
                descriptor,
                new CompilerMatrixTileAccumulatorPolicyAbi(
                    projection.SecondaryTileDescriptor,
                    projection.ResultTileDescriptor,
                    projection.MaccContract?.ElementKind ?? MatrixTileNumericElementKind.Unspecified,
                    projection.MaccContract?.AccumulatorPolicy ?? MatrixTileAccumulatorPolicyKind.Unspecified)
                {
                    MatrixTileNumericPolicy = projection.MaccContract?.NumericPolicy,
                    MatrixTileLayoutPolicy = projection.MaccContract?.LayoutPolicy
                }),
            InstructionsEnum.MTRANSPOSE => CompilerMatrixTileEmissionRequest.Mtranspose(
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.DestSrc1Pointer)),
                CompilerMatrixTileTileOperand.Create(GetTileId(instruction.Src2Pointer)),
                descriptor,
                new CompilerMatrixTileTransposePolicyAbi(
                    projection.ResultTileDescriptor,
                    projection.TransposeContract?.AliasPolicy ?? MatrixTileTransposeAliasPolicyKind.Unspecified)
                {
                    MatrixTileLayoutPolicy = projection.TransposeContract?.LayoutPolicy
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unknown MTILE compiler opcode.")
        };
    }

    private static void ValidateMemoryRequest(
        CompilerMatrixTileEmissionRequest request,
        MatrixTileMemoryOperationKind operation)
    {
        if (request.MemoryFaultAbi is not { } memory)
        {
            throw new ArgumentException(
                $"{request.Mnemonic} compiler helper requires tile memory/fault ABI inputs.",
                nameof(request));
        }

        if (memory.PageSizeBytes != MatrixTileMemoryShapeAndFaultAbi.DefaultPageSizeBytes)
        {
            throw new ArgumentException(
                $"{request.Mnemonic} compiler carrier uses the runtime default page-size fault ABI; alternate page sizes cannot be encoded by this helper.",
                nameof(request));
        }

        MatrixTileMemoryShapeContract contract = operation == MatrixTileMemoryOperationKind.Load
            ? MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                request.Descriptor.CanonicalDescriptor,
                memory.BaseAddress,
                memory.PageSizeBytes)
            : MatrixTileMemoryShapeAndFaultAbi.CreateStoreContract(
                request.Descriptor.CanonicalDescriptor,
                memory.BaseAddress,
                memory.PageSizeBytes);
        MatrixTileMemoryShapeValidationResult validation =
            MatrixTileMemoryShapeAndFaultAbi.Validate(contract);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"{request.Mnemonic} compiler helper rejected malformed tile memory/fault ABI before emission: {validation.FaultKind}.",
                nameof(request));
        }
    }

    private static void ValidateMaccRequest(CompilerMatrixTileEmissionRequest request)
    {
        if (request.AccumulatorPolicyAbi is not { } accumulatorPolicy)
        {
            throw new ArgumentException(
                "MTILE_MACC compiler helper requires accumulator policy ABI inputs.",
                nameof(request));
        }

        if (accumulatorPolicy.MatrixTileNumericPolicy is not { } numericPolicy)
        {
            throw new ArgumentException(
                "MTILE_MACC compiler helper requires explicit runtime-owned MatrixTileNumericPolicy sideband.",
                nameof(request));
        }

        if (accumulatorPolicy.MatrixTileLayoutPolicy is not { } layoutPolicy)
        {
            throw new ArgumentException(
                "MTILE_MACC compiler helper requires explicit runtime-owned MatrixTileLayoutPolicy sideband.",
                nameof(request));
        }

        MatrixTileNumericPolicyValidationResult numericValidation =
            MatrixTileNumericPolicyAbi.Validate(numericPolicy);
        if (!numericValidation.IsValid ||
            numericPolicy.ElementType != request.Descriptor.ElementType)
        {
            throw new ArgumentException(
                $"MTILE_MACC compiler helper rejected numeric policy sideband before emission: {numericValidation.FaultKind}.",
                nameof(request));
        }

        MatrixTileLayoutPolicyValidationResult layoutValidation =
            MatrixTileLayoutPolicyAbi.Validate(
                layoutPolicy,
                MatrixTileProjectedOperationKind.Macc);
        if (!layoutValidation.IsValid)
        {
            throw new ArgumentException(
                $"MTILE_MACC compiler helper rejected layout policy sideband before emission: {layoutValidation.FaultKind}.",
                nameof(request));
        }

        MatrixTileMaccSemanticContract contract =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                request.Descriptor.CanonicalDescriptor,
                accumulatorPolicy.RightSourceDescriptor,
                accumulatorPolicy.AccumulatorDescriptor,
                numericPolicy,
                layoutPolicy);

        if (contract.ElementKind != accumulatorPolicy.ElementKind ||
            contract.AccumulatorPolicy != accumulatorPolicy.AccumulatorPolicy)
        {
            throw new ArgumentException(
                "MTILE_MACC compiler helper requires accumulator ABI to match the runtime-owned numeric policy.",
                nameof(request));
        }

        MatrixTileSemanticValidationResult validation =
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateRuntimeMacc(contract);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"MTILE_MACC compiler helper rejected malformed accumulator ABI before emission: {validation.FaultKind}.",
                nameof(request));
        }
    }

    private static void ValidateTransposeRequest(CompilerMatrixTileEmissionRequest request)
    {
        if (request.TransposePolicyAbi is not { } transposePolicy)
        {
            throw new ArgumentException(
                "MTRANSPOSE compiler helper requires transpose policy ABI inputs.",
                nameof(request));
        }

        if (transposePolicy.MatrixTileLayoutPolicy is not { } layoutPolicy)
        {
            throw new ArgumentException(
                "MTRANSPOSE compiler helper requires explicit runtime-owned MatrixTileLayoutPolicy sideband.",
                nameof(request));
        }

        MatrixTileLayoutPolicyValidationResult layoutValidation =
            MatrixTileLayoutPolicyAbi.Validate(
                layoutPolicy,
                MatrixTileProjectedOperationKind.Transpose);
        if (!layoutValidation.IsValid)
        {
            throw new ArgumentException(
                $"MTRANSPOSE compiler helper rejected layout policy sideband before emission: {layoutValidation.FaultKind}.",
                nameof(request));
        }

        MatrixTileTransposeSemanticContract contract =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                request.Descriptor.CanonicalDescriptor,
                transposePolicy.DestinationDescriptor,
                request.PrimaryTile.TileId,
                request.DestinationTile.TileId,
                layoutPolicy);

        if (contract.AliasPolicy != transposePolicy.AliasPolicy)
        {
            throw new ArgumentException(
                "MTRANSPOSE compiler helper requires the runtime alias policy ABI.",
                nameof(request));
        }

        MatrixTileSemanticValidationResult validation =
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(contract);
        if (!validation.IsValid)
        {
            throw new ArgumentException(
                $"MTRANSPOSE compiler helper rejected malformed transpose policy ABI before emission: {validation.FaultKind}.",
                nameof(request));
        }
    }

    private static void ValidateProjectionMatchesTypedRequest(
        CompilerMatrixTileEmissionRequest request,
        MatrixTileInstructionIrProjection projection)
    {
        if (projection.Opcode != request.Opcode ||
            projection.TileDescriptor != request.Descriptor.CanonicalDescriptor ||
            projection.UsesFallbackPath ||
            projection.FaultKind != MatrixTileIrProjectionFaultKind.None)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler helper projection did not round-trip through runtime-owned MTILE authority.");
        }

        switch (request.Kind)
        {
            case CompilerMatrixTilePositiveEmissionKind.MtileLoad:
            case CompilerMatrixTilePositiveEmissionKind.MtileStore:
                if (projection.MemoryContract is null ||
                    projection.MemoryValidation is not { IsValid: true })
                {
                    throw new InvalidOperationException(
                        $"{request.Mnemonic} compiler helper lost tile memory/fault ABI projection.");
                }
                break;
            case CompilerMatrixTilePositiveEmissionKind.MtileMacc:
                if (request.AccumulatorPolicyAbi is not { } accumulatorPolicy ||
                    accumulatorPolicy.MatrixTileNumericPolicy is not { } numericPolicy ||
                    accumulatorPolicy.MatrixTileLayoutPolicy is not { } layoutPolicy ||
                    projection.MaccContract is not { } maccContract ||
                    maccContract.Right != accumulatorPolicy.RightSourceDescriptor ||
                    maccContract.Accumulator != accumulatorPolicy.AccumulatorDescriptor ||
                    maccContract.ElementKind != accumulatorPolicy.ElementKind ||
                    maccContract.AccumulatorPolicy != accumulatorPolicy.AccumulatorPolicy ||
                    maccContract.NumericPolicy != numericPolicy ||
                    maccContract.LayoutPolicy != layoutPolicy ||
                    !maccContract.HasExplicitNumericPolicy ||
                    !maccContract.HasExplicitLayoutPolicy)
                {
                    throw new InvalidOperationException(
                        "MTILE_MACC compiler helper lost accumulator policy ABI projection.");
                }
                break;
            case CompilerMatrixTilePositiveEmissionKind.Mtranspose:
                if (request.TransposePolicyAbi is not { } transposePolicy ||
                    transposePolicy.MatrixTileLayoutPolicy is not { } transposeLayoutPolicy ||
                    projection.TransposeContract is not { } transposeContract ||
                    transposeContract.Destination != transposePolicy.DestinationDescriptor ||
                    transposeContract.AliasPolicy != transposePolicy.AliasPolicy ||
                    transposeContract.LayoutPolicy != transposeLayoutPolicy ||
                    !transposeContract.HasExplicitLayoutPolicy)
                {
                    throw new InvalidOperationException(
                        "MTRANSPOSE compiler helper lost transpose policy ABI projection.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "Unknown MTILE compiler helper kind.");
        }
    }

    private static void ValidateCarrierShape(MatrixTileCanonicalDescriptorAbi descriptor)
    {
        if (descriptor.Rows == 0 || descriptor.Columns == 0)
        {
            throw new ArgumentException("MTILE compiler emission requires a non-empty tile shape.");
        }

        _ = checked((uint)descriptor.Rows * descriptor.Columns);
        if (descriptor.StrideBytes > 0x1FFFu)
        {
            throw new ArgumentOutOfRangeException(
                nameof(descriptor),
                descriptor.StrideBytes,
                "MTILE compiler carrier RowStride is 13 bits; unsupported descriptor stride fails closed before emission.");
        }
    }

    private static VectorInstructionPayload CreatePayload(
        in VLIW_Instruction instruction,
        MatrixTileNumericPolicy? matrixTileNumericPolicy = null,
        MatrixTileLayoutPolicy? matrixTileLayoutPolicy = null)
    {
        return new VectorInstructionPayload(
            instruction.DestSrc1Pointer,
            instruction.Src2Pointer,
            instruction.StreamLength,
            instruction.Stride,
            instruction.RowStride,
            instruction.Indexed,
            instruction.Is2D,
            instruction.TailAgnostic,
            instruction.MaskAgnostic,
            instruction.Saturating,
            instruction.PredicateMask,
            instruction.DataType)
        {
            MatrixTileNumericPolicy = matrixTileNumericPolicy,
            MatrixTileLayoutPolicy = matrixTileLayoutPolicy
        };
    }

    private static ushort GetTileId(ulong pointerOrTileToken) =>
        (ushort)(pointerOrTileToken & ushort.MaxValue);

    private static ushort GetHighTileIdOrDefault(
        ulong pointerOrTileToken,
        ushort fallbackTileId)
    {
        ushort highTileId = (ushort)((pointerOrTileToken >> 16) & ushort.MaxValue);
        return highTileId == 0
            ? fallbackTileId
            : highTileId;
    }
}
