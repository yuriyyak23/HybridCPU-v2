using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileProjectedOperationKind : byte
{
    Unspecified = 0,
    Load = 1,
    Store = 2,
    Macc = 3,
    Transpose = 4,
}

public enum MatrixTileIrProjectionFaultKind : byte
{
    None = 0,
    NonMatrixTileOpcode = 1,
    MissingVectorPayload = 2,
    UnsupportedDataType = 3,
    InvalidShapeEncoding = 4,
    MemoryShapeFault = 5,
    MaccSemanticFault = 6,
    TransposeSemanticFault = 7,
    RuntimeOwnedVlmFault = 8,
    NumericPolicyFault = 9,
    LayoutPolicyFault = 10,
}

public readonly record struct MatrixTileInstructionIrProjection(
    InstructionsEnum Opcode,
    string Mnemonic,
    MatrixTileProjectedOperationKind OperationKind,
    VectorInstructionPayload SourcePayload,
    MatrixTileCanonicalDescriptorAbi TileDescriptor,
    MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor,
    MatrixTileCanonicalDescriptorAbi ResultTileDescriptor,
    MatrixTileMemoryShapeContract? MemoryContract,
    MatrixTileMemoryShapeValidationResult? MemoryValidation,
    MatrixTileMaccSemanticContract? MaccContract,
    MatrixTileTransposeSemanticContract? TransposeContract,
    MatrixTileSemanticValidationResult? SemanticValidation,
    MatrixTileVlmLegalityValidationResult VlmValidation,
    MatrixTileIrProjectionFaultKind FaultKind,
    string DescriptorValidationDecision,
    bool HasTileDescriptorProjection,
    bool HasMemoryOperandProjection,
    bool HasAccumulatorOperandProjection,
    bool HasTransposePolicyProjection,
    bool PreservesDescriptorValidationResults,
    bool UsesFallbackPath,
    bool OpensExecution)
{
    public MatrixTileNumericPolicyValidationResult? NumericPolicyValidation { get; init; }

    public MatrixTileLayoutPolicyValidationResult? LayoutPolicyValidation { get; init; }

    public bool HasExplicitNumericPolicyProjection =>
        OperationKind != MatrixTileProjectedOperationKind.Macc ||
        MaccContract is { HasExplicitNumericPolicy: true };

    public bool HasExplicitLayoutPolicyProjection =>
        OperationKind switch
        {
            MatrixTileProjectedOperationKind.Macc =>
                MaccContract is { HasExplicitLayoutPolicy: true },
            MatrixTileProjectedOperationKind.Transpose =>
                TransposeContract is { HasExplicitLayoutPolicy: true },
            _ => true,
        };

    public bool IsRuntimeLegal =>
        FaultKind == MatrixTileIrProjectionFaultKind.None &&
        VlmValidation.IsLegal &&
        !UsesFallbackPath &&
        !OpensExecution;
}

public readonly record struct MatrixTileMaterializedInstruction(
    InstructionsEnum Opcode,
    string Mnemonic,
    MatrixTileProjectedOperationKind OperationKind,
    MatrixTileInstructionIrProjection Projection,
    MatrixTileCanonicalDescriptorAbi TileDescriptor,
    MatrixTileCanonicalDescriptorAbi SecondaryTileDescriptor,
    MatrixTileCanonicalDescriptorAbi ResultTileDescriptor,
    bool IsTypedCloseToRtlRuntimeObject,
    bool PublishesTypedTileMicroOp,
    bool OpensExecution,
    bool UsesFallbackPath)
{
    public bool IsRuntimeLegal =>
        Projection.IsRuntimeLegal &&
        IsTypedCloseToRtlRuntimeObject &&
        !PublishesTypedTileMicroOp &&
        !OpensExecution &&
        !UsesFallbackPath;
}

