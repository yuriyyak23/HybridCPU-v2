using System;
using System.Collections.Generic;
using System.Linq;
using HybridCPU.Compiler.Core.IR;

namespace HybridCPU_ISE.Tests.TestHelpers;

public sealed class CompilerFailClosedEmissionRow
{
    public CompilerFailClosedEmissionRow(
        string mnemonic,
        string enumCandidate,
        string facadeHelperFragment,
        string? contractMetadataFragment = null,
        IReadOnlyList<string>? publicHelperFragments = null,
        IReadOnlyList<string>? compilerSourceFragments = null)
    {
        Mnemonic = mnemonic;
        EnumCandidate = enumCandidate;
        ContractMetadataFragment = contractMetadataFragment ?? mnemonic;
        FacadeHelperFragment = facadeHelperFragment;
        PublicHelperFragments = (publicHelperFragments ?? [facadeHelperFragment]).ToArray();
        CompilerSourceFragments = (compilerSourceFragments ??
            BuildCompilerSourceFragments(enumCandidate, PublicHelperFragments)).ToArray();
    }

    public string Mnemonic { get; }

    public string EnumCandidate { get; }

    public string ContractMetadataFragment { get; }

    public string FacadeHelperFragment { get; }

    public IReadOnlyList<string> PublicHelperFragments { get; }

    public IReadOnlyList<string> CompilerSourceFragments { get; }

    public void Deconstruct(
        out string mnemonic,
        out string enumCandidate,
        out string facadeHelperFragment)
    {
        mnemonic = Mnemonic;
        enumCandidate = EnumCandidate;
        facadeHelperFragment = FacadeHelperFragment;
    }

    public void Deconstruct(
        out string mnemonic,
        out string enumCandidate,
        out string contractMetadataFragment,
        out string facadeHelperFragment)
    {
        mnemonic = Mnemonic;
        enumCandidate = EnumCandidate;
        contractMetadataFragment = ContractMetadataFragment;
        facadeHelperFragment = FacadeHelperFragment;
    }

