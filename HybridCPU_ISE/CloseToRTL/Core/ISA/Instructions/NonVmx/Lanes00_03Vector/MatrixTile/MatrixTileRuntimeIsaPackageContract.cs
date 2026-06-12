namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public readonly record struct MatrixTileRuntimeIsaPackageRow(
    string Mnemonic,
    bool IsTileMemory,
    bool IsLoad,
    bool IsStore,
    bool IsMacc,
    bool IsTranspose,
    bool RequiresTileMemoryShapeFaultModel,
    bool RequiresAccumulatorTileAbi,
    bool RequiresTransposeTilePolicyAbi,
    bool RequiresRetireStagedPublication,
    bool RequiresRetireStagedCommit,
    string PrimaryBlockedGate);

public readonly record struct MatrixTileRuntimeIsaClosureGateRow(
    string GateName,
    string EvidenceCode,
    bool IsExecutableClosure,
    bool BlocksCompilerUpdate,
    string Blocker);

public readonly record struct MatrixTileRemainingRuntimeIsaTaskRow(
    string TaskName,
    string Status,
    bool BlocksExecutableClosure,
    bool BlocksCompilerUpdate,
    string RequiredDecision);

public readonly record struct MatrixTileOpcodeAuthorityRow(
    string Mnemonic,
    ushort NumericOpcode,
    bool HasPackageOpcodeIdentityAuthority,
    string StatusCatalogAuthority,
    bool HasExecutableStatusCatalogPromotion,
    bool HasExecutableOpcodeAuthority,
    bool HasDescriptorAuthority,
    bool RequiresFutureAdr);

public readonly record struct MatrixTilePhase01AuthorityDecisionRow(
    string Mnemonic,
    ushort NumericOpcode,
    string RetainedNumericOpcodeRole,
    string DescriptorAuthorityRole,
    string PackageFeatureRole,
    string MemoryOrTransposePolicyDecision,
    string StatusCatalogPromotionRule,
    bool HasProductionAuthoritySource,
    bool BlocksPhase02);

public readonly record struct MatrixTilePhase02StateDescriptorDecisionRow(
    string OpenPoolItem,
    string RequiredAbiSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasRuntimeOwnedAbi,
    bool HasCanonicalCarrier,
    bool BlocksPhase03);

public readonly record struct MatrixTilePhase02InstructionDescriptorRequirementRow(
    string Mnemonic,
    string TileStateRequirement,
    string DescriptorRequirement,
    string DescriptorValidationDecision,
    bool HasDescriptorRoundTripAbi,
    bool HasReservedDescriptorFailFastTests,
    bool KeepsExternalDescriptorsNonAuthority);

public readonly record struct MatrixTilePhase03MemoryFaultDecisionRow(
    string Mnemonic,
    bool IsLoad,
    bool IsStore,
    string RequiredAbiSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasDeterministicShapeValidation,
    bool HasReplayableFaultModel,
    bool HasRetireOwnedSideEffectCommit,
    bool KeepsEaFallbackNonAuthority);

public readonly record struct MatrixTilePhase04SemanticAbiDecisionRow(
    string Mnemonic,
    string RequiredAbiSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasShapeCompatibilityPolicy,
    bool HasElementTypePolicy,
    bool HasRetireReplayPolicy,
    bool KeepsFallbackNonAuthority);

public readonly record struct MatrixTilePhase05VlmDecisionRow(
    string Mnemonic,
    string RequiredVlmSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasRuntimeOwnedVlmRow,
    bool HasFeatureGate,
    bool HasElementWidthLegality,
    bool HasTileShapeLegality,
    bool HasMemoryLayoutLegality,
    bool HasReservedDisabledRow,
    bool KeepsMetadataNonAuthority,
    bool KeepsDecoderAdmissionFailClosed);

public readonly record struct MatrixTilePhase06DecoderEncoderDecisionRow(
    string Mnemonic,
    string RequiredAbiSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasCanonicalDecoderAcceptance,
    bool HasEncoderRoundTripAbi,
    bool HasDescriptorDecodeValidation,
    bool HasReservedFieldFaultBehavior,
    bool KeepsIllegalRowsBeforeIrMaterializer,
    bool KeepsCompilerAcceptanceNonEvidence);

public readonly record struct MatrixTilePhase07IrMaterializerDecisionRow(
    string Mnemonic,
    string RequiredRuntimeSurface,
    string Decision,
    string PrimaryBlocker,
    bool HasInstructionIrTileDescriptorProjection,
    bool HasMemoryOperandProjection,
    bool HasAccumulatorOperandProjection,
    bool HasTransposePolicyProjection,
    bool HasRegistryEntry,
    bool HasMaterializerFactory,
    bool HasMaterializedTypedRuntimeObject,
    bool PreservesDescriptorValidationResults,
    bool KeepsCompilerIrNonAuthority);

public readonly record struct MatrixTilePhase08MicroOpSchedulerDecisionRow(
    string Mnemonic,
    string RequiredRuntimeSurface,
    string Decision,
    string PrimaryBlocker,
    bool RequiresTileMemoryDependencyMetadata,
    bool RequiresTileRegisterDependencyMetadata,
    bool RequiresAccumulatorDependencyMetadata,
    bool HasTypedTileMicroOp,
    bool HasTileMemoryDependencyMetadata,
    bool HasTileRegisterDependencyMetadata,
    bool HasAccumulatorDependencyMetadata,
    bool HasSchedulerLaneBinding,
    bool HasIssueConstraints,
    bool HasCaptureBarriers,
    bool BlocksSchedulerMaterializerVlmBypass,
    bool KeepsVmxBackendFallbackNonAuthority);

public readonly record struct MatrixTilePhase09ExecuteCaptureDecisionRow(
    string Mnemonic,
    string RequiredRuntimeSurface,
    string Decision,
    string PrimaryBlocker,
    bool RequiresTileLoadCaptureBuffer,
    bool RequiresTileStorePendingWriteBuffer,
    bool RequiresMaccCaptureResult,
    bool RequiresTransposeCaptureResult,
    bool RequiresDeterministicExceptionCapture,
    bool RequiresMemoryFaultCapture,
    bool RequiresTileStateReadSnapshot,
    bool RequiresAccumulatorReadSnapshot,
    bool HasExecutionCaptureSemantics,
    bool HasTileLoadCaptureBuffer,
    bool HasTileStorePendingWriteBuffer,
    bool HasMaccCaptureResult,
    bool HasTransposeCaptureResult,
    bool HasDeterministicExceptionCapture,
    bool HasMemoryFaultCapture,
    bool HasTileStateReadSnapshot,
    bool HasAccumulatorReadSnapshot,
    bool HasRetirePublication,
    bool HasReplayRollbackConformance,
    bool BlocksArchitecturalSideEffectsBeforeRetire,
    bool KeepsVmxBackendFallbackNonAuthority);

public readonly record struct MatrixTilePhase10RetirePublicationDecisionRow(
    string Mnemonic,
    string RequiredRuntimeSurface,
    string Decision,
    string PrimaryBlocker,
    bool RequiresTileLoadRetirePublication,
    bool RequiresTileStoreRetireCommit,
    bool RequiresAccumulatorRetirePublication,
    bool RequiresTransposeRetirePublication,
    bool RequiresFaultRetirementPolicy,
    bool RequiresWritebackOwnership,
    bool RequiresSideEffectPublicationOrdering,
    bool RequiresArchitecturalStateVisibilityRules,
    bool HasRetirePublicationAndCommit,
    bool HasTileLoadRetirePublication,
    bool HasTileStoreRetireCommit,
    bool HasAccumulatorRetirePublication,
    bool HasTransposeRetirePublication,
    bool HasFaultRetirementPolicy,
    bool HasWritebackOwnership,
    bool HasSideEffectPublicationOrdering,
    bool HasArchitecturalStateVisibilityRules,
    bool BlocksExecuteCaptureToRetireBypass,
    bool KeepsHostOwnedEvidenceNonArchitectural,
    bool KeepsVmxBackendFallbackNonAuthority);

public readonly record struct MatrixTilePhase11ReplayRollbackDecisionRow(
    string Mnemonic,
    string RequiredRuntimeSurface,
    string Decision,
    string PrimaryBlocker,
    bool RequiresDecodedInstructionReplayIdentity,
    bool RequiresTileDescriptorReplayIdentity,
    bool RequiresPendingTileWriteRollback,
    bool RequiresPendingMemoryStoreRollback,
    bool RequiresAccumulatorRollback,
    bool RequiresDeterministicReplayAfterMemoryFault,
    bool RequiresDeterministicReplayAfterDescriptorFault,
    bool RequiresLegalIllegalConformanceVectors,
    bool HasReplayRollbackConformance,
    bool HasDecodedInstructionReplayIdentity,
    bool HasTileDescriptorReplayIdentity,
    bool HasPendingTileWriteRollback,
    bool HasPendingMemoryStoreRollback,
    bool HasAccumulatorRollback,
    bool HasDeterministicReplayAfterMemoryFault,
    bool HasDeterministicReplayAfterDescriptorFault,
    bool HasLegalIllegalConformanceVectors,
    bool BlocksReplayWithoutRetirePublication,
    bool BlocksCaptureRecordIdentityBypass,
    bool KeepsVmxBackendFallbackNonAuthority);

public readonly record struct MatrixTilePhase12GoldenArtifactDecisionRow(
    string Mnemonic,
    string RequiredArtifactSurface,
    string Decision,
    string NoFallbackDecision,
    string PrimaryBlocker,
    bool RequiresLegalDecodeEncodeRoundTripVectors,
    bool RequiresLegalIrMaterializerProjectionVectors,
    bool RequiresLegalExecuteRetireVectors,
    bool RequiresMemoryFaultVectors,
    bool RequiresDescriptorFaultVectors,
    bool RequiresAccumulatorVectors,
    bool RequiresTransposeVectors,
    bool RequiresReplayRollbackVectors,
    bool RequiresNegativeReservedVectors,
    bool RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence,
    bool HasPositiveExecutableGoldenArtifacts,
    bool HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence,
    bool HasLegalDecodeEncodeRoundTripVectors,
    bool HasLegalIrMaterializerProjectionVectors,
    bool HasLegalExecuteRetireVectors,
    bool HasMemoryFaultVectors,
    bool HasDescriptorFaultVectors,
    bool HasAccumulatorVectors,
    bool HasTransposeVectors,
    bool HasReplayRollbackVectors,
    bool HasNegativeReservedVectors,
    bool BlocksPositiveGoldenPublication,
    bool BlocksNoFallbackRegressionEvidenceBypass,
    bool KeepsVmxBackendFallbackNonAuthority,
    bool KeepsCompilerScopeClosed,
    bool KeepsCompilerHandoffBlocked);