public static class MatrixTileIrProjectionAndMaterializer
{
    public const string IrProjectionMaterializerDecision = "ClosedMatrixTileIrProjectionAndMaterializer";
    public const string TileDescriptorProjectionDecision = "ClosedInstructionIrTileDescriptorProjection";
    public const string MemoryOperandProjectionDecision = "ClosedMatrixTileMemoryOperandProjection";
    public const string AccumulatorProjectionDecision = "ClosedMatrixTileAccumulatorOperandProjection";
    public const string TransposeProjectionDecision = "ClosedMatrixTileTransposePolicyProjection";
    public const string RegistryEntryDecision = "ClosedMatrixTileRegistryEntries";
    public const string MaterializerFactoryDecision = "ClosedMatrixTileMaterializerFactories";
    public const string TypedObjectDecision = "ClosedMaterializedTypedCloseToRtlInstructionObjects";
    public const string InvalidIrDecision = "ClosedInvalidTileIrProjectionFaultAbi";
    public const string CompilerIrBoundaryDecision = "CompilerIrIsNotRuntimeProjectionEvidence";
    public const string FallbackDecision = "NoScalarVectorDotDscLane7VmxOrBackendFallbackAuthority";

    public const bool HasInstructionIrTileProjection = true;
    public const bool HasTileDescriptorIrCarrier = true;
    public const bool HasMemoryOperandProjection = true;
    public const bool HasAccumulatorOperandProjection = true;
    public const bool HasTransposePolicyProjection = true;
    public const bool HasRegistryEntries = true;
    public const bool HasMaterializerFactories = true;
    public const bool HasMaterializedTypedRuntimeObjects = true;
    public const bool HasDescriptorValidationResultPreservation = true;
    public const bool HasInvalidIrProjectionFaults = true;
    public const bool KeepsCompilerIrNonAuthority = true;
    public const bool KeepsCompilerScopeClosed = true;
    public const bool KeepsCompilerHandoffBlocked = true;
    public const bool PublishesTypedTileMicroOp = false;
    public const bool OpensExecution = false;
    public const bool UsesFallbackPath = false;