    private static string[] BuildCompilerSourceFragments(
        string enumCandidate,
        IReadOnlyList<string> helperFragments)
    {
        var fragments = new List<string>
        {
            $"InstructionsEnum.{enumCandidate}",
            $"IsaOpcodeValues.{enumCandidate}",
            $"OpcodeValues.{enumCandidate}"
        };

        foreach (string helperFragment in helperFragments)
        {
            fragments.Add($"Compile{helperFragment}");
            fragments.Add($"Emit{helperFragment}");
        }

        return fragments
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

public static class CompilerFailClosedEmissionInventory
{
    public static CompilerFailClosedEmissionRow PauseHint { get; } =
        Row("PAUSE", "PAUSE", "Pause");

    public static IReadOnlyList<CompilerFailClosedEmissionRow> VectorFixedPointSaturationRows { get; } =
    [
        Row("VSUB.SAT", "VSUB_SAT", "VsubSat", helpers: ["VsubSat", "VectorSubtractSaturating", "SaturatingSubtract", "SaturatingVectorSubtract"]),
        Row("VMUL.SAT", "VMUL_SAT", "VmulSat", helpers: ["VmulSat", "VectorMultiplySaturating", "SaturatingMultiply", "SaturatingVectorMultiply"]),
        Row("VSLL.SAT", "VSLL_SAT", "VsllSat", helpers: ["VsllSat", "VectorShiftLeftSaturating", "SaturatingShiftLeft", "SaturatingVectorShiftLeft"]),
        Row("VSRL.SAT", "VSRL_SAT", "VsrlSat", helpers: ["VsrlSat", "VectorShiftRightLogicalSaturating", "SaturatingShiftRightLogical", "SaturatingVectorShiftRightLogical"]),
        Row("VSRA.SAT", "VSRA_SAT", "VsraSat", helpers: ["VsraSat", "VectorShiftRightArithmeticSaturating", "SaturatingShiftRightArithmetic", "SaturatingVectorShiftRightArithmetic"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> VectorFixedPointAverageClipRows { get; } =
    [
        Row("VAVG", "VAVG", "Vavg", helpers: ["Vavg", "VectorAverage", "FixedPointAverage", "AverageVector"]),
        Row("VAVG.R", "VAVG_R", "VavgR", helpers: ["VavgR", "VectorAverageRounded", "RoundedVectorAverage", "FixedPointRoundedAverage"]),
        Row("VCLIP", "VCLIP", "Vclip", helpers: ["Vclip", "VectorClip", "FixedPointClip", "ClipVector"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> VectorDotTileVariantRows { get; } =
    [
        Row("VDOT.BLOCKSCALE", "VDOT_BLOCKSCALE", "VdotBlockscale", helpers: ["VdotBlockscale", "VdotBlockScale", "BlockscaleDot", "BlockScaledDot"]),
        Row("VDOT.ACCUM", "VDOT_ACCUM", "VdotAccum", helpers: ["VdotAccum", "DotAccum", "VectorDotAccum"]),
        Row("VDOT.WIDE.I16", "VDOT_WIDE_I16", "VdotWideI16", helpers: ["VdotWideI16", "DotWideI16", "VectorDotWideI16"]),
        Row("VDOT.WIDE.I32", "VDOT_WIDE_I32", "VdotWideI32", helpers: ["VdotWideI32", "DotWideI32", "VectorDotWideI32"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> MatrixTileOptionalDisabledRows { get; } =
        [];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> MatrixTilePositiveEmissionRows { get; } =
    [
        Row(
            "MTILE_LOAD",
            "MTILE_LOAD",
            "MtileLoad",
            contractMetadataFragment: CompilerMatrixTilePositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            helpers: ["MtileLoad", "CompileMtileLoad"]),
        Row(
            "MTILE_STORE",
            "MTILE_STORE",
            "MtileStore",
            contractMetadataFragment: CompilerMatrixTilePositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            helpers: ["MtileStore", "CompileMtileStore"]),
        Row(
            "MTILE_MACC",
            "MTILE_MACC",
            "MtileMacc",
            contractMetadataFragment: CompilerMatrixTilePositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            helpers: ["MtileMacc", "CompileMtileMacc"]),
        Row(
            "MTRANSPOSE",
            "MTRANSPOSE",
            "Mtranspose",
            contractMetadataFragment: CompilerMatrixTilePositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            helpers: ["Mtranspose", "CompileMtranspose"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> VectorTransferPositiveEmissionRows { get; } =
    [
        new CompilerFailClosedEmissionRow(
            "VLOAD",
            "VLOAD",
            "VLoad",
            contractMetadataFragment: CompilerVectorTransferPositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            publicHelperFragments: ["VLoad", "CompileVload"],
            compilerSourceFragments:
            [
                "InstructionsEnum.VLOAD",
                "CompileVload",
                "VLoad",
                CompilerVectorTransferPositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.RuntimeHandoffAuthorityDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.NoFallbackDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.RuntimeHandoffReference
            ]),
        new CompilerFailClosedEmissionRow(
            "VSTORE",
            "VSTORE",
            "VStore",
            contractMetadataFragment: CompilerVectorTransferPositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
            publicHelperFragments: ["VStore", "CompileVstore"],
            compilerSourceFragments:
            [
                "InstructionsEnum.VSTORE",
                "CompileVstore",
                "VStore",
                CompilerVectorTransferPositiveEmissionAbiContract.CompilerPositiveEmissionDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.RuntimeHandoffAuthorityDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.NoFallbackDecision,
                CompilerVectorTransferPositiveEmissionAbiContract.RuntimeHandoffReference
            ])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> VectorVlmBlockedRows { get; } =
    [
        Row("VMERGE", "VMERGE", "Vmerge"),
        Row("VSELECT", "VSELECT", "Vselect"),
        Row("VFIRST", "VFIRST", "Vfirst"),
        Row("VANY", "VANY", "Vany"),
        Row("VALL", "VALL", "Vall"),
        Row("VMSIF", "VMSIF", "Vmsif"),
        Row("VMSOF", "VMSOF", "Vmsof"),
        Row("VSEXT", "VSEXT", "Vsext"),
        .. VectorFixedPointSaturationRows,
        .. VectorFixedPointAverageClipRows,
        .. VectorDotTileVariantRows,
        Row("VWADD", "VWADD", "Vwadd"),
        Row("VWADDU", "VWADDU", "Vwaddu"),
        Row("VWSUB", "VWSUB", "Vwsub"),
        Row("VWSUBU", "VWSUBU", "Vwsubu"),
        Row("VWMUL", "VWMUL", "Vwmul"),
        Row("VWMULU", "VWMULU", "Vwmulu"),
        Row("VWMACC", "VWMACC", "Vwmacc"),
        Row("VNSRL", "VNSRL", "Vnsrl"),
        Row("VNSRA", "VNSRA", "Vnsra"),
        Row("VCVT.I", "VCVT_I", "VcvtI"),
        Row("VCVT.U", "VCVT_U", "VcvtU"),
        Row("VCVT.F", "VCVT_F", "VcvtF"),
        Row("VSCAN.MIN", "VSCAN_MIN", "VscanMin"),
        Row("VSCAN.MAX", "VSCAN_MAX", "VscanMax"),
        Row("VZIP", "VZIP", "Vzip"),
        Row("VUNZIP", "VUNZIP", "Vunzip"),
        Row("VINTERLEAVE", "VINTERLEAVE", "Vinterleave"),
        Row("VDEINTERLEAVE", "VDEINTERLEAVE", "Vdeinterleave"),
        Row("VLDSEG2", "VLDSEG2", "Vldseg2"),
        Row("VLDSEG4", "VLDSEG4", "Vldseg4"),
        Row("VLDSEG8", "VLDSEG8", "Vldseg8"),
        Row("VSTSEG2", "VSTSEG2", "Vstseg2"),
        Row("VSTSEG4", "VSTSEG4", "Vstseg4"),
        Row("VSTSEG8", "VSTSEG8", "Vstseg8")
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane6DescriptorRows { get; } =
    [
        Row("DmaStreamCompute.SUB", "DmaStreamCompute_SUB", "DmaStreamComputeSub", helpers: ["DscSub", "DmaStreamComputeSub"]),
        Row("DmaStreamCompute.MIN", "DmaStreamCompute_MIN", "DmaStreamComputeMin", helpers: ["DscMin", "DmaStreamComputeMin"]),
        Row("DmaStreamCompute.MAX", "DmaStreamCompute_MAX", "DmaStreamComputeMax", helpers: ["DscMax", "DmaStreamComputeMax"]),
        Row("DmaStreamCompute.ABSDIFF", "DmaStreamCompute_ABSDIFF", "DmaStreamComputeAbsDiff", helpers: ["DscAbsDiff", "DmaStreamComputeAbsDiff"]),
        Row("DmaStreamCompute.CLAMP", "DmaStreamCompute_CLAMP", "DmaStreamComputeClamp", helpers: ["DscClamp", "DmaStreamComputeClamp"]),
        Row("DmaStreamCompute.CONVERT", "DmaStreamCompute_CONVERT", "DmaStreamComputeConvert", helpers: ["DscConvert", "DmaStreamComputeConvert"]),
        Row("DmaStreamCompute.COMPARE", "DmaStreamCompute_COMPARE", "DmaStreamComputeCompare", helpers: ["DscCompare", "DmaStreamComputeCompare"]),
        Row("DmaStreamCompute.SELECT", "DmaStreamCompute_SELECT", "DmaStreamComputeSelect", helpers: ["DscSelect", "DmaStreamComputeSelect"]),
        Row("DmaStreamCompute.REDUCE_SUM", "DmaStreamCompute_REDUCE_SUM", "DmaStreamComputeReduceSum", helpers: ["DscReduceSum", "DmaStreamComputeReduceSum"]),
        Row("DmaStreamCompute.REDUCE_MIN", "DmaStreamCompute_REDUCE_MIN", "DmaStreamComputeReduceMin", helpers: ["DscReduceMin", "DmaStreamComputeReduceMin"]),
        Row("DmaStreamCompute.REDUCE_MAX", "DmaStreamCompute_REDUCE_MAX", "DmaStreamComputeReduceMax", helpers: ["DscReduceMax", "DmaStreamComputeReduceMax"]),
        Row("DmaStreamCompute.REDUCE_AND", "DmaStreamCompute_REDUCE_AND", "DmaStreamComputeReduceAnd", helpers: ["DscReduceAnd", "DmaStreamComputeReduceAnd"]),
        Row("DmaStreamCompute.REDUCE_OR", "DmaStreamCompute_REDUCE_OR", "DmaStreamComputeReduceOr", helpers: ["DscReduceOr", "DmaStreamComputeReduceOr"]),
        Row("DmaStreamCompute.REDUCE_XOR", "DmaStreamCompute_REDUCE_XOR", "DmaStreamComputeReduceXor", helpers: ["DscReduceXor", "DmaStreamComputeReduceXor"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane6ShapeRows { get; } =
    [
        Row("DSC_SHAPE_STRIDED", "DSC_SHAPE_STRIDED", "DscShapeStrided", helpers: ["DscShapeStrided", "DscStrided", "DmaStreamComputeShapeStrided"]),
        Row("DSC_SHAPE_TILED", "DSC_SHAPE_TILED", "DscShapeTiled", helpers: ["DscShapeTiled", "DscTiled", "DmaStreamComputeShapeTiled"]),
        Row("DSC_SHAPE_SCATTER_GATHER", "DSC_SHAPE_SCATTER_GATHER", "DscShapeScatterGather", helpers: ["DscShapeScatterGather", "DscScatterGather", "DmaStreamComputeShapeScatterGather"]),
        Row("DSC_SHAPE_2D", "DSC_SHAPE_2D", "DscShape2D", helpers: ["DscShape2D", "Dsc2DShape", "DmaStreamComputeShape2D"]),
        Row("DSC_SHAPE_MULTI_RANGE", "DSC_SHAPE_MULTI_RANGE", "DscShapeMultiRange", helpers: ["DscShapeMultiRange", "DscMultiRange", "DmaStreamComputeShapeMultiRange"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane6QueueQueryDsc2Rows { get; } =
    [
        Row("DSC_POLL", "DSC_POLL", "DscPoll", helpers: ["DscPoll", "DmaStreamComputePoll"]),
        Row("DSC_WAIT", "DSC_WAIT", "DscWait", helpers: ["DscWait", "DmaStreamComputeWait"]),
        Row("DSC_CANCEL", "DSC_CANCEL", "DscCancel", helpers: ["DscCancel", "DmaStreamComputeCancel"]),
        Row("DSC_FENCE", "DSC_FENCE", "DscFence", helpers: ["DscFence", "DmaStreamComputeFence"]),
        Row("DSC_COMMIT", "DSC_COMMIT", "DscCommit", helpers: ["DscCommit", "DmaStreamComputeCommit"]),
        Row("DSC_QUERY_BACKEND", "DSC_QUERY_BACKEND", "DscQueryBackend", helpers: ["DscQueryBackend", "DmaStreamComputeQueryBackend"]),
        Row("DSC_QUERY_SHAPE", "DSC_QUERY_SHAPE", "DscQueryShape", helpers: ["DscQueryShape", "DmaStreamComputeQueryShape"]),
        Row("DSC2", "DSC2", "Dsc2", helpers: ["Dsc2", "DmaStreamComputeDsc2"])
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane6DeferredRows { get; } =
        Lane6DescriptorRows
            .Concat(Lane6ShapeRows)
            .Concat(Lane6QueueQueryDsc2Rows)
            .ToArray();

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane7CacheTlbIommuRows { get; } =
    [
        Row("SFENCE.VMA", "SFENCE_VMA", "SfenceVma"),
        Row("ICACHE_INVAL", "ICACHE_INVAL", "IcacheInval"),
        Row("DCACHE_CLEAN", "DCACHE_CLEAN", "DcacheClean"),
        Row("DCACHE_INVAL", "DCACHE_INVAL", "DcacheInval"),
        Row("DCACHE_FLUSH", "DCACHE_FLUSH", "DcacheFlush"),
        Row("IOTLB_INV", "IOTLB_INV", "IotlbInv"),
        Row("IOMMU_FENCE", "IOMMU_FENCE", "IommuFence")
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane7AcceleratorControlRows { get; } =
    [
        Row("ACCEL_QUERY_ABI", "ACCEL_QUERY_ABI", "AccelQueryAbi"),
        Row("ACCEL_QUERY_TOPOLOGY", "ACCEL_QUERY_TOPOLOGY", "AccelQueryTopology"),
        Row("ACCEL_OPEN", "ACCEL_OPEN", "AccelOpen"),
        Row("ACCEL_CLOSE", "ACCEL_CLOSE", "AccelClose"),
        Row("ACCEL_BIND_QUEUE", "ACCEL_BIND_QUEUE", "AccelBindQueue"),
        Row("ACCEL_UNBIND_QUEUE", "ACCEL_UNBIND_QUEUE", "AccelUnbindQueue")
    ];

    public static IReadOnlyList<CompilerFailClosedEmissionRow> Lane7DeferredRows { get; } =
        new[] { PauseHint }
            .Concat(Lane7CacheTlbIommuRows)
            .Concat(Lane7AcceleratorControlRows)
            .ToArray();

    public static IReadOnlyList<string> Lane6DescriptorMnemonics =>
        Lane6DescriptorRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> Lane6ShapeMnemonics =>
        Lane6ShapeRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> Lane6QueueControlMnemonics =>
        Lane6QueueQueryDsc2Rows
            .Where(static row => row.Mnemonic is "DSC_POLL" or "DSC_WAIT" or "DSC_CANCEL" or "DSC_FENCE" or "DSC_COMMIT")
            .Select(static row => row.Mnemonic)
            .ToArray();

    public static IReadOnlyList<string> Lane6QueryMnemonics =>
        Lane6QueueQueryDsc2Rows
            .Where(static row => row.Mnemonic is "DSC_QUERY_BACKEND" or "DSC_QUERY_SHAPE")
            .Select(static row => row.Mnemonic)
            .ToArray();

    public static IReadOnlyList<string> Lane7CacheTlbIommuMnemonics =>
        Lane7CacheTlbIommuRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> Lane7AcceleratorControlMnemonics =>
        Lane7AcceleratorControlRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> VectorFixedPointSaturationMnemonics =>
        VectorFixedPointSaturationRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> VectorFixedPointSaturationCompilerSourceFragments =>
        Unique(VectorFixedPointSaturationRows.SelectMany(static row => row.CompilerSourceFragments));

    public static IReadOnlyList<string> VectorFixedPointAverageClipMnemonics =>
        VectorFixedPointAverageClipRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> VectorFixedPointAverageClipCompilerSourceFragments =>
        Unique(VectorFixedPointAverageClipRows.SelectMany(static row => row.CompilerSourceFragments));

    public static IReadOnlyList<string> VectorDotTileVariantMnemonics =>
        VectorDotTileVariantRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> VectorDotTileVariantCompilerSourceFragments =>
        Unique(VectorDotTileVariantRows.SelectMany(static row => row.CompilerSourceFragments));

    public static IReadOnlyList<string> MatrixTileOptionalDisabledMnemonics =>
        MatrixTileOptionalDisabledRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> MatrixTileOptionalDisabledCompilerSourceFragments =>
        Unique(MatrixTileOptionalDisabledRows.SelectMany(static row => row.CompilerSourceFragments));

    public static IReadOnlyList<string> MatrixTilePositiveEmissionMnemonics =>
        MatrixTilePositiveEmissionRows.Select(static row => row.Mnemonic).ToArray();

    public static IReadOnlyList<string> Lane7CacheTlbIommuCompilerSourceFragments =>
        Unique(
            Lane7CacheTlbIommuRows.SelectMany(static row => row.CompilerSourceFragments)
                .Concat(
                [
                    "Lane7CacheMaintenanceHelper",
                    "Lane7TlbMaintenanceHelper",
                    "Lane7IommuMaintenanceHelper"
                ]));

    public static IReadOnlyList<string> Lane7AcceleratorControlCompilerSourceFragments =>
        Unique(
            Lane7AcceleratorControlRows.SelectMany(static row => row.CompilerSourceFragments)
                .Concat(
                [
                    "Lane7AcceleratorControlHelper",
                    "AcceleratorTopologyHelper",
                    "AcceleratorQueueBindingHelper"
                ]));

    private static CompilerFailClosedEmissionRow Row(
        string mnemonic,
        string enumCandidate,
        string facadeHelperFragment,
        string? contractMetadataFragment = null,
        IReadOnlyList<string>? helpers = null) =>
        new(
            mnemonic,
            enumCandidate,
            facadeHelperFragment,
            contractMetadataFragment,
            helpers);

    private static string[] Unique(IEnumerable<string> values) =>
        values
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