public static class MatrixTileRuntimeIsaPackageContract
{
    public const string Phase = "Phase14";
    public const string PackageName = "MatrixTileRuntimeIsaPackage";
    public const string Decision = "Phase14TileStreamResourceContourAndPositiveCompilerHandoffClosed";
    public const string ClosureAttemptDate = "2026-06-07";
    public const string ClosureAttemptDecision = "ExecutableClosureBlockedByMissingAbiRuntimeEvidence";
    public const string EvidenceBoundary = "MatrixTileRuntimeExecutableAuthority";
    public const string ExtensionName = "XMatrix";
    public const string VmxBoundary = "GenericRuntimeOnly";
    public const string TileExecutionModelGateDecision = "ClosedExecuteCaptureTileStateSnapshotModel";
    public const string TileDescriptorAbiGateDecision = "ClosedCanonicalTileDescriptorAbi";
    public const string MemoryFaultModelGateDecision = MatrixTileMemoryShapeAndFaultAbi.MemoryShapeFaultDecision;
    public const string AccumulatorTileAbiGateDecision = MatrixTileAccumulatorAndTransposePolicyAbi.MaccAccumulatorDecision;
    public const string TransposePolicyAbiGateDecision = MatrixTileAccumulatorAndTransposePolicyAbi.TransposeCarrierDecision;
    public const string VectorLegalityMatrixGateDecision = MatrixTileRuntimeOwnedVlmRows.RuntimeOwnedVlmRowsDecision;
    public const string RuntimePipelineGateDecision = "ClosedMatrixTileRuntimePipelineThroughPositiveGoldens";
    public const string DecoderEncoderGateDecision = "ClosedMatrixTileDecoderEncoderAbi";
    public const string InstructionIrProjectionGateDecision = MatrixTileIrProjectionAndMaterializer.TileDescriptorProjectionDecision;
    public const string RegistryMaterializerGateDecision = MatrixTileIrProjectionAndMaterializer.MaterializerFactoryDecision;
    public const string TypedTileMicroOpGateDecision = "ClosedOperationSpecificMatrixTileMicroOpCarrier";
    public const string SchedulerLaneBindingGateDecision = "ClosedMatrixTileMemoryLane6AndComputeLanePlacement";
    public const string ExecuteCaptureGateDecision = MatrixTileExecuteCaptureAbi.ExecuteCaptureDecision;
    public const string RetirePublicationGateDecision = MatrixTileRetirePublicationAbi.RetirePublicationDecision;
    public const string ReplayRollbackGateDecision = MatrixTileReplayRollbackAbi.ReplayRollbackDecision;
    public const string GoldenArtifactsGateDecision = MatrixTilePositiveGoldenArtifactManifest.ManifestDecision;
    public const string CompilerUpdateReadinessDecision = MatrixTileCompilerEmissionHandoffPackage.CompilerBoundaryDecision;
    public const string CompilerNoEmissionBoundaryDecision = "SupersededByExistingPositiveCompilerImplementationCompilerCodeUnchangedByPhase14";
    public const string ClosureLedgerDecision = "RuntimeIsaPhases01To14ClosedExecutableStatusAndCompilerHandoffReady";
    public const string NegativeGoldenManifestPath = "CloseToRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/PHASE_09B_MATRIX_TILE_NEGATIVE_GOLDEN_MANIFEST_2026-06-07.md";
    public const string PositiveGoldenManifestPath = "CloseToRTL/Core/ISA/Instructions/NonVmx/Lanes00_03Vector/MatrixTile/MatrixTilePositiveGoldenArtifactManifest.cs";
    public const string GoldenArtifactPublicationDecision = MatrixTilePositiveGoldenArtifactManifest.ManifestDecision;
    public const string RemainingRuntimeIsaTaskPoolDecision = "RuntimeIsaPhases01To14ClosedNoRemainingRuntimeTasks";
    public const string StatusCatalogAuthorityDecision = MatrixTileCompilerEmissionHandoffPackage.StatusCatalogDecision;
    public const string OpcodeDescriptorAuthorityDecision = "PackageOpcodesAreExecutableIdentityAuthorityWithinClosedRuntimeEvidenceChain";
    public const string Phase01AuthorityAdrPath = "Documentation/InstructionsList/MTILE_RefPlan/01_authority_status_catalog_and_opcode_adr.md";
    public const string Phase01AuthorityDecision = "ClosedRetainedNumericOpcodesArePackageOpcodeIdentityAuthority";
    public const string Phase01AuthorityReviewDate = "2026-06-08";
    public const string Phase01RetainedOpcodeRole = "PackageOpcodeIdentityAuthorityOnlyNoExecution";
    public const string Phase01DescriptorAuthorityRole = "NoDescriptorOpTypeAuthorityOpened";
    public const string Phase01PackageFeatureRole = "SingleXMatrixDeclaredPackageFeatureDeferred";
    public const string Phase01StatusCatalogPromotionRule = "PositiveStatusCatalogPromotionOnlyAfterFullRuntimeIsaEvidenceChainAndPhase13";
    public const string Phase01MtileMemoryPolicyDecision = MatrixTileMemoryShapeAndFaultAbi.MemoryShapeFaultDecision;
    public const string Phase01MtransposePolicyDecision = MatrixTileAccumulatorAndTransposePolicyAbi.TransposeCarrierDecision;
    public const string Phase01BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase02StateDescriptorAbiPath = "Documentation/InstructionsList/MTILE_RefPlan/02_tile_state_and_descriptor_abi.md";
    public const string Phase02StateDescriptorDecision = "ClosedGuestArchitecturalTileStateOwnerAndCanonicalDescriptorAbi";
    public const string Phase02ReviewDate = "2026-06-08";
    public const string Phase02TileStateOwnerDecision = MatrixTileArchitecturalTileStateAndDescriptorAbi.TileStateOwnerDecision;
    public const string Phase02DescriptorCarrierDecision = MatrixTileArchitecturalTileStateAndDescriptorAbi.DescriptorCarrierDecision;
    public const string Phase02DescriptorValidationDecision = MatrixTileArchitecturalTileStateAndDescriptorAbi.DescriptorValidationDecision;
    public const string Phase02ReservedDescriptorDecision = MatrixTileArchitecturalTileStateAndDescriptorAbi.ReservedDescriptorDecision;
    public const string Phase02GuestHostEvidenceBoundary = "HostOwnedEvidenceCannotBecomeGuestArchitecturalTileState";
    public const string Phase02BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase03MemoryShapeFaultAbiPath = "Documentation/InstructionsList/MTILE_RefPlan/03_memory_shape_and_fault_model.md";
    public const string Phase03MemoryShapeFaultDecision = MatrixTileMemoryShapeAndFaultAbi.MemoryShapeFaultDecision;
    public const string Phase03ReviewDate = "2026-06-08";
    public const string Phase03EffectiveAddressDecision = MatrixTileMemoryShapeAndFaultAbi.EffectiveAddressDecision;
    public const string Phase03ShapeValidationDecision = MatrixTileMemoryShapeAndFaultAbi.ShapeValidationDecision;
    public const string Phase03FaultReplayDecision = MatrixTileMemoryShapeAndFaultAbi.FaultReplayDecision;
    public const string Phase03SideEffectOwnerDecision = MatrixTileMemoryShapeAndFaultAbi.SideEffectOwnerDecision;
    public const string Phase03EaFallbackDecision = MatrixTileMemoryShapeAndFaultAbi.EaFallbackDecision;
    public const string Phase03BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase04AccumulatorTransposeAbiPath = "Documentation/InstructionsList/MTILE_RefPlan/04_accumulator_and_transpose_policy_abi.md";
    public const string Phase04AccumulatorTransposeDecision = MatrixTileAccumulatorAndTransposePolicyAbi.AccumulatorTransposeDecision;
    public const string Phase04ReviewDate = "2026-06-08";
    public const string Phase04MaccAccumulatorDecision = MatrixTileAccumulatorAndTransposePolicyAbi.MaccAccumulatorDecision;
    public const string Phase04MaccShapeDecision = MatrixTileAccumulatorAndTransposePolicyAbi.MaccShapeDecision;
    public const string Phase04MaccRetireReplayDecision = MatrixTileAccumulatorAndTransposePolicyAbi.MaccRetireReplayDecision;
    public const string Phase04TransposeCarrierDecision = MatrixTileAccumulatorAndTransposePolicyAbi.TransposeCarrierDecision;
    public const string Phase04TransposeLayoutDecision = MatrixTileAccumulatorAndTransposePolicyAbi.TransposeLayoutDecision;
    public const string Phase04TransposeRetireReplayDecision = MatrixTileAccumulatorAndTransposePolicyAbi.TransposeRetireReplayDecision;
    public const string Phase04FallbackDecision = MatrixTileAccumulatorAndTransposePolicyAbi.FallbackDecision;
    public const string Phase04BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase05RuntimeOwnedVlmRowsPath = "Documentation/InstructionsList/MTILE_RefPlan/05_runtime_owned_vlm_rows.md";
    public const string Phase05RuntimeOwnedVlmRowsDecision = MatrixTileRuntimeOwnedVlmRows.RuntimeOwnedVlmRowsDecision;
    public const string Phase05ReviewDate = "2026-06-08";
    public const string Phase05FeatureGateDecision = MatrixTileRuntimeOwnedVlmRows.FeatureGateDecision;
    public const string Phase05ElementWidthDecision = MatrixTileRuntimeOwnedVlmRows.ElementWidthDecision;
    public const string Phase05TileShapeDecision = MatrixTileRuntimeOwnedVlmRows.TileShapeDecision;
    public const string Phase05MemoryLayoutDecision = MatrixTileRuntimeOwnedVlmRows.MemoryLayoutDecision;
    public const string Phase05ReservedDisabledDecision = MatrixTileRuntimeOwnedVlmRows.ReservedDisabledDecision;
    public const string Phase05MetadataNonAuthorityDecision = MatrixTileRuntimeOwnedVlmRows.MetadataNonAuthorityDecision;
    public const string Phase05DecoderAdmissionDecision = MatrixTileRuntimeOwnedVlmRows.DecoderAdmissionDecision;
    public const string Phase05BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase06DecoderEncoderAbiPath = "Documentation/InstructionsList/MTILE_RefPlan/06_decoder_encoder_abi.md";
    public const string Phase06DecoderEncoderDecision = "ClosedMatrixTileDecoderEncoderAbi";
    public const string Phase06ReviewDate = "2026-06-08";
    public const string Phase06DispatchSourceDecision = "OpcodeOrDescriptorDispatchSourceSelected";
    public const string Phase06BinaryLayoutDecision = "CanonicalVectorCarrierBinaryFieldLayoutSelected";
    public const string Phase06OperandMappingDecision = "CanonicalVectorCarrierOperandMappingSelected";
    public const string Phase06DescriptorDecodeDecision = "DescriptorEncodingAndValidationSelected";
    public const string Phase06ReservedFieldDecision = "ReservedMalformedDisabledDecodeFaultAbiSelected";
    public const string Phase06EncoderRoundTripDecision = "EncoderRoundTripAbiSelected";
    public const string Phase06FeatureGateDecision = "RuntimeOwnedVlmRowsSelectCanonicalMatrixTileDecode";
    public const string Phase06BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase07IrProjectionMaterializerPath = "Documentation/InstructionsList/MTILE_RefPlan/07_ir_projection_and_materializer.md";
    public const string Phase07IrProjectionMaterializerDecision = MatrixTileIrProjectionAndMaterializer.IrProjectionMaterializerDecision;
    public const string Phase07ReviewDate = "2026-06-08";
    public const string Phase07TileDescriptorProjectionDecision = MatrixTileIrProjectionAndMaterializer.TileDescriptorProjectionDecision;
    public const string Phase07MemoryOperandProjectionDecision = MatrixTileIrProjectionAndMaterializer.MemoryOperandProjectionDecision;
    public const string Phase07AccumulatorProjectionDecision = MatrixTileIrProjectionAndMaterializer.AccumulatorProjectionDecision;
    public const string Phase07TransposeProjectionDecision = MatrixTileIrProjectionAndMaterializer.TransposeProjectionDecision;
    public const string Phase07RegistryEntryDecision = MatrixTileIrProjectionAndMaterializer.RegistryEntryDecision;
    public const string Phase07MaterializerFactoryDecision = MatrixTileIrProjectionAndMaterializer.MaterializerFactoryDecision;
    public const string Phase07TypedObjectDecision = MatrixTileIrProjectionAndMaterializer.TypedObjectDecision;
    public const string Phase07InvalidIrDecision = MatrixTileIrProjectionAndMaterializer.InvalidIrDecision;
    public const string Phase07CompilerIrBoundaryDecision = MatrixTileIrProjectionAndMaterializer.CompilerIrBoundaryDecision;
    public const string Phase07BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase08TypedTileMicroOpSchedulerLanePath = "Documentation/InstructionsList/MTILE_RefPlan/08_typed_tile_microop_and_scheduler_lane.md";
    public const string Phase08TypedTileMicroOpSchedulerLaneDecision = "SupersededByPhase14OperationSpecificResourceContour";
    public const string Phase08ReviewDate = "2026-06-08";
    public const string Phase08TypedMicroOpDecision = "ClosedTypedTileMicroOpAbi";
    public const string Phase08TileMemoryDependencyDecision = "ClosedTileMemoryDependencyMetadata";
    public const string Phase08TileRegisterDependencyDecision = "ClosedTileRegisterDependencyMetadata";
    public const string Phase08AccumulatorDependencyDecision = "ClosedAccumulatorDependencyMetadata";
    public const string Phase08SchedulerLaneBindingDecision = "ClosedMatrixTileMemoryLane6AndComputeLanes00To03Binding";
    public const string Phase08IssueConstraintsDecision = "ClosedTileIssueConstraints";
    public const string Phase08CaptureBarrierDecision = "ClosedTileMemoryAndTileStateCaptureBarrierMetadata";
    public const string Phase08BypassDecision = "NoMicroOpSchedulerMaterializerBypassWithoutPriorPhases";
    public const string Phase08FallbackDecision = "NoVmxBackendOrExternalFallbackAuthority";
    public const string Phase08BlockingReason = "Phase12ClosedStatusCatalogAndCompilerHandoffRemain";
    public const string Phase09ExecuteCaptureSemanticsPath = "Documentation/InstructionsList/MTILE_RefPlan/09_execute_capture_semantics.md";
    public const string Phase09ExecuteCaptureSemanticsDecision = MatrixTileExecuteCaptureAbi.ExecuteCaptureDecision;
    public const string Phase09ReviewDate = "2026-06-09";
    public const string Phase09LoadCaptureDecision = MatrixTileExecuteCaptureAbi.TileLoadCaptureDecision;
    public const string Phase09StoreCaptureDecision = MatrixTileExecuteCaptureAbi.TileStoreCaptureDecision;
    public const string Phase09MaccCaptureDecision = MatrixTileExecuteCaptureAbi.MaccCaptureDecision;
    public const string Phase09TransposeCaptureDecision = MatrixTileExecuteCaptureAbi.TransposeCaptureDecision;
    public const string Phase09DeterministicCaptureDecision = MatrixTileExecuteCaptureAbi.DeterministicExceptionCaptureDecision;
    public const string Phase09MemoryFaultCaptureDecision = MatrixTileExecuteCaptureAbi.MemoryFaultCaptureDecision;
    public const string Phase09TileStateSnapshotDecision = MatrixTileExecuteCaptureAbi.TileStateSnapshotDecision;
    public const string Phase09AccumulatorSnapshotDecision = MatrixTileExecuteCaptureAbi.AccumulatorSnapshotDecision;
    public const string Phase09RetirePublicationDecision = MatrixTileExecuteCaptureAbi.RetirePublicationDecision;
    public const string Phase09ReplayRollbackDecision = MatrixTileExecuteCaptureAbi.ReplayRollbackDecision;
    public const string Phase09BypassDecision = MatrixTileExecuteCaptureAbi.BypassDecision;
    public const string Phase09FallbackDecision = MatrixTileExecuteCaptureAbi.FallbackDecision;
    public const string Phase09BlockingReason = "NonePhase14ClosedHistoricalExecuteCaptureBoundaryPreserved";
    public const string Phase10RetirePublicationCommitPath = "Documentation/InstructionsList/MTILE_RefPlan/10_retire_publication_and_commit.md";
    public const string Phase10RetirePublicationCommitDecision = MatrixTileRetirePublicationAbi.RetirePublicationDecision;
    public const string Phase10ReviewDate = "2026-06-10";
    public const string Phase10LoadRetireDecision = MatrixTileRetirePublicationAbi.LoadPublicationDecision;
    public const string Phase10StoreRetireDecision = MatrixTileRetirePublicationAbi.StoreCommitDecision;
    public const string Phase10MaccRetireDecision = MatrixTileRetirePublicationAbi.MaccPublicationDecision;
    public const string Phase10TransposeRetireDecision = MatrixTileRetirePublicationAbi.TransposePublicationDecision;
    public const string Phase10FaultRetirementDecision = MatrixTileRetirePublicationAbi.FaultRetirementDecision;
    public const string Phase10WritebackOwnershipDecision = MatrixTileRetirePublicationAbi.WritebackOwnershipDecision;
    public const string Phase10SideEffectOrderingDecision = MatrixTileRetirePublicationAbi.SideEffectOrderingDecision;
    public const string Phase10ArchitecturalVisibilityDecision = MatrixTileRetirePublicationAbi.ArchitecturalVisibilityDecision;
    public const string Phase10BypassDecision = MatrixTileRetirePublicationAbi.DuplicateRetireDecision;
    public const string Phase10CaptureCorrelationDecision = MatrixTileRetirePublicationAbi.CorrelationDecision;
    public const string Phase10HostEvidenceDecision = "NoHostOwnedEvidencePublishedAsGuestArchitecturalState";
    public const string Phase10FallbackDecision = MatrixTileRetirePublicationAbi.FallbackDecision;
    public const string Phase10BlockingReason = "NonePhase10ClosedPhase11ReplayRollbackClosed";
    public const string Phase11ReplayRollbackConformancePath = "Documentation/InstructionsList/MTILE_RefPlan/11_replay_rollback_conformance.md";
    public const string Phase11ReplayRollbackConformanceDecision = MatrixTileReplayRollbackAbi.ReplayRollbackDecision;
    public const string Phase11ReviewDate = "2026-06-10";
    public const string Phase11DecodedInstructionReplayIdentityDecision = MatrixTileReplayRollbackAbi.DecodedInstructionIdentityDecision;
    public const string Phase11TileDescriptorReplayIdentityDecision = MatrixTileReplayRollbackAbi.TileDescriptorIdentityDecision;
    public const string Phase11PendingTileWriteRollbackDecision = MatrixTileReplayRollbackAbi.TileRollbackDecision;
    public const string Phase11PendingMemoryStoreRollbackDecision = MatrixTileReplayRollbackAbi.MemoryRollbackDecision;
    public const string Phase11AccumulatorRollbackDecision = MatrixTileReplayRollbackAbi.AccumulatorRollbackDecision;
    public const string Phase11MemoryFaultReplayDecision = MatrixTileReplayRollbackAbi.MemoryFaultReplayDecision;
    public const string Phase11DescriptorFaultReplayDecision = MatrixTileReplayRollbackAbi.DescriptorFaultReplayDecision;
    public const string Phase11ConformanceVectorDecision = "ClosedReplayRollbackLegalIllegalConformanceVectors";
    public const string Phase11BypassDecision = MatrixTileReplayRollbackAbi.BypassDecision;
    public const string Phase11FallbackDecision = MatrixTileReplayRollbackAbi.FallbackDecision;
    public const string Phase11BlockingReason = "NonePhase11ClosedPhase12Closed";
    public const string Phase12PositiveExecutableGoldenArtifactsPath = "Documentation/InstructionsList/MTILE_RefPlan/12_positive_executable_golden_artifacts.md";
    public const string Phase12PositiveExecutableGoldenArtifactsDecision = MatrixTilePositiveGoldenArtifactManifest.ManifestDecision;
    public const string Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision = MatrixTilePositiveGoldenArtifactManifest.NoFallbackDecision;
    public const string Phase12ReviewDate = "2026-06-10";
    public const string Phase12DecodeEncodeVectorsDecision = "ClosedLegalDecodeEncodeRoundTripGoldenVectors";
    public const string Phase12IrMaterializerVectorsDecision = "ClosedLegalIrMaterializerProjectionGoldenVectors";
    public const string Phase12ExecuteRetireVectorsDecision = "ClosedLegalExecuteRetireGoldenVectors";
    public const string Phase12MemoryFaultVectorsDecision = "ClosedLoadStoreAllOrNoneMemoryFaultGoldenVectors";
    public const string Phase12DescriptorFaultVectorsDecision = "ClosedDescriptorFaultGoldenVectors";
    public const string Phase12AccumulatorVectorsDecision = "ClosedAccumulatorGoldenVectors";
    public const string Phase12TransposeVectorsDecision = "ClosedTransposeGoldenVectors";
    public const string Phase12ReplayRollbackVectorsDecision = "ClosedReplayRollbackGoldenVectors";
    public const string Phase12ReservedRowsDecision = MatrixTilePositiveGoldenArtifactManifest.ReservedCarrierDecision;
    public const string Phase12NoFallbackRegressionDecision = MatrixTileNoFallbackEvidenceContract.EvidenceDecision;
    public const string Phase12BlockingReason = "NonePhase14RegeneratedPlacementSensitiveEvidenceClosed";
    public const string Phase13RuntimeIsaReadinessForCompilerHandoffPath = "Documentation/InstructionsList/MTILE_RefPlan/13_runtime_isa_readiness_for_compiler_handoff.md";
    public const string Phase13ReviewDate = "2026-06-10";
    public const string Phase13CompilerVisibleNoEmissionBoundaryDecision = "SupersededByExistingPositiveCompilerImplementation";
    public const string Phase13PositiveCompilerEmissionHandoffDecision = MatrixTileCompilerEmissionHandoffPackage.HandoffDecision;
    public const string Phase13BlockingReason = "SupersededPhase13ReadinessWasSuspendedUntilPhase14Evidence";
    public const string Phase14TileStreamResourceContourPath = "Documentation/InstructionsList/MTILE_RefPlan/14_tile_stream_resource_contour_correction.md";
    public const string Phase14ReviewDate = "2026-06-11";
    public const string Phase14ResourceClassificationDecision = "ClosedMatrixTileMemoryAndMatrixTileComputeRuntimeClassification";
    public const string Phase14SchedulerPlacementDecision = "ClosedMatrixTileMemoryLane6AndMatrixTileComputePlacement";
    public const string Phase14StreamTransferDecision = MatrixTileStreamTransferAbi.TransferDecision;
    public const string Phase14ReadinessDecision = "ClosedAfterRegeneratedPlacementSensitiveRuntimeEvidence";
    public const string SupersededSchedulerClaim = "AllMatrixTileMicroOpsUseCommonAluClass";
    public const string CompilerHandoffGateDecision = Phase13PositiveCompilerEmissionHandoffDecision;

