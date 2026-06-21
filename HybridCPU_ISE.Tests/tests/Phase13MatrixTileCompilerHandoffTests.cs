using System;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileCompilerHandoffTests
{
    private static readonly string[] Mnemonics =
    [
        "MTILE_LOAD",
        "MTILE_STORE",
        "MTILE_MACC",
        "MTRANSPOSE"
    ];

    [Fact]
    public void StatusCatalogPublishesExecutableRuntimeAuthorityForAllRows()
    {
        foreach (string mnemonic in Mnemonics)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);

            Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
            Assert.Equal("XMatrix", status.ExtensionName);
            Assert.True(status.HasNumericOpcode);
            Assert.True(status.HasRuntimeOpcodeMetadata);
            Assert.True(status.HasCanonicalDecoderAcceptance);
            Assert.True(status.HasRegistryFactory);
            Assert.True(status.HasExecutionSemantics);
            Assert.True(status.IsExecutableClaim);
            Assert.Contains(mnemonic, IsaV4Surface.OptionalEnabledOpcodes);
            Assert.DoesNotContain(mnemonic, IsaV4Surface.OptionalDisabledOpcodes);

            MatrixTileRuntimeIsaPackageContract.RequireExecutableAuthority(mnemonic);
        }
    }

    [Fact]
    public void PositiveCompilerHandoffPackageIsTypedCompleteAndRuntimeOwned()
    {
        Assert.Equal(
            "ClosedPhase14PositiveCompilerEmissionHandoffPackage",
            MatrixTileCompilerEmissionHandoffPackage.HandoffDecision);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.HasPositiveStatusCatalogPromotion);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.HasPositiveCompilerEmissionHandoffPackage);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.RuntimeAuthorityReadyForSeparateCompilerImplementation);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.CurrentCompilerImplementationExists);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.CurrentCompilerHelperExists);
        Assert.True(MatrixTileCompilerEmissionHandoffPackage.CurrentCompilerEmissionExists);
        Assert.False(MatrixTileCompilerEmissionHandoffPackage.ModifiesCompilerCode);
        Assert.False(MatrixTileCompilerEmissionHandoffPackage.AllowsCompilerToOverrideRuntimeLegality);
        Assert.False(MatrixTileCompilerEmissionHandoffPackage.AllowsFallbackOrHiddenLowering);

        Assert.Equal(
            Mnemonics.Order(StringComparer.Ordinal),
            MatrixTileCompilerEmissionHandoffPackage.Rows
                .Select(static row => row.Mnemonic)
                .Order(StringComparer.Ordinal));

        foreach (MatrixTileCompilerEmissionHandoffRow row in MatrixTileCompilerEmissionHandoffPackage.Rows)
        {
            Assert.Equal(Enum.Parse<InstructionsEnum>(row.Mnemonic), row.Opcode);
            Assert.Equal(IsaInstructionStatus.OptionalEnabled, row.RuntimeStatus);
            Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, row.RuntimeEvidence);
            MatrixTileRuntimeResourceClass expectedClass =
                MatrixTileResourceContour.Classify((uint)row.Opcode);
            Assert.Equal(expectedClass, row.RuntimeResourceClass);
            Assert.Equal(
                MatrixTileResourceContour.ResolveSlotClass(expectedClass),
                row.RequiredSlotClass);
            Assert.Equal(
                SlotClassLaneMap.GetLaneMask(row.RequiredSlotClass),
                row.PhysicalLaneMask);
            Assert.True(row.HasRuntimeExecutableAuthority);
            Assert.True(row.RequiresDedicatedCompilerImplementation);
            Assert.True(row.HasCurrentCompilerHelper);
            Assert.True(row.HasCurrentCompilerEmission);
            Assert.False(string.IsNullOrWhiteSpace(row.RuntimePublicationKind));
        }
    }

    [Fact]
    public void RuntimeContractAndHandoffRowsCannotBeMutatedThroughPublishedArrays()
    {
        MatrixTileCompilerEmissionHandoffRow[] handoffRows =
            MatrixTileCompilerEmissionHandoffPackage.Rows;
        MatrixTileRuntimeIsaPackageRow[] packageRows =
            MatrixTileRuntimeIsaPackageContract.Rows;
        MatrixTilePhase09ExecuteCaptureDecisionRow[] phase09Rows =
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureDecisionRows;
        MatrixTilePhase10RetirePublicationDecisionRow[] phase10Rows =
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationDecisionRows;
        MatrixTilePhase11ReplayRollbackDecisionRow[] phase11Rows =
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackDecisionRows;
        MatrixTilePhase12GoldenArtifactDecisionRow[] phase12Rows =
            MatrixTileRuntimeIsaPackageContract.Phase12GoldenArtifactDecisionRows;
        MatrixTileRuntimeIsaClosureGateRow[] closureRows =
            MatrixTileRuntimeIsaPackageContract.ClosureGateRows;

        handoffRows[0] = default;
        packageRows[0] = default;
        phase09Rows[0] = default;
        phase10Rows[0] = default;
        phase11Rows[0] = default;
        phase12Rows[0] = default;
        closureRows[0] = default;

        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileCompilerEmissionHandoffPackage.GetRow("MTILE_LOAD").Mnemonic);
        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileRuntimeIsaPackageContract.GetRow("MTILE_LOAD").Mnemonic);
        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileRuntimeIsaPackageContract.Phase09ExecuteCaptureDecisionRows[0].Mnemonic);
        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileRuntimeIsaPackageContract.Phase10RetirePublicationDecisionRows[0].Mnemonic);
        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileRuntimeIsaPackageContract.Phase11ReplayRollbackDecisionRows[0].Mnemonic);
        Assert.Equal(
            "MTILE_LOAD",
            MatrixTileRuntimeIsaPackageContract.Phase12GoldenArtifactDecisionRows[0].Mnemonic);
        Assert.Equal(
            "status/catalog promotion",
            MatrixTileRuntimeIsaPackageContract.ClosureGateRows[0].GateName);
    }

    [Fact]
    public void RuntimePackageClosesNumericSensitiveCompilerHandoffAfterPhase19SidebandConformance()
    {
        Assert.Equal("Phase14", MatrixTileRuntimeIsaPackageContract.Phase);
        Assert.True(MatrixTileRuntimeIsaPackageContract.ClosesTileStreamResourceContour);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase14WasFailClosedDuringCorrection);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase14CompilerHandoffWasSuspendedUntilClosure);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutableRuntimeIsaClosure);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExecutableStatusCatalogPromotion);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForCompilerUpdate);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksCompilerUpdate);
        Assert.True(MatrixTileRuntimeIsaPackageContract.IsReadyForPositiveCompilerEmissionHandoff);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPositiveCompilerEmissionHandoffPackage);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCompilerHandoffPackage);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksPositiveCompilerEmission);
        Assert.False(MatrixTileRuntimeIsaPackageContract.BlocksCompilerHelperIrSelectionEmission);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasCurrentCompilerImplementation);
        Assert.False(MatrixTileRuntimeIsaPackageContract.HasRemainingRuntimeIsaOpenTasks);
        Assert.Empty(MatrixTileRuntimeIsaPackageContract.RemainingRuntimeIsaOpenTasks);
        Assert.All(MatrixTileRuntimeIsaPackageContract.ClosureGateRows, static row =>
        {
            Assert.True(row.IsExecutableClosure, row.GateName);
            Assert.False(row.BlocksCompilerUpdate, row.GateName);
        });

        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExplicitNumericPolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19RuntimeNumericEvidenceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerSidebandConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19PackageReclosureReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.PositiveNumericHandoffReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitivePackageReadiness);
        Assert.Equal(
            "ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands",
            MatrixTileRuntimeIsaPackageContract.Phase19CompilerConformanceDecision);
        MatrixTileRuntimeIsaPackageContract.RequireCompilerUpdateReadiness();
        MatrixTileRuntimeIsaPackageContract.RequirePositiveCompilerEmissionReadiness();
        Assert.Throws<InvalidOperationException>(
            MatrixTileRuntimeIsaPackageContract.RequireCompilerVisibleNoEmissionBoundaryReadiness);
    }

    [Fact]
    public void CurrentCompilerSurfacesRemainVisibleAndPositiveHandoffIsOpenAfterPhase19SidebandGate()
    {
        string compilerEmissionSurface = CompilerSourceScanner.ReadCompilerEmissionSurfaceSource();
        string[] publicCompilerMethods =
        [
            .. typeof(IAppAsmFacade).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name),
            .. typeof(AppAsmFacade).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name),
            .. typeof(HybridCpuThreadCompilerContext).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(static method => !method.IsSpecialName)
                .Select(static method => method.Name)
        ];

        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.HasCurrentCompilerImplementation);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.HasCurrentCompilerHelper);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.HasCurrentCompilerEmission);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.UsesPhase13RuntimeHandoff);
        Assert.True(CompilerMatrixTilePositiveEmissionAbiContract.RuntimeOwnedLegalityIsFinal);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.AllowsCompilerToOverrideRuntimeLegality);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesOldOptionalDisabledMetadataAsAuthority);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesFallbackPath);
        Assert.False(CompilerMatrixTilePositiveEmissionAbiContract.UsesAliasPromotion);

        foreach (CompilerMatrixTilePositiveEmissionRow row in CompilerMatrixTilePositiveEmissionAbiContract.Rows)
        {
            Assert.Equal(MatrixTileCompilerEmissionHandoffPackage.GetRow(row.Mnemonic).Opcode, row.Opcode);
            Assert.Contains(row.HelperName, publicCompilerMethods);
            Assert.Contains(row.HelperName, compilerEmissionSurface, StringComparison.Ordinal);
            Assert.Contains($"InstructionsEnum.{row.Opcode}", compilerEmissionSurface, StringComparison.Ordinal);
            CompilerMatrixTilePositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(row.Mnemonic);
        }

        Assert.False(MtileLoadInstruction.CompilerHelperAllowed);
        Assert.False(MtileStoreInstruction.CompilerHelperAllowed);
        Assert.False(MtileMaccInstruction.CompilerHelperAllowed);
        Assert.False(MtransposeInstruction.CompilerHelperAllowed);
        Assert.True(MtileLoadInstruction.NoCompilerHelperEmission);
        Assert.True(MtileStoreInstruction.NoCompilerHelperEmission);
        Assert.True(MtileMaccInstruction.NoCompilerHelperEmission);
        Assert.True(MtransposeInstruction.NoCompilerHelperEmission);
    }
}
