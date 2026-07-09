using System;
using System.IO;
using System.Linq;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileRuntimeIsaPackageContractTests
{
    private static readonly string[] MatrixTileMnemonics =
    [
        "MTILE_LOAD",
        "MTILE_STORE",
        "MTILE_MACC",
        "MTRANSPOSE"
    ];

    [Fact]
    public void MatrixTileRuntimeIsaPackage_RecordsAllPhase14ExecutableRows()
    {
        Assert.Equal("Phase14", MatrixTileRuntimeIsaPackageContract.Phase);
        Assert.Equal("MatrixTileRuntimeIsaPackage", MatrixTileRuntimeIsaPackageContract.PackageName);
        Assert.Equal("MatrixTileRuntimeExecutableAuthority", MatrixTileRuntimeIsaPackageContract.EvidenceBoundary);
        Assert.Equal("XMatrix", MatrixTileRuntimeIsaPackageContract.ExtensionName);
        Assert.Equal("GenericRuntimeOnly", MatrixTileRuntimeIsaPackageContract.VmxBoundary);

        Assert.Equal(
            MatrixTileMnemonics.Order(StringComparer.Ordinal),
            MatrixTileRuntimeIsaPackageContract.Rows
                .Select(static row => row.Mnemonic)
                .Order(StringComparer.Ordinal));

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            Assert.True(MatrixTileRuntimeIsaPackageContract.ContainsRow(mnemonic));

            MatrixTileRuntimeIsaPackageRow row =
                MatrixTileRuntimeIsaPackageContract.GetRow(mnemonic);

            InstructionSupportStatus status =
                InstructionSupportStatusCatalog.GetStatus(mnemonic);

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("XMatrix", status.ExtensionName);
            Assert.True(status.HasNumericOpcode);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.True(HasEnum(mnemonic));
            Assert.True(HasIsaOpcodeValue(mnemonic));
            Assert.True(HasRegistryMnemonic(mnemonic));
            Assert.Equal(mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.IsTileMemory);
            Assert.Equal(mnemonic == "MTILE_LOAD", row.IsLoad);
            Assert.Equal(mnemonic == "MTILE_STORE", row.IsStore);
            Assert.Equal(mnemonic == "MTILE_MACC", row.IsMacc);
            Assert.Equal(mnemonic == "MTRANSPOSE", row.IsTranspose);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_ClosesExecutableEvidenceAndCompilerHandoff()
    {
        Assert.Equal("2026-06-07", MatrixTileRuntimeIsaPackageContract.ClosureAttemptDate);
        Assert.Equal(
            "ExecutableClosureBlockedByMissingAbiRuntimeEvidence",
            MatrixTileRuntimeIsaPackageContract.ClosureAttemptDecision);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTileExecutionModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTileDescriptorAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesMemoryShapeFaultModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesAccumulatorTileAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTransposePolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesVectorLegalityMatrix);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesDecoderEncoderAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesInstructionIrProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesRegistryMaterializer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesSchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesExecuteCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesReplayRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensDecoderEncoderAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensInstructionIrProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensRegistryMaterializer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.PublishesTypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensSchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensExecutionCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensRetireWritebackOrSideEffects);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensReplayRollback);
        Assert.False(MatrixTileRuntimeIsaPackageContract.OpensCompilerHelper);
        Assert.False(MatrixTileRuntimeIsaPackageContract.OpensVmxSpecificPath);
        Assert.False(MatrixTileRuntimeIsaPackageContract.PublishesHostOwnedEvidence);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasArchitecturalTileRegisterFile);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasMemoryBackedTileStateModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCanonicalTileDescriptorCarrier);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTileLifetimeOwnershipPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTileElementTypePolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTileShapePolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTileMemoryAlignmentPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPartialFaultPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRetireOwnedTilePublicationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasAccumulatorTileAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasAccumulatorTileStateOwner);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasAccumulatorTileFootprintPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasAccumulatorDtypePolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasMaccShapeCompatibilityPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasMaccExceptionOrSaturationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTransposePolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTransposePolicyCarrier);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTransposeSourceDestinationAliasPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasInPlaceTransposePolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTransposeLayoutPermutationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasVectorLegalityMatrixRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasMatrixTileVlmContour);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForCompilerUpdate);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutableRuntimeIsaClosure);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksCompilerUpdate);
        Assert.False(MatrixTileRuntimeIsaPackageContract.IsReadyForCompilerVisibleNoEmissionBoundary);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCompilerVisibleNoEmissionBoundaryPackage);
        Assert.False(MatrixTileRuntimeIsaPackageContract.AllowsCompilerVisibleNoEmissionBoundaryWork);
        Assert.True(MatrixTileRuntimeIsaPackageContract.BlocksCompilerVisibleNoEmissionBoundary);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForPositiveCompilerEmissionHandoff);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPositiveCompilerEmissionHandoffPackage);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksPositiveCompilerEmission);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksCompilerHelperIrSelectionEmission);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase13ClosesCompilerVisibleNoEmissionBoundary);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase13KeepsPositiveCompilerEmissionBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase13KeepsCompilerCodeUnmodified);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasNegativeGoldenManifest);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPositiveExecutableGoldenVectors);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksPositiveGoldenPublication);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasClosableRuntimeIsaTaskWithoutAdr);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasRemainingRuntimeIsaOpenTasks);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutableStatusCatalogPromotion);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasOpcodeOrDescriptorAuthority);
        Assert.False(MatrixTileRuntimeIsaPackageContract.TreatsRetainedNumericOpcodeAsExecutableAuthority);
        Assert.False(MatrixTileRuntimeIsaPackageContract.RequiresAdrForOpcodeOrDescriptorAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCanonicalDecoderAcceptance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasEncoderAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasInstructionIrTileProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRegistryFactory);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasMaterializerFactory);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasSchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutionCaptureSemantics);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRetireWritebackOrSideEffectPublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasReplayRollbackConformance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasGoldenArtifacts);
        Assert.False(MatrixTileRuntimeIsaPackageContract.TreatsLane6Dsc2Tile2DAsAuthority);
        Assert.False(MatrixTileRuntimeIsaPackageContract.TreatsLane7MatMulDescriptorAsAuthority);
        Assert.False(MatrixTileRuntimeIsaPackageContract.TreatsMemoryEaFallbackAsTileExecutionAuthority);
        Assert.False(MatrixTileRuntimeIsaPackageContract.TreatsInstructionClassifierAsExecutionAuthority);
        Assert.Equal(
            "ClosedPhase14PositiveCompilerEmissionHandoffPackage",
            MatrixTileRuntimeIsaPackageContract.CompilerHandoffGateDecision);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCompilerHandoffPackage);

        foreach (string gate in new[]
                 {
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
                 })
        {
            Assert.Contains(gate, MatrixTileRuntimeIsaPackageContract.RequestedClosureGates);
        }

        Assert.DoesNotContain("StatusCatalogPromotion", MatrixTileRuntimeIsaPackageContract.BlockedProductionGates);
        Assert.DoesNotContain("NoPositiveCompilerEmissionHandoffPackage", MatrixTileRuntimeIsaPackageContract.BlockedProductionGates);

        Assert.DoesNotContain(
            "ExecutionCapture",
            MatrixTileRuntimeIsaPackageContract.BlockedProductionGates);

        Assert.Equal(
            new[] { "CAT", "OP", "DEC", "IR", "MAT", "OBJ", "UOP", "SCH", "RSC", "EXE", "RET", "RPL", "TST", "GLD", "NOE", "HND" },
            MatrixTileRuntimeIsaPackageContract.RequiredExecutableEvidenceChain);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_StatusCatalogAndOpcodeAuthorityAreExecutable()
    {
        Assert.Equal(
            "ClosedOptionalEnabledConformanceTestedStatusCatalogPromotion",
            MatrixTileRuntimeIsaPackageContract.StatusCatalogAuthorityDecision);
        Assert.Equal(
            "PackageOpcodesAreExecutableIdentityAuthorityWithinClosedRuntimeEvidenceChain",
            MatrixTileRuntimeIsaPackageContract.OpcodeDescriptorAuthorityDecision);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.OpcodeAuthorityRows.Select(static row => row.Mnemonic).ToArray());

        foreach (MatrixTileOpcodeAuthorityRow authority in MatrixTileRuntimeIsaPackageContract.OpcodeAuthorityRows)
        {
            InstructionsEnum opcode = Enum.Parse<InstructionsEnum>(authority.Mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(authority.Mnemonic);

            Assert.Equal(Convert.ToUInt16(opcode), authority.NumericOpcode);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.Equal("ClosedOptionalEnabledConformanceTestedStatusCatalogPromotion", authority.StatusCatalogAuthority);
            Assert.True(authority.HasPackageOpcodeIdentityAuthority);
            Assert.True(authority.HasExecutableStatusCatalogPromotion);
            Assert.True(authority.HasExecutableOpcodeAuthority);
            Assert.False(authority.HasDescriptorAuthority);
            Assert.False(authority.RequiresFutureAdr);
            Assert.True(status.IsExecutableClaim);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase01AuthorityAdr_ClosesOpcodeIdentityOnlyAndCatalogPromotionRemainsBlocked()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/01_authority_status_catalog_and_opcode_adr.md",
            MatrixTileRuntimeIsaPackageContract.Phase01AuthorityAdrPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase01AuthorityReviewDate);
        Assert.Equal(
            "ClosedRetainedNumericOpcodesArePackageOpcodeIdentityAuthority",
            MatrixTileRuntimeIsaPackageContract.Phase01AuthorityDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase01BlockingReason);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase01ProductionAuthoritySource);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase01AuthorityAdrBlocksPhase02);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase01KeepsStatusCatalogOptionalDisabled);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase01KeepsDecoderEncoderFailClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase01KeepsCompilerHandoffBlocked);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase01AuthorityDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase01AuthorityDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase01AuthorityDecisionRows)
        {
            InstructionsEnum opcode = Enum.Parse<InstructionsEnum>(row.Mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.Equal(Convert.ToUInt16(opcode), row.NumericOpcode);
            Assert.Equal(
                "PackageOpcodeIdentityAuthorityOnlyNoExecution",
                row.RetainedNumericOpcodeRole);
            Assert.Equal(
                "NoDescriptorOpTypeAuthorityOpened",
                row.DescriptorAuthorityRole);
            Assert.Equal(
                "SingleXMatrixDeclaredPackageFeatureDeferred",
                row.PackageFeatureRole);
            Assert.Equal(
                "PositiveStatusCatalogPromotionOnlyAfterFullRuntimeIsaEvidenceChainAndPhase13",
                row.StatusCatalogPromotionRule);
            Assert.True(row.HasProductionAuthoritySource);
            Assert.False(row.BlocksPhase02);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.IsExecutableClaim);
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase01Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase01AuthorityAdrPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase01Path))
        {
            return;
        }

        string phase01 = File.ReadAllText(phase01Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/opcode-authority-only",
                     "production package opcode identity",
                     "No descriptor op-type authority is opened",
                     "Positive status/catalog promotion is delayed until Phase 13",
                     "Phase 05 runtime-owned VLM",
                     "rows are closed",
                     "accumulator/transpose semantic ABI"
                 })
        {
            Assert.Contains(requiredText, phase01, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase02StateDescriptorAbi_ClosesGuestArchitecturalOwnerAndCanonicalCarrier()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/02_tile_state_and_descriptor_abi.md",
            MatrixTileRuntimeIsaPackageContract.Phase02StateDescriptorAbiPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase02ReviewDate);
        Assert.Equal(
            "ClosedGuestArchitecturalTileStateOwnerAndCanonicalDescriptorAbi",
            MatrixTileRuntimeIsaPackageContract.Phase02StateDescriptorDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase02BlockingReason);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase02ArchitecturalTileStateOwner);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase02CanonicalTileDescriptorAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase02RuntimeOwnedTileStateContract);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase02TileDescriptorValidationHelpers);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase02ReservedDescriptorFailFastTests);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase02KeepsExternalDescriptorsNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase02KeepsHostOwnedEvidenceNonArchitectural);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase02KeepsCompilerHandoffBlocked);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase02BlocksPhase03);

        Assert.Equal(
            new[] { "architectural tile state owner", "canonical tile descriptor ABI" },
            MatrixTileRuntimeIsaPackageContract.Phase02StateDescriptorDecisionRows
                .Select(static row => row.OpenPoolItem)
                .ToArray());

        foreach (MatrixTilePhase02StateDescriptorDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase02StateDescriptorDecisionRows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.RequiredAbiSurface), row.OpenPoolItem);
            Assert.False(string.IsNullOrWhiteSpace(row.Decision), row.OpenPoolItem);
            Assert.False(string.IsNullOrWhiteSpace(row.PrimaryBlocker), row.OpenPoolItem);
            Assert.True(row.HasRuntimeOwnedAbi, row.OpenPoolItem);
            Assert.True(row.HasCanonicalCarrier, row.OpenPoolItem);
            Assert.False(row.BlocksPhase03, row.OpenPoolItem);
        }

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase02InstructionDescriptorRequirementRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase02InstructionDescriptorRequirementRow row in MatrixTileRuntimeIsaPackageContract.Phase02InstructionDescriptorRequirementRows)
        {
            Assert.Equal(
                MatrixTileRuntimeIsaPackageContract.Phase02TileStateOwnerDecision,
                row.TileStateRequirement);
            Assert.Equal(
                MatrixTileRuntimeIsaPackageContract.Phase02DescriptorCarrierDecision,
                row.DescriptorRequirement);
            Assert.Equal(
                MatrixTileRuntimeIsaPackageContract.Phase02DescriptorValidationDecision,
                row.DescriptorValidationDecision);
            Assert.True(row.HasDescriptorRoundTripAbi, row.Mnemonic);
            Assert.True(row.HasReservedDescriptorFailFastTests, row.Mnemonic);
            Assert.True(row.KeepsExternalDescriptorsNonAuthority, row.Mnemonic);
        }

        Assert.Equal(
            "GuestArchitecturalTileRegisterFileOwnerSelected",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.TileStateOwnerDecision);
        Assert.Equal(
            "GuestArchitecturalTileRegisterFileLifetimeSelected",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.TileStateLifetimeDecision);
        Assert.Equal(
            "CanonicalMatrixTileDescriptorCarrier",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.DescriptorCarrierDecision);
        Assert.Equal(
            "RowsColumnsElementSizeStrideAndLayoutValidated",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.DescriptorValidationDecision);
        Assert.Equal(
            "ZeroAndReservedDescriptorsFailClosed",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.ReservedDescriptorDecision);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasArchitecturalTileStateOwner);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasCanonicalTileDescriptorAbi);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasRuntimeOwnedTileStateContract);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasTileDescriptorValidationHelpers);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasReservedDescriptorFailFastTests);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.IsGuestArchitecturalOwner(
            MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.OwnerKind));
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.HasCanonicalLifetimePolicy(
            MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.LifetimePolicy));
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.GuestVisible);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.HostOwnedEvidenceIsNonArchitectural);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.HasSelectedOwner);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.HasSelectedLifetimePolicy);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.HasCanonicalDescriptor);
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.IsCanonicalDescriptor(
            MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.Descriptor));
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.IsZeroDescriptor(
            MatrixTileCanonicalDescriptorAbi.Zero));
        Assert.True(MatrixTileArchitecturalTileStateAndDescriptorAbi.IsReservedDescriptor(
            MatrixTileCanonicalDescriptorAbi.Create(1, 0, 1, 1)));
        Assert.Equal(
            MatrixTileArchitecturalTileStateAndDescriptorAbi.DescriptorValidationDecision,
            MatrixTileArchitecturalTileStateAndDescriptorAbi.ValidateDescriptor(
                MatrixTileArchitecturalTileStateAndDescriptorAbi.GuestArchitecturalTileStateContract.Descriptor));
        Assert.Equal(
            "ZeroMatrixTileDescriptorEncodingRejected",
            MatrixTileArchitecturalTileStateAndDescriptorAbi.ValidateDescriptor(
                MatrixTileCanonicalDescriptorAbi.Zero));

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase02Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase02StateDescriptorAbiPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase02Path))
        {
            return;
        }

        string phase02 = File.ReadAllText(phase02Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-owned-owner-and-canonical-descriptor-abi",
                     "GuestArchitecturalTileRegisterFileOwnerSelected",
                     "CanonicalMatrixTileDescriptorCarrier",
                     "Phase 03 tile memory shape/fault",
                     "Phase 04 accumulator/transpose semantic ABI",
                     "Phase 05 runtime-owned VLM",
                     "rows are closed",
                     "zero and reserved descriptors fail closed"
                 })
        {
            Assert.Contains(requiredText, phase02, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase03MemoryShapeFaultAbi_ClosesLoadStoreMemoryAbi()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/03_memory_shape_and_fault_model.md",
            MatrixTileRuntimeIsaPackageContract.Phase03MemoryShapeFaultAbiPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase03ReviewDate);
        Assert.Equal(
            "ClosedTileMemoryShapeAndFaultAbi",
            MatrixTileRuntimeIsaPackageContract.Phase03MemoryShapeFaultDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase03BlockingReason);
        Assert.Equal(
            "MemoryEaFallbackIsNotTileMemoryExecutionAuthority",
            MatrixTileRuntimeIsaPackageContract.Phase03EaFallbackDecision);
        Assert.Equal(
            "ExplicitRuntimeTileMemoryOperandEaSelected",
            MatrixTileRuntimeIsaPackageContract.Phase03EffectiveAddressDecision);
        Assert.Equal(
            "DescriptorRowsColumnsElementStrideAndAddressOverflowValidated",
            MatrixTileRuntimeIsaPackageContract.Phase03ShapeValidationDecision);
        Assert.Equal(
            "PreciseRowColumnFaultPointAndReplayIdentitySelected",
            MatrixTileRuntimeIsaPackageContract.Phase03FaultReplayDecision);
        Assert.Equal(
            "RetireOwnedLoadPublicationAndStoreCommitSelected",
            MatrixTileRuntimeIsaPackageContract.Phase03SideEffectOwnerDecision);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03TileMemoryShapeFaultAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03EffectiveAddressSource);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03ShapeValidation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03AddressOverflowValidation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03AlignmentPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03PageCrossingPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03PartialFaultPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03MemoryOrderingPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03LoadStoreSideEffectOwner);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03RetireRollbackPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase03KeepsEaFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase03KeepsMemorySideEffectsUnopened);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase03KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesMemoryShapeFaultModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesExecuteCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensExecutionCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensRetireWritebackOrSideEffects);

        Assert.Equal(
            new[] { "MTILE_LOAD", "MTILE_STORE" },
            MatrixTileRuntimeIsaPackageContract.Phase03MemoryFaultDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase03MemoryFaultDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase03MemoryFaultDecisionRows)
        {
            Assert.Equal(row.Mnemonic == "MTILE_LOAD", row.IsLoad);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.IsStore);
            Assert.Equal("ClosedTileMemoryShapeAndFaultAbi", row.Decision);
            Assert.False(string.IsNullOrWhiteSpace(row.RequiredAbiSurface), row.Mnemonic);
            Assert.False(string.IsNullOrWhiteSpace(row.PrimaryBlocker), row.Mnemonic);
            Assert.True(row.HasDeterministicShapeValidation, row.Mnemonic);
            Assert.True(row.HasReplayableFaultModel, row.Mnemonic);
            Assert.True(row.HasRetireOwnedSideEffectCommit, row.Mnemonic);
            Assert.True(row.KeepsEaFallbackNonAuthority, row.Mnemonic);

            MatrixTileRuntimeIsaPackageRow packageRow =
                MatrixTileRuntimeIsaPackageContract.GetRow(row.Mnemonic);
            Assert.True(packageRow.IsTileMemory);
            Assert.True(packageRow.RequiresTileMemoryShapeFaultModel);
            Assert.Equal("NonePhase14Closed", packageRow.PrimaryBlockedGate);
        }

        MatrixTileCanonicalDescriptorAbi descriptor =
            MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 16);
        MatrixTileMemoryShapeContract load =
            MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(descriptor, 0x1000UL);
        MatrixTileMemoryShapeValidationResult loadValidation =
            MatrixTileMemoryShapeAndFaultAbi.Validate(load);
        Assert.True(loadValidation.IsMemoryShapeAbiAccepted);
        Assert.Equal(MatrixTileMemoryFaultKind.None, loadValidation.FaultKind);
        Assert.Equal(0x1000UL, loadValidation.FirstByteAddress);
        Assert.Equal(0x101BUL, loadValidation.LastByteAddress);
        Assert.Equal(28UL, loadValidation.TotalByteFootprint);
        Assert.Equal(12U, loadValidation.RowByteCount);
        Assert.False(loadValidation.CrossesPageBoundary);
        Assert.Equal(
            MatrixTileMemoryPublicationPolicyKind.RetireStagedLoadPublication,
            loadValidation.PublicationPolicy);
        Assert.Equal(
            MatrixTileMemoryOrderingPolicyKind.RetireOrderedAllOrNone,
            loadValidation.OrderingPolicy);

        MatrixTileMemoryShapeContract crossingLoad =
            MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(descriptor, 0x0FF0UL);
        Assert.True(MatrixTileMemoryShapeAndFaultAbi.Validate(crossingLoad).CrossesPageBoundary);

        MatrixTileMemoryShapeContract store =
            MatrixTileMemoryShapeAndFaultAbi.CreateStoreContract(
                MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16),
                0x2000UL);
        MatrixTileMemoryShapeValidationResult storeValidation =
            MatrixTileMemoryShapeAndFaultAbi.Validate(store);
        Assert.True(storeValidation.IsMemoryShapeAbiAccepted);
        Assert.Equal(
            MatrixTileMemoryPublicationPolicyKind.RetireStagedStoreCommit,
            storeValidation.PublicationPolicy);

        MatrixTileMemoryShapeValidationResult partialFault =
            MatrixTileMemoryShapeAndFaultAbi.ProjectPartialMemoryFault(
                store,
                row: 1,
                column: 2,
                byteOffsetInElement: 3);
        Assert.False(partialFault.IsMemoryShapeAbiAccepted);
        Assert.Equal(MatrixTileMemoryFaultKind.PartialMemoryFault, partialFault.FaultKind);
        Assert.True(partialFault.HasFaultPoint);
        Assert.Equal((ushort)1, partialFault.FaultPoint.Row);
        Assert.Equal((ushort)2, partialFault.FaultPoint.Column);
        Assert.Equal(11U, partialFault.FaultPoint.ByteOffsetInRow);
        Assert.Equal(0x201BUL, partialFault.FaultPoint.Address);
        Assert.True(partialFault.FaultPoint.IsStore);

        Assert.Equal(
            MatrixTileMemoryFaultKind.ZeroDescriptor,
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Zero,
                    0x1000UL)).FaultKind);
        Assert.Equal(
            MatrixTileMemoryFaultKind.ReservedDescriptor,
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Create(1, 0, 4, 4),
                    0x1000UL)).FaultKind);
        Assert.Equal(
            MatrixTileMemoryFaultKind.UnsupportedElementSize,
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Create(1, 1, 3, 3),
                    0x1000UL)).FaultKind);
        Assert.Equal(
            MatrixTileMemoryFaultKind.StrideTooSmall,
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Create(1, 4, 4, 8),
                    0x1000UL)).FaultKind);
        MatrixTileMemoryShapeValidationResult alignmentFault =
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Create(1, 1, 4, 4),
                    0x1002UL));
        Assert.Equal(MatrixTileMemoryFaultKind.AlignmentFault, alignmentFault.FaultKind);
        Assert.True(alignmentFault.HasFaultPoint);
        Assert.Equal(0x1002UL, alignmentFault.FaultPoint.Address);
        Assert.Equal(
            MatrixTileMemoryFaultKind.AddressOverflow,
            MatrixTileMemoryShapeAndFaultAbi.Validate(
                    MatrixTileMemoryShapeAndFaultAbi.CreateStoreContract(
                        MatrixTileCanonicalDescriptorAbi.Create(2, 8, 8, 64),
                    ulong.MaxValue - 7)).FaultKind);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase03Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase03MemoryShapeFaultAbiPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase03Path))
        {
            return;
        }

        string phase03 = File.ReadAllText(phase03Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-owned-memory-shape-and-fault-abi",
                     "ClosedTileMemoryShapeAndFaultAbi",
                     "ExplicitRuntimeTileMemoryOperandEaSelected",
                     "DescriptorRowsColumnsElementStrideAndAddressOverflowValidated",
                     "PreciseRowColumnFaultPointAndReplayIdentitySelected",
                     "RetireOwnedLoadPublicationAndStoreCommitSelected",
                     "EA fallback remains non-authority",
                     "Phase 05 runtime-owned VLM",
                     "rows are closed"
                 })
        {
            Assert.Contains(requiredText, phase03, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase04AccumulatorTransposeAbi_ClosesSemanticAbi()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/04_accumulator_and_transpose_policy_abi.md",
            MatrixTileRuntimeIsaPackageContract.Phase04AccumulatorTransposeAbiPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase04ReviewDate);
        Assert.Equal(
            "ClosedAccumulatorAndTransposeSemanticAbi",
            MatrixTileRuntimeIsaPackageContract.Phase04AccumulatorTransposeDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase04BlockingReason);
        Assert.Equal(
            "ClosedAccumulatorTileAbi",
            MatrixTileRuntimeIsaPackageContract.Phase04MaccAccumulatorDecision);
        Assert.Equal(
            "MaccRowsKColumnsShapeCompatibilityValidated",
            MatrixTileRuntimeIsaPackageContract.Phase04MaccShapeDecision);
        Assert.Equal(
            "RetireOwnedAccumulatorPublicationAndReplayIdentitySelected",
            MatrixTileRuntimeIsaPackageContract.Phase04MaccRetireReplayDecision);
        Assert.Equal(
            "ClosedTransposePolicyAbi",
            MatrixTileRuntimeIsaPackageContract.Phase04TransposeCarrierDecision);
        Assert.Equal(
            "RowMajorTransposeShapePermutationSelected",
            MatrixTileRuntimeIsaPackageContract.Phase04TransposeLayoutDecision);
        Assert.Equal(
            "RetireOwnedTransposePublicationAndReplayIdentitySelected",
            MatrixTileRuntimeIsaPackageContract.Phase04TransposeRetireReplayDecision);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04AccumulatorTileAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04TransposePolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04AccumulatorFootprint);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04AccumulatorDtypePromotion);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04AccumulatorExceptionSaturationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04AccumulatorShapeCompatibility);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04TransposeSourceDestinationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04TransposeInPlaceAliasPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04TransposeLayoutPermutationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase04InvalidShapeTypeAliasDeterminism);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase04KeepsVectorTransposeNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase04KeepsExternalBackendNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase04KeepsCompilerMatrixIrIndependent);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase04KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesAccumulatorTileAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTransposePolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesExecuteCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensExecutionCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensRetireWritebackOrSideEffects);

        Assert.Equal(
            new[] { "MTILE_MACC", "MTRANSPOSE" },
            MatrixTileRuntimeIsaPackageContract.Phase04SemanticAbiDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase04SemanticAbiDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase04SemanticAbiDecisionRows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.RequiredAbiSurface), row.Mnemonic);
            Assert.False(string.IsNullOrWhiteSpace(row.Decision), row.Mnemonic);
            Assert.False(string.IsNullOrWhiteSpace(row.PrimaryBlocker), row.Mnemonic);
            Assert.True(row.HasShapeCompatibilityPolicy, row.Mnemonic);
            Assert.True(row.HasElementTypePolicy, row.Mnemonic);
            Assert.True(row.HasRetireReplayPolicy, row.Mnemonic);
            Assert.True(row.KeepsFallbackNonAuthority, row.Mnemonic);

            MatrixTileRuntimeIsaPackageRow packageRow =
                MatrixTileRuntimeIsaPackageContract.GetRow(row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", packageRow.IsMacc);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", packageRow.IsTranspose);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", packageRow.RequiresAccumulatorTileAbi);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", packageRow.RequiresTransposeTilePolicyAbi);
            Assert.Equal("NonePhase14Closed", packageRow.PrimaryBlockedGate);
        }

        MatrixTileMaccSemanticContract macc =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16));
        MatrixTileSemanticValidationResult maccResult =
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(macc);
        Assert.True(maccResult.IsSemanticAbiAccepted);
        Assert.Equal(MatrixTileSemanticFaultKind.None, maccResult.FaultKind);
        Assert.Equal((ushort)4, maccResult.ResultElementSizeBytes);
        Assert.Equal((ushort)2, maccResult.ResultDescriptor.Rows);
        Assert.Equal((ushort)4, maccResult.ResultDescriptor.Columns);
        Assert.True(maccResult.RequiresRetirePublication);
        Assert.True(maccResult.RequiresReplayIdentity);
        Assert.False(maccResult.UsesFallbackPath);
        Assert.Equal(
            (ushort)8,
            MatrixTileAccumulatorAndTransposePolicyAbi.GetAccumulatorElementSizeBytes(4));

        Assert.Equal(
            MatrixTileSemanticFaultKind.MaccInnerDimensionMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 2, 2, 4),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16))).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.MaccAccumulatorShapeMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 4, 16))).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.MaccAccumulatorElementSizeMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 2, 8))).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.UnsupportedElementKind,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(
                new MatrixTileMaccSemanticContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16),
                    MatrixTileNumericElementKind.Unspecified,
                    MatrixTileAccumulatorPolicyKind.WideningIntegerAccumulatorWithOverflowTrap)).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.ZeroDescriptor,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Zero,
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16))).FaultKind);

        MatrixTileTransposeSemanticContract transpose =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                MatrixTileCanonicalDescriptorAbi.Create(3, 2, 4, 8),
                sourceTileId: 1,
                destinationTileId: 2);
        MatrixTileSemanticValidationResult transposeResult =
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(transpose);
        Assert.True(transposeResult.IsSemanticAbiAccepted);
        Assert.Equal((ushort)3, transposeResult.ResultDescriptor.Rows);
        Assert.Equal((ushort)2, transposeResult.ResultDescriptor.Columns);
        Assert.True(transposeResult.RequiresRetirePublication);
        Assert.True(transposeResult.RequiresReplayIdentity);
        Assert.False(transposeResult.UsesFallbackPath);

        MatrixTileTransposeSemanticContract inPlaceTranspose =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                MatrixTileCanonicalDescriptorAbi.Create(3, 3, 4, 12),
                MatrixTileCanonicalDescriptorAbi.Create(3, 3, 4, 12),
                sourceTileId: 4,
                destinationTileId: 4);
        Assert.True(MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(inPlaceTranspose).IsSemanticAbiAccepted);

        Assert.Equal(
            MatrixTileSemanticFaultKind.TransposeShapeMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    sourceTileId: 1,
                    destinationTileId: 2)).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.TransposeElementSizeMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 2, 8, 16),
                    sourceTileId: 1,
                    destinationTileId: 2)).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.TransposeInPlaceRequiresSquareShape,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    sourceTileId: 7,
                    destinationTileId: 7)).FaultKind);
        Assert.Equal(
            MatrixTileSemanticFaultKind.TransposeInPlaceDescriptorMismatch,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(3, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 3, 4, 16),
                    sourceTileId: 7,
                    destinationTileId: 7)).FaultKind);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase04Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase04AccumulatorTransposeAbiPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase04Path))
        {
            return;
        }

        string phase04 = File.ReadAllText(phase04Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-owned-accumulator-and-transpose-semantic-abi",
                     "ClosedAccumulatorAndTransposeSemanticAbi",
                     "MaccRowsKColumnsShapeCompatibilityValidated",
                     "WideningIntegerAccumulatorDtypePolicySelected",
                     "OutOfPlaceOrSquareInPlaceAliasPolicySelected",
                     "RowMajorTransposeShapePermutationSelected",
                     "Phase 05 runtime-owned VLM",
                     "rows are closed",
                     "Vector transpose and external backend evidence remain non-authority"
                 })
        {
            Assert.Contains(requiredText, phase04, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase05RuntimeOwnedVlmRows_ClosesDescriptorOnlyLegalityRows()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/05_runtime_owned_vlm_rows.md",
            MatrixTileRuntimeIsaPackageContract.Phase05RuntimeOwnedVlmRowsPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase05ReviewDate);
        Assert.Equal(
            "ClosedRuntimeOwnedMatrixTileVlmRows",
            MatrixTileRuntimeIsaPackageContract.Phase05RuntimeOwnedVlmRowsDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase05BlockingReason);
        Assert.Equal(
            "XMatrixExtensionLegalityGateSelected",
            MatrixTileRuntimeIsaPackageContract.Phase05FeatureGateDecision);
        Assert.Equal(
            "MatrixTileElementWidths1_2_4_8Legal",
            MatrixTileRuntimeIsaPackageContract.Phase05ElementWidthDecision);
        Assert.Equal(
            "CanonicalTileDescriptorRowsColumnsStrideContourSelected",
            MatrixTileRuntimeIsaPackageContract.Phase05TileShapeDecision);
        Assert.Equal(
            "RowMajorTileMemoryLayoutContourSelected",
            MatrixTileRuntimeIsaPackageContract.Phase05MemoryLayoutDecision);
        Assert.Equal(
            "ReservedDisabledMatrixTileVlmContoursFailClosed",
            MatrixTileRuntimeIsaPackageContract.Phase05ReservedDisabledDecision);
        Assert.Equal(
            "ClassifierAndOptionalDisabledMetadataAreNotVlmAuthority",
            MatrixTileRuntimeIsaPackageContract.Phase05MetadataNonAuthorityDecision);
        Assert.Equal(
            "DecoderAdmissionOwnedByPhase06DecoderEncoderAbiNotVlmRows",
            MatrixTileRuntimeIsaPackageContract.Phase05DecoderAdmissionDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05RuntimeOwnedVlmRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05MatrixTileVlmFamily);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05DescriptorBackedOnlyContour);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05FeatureGate);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05ElementWidthLegality);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05TileShapeLegality);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05MemoryLayoutLegality);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05ReservedDisabledRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsNonDescriptorContoursFailClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsExecutableContoursClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsClassifierMetadataNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsOptionalDisabledStatusNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsDecoderAdmissionBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsCompilerEmissionIndependent);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsCompilerHandoffBlocked);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase05VlmDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase05VlmDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase05VlmDecisionRows)
        {
            var opcode = Enum.Parse<InstructionsEnum>(row.Mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredVlmSurface), row.Mnemonic);
            Assert.Equal("ClosedRuntimeOwnedMatrixTileVlmRows", row.Decision);
            Assert.Equal("Phase12ClosedStatusCatalogAndCompilerHandoffRemain", row.PrimaryBlocker);
            Assert.True(row.HasRuntimeOwnedVlmRow, row.Mnemonic);
            Assert.True(row.HasFeatureGate, row.Mnemonic);
            Assert.True(row.HasElementWidthLegality, row.Mnemonic);
            Assert.True(row.HasTileShapeLegality, row.Mnemonic);
            Assert.True(row.HasMemoryLayoutLegality, row.Mnemonic);
            Assert.True(row.HasReservedDisabledRow, row.Mnemonic);
            Assert.True(row.KeepsMetadataNonAuthority, row.Mnemonic);
            Assert.True(row.KeepsDecoderAdmissionFailClosed, row.Mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.True(status.IsExecutableClaim);
            AssertDescriptorOnlyMatrixTileVlmRow(opcode);
            Assert.True(VectorLegalityMatrix.TryGetAddressingStatus(
                opcode,
                indexed: false,
                is2D: false,
                out VectorContourLegalityStatus status2D));
            Assert.Equal(VectorContourLegalityStatus.FailClosed, status2D);
        }

        MatrixTileVlmLegalityValidationResult load =
            MatrixTileRuntimeOwnedVlmRows.ValidateLoad(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    baseAddress: 0x1000));
        Assert.True(load.IsLegal);
        Assert.Equal(InstructionsEnum.MTILE_LOAD, load.Opcode);
        Assert.Equal(MatrixTileVlmLegalityFaultKind.None, load.FaultKind);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, load.DescriptorBackedStatus);
        Assert.False(load.OpensDecoderAdmission);
        Assert.False(load.OpensExecution);
        Assert.False(load.UsesFallbackPath);

        MatrixTileVlmLegalityValidationResult store =
            MatrixTileRuntimeOwnedVlmRows.ValidateStore(
                MatrixTileMemoryShapeAndFaultAbi.CreateStoreContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    baseAddress: 0x1000));
        Assert.True(store.IsLegal);
        Assert.Equal(InstructionsEnum.MTILE_STORE, store.Opcode);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, store.DescriptorBackedStatus);
        Assert.False(store.OpensExecution);
        Assert.False(store.UsesFallbackPath);

        MatrixTileVlmLegalityValidationResult invalidLoad =
            MatrixTileRuntimeOwnedVlmRows.ValidateLoad(
                MatrixTileMemoryShapeAndFaultAbi.CreateLoadContract(
                    MatrixTileCanonicalDescriptorAbi.Zero,
                    baseAddress: 0x1000));
        Assert.False(invalidLoad.IsLegal);
        Assert.Equal(MatrixTileVlmLegalityFaultKind.MemoryShapeFault, invalidLoad.FaultKind);

        MatrixTileVlmLegalityValidationResult macc =
            MatrixTileRuntimeOwnedVlmRows.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 2, 6),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16)));
        Assert.True(macc.IsLegal);
        Assert.Equal(InstructionsEnum.MTILE_MACC, macc.Opcode);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, macc.DescriptorBackedStatus);
        Assert.False(macc.OpensExecution);
        Assert.False(macc.UsesFallbackPath);

        MatrixTileVlmLegalityValidationResult invalidMacc =
            MatrixTileRuntimeOwnedVlmRows.ValidateMacc(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 2, 2, 4),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 4, 2, 8),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 4, 4, 16)));
        Assert.False(invalidMacc.IsLegal);
        Assert.Equal(MatrixTileVlmLegalityFaultKind.MaccSemanticFault, invalidMacc.FaultKind);

        MatrixTileVlmLegalityValidationResult transpose =
            MatrixTileRuntimeOwnedVlmRows.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(3, 2, 4, 8),
                    sourceTileId: 1,
                    destinationTileId: 2));
        Assert.True(transpose.IsLegal);
        Assert.Equal(InstructionsEnum.MTRANSPOSE, transpose.Opcode);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, transpose.DescriptorBackedStatus);
        Assert.False(transpose.OpensExecution);
        Assert.False(transpose.UsesFallbackPath);

        MatrixTileVlmLegalityValidationResult invalidTranspose =
            MatrixTileRuntimeOwnedVlmRows.ValidateTranspose(
                MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    MatrixTileCanonicalDescriptorAbi.Create(2, 3, 4, 12),
                    sourceTileId: 1,
                    destinationTileId: 2));
        Assert.False(invalidTranspose.IsLegal);
        Assert.Equal(MatrixTileVlmLegalityFaultKind.TransposeSemanticFault, invalidTranspose.FaultKind);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase05Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase05RuntimeOwnedVlmRowsPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase05Path))
        {
            return;
        }

        string phase05 = File.ReadAllText(phase05Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-owned-descriptor-only-vlm-rows",
                     "ClosedRuntimeOwnedMatrixTileVlmRows",
                     "Descriptor-backed VLM legality",
                     "descriptor-only",
                     "Non-descriptor contours fail closed",
                     "Classifier and optional-disabled metadata",
                     "remain non-authority",
                     "VLM rows do not own decoder admission",
                     "Phase 06 decoder/encoder ABI now owns canonical decoder acceptance"
                 })
        {
            Assert.Contains(requiredText, phase05, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase06DecoderEncoderAbi_ClosesCanonicalDecoderEncoderAbi()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/06_decoder_encoder_abi.md",
            MatrixTileRuntimeIsaPackageContract.Phase06DecoderEncoderAbiPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase06ReviewDate);
        Assert.Equal(
            "ClosedMatrixTileDecoderEncoderAbi",
            MatrixTileRuntimeIsaPackageContract.Phase06DecoderEncoderDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase06BlockingReason);
        Assert.Equal(
            "OpcodeOrDescriptorDispatchSourceSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06DispatchSourceDecision);
        Assert.Equal(
            "CanonicalVectorCarrierBinaryFieldLayoutSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06BinaryLayoutDecision);
        Assert.Equal(
            "CanonicalVectorCarrierOperandMappingSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06OperandMappingDecision);
        Assert.Equal(
            "DescriptorEncodingAndValidationSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06DescriptorDecodeDecision);
        Assert.Equal(
            "ReservedMalformedDisabledDecodeFaultAbiSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06ReservedFieldDecision);
        Assert.Equal(
            "EncoderRoundTripAbiSelected",
            MatrixTileRuntimeIsaPackageContract.Phase06EncoderRoundTripDecision);
        Assert.Equal(
            "RuntimeOwnedVlmRowsSelectCanonicalMatrixTileDecode",
            MatrixTileRuntimeIsaPackageContract.Phase06FeatureGateDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06CanonicalDecoderAcceptance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06EncoderAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06BinaryFieldLayout);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06OperandFieldMapping);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06DescriptorDecodeValidation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06ReservedMalformedFaultBehavior);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06EncoderRoundTripTests);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06PackageFeatureDecodeGate);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase06KeepsIllegalRowsBeforeIrMaterializer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase06KeepsCompilerAcceptanceNonEvidence);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase06KeepsCompilerEmissionOutOfScope);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase06KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCanonicalDecoderAcceptance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasEncoderAbi);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase06DecoderEncoderDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase06DecoderEncoderDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase06DecoderEncoderDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredAbiSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTileDecoderEncoderAbi", row.Decision);
            Assert.Equal("Phase12ClosedStatusCatalogAndCompilerHandoffRemain", row.PrimaryBlocker);
            Assert.True(row.HasCanonicalDecoderAcceptance, row.Mnemonic);
            Assert.True(row.HasEncoderRoundTripAbi, row.Mnemonic);
            Assert.True(row.HasDescriptorDecodeValidation, row.Mnemonic);
            Assert.True(row.HasReservedFieldFaultBehavior, row.Mnemonic);
            Assert.True(row.KeepsIllegalRowsBeforeIrMaterializer, row.Mnemonic);
            Assert.True(row.KeepsCompilerAcceptanceNonEvidence, row.Mnemonic);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.True(HasRegistryMnemonic(row.Mnemonic));
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase06Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase06DecoderEncoderAbiPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase06Path))
        {
            return;
        }

        string phase06 = File.ReadAllText(phase06Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/decoder-encoder-abi",
                     "Decision: `ClosedMatrixTileDecoderEncoderAbi`",
                     "historical blocking reason moved forward",
                     "all downstream phases, including Phase 14 resource correction",
                     "Canonical vector-carrier binary field layout",
                     "Encoder round-trip",
                     "Illegal rows remain rejected before typed MicroOp/scheduler",
                     "Compiler acceptance is not used as evidence"
                 })
        {
            Assert.Contains(requiredText, phase06, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase07IrProjectionAndMaterializer_ClosesProjectionAndMaterializer()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/07_ir_projection_and_materializer.md",
            MatrixTileRuntimeIsaPackageContract.Phase07IrProjectionMaterializerPath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase07ReviewDate);
        Assert.Equal(
            "ClosedMatrixTileIrProjectionAndMaterializer",
            MatrixTileRuntimeIsaPackageContract.Phase07IrProjectionMaterializerDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase07BlockingReason);
        Assert.Equal(
            "ClosedInstructionIrTileDescriptorProjection",
            MatrixTileRuntimeIsaPackageContract.Phase07TileDescriptorProjectionDecision);
        Assert.Equal(
            "ClosedMatrixTileMemoryOperandProjection",
            MatrixTileRuntimeIsaPackageContract.Phase07MemoryOperandProjectionDecision);
        Assert.Equal(
            "ClosedMatrixTileAccumulatorOperandProjection",
            MatrixTileRuntimeIsaPackageContract.Phase07AccumulatorProjectionDecision);
        Assert.Equal(
            "ClosedMatrixTileTransposePolicyProjection",
            MatrixTileRuntimeIsaPackageContract.Phase07TransposeProjectionDecision);
        Assert.Equal(
            "ClosedMatrixTileRegistryEntries",
            MatrixTileRuntimeIsaPackageContract.Phase07RegistryEntryDecision);
        Assert.Equal(
            "ClosedMatrixTileMaterializerFactories",
            MatrixTileRuntimeIsaPackageContract.Phase07MaterializerFactoryDecision);
        Assert.Equal(
            "ClosedMaterializedTypedCloseToRtlInstructionObjects",
            MatrixTileRuntimeIsaPackageContract.Phase07TypedObjectDecision);
        Assert.Equal(
            "ClosedInvalidTileIrProjectionFaultAbi",
            MatrixTileRuntimeIsaPackageContract.Phase07InvalidIrDecision);
        Assert.Equal(
            "CompilerIrIsNotRuntimeProjectionEvidence",
            MatrixTileRuntimeIsaPackageContract.Phase07CompilerIrBoundaryDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07InstructionIrTileProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07TileDescriptorIrCarrier);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07MemoryOperandProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07AccumulatorOperandProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07TransposePolicyProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07RegistryEntries);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07MaterializerFactories);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07MaterializedTypedRuntimeObjects);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07DescriptorValidationResultPreservation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07InvalidIrProjectionFaults);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase07KeepsCompilerIrNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase07KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase07KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasInstructionIrTileProjection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRegistryFactory);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasMaterializerFactory);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase07IrMaterializerDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase07IrMaterializerDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase07IrMaterializerDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredRuntimeSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTileIrProjectionAndMaterializer", row.Decision);
            Assert.Equal("Phase12ClosedStatusCatalogAndCompilerHandoffRemain", row.PrimaryBlocker);
            Assert.True(row.HasInstructionIrTileDescriptorProjection, row.Mnemonic);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.HasMemoryOperandProjection);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasAccumulatorOperandProjection);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.HasTransposePolicyProjection);
            Assert.True(row.HasRegistryEntry, row.Mnemonic);
            Assert.True(row.HasMaterializerFactory, row.Mnemonic);
            Assert.True(row.HasMaterializedTypedRuntimeObject, row.Mnemonic);
            Assert.True(row.PreservesDescriptorValidationResults, row.Mnemonic);
            Assert.True(row.KeepsCompilerIrNonAuthority, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.True(HasRegistryMnemonic(row.Mnemonic));
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase07Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase07IrProjectionMaterializerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase07Path))
        {
            return;
        }

        string phase07 = File.ReadAllText(phase07Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/ir-projection-and-materializer",
                     "ClosedMatrixTileIrProjectionAndMaterializer",
                     "InstructionIR tile descriptor projection is opened",
                     "registry/materializer entries are opened",
                     "typed runtime objects are materialized",
                     "Invalid IR projections remain blocked before execution",
                     "Compiler IR is not runtime projection evidence"
                 })
        {
            Assert.Contains(requiredText, phase07, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.MTILE_LOAD, MatrixTileProjectedOperationKind.Load, true, false, false)]
    [InlineData(InstructionsEnum.MTILE_STORE, MatrixTileProjectedOperationKind.Store, true, false, false)]
    [InlineData(InstructionsEnum.MTILE_MACC, MatrixTileProjectedOperationKind.Macc, false, true, false)]
    [InlineData(InstructionsEnum.MTRANSPOSE, MatrixTileProjectedOperationKind.Transpose, false, false, true)]
    public void MatrixTileRuntimeIsaPackage_Phase07Materializer_ProducesTypedRuntimeObjectsWithoutMicroOp(
        InstructionsEnum opcode,
        MatrixTileProjectedOperationKind expectedOperation,
        bool expectsMemoryProjection,
        bool expectsAccumulatorProjection,
        bool expectsTransposeProjection)
    {
        var decoder = new VliwDecoderV4();
        VLIW_Instruction instruction = CreateLegalMatrixTileInstruction(opcode);
        InstructionIR ir = DecodeMatrixTileInstruction(
            decoder,
            instruction,
            opcode,
            bundleSerial: 7);

        Assert.True(ir.MatrixTileProjection.HasValue);
        MatrixTileInstructionIrProjection projection = ir.MatrixTileProjection.Value;
        Assert.Equal(opcode, projection.Opcode);
        Assert.Equal(expectedOperation, projection.OperationKind);
        Assert.True(projection.HasTileDescriptorProjection);
        Assert.Equal(expectsMemoryProjection, projection.HasMemoryOperandProjection);
        Assert.Equal(expectsAccumulatorProjection, projection.HasAccumulatorOperandProjection);
        Assert.Equal(expectsTransposeProjection, projection.HasTransposePolicyProjection);
        Assert.True(projection.PreservesDescriptorValidationResults);
        Assert.False(projection.UsesFallbackPath);
        Assert.False(projection.OpensExecution);
        Assert.True(projection.IsRuntimeLegal);
        Assert.Equal(MatrixTileIrProjectionFaultKind.None, projection.FaultKind);

        Assert.True(InstructionRegistry.IsMatrixTileMaterializerRegistered((uint)opcode));
        Assert.True(InstructionRegistry.IsRegistered((uint)opcode));
        Assert.True(InstructionRegistry.IsMatrixTileMicroOpRegistered((uint)opcode));
        Assert.True(InstructionRegistry.TryCreateMatrixTileRuntimeObject(
            ir,
            out MatrixTileMaterializedInstruction materialized,
            out MatrixTileIrProjectionFaultKind faultKind));
        Assert.Equal(MatrixTileIrProjectionFaultKind.None, faultKind);
        Assert.True(materialized.IsRuntimeLegal);
        Assert.True(materialized.IsTypedCloseToRtlRuntimeObject);
        Assert.False(materialized.PublishesTypedTileMicroOp);
        Assert.False(materialized.OpensExecution);
        Assert.False(materialized.UsesFallbackPath);

        Assert.Throws<DecodeProjectionFaultException>(
            () => InstructionRegistry.CreateMicroOp((uint)opcode, default));
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase07Materializer_FailsClosedForInvalidIrProjection()
    {
        InstructionIR missingPayload = new()
        {
            CanonicalOpcode = new Processor.CPU_Core.IsaOpcode((ushort)InstructionsEnum.MTILE_LOAD),
            Class = InstructionClass.Memory,
            SerializationClass = SerializationClass.Free,
            Rd = VLIW_Instruction.NoArchReg,
            Rs1 = VLIW_Instruction.NoArchReg,
            Rs2 = VLIW_Instruction.NoArchReg,
            Imm = 0
        };

        Assert.False(InstructionRegistry.TryCreateMatrixTileRuntimeObject(
            missingPayload,
            out _,
            out MatrixTileIrProjectionFaultKind missingPayloadFault));
        Assert.Equal(MatrixTileIrProjectionFaultKind.MissingVectorPayload, missingPayloadFault);

        var decoder = new VliwDecoderV4();
        VLIW_Instruction invalidMemoryShape = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.MTILE_LOAD,
            DataTypeEnum.INT32,
            destSrc1Ptr: 0x1000,
            src2Ptr: 0x2000,
            streamLength: 4,
            stride: 4);
        InstructionIR invalidIr = decoder.Decode(in invalidMemoryShape, slotIndex: 0);

        Assert.True(invalidIr.MatrixTileProjection.HasValue);
        Assert.False(invalidIr.MatrixTileProjection.Value.IsRuntimeLegal);
        Assert.Equal(
            MatrixTileIrProjectionFaultKind.MemoryShapeFault,
            invalidIr.MatrixTileProjection.Value.FaultKind);
        Assert.False(InstructionRegistry.TryCreateMatrixTileRuntimeObject(
            invalidIr,
            out _,
            out MatrixTileIrProjectionFaultKind memoryShapeFault));
        Assert.Equal(MatrixTileIrProjectionFaultKind.MemoryShapeFault, memoryShapeFault);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase08TypedTileMicroOpAndSchedulerLane_ClosesRuntimeCarrier()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/08_typed_tile_microop_and_scheduler_lane.md",
            MatrixTileRuntimeIsaPackageContract.Phase08TypedTileMicroOpSchedulerLanePath);
        Assert.Equal("2026-06-08", MatrixTileRuntimeIsaPackageContract.Phase08ReviewDate);
        Assert.Equal(
            "SupersededByPhase14OperationSpecificResourceContour",
            MatrixTileRuntimeIsaPackageContract.Phase08TypedTileMicroOpSchedulerLaneDecision);
        Assert.Equal(
            "Phase12ClosedStatusCatalogAndCompilerHandoffRemain",
            MatrixTileRuntimeIsaPackageContract.Phase08BlockingReason);
        Assert.Equal(
            "ClosedTypedTileMicroOpAbi",
            MatrixTileRuntimeIsaPackageContract.Phase08TypedMicroOpDecision);
        Assert.Equal(
            "ClosedTileMemoryDependencyMetadata",
            MatrixTileRuntimeIsaPackageContract.Phase08TileMemoryDependencyDecision);
        Assert.Equal(
            "ClosedTileRegisterDependencyMetadata",
            MatrixTileRuntimeIsaPackageContract.Phase08TileRegisterDependencyDecision);
        Assert.Equal(
            "ClosedAccumulatorDependencyMetadata",
            MatrixTileRuntimeIsaPackageContract.Phase08AccumulatorDependencyDecision);
        Assert.Equal(
            "ClosedMatrixTileMemoryLane6AndComputeLanes00To03Binding",
            MatrixTileRuntimeIsaPackageContract.Phase08SchedulerLaneBindingDecision);
        Assert.Equal(
            "ClosedTileIssueConstraints",
            MatrixTileRuntimeIsaPackageContract.Phase08IssueConstraintsDecision);
        Assert.Equal(
            "ClosedTileMemoryAndTileStateCaptureBarrierMetadata",
            MatrixTileRuntimeIsaPackageContract.Phase08CaptureBarrierDecision);
        Assert.Equal(
            "NoMicroOpSchedulerMaterializerBypassWithoutPriorPhases",
            MatrixTileRuntimeIsaPackageContract.Phase08BypassDecision);
        Assert.Equal(
            "NoVmxBackendOrExternalFallbackAuthority",
            MatrixTileRuntimeIsaPackageContract.Phase08FallbackDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TileMemoryDependencyMetadata);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TileRegisterDependencyMetadata);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08AccumulatorDependencyMetadata);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TileDependencyModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08SchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08IssueConstraints);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08CaptureBarriers);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasPhase08VmxOrBackendFallback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08BlocksMicroOpCreationBeforeMaterializer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08BlocksSchedulerMaterializerVlmBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08KeepsVmxBackendFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasTypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasSchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.PublishesTypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensSchedulerLaneBinding);
        Assert.False(MatrixTileRuntimeIsaPackageContract.OpensCompilerHelper);
        Assert.False(MatrixTileRuntimeIsaPackageContract.OpensVmxSpecificPath);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase08MicroOpSchedulerDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase08MicroOpSchedulerDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase08MicroOpSchedulerDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredRuntimeSurface), row.Mnemonic);
            Assert.Equal("SupersededByPhase14OperationSpecificResourceContour", row.Decision);
            Assert.Equal("Phase12ClosedStatusCatalogAndCompilerHandoffRemain", row.PrimaryBlocker);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.RequiresTileMemoryDependencyMetadata);
            Assert.True(row.RequiresTileRegisterDependencyMetadata, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresAccumulatorDependencyMetadata);
            Assert.True(row.HasTypedTileMicroOp, row.Mnemonic);
            Assert.Equal(row.RequiresTileMemoryDependencyMetadata, row.HasTileMemoryDependencyMetadata);
            Assert.True(row.HasTileRegisterDependencyMetadata, row.Mnemonic);
            Assert.Equal(row.RequiresAccumulatorDependencyMetadata, row.HasAccumulatorDependencyMetadata);
            Assert.True(row.HasSchedulerLaneBinding, row.Mnemonic);
            Assert.True(row.HasIssueConstraints, row.Mnemonic);
            Assert.True(row.HasCaptureBarriers, row.Mnemonic);
            Assert.True(row.BlocksSchedulerMaterializerVlmBypass, row.Mnemonic);
            Assert.True(row.KeepsVmxBackendFallbackNonAuthority, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }

        Assert.Empty(MatrixTileRuntimeIsaPackageContract.MicroOpSchedulerBlockers);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase08Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase08TypedTileMicroOpSchedulerLanePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase08Path))
        {
            return;
        }

        string phase08 = File.ReadAllText(phase08Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-isa",
                     "Typed tile MicroOp ABI is opened",
                     "Scheduler lane binding is opened",
                     "Tile memory dependency metadata is published",
                     "Tile register and accumulator dependency metadata is published",
                     "Scheduler/materializer/VLM bypass remains impossible",
                     "Compiler scope remains closed"
                 })
        {
            Assert.Contains(requiredText, phase08, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase08LeafMarkersPublishTypedMicroOpAndSchedulerAuthority()
    {
        var markers = new[]
        {
            (MtileLoadInstruction.Mnemonic, MtileLoadInstruction.RequiresTypedTileMicroOp, MtileLoadInstruction.NoTypedMicroOpPublication, MtileLoadInstruction.NoSchedulerLaneBindingPublication, MtileLoadInstruction.ExecutionLaneBinding, MtileLoadInstruction.IsExecutable),
            (MtileStoreInstruction.Mnemonic, MtileStoreInstruction.RequiresTypedTileMicroOp, MtileStoreInstruction.NoTypedMicroOpPublication, MtileStoreInstruction.NoSchedulerLaneBindingPublication, MtileStoreInstruction.ExecutionLaneBinding, MtileStoreInstruction.IsExecutable),
            (MtileMaccInstruction.Mnemonic, MtileMaccInstruction.RequiresTypedTileMicroOp, MtileMaccInstruction.NoTypedMicroOpPublication, MtileMaccInstruction.NoSchedulerLaneBindingPublication, MtileMaccInstruction.ExecutionLaneBinding, MtileMaccInstruction.IsExecutable),
            (MtransposeInstruction.Mnemonic, MtransposeInstruction.RequiresTypedTileMicroOp, MtransposeInstruction.NoTypedMicroOpPublication, MtransposeInstruction.NoSchedulerLaneBindingPublication, MtransposeInstruction.ExecutionLaneBinding, MtransposeInstruction.IsExecutable)
        };

        foreach ((string mnemonic, bool requiresTypedMicroOp, bool noTypedMicroOpPublication, bool noSchedulerLaneBindingPublication, string executionLaneBinding, bool isExecutable) in markers)
        {
            Assert.Contains(mnemonic, MatrixTileMnemonics);
            Assert.True(requiresTypedMicroOp, mnemonic);
            Assert.False(noTypedMicroOpPublication, mnemonic);
            Assert.False(noSchedulerLaneBindingPublication, mnemonic);
            Assert.Equal(
                mnemonic is "MTILE_LOAD" or "MTILE_STORE"
                    ? "MatrixTileStreamLane6"
                    : "MatrixTileComputeLanes00_03",
                executionLaneBinding);
            Assert.True(isExecutable, mnemonic);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase08SchedulerMaterializerAndVlmBypass_IsImpossible()
    {
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05RuntimeOwnedVlmRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06CanonicalDecoderAcceptance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07MaterializerFactories);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08SchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08BlocksSchedulerMaterializerVlmBypass);

        Type[] runtimeTypes = typeof(Processor.CPU_Core).Assembly.GetTypes();
        Assert.Contains(
            runtimeTypes,
            static type =>
                type != typeof(MatrixTileMicroOp) &&
                typeof(MatrixTileMicroOp).IsAssignableFrom(type));

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            var opcode = Enum.Parse<InstructionsEnum>(mnemonic);
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

            AssertDescriptorOnlyMatrixTileVlmRow(opcode);
            Assert.True(HasRegistryMnemonic(mnemonic));
            Assert.True(InstructionRegistry.IsMatrixTileMicroOpRegistered((uint)opcode));
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.MTILE_LOAD, typeof(MtileLoadMicroOp), true, false, true, false, false)]
    [InlineData(InstructionsEnum.MTILE_STORE, typeof(MtileStoreMicroOp), false, true, true, false, false)]
    [InlineData(InstructionsEnum.MTILE_MACC, typeof(MtileMaccMicroOp), false, false, false, true, false)]
    [InlineData(InstructionsEnum.MTRANSPOSE, typeof(MtransposeMicroOp), false, false, false, false, true)]
    public void MatrixTileRuntimeIsaPackage_Phase09RegistryPublishesTypedMicroOpExecutionCapture(
        InstructionsEnum opcode,
        Type expectedType,
        bool expectsReadMemoryRange,
        bool expectsWriteMemoryRange,
        bool expectsTileMemoryDependency,
        bool expectsAccumulatorDependency,
        bool expectsTransposeDependency)
    {
        DecoderContext context = CreateValidMatrixTileDecoderContext(opcode);

        MicroOp microOp = InstructionRegistry.CreateMicroOp((uint)opcode, context);
        MatrixTileMicroOp tileMicroOp = Assert.IsAssignableFrom<MatrixTileMicroOp>(microOp);

        Assert.IsType(expectedType, microOp);
        Assert.Equal(opcode, tileMicroOp.MaterializedInstruction.Opcode);
        Assert.True(tileMicroOp.MaterializedInstruction.IsRuntimeLegal);
        Assert.True(tileMicroOp.PublishesTypedTileMicroOp);
        Assert.True(tileMicroOp.PublishesSchedulerLaneBinding);
        Assert.True(tileMicroOp.PublishesIssueConstraints);
        Assert.True(tileMicroOp.PublishesCaptureBarriers);
        Assert.True(tileMicroOp.PublishesExecutionCaptureSemantics);
        Assert.True(tileMicroOp.OpensExecution);
        Assert.False(tileMicroOp.UsesFallbackPath);
        Assert.Equal(CanonicalDecodePublicationMode.SelfPublishes, tileMicroOp.CanonicalDecodePublication);
        bool isMemory = opcode is InstructionsEnum.MTILE_LOAD or InstructionsEnum.MTILE_STORE;
        Assert.Equal(
            isMemory ? SlotClass.MatrixTileStreamClass : SlotClass.AluClass,
            tileMicroOp.Placement.RequiredSlotClass);
        Assert.Equal(isMemory ? 0x40 : 0x0F, tileMicroOp.SchedulerLaneMask);
        Assert.True(tileMicroOp.ResourceMask.IsNonZero);
        Assert.True(tileMicroOp.SafetyMask.IsNonZero);
        Assert.Equal(expectsReadMemoryRange, tileMicroOp.ReadMemoryRanges.Count == 1);
        Assert.Equal(expectsWriteMemoryRange, tileMicroOp.WriteMemoryRanges.Count == 1);
        Assert.Equal(expectsTileMemoryDependency, tileMicroOp.DependencyMetadata.HasTileMemoryDependencyMetadata);
        Assert.True(tileMicroOp.DependencyMetadata.HasTileRegisterDependencyMetadata);
        Assert.Equal(expectsAccumulatorDependency, tileMicroOp.DependencyMetadata.HasAccumulatorDependencyMetadata);
        Assert.Equal(expectsTransposeDependency, tileMicroOp.DependencyMetadata.HasTransposePolicyDependencyMetadata);

        Processor.CPU_Core core = default;
        Assert.True(tileMicroOp.Execute(ref core));
        MatrixTileExecutionCaptureRecord? capture = tileMicroOp.LastExecutionCapture;
        Assert.True(capture.HasValue);
        Assert.Equal(tileMicroOp.OperationKind, capture.Value.OperationKind);
        Assert.Equal(tileMicroOp.Projection.Mnemonic, capture.Value.Mnemonic);
        Assert.True(capture.Value.BlocksArchitecturalSideEffectsBeforeRetire);
        Assert.False(capture.Value.UsesFallbackPath);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase09ExecuteCaptureSemantics_RemainsDistinctFromCurrentRetireAndReplayAuthority()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/09_execute_capture_semantics.md",
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureSemanticsPath);
        Assert.Equal("2026-06-09", MatrixTileRuntimeIsaPackageContract.Phase09ReviewDate);
        Assert.Equal(
            "ClosedMatrixTileExecuteCaptureSemantics",
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureSemanticsDecision);
        Assert.Equal(
            "NonePhase14ClosedHistoricalExecuteCaptureBoundaryPreserved",
            MatrixTileRuntimeIsaPackageContract.Phase09BlockingReason);
        Assert.Equal(
            "ClosedTileLoadCaptureBuffer",
            MatrixTileRuntimeIsaPackageContract.Phase09LoadCaptureDecision);
        Assert.Equal(
            "ClosedTileStorePendingWriteBuffer",
            MatrixTileRuntimeIsaPackageContract.Phase09StoreCaptureDecision);
        Assert.Equal(
            "ClosedMatrixTileMaccCaptureResult",
            MatrixTileRuntimeIsaPackageContract.Phase09MaccCaptureDecision);
        Assert.Equal(
            "ClosedMatrixTileTransposeCaptureResult",
            MatrixTileRuntimeIsaPackageContract.Phase09TransposeCaptureDecision);
        Assert.Equal(
            "ClosedDeterministicExceptionCapture",
            MatrixTileRuntimeIsaPackageContract.Phase09DeterministicCaptureDecision);
        Assert.Equal(
            "ClosedMemoryFaultCapture",
            MatrixTileRuntimeIsaPackageContract.Phase09MemoryFaultCaptureDecision);
        Assert.Equal(
            "ClosedTileStateReadSnapshot",
            MatrixTileRuntimeIsaPackageContract.Phase09TileStateSnapshotDecision);
        Assert.Equal(
            "ClosedAccumulatorReadSnapshot",
            MatrixTileRuntimeIsaPackageContract.Phase09AccumulatorSnapshotDecision);
        Assert.Equal(
            "NoRetireOwnedTilePublicationOpened",
            MatrixTileRuntimeIsaPackageContract.Phase09RetirePublicationDecision);
        Assert.Equal(
            "NoReplayRollbackPublicationOpened",
            MatrixTileRuntimeIsaPackageContract.Phase09ReplayRollbackDecision);
        Assert.Equal(
            "NoCaptureToRetireBypassWithoutRetireOwnership",
            MatrixTileRuntimeIsaPackageContract.Phase09BypassDecision);
        Assert.Equal(
            "NoScalarVectorDotDscLane7VmxOrBackendFallbackAuthority",
            MatrixTileRuntimeIsaPackageContract.Phase09FallbackDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09ExecutionCaptureSemantics);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09TileLoadCaptureBuffer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09TileStorePendingWriteBuffer);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09MaccCaptureResult);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09TransposeCaptureResult);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09DeterministicExceptionCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09MemoryFaultCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09TileStateReadSnapshot);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09AccumulatorReadSnapshot);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasPhase09RetirePublication);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasPhase09ReplayRollbackConformance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09BlocksCaptureToRetireBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09KeepsRetirePublicationNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09KeepsReplayRollbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09KeepsVmxBackendFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutionCaptureSemantics);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRetireWritebackOrSideEffectPublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasReplayRollbackConformance);
        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase09ExecuteCaptureDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredRuntimeSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTileExecuteCaptureSemantics", row.Decision);
            Assert.Equal(
                "HistoricalPhase09BoundaryRetireReplayGoldenAndPromotionOpen",
                row.PrimaryBlocker);
            Assert.Equal(row.Mnemonic == "MTILE_LOAD", row.RequiresTileLoadCaptureBuffer);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.RequiresTileStorePendingWriteBuffer);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresMaccCaptureResult);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.RequiresTransposeCaptureResult);
            Assert.True(row.RequiresDeterministicExceptionCapture, row.Mnemonic);
            Assert.True(row.RequiresMemoryFaultCapture, row.Mnemonic);
            Assert.True(row.RequiresTileStateReadSnapshot, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresAccumulatorReadSnapshot);
            Assert.True(row.HasExecutionCaptureSemantics, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_LOAD", row.HasTileLoadCaptureBuffer);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.HasTileStorePendingWriteBuffer);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasMaccCaptureResult);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.HasTransposeCaptureResult);
            Assert.True(row.HasDeterministicExceptionCapture, row.Mnemonic);
            Assert.True(row.HasMemoryFaultCapture, row.Mnemonic);
            Assert.True(row.HasTileStateReadSnapshot, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasAccumulatorReadSnapshot);
            Assert.False(row.HasRetirePublication, row.Mnemonic);
            Assert.False(row.HasReplayRollbackConformance, row.Mnemonic);
            Assert.True(row.BlocksArchitecturalSideEffectsBeforeRetire, row.Mnemonic);
            Assert.True(row.KeepsVmxBackendFallbackNonAuthority, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase09Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureSemanticsPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase09Path))
        {
            return;
        }

        string phase09 = File.ReadAllText(phase09Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-isa",
                     "executable execute/capture ABI",
                     "NoExecutionCapturePublication = false",
                     "Tile load capture buffer is opened",
                     "Tile store pending write buffer is opened",
                     "Matrix/tile capture result is opened",
                     "Deterministic exception capture is opened",
                     "Memory fault capture ABI is opened",
                     "Tile-state read snapshot is opened",
                     "Accumulator read snapshot is opened",
                     "Phase 09 itself opens no retire-owned publication",
                     "no replay/rollback authority",
                     "Compiler scope remains closed"
                 })
        {
            Assert.Contains(requiredText, phase09, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_CurrentLeafMarkersPublishCaptureRetireAndReplayButKeepGoldenClosed()
    {
        AssertLeafPipelineFailClosed(
            MtileLoadInstruction.Mnemonic,
            MtileLoadInstruction.IsExecutable,
            MtileLoadInstruction.CompilerHelperAllowed,
            MtileLoadInstruction.NoDecoderEncoderAbiPublication,
            MtileLoadInstruction.NoInstructionIrProjectionPublication,
            MtileLoadInstruction.NoRegistryMaterializerPublication,
            MtileLoadInstruction.NoTypedMicroOpPublication,
            MtileLoadInstruction.NoSchedulerLaneBindingPublication,
            MtileLoadInstruction.NoExecutionCapturePublication,
            MtileLoadInstruction.NoRetireWritebackPublication,
            MtileLoadInstruction.NoReplayRollbackPublication,
            MtileLoadInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileStoreInstruction.Mnemonic,
            MtileStoreInstruction.IsExecutable,
            MtileStoreInstruction.CompilerHelperAllowed,
            MtileStoreInstruction.NoDecoderEncoderAbiPublication,
            MtileStoreInstruction.NoInstructionIrProjectionPublication,
            MtileStoreInstruction.NoRegistryMaterializerPublication,
            MtileStoreInstruction.NoTypedMicroOpPublication,
            MtileStoreInstruction.NoSchedulerLaneBindingPublication,
            MtileStoreInstruction.NoExecutionCapturePublication,
            MtileStoreInstruction.NoRetireWritebackPublication,
            MtileStoreInstruction.NoReplayRollbackPublication,
            MtileStoreInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileMaccInstruction.Mnemonic,
            MtileMaccInstruction.IsExecutable,
            MtileMaccInstruction.CompilerHelperAllowed,
            MtileMaccInstruction.NoDecoderEncoderAbiPublication,
            MtileMaccInstruction.NoInstructionIrProjectionPublication,
            MtileMaccInstruction.NoRegistryMaterializerPublication,
            MtileMaccInstruction.NoTypedMicroOpPublication,
            MtileMaccInstruction.NoSchedulerLaneBindingPublication,
            MtileMaccInstruction.NoExecutionCapturePublication,
            MtileMaccInstruction.NoRetireWritebackPublication,
            MtileMaccInstruction.NoReplayRollbackPublication,
            MtileMaccInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtransposeInstruction.Mnemonic,
            MtransposeInstruction.IsExecutable,
            MtransposeInstruction.CompilerHelperAllowed,
            MtransposeInstruction.NoDecoderEncoderAbiPublication,
            MtransposeInstruction.NoInstructionIrProjectionPublication,
            MtransposeInstruction.NoRegistryMaterializerPublication,
            MtransposeInstruction.NoTypedMicroOpPublication,
            MtransposeInstruction.NoSchedulerLaneBindingPublication,
            MtransposeInstruction.NoExecutionCapturePublication,
            MtransposeInstruction.NoRetireWritebackPublication,
            MtransposeInstruction.NoReplayRollbackPublication,
            MtransposeInstruction.RequiresGoldenArtifacts);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase10RetirePublicationCommit_ClosesOnPhase09CaptureRecords()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/10_retire_publication_and_commit.md",
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationCommitPath);
        Assert.Equal("2026-06-10", MatrixTileRuntimeIsaPackageContract.Phase10ReviewDate);
        Assert.Equal(
            "ClosedMatrixTileRetirePublicationAndCommit",
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationCommitDecision);
        Assert.Equal(
            "NonePhase10ClosedPhase11ReplayRollbackClosed",
            MatrixTileRuntimeIsaPackageContract.Phase10BlockingReason);
        Assert.Equal(
            "RetireOwnedTileLoadPublication",
            MatrixTileRuntimeIsaPackageContract.Phase10LoadRetireDecision);
        Assert.Equal(
            "RetireOwnedAllOrNoneTileStoreCommit",
            MatrixTileRuntimeIsaPackageContract.Phase10StoreRetireDecision);
        Assert.Equal(
            "RetireOwnedAccumulatorPublication",
            MatrixTileRuntimeIsaPackageContract.Phase10MaccRetireDecision);
        Assert.Equal(
            "RetireOwnedTransposeDestinationPublication",
            MatrixTileRuntimeIsaPackageContract.Phase10TransposeRetireDecision);
        Assert.Equal(
            "DeterministicFaultRetirementWithoutPartialPublication",
            MatrixTileRuntimeIsaPackageContract.Phase10FaultRetirementDecision);
        Assert.Equal(
            "MatrixTileMicroOpWriteBackOwnsCaptureConsumption",
            MatrixTileRuntimeIsaPackageContract.Phase10WritebackOwnershipDecision);
        Assert.Equal(
            "ValidateThenPublishSingleArchitecturalEffectAtRetire",
            MatrixTileRuntimeIsaPackageContract.Phase10SideEffectOrderingDecision);
        Assert.Equal(
            "CaptureInvisibleUntilRetirePublication",
            MatrixTileRuntimeIsaPackageContract.Phase10ArchitecturalVisibilityDecision);
        Assert.Equal(
            "DuplicateCancelledAndMismatchedRetireFailClosed",
            MatrixTileRuntimeIsaPackageContract.Phase10BypassDecision);
        Assert.Equal(
            "CoreOwnerOpcodeOperationAndCaptureOrdinalCorrelated",
            MatrixTileRuntimeIsaPackageContract.Phase10CaptureCorrelationDecision);
        Assert.Equal(
            "NoHostOwnedEvidencePublishedAsGuestArchitecturalState",
            MatrixTileRuntimeIsaPackageContract.Phase10HostEvidenceDecision);
        Assert.Equal(
            "NoScalarVectorBaseMemoryDotDscLane7VmxBackendOrCompilerFallback",
            MatrixTileRuntimeIsaPackageContract.Phase10FallbackDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10RetirePublicationCommit);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10TileLoadRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10TileStoreRetireCommit);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10AccumulatorRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10TransposeRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10FaultRetirementPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10WritebackOwnership);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10SideEffectPublicationOrdering);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10ArchitecturalStateVisibilityRules);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10CaptureToRetireCorrelation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10RejectsDuplicateRetire);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10RejectsCancelledRetire);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10BlocksExecuteCaptureToRetireBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10KeepsHostOwnedEvidenceNonArchitectural);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10KeepsVmxBackendFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase10KeepsCompilerHandoffBlocked);

        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensRetireWritebackOrSideEffects);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRetireWritebackOrSideEffectPublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasRetireOwnedTilePublicationPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09ExecutionCaptureSemantics);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasPhase09RetirePublication);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasPhase09ReplayRollbackConformance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08TypedTileMicroOp);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08SchedulerLaneBinding);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase07MaterializerFactories);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06CanonicalDecoderAcceptance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05RuntimeOwnedVlmRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase08BlocksSchedulerMaterializerVlmBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase09BlocksCaptureToRetireBypass);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase10RetirePublicationDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredRuntimeSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTileRetirePublicationAndCommit", row.Decision);
            Assert.Equal("NonePhase10Closed", row.PrimaryBlocker);
            Assert.Equal(row.Mnemonic == "MTILE_LOAD", row.RequiresTileLoadRetirePublication);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.RequiresTileStoreRetireCommit);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresAccumulatorRetirePublication);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.RequiresTransposeRetirePublication);
            Assert.True(row.RequiresFaultRetirementPolicy, row.Mnemonic);
            Assert.True(row.RequiresWritebackOwnership, row.Mnemonic);
            Assert.True(row.RequiresSideEffectPublicationOrdering, row.Mnemonic);
            Assert.True(row.RequiresArchitecturalStateVisibilityRules, row.Mnemonic);
            Assert.True(row.HasRetirePublicationAndCommit, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_LOAD", row.HasTileLoadRetirePublication);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.HasTileStoreRetireCommit);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasAccumulatorRetirePublication);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.HasTransposeRetirePublication);
            Assert.True(row.HasFaultRetirementPolicy, row.Mnemonic);
            Assert.True(row.HasWritebackOwnership, row.Mnemonic);
            Assert.True(row.HasSideEffectPublicationOrdering, row.Mnemonic);
            Assert.True(row.HasArchitecturalStateVisibilityRules, row.Mnemonic);
            Assert.True(row.BlocksExecuteCaptureToRetireBypass, row.Mnemonic);
            Assert.True(row.KeepsHostOwnedEvidenceNonArchitectural, row.Mnemonic);
            Assert.True(row.KeepsVmxBackendFallbackNonAuthority, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase10Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationCommitPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase10Path))
        {
            return;
        }

        string phase10 = File.ReadAllText(phase10Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-isa",
                     "ClosedMatrixTileRetirePublicationAndCommit",
                     "RetireOwnedTileLoadPublication",
                     "RetireOwnedAllOrNoneTileStoreCommit",
                     "RetireOwnedAccumulatorPublication",
                     "RetireOwnedTransposeDestinationPublication",
                     "DeterministicFaultRetirementWithoutPartialPublication",
                     "MatrixTileMicroOpWriteBackOwnsCaptureConsumption",
                     "CaptureInvisibleUntilRetirePublication",
                     "Compiler scope remains closed"
                 })
        {
            Assert.Contains(requiredText, phase10, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase10LeafMarkersPublishRetireButKeepReplayClosed()
    {
        Assert.True(MtileLoadInstruction.RequiresRetireStagedPublication);
        Assert.True(MtileStoreInstruction.RequiresRetireStagedCommit);
        Assert.True(MtileMaccInstruction.RequiresRetireStagedPublication);
        Assert.True(MtransposeInstruction.RequiresRetireStagedPublication);

        AssertLeafPipelineFailClosed(
            MtileLoadInstruction.Mnemonic,
            MtileLoadInstruction.IsExecutable,
            MtileLoadInstruction.CompilerHelperAllowed,
            MtileLoadInstruction.NoDecoderEncoderAbiPublication,
            MtileLoadInstruction.NoInstructionIrProjectionPublication,
            MtileLoadInstruction.NoRegistryMaterializerPublication,
            MtileLoadInstruction.NoTypedMicroOpPublication,
            MtileLoadInstruction.NoSchedulerLaneBindingPublication,
            MtileLoadInstruction.NoExecutionCapturePublication,
            MtileLoadInstruction.NoRetireWritebackPublication,
            MtileLoadInstruction.NoReplayRollbackPublication,
            MtileLoadInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileStoreInstruction.Mnemonic,
            MtileStoreInstruction.IsExecutable,
            MtileStoreInstruction.CompilerHelperAllowed,
            MtileStoreInstruction.NoDecoderEncoderAbiPublication,
            MtileStoreInstruction.NoInstructionIrProjectionPublication,
            MtileStoreInstruction.NoRegistryMaterializerPublication,
            MtileStoreInstruction.NoTypedMicroOpPublication,
            MtileStoreInstruction.NoSchedulerLaneBindingPublication,
            MtileStoreInstruction.NoExecutionCapturePublication,
            MtileStoreInstruction.NoRetireWritebackPublication,
            MtileStoreInstruction.NoReplayRollbackPublication,
            MtileStoreInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileMaccInstruction.Mnemonic,
            MtileMaccInstruction.IsExecutable,
            MtileMaccInstruction.CompilerHelperAllowed,
            MtileMaccInstruction.NoDecoderEncoderAbiPublication,
            MtileMaccInstruction.NoInstructionIrProjectionPublication,
            MtileMaccInstruction.NoRegistryMaterializerPublication,
            MtileMaccInstruction.NoTypedMicroOpPublication,
            MtileMaccInstruction.NoSchedulerLaneBindingPublication,
            MtileMaccInstruction.NoExecutionCapturePublication,
            MtileMaccInstruction.NoRetireWritebackPublication,
            MtileMaccInstruction.NoReplayRollbackPublication,
            MtileMaccInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtransposeInstruction.Mnemonic,
            MtransposeInstruction.IsExecutable,
            MtransposeInstruction.CompilerHelperAllowed,
            MtransposeInstruction.NoDecoderEncoderAbiPublication,
            MtransposeInstruction.NoInstructionIrProjectionPublication,
            MtransposeInstruction.NoRegistryMaterializerPublication,
            MtransposeInstruction.NoTypedMicroOpPublication,
            MtransposeInstruction.NoSchedulerLaneBindingPublication,
            MtransposeInstruction.NoExecutionCapturePublication,
            MtransposeInstruction.NoRetireWritebackPublication,
            MtransposeInstruction.NoReplayRollbackPublication,
            MtransposeInstruction.RequiresGoldenArtifacts);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase11ReplayRollbackConformance_ClosesRuntimeAuthority()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/11_replay_rollback_conformance.md",
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackConformancePath);
        Assert.Equal("2026-06-10", MatrixTileRuntimeIsaPackageContract.Phase11ReviewDate);
        Assert.Equal(
            "ClosedMatrixTileReplayRollbackConformance",
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackConformanceDecision);
        Assert.Equal(
            "NonePhase11ClosedPhase12Closed",
            MatrixTileRuntimeIsaPackageContract.Phase11BlockingReason);
        Assert.Equal(
            "ReplayStableDecodedInstructionFingerprint",
            MatrixTileRuntimeIsaPackageContract.Phase11DecodedInstructionReplayIdentityDecision);
        Assert.Equal(
            "ReplayStableCanonicalTileDescriptorFingerprint",
            MatrixTileRuntimeIsaPackageContract.Phase11TileDescriptorReplayIdentityDecision);
        Assert.Equal(
            "CoreOwnedTileCheckpointRestore",
            MatrixTileRuntimeIsaPackageContract.Phase11PendingTileWriteRollbackDecision);
        Assert.Equal(
            "CoreOwnedAllOrNoneStoreCheckpointRestore",
            MatrixTileRuntimeIsaPackageContract.Phase11PendingMemoryStoreRollbackDecision);
        Assert.Equal(
            "CoreOwnedAccumulatorCheckpointRestore",
            MatrixTileRuntimeIsaPackageContract.Phase11AccumulatorRollbackDecision);
        Assert.Equal(
            "DeterministicCapturedAndRetireMemoryFaultReplay",
            MatrixTileRuntimeIsaPackageContract.Phase11MemoryFaultReplayDecision);
        Assert.Equal(
            "DeterministicDescriptorAndSemanticFaultReplay",
            MatrixTileRuntimeIsaPackageContract.Phase11DescriptorFaultReplayDecision);
        Assert.Equal(
            "ClosedReplayRollbackLegalIllegalConformanceVectors",
            MatrixTileRuntimeIsaPackageContract.Phase11ConformanceVectorDecision);
        Assert.Equal(
            "ReplayRequiresRegisteredRetiredCaptureJournal",
            MatrixTileRuntimeIsaPackageContract.Phase11BypassDecision);
        Assert.Equal(
            "NoScalarVectorBaseMemoryDotDscLane7VmxBackendOrCompilerFallback",
            MatrixTileRuntimeIsaPackageContract.Phase11FallbackDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11ReplayRollbackConformance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11DecodedInstructionReplayIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11TileDescriptorReplayIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11PendingTileWriteRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11PendingMemoryStoreRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11AccumulatorRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11DeterministicReplayAfterMemoryFault);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11DeterministicReplayAfterDescriptorFault);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase11LegalIllegalConformanceVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase11BlocksReplayWithoutRetirePublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase11BlocksCaptureRecordIdentityBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase11KeepsVmxBackendFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase11KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase11KeepsCompilerHandoffBlocked);

        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesReplayRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.OpensReplayRollback);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasReplayRollbackConformance);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10RetirePublicationCommit);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase10FaultRetirementPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09ExecutionCaptureSemantics);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09MemoryFaultCapture);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09TileStateReadSnapshot);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase09AccumulatorReadSnapshot);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase08CaptureBarriers);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase06DescriptorDecodeValidation);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03RetireRollbackPolicy);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase03PartialFaultPolicy);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase11ReplayRollbackDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredRuntimeSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTileReplayRollbackConformance", row.Decision);
            Assert.Equal("NonePhase11Closed", row.PrimaryBlocker);
            Assert.True(row.RequiresDecodedInstructionReplayIdentity, row.Mnemonic);
            Assert.True(row.RequiresTileDescriptorReplayIdentity, row.Mnemonic);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTRANSPOSE", row.RequiresPendingTileWriteRollback);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.RequiresPendingMemoryStoreRollback);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresAccumulatorRollback);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.RequiresDeterministicReplayAfterMemoryFault);
            Assert.True(row.RequiresDeterministicReplayAfterDescriptorFault, row.Mnemonic);
            Assert.True(row.RequiresLegalIllegalConformanceVectors, row.Mnemonic);
            Assert.True(row.HasReplayRollbackConformance, row.Mnemonic);
            Assert.True(row.HasDecodedInstructionReplayIdentity, row.Mnemonic);
            Assert.True(row.HasTileDescriptorReplayIdentity, row.Mnemonic);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTRANSPOSE", row.HasPendingTileWriteRollback);
            Assert.Equal(row.Mnemonic == "MTILE_STORE", row.HasPendingMemoryStoreRollback);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasAccumulatorRollback);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.HasDeterministicReplayAfterMemoryFault);
            Assert.True(row.HasDeterministicReplayAfterDescriptorFault, row.Mnemonic);
            Assert.True(row.HasLegalIllegalConformanceVectors, row.Mnemonic);
            Assert.True(row.BlocksReplayWithoutRetirePublication, row.Mnemonic);
            Assert.True(row.BlocksCaptureRecordIdentityBypass, row.Mnemonic);
            Assert.True(row.KeepsVmxBackendFallbackNonAuthority, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase11Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackConformancePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase11Path))
        {
            return;
        }

        string phase11 = File.ReadAllText(phase11Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed/runtime-only",
                     "replay-stable decoded and materialized instruction identity",
                     "core-owned tile and accumulator checkpoints",
                     "all-or-none memory checkpoint restore",
                     "deterministic replay of retired faults",
                     "duplicate replay and stale checkpoint rejection",
                     "Compiler scope remains closed"
                 })
        {
            Assert.Contains(requiredText, phase11, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase11LeafMarkersPublishReplayRollbackAuthorityOnly()
    {
        Assert.True(MtileLoadInstruction.RequiresReplayRollbackConformance);
        Assert.True(MtileStoreInstruction.RequiresReplayRollbackConformance);
        Assert.True(MtileMaccInstruction.RequiresReplayRollbackConformance);
        Assert.True(MtransposeInstruction.RequiresReplayRollbackConformance);

        AssertLeafPipelineFailClosed(
            MtileLoadInstruction.Mnemonic,
            MtileLoadInstruction.IsExecutable,
            MtileLoadInstruction.CompilerHelperAllowed,
            MtileLoadInstruction.NoDecoderEncoderAbiPublication,
            MtileLoadInstruction.NoInstructionIrProjectionPublication,
            MtileLoadInstruction.NoRegistryMaterializerPublication,
            MtileLoadInstruction.NoTypedMicroOpPublication,
            MtileLoadInstruction.NoSchedulerLaneBindingPublication,
            MtileLoadInstruction.NoExecutionCapturePublication,
            MtileLoadInstruction.NoRetireWritebackPublication,
            MtileLoadInstruction.NoReplayRollbackPublication,
            MtileLoadInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileStoreInstruction.Mnemonic,
            MtileStoreInstruction.IsExecutable,
            MtileStoreInstruction.CompilerHelperAllowed,
            MtileStoreInstruction.NoDecoderEncoderAbiPublication,
            MtileStoreInstruction.NoInstructionIrProjectionPublication,
            MtileStoreInstruction.NoRegistryMaterializerPublication,
            MtileStoreInstruction.NoTypedMicroOpPublication,
            MtileStoreInstruction.NoSchedulerLaneBindingPublication,
            MtileStoreInstruction.NoExecutionCapturePublication,
            MtileStoreInstruction.NoRetireWritebackPublication,
            MtileStoreInstruction.NoReplayRollbackPublication,
            MtileStoreInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileMaccInstruction.Mnemonic,
            MtileMaccInstruction.IsExecutable,
            MtileMaccInstruction.CompilerHelperAllowed,
            MtileMaccInstruction.NoDecoderEncoderAbiPublication,
            MtileMaccInstruction.NoInstructionIrProjectionPublication,
            MtileMaccInstruction.NoRegistryMaterializerPublication,
            MtileMaccInstruction.NoTypedMicroOpPublication,
            MtileMaccInstruction.NoSchedulerLaneBindingPublication,
            MtileMaccInstruction.NoExecutionCapturePublication,
            MtileMaccInstruction.NoRetireWritebackPublication,
            MtileMaccInstruction.NoReplayRollbackPublication,
            MtileMaccInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtransposeInstruction.Mnemonic,
            MtransposeInstruction.IsExecutable,
            MtransposeInstruction.CompilerHelperAllowed,
            MtransposeInstruction.NoDecoderEncoderAbiPublication,
            MtransposeInstruction.NoInstructionIrProjectionPublication,
            MtransposeInstruction.NoRegistryMaterializerPublication,
            MtransposeInstruction.NoTypedMicroOpPublication,
            MtransposeInstruction.NoSchedulerLaneBindingPublication,
            MtransposeInstruction.NoExecutionCapturePublication,
            MtransposeInstruction.NoRetireWritebackPublication,
            MtransposeInstruction.NoReplayRollbackPublication,
            MtransposeInstruction.RequiresGoldenArtifacts);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase12PositiveExecutableGoldenArtifacts_CloseRuntimeEvidence()
    {
        Assert.Equal(
            "Documentation/InstructionsList/MTILE_RefPlan/12_positive_executable_golden_artifacts.md",
            MatrixTileRuntimeIsaPackageContract.Phase12PositiveExecutableGoldenArtifactsPath);
        Assert.Equal("2026-06-10", MatrixTileRuntimeIsaPackageContract.Phase12ReviewDate);
        Assert.Equal(
            "ClosedMatrixTilePositiveExecutableGoldenArtifacts",
            MatrixTileRuntimeIsaPackageContract.Phase12PositiveExecutableGoldenArtifactsDecision);
        Assert.Equal(
            "ClosedRuntimeNoFallbackNoHiddenLoweringRegressionEvidence",
            MatrixTileRuntimeIsaPackageContract.Phase12RuntimeNoFallbackNoHiddenLoweringRegressionDecision);
        Assert.Equal(
            "NonePhase14RegeneratedPlacementSensitiveEvidenceClosed",
            MatrixTileRuntimeIsaPackageContract.Phase12BlockingReason);
        Assert.Equal(
            "ClosedLegalDecodeEncodeRoundTripGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12DecodeEncodeVectorsDecision);
        Assert.Equal(
            "ClosedLegalIrMaterializerProjectionGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12IrMaterializerVectorsDecision);
        Assert.Equal(
            "ClosedLegalExecuteRetireGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12ExecuteRetireVectorsDecision);
        Assert.Equal(
            "ClosedLoadStoreAllOrNoneMemoryFaultGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12MemoryFaultVectorsDecision);
        Assert.Equal(
            "ClosedDescriptorFaultGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12DescriptorFaultVectorsDecision);
        Assert.Equal(
            "ClosedAccumulatorGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12AccumulatorVectorsDecision);
        Assert.Equal(
            "ClosedTransposeGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12TransposeVectorsDecision);
        Assert.Equal(
            "ClosedReplayRollbackGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.Phase12ReplayRollbackVectorsDecision);
        Assert.Equal(
            "ReservedMatrixTileCarrierRowsFailClosed",
            MatrixTileRuntimeIsaPackageContract.Phase12ReservedRowsDecision);
        Assert.Equal(
            "ClosedMatrixTileRuntimeNoFallbackAndNoHiddenLowering",
            MatrixTileRuntimeIsaPackageContract.Phase12NoFallbackRegressionDecision);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12PositiveExecutableGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12RuntimeNoFallbackNoHiddenLoweringRegressionEvidence);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12DecodeEncodeGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12IrMaterializerGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12ExecuteRetireGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12MemoryFaultGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12DescriptorFaultGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12AccumulatorGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12TransposeGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12ReplayRollbackGoldenVectors);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12ReservedRowGoldenVectors);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase12BlocksPositiveGoldenPublication);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase12BlocksNoFallbackRegressionEvidenceBypass);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase12KeepsVmxBackendFallbackNonAuthority);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase12KeepsCompilerScopeClosed);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase12KeepsCompilerHandoffBlocked);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPositiveExecutableGoldenVectors);

        Assert.Contains(
            "RuntimeNoFallbackNoHiddenLoweringRegressionEvidence",
            MatrixTileRuntimeIsaPackageContract.PositiveGoldenPublicationPrerequisites);
        Assert.Contains(
            "ExecutableGoldenVectors",
            MatrixTileRuntimeIsaPackageContract.PositiveGoldenPublicationPrerequisites);

        Assert.Equal(
            MatrixTileMnemonics,
            MatrixTileRuntimeIsaPackageContract.Phase12GoldenArtifactDecisionRows
                .Select(static row => row.Mnemonic)
                .ToArray());

        foreach (MatrixTilePhase12GoldenArtifactDecisionRow row in MatrixTileRuntimeIsaPackageContract.Phase12GoldenArtifactDecisionRows)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(row.Mnemonic);

            Assert.False(string.IsNullOrWhiteSpace(row.RequiredArtifactSurface), row.Mnemonic);
            Assert.Equal("ClosedMatrixTilePositiveExecutableGoldenArtifacts", row.Decision);
            Assert.Equal("ClosedRuntimeNoFallbackNoHiddenLoweringRegressionEvidence", row.NoFallbackDecision);
            Assert.Equal("NonePhase12Closed", row.PrimaryBlocker);
            Assert.True(row.RequiresLegalDecodeEncodeRoundTripVectors, row.Mnemonic);
            Assert.True(row.RequiresLegalIrMaterializerProjectionVectors, row.Mnemonic);
            Assert.True(row.RequiresLegalExecuteRetireVectors, row.Mnemonic);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.RequiresMemoryFaultVectors);
            Assert.True(row.RequiresDescriptorFaultVectors, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.RequiresAccumulatorVectors);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.RequiresTransposeVectors);
            Assert.True(row.RequiresReplayRollbackVectors, row.Mnemonic);
            Assert.True(row.RequiresNegativeReservedVectors, row.Mnemonic);
            Assert.True(row.RequiresRuntimeNoFallbackNoHiddenLoweringRegressionEvidence, row.Mnemonic);
            Assert.True(row.HasPositiveExecutableGoldenArtifacts, row.Mnemonic);
            Assert.True(row.HasRuntimeNoFallbackNoHiddenLoweringRegressionEvidence, row.Mnemonic);
            Assert.True(row.HasLegalDecodeEncodeRoundTripVectors, row.Mnemonic);
            Assert.True(row.HasLegalIrMaterializerProjectionVectors, row.Mnemonic);
            Assert.True(row.HasLegalExecuteRetireVectors, row.Mnemonic);
            Assert.Equal(row.Mnemonic is "MTILE_LOAD" or "MTILE_STORE", row.HasMemoryFaultVectors);
            Assert.True(row.HasDescriptorFaultVectors, row.Mnemonic);
            Assert.Equal(row.Mnemonic == "MTILE_MACC", row.HasAccumulatorVectors);
            Assert.Equal(row.Mnemonic == "MTRANSPOSE", row.HasTransposeVectors);
            Assert.True(row.HasReplayRollbackVectors, row.Mnemonic);
            Assert.True(row.HasNegativeReservedVectors, row.Mnemonic);
            Assert.False(row.BlocksPositiveGoldenPublication, row.Mnemonic);
            Assert.True(row.BlocksNoFallbackRegressionEvidenceBypass, row.Mnemonic);
            Assert.True(row.KeepsVmxBackendFallbackNonAuthority, row.Mnemonic);
            Assert.True(row.KeepsCompilerScopeClosed, row.Mnemonic);
            Assert.True(row.KeepsCompilerHandoffBlocked, row.Mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
        }

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase12Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase12PositiveExecutableGoldenArtifactsPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase12Path))
        {
            return;
        }

        string phase12 = File.ReadAllText(phase12Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed",
                     "positive executable golden artifacts",
                     "runtime no-fallback/no-hidden-lowering",
                     "legal decode/encode round-trip",
                     "IR/materializer",
                     "execute/retire",
                     "memory fault",
                     "descriptor fault",
                     "replay/rollback",
                     "Compiler scope remains closed."
                 })
        {
            Assert.Contains(requiredText, phase12, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_Phase12LeafMarkersKeepCatalogAndCompilerClosed()
    {
        AssertLeafPipelineFailClosed(
            MtileLoadInstruction.Mnemonic,
            MtileLoadInstruction.IsExecutable,
            MtileLoadInstruction.CompilerHelperAllowed,
            MtileLoadInstruction.NoDecoderEncoderAbiPublication,
            MtileLoadInstruction.NoInstructionIrProjectionPublication,
            MtileLoadInstruction.NoRegistryMaterializerPublication,
            MtileLoadInstruction.NoTypedMicroOpPublication,
            MtileLoadInstruction.NoSchedulerLaneBindingPublication,
            MtileLoadInstruction.NoExecutionCapturePublication,
            MtileLoadInstruction.NoRetireWritebackPublication,
            MtileLoadInstruction.NoReplayRollbackPublication,
            MtileLoadInstruction.RequiresGoldenArtifacts);
        Assert.True(MtileLoadInstruction.NoHiddenScalarLowering);
        Assert.True(MtileLoadInstruction.NoHiddenVectorLowering);
        Assert.True(MtileLoadInstruction.UsesDedicatedMatrixTileLane6Transport);
        Assert.True(MtileLoadInstruction.NoLane6DscFallback);
        Assert.True(MtileLoadInstruction.NoGenericStreamEngineExecutionAuthority);
        Assert.True(MtileLoadInstruction.NoLane7Fallback);
        Assert.True(MtileLoadInstruction.NoExternalBackendFallback);
        Assert.True(MtileLoadInstruction.NoVmxSpecificPath);

        AssertLeafPipelineFailClosed(
            MtileStoreInstruction.Mnemonic,
            MtileStoreInstruction.IsExecutable,
            MtileStoreInstruction.CompilerHelperAllowed,
            MtileStoreInstruction.NoDecoderEncoderAbiPublication,
            MtileStoreInstruction.NoInstructionIrProjectionPublication,
            MtileStoreInstruction.NoRegistryMaterializerPublication,
            MtileStoreInstruction.NoTypedMicroOpPublication,
            MtileStoreInstruction.NoSchedulerLaneBindingPublication,
            MtileStoreInstruction.NoExecutionCapturePublication,
            MtileStoreInstruction.NoRetireWritebackPublication,
            MtileStoreInstruction.NoReplayRollbackPublication,
            MtileStoreInstruction.RequiresGoldenArtifacts);
        Assert.True(MtileStoreInstruction.NoHiddenScalarLowering);
        Assert.True(MtileStoreInstruction.NoHiddenVectorLowering);
        Assert.True(MtileStoreInstruction.UsesDedicatedMatrixTileLane6Transport);
        Assert.True(MtileStoreInstruction.NoLane6DscFallback);
        Assert.True(MtileStoreInstruction.NoGenericStreamEngineExecutionAuthority);
        Assert.True(MtileStoreInstruction.NoLane7Fallback);
        Assert.True(MtileStoreInstruction.NoExternalBackendFallback);
        Assert.True(MtileStoreInstruction.NoVmxSpecificPath);

        AssertLeafPipelineFailClosed(
            MtileMaccInstruction.Mnemonic,
            MtileMaccInstruction.IsExecutable,
            MtileMaccInstruction.CompilerHelperAllowed,
            MtileMaccInstruction.NoDecoderEncoderAbiPublication,
            MtileMaccInstruction.NoInstructionIrProjectionPublication,
            MtileMaccInstruction.NoRegistryMaterializerPublication,
            MtileMaccInstruction.NoTypedMicroOpPublication,
            MtileMaccInstruction.NoSchedulerLaneBindingPublication,
            MtileMaccInstruction.NoExecutionCapturePublication,
            MtileMaccInstruction.NoRetireWritebackPublication,
            MtileMaccInstruction.NoReplayRollbackPublication,
            MtileMaccInstruction.RequiresGoldenArtifacts);
        Assert.True(MtileMaccInstruction.NoHiddenScalarLowering);
        Assert.True(MtileMaccInstruction.NoHiddenVectorLowering);
        Assert.True(MtileMaccInstruction.NoLane6Placement);
        Assert.True(MtileMaccInstruction.NoLane6DscFallback);
        Assert.True(MtileMaccInstruction.NoLane7Fallback);
        Assert.True(MtileMaccInstruction.NoExternalBackendFallback);
        Assert.True(MtileMaccInstruction.NoVmxSpecificPath);

        AssertLeafPipelineFailClosed(
            MtransposeInstruction.Mnemonic,
            MtransposeInstruction.IsExecutable,
            MtransposeInstruction.CompilerHelperAllowed,
            MtransposeInstruction.NoDecoderEncoderAbiPublication,
            MtransposeInstruction.NoInstructionIrProjectionPublication,
            MtransposeInstruction.NoRegistryMaterializerPublication,
            MtransposeInstruction.NoTypedMicroOpPublication,
            MtransposeInstruction.NoSchedulerLaneBindingPublication,
            MtransposeInstruction.NoExecutionCapturePublication,
            MtransposeInstruction.NoRetireWritebackPublication,
            MtransposeInstruction.NoReplayRollbackPublication,
            MtransposeInstruction.RequiresGoldenArtifacts);
        Assert.True(MtransposeInstruction.NoHiddenScalarLowering);
        Assert.True(MtransposeInstruction.NoHiddenVectorLowering);
        Assert.True(MtransposeInstruction.NoLane6Placement);
        Assert.True(MtransposeInstruction.NoLane6DscFallback);
        Assert.True(MtransposeInstruction.NoLane7Fallback);
        Assert.True(MtransposeInstruction.NoExternalBackendFallback);
        Assert.True(MtransposeInstruction.NoVmxSpecificPath);

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12PositiveExecutableGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase12RuntimeNoFallbackNoHiddenLoweringRegressionEvidence);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPositiveExecutableGoldenVectors);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_HasNoRemainingRuntimeIsaOpenTasks()
    {
        Assert.Equal(
            "RuntimeIsaPhases01To14ClosedNoRemainingRuntimeTasks",
            MatrixTileRuntimeIsaPackageContract.RemainingRuntimeIsaTaskPoolDecision);

        Assert.Empty(MatrixTileRuntimeIsaPackageContract.RemainingRuntimeIsaOpenTasks);

        foreach (string closedPool in new[]
                 {
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
                 })
        {
            Assert.Contains(closedPool, MatrixTileRuntimeIsaPackageContract.ClosedAsBlockedRuntimeIsaTaskPools);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_PublishesPositiveGoldenManifestAndRetainsNegativeHistory()
    {
        Assert.Equal(
            "ClosedMatrixTilePositiveExecutableGoldenArtifacts",
            MatrixTileRuntimeIsaPackageContract.GoldenArtifactPublicationDecision);
        Assert.Equal(
            "NonRTL/Core/ISA/Instructions/NonVmx/Docs/ImplPlan/PHASE_09B_MATRIX_TILE_NEGATIVE_GOLDEN_MANIFEST_2026-06-07.md",
            MatrixTileRuntimeIsaPackageContract.NegativeGoldenManifestPath);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string manifestPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            MatrixTileRuntimeIsaPackageContract.NegativeGoldenManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
        {
            return;
        }

        string manifest = File.ReadAllText(manifestPath);

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            Assert.Contains(mnemonic, manifest, StringComparison.Ordinal);
        }

        foreach (string requiredText in new[]
                 {
                     "No executable matrix/tile golden artifacts are published",
                     "negative golden manifest only",
                     "tile state/descriptor ABI",
                     "accumulator/transpose ABI",
                     "runtime-owned VLM rows",
                     "typed tile MicroOp",
                     "replay/rollback conformance",
                     "compiler no-emission or helper contracts"
                 })
        {
            Assert.Contains(requiredText, manifest, StringComparison.Ordinal);
        }

        string positiveManifestPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            MatrixTileRuntimeIsaPackageContract.PositiveGoldenManifestPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(positiveManifestPath))
        {
            return;
        }

        string positiveManifest = File.ReadAllText(positiveManifestPath);

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            Assert.Contains(mnemonic, positiveManifest, StringComparison.Ordinal);
        }

        Assert.Contains(
            "MatrixTilePositiveGoldenArtifactManifest",
            positiveManifest,
            StringComparison.Ordinal);
        Assert.Contains(
            "MatrixTileNoFallbackEvidenceContract",
            positiveManifest,
            StringComparison.Ordinal);

        foreach (string prerequisite in new[]
                 {
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
                     "ExecutableGoldenVectors"
                 })
        {
            Assert.Contains(prerequisite, MatrixTileRuntimeIsaPackageContract.PositiveGoldenPublicationPrerequisites);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_FullClosureLedger_ClosesPhase14Handoff()
    {
        Assert.Equal(
            "RuntimeIsaPhases01To14ClosedExecutableStatusAndCompilerHandoffReady",
            MatrixTileRuntimeIsaPackageContract.ClosureLedgerDecision);

        string[] expectedGates =
        [
            "status/catalog promotion",
            "opcode/descriptor authority",
            "tile state/descriptor ABI",
            "memory-shape/fault model",
            "accumulator/transpose ABI",
            "VLM rows",
            "decoder/encoder",
            "IR projection",
            "materializer",
            "typed tile MicroOp",
            "scheduler lane",
            "tile-stream resource contour",
            "execute/capture",
            "retire",
            "replay/rollback",
            "runtime/ISA conformance tests",
            "golden artifacts",
            "runtime no-fallback/no-hidden-lowering regression evidence",
            "positive compiler emission handoff package"
        ];

        Assert.Equal(
            expectedGates,
            MatrixTileRuntimeIsaPackageContract.ClosureGateRows.Select(static row => row.GateName).ToArray());

        foreach (MatrixTileRuntimeIsaClosureGateRow row in MatrixTileRuntimeIsaPackageContract.ClosureGateRows)
        {
            Assert.True(row.IsExecutableClosure, row.GateName);
            Assert.False(row.BlocksCompilerUpdate, row.GateName);
            Assert.False(string.IsNullOrWhiteSpace(row.Blocker), row.GateName);
        }

        foreach (string evidenceCode in new[] { "CAT", "OP", "ABI", "VLM", "DEC", "IR", "MAT", "UOP", "SCH", "RSC", "EXE", "RET", "RPL", "TST", "GLD", "NOE", "HND" })
        {
            Assert.Contains(
                MatrixTileRuntimeIsaPackageContract.ClosureGateRows,
                row => string.Equals(row.EvidenceCode, evidenceCode, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_AllowsPositiveCompilerHandoffAfterNumericReclosure()
    {
        MatrixTileRuntimeIsaPackageContract.RequireCompilerUpdateReadiness();
        MatrixTileRuntimeIsaPackageContract.RequirePositiveCompilerEmissionReadiness();
        Assert.True(MatrixTileRuntimeIsaPackageContract.PositiveNumericHandoffReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitivePackageReadiness);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCurrentCompilerImplementation);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_RecordsExistingPositiveCompilerImplementationAfterHandoff()
    {
        Assert.Equal(
            "SupersededByExistingPositiveCompilerImplementationCompilerCodeUnchangedByPhase14",
            MatrixTileRuntimeIsaPackageContract.CompilerNoEmissionBoundaryDecision);
        Assert.Equal(
            "SupersededByExistingPositiveCompilerImplementation",
            MatrixTileRuntimeIsaPackageContract.Phase13CompilerVisibleNoEmissionBoundaryDecision);
        Assert.Equal(
            "ClosedPhase14PositiveCompilerEmissionHandoffPackage",
            MatrixTileRuntimeIsaPackageContract.Phase13PositiveCompilerEmissionHandoffDecision);
        Assert.Equal(
            "SupersededPhase13ReadinessWasSuspendedUntilPhase14Evidence",
            MatrixTileRuntimeIsaPackageContract.Phase13BlockingReason);

        Assert.Throws<InvalidOperationException>(
            MatrixTileRuntimeIsaPackageContract.RequireCompilerVisibleNoEmissionBoundaryReadiness);

        Assert.False(MatrixTileRuntimeIsaPackageContract.IsReadyForCompilerVisibleNoEmissionBoundary);
        Assert.False(MatrixTileRuntimeIsaPackageContract.AllowsCompilerVisibleNoEmissionBoundaryWork);
        Assert.True(MatrixTileRuntimeIsaPackageContract.BlocksCompilerVisibleNoEmissionBoundary);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForPositiveCompilerEmissionHandoff);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksPositiveCompilerEmission);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksCompilerHelperIrSelectionEmission);

        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string phase13Path = Path.Combine(
            repoRoot,
            MatrixTileRuntimeIsaPackageContract.Phase13RuntimeIsaReadinessForCompilerHandoffPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(phase13Path))
        {
            return;
        }

        string phase13 = File.ReadAllText(phase13Path);

        foreach (string requiredText in new[]
                 {
                     "Status: closed",
                     "positive status/catalog promotion",
                     "positive compiler emission handoff package",
                     "No compiler code is edited by Phase 13 or Phase 14",
                     "current compiler implementation already exists in this checkout"
                 })
        {
            Assert.Contains(requiredText, phase13, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_AccumulatorTransposeAndVlmPool_IsReadyForCompilerHandoff()
    {
        Assert.Equal(
            "ClosedAccumulatorTileAbi",
            MatrixTileRuntimeIsaPackageContract.AccumulatorTileAbiGateDecision);
        Assert.Equal(
            "ClosedTransposePolicyAbi",
            MatrixTileRuntimeIsaPackageContract.TransposePolicyAbiGateDecision);
        Assert.Equal(
            "ClosedRuntimeOwnedMatrixTileVlmRows",
            MatrixTileRuntimeIsaPackageContract.VectorLegalityMatrixGateDecision);
        Assert.Equal(
            "RuntimeHandoffConsumedByExistingPositiveCompilerImplementationCompilerCodeUnchangedByPhase14",
            MatrixTileRuntimeIsaPackageContract.CompilerUpdateReadinessDecision);

        Assert.Empty(MatrixTileRuntimeIsaPackageContract.AccumulatorTileAbiBlockers);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.TransposePolicyAbiBlockers);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.VectorLegalityMatrixBlockers);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase05RuntimeOwnedVlmRows);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase05KeepsExecutableContoursClosed);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_RuntimePipelinePool_ClosesThroughGoldenAndNoFallbackEvidence()
    {
        Assert.Equal(
            "ClosedMatrixTileRuntimePipelineThroughPositiveGoldens",
            MatrixTileRuntimeIsaPackageContract.RuntimePipelineGateDecision);
        Assert.Equal(
            "ClosedMatrixTileDecoderEncoderAbi",
            MatrixTileRuntimeIsaPackageContract.DecoderEncoderGateDecision);
        Assert.Equal(
            "ClosedInstructionIrTileDescriptorProjection",
            MatrixTileRuntimeIsaPackageContract.InstructionIrProjectionGateDecision);
        Assert.Equal(
            "ClosedMatrixTileMaterializerFactories",
            MatrixTileRuntimeIsaPackageContract.RegistryMaterializerGateDecision);
        Assert.Equal(
            "ClosedOperationSpecificMatrixTileMicroOpCarrier",
            MatrixTileRuntimeIsaPackageContract.TypedTileMicroOpGateDecision);
        Assert.Equal(
            "ClosedMatrixTileMemoryLane6AndComputeLanePlacement",
            MatrixTileRuntimeIsaPackageContract.SchedulerLaneBindingGateDecision);
        Assert.Equal(
            "ClosedMatrixTileExecuteCaptureSemantics",
            MatrixTileRuntimeIsaPackageContract.ExecuteCaptureGateDecision);
        Assert.Equal(
            "ClosedMatrixTileRetirePublicationAndCommit",
            MatrixTileRuntimeIsaPackageContract.RetirePublicationGateDecision);
        Assert.Equal(
            "ClosedMatrixTileReplayRollbackConformance",
            MatrixTileRuntimeIsaPackageContract.ReplayRollbackGateDecision);
        Assert.Equal(
            "ClosedMatrixTilePositiveExecutableGoldenArtifacts",
            MatrixTileRuntimeIsaPackageContract.GoldenArtifactsGateDecision);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.RuntimePipelineBlockers);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_FirstGatePool_ClosesTileExecutionSnapshotModel()
    {
        Assert.Equal(
            "ClosedExecuteCaptureTileStateSnapshotModel",
            MatrixTileRuntimeIsaPackageContract.TileExecutionModelGateDecision);
        Assert.Equal(
            "ClosedCanonicalTileDescriptorAbi",
            MatrixTileRuntimeIsaPackageContract.TileDescriptorAbiGateDecision);
        Assert.Equal(
            "ClosedTileMemoryShapeAndFaultAbi",
            MatrixTileRuntimeIsaPackageContract.MemoryFaultModelGateDecision);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTileDescriptorAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesMemoryShapeFaultModel);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTileExecutionModel);

        Assert.Empty(MatrixTileRuntimeIsaPackageContract.TileExecutionModelBlockers);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.TileDescriptorAbiBlockers);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.MemoryFaultModelBlockers);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_LeafMarkersDoNotPublishExecutableOrReplayAuthority()
    {
        AssertLeafPipelineFailClosed(
            MtileLoadInstruction.Mnemonic,
            MtileLoadInstruction.IsExecutable,
            MtileLoadInstruction.CompilerHelperAllowed,
            MtileLoadInstruction.NoDecoderEncoderAbiPublication,
            MtileLoadInstruction.NoInstructionIrProjectionPublication,
            MtileLoadInstruction.NoRegistryMaterializerPublication,
            MtileLoadInstruction.NoTypedMicroOpPublication,
            MtileLoadInstruction.NoSchedulerLaneBindingPublication,
            MtileLoadInstruction.NoExecutionCapturePublication,
            MtileLoadInstruction.NoRetireWritebackPublication,
            MtileLoadInstruction.NoReplayRollbackPublication,
            MtileLoadInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileStoreInstruction.Mnemonic,
            MtileStoreInstruction.IsExecutable,
            MtileStoreInstruction.CompilerHelperAllowed,
            MtileStoreInstruction.NoDecoderEncoderAbiPublication,
            MtileStoreInstruction.NoInstructionIrProjectionPublication,
            MtileStoreInstruction.NoRegistryMaterializerPublication,
            MtileStoreInstruction.NoTypedMicroOpPublication,
            MtileStoreInstruction.NoSchedulerLaneBindingPublication,
            MtileStoreInstruction.NoExecutionCapturePublication,
            MtileStoreInstruction.NoRetireWritebackPublication,
            MtileStoreInstruction.NoReplayRollbackPublication,
            MtileStoreInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtileMaccInstruction.Mnemonic,
            MtileMaccInstruction.IsExecutable,
            MtileMaccInstruction.CompilerHelperAllowed,
            MtileMaccInstruction.NoDecoderEncoderAbiPublication,
            MtileMaccInstruction.NoInstructionIrProjectionPublication,
            MtileMaccInstruction.NoRegistryMaterializerPublication,
            MtileMaccInstruction.NoTypedMicroOpPublication,
            MtileMaccInstruction.NoSchedulerLaneBindingPublication,
            MtileMaccInstruction.NoExecutionCapturePublication,
            MtileMaccInstruction.NoRetireWritebackPublication,
            MtileMaccInstruction.NoReplayRollbackPublication,
            MtileMaccInstruction.RequiresGoldenArtifacts);

        AssertLeafPipelineFailClosed(
            MtransposeInstruction.Mnemonic,
            MtransposeInstruction.IsExecutable,
            MtransposeInstruction.CompilerHelperAllowed,
            MtransposeInstruction.NoDecoderEncoderAbiPublication,
            MtransposeInstruction.NoInstructionIrProjectionPublication,
            MtransposeInstruction.NoRegistryMaterializerPublication,
            MtransposeInstruction.NoTypedMicroOpPublication,
            MtransposeInstruction.NoSchedulerLaneBindingPublication,
            MtransposeInstruction.NoExecutionCapturePublication,
            MtransposeInstruction.NoRetireWritebackPublication,
            MtransposeInstruction.NoReplayRollbackPublication,
            MtransposeInstruction.RequiresGoldenArtifacts);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_VlmAndClassifierMetadataDoNotCreateExecutionAuthority()
    {
        Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.MTILE_LOAD));
        Assert.Equal(InstructionClass.Memory, InstructionClassifier.GetClass(InstructionsEnum.MTILE_STORE));
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.MTILE_MACC));
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.MTRANSPOSE));

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            var opcode = Enum.Parse<InstructionsEnum>(mnemonic);

            AssertDescriptorOnlyMatrixTileVlmRow(opcode);
            Assert.True(VectorLegalityMatrix.TryGetAddressingStatus(
                opcode,
                indexed: false,
                is2D: false,
                out VectorContourLegalityStatus status));
            Assert.Equal(VectorContourLegalityStatus.FailClosed, status);
        }
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_MaccAndTransposeLeafMarkersKeepSemanticAbiUnopened()
    {
        Assert.True(MtileMaccInstruction.RequiresAccumulatorTileAbi);
        Assert.True(MtileMaccInstruction.RequiresVectorLegalityMatrixClosure);
        Assert.True(MtileMaccInstruction.RequiresRetireStagedPublication);
        Assert.True(MtileMaccInstruction.RequiresReplayRollbackConformance);
        Assert.True(MtileMaccInstruction.RequiresGoldenArtifacts);
        Assert.True(MtileMaccInstruction.NoHiddenScalarLowering);
        Assert.True(MtileMaccInstruction.NoHiddenVectorLowering);
        Assert.True(MtileMaccInstruction.IsExecutable);

        Assert.True(MtransposeInstruction.RequiresTransposeTilePolicyAbi);
        Assert.True(MtransposeInstruction.RequiresVectorLegalityMatrixClosure);
        Assert.True(MtransposeInstruction.RequiresRetireStagedPublication);
        Assert.True(MtransposeInstruction.RequiresReplayRollbackConformance);
        Assert.True(MtransposeInstruction.RequiresGoldenArtifacts);
        Assert.True(MtransposeInstruction.NoHiddenScalarLowering);
        Assert.True(MtransposeInstruction.NoHiddenVectorLowering);
        Assert.True(MtransposeInstruction.IsExecutable);
    }

    [Fact]
    public void MatrixTileRuntimeIsaPackage_ExternalDescriptorsAreEvidenceButNotMtileAuthority()
    {
        foreach (string source in new[]
                 {
                     "Lane6Dsc2Tile2DParser",
                     "Lane7MatMulDescriptor",
                     "Lane7AcceleratorTopology",
                     "VectorTranspose",
                     "VectorSegmentMemory",
                     "ScopedVdotWide"
                 })
        {
            Assert.Contains(source, MatrixTileRuntimeIsaPackageContract.ExternalEvidenceNonAuthoritySources);
        }

        foreach (string mnemonic in MatrixTileMnemonics)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            AssertDescriptorOnlyMatrixTileVlmRow(Enum.Parse<InstructionsEnum>(mnemonic));
        }
    }

    [Theory]
    [InlineData("MTILE_LOAD", "NonePhase14Closed", true, false, false)]
    [InlineData("MTILE_STORE", "NonePhase14Closed", true, false, false)]
    [InlineData("MTILE_MACC", "NonePhase14Closed", false, true, false)]
    [InlineData("MTRANSPOSE", "NonePhase14Closed", false, false, true)]
    public void MatrixTileRuntimeIsaPackage_AllowsExecutableAuthorityAfterPrimaryGateCloses(
        string mnemonic,
        string primaryBlockedGate,
        bool expectsMemoryGate,
        bool expectsAccumulatorGate,
        bool expectsTransposeGate)
    {
        MatrixTileRuntimeIsaPackageRow row =
            MatrixTileRuntimeIsaPackageContract.GetRow(mnemonic);

        Assert.Equal(primaryBlockedGate, row.PrimaryBlockedGate);
        Assert.Equal(expectsMemoryGate, row.RequiresTileMemoryShapeFaultModel);
        Assert.Equal(expectsAccumulatorGate, row.RequiresAccumulatorTileAbi);
        Assert.Equal(expectsTransposeGate, row.RequiresTransposeTilePolicyAbi);
        Assert.Equal(mnemonic != "MTILE_STORE", row.RequiresRetireStagedPublication);
        Assert.Equal(mnemonic == "MTILE_STORE", row.RequiresRetireStagedCommit);

        MatrixTileRuntimeIsaPackageContract.RequireExecutableAuthority(mnemonic);
    }

    private static VLIW_Instruction CreateLegalMatrixTileInstruction(InstructionsEnum opcode)
    {
        return opcode switch
        {
            InstructionsEnum.MTILE_LOAD or InstructionsEnum.MTILE_STORE =>
                InstructionEncoder.EncodeVector1D(
                    (uint)opcode,
                    DataTypeEnum.INT32,
                    destSrc1Ptr: 0x1000,
                    src2Ptr: 0x2000,
                    streamLength: 4,
                    stride: 16),
            InstructionsEnum.MTILE_MACC or InstructionsEnum.MTRANSPOSE =>
                InstructionEncoder.EncodeVector2D(
                    (uint)opcode,
                    DataTypeEnum.INT32,
                    destSrc1Ptr: 0x1000,
                    src2Ptr: 0x2000,
                    streamLength: 8,
                    colStride: 4,
                    rowStride: 32,
                    rowLength: 4),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static DecoderContext CreateValidMatrixTileDecoderContext(InstructionsEnum opcode)
    {
        return new DecoderContext
        {
            OpCode = (uint)opcode,
            Immediate = 2,
            HasImmediate = true,
            DataType = (byte)DataTypeEnum.INT8,
            HasDataType = true,
            VectorPrimaryPointer = 0x1000,
            VectorSecondaryPointer = 0x0002,
            VectorStreamLength = 4,
            VectorStride = 1,
            VectorRowStride = 2,
            MatrixTileNumericPolicy = opcode == InstructionsEnum.MTILE_MACC
                ? MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                    MatrixTileNumericProfileId.SignedInt8ToInt32)
                : null,
            MatrixTileLayoutPolicy = opcode switch
            {
                InstructionsEnum.MTILE_MACC => MatrixTileLayoutPolicyAbi.CreateMaccPolicy(),
                InstructionsEnum.MTRANSPOSE => MatrixTileLayoutPolicyAbi.CreateTransposePolicy(),
                _ => null
            },
            HasVectorPayload = true,
            HasVectorAddressingContour = true,
            Is2DAddressing = true,
            IndexedAddressing = false,
            PredicateMask = 0
        };
    }

    private static InstructionIR DecodeMatrixTileInstruction(
        VliwDecoderV4 decoder,
        VLIW_Instruction instruction,
        InstructionsEnum opcode,
        ulong bundleSerial)
    {
        var slots = new VLIW_Instruction[8];
        slots[0] = instruction;
        var metadata = new InstructionSlotMetadata[8];
        for (int index = 0; index < metadata.Length; index++)
        {
            metadata[index] = InstructionSlotMetadata.Default;
        }

        if (opcode is InstructionsEnum.MTILE_MACC or InstructionsEnum.MTRANSPOSE)
        {
            metadata[0] = metadata[0] with
            {
                MatrixTileNumericPolicy = opcode == InstructionsEnum.MTILE_MACC
                    ? MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                        MatrixTileNumericProfileId.SignedInt32ToInt64)
                    : null,
                MatrixTileLayoutPolicy = opcode == InstructionsEnum.MTILE_MACC
                    ? MatrixTileLayoutPolicyAbi.CreateMaccPolicy()
                    : MatrixTileLayoutPolicyAbi.CreateTransposePolicy()
            };
        }

        DecodedInstructionBundle bundle = decoder.DecodeInstructionBundle(
            slots,
            new VliwBundleAnnotations(metadata),
            bundleAddress: 0x1000,
            bundleSerial: bundleSerial);
        return bundle.GetDecodedSlot(0).RequireInstruction();
    }

    private static void AssertDescriptorOnlyMatrixTileVlmRow(InstructionsEnum opcode)
    {
        Assert.True(MatrixTileRuntimeOwnedVlmRows.IsMatrixTileOpcode(opcode), opcode.ToString());
        Assert.True(MatrixTileRuntimeOwnedVlmRows.TryGetRuntimeOwnedRow(opcode, out VectorLegalityMatrixRow? row));
        Assert.NotNull(row);
        Assert.Equal("XMatrix", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.DescriptorOnly, row.DescriptorBacked);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.Reduction);
        Assert.DoesNotContain(VectorContourLegalityStatus.Executable, new[]
        {
            row.OneDimensional,
            row.IndexedAddressing,
            row.TwoDimensionalAddressing,
            row.Masked,
            row.TailMaskPolicy,
            row.Reduction,
            row.DescriptorBacked
        });
        Assert.False(VectorLegalityMatrix.AllowsAddressingExecution(
            opcode,
            indexed: false,
            is2D: false));
    }

    private static bool HasEnum(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return Enum.GetNames<InstructionsEnum>().Contains(enumCandidate);
    }

    private static bool HasIsaOpcodeValue(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        return typeof(Processor.CPU_Core.IsaOpcodeValues).GetField(enumCandidate) is not null;
    }

    private static bool HasRegistryMnemonic(string mnemonic) =>
        OpcodeRegistry.Opcodes.Any(info =>
            string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));

    private static void AssertLeafPipelineFailClosed(
        string mnemonic,
        bool isExecutable,
        bool compilerHelperAllowed,
        bool noDecoderEncoderAbiPublication,
        bool noInstructionIrProjectionPublication,
        bool noRegistryMaterializerPublication,
        bool noTypedMicroOpPublication,
        bool noSchedulerLaneBindingPublication,
        bool noExecutionCapturePublication,
        bool noRetireWritebackPublication,
        bool noReplayRollbackPublication,
        bool requiresGoldenArtifacts)
    {
        Assert.Contains(mnemonic, MatrixTileMnemonics);
        Assert.True(isExecutable);
        Assert.False(compilerHelperAllowed);
        Assert.True(noDecoderEncoderAbiPublication);
        Assert.False(noInstructionIrProjectionPublication);
        Assert.False(noRegistryMaterializerPublication);
        Assert.False(noTypedMicroOpPublication);
        Assert.False(noSchedulerLaneBindingPublication);
        Assert.False(noExecutionCapturePublication);
        Assert.False(noRetireWritebackPublication);
        Assert.False(noReplayRollbackPublication);
        Assert.True(requiresGoldenArtifacts);
    }
}