    public const bool ClosesTileExecutionModel = true;
    public const bool ClosesTileDescriptorAbi = true;
    public const bool ClosesMemoryShapeFaultModel = true;
    public const bool ClosesAccumulatorTileAbi = true;
    public const bool ClosesTransposePolicyAbi = true;
    public const bool ClosesVectorLegalityMatrix = true;
    public const bool ClosesDecoderEncoderAbi = true;
    public const bool ClosesInstructionIrProjection = true;
    public const bool ClosesRegistryMaterializer = true;
    public const bool ClosesTypedTileMicroOp = true;
    public const bool ClosesSchedulerLaneBinding = true;
    public const bool ClosesExecuteCapture = true;
    public const bool ClosesRetirePublication = true;
    public const bool ClosesReplayRollback = true;
    public const bool ClosesGoldenArtifacts = true;
    public const bool ClosesTileStreamResourceContour = true;

    public const bool OpensDecoderEncoderAbi = true;
    public const bool OpensInstructionIrProjection = true;
    public const bool OpensRegistryMaterializer = true;
    public const bool PublishesTypedTileMicroOp = true;
    public const bool OpensSchedulerLaneBinding = true;
    public const bool OpensExecutionCapture = true;
    public const bool OpensRetireWritebackOrSideEffects = true;
    public const bool OpensReplayRollback = true;
    public const bool OpensCompilerHelper = false;
    public const bool OpensVmxSpecificPath = false;
    public const bool PublishesHostOwnedEvidence = false;
    public const bool HasArchitecturalTileRegisterFile = true;
    public const bool HasMemoryBackedTileStateModel = false;
    public const bool HasCanonicalTileDescriptorCarrier = true;
    public const bool HasTileLifetimeOwnershipPolicy = true;
    public const bool HasTileElementTypePolicy = true;
    public const bool HasTileShapePolicy = true;
    public const bool HasTileMemoryAlignmentPolicy = true;
    public const bool HasPartialFaultPolicy = true;
    public const bool HasRetireOwnedTilePublicationPolicy = true;
    public const bool HasAccumulatorTileAbi = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorTileAbi;
    public const bool HasAccumulatorTileStateOwner = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorTileStateOwner;
    public const bool HasAccumulatorTileFootprintPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorTileFootprintPolicy;
    public const bool HasAccumulatorDtypePolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorDtypePolicy;
    public const bool HasMaccShapeCompatibilityPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasMaccShapeCompatibilityPolicy;
    public const bool HasMaccExceptionOrSaturationPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasMaccExceptionOrSaturationPolicy;
    public const bool HasTransposePolicyAbi = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposePolicyAbi;
    public const bool HasTransposePolicyCarrier = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposePolicyCarrier;
    public const bool HasTransposeSourceDestinationAliasPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposeSourceDestinationAliasPolicy;
    public const bool HasInPlaceTransposePolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasInPlaceTransposePolicy;
    public const bool HasTransposeLayoutPermutationPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposeLayoutPermutationPolicy;
    public static bool HasVectorLegalityMatrixRows => MatrixTileRuntimeOwnedVlmRows.HasRuntimeOwnedVlmRows;
    public static bool HasMatrixTileVlmContour => MatrixTileRuntimeOwnedVlmRows.HasDescriptorBackedOnlyContour;
    public const bool IsReadyForCompilerUpdate = true;
    public const bool HasExecutableRuntimeIsaClosure = true;
    public const bool BlocksCompilerUpdate = false;
    public const bool IsReadyForCompilerVisibleNoEmissionBoundary = false;
    public const bool HasCompilerVisibleNoEmissionBoundaryPackage = true;
    public const bool AllowsCompilerVisibleNoEmissionBoundaryWork = false;
    public const bool BlocksCompilerVisibleNoEmissionBoundary = true;
    public const bool IsReadyForPositiveCompilerEmissionHandoff = true;
    public const bool HasPositiveCompilerEmissionHandoffPackage = true;
    public const bool BlocksPositiveCompilerEmission = false;
    public const bool BlocksCompilerHelperIrSelectionEmission = false;
    public const bool Phase13ClosesCompilerVisibleNoEmissionBoundary = false;
    public const bool Phase13KeepsPositiveCompilerEmissionBlocked = false;
    public const bool Phase13KeepsCompilerCodeUnmodified = true;
    public const bool Phase14WasFailClosedDuringCorrection = true;
    public const bool Phase14SupersedesAllAluSchedulerClaim = true;
    public const bool Phase14RegeneratedPlacementSensitiveGoldenEvidence = true;
    public const bool Phase14CompilerHandoffWasSuspendedUntilClosure = true;
    public const bool HasMatrixTileMemoryResourceClass = true;
    public const bool HasMatrixTileComputeResourceClass = true;
    public const bool HasDedicatedMatrixTileStreamLane6 = true;
    public const bool UsesDmaStreamComputeAuthority = false;
    public const bool UsesGenericStreamEngineExecutionAuthority = false;
    public const bool StreamTransportOwnsArchitecturalPublication = false;
    public const bool HasNegativeGoldenManifest = true;
    public const bool HasPositiveExecutableGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasPositiveExecutableGoldenArtifacts;
    public const bool BlocksPositiveGoldenPublication = false;
    public const bool HasClosableRuntimeIsaTaskWithoutAdr = false;
    public const bool HasRemainingRuntimeIsaOpenTasks = false;
    public const bool HasExecutableStatusCatalogPromotion = true;
    public const bool HasOpcodeOrDescriptorAuthority = true;
    public const bool TreatsRetainedNumericOpcodeAsExecutableAuthority = false;
    public const bool RequiresAdrForOpcodeOrDescriptorAuthority = false;
    public const bool HasPhase01ProductionAuthoritySource = true;
    public const bool Phase01AuthorityAdrBlocksPhase02 = false;
    public const bool Phase01KeepsStatusCatalogOptionalDisabled = true;
    public const bool Phase01KeepsDecoderEncoderFailClosed = true;
    public const bool Phase01KeepsCompilerHandoffBlocked = true;
    public const bool HasPhase02ArchitecturalTileStateOwner = MatrixTileArchitecturalTileStateAndDescriptorAbi.HasArchitecturalTileStateOwner;
    public const bool HasPhase02CanonicalTileDescriptorAbi = MatrixTileArchitecturalTileStateAndDescriptorAbi.HasCanonicalTileDescriptorAbi;
    public const bool HasPhase02RuntimeOwnedTileStateContract = MatrixTileArchitecturalTileStateAndDescriptorAbi.HasRuntimeOwnedTileStateContract;
    public const bool HasPhase02TileDescriptorValidationHelpers = MatrixTileArchitecturalTileStateAndDescriptorAbi.HasTileDescriptorValidationHelpers;
    public const bool HasPhase02ReservedDescriptorFailFastTests = MatrixTileArchitecturalTileStateAndDescriptorAbi.HasReservedDescriptorFailFastTests;
    public const bool Phase02KeepsExternalDescriptorsNonAuthority = true;
    public const bool Phase02KeepsHostOwnedEvidenceNonArchitectural = true;
    public const bool Phase02KeepsCompilerHandoffBlocked = true;
    public const bool Phase02BlocksPhase03 = false;
    public const bool HasPhase03TileMemoryShapeFaultAbi = MatrixTileMemoryShapeAndFaultAbi.HasTileMemoryShapeFaultAbi;
    public const bool HasPhase03EffectiveAddressSource = MatrixTileMemoryShapeAndFaultAbi.HasEffectiveAddressSource;
    public const bool HasPhase03ShapeValidation = MatrixTileMemoryShapeAndFaultAbi.HasShapeValidation;
    public const bool HasPhase03AddressOverflowValidation = MatrixTileMemoryShapeAndFaultAbi.HasAddressOverflowValidation;
    public const bool HasPhase03AlignmentPolicy = MatrixTileMemoryShapeAndFaultAbi.HasAlignmentPolicy;
    public const bool HasPhase03PageCrossingPolicy = MatrixTileMemoryShapeAndFaultAbi.HasPageCrossingPolicy;
    public const bool HasPhase03PartialFaultPolicy = MatrixTileMemoryShapeAndFaultAbi.HasPartialFaultPolicy;
    public const bool HasPhase03MemoryOrderingPolicy = MatrixTileMemoryShapeAndFaultAbi.HasMemoryOrderingPolicy;
    public const bool HasPhase03LoadStoreSideEffectOwner = MatrixTileMemoryShapeAndFaultAbi.HasLoadStoreSideEffectOwner;
    public const bool HasPhase03RetireRollbackPolicy = MatrixTileMemoryShapeAndFaultAbi.HasRetireRollbackPolicy;
    public const bool Phase03KeepsEaFallbackNonAuthority = MatrixTileMemoryShapeAndFaultAbi.KeepsEaFallbackNonAuthority;
    public const bool Phase03KeepsMemorySideEffectsUnopened = MatrixTileMemoryShapeAndFaultAbi.KeepsMemorySideEffectsUnopened;
    public const bool Phase03KeepsCompilerHandoffBlocked = MatrixTileMemoryShapeAndFaultAbi.KeepsCompilerHandoffBlocked;
    public const bool HasPhase04AccumulatorTileAbi = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorTileAbi;
    public const bool HasPhase04TransposePolicyAbi = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposePolicyAbi;
    public const bool HasPhase04AccumulatorFootprint = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorTileFootprintPolicy;
    public const bool HasPhase04AccumulatorDtypePromotion = MatrixTileAccumulatorAndTransposePolicyAbi.HasAccumulatorDtypePolicy;
    public const bool HasPhase04AccumulatorExceptionSaturationPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasMaccExceptionOrSaturationPolicy;
    public const bool HasPhase04AccumulatorShapeCompatibility = MatrixTileAccumulatorAndTransposePolicyAbi.HasMaccShapeCompatibilityPolicy;
    public const bool HasPhase04TransposeSourceDestinationPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposeSourceDestinationAliasPolicy;
    public const bool HasPhase04TransposeInPlaceAliasPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasInPlaceTransposePolicy;
    public const bool HasPhase04TransposeLayoutPermutationPolicy = MatrixTileAccumulatorAndTransposePolicyAbi.HasTransposeLayoutPermutationPolicy;
    public const bool HasPhase04InvalidShapeTypeAliasDeterminism = MatrixTileAccumulatorAndTransposePolicyAbi.HasInvalidShapeTypeAliasDeterminism;
    public const bool Phase04KeepsVectorTransposeNonAuthority = MatrixTileAccumulatorAndTransposePolicyAbi.KeepsVectorTransposeNonAuthority;
    public const bool Phase04KeepsExternalBackendNonAuthority = MatrixTileAccumulatorAndTransposePolicyAbi.KeepsExternalBackendNonAuthority;
    public const bool Phase04KeepsCompilerMatrixIrIndependent = MatrixTileAccumulatorAndTransposePolicyAbi.KeepsCompilerMatrixIrIndependent;
    public const bool Phase04KeepsCompilerHandoffBlocked = MatrixTileAccumulatorAndTransposePolicyAbi.KeepsCompilerHandoffBlocked;
    public static bool HasPhase05RuntimeOwnedVlmRows => MatrixTileRuntimeOwnedVlmRows.HasRuntimeOwnedVlmRows;
    public static bool HasPhase05MatrixTileVlmFamily => MatrixTileRuntimeOwnedVlmRows.HasMatrixTileVlmFamily;
    public static bool HasPhase05DescriptorBackedOnlyContour => MatrixTileRuntimeOwnedVlmRows.HasDescriptorBackedOnlyContour;
    public static bool HasPhase05FeatureGate => MatrixTileRuntimeOwnedVlmRows.HasFeatureGate;
    public static bool HasPhase05ElementWidthLegality => MatrixTileRuntimeOwnedVlmRows.HasElementWidthLegality;
    public static bool HasPhase05TileShapeLegality => MatrixTileRuntimeOwnedVlmRows.HasTileShapeLegality;
    public static bool HasPhase05MemoryLayoutLegality => MatrixTileRuntimeOwnedVlmRows.HasMemoryLayoutLegality;
    public static bool HasPhase05ReservedDisabledRows => MatrixTileRuntimeOwnedVlmRows.HasReservedDisabledRows;
    public static bool Phase05KeepsNonDescriptorContoursFailClosed => MatrixTileRuntimeOwnedVlmRows.KeepsNonDescriptorContoursFailClosed;
    public static bool Phase05KeepsExecutableContoursClosed => MatrixTileRuntimeOwnedVlmRows.KeepsExecutableContoursClosed;
    public const bool Phase05KeepsClassifierMetadataNonAuthority = MatrixTileRuntimeOwnedVlmRows.KeepsClassifierMetadataNonAuthority;
    public const bool Phase05KeepsOptionalDisabledStatusNonAuthority = MatrixTileRuntimeOwnedVlmRows.KeepsOptionalDisabledStatusNonAuthority;
    public const bool Phase05KeepsDecoderAdmissionBlocked = MatrixTileRuntimeOwnedVlmRows.KeepsDecoderAdmissionBlocked;
    public const bool Phase05KeepsCompilerEmissionIndependent = MatrixTileRuntimeOwnedVlmRows.KeepsCompilerEmissionIndependent;
    public const bool Phase05KeepsCompilerHandoffBlocked = MatrixTileRuntimeOwnedVlmRows.KeepsCompilerHandoffBlocked;
    public const bool HasPhase06CanonicalDecoderAcceptance = true;
    public const bool HasPhase06EncoderAbi = true;
    public const bool HasPhase06BinaryFieldLayout = true;
    public const bool HasPhase06OperandFieldMapping = true;
    public const bool HasPhase06DescriptorDecodeValidation = true;
    public const bool HasPhase06ReservedMalformedFaultBehavior = true;
    public const bool HasPhase06EncoderRoundTripTests = true;
    public const bool HasPhase06PackageFeatureDecodeGate = true;
    public const bool Phase06KeepsIllegalRowsBeforeIrMaterializer = true;
    public const bool Phase06KeepsCompilerAcceptanceNonEvidence = true;
    public const bool Phase06KeepsCompilerEmissionOutOfScope = true;
    public const bool Phase06KeepsCompilerHandoffBlocked = true;
    public const bool HasPhase07InstructionIrTileProjection = MatrixTileIrProjectionAndMaterializer.HasInstructionIrTileProjection;
    public const bool HasPhase07TileDescriptorIrCarrier = MatrixTileIrProjectionAndMaterializer.HasTileDescriptorIrCarrier;
    public const bool HasPhase07MemoryOperandProjection = MatrixTileIrProjectionAndMaterializer.HasMemoryOperandProjection;
    public const bool HasPhase07AccumulatorOperandProjection = MatrixTileIrProjectionAndMaterializer.HasAccumulatorOperandProjection;
    public const bool HasPhase07TransposePolicyProjection = MatrixTileIrProjectionAndMaterializer.HasTransposePolicyProjection;
    public const bool HasPhase07RegistryEntries = MatrixTileIrProjectionAndMaterializer.HasRegistryEntries;
    public const bool HasPhase07MaterializerFactories = MatrixTileIrProjectionAndMaterializer.HasMaterializerFactories;
    public const bool HasPhase07MaterializedTypedRuntimeObjects = MatrixTileIrProjectionAndMaterializer.HasMaterializedTypedRuntimeObjects;
    public const bool HasPhase07DescriptorValidationResultPreservation = MatrixTileIrProjectionAndMaterializer.HasDescriptorValidationResultPreservation;
    public const bool HasPhase07InvalidIrProjectionFaults = MatrixTileIrProjectionAndMaterializer.HasInvalidIrProjectionFaults;
    public const bool Phase07KeepsCompilerIrNonAuthority = MatrixTileIrProjectionAndMaterializer.KeepsCompilerIrNonAuthority;
    public const bool Phase07KeepsCompilerScopeClosed = MatrixTileIrProjectionAndMaterializer.KeepsCompilerScopeClosed;
    public const bool Phase07KeepsCompilerHandoffBlocked = MatrixTileIrProjectionAndMaterializer.KeepsCompilerHandoffBlocked;
    public const bool HasPhase08TypedTileMicroOp = true;
    public const bool HasPhase08TileMemoryDependencyMetadata = true;
    public const bool HasPhase08TileRegisterDependencyMetadata = true;
    public const bool HasPhase08AccumulatorDependencyMetadata = true;
    public const bool HasPhase08TileDependencyModel = true;
    public const bool HasPhase08SchedulerLaneBinding = true;
    public const bool HasPhase08IssueConstraints = true;
    public const bool HasPhase08CaptureBarriers = true;
    public const bool HasPhase08VmxOrBackendFallback = false;
    public const bool Phase08BlocksMicroOpCreationBeforeMaterializer = true;
    public const bool Phase08BlocksSchedulerMaterializerVlmBypass = true;
    public const bool Phase08KeepsVmxBackendFallbackNonAuthority = true;
    public const bool Phase08KeepsCompilerScopeClosed = true;
    public const bool Phase08KeepsCompilerHandoffBlocked = true;
    public const bool HasPhase09ExecutionCaptureSemantics = MatrixTileExecuteCaptureAbi.HasExecutionCaptureSemantics;
    public const bool HasPhase09TileLoadCaptureBuffer = MatrixTileExecuteCaptureAbi.HasTileLoadCaptureBuffer;
    public const bool HasPhase09TileStorePendingWriteBuffer = MatrixTileExecuteCaptureAbi.HasTileStorePendingWriteBuffer;
    public const bool HasPhase09MaccCaptureResult = MatrixTileExecuteCaptureAbi.HasMaccCaptureResult;
    public const bool HasPhase09TransposeCaptureResult = MatrixTileExecuteCaptureAbi.HasTransposeCaptureResult;
    public const bool HasPhase09DeterministicExceptionCapture = MatrixTileExecuteCaptureAbi.HasDeterministicExceptionCapture;
    public const bool HasPhase09MemoryFaultCapture = MatrixTileExecuteCaptureAbi.HasMemoryFaultCapture;
    public const bool HasPhase09TileStateReadSnapshot = MatrixTileExecuteCaptureAbi.HasTileStateReadSnapshot;
    public const bool HasPhase09AccumulatorReadSnapshot = MatrixTileExecuteCaptureAbi.HasAccumulatorReadSnapshot;
    public const bool HasPhase09RetirePublication = MatrixTileExecuteCaptureAbi.HasRetirePublication;
    public const bool HasPhase09ReplayRollbackConformance = MatrixTileExecuteCaptureAbi.HasReplayRollbackConformance;
    public const bool Phase09BlocksCaptureToRetireBypass = MatrixTileExecuteCaptureAbi.BlocksCaptureToRetireBypass;
    public const bool Phase09KeepsRetirePublicationNonAuthority = MatrixTileExecuteCaptureAbi.KeepsRetirePublicationNonAuthority;
    public const bool Phase09KeepsReplayRollbackNonAuthority = MatrixTileExecuteCaptureAbi.KeepsReplayRollbackNonAuthority;
    public const bool Phase09KeepsVmxBackendFallbackNonAuthority = true;
    public const bool Phase09KeepsCompilerScopeClosed = MatrixTileExecuteCaptureAbi.KeepsCompilerScopeClosed;
    public const bool Phase09KeepsCompilerHandoffBlocked = MatrixTileExecuteCaptureAbi.KeepsCompilerHandoffBlocked;
    public const bool HasPhase10RetirePublicationCommit = MatrixTileRetirePublicationAbi.HasRetirePublicationAndCommit;
    public const bool HasPhase10TileLoadRetirePublication = MatrixTileRetirePublicationAbi.HasTileLoadRetirePublication;
    public const bool HasPhase10TileStoreRetireCommit = MatrixTileRetirePublicationAbi.HasTileStoreRetireCommit;
    public const bool HasPhase10AccumulatorRetirePublication = MatrixTileRetirePublicationAbi.HasAccumulatorRetirePublication;
    public const bool HasPhase10TransposeRetirePublication = MatrixTileRetirePublicationAbi.HasTransposeRetirePublication;
    public const bool HasPhase10FaultRetirementPolicy = MatrixTileRetirePublicationAbi.HasFaultRetirementPolicy;
    public const bool HasPhase10WritebackOwnership = MatrixTileRetirePublicationAbi.HasWritebackOwnership;
    public const bool HasPhase10SideEffectPublicationOrdering = MatrixTileRetirePublicationAbi.HasSideEffectPublicationOrdering;
    public const bool HasPhase10ArchitecturalStateVisibilityRules = MatrixTileRetirePublicationAbi.HasArchitecturalStateVisibilityRules;
    public const bool HasPhase10CaptureToRetireCorrelation = MatrixTileRetirePublicationAbi.HasCaptureToRetireCorrelation;
    public const bool Phase10RejectsDuplicateRetire = MatrixTileRetirePublicationAbi.RejectsDuplicateRetire;
    public const bool Phase10RejectsCancelledRetire = MatrixTileRetirePublicationAbi.RejectsCancelledRetire;
    public const bool Phase10BlocksExecuteCaptureToRetireBypass = MatrixTileRetirePublicationAbi.BlocksExecuteCaptureToRetireBypass;
    public const bool Phase10KeepsHostOwnedEvidenceNonArchitectural = MatrixTileRetirePublicationAbi.KeepsHostOwnedEvidenceNonArchitectural;
    public const bool Phase10KeepsVmxBackendFallbackNonAuthority = true;
    public const bool Phase10KeepsCompilerScopeClosed = MatrixTileRetirePublicationAbi.KeepsCompilerScopeClosed;
    public const bool Phase10KeepsCompilerHandoffBlocked = true;
    public const bool HasPhase11ReplayRollbackConformance = MatrixTileReplayRollbackAbi.HasReplayRollbackConformance;
    public const bool HasPhase11DecodedInstructionReplayIdentity = MatrixTileReplayRollbackAbi.HasDecodedInstructionReplayIdentity;
    public const bool HasPhase11TileDescriptorReplayIdentity = MatrixTileReplayRollbackAbi.HasTileDescriptorReplayIdentity;
    public const bool HasPhase11PendingTileWriteRollback = MatrixTileReplayRollbackAbi.HasPendingTileWriteRollback;
    public const bool HasPhase11PendingMemoryStoreRollback = MatrixTileReplayRollbackAbi.HasPendingMemoryStoreRollback;
    public const bool HasPhase11AccumulatorRollback = MatrixTileReplayRollbackAbi.HasAccumulatorRollback;
    public const bool HasPhase11DeterministicReplayAfterMemoryFault = MatrixTileReplayRollbackAbi.HasDeterministicReplayAfterMemoryFault;
    public const bool HasPhase11DeterministicReplayAfterDescriptorFault = MatrixTileReplayRollbackAbi.HasDeterministicReplayAfterDescriptorFault;
    public const bool HasPhase11LegalIllegalConformanceVectors = MatrixTileReplayRollbackAbi.HasLegalIllegalConformanceVectors;
    public const bool Phase11BlocksReplayWithoutRetirePublication = MatrixTileReplayRollbackAbi.BlocksReplayWithoutRetirePublication;
    public const bool Phase11BlocksCaptureRecordIdentityBypass = MatrixTileReplayRollbackAbi.BlocksCaptureRecordIdentityBypass;
    public const bool Phase11KeepsVmxBackendFallbackNonAuthority = true;
    public const bool Phase11KeepsCompilerScopeClosed = true;
    public const bool Phase11KeepsCompilerHandoffBlocked = true;
    public const bool HasPhase12PositiveExecutableGoldenArtifacts = MatrixTilePositiveGoldenArtifactManifest.HasPositiveExecutableGoldenArtifacts;
    public const bool HasPhase12RuntimeNoFallbackNoHiddenLoweringRegressionEvidence = MatrixTilePositiveGoldenArtifactManifest.HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence;
    public const bool HasPhase12DecodeEncodeGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasLegalDecodeEncodeRoundTripVectors;
    public const bool HasPhase12IrMaterializerGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasLegalIrMaterializerProjectionVectors;
    public const bool HasPhase12ExecuteRetireGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasLegalExecuteRetireVectors;
    public const bool HasPhase12MemoryFaultGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasMemoryFaultVectors;
    public const bool HasPhase12DescriptorFaultGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasDescriptorFaultVectors;
    public const bool HasPhase12AccumulatorGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasAccumulatorVectors;
    public const bool HasPhase12TransposeGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasTransposeVectors;
    public const bool HasPhase12ReplayRollbackGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasReplayRollbackVectors;
    public const bool HasPhase12ReservedRowGoldenVectors = MatrixTilePositiveGoldenArtifactManifest.HasNegativeReservedCarrierVectors;
    public const bool Phase12BlocksPositiveGoldenPublication = false;
    public const bool Phase12BlocksNoFallbackRegressionEvidenceBypass = true;
    public const bool Phase12KeepsVmxBackendFallbackNonAuthority = true;
    public const bool Phase12KeepsCompilerScopeClosed = true;
    public const bool Phase12KeepsCompilerHandoffBlocked = true;
    public const bool HasCanonicalDecoderAcceptance = true;
    public const bool HasEncoderAbi = true;
    public const bool HasInstructionIrTileProjection = true;
    public const bool HasRegistryFactory = true;
    public const bool HasMaterializerFactory = true;
    public const bool HasTypedTileMicroOp = true;
    public const bool HasSchedulerLaneBinding = true;
    public const bool HasExecutionCaptureSemantics = true;
    public const bool HasRetireWritebackOrSideEffectPublication = true;
    public const bool HasReplayRollbackConformance = true;
    public const bool HasGoldenArtifacts = MatrixTilePositiveGoldenArtifactManifest.HasPositiveExecutableGoldenArtifacts;
    public const bool TreatsLane6Dsc2Tile2DAsAuthority = false;
    public const bool TreatsLane7MatMulDescriptorAsAuthority = false;
    public const bool TreatsMemoryEaFallbackAsTileExecutionAuthority = false;
    public const bool TreatsInstructionClassifierAsExecutionAuthority = false;
    public const bool HasCompilerHandoffPackage = true;
    public const bool HasCurrentCompilerImplementation = MatrixTileCompilerEmissionHandoffPackage.CurrentCompilerImplementationExists;