    public static bool TryProject(
        InstructionIR instruction,
        out MatrixTileInstructionIrProjection projection,
        out MatrixTileIrProjectionFaultKind faultKind)
    {
        InstructionsEnum opcode = instruction.CanonicalOpcode.ToInstructionsEnum();
        if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode))
        {
            projection = default;
            faultKind = MatrixTileIrProjectionFaultKind.NonMatrixTileOpcode;
            return false;
        }

        if (!instruction.VectorPayload.HasValue)
        {
            projection = default;
            faultKind = MatrixTileIrProjectionFaultKind.MissingVectorPayload;
            return false;
        }

        projection = ProjectDecodedVectorPayload(
            opcode,
            instruction.VectorPayload.Value,
            instruction.Imm,
            requireExplicitNumericPolicy: true);
        faultKind = projection.FaultKind;
        return true;
    }

    public static MatrixTileInstructionIrProjection ProjectDecodedVectorPayload(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        long immediate)
    {
        return ProjectDecodedVectorPayload(
            opcode,
            payload,
            immediate,
            requireExplicitNumericPolicy: false);
    }

    public static MatrixTileInstructionIrProjection ProjectDecodedVectorPayload(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        long immediate,
        bool requireExplicitNumericPolicy)
    {
        if (!MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode))
        {
            return CreateFaultProjection(
                opcode,
                payload,
                MatrixTileProjectedOperationKind.Unspecified,
                MatrixTileIrProjectionFaultKind.NonMatrixTileOpcode);
        }

        MatrixTileProjectedOperationKind operationKind = GetOperationKind(opcode);
        if (!TryCreatePrimaryDescriptor(payload, immediate, out MatrixTileCanonicalDescriptorAbi descriptor))
        {
            return CreateFaultProjection(
                opcode,
                payload,
                operationKind,
                MatrixTileIrProjectionFaultKind.InvalidShapeEncoding);
        }

        if (!TryGetDataType(payload.DataType, out DataTypeEnum dataType, out MatrixTileNumericElementKind elementKind))
        {
            return CreateFaultProjection(
                opcode,
                payload,
                operationKind,
                MatrixTileIrProjectionFaultKind.UnsupportedDataType,
                descriptor);
        }

        return opcode switch
        {
            InstructionsEnum.MTILE_LOAD => ProjectLoad(opcode, payload, descriptor),
            InstructionsEnum.MTILE_STORE => ProjectStore(opcode, payload, descriptor),
            InstructionsEnum.MTILE_MACC => ProjectMacc(
                opcode,
                payload,
                descriptor,
                dataType,
                elementKind,
                requireExplicitNumericPolicy),
            InstructionsEnum.MTRANSPOSE => ProjectTranspose(
                opcode,
                payload,
                descriptor,
                requireExplicitNumericPolicy),
            _ => CreateFaultProjection(
                opcode,
                payload,
                operationKind,
                MatrixTileIrProjectionFaultKind.NonMatrixTileOpcode,
                descriptor),
        };
    }

    public static bool TryMaterialize(
        InstructionIR instruction,
        out MatrixTileMaterializedInstruction materializedInstruction,
        out MatrixTileIrProjectionFaultKind faultKind)
    {
        materializedInstruction = default;
        if (!TryProject(instruction, out MatrixTileInstructionIrProjection projection, out faultKind))
        {
            return false;
        }

        return TryMaterialize(projection, out materializedInstruction, out faultKind);
    }

    public static bool TryMaterialize(
        MatrixTileInstructionIrProjection projection,
        out MatrixTileMaterializedInstruction materializedInstruction,
        out MatrixTileIrProjectionFaultKind faultKind)
    {
        materializedInstruction = default;
        faultKind = projection.FaultKind;
        if (!projection.IsRuntimeLegal)
        {
            if (faultKind == MatrixTileIrProjectionFaultKind.None)
            {
                faultKind = MatrixTileIrProjectionFaultKind.RuntimeOwnedVlmFault;
            }

            return false;
        }

        materializedInstruction = new MatrixTileMaterializedInstruction(
            projection.Opcode,
            projection.Mnemonic,
            projection.OperationKind,
            projection,
            projection.TileDescriptor,
            projection.SecondaryTileDescriptor,
            projection.ResultTileDescriptor,
            IsTypedCloseToRtlRuntimeObject: true,
            PublishesTypedTileMicroOp: false,
            OpensExecution: false,
            UsesFallbackPath: false);
        faultKind = MatrixTileIrProjectionFaultKind.None;
        return true;
    }

    private static MatrixTileInstructionIrProjection ProjectLoad(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileCanonicalDescriptorAbi descriptor)
    {
        MatrixTileMemoryShapeContract contract =
            MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(descriptor, payload.PrimaryPointer);
        MatrixTileMemoryShapeValidationResult memoryValidation =
            MatrixTileMemoryShapeAndFaultAbi.Validate(contract);
        MatrixTileVlmLegalityValidationResult vlmValidation =
            MatrixTileRuntimeOwnedVlmRows.ValidateLoad(contract);

        return CreateProjection(
            opcode,
            payload,
            MatrixTileProjectedOperationKind.Load,
            descriptor,
            secondaryDescriptor: default,
            resultDescriptor: descriptor,
            memoryContract: contract,
            memoryValidation: memoryValidation,
            maccContract: null,
            transposeContract: null,
            semanticValidation: null,
            vlmValidation: vlmValidation,
            faultKind: memoryValidation.IsMemoryShapeAbiAccepted
                ? ToProjectionFault(vlmValidation)
                : MatrixTileIrProjectionFaultKind.MemoryShapeFault,
            hasMemoryProjection: true,
            hasAccumulatorProjection: false,
            hasTransposeProjection: false);
    }

    private static MatrixTileInstructionIrProjection ProjectStore(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileCanonicalDescriptorAbi descriptor)
    {
        MatrixTileMemoryShapeContract contract =
            MatrixTileMemoryShapeAndFaultAbi.CreateStoreContract(descriptor, payload.PrimaryPointer);
        MatrixTileMemoryShapeValidationResult memoryValidation =
            MatrixTileMemoryShapeAndFaultAbi.Validate(contract);
        MatrixTileVlmLegalityValidationResult vlmValidation =
            MatrixTileRuntimeOwnedVlmRows.ValidateStore(contract);

        return CreateProjection(
            opcode,
            payload,
            MatrixTileProjectedOperationKind.Store,
            descriptor,
            secondaryDescriptor: default,
            resultDescriptor: descriptor,
            memoryContract: contract,
            memoryValidation: memoryValidation,
            maccContract: null,
            transposeContract: null,
            semanticValidation: null,
            vlmValidation: vlmValidation,
            faultKind: memoryValidation.IsMemoryShapeAbiAccepted
                ? ToProjectionFault(vlmValidation)
                : MatrixTileIrProjectionFaultKind.MemoryShapeFault,
            hasMemoryProjection: true,
            hasAccumulatorProjection: false,
            hasTransposeProjection: false);
    }

    private static MatrixTileInstructionIrProjection ProjectMacc(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileCanonicalDescriptorAbi leftDescriptor,
        DataTypeEnum dataType,
        MatrixTileNumericElementKind elementKind,
        bool requireExplicitNumericPolicy)
    {
        MatrixTileLayoutPolicy? layoutPolicy = payload.MatrixTileLayoutPolicy;
        bool hasExplicitLayoutPolicy = layoutPolicy.HasValue;
        if (!layoutPolicy.HasValue && !requireExplicitNumericPolicy)
        {
            layoutPolicy = MatrixTileLayoutPolicyAbi.CreateMaccPolicy();
        }

        MatrixTileLayoutPolicyValidationResult layoutValidation =
            MatrixTileLayoutPolicyAbi.Validate(
                layoutPolicy,
                MatrixTileProjectedOperationKind.Macc);
        if (!layoutValidation.IsRuntimeOwnedLayoutPolicyAccepted)
        {
            return CreateFaultProjection(
                opcode,
                payload,
                MatrixTileProjectedOperationKind.Macc,
                MatrixTileIrProjectionFaultKind.LayoutPolicyFault,
                leftDescriptor) with
            {
                LayoutPolicyValidation = layoutValidation
            };
        }

        MatrixTileNumericPolicy? numericPolicy = payload.MatrixTileNumericPolicy;
        bool hasExplicitNumericPolicy = numericPolicy.HasValue;
        if (!numericPolicy.HasValue &&
            !requireExplicitNumericPolicy &&
            MatrixTileNumericPolicyAbi.TryCreateLegacyPhase14Policy(
                dataType,
                out MatrixTileNumericPolicy legacyPolicy))
        {
            numericPolicy = legacyPolicy;
        }

        MatrixTileNumericPolicyValidationResult numericValidation =
            MatrixTileNumericPolicyAbi.Validate(numericPolicy);
        if (numericValidation.IsRuntimeOwnedNumericPolicyAccepted &&
            numericPolicy!.Value.ElementType != dataType)
        {
            numericValidation = MatrixTileNumericPolicyValidationResult.Fault(
                MatrixTileNumericPolicyFaultKind.ContradictoryElementType,
                numericValidation.Profile);
        }

        if (!numericValidation.IsRuntimeOwnedNumericPolicyAccepted)
        {
            return CreateFaultProjection(
                opcode,
                payload,
                MatrixTileProjectedOperationKind.Macc,
                MatrixTileIrProjectionFaultKind.NumericPolicyFault,
                leftDescriptor) with
            {
                NumericPolicyValidation = numericValidation
            };
        }

        ushort accumulatorElementSize = checked((ushort)
            MatrixTileNumericPolicyAbi.GetElementSizeBytes(
                numericPolicy!.Value.AccumulatorType));
        MatrixTileCanonicalDescriptorAbi rightDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                leftDescriptor.Columns,
                leftDescriptor.Rows,
                leftDescriptor.ElementSizeBytes,
                GetCanonicalStrideBytes(leftDescriptor.Rows, leftDescriptor.ElementSizeBytes));
        MatrixTileCanonicalDescriptorAbi accumulatorDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                leftDescriptor.Rows,
                rightDescriptor.Columns,
                accumulatorElementSize,
                GetCanonicalStrideBytes(rightDescriptor.Columns, accumulatorElementSize));
        MatrixTileMaccSemanticContract contract = (hasExplicitNumericPolicy
            ? MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                leftDescriptor,
                rightDescriptor,
                accumulatorDescriptor,
                numericPolicy.Value)
            : MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                leftDescriptor,
                rightDescriptor,
                accumulatorDescriptor,
                elementKind)) with
        {
            LayoutPolicy = layoutPolicy.Value,
            HasExplicitLayoutPolicy = hasExplicitLayoutPolicy
        };
        MatrixTileSemanticValidationResult semanticValidation =
            requireExplicitNumericPolicy
                ? MatrixTileAccumulatorAndTransposePolicyAbi.ValidateRuntimeMacc(contract)
                : MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(contract);
        MatrixTileVlmLegalityValidationResult vlmValidation =
            MatrixTileRuntimeOwnedVlmRows.ValidateMacc(contract);

        return CreateProjection(
            opcode,
            payload,
            MatrixTileProjectedOperationKind.Macc,
            leftDescriptor,
            rightDescriptor,
            accumulatorDescriptor,
            memoryContract: null,
            memoryValidation: null,
            maccContract: contract,
            transposeContract: null,
            semanticValidation: semanticValidation,
            vlmValidation: vlmValidation,
            faultKind: semanticValidation.IsSemanticAbiAccepted
                ? ToProjectionFault(vlmValidation)
                : MatrixTileIrProjectionFaultKind.MaccSemanticFault,
            hasMemoryProjection: false,
            hasAccumulatorProjection: true,
            hasTransposeProjection: false) with
        {
            NumericPolicyValidation = numericValidation,
            LayoutPolicyValidation = layoutValidation
        };
    }

    private static MatrixTileInstructionIrProjection ProjectTranspose(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileCanonicalDescriptorAbi sourceDescriptor,
        bool requireExplicitLayoutPolicy)
    {
        MatrixTileLayoutPolicy? layoutPolicy = payload.MatrixTileLayoutPolicy;
        bool hasExplicitLayoutPolicy = layoutPolicy.HasValue;
        if (payload.MatrixTileNumericPolicy.HasValue)
        {
            return CreateFaultProjection(
                opcode,
                payload,
                MatrixTileProjectedOperationKind.Transpose,
                MatrixTileIrProjectionFaultKind.NumericPolicyFault,
                sourceDescriptor);
        }

        if (!layoutPolicy.HasValue && !requireExplicitLayoutPolicy)
        {
            layoutPolicy = MatrixTileLayoutPolicyAbi.CreateTransposePolicy();
        }

        MatrixTileLayoutPolicyValidationResult layoutValidation =
            MatrixTileLayoutPolicyAbi.Validate(
                layoutPolicy,
                MatrixTileProjectedOperationKind.Transpose);
        if (!layoutValidation.IsRuntimeOwnedLayoutPolicyAccepted)
        {
            return CreateFaultProjection(
                opcode,
                payload,
                MatrixTileProjectedOperationKind.Transpose,
                MatrixTileIrProjectionFaultKind.LayoutPolicyFault,
                sourceDescriptor) with
            {
                LayoutPolicyValidation = layoutValidation
            };
        }

        MatrixTileCanonicalDescriptorAbi destinationDescriptor =
            MatrixTileCanonicalDescriptorAbi.Create(
                sourceDescriptor.Columns,
                sourceDescriptor.Rows,
                sourceDescriptor.ElementSizeBytes,
                GetCanonicalStrideBytes(sourceDescriptor.Rows, sourceDescriptor.ElementSizeBytes));
        MatrixTileTransposeSemanticContract contract =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                sourceDescriptor,
                destinationDescriptor,
                GetTileId(payload.PrimaryPointer),
                GetTileId(payload.SecondaryPointer)) with
            {
                LayoutPolicy = layoutPolicy.Value,
                HasExplicitLayoutPolicy = hasExplicitLayoutPolicy
            };
        MatrixTileSemanticValidationResult semanticValidation =
            requireExplicitLayoutPolicy
                ? MatrixTileAccumulatorAndTransposePolicyAbi.ValidateRuntimeTranspose(contract)
                : MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(contract);
        MatrixTileVlmLegalityValidationResult vlmValidation =
            MatrixTileRuntimeOwnedVlmRows.ValidateTranspose(contract);

        return CreateProjection(
            opcode,
            payload,
            MatrixTileProjectedOperationKind.Transpose,
            sourceDescriptor,
            secondaryDescriptor: default,
            resultDescriptor: destinationDescriptor,
            memoryContract: null,
            memoryValidation: null,
            maccContract: null,
            transposeContract: contract,
            semanticValidation: semanticValidation,
            vlmValidation: vlmValidation,
            faultKind: semanticValidation.IsSemanticAbiAccepted
                ? ToProjectionFault(vlmValidation)
                : MatrixTileIrProjectionFaultKind.TransposeSemanticFault,
            hasMemoryProjection: false,
            hasAccumulatorProjection: false,
            hasTransposeProjection: true) with
        {
            LayoutPolicyValidation = layoutValidation
        };
    }

    private static MatrixTileInstructionIrProjection CreateProjection(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileProjectedOperationKind operationKind,
        MatrixTileCanonicalDescriptorAbi descriptor,
        MatrixTileCanonicalDescriptorAbi secondaryDescriptor,
        MatrixTileCanonicalDescriptorAbi resultDescriptor,
        MatrixTileMemoryShapeContract? memoryContract,
        MatrixTileMemoryShapeValidationResult? memoryValidation,
        MatrixTileMaccSemanticContract? maccContract,
        MatrixTileTransposeSemanticContract? transposeContract,
        MatrixTileSemanticValidationResult? semanticValidation,
        MatrixTileVlmLegalityValidationResult vlmValidation,
        MatrixTileIrProjectionFaultKind faultKind,
        bool hasMemoryProjection,
        bool hasAccumulatorProjection,
        bool hasTransposeProjection)
    {
        return new MatrixTileInstructionIrProjection(
            opcode,
            GetMnemonic(opcode),
            operationKind,
            payload,
            descriptor,
            secondaryDescriptor,
            resultDescriptor,
            memoryContract,
            memoryValidation,
            maccContract,
            transposeContract,
            semanticValidation,
            vlmValidation,
            faultKind,
            MatrixTileArchitecturalTileStateAndDescriptorAbi.ValidateDescriptor(descriptor),
            HasTileDescriptorProjection: descriptor.IsCanonical,
            HasMemoryOperandProjection: hasMemoryProjection,
            HasAccumulatorOperandProjection: hasAccumulatorProjection,
            HasTransposePolicyProjection: hasTransposeProjection,
            PreservesDescriptorValidationResults: true,
            UsesFallbackPath: false,
            OpensExecution: false);
    }

    private static MatrixTileInstructionIrProjection CreateFaultProjection(
        InstructionsEnum opcode,
        VectorInstructionPayload payload,
        MatrixTileProjectedOperationKind operationKind,
        MatrixTileIrProjectionFaultKind faultKind,
        MatrixTileCanonicalDescriptorAbi descriptor = default)
    {
        MatrixTileVlmLegalityValidationResult vlmValidation =
            MatrixTileVlmLegalityValidationResult.Fault(
                MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode)
                    ? opcode
                    : InstructionsEnum.MTILE_LOAD,
                faultKind == MatrixTileIrProjectionFaultKind.NonMatrixTileOpcode
                    ? MatrixTileVlmLegalityFaultKind.NonMatrixTileOpcode
                    : MatrixTileVlmLegalityFaultKind.NonDescriptorBackedContour);

        return new MatrixTileInstructionIrProjection(
            opcode,
            GetMnemonic(opcode),
            operationKind,
            payload,
            descriptor,
            SecondaryTileDescriptor: default,
            ResultTileDescriptor: default,
            MemoryContract: null,
            MemoryValidation: null,
            MaccContract: null,
            TransposeContract: null,
            SemanticValidation: null,
            vlmValidation,
            faultKind,
            descriptor.Equals(default(MatrixTileCanonicalDescriptorAbi))
                ? "NoMatrixTileDescriptorProjected"
                : MatrixTileArchitecturalTileStateAndDescriptorAbi.ValidateDescriptor(descriptor),
            HasTileDescriptorProjection: descriptor.IsCanonical,
            HasMemoryOperandProjection: operationKind is MatrixTileProjectedOperationKind.Load or MatrixTileProjectedOperationKind.Store,
            HasAccumulatorOperandProjection: operationKind == MatrixTileProjectedOperationKind.Macc,
            HasTransposePolicyProjection: operationKind == MatrixTileProjectedOperationKind.Transpose,
            PreservesDescriptorValidationResults: true,
            UsesFallbackPath: false,
            OpensExecution: false);
    }

    private static bool TryCreatePrimaryDescriptor(
        VectorInstructionPayload payload,
        long immediate,
        out MatrixTileCanonicalDescriptorAbi descriptor)
    {
        descriptor = default;
        if (!TryGetDataType(payload.DataType, out DataTypeEnum dataType, out _))
        {
            ushort rows = 1;
            ushort columns = payload.StreamLength <= ushort.MaxValue
                ? (ushort)payload.StreamLength
                : (ushort)0;
            descriptor = MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                columns,
                elementSizeBytes: 0,
                strideBytes: payload.Stride);
            return true;
        }

        ushort elementSize = checked((ushort)DataTypeUtils.SizeOf(dataType));
        if (!TryGetShape(payload, immediate, elementSize, out ushort rowsValue, out ushort columnsValue, out uint strideBytes))
        {
            return false;
        }

        descriptor = MatrixTileCanonicalDescriptorAbi.Create(
            rowsValue,
            columnsValue,
            elementSize,
            strideBytes);
        return true;
    }

    private static bool TryGetShape(
        VectorInstructionPayload payload,
        long immediate,
        ushort elementSizeBytes,
        out ushort rows,
        out ushort columns,
        out uint strideBytes)
    {
        rows = 0;
        columns = 0;
        strideBytes = 0;

        if (payload.StreamLength == 0 || payload.StreamLength > ushort.MaxValue)
        {
            return false;
        }

        if (payload.Is2D)
        {
            if (immediate <= 0 ||
                immediate > ushort.MaxValue ||
                payload.StreamLength % (uint)immediate != 0)
            {
                return false;
            }

            uint rowCount = payload.StreamLength / (uint)immediate;
            if (rowCount == 0 || rowCount > ushort.MaxValue)
            {
                return false;
            }

            rows = (ushort)rowCount;
            columns = (ushort)immediate;
            strideBytes = payload.RowStride != 0
                ? payload.RowStride
                : GetCanonicalStrideBytes(columns, elementSizeBytes);
            return true;
        }

        rows = 1;
        columns = (ushort)payload.StreamLength;
        strideBytes = payload.Stride != 0
            ? payload.Stride
            : GetCanonicalStrideBytes(columns, elementSizeBytes);
        return true;
    }

    private static bool TryGetDataType(
        byte rawDataType,
        out DataTypeEnum dataType,
        out MatrixTileNumericElementKind elementKind)
    {
        dataType = (DataTypeEnum)rawDataType;
        elementKind = MatrixTileNumericElementKind.Unspecified;
        if (!IsKnownProjectionDataType(dataType))
        {
            return false;
        }

        if (DataTypeUtils.IsSignedInteger(dataType))
        {
            elementKind = MatrixTileNumericElementKind.SignedInteger;
        }
        else if (DataTypeUtils.IsUnsignedInteger(dataType))
        {
            elementKind = MatrixTileNumericElementKind.UnsignedInteger;
        }
        else if (DataTypeUtils.IsFloatingPoint(dataType))
        {
            elementKind = MatrixTileNumericElementKind.FloatingPoint;
        }

        return true;
    }

    private static bool IsKnownProjectionDataType(DataTypeEnum dataType)
    {
        try
        {
            _ = DataTypeUtils.SizeOf(dataType);
            return true;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static MatrixTileProjectedOperationKind GetOperationKind(InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.MTILE_LOAD => MatrixTileProjectedOperationKind.Load,
            InstructionsEnum.MTILE_STORE => MatrixTileProjectedOperationKind.Store,
            InstructionsEnum.MTILE_MACC => MatrixTileProjectedOperationKind.Macc,
            InstructionsEnum.MTRANSPOSE => MatrixTileProjectedOperationKind.Transpose,
            _ => MatrixTileProjectedOperationKind.Unspecified,
        };
    }

    private static MatrixTileIrProjectionFaultKind ToProjectionFault(
        MatrixTileVlmLegalityValidationResult validation)
    {
        return validation.IsLegal
            ? MatrixTileIrProjectionFaultKind.None
            : MatrixTileIrProjectionFaultKind.RuntimeOwnedVlmFault;
    }

    private static uint GetCanonicalStrideBytes(ushort columns, ushort elementSizeBytes) =>
        (uint)columns * elementSizeBytes;

    private static ushort GetTileId(ulong pointerOrTileToken) =>
        (ushort)(pointerOrTileToken & ushort.MaxValue);

    private static string GetMnemonic(InstructionsEnum opcode) =>
        OpcodeRegistry.TryGetMnemonic((uint)opcode, out string mnemonic)
            ? mnemonic
            : opcode.ToString();
}
