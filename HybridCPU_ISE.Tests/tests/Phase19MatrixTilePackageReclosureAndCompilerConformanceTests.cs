using System;
using System.IO;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTilePackageReclosureAndCompilerConformanceTests
{
    [Fact]
    public void RuntimeNumericEvidenceAndCompilerSidebandConformanceClosePackageReclosure()
    {
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasExplicitNumericPolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase15SupportedNumericProfileMatrix);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase16FormalMaccArithmetic);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase16LayoutPolicyAbi);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase17PolicyBoundCaptureIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase17PolicyBoundReplayIdentity);
        Assert.True(MatrixTileRuntimeIsaPackageContract.HasPhase18MachineReadableNumericLayoutGoldenCorpus);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19RuntimeNumericEvidenceReady);

        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerCarrierConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerNoFallbackConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerRuntimeRejectionConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19CompilerSidebandConformanceReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.Phase19PackageReclosureReady);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitivePackageReadiness);
        Assert.True(MatrixTileRuntimeIsaPackageContract.NumericSensitiveClosesGoldenArtifacts);
        Assert.True(MatrixTileRuntimeIsaPackageContract.PositiveNumericHandoffReady);
        Assert.False(MatrixTileRuntimeIsaPackageContract.StatusCatalogPromotionIsProvisionalDuringNumericReclosure);
        Assert.False(MatrixTileRuntimeIsaPackageContract.Phase19RequiresCompilerOwnedSidebandBridge);
        Assert.Equal(
            "ClosedCompilerMatrixTileLoweredAnnotationsCarryNumericLayoutPolicySidebands",
            MatrixTileRuntimeIsaPackageContract.Phase19CompilerConformanceDecision);
        Assert.Equal(
            "NonePhase19CompilerSidebandConformanceClosed",
            MatrixTileRuntimeIsaPackageContract.Phase19CompilerConformanceBlocker);

        MatrixTileRuntimeIsaPackageContract.RequirePositiveCompilerEmissionReadiness();
    }

    [Fact]
    public void CompilerLoweredAnnotationsCarryRuntimeOwnedNumericAndLayoutPolicySidebands()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string runtimeMetadataPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Contracts",
            "CompilerTransport",
            "InstructionSlotMetadata.cs");
        string runtimeDecoderPath = Path.Combine(
            repoRoot,
            "HybridCPU_ISE",
            "CloseToRTL",
            "Core",
            "Frontend",
            "Decode",
            "VliwDecoderV4Bridge",
            "VliwDecoderV4.cs");
        string compilerBundleLowererPath = Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "Core",
            "IR",
            "Bundling",
            "HybridCpuBundleLowerer.cs");
        string compilerThreadMatrixTilePath = Path.Combine(
            repoRoot,
            "HybridCPU_Compiler",
            "API",
            "Threading",
            "ThreadCompilerContext.MatrixTile.cs");

        string runtimeMetadata = File.ReadAllText(runtimeMetadataPath);
        string runtimeDecoder = File.ReadAllText(runtimeDecoderPath);
        string compilerBundleLowerer = File.ReadAllText(compilerBundleLowererPath);
        string compilerThreadMatrixTile = File.ReadAllText(compilerThreadMatrixTilePath);

        Assert.Contains("MatrixTileNumericPolicy", runtimeMetadata, StringComparison.Ordinal);
        Assert.Contains("MatrixTileLayoutPolicy", runtimeMetadata, StringComparison.Ordinal);
        Assert.Contains("requireExplicitNumericPolicy: true", runtimeDecoder, StringComparison.Ordinal);
        Assert.Contains(
            "MatrixTileNumericPolicy = slotMetadata.MatrixTileNumericPolicy",
            runtimeDecoder,
            StringComparison.Ordinal);
        Assert.Contains(
            "MatrixTileLayoutPolicy = slotMetadata.MatrixTileLayoutPolicy",
            runtimeDecoder,
            StringComparison.Ordinal);

        Assert.Contains("MatrixTileEmission", compilerBundleLowerer, StringComparison.Ordinal);
        Assert.Contains("DmaStreamComputeDescriptor = instruction.DmaStreamComputeDescriptor", compilerBundleLowerer, StringComparison.Ordinal);
        Assert.Contains("AcceleratorCommandDescriptor = instruction.AcceleratorCommandDescriptor", compilerBundleLowerer, StringComparison.Ordinal);
        Assert.Contains(
            "MatrixTileNumericPolicy = instruction.MatrixTileEmission?.MatrixTileNumericPolicy",
            compilerBundleLowerer,
            StringComparison.Ordinal);
        Assert.Contains(
            "MatrixTileLayoutPolicy = instruction.MatrixTileEmission?.MatrixTileLayoutPolicy",
            compilerBundleLowerer,
            StringComparison.Ordinal);
        Assert.Contains("MatrixTileNumericPolicy = plan.MatrixTileNumericPolicy", compilerThreadMatrixTile, StringComparison.Ordinal);
        Assert.Contains("MatrixTileLayoutPolicy = plan.MatrixTileLayoutPolicy", compilerThreadMatrixTile, StringComparison.Ordinal);
    }
}