    private static readonly MatrixTileRuntimeIsaPackageRow[] RowTable =
    [
        new(
            "MTILE_LOAD",
            IsTileMemory: true,
            IsLoad: true,
            IsStore: false,
            IsMacc: false,
            IsTranspose: false,
            RequiresTileMemoryShapeFaultModel: true,
            RequiresAccumulatorTileAbi: false,
            RequiresTransposeTilePolicyAbi: false,
            RequiresRetireStagedPublication: true,
            RequiresRetireStagedCommit: false,
            PrimaryBlockedGate: "NonePhase14Closed"),
        new(
            "MTILE_STORE",
            IsTileMemory: true,
            IsLoad: false,
            IsStore: true,
            IsMacc: false,
            IsTranspose: false,
            RequiresTileMemoryShapeFaultModel: true,
            RequiresAccumulatorTileAbi: false,
            RequiresTransposeTilePolicyAbi: false,
            RequiresRetireStagedPublication: false,
            RequiresRetireStagedCommit: true,
            PrimaryBlockedGate: "NonePhase14Closed"),
        new(
            "MTILE_MACC",
            IsTileMemory: false,
            IsLoad: false,
            IsStore: false,
            IsMacc: true,
            IsTranspose: false,
            RequiresTileMemoryShapeFaultModel: false,
            RequiresAccumulatorTileAbi: true,
            RequiresTransposeTilePolicyAbi: false,
            RequiresRetireStagedPublication: true,
            RequiresRetireStagedCommit: false,
            PrimaryBlockedGate: "NonePhase14Closed"),
        new(
            "MTRANSPOSE",
            IsTileMemory: false,
            IsLoad: false,
            IsStore: false,
            IsMacc: false,
            IsTranspose: true,
            RequiresTileMemoryShapeFaultModel: false,
            RequiresAccumulatorTileAbi: false,
            RequiresTransposeTilePolicyAbi: true,
            RequiresRetireStagedPublication: true,
            RequiresRetireStagedCommit: false,
            PrimaryBlockedGate: "NonePhase14Closed")
    ];

    public static MatrixTileRuntimeIsaPackageRow[] Rows =>
        (MatrixTileRuntimeIsaPackageRow[])RowTable.Clone();

    private static readonly MatrixTileOpcodeAuthorityRow[] OpcodeAuthorityRowTable =
    [
        new(
            "MTILE_LOAD",
            NumericOpcode: 216,
            HasPackageOpcodeIdentityAuthority: true,
            StatusCatalogAuthority: StatusCatalogAuthorityDecision,
            HasExecutableStatusCatalogPromotion: true,
            HasExecutableOpcodeAuthority: true,
            HasDescriptorAuthority: false,
            RequiresFutureAdr: false),
        new(
            "MTILE_STORE",
            NumericOpcode: 217,
            HasPackageOpcodeIdentityAuthority: true,
            StatusCatalogAuthority: StatusCatalogAuthorityDecision,
            HasExecutableStatusCatalogPromotion: true,
            HasExecutableOpcodeAuthority: true,
            HasDescriptorAuthority: false,
            RequiresFutureAdr: false),
        new(
            "MTILE_MACC",
            NumericOpcode: 218,
            HasPackageOpcodeIdentityAuthority: true,
            StatusCatalogAuthority: StatusCatalogAuthorityDecision,
            HasExecutableStatusCatalogPromotion: true,
            HasExecutableOpcodeAuthority: true,
            HasDescriptorAuthority: false,
            RequiresFutureAdr: false),
        new(
            "MTRANSPOSE",
            NumericOpcode: 219,
            HasPackageOpcodeIdentityAuthority: true,
            StatusCatalogAuthority: StatusCatalogAuthorityDecision,
            HasExecutableStatusCatalogPromotion: true,
            HasExecutableOpcodeAuthority: true,
            HasDescriptorAuthority: false,
            RequiresFutureAdr: false)
    ];

    public static MatrixTileOpcodeAuthorityRow[] OpcodeAuthorityRows =>
        (MatrixTileOpcodeAuthorityRow[])OpcodeAuthorityRowTable.Clone();

    public static MatrixTilePhase01AuthorityDecisionRow[] Phase01AuthorityDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            NumericOpcode: 216,
            RetainedNumericOpcodeRole: Phase01RetainedOpcodeRole,
            DescriptorAuthorityRole: Phase01DescriptorAuthorityRole,
            PackageFeatureRole: Phase01PackageFeatureRole,
            MemoryOrTransposePolicyDecision: Phase01MtileMemoryPolicyDecision,
            StatusCatalogPromotionRule: Phase01StatusCatalogPromotionRule,
            HasProductionAuthoritySource: true,
            BlocksPhase02: false),
        new(
            "MTILE_STORE",
            NumericOpcode: 217,
            RetainedNumericOpcodeRole: Phase01RetainedOpcodeRole,
            DescriptorAuthorityRole: Phase01DescriptorAuthorityRole,
            PackageFeatureRole: Phase01PackageFeatureRole,
            MemoryOrTransposePolicyDecision: Phase01MtileMemoryPolicyDecision,
            StatusCatalogPromotionRule: Phase01StatusCatalogPromotionRule,
            HasProductionAuthoritySource: true,
            BlocksPhase02: false),
        new(
            "MTILE_MACC",
            NumericOpcode: 218,
            RetainedNumericOpcodeRole: Phase01RetainedOpcodeRole,
            DescriptorAuthorityRole: Phase01DescriptorAuthorityRole,
            PackageFeatureRole: Phase01PackageFeatureRole,
            MemoryOrTransposePolicyDecision: AccumulatorTileAbiGateDecision,
            StatusCatalogPromotionRule: Phase01StatusCatalogPromotionRule,
            HasProductionAuthoritySource: true,
            BlocksPhase02: false),
        new(
            "MTRANSPOSE",
            NumericOpcode: 219,
            RetainedNumericOpcodeRole: Phase01RetainedOpcodeRole,
            DescriptorAuthorityRole: Phase01DescriptorAuthorityRole,
            PackageFeatureRole: Phase01PackageFeatureRole,
            MemoryOrTransposePolicyDecision: Phase01MtransposePolicyDecision,
            StatusCatalogPromotionRule: Phase01StatusCatalogPromotionRule,
            HasProductionAuthoritySource: true,
            BlocksPhase02: false)
    ];

    public static MatrixTilePhase02StateDescriptorDecisionRow[] Phase02StateDescriptorDecisionRows { get; } =
    [
        new(
            "architectural tile state owner",
            RequiredAbiSurface: "tile register namespace, lifetime, guest-visible contents, save/restore, replay/rollback owner",
            Decision: Phase02TileStateOwnerDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedAbi: true,
            HasCanonicalCarrier: true,
            BlocksPhase03: false),
        new(
            "canonical tile descriptor ABI",
            RequiredAbiSurface: "tile identity, rows, columns, element size, stride, layout, zero/invalid/reserved encodings",
            Decision: Phase02DescriptorCarrierDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedAbi: true,
            HasCanonicalCarrier: true,
            BlocksPhase03: false)
    ];

    public static MatrixTilePhase02InstructionDescriptorRequirementRow[] Phase02InstructionDescriptorRequirementRows { get; } =
    [
        new(
            "MTILE_LOAD",
            TileStateRequirement: Phase02TileStateOwnerDecision,
            DescriptorRequirement: Phase02DescriptorCarrierDecision,
            DescriptorValidationDecision: Phase02DescriptorValidationDecision,
            HasDescriptorRoundTripAbi: true,
            HasReservedDescriptorFailFastTests: true,
            KeepsExternalDescriptorsNonAuthority: true),
        new(
            "MTILE_STORE",
            TileStateRequirement: Phase02TileStateOwnerDecision,
            DescriptorRequirement: Phase02DescriptorCarrierDecision,
            DescriptorValidationDecision: Phase02DescriptorValidationDecision,
            HasDescriptorRoundTripAbi: true,
            HasReservedDescriptorFailFastTests: true,
            KeepsExternalDescriptorsNonAuthority: true),
        new(
            "MTILE_MACC",
            TileStateRequirement: Phase02TileStateOwnerDecision,
            DescriptorRequirement: Phase02DescriptorCarrierDecision,
            DescriptorValidationDecision: Phase02DescriptorValidationDecision,
            HasDescriptorRoundTripAbi: true,
            HasReservedDescriptorFailFastTests: true,
            KeepsExternalDescriptorsNonAuthority: true),
        new(
            "MTRANSPOSE",
            TileStateRequirement: Phase02TileStateOwnerDecision,
            DescriptorRequirement: Phase02DescriptorCarrierDecision,
            DescriptorValidationDecision: Phase02DescriptorValidationDecision,
            HasDescriptorRoundTripAbi: true,
            HasReservedDescriptorFailFastTests: true,
            KeepsExternalDescriptorsNonAuthority: true)
    ];

    public static MatrixTilePhase03MemoryFaultDecisionRow[] Phase03MemoryFaultDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            IsLoad: true,
            IsStore: false,
            RequiredAbiSurface: "effective address, tile descriptor shape, alignment, page crossing, partial-fault capture, retire publication, replay/rollback",
            Decision: Phase03MemoryShapeFaultDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasDeterministicShapeValidation: true,
            HasReplayableFaultModel: true,
            HasRetireOwnedSideEffectCommit: true,
            KeepsEaFallbackNonAuthority: true),
        new(
            "MTILE_STORE",
            IsLoad: false,
            IsStore: true,
            RequiredAbiSurface: "effective address, tile descriptor shape, alignment, page crossing, partial-fault capture, retire store commit, replay/rollback",
            Decision: Phase03MemoryShapeFaultDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasDeterministicShapeValidation: true,
            HasReplayableFaultModel: true,
            HasRetireOwnedSideEffectCommit: true,
            KeepsEaFallbackNonAuthority: true)
    ];

    public static MatrixTilePhase04SemanticAbiDecisionRow[] Phase04SemanticAbiDecisionRows { get; } =
    [
        new(
            "MTILE_MACC",
            RequiredAbiSurface: "source tile operands, accumulator operand, output ownership, shape compatibility, dtype widening, signedness, saturation/wrap/exception behavior, invalid shape behavior, retire/replay",
            Decision: Phase04MaccAccumulatorDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasShapeCompatibilityPolicy: true,
            HasElementTypePolicy: true,
            HasRetireReplayPolicy: true,
            KeepsFallbackNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredAbiSurface: "source and destination tile operands, in-place/out-of-place carrier, aliasing, layout permutation, shape transform, element preservation, reserved descriptor behavior, retire/replay",
            Decision: Phase04TransposeCarrierDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasShapeCompatibilityPolicy: true,
            HasElementTypePolicy: true,
            HasRetireReplayPolicy: true,
            KeepsFallbackNonAuthority: true)
    ];

    public static MatrixTilePhase05VlmDecisionRow[] Phase05VlmDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            RequiredVlmSurface: "instruction legality, XMatrix feature gate, element widths, tile shape contour, memory layout modes, disabled/reserved row behavior, decoder rejection",
            Decision: Phase05RuntimeOwnedVlmRowsDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedVlmRow: true,
            HasFeatureGate: true,
            HasElementWidthLegality: true,
            HasTileShapeLegality: true,
            HasMemoryLayoutLegality: true,
            HasReservedDisabledRow: true,
            KeepsMetadataNonAuthority: true,
            KeepsDecoderAdmissionFailClosed: true),
        new(
            "MTILE_STORE",
            RequiredVlmSurface: "instruction legality, XMatrix feature gate, element widths, tile shape contour, memory layout modes, disabled/reserved row behavior, decoder rejection",
            Decision: Phase05RuntimeOwnedVlmRowsDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedVlmRow: true,
            HasFeatureGate: true,
            HasElementWidthLegality: true,
            HasTileShapeLegality: true,
            HasMemoryLayoutLegality: true,
            HasReservedDisabledRow: true,
            KeepsMetadataNonAuthority: true,
            KeepsDecoderAdmissionFailClosed: true),
        new(
            "MTILE_MACC",
            RequiredVlmSurface: "instruction legality, XMatrix feature gate, element widths, tile shape contour, accumulator shape/dtype legality, disabled/reserved row behavior, decoder rejection",
            Decision: Phase05RuntimeOwnedVlmRowsDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedVlmRow: true,
            HasFeatureGate: true,
            HasElementWidthLegality: true,
            HasTileShapeLegality: true,
            HasMemoryLayoutLegality: true,
            HasReservedDisabledRow: true,
            KeepsMetadataNonAuthority: true,
            KeepsDecoderAdmissionFailClosed: true),
        new(
            "MTRANSPOSE",
            RequiredVlmSurface: "instruction legality, XMatrix feature gate, element widths, tile shape contour, transpose alias/layout legality, disabled/reserved row behavior, decoder rejection",
            Decision: Phase05RuntimeOwnedVlmRowsDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasRuntimeOwnedVlmRow: true,
            HasFeatureGate: true,
            HasElementWidthLegality: true,
            HasTileShapeLegality: true,
            HasMemoryLayoutLegality: true,
            HasReservedDisabledRow: true,
            KeepsMetadataNonAuthority: true,
            KeepsDecoderAdmissionFailClosed: true)
    ];

    public static MatrixTilePhase06DecoderEncoderDecisionRow[] Phase06DecoderEncoderDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            RequiredAbiSurface: "opcode or descriptor dispatch source, binary field layout, operand mapping, descriptor encoding and validation, reserved/malformed/disabled decode faults, encoder round-trip, package feature gate",
            Decision: Phase06DecoderEncoderDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasCanonicalDecoderAcceptance: true,
            HasEncoderRoundTripAbi: true,
            HasDescriptorDecodeValidation: true,
            HasReservedFieldFaultBehavior: true,
            KeepsIllegalRowsBeforeIrMaterializer: true,
            KeepsCompilerAcceptanceNonEvidence: true),
        new(
            "MTILE_STORE",
            RequiredAbiSurface: "opcode or descriptor dispatch source, binary field layout, operand mapping, descriptor encoding and validation, reserved/malformed/disabled decode faults, encoder round-trip, package feature gate",
            Decision: Phase06DecoderEncoderDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasCanonicalDecoderAcceptance: true,
            HasEncoderRoundTripAbi: true,
            HasDescriptorDecodeValidation: true,
            HasReservedFieldFaultBehavior: true,
            KeepsIllegalRowsBeforeIrMaterializer: true,
            KeepsCompilerAcceptanceNonEvidence: true),
        new(
            "MTILE_MACC",
            RequiredAbiSurface: "opcode or descriptor dispatch source, binary field layout, operand mapping, descriptor encoding and validation, reserved/malformed/disabled decode faults, encoder round-trip, package feature gate",
            Decision: Phase06DecoderEncoderDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasCanonicalDecoderAcceptance: true,
            HasEncoderRoundTripAbi: true,
            HasDescriptorDecodeValidation: true,
            HasReservedFieldFaultBehavior: true,
            KeepsIllegalRowsBeforeIrMaterializer: true,
            KeepsCompilerAcceptanceNonEvidence: true),
        new(
            "MTRANSPOSE",
            RequiredAbiSurface: "opcode or descriptor dispatch source, binary field layout, operand mapping, descriptor encoding and validation, reserved/malformed/disabled decode faults, encoder round-trip, package feature gate",
            Decision: Phase06DecoderEncoderDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasCanonicalDecoderAcceptance: true,
            HasEncoderRoundTripAbi: true,
            HasDescriptorDecodeValidation: true,
            HasReservedFieldFaultBehavior: true,
            KeepsIllegalRowsBeforeIrMaterializer: true,
            KeepsCompilerAcceptanceNonEvidence: true)
    ];

    public static MatrixTilePhase07IrMaterializerDecisionRow[] Phase07IrMaterializerDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            RequiredRuntimeSurface: "InstructionIR tile descriptor projection, load memory operand projection, registry entry, materializer factory, typed runtime object, descriptor validation result preservation",
            Decision: Phase07IrProjectionMaterializerDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasInstructionIrTileDescriptorProjection: true,
            HasMemoryOperandProjection: true,
            HasAccumulatorOperandProjection: false,
            HasTransposePolicyProjection: false,
            HasRegistryEntry: true,
            HasMaterializerFactory: true,
            HasMaterializedTypedRuntimeObject: true,
            PreservesDescriptorValidationResults: true,
            KeepsCompilerIrNonAuthority: true),
        new(
            "MTILE_STORE",
            RequiredRuntimeSurface: "InstructionIR tile descriptor projection, store memory operand projection, registry entry, materializer factory, typed runtime object, descriptor validation result preservation",
            Decision: Phase07IrProjectionMaterializerDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasInstructionIrTileDescriptorProjection: true,
            HasMemoryOperandProjection: true,
            HasAccumulatorOperandProjection: false,
            HasTransposePolicyProjection: false,
            HasRegistryEntry: true,
            HasMaterializerFactory: true,
            HasMaterializedTypedRuntimeObject: true,
            PreservesDescriptorValidationResults: true,
            KeepsCompilerIrNonAuthority: true),
        new(
            "MTILE_MACC",
            RequiredRuntimeSurface: "InstructionIR tile descriptor projection, accumulator operand projection, registry entry, materializer factory, typed runtime object, descriptor validation result preservation",
            Decision: Phase07IrProjectionMaterializerDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasInstructionIrTileDescriptorProjection: true,
            HasMemoryOperandProjection: false,
            HasAccumulatorOperandProjection: true,
            HasTransposePolicyProjection: false,
            HasRegistryEntry: true,
            HasMaterializerFactory: true,
            HasMaterializedTypedRuntimeObject: true,
            PreservesDescriptorValidationResults: true,
            KeepsCompilerIrNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredRuntimeSurface: "InstructionIR tile descriptor projection, transpose policy projection, registry entry, materializer factory, typed runtime object, descriptor validation result preservation",
            Decision: Phase07IrProjectionMaterializerDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            HasInstructionIrTileDescriptorProjection: true,
            HasMemoryOperandProjection: false,
            HasAccumulatorOperandProjection: false,
            HasTransposePolicyProjection: true,
            HasRegistryEntry: true,
            HasMaterializerFactory: true,
            HasMaterializedTypedRuntimeObject: true,
            PreservesDescriptorValidationResults: true,
            KeepsCompilerIrNonAuthority: true)
    ];

    public static MatrixTilePhase08MicroOpSchedulerDecisionRow[] Phase08MicroOpSchedulerDecisionRows { get; } =
    [
        new(
            "MTILE_LOAD",
            RequiredRuntimeSurface: "typed MTILE_LOAD MicroOp, canonical tile descriptor, tile memory dependency metadata, tile register write dependency metadata, scheduler lane binding, issue constraints, memory and tile-state capture barriers",
            Decision: Phase08TypedTileMicroOpSchedulerLaneDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            RequiresTileMemoryDependencyMetadata: true,
            RequiresTileRegisterDependencyMetadata: true,
            RequiresAccumulatorDependencyMetadata: false,
            HasTypedTileMicroOp: true,
            HasTileMemoryDependencyMetadata: true,
            HasTileRegisterDependencyMetadata: true,
            HasAccumulatorDependencyMetadata: false,
            HasSchedulerLaneBinding: true,
            HasIssueConstraints: true,
            HasCaptureBarriers: true,
            BlocksSchedulerMaterializerVlmBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_STORE",
            RequiredRuntimeSurface: "typed MTILE_STORE MicroOp, canonical tile descriptor, tile memory dependency metadata, tile register read dependency metadata, scheduler lane binding, issue constraints, memory and tile-state capture barriers",
            Decision: Phase08TypedTileMicroOpSchedulerLaneDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            RequiresTileMemoryDependencyMetadata: true,
            RequiresTileRegisterDependencyMetadata: true,
            RequiresAccumulatorDependencyMetadata: false,
            HasTypedTileMicroOp: true,
            HasTileMemoryDependencyMetadata: true,
            HasTileRegisterDependencyMetadata: true,
            HasAccumulatorDependencyMetadata: false,
            HasSchedulerLaneBinding: true,
            HasIssueConstraints: true,
            HasCaptureBarriers: true,
            BlocksSchedulerMaterializerVlmBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_MACC",
            RequiredRuntimeSurface: "typed MTILE_MACC MicroOp, canonical tile descriptor, tile register read/write dependency metadata, accumulator dependency metadata, scheduler lane binding, issue constraints, tile-state capture barriers",
            Decision: Phase08TypedTileMicroOpSchedulerLaneDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            RequiresTileMemoryDependencyMetadata: false,
            RequiresTileRegisterDependencyMetadata: true,
            RequiresAccumulatorDependencyMetadata: true,
            HasTypedTileMicroOp: true,
            HasTileMemoryDependencyMetadata: false,
            HasTileRegisterDependencyMetadata: true,
            HasAccumulatorDependencyMetadata: true,
            HasSchedulerLaneBinding: true,
            HasIssueConstraints: true,
            HasCaptureBarriers: true,
            BlocksSchedulerMaterializerVlmBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredRuntimeSurface: "typed MTRANSPOSE MicroOp, canonical tile descriptor, tile register read/write dependency metadata, transpose policy dependency metadata, scheduler lane binding, issue constraints, tile-state capture barriers",
            Decision: Phase08TypedTileMicroOpSchedulerLaneDecision,
            PrimaryBlocker: "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            RequiresTileMemoryDependencyMetadata: false,
            RequiresTileRegisterDependencyMetadata: true,
            RequiresAccumulatorDependencyMetadata: false,
            HasTypedTileMicroOp: true,
            HasTileMemoryDependencyMetadata: false,
            HasTileRegisterDependencyMetadata: true,
            HasAccumulatorDependencyMetadata: false,
            HasSchedulerLaneBinding: true,
            HasIssueConstraints: true,
            HasCaptureBarriers: true,
            BlocksSchedulerMaterializerVlmBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true)
    ];

    private static readonly MatrixTilePhase09ExecuteCaptureDecisionRow[] Phase09ExecuteCaptureDecisionRowTable =
    [
        new(
            "MTILE_LOAD",
            RequiredRuntimeSurface: "typed MTILE_LOAD MicroOp, tile load capture buffer, deterministic exception capture, memory fault capture, tile state read snapshot, retire-owned publication, replayable fault capture",
            Decision: Phase09ExecuteCaptureSemanticsDecision,
            PrimaryBlocker: "HistoricalPhase09BoundaryRetireReplayGoldenAndPromotionOpen",
            RequiresTileLoadCaptureBuffer: true,
            RequiresTileStorePendingWriteBuffer: false,
            RequiresMaccCaptureResult: false,
            RequiresTransposeCaptureResult: false,
            RequiresDeterministicExceptionCapture: true,
            RequiresMemoryFaultCapture: true,
            RequiresTileStateReadSnapshot: true,
            RequiresAccumulatorReadSnapshot: false,
            HasExecutionCaptureSemantics: true,
            HasTileLoadCaptureBuffer: true,
            HasTileStorePendingWriteBuffer: false,
            HasMaccCaptureResult: false,
            HasTransposeCaptureResult: false,
            HasDeterministicExceptionCapture: true,
            HasMemoryFaultCapture: true,
            HasTileStateReadSnapshot: true,
            HasAccumulatorReadSnapshot: false,
            HasRetirePublication: false,
            HasReplayRollbackConformance: false,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_STORE",
            RequiredRuntimeSurface: "typed MTILE_STORE MicroOp, tile store pending write buffer, deterministic exception capture, memory fault capture, tile state read snapshot, retire-owned publication, replayable fault capture",
            Decision: Phase09ExecuteCaptureSemanticsDecision,
            PrimaryBlocker: "HistoricalPhase09BoundaryRetireReplayGoldenAndPromotionOpen",
            RequiresTileLoadCaptureBuffer: false,
            RequiresTileStorePendingWriteBuffer: true,
            RequiresMaccCaptureResult: false,
            RequiresTransposeCaptureResult: false,
            RequiresDeterministicExceptionCapture: true,
            RequiresMemoryFaultCapture: true,
            RequiresTileStateReadSnapshot: true,
            RequiresAccumulatorReadSnapshot: false,
            HasExecutionCaptureSemantics: true,
            HasTileLoadCaptureBuffer: false,
            HasTileStorePendingWriteBuffer: true,
            HasMaccCaptureResult: false,
            HasTransposeCaptureResult: false,
            HasDeterministicExceptionCapture: true,
            HasMemoryFaultCapture: true,
            HasTileStateReadSnapshot: true,
            HasAccumulatorReadSnapshot: false,
            HasRetirePublication: false,
            HasReplayRollbackConformance: false,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_MACC",
            RequiredRuntimeSurface: "typed MTILE_MACC MicroOp, matrix MACC capture result, accumulator read snapshot, deterministic exception capture, memory fault capture, retire-owned publication, replayable fault capture",
            Decision: Phase09ExecuteCaptureSemanticsDecision,
            PrimaryBlocker: "HistoricalPhase09BoundaryRetireReplayGoldenAndPromotionOpen",
            RequiresTileLoadCaptureBuffer: false,
            RequiresTileStorePendingWriteBuffer: false,
            RequiresMaccCaptureResult: true,
            RequiresTransposeCaptureResult: false,
            RequiresDeterministicExceptionCapture: true,
            RequiresMemoryFaultCapture: true,
            RequiresTileStateReadSnapshot: true,
            RequiresAccumulatorReadSnapshot: true,
            HasExecutionCaptureSemantics: true,
            HasTileLoadCaptureBuffer: false,
            HasTileStorePendingWriteBuffer: false,
            HasMaccCaptureResult: true,
            HasTransposeCaptureResult: false,
            HasDeterministicExceptionCapture: true,
            HasMemoryFaultCapture: true,
            HasTileStateReadSnapshot: true,
            HasAccumulatorReadSnapshot: true,
            HasRetirePublication: false,
            HasReplayRollbackConformance: false,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredRuntimeSurface: "typed MTRANSPOSE MicroOp, matrix transpose capture result, tile state read snapshot, deterministic exception capture, memory fault capture, retire-owned publication, replayable fault capture",
            Decision: Phase09ExecuteCaptureSemanticsDecision,
            PrimaryBlocker: "HistoricalPhase09BoundaryRetireReplayGoldenAndPromotionOpen",
            RequiresTileLoadCaptureBuffer: false,
            RequiresTileStorePendingWriteBuffer: false,
            RequiresMaccCaptureResult: false,
            RequiresTransposeCaptureResult: true,
            RequiresDeterministicExceptionCapture: true,
            RequiresMemoryFaultCapture: true,
            RequiresTileStateReadSnapshot: true,
            RequiresAccumulatorReadSnapshot: false,
            HasExecutionCaptureSemantics: true,
            HasTileLoadCaptureBuffer: false,
            HasTileStorePendingWriteBuffer: false,
            HasMaccCaptureResult: false,
            HasTransposeCaptureResult: true,
            HasDeterministicExceptionCapture: true,
            HasMemoryFaultCapture: true,
            HasTileStateReadSnapshot: true,
            HasAccumulatorReadSnapshot: false,
            HasRetirePublication: false,
            HasReplayRollbackConformance: false,
            BlocksArchitecturalSideEffectsBeforeRetire: true,
            KeepsVmxBackendFallbackNonAuthority: true)
    ];

    public static MatrixTilePhase09ExecuteCaptureDecisionRow[] Phase09ExecuteCaptureDecisionRows =>
        (MatrixTilePhase09ExecuteCaptureDecisionRow[])Phase09ExecuteCaptureDecisionRowTable.Clone();

    private static readonly MatrixTilePhase10RetirePublicationDecisionRow[] Phase10RetirePublicationDecisionRowTable =
    [
        new(
            "MTILE_LOAD",
            RequiredRuntimeSurface: "retire-owned tile load publication path, fault retirement policy, writeback ownership, side-effect publication ordering, architectural tile-state visibility rules",
            Decision: Phase10RetirePublicationCommitDecision,
            PrimaryBlocker: "NonePhase10Closed",
            RequiresTileLoadRetirePublication: true,
            RequiresTileStoreRetireCommit: false,
            RequiresAccumulatorRetirePublication: false,
            RequiresTransposeRetirePublication: false,
            RequiresFaultRetirementPolicy: true,
            RequiresWritebackOwnership: true,
            RequiresSideEffectPublicationOrdering: true,
            RequiresArchitecturalStateVisibilityRules: true,
            HasRetirePublicationAndCommit: true,
            HasTileLoadRetirePublication: true,
            HasTileStoreRetireCommit: false,
            HasAccumulatorRetirePublication: false,
            HasTransposeRetirePublication: false,
            HasFaultRetirementPolicy: true,
            HasWritebackOwnership: true,
            HasSideEffectPublicationOrdering: true,
            HasArchitecturalStateVisibilityRules: true,
            BlocksExecuteCaptureToRetireBypass: true,
            KeepsHostOwnedEvidenceNonArchitectural: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_STORE",
            RequiredRuntimeSurface: "retire-owned tile store commit path, fault retirement policy, writeback ownership, side-effect publication ordering, architectural memory visibility rules",
            Decision: Phase10RetirePublicationCommitDecision,
            PrimaryBlocker: "NonePhase10Closed",
            RequiresTileLoadRetirePublication: false,
            RequiresTileStoreRetireCommit: true,
            RequiresAccumulatorRetirePublication: false,
            RequiresTransposeRetirePublication: false,
            RequiresFaultRetirementPolicy: true,
            RequiresWritebackOwnership: true,
            RequiresSideEffectPublicationOrdering: true,
            RequiresArchitecturalStateVisibilityRules: true,
            HasRetirePublicationAndCommit: true,
            HasTileLoadRetirePublication: false,
            HasTileStoreRetireCommit: true,
            HasAccumulatorRetirePublication: false,
            HasTransposeRetirePublication: false,
            HasFaultRetirementPolicy: true,
            HasWritebackOwnership: true,
            HasSideEffectPublicationOrdering: true,
            HasArchitecturalStateVisibilityRules: true,
            BlocksExecuteCaptureToRetireBypass: true,
            KeepsHostOwnedEvidenceNonArchitectural: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_MACC",
            RequiredRuntimeSurface: "retire-owned accumulator update publication path, fault retirement policy, writeback ownership, side-effect publication ordering, architectural accumulator visibility rules",
            Decision: Phase10RetirePublicationCommitDecision,
            PrimaryBlocker: "NonePhase10Closed",
            RequiresTileLoadRetirePublication: false,
            RequiresTileStoreRetireCommit: false,
            RequiresAccumulatorRetirePublication: true,
            RequiresTransposeRetirePublication: false,
            RequiresFaultRetirementPolicy: true,
            RequiresWritebackOwnership: true,
            RequiresSideEffectPublicationOrdering: true,
            RequiresArchitecturalStateVisibilityRules: true,
            HasRetirePublicationAndCommit: true,
            HasTileLoadRetirePublication: false,
            HasTileStoreRetireCommit: false,
            HasAccumulatorRetirePublication: true,
            HasTransposeRetirePublication: false,
            HasFaultRetirementPolicy: true,
            HasWritebackOwnership: true,
            HasSideEffectPublicationOrdering: true,
            HasArchitecturalStateVisibilityRules: true,
            BlocksExecuteCaptureToRetireBypass: true,
            KeepsHostOwnedEvidenceNonArchitectural: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredRuntimeSurface: "retire-owned transpose result publication path, fault retirement policy, writeback ownership, side-effect publication ordering, architectural tile-state visibility rules",
            Decision: Phase10RetirePublicationCommitDecision,
            PrimaryBlocker: "NonePhase10Closed",
            RequiresTileLoadRetirePublication: false,
            RequiresTileStoreRetireCommit: false,
            RequiresAccumulatorRetirePublication: false,
            RequiresTransposeRetirePublication: true,
            RequiresFaultRetirementPolicy: true,
            RequiresWritebackOwnership: true,
            RequiresSideEffectPublicationOrdering: true,
            RequiresArchitecturalStateVisibilityRules: true,
            HasRetirePublicationAndCommit: true,
            HasTileLoadRetirePublication: false,
            HasTileStoreRetireCommit: false,
            HasAccumulatorRetirePublication: false,
            HasTransposeRetirePublication: true,
            HasFaultRetirementPolicy: true,
            HasWritebackOwnership: true,
            HasSideEffectPublicationOrdering: true,
            HasArchitecturalStateVisibilityRules: true,
            BlocksExecuteCaptureToRetireBypass: true,
            KeepsHostOwnedEvidenceNonArchitectural: true,
            KeepsVmxBackendFallbackNonAuthority: true)
    ];

    public static MatrixTilePhase10RetirePublicationDecisionRow[] Phase10RetirePublicationDecisionRows =>
        (MatrixTilePhase10RetirePublicationDecisionRow[])Phase10RetirePublicationDecisionRowTable.Clone();

    private static readonly MatrixTilePhase11ReplayRollbackDecisionRow[] Phase11ReplayRollbackDecisionRowTable =
    [
        new(
            "MTILE_LOAD",
            RequiredRuntimeSurface: "decoded MTILE_LOAD replay identity, tile descriptor replay identity, pending tile write rollback, deterministic memory-fault replay, descriptor-fault replay, legal/illegal conformance vectors",
            Decision: Phase11ReplayRollbackConformanceDecision,
            PrimaryBlocker: "NonePhase11Closed",
            RequiresDecodedInstructionReplayIdentity: true,
            RequiresTileDescriptorReplayIdentity: true,
            RequiresPendingTileWriteRollback: true,
            RequiresPendingMemoryStoreRollback: false,
            RequiresAccumulatorRollback: false,
            RequiresDeterministicReplayAfterMemoryFault: true,
            RequiresDeterministicReplayAfterDescriptorFault: true,
            RequiresLegalIllegalConformanceVectors: true,
            HasReplayRollbackConformance: true,
            HasDecodedInstructionReplayIdentity: true,
            HasTileDescriptorReplayIdentity: true,
            HasPendingTileWriteRollback: true,
            HasPendingMemoryStoreRollback: false,
            HasAccumulatorRollback: false,
            HasDeterministicReplayAfterMemoryFault: true,
            HasDeterministicReplayAfterDescriptorFault: true,
            HasLegalIllegalConformanceVectors: true,
            BlocksReplayWithoutRetirePublication: true,
            BlocksCaptureRecordIdentityBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_STORE",
            RequiredRuntimeSurface: "decoded MTILE_STORE replay identity, tile descriptor replay identity, pending memory store rollback, deterministic memory-fault replay, descriptor-fault replay, legal/illegal conformance vectors",
            Decision: Phase11ReplayRollbackConformanceDecision,
            PrimaryBlocker: "NonePhase11Closed",
            RequiresDecodedInstructionReplayIdentity: true,
            RequiresTileDescriptorReplayIdentity: true,
            RequiresPendingTileWriteRollback: false,
            RequiresPendingMemoryStoreRollback: true,
            RequiresAccumulatorRollback: false,
            RequiresDeterministicReplayAfterMemoryFault: true,
            RequiresDeterministicReplayAfterDescriptorFault: true,
            RequiresLegalIllegalConformanceVectors: true,
            HasReplayRollbackConformance: true,
            HasDecodedInstructionReplayIdentity: true,
            HasTileDescriptorReplayIdentity: true,
            HasPendingTileWriteRollback: false,
            HasPendingMemoryStoreRollback: true,
            HasAccumulatorRollback: false,
            HasDeterministicReplayAfterMemoryFault: true,
            HasDeterministicReplayAfterDescriptorFault: true,
            HasLegalIllegalConformanceVectors: true,
            BlocksReplayWithoutRetirePublication: true,
            BlocksCaptureRecordIdentityBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTILE_MACC",
            RequiredRuntimeSurface: "decoded MTILE_MACC replay identity, tile descriptor replay identity, accumulator rollback, deterministic descriptor-fault replay, legal/illegal conformance vectors",
            Decision: Phase11ReplayRollbackConformanceDecision,
            PrimaryBlocker: "NonePhase11Closed",
            RequiresDecodedInstructionReplayIdentity: true,
            RequiresTileDescriptorReplayIdentity: true,
            RequiresPendingTileWriteRollback: false,
            RequiresPendingMemoryStoreRollback: false,
            RequiresAccumulatorRollback: true,
            RequiresDeterministicReplayAfterMemoryFault: false,
            RequiresDeterministicReplayAfterDescriptorFault: true,
            RequiresLegalIllegalConformanceVectors: true,
            HasReplayRollbackConformance: true,
            HasDecodedInstructionReplayIdentity: true,
            HasTileDescriptorReplayIdentity: true,
            HasPendingTileWriteRollback: false,
            HasPendingMemoryStoreRollback: false,
            HasAccumulatorRollback: true,
            HasDeterministicReplayAfterMemoryFault: false,
            HasDeterministicReplayAfterDescriptorFault: true,
            HasLegalIllegalConformanceVectors: true,
            BlocksReplayWithoutRetirePublication: true,
            BlocksCaptureRecordIdentityBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true),
        new(
            "MTRANSPOSE",
            RequiredRuntimeSurface: "decoded MTRANSPOSE replay identity, tile descriptor replay identity, pending tile write rollback, deterministic descriptor-fault replay, legal/illegal conformance vectors",
            Decision: Phase11ReplayRollbackConformanceDecision,
            PrimaryBlocker: "NonePhase11Closed",
            RequiresDecodedInstructionReplayIdentity: true,
            RequiresTileDescriptorReplayIdentity: true,
            RequiresPendingTileWriteRollback: true,
            RequiresPendingMemoryStoreRollback: false,
            RequiresAccumulatorRollback: false,
            RequiresDeterministicReplayAfterMemoryFault: false,
            RequiresDeterministicReplayAfterDescriptorFault: true,
            RequiresLegalIllegalConformanceVectors: true,
            HasReplayRollbackConformance: true,
            HasDecodedInstructionReplayIdentity: true,
            HasTileDescriptorReplayIdentity: true,
            HasPendingTileWriteRollback: true,
            HasPendingMemoryStoreRollback: false,
            HasAccumulatorRollback: false,
            HasDeterministicReplayAfterMemoryFault: false,
            HasDeterministicReplayAfterDescriptorFault: true,
            HasLegalIllegalConformanceVectors: true,
            BlocksReplayWithoutRetirePublication: true,
            BlocksCaptureRecordIdentityBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true)
    ];

    public static MatrixTilePhase11ReplayRollbackDecisionRow[] Phase11ReplayRollbackDecisionRows =>
        (MatrixTilePhase11ReplayRollbackDecisionRow[])Phase11ReplayRollbackDecisionRowTable.Clone();

    private static readonly MatrixTilePhase12GoldenArtifactDecisionRow[] Phase12GoldenArtifactDecisionRowTable =
    [
        new(
            "MTILE_LOAD",
            RequiredArtifactSurface: "positive decode/encode round-trip vectors, IR/materializer projection vectors, execute/retire vectors, memory-fault vectors, descriptor-fault vectors, replay/rollback vectors, reserved-row vectors, runtime no-fallback/no-hidden-lowering evidence",
            Decision: Phase12PositiveExecutableGoldenArtifactsDecision,
            NoFallbackDecision: Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision,
            PrimaryBlocker: "NonePhase12Closed",
            RequiresLegalDecodeEncodeRoundTripVectors: true,
            RequiresLegalIrMaterializerProjectionVectors: true,
            RequiresLegalExecuteRetireVectors: true,
            RequiresMemoryFaultVectors: true,
            RequiresDescriptorFaultVectors: true,
            RequiresAccumulatorVectors: false,
            RequiresTransposeVectors: false,
            RequiresReplayRollbackVectors: true,
            RequiresNegativeReservedVectors: true,
            RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasPositiveExecutableGoldenArtifacts: true,
            HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasLegalDecodeEncodeRoundTripVectors: true,
            HasLegalIrMaterializerProjectionVectors: true,
            HasLegalExecuteRetireVectors: true,
            HasMemoryFaultVectors: true,
            HasDescriptorFaultVectors: true,
            HasAccumulatorVectors: false,
            HasTransposeVectors: false,
            HasReplayRollbackVectors: true,
            HasNegativeReservedVectors: true,
            BlocksPositiveGoldenPublication: false,
            BlocksNoFallbackRegressionEvidenceBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true,
            KeepsCompilerScopeClosed: true,
            KeepsCompilerHandoffBlocked: true),
        new(
            "MTILE_STORE",
            RequiredArtifactSurface: "positive decode/encode round-trip vectors, IR/materializer projection vectors, execute/retire vectors, memory-fault vectors, descriptor-fault vectors, replay/rollback vectors, reserved-row vectors, runtime no-fallback/no-hidden-lowering evidence",
            Decision: Phase12PositiveExecutableGoldenArtifactsDecision,
            NoFallbackDecision: Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision,
            PrimaryBlocker: "NonePhase12Closed",
            RequiresLegalDecodeEncodeRoundTripVectors: true,
            RequiresLegalIrMaterializerProjectionVectors: true,
            RequiresLegalExecuteRetireVectors: true,
            RequiresMemoryFaultVectors: true,
            RequiresDescriptorFaultVectors: true,
            RequiresAccumulatorVectors: false,
            RequiresTransposeVectors: false,
            RequiresReplayRollbackVectors: true,
            RequiresNegativeReservedVectors: true,
            RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasPositiveExecutableGoldenArtifacts: true,
            HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasLegalDecodeEncodeRoundTripVectors: true,
            HasLegalIrMaterializerProjectionVectors: true,
            HasLegalExecuteRetireVectors: true,
            HasMemoryFaultVectors: true,
            HasDescriptorFaultVectors: true,
            HasAccumulatorVectors: false,
            HasTransposeVectors: false,
            HasReplayRollbackVectors: true,
            HasNegativeReservedVectors: true,
            BlocksPositiveGoldenPublication: false,
            BlocksNoFallbackRegressionEvidenceBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true,
            KeepsCompilerScopeClosed: true,
            KeepsCompilerHandoffBlocked: true),
        new(
            "MTILE_MACC",
            RequiredArtifactSurface: "positive decode/encode round-trip vectors, IR/materializer projection vectors, execute/retire vectors, accumulator vectors, descriptor-fault vectors, replay/rollback vectors, reserved-row vectors, runtime no-fallback/no-hidden-lowering evidence",
            Decision: Phase12PositiveExecutableGoldenArtifactsDecision,
            NoFallbackDecision: Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision,
            PrimaryBlocker: "NonePhase12Closed",
            RequiresLegalDecodeEncodeRoundTripVectors: true,
            RequiresLegalIrMaterializerProjectionVectors: true,
            RequiresLegalExecuteRetireVectors: true,
            RequiresMemoryFaultVectors: false,
            RequiresDescriptorFaultVectors: true,
            RequiresAccumulatorVectors: true,
            RequiresTransposeVectors: false,
            RequiresReplayRollbackVectors: true,
            RequiresNegativeReservedVectors: true,
            RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasPositiveExecutableGoldenArtifacts: true,
            HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasLegalDecodeEncodeRoundTripVectors: true,
            HasLegalIrMaterializerProjectionVectors: true,
            HasLegalExecuteRetireVectors: true,
            HasMemoryFaultVectors: false,
            HasDescriptorFaultVectors: true,
            HasAccumulatorVectors: true,
            HasTransposeVectors: false,
            HasReplayRollbackVectors: true,
            HasNegativeReservedVectors: true,
            BlocksPositiveGoldenPublication: false,
            BlocksNoFallbackRegressionEvidenceBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true,
            KeepsCompilerScopeClosed: true,
            KeepsCompilerHandoffBlocked: true),
        new(
            "MTRANSPOSE",
            RequiredArtifactSurface: "positive decode/encode round-trip vectors, IR/materializer projection vectors, execute/retire vectors, transpose vectors, descriptor-fault vectors, replay/rollback vectors, reserved-row vectors, runtime no-fallback/no-hidden-lowering evidence",
            Decision: Phase12PositiveExecutableGoldenArtifactsDecision,
            NoFallbackDecision: Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision,
            PrimaryBlocker: "NonePhase12Closed",
            RequiresLegalDecodeEncodeRoundTripVectors: true,
            RequiresLegalIrMaterializerProjectionVectors: true,
            RequiresLegalExecuteRetireVectors: true,
            RequiresMemoryFaultVectors: false,
            RequiresDescriptorFaultVectors: true,
            RequiresAccumulatorVectors: false,
            RequiresTransposeVectors: true,
            RequiresReplayRollbackVectors: true,
            RequiresNegativeReservedVectors: true,
            RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasPositiveExecutableGoldenArtifacts: true,
            HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence: true,
            HasLegalDecodeEncodeRoundTripVectors: true,
            HasLegalIrMaterializerProjectionVectors: true,
            HasLegalExecuteRetireVectors: true,
            HasMemoryFaultVectors: false,
            HasDescriptorFaultVectors: true,
            HasAccumulatorVectors: false,
            HasTransposeVectors: true,
            HasReplayRollbackVectors: true,
            HasNegativeReservedVectors: true,
            BlocksPositiveGoldenPublication: false,
            BlocksNoFallbackRegressionEvidenceBypass: true,
            KeepsVmxBackendFallbackNonAuthority: true,
            KeepsCompilerScopeClosed: true,
            KeepsCompilerHandoffBlocked: true)
    ];

    public static MatrixTilePhase12GoldenArtifactDecisionRow[] Phase12GoldenArtifactDecisionRows =>
        (MatrixTilePhase12GoldenArtifactDecisionRow[])Phase12GoldenArtifactDecisionRowTable.Clone();

    public static string[] RequiredExecutableEvidenceChain { get; } =
    [
        "CAT",
        "OP",
        "DEC",
        "IR",
        "MAT",
        "OBJ",
        "UOP",
        "SCH",
        "RSC",
        "EXE",
        "RET",
        "RPL",
        "TST",
        "GLD",
        "NOE",
        "HND"
    ];

    public static string[] RequestedClosureGates { get; } =
    [
        "tile execution model",
        "tile descriptor ABI",
        "memory-shape/fault model",
        "accumulator tile ABI",
        "transpose policy ABI",
        "VLM closure",
        "decoder/encoder ABI",
        "IR projection",
        "materializer",
        "typed tile MicroOp",
        "scheduler lane binding",
        "tile-stream resource contour",
        "execute/capture",
        "retire",
        "replay/rollback",
        "runtime/ISA conformance tests",
        "golden artifacts",
        "runtime no-fallback/no-hidden-lowering regression evidence",
        "positive compiler emission handoff package"
    ];

    public static string[] BlockedProductionGates { get; } =
    [
        "NoHostOwnedEvidencePublication",
        "NoLane6DscFallback",
        "NoGenericStreamEngineExecutionAuthority",
        "NoLane7Fallback",
        "NoExternalBackendFallback",
        "NoVmxSpecificPath"
    ];

    public static string[] TileExecutionModelBlockers { get; } =
    [
    ];

    public static string[] TileDescriptorAbiBlockers { get; } =
    [
    ];

    public static string[] MemoryFaultModelBlockers { get; } =
    [
    ];

    public static string[] AccumulatorTileAbiBlockers { get; } =
    [
    ];

    public static string[] TransposePolicyAbiBlockers { get; } =
    [
    ];

    public static string[] VectorLegalityMatrixBlockers { get; } =
    [
    ];

    public static string[] MicroOpSchedulerBlockers { get; } =
    [
    ];

    private static readonly MatrixTileRuntimeIsaClosureGateRow[] ClosureGateRowTable =
    [
        new(
            "status/catalog promotion",
            "CAT",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileCompilerEmissionHandoffPackage.StatusCatalogDecision),
        new(
            "opcode/descriptor authority",
            "OP",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: "ClosedExecutablePackageOpcodeAuthority"),
        new(
            "tile state/descriptor ABI",
            "ABI",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: "ClosedRuntimeOwnedTileStateAndDescriptorAbi"),
        new(
            "memory-shape/fault model",
            "ABI",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: "ClosedTileMemoryShapeAndFaultAbi"),
        new(
            "accumulator/transpose ABI",
            "ABI",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: "ClosedAccumulatorAndTransposeSemanticAbi"),
        new(
            "VLM rows",
            "VLM",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileRuntimeOwnedVlmRows.RuntimeOwnedVlmRowsDecision),
        new(
            "decoder/encoder",
            "DEC",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: Phase06DecoderEncoderDecision),
        new(
            "IR projection",
            "IR",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileIrProjectionAndMaterializer.TileDescriptorProjectionDecision),
        new(
            "materializer",
            "MAT",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileIrProjectionAndMaterializer.MaterializerFactoryDecision),
        new(
            "typed tile MicroOp",
            "UOP",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: TypedTileMicroOpGateDecision),
        new(
            "scheduler lane",
            "SCH",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: SchedulerLaneBindingGateDecision),
        new(
            "tile-stream resource contour",
            "RSC",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: Phase14ReadinessDecision),
        new(
            "execute/capture",
            "EXE",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileExecuteCaptureAbi.ExecuteCaptureDecision),
        new(
            "retire",
            "RET",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileRetirePublicationAbi.RetirePublicationDecision),
        new(
            "replay/rollback",
            "RPL",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileReplayRollbackAbi.ReplayRollbackDecision),
        new(
            "runtime/ISA conformance tests",
            "TST",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: "ClosedMatrixTilePhase11ConformanceVectors"),
        new(
            "golden artifacts",
            "GLD",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTilePositiveGoldenArtifactManifest.ManifestDecision),
        new(
            "runtime no-fallback/no-hidden-lowering regression evidence",
            "NOE",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileNoFallbackEvidenceContract.EvidenceDecision),
        new(
            "positive compiler emission handoff package",
            "HND",
            IsExecutableClosure: true,
            BlocksCompilerUpdate: false,
            Blocker: MatrixTileCompilerEmissionHandoffPackage.HandoffDecision)
    ];

    public static MatrixTileRuntimeIsaClosureGateRow[] ClosureGateRows =>
        (MatrixTileRuntimeIsaClosureGateRow[])ClosureGateRowTable.Clone();

    public static string[] PositiveGoldenPublicationPrerequisites { get; } =
    [
        "ArchitecturalTileStateOwner",
        "CanonicalTileDescriptorAbi",
        "MemoryShapeAndPartialFaultModel",
        "AccumulatorTileAbi",
        "TransposePolicyAbi",
        "RuntimeOwnedVlmRows",
        "DecoderEncoderAbi",
        "InstructionIrTileProjection",
        "RegistryMaterializerAuthority",
        "TypedTileMicroOp",
        "SchedulerLaneBinding",
        "ExecutionCaptureSemantics",
        "RetireOwnedPublicationOrCommit",
        "ReplayRollbackConformance",
        "RuntimeNoFallbackNoHiddenLoweringRegressionEvidence",
        "ExecutableGoldenVectors"
    ];

    public static MatrixTileRemainingRuntimeIsaTaskRow[] RemainingRuntimeIsaOpenTasks { get; } =
    [
    ];

    public static string[] ClosedAsBlockedRuntimeIsaTaskPools { get; } =
    [
        "TileStateAndDescriptorAbiAudit",
        "Phase02TileStateAndDescriptorAbi",
        "Phase03TileMemoryShapeFaultAbi",
        "Phase04AccumulatorTransposeSemanticAbi",
        "AccumulatorAndTransposeAbiAudit",
        "Phase05RuntimeOwnedMatrixTileVlmRows",
        "Phase06DecoderEncoderAbi",
        "Phase07IrProjectionAndMaterializer",
        "VectorLegalityMatrixDescriptorOnlyAudit",
        "DecoderIrMaterializerUopFailClosedAudit",
        "TypedTileMicroOpSchedulerLaneAudit",
        "Phase09ExecuteCaptureSemantics",
        "Phase10RetirePublicationAndCommit",
        "Phase11ReplayRollbackConformance",
        "Phase12PositiveExecutableGoldenArtifacts",
        "Phase12RuntimeNoFallbackNoHiddenLoweringEvidence",
        "ExecuteRetireReplayFailClosedAudit",
        "NegativeGoldenManifest",
        "PositiveGoldenArtifactManifest",
        "Phase01OpcodeIdentityAuthorityAdr",
        "CompilerVisibleNoEmissionBoundaryHandoff",
        "Phase13PositiveStatusCatalogPromotion",
        "Phase13PositiveCompilerEmissionHandoffPackage",
        "Phase14TileStreamResourceContourCorrection"
    ];

    public static string[] ExternalEvidenceNonAuthoritySources { get; } =
    [
        "Lane6Dsc2Tile2DParser",
        "Lane7MatMulDescriptor",
        "Lane7AcceleratorTopology",
        "VectorTranspose",
        "VectorSegmentMemory",
        "ScopedVdotWide"
    ];

    public static string[] RuntimePipelineBlockers { get; } =
    [
    ];

    public static MatrixTileRuntimeIsaPackageRow GetRow(string mnemonic)
    {
        foreach (MatrixTileRuntimeIsaPackageRow row in RowTable)
        {
            if (string.Equals(row.Mnemonic, mnemonic, System.StringComparison.Ordinal))
            {
                return row;
            }
        }

        throw new System.ArgumentOutOfRangeException(
            nameof(mnemonic),
            mnemonic,
            "Unknown matrix/tile runtime ISA package row.");
    }

    public static bool ContainsRow(string mnemonic)
    {
        foreach (MatrixTileRuntimeIsaPackageRow row in RowTable)
        {
            if (string.Equals(row.Mnemonic, mnemonic, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static void RequireExecutableAuthority(string mnemonic)
    {
        GetRow(mnemonic);
        MatrixTileCompilerEmissionHandoffPackage.RequireRuntimeExecutableAuthority(mnemonic);
    }

    public static void RequireCompilerUpdateReadiness()
    {
        RequirePositiveCompilerEmissionReadiness();
    }

    public static void RequirePositiveCompilerEmissionReadiness()
    {
        if (!IsReadyForPositiveCompilerEmissionHandoff ||
            !HasPositiveCompilerEmissionHandoffPackage ||
            BlocksPositiveCompilerEmission)
        {
            throw new System.InvalidOperationException(
                $"{PackageName} is not ready for positive compiler emission scope: {CompilerUpdateReadinessDecision}.");
        }
    }

    public static void RequireCompilerVisibleNoEmissionBoundaryReadiness()
    {
        if (!IsReadyForCompilerVisibleNoEmissionBoundary)
        {
            throw new System.InvalidOperationException(
                $"{PackageName} is not ready for compiler-visible no-emission boundary: {CompilerNoEmissionBoundaryDecision}.");
        }
    }
}
