using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Contracts;
using YAKSys_Hybrid_CPU.Core.Vmcs.V2;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.CompilerTests;

public sealed class CompilerNoEmissionBoundaryTests
{
    private static readonly string[] ClosedHelperNameFragments =
    [
        "Atomic",
        "Amo",
        "LoadReserved",
        "StoreConditional",
        "Fence",
        "Sfence",
        "DCache",
        "ICache",
        "Tlb",
        "Matrix",
        "Mtile",
        "Cache",
        "Iotlb",
        "Iommu",
        "Seqz",
        "Snez",
        "Csel",
        "Czero",
        "Cpop",
        "Clz",
        "Ctz",
        "Popcnt",
        "PopulationCount",
        "Rol",
        "Ror",
        "Roli",
        "Rori",
        "Rev8",
        "Brev8",
        "RotateLeftImmediate",
        "RotateRightImmediate",
        "ReverseBytes",
        "ReverseBitsInByte",
        "Andn",
        "Orn",
        "Xnor",
        "SextB",
        "SextH",
        "ZextH",
        "SignExtendByte",
        "SignExtendHalf",
        "ZeroExtendHalf",
        "Bset",
        "Bclr",
        "Binv",
        "Bext",
        "Bseti",
        "Bclri",
        "Binvi",
        "Bexti",
        "BitSet",
        "BitClear",
        "BitInvert",
        "BitExtract",
        "Bitfield",
        "Sh1Add",
        "Sh2Add",
        "Sh3Add",
        "AddUw",
        "Sh1AddUw",
        "Sh2AddUw",
        "Sh3AddUw",
        "SlliUw",
        "RdCycle",
        "RdTime",
        "RdInstret",
        "Pause",
        "ClMul",
        "ClMulH",
        "ClMulR",
        "Crc32",
        "Crc64",
        "Adc",
        "Sbc",
        "Addc",
        "Subc",
        "AddWithCarry",
        "AddCarry",
        "SubtractWithBorrow",
        "SubWithBorrow",
        "SubBorrow",
        "VMerge",
        "VSelect",
        "VFirst",
        "VAny",
        "VAll",
        "Vmsbf",
        "Vmsif",
        "Vmsof",
        "Vzext",
        "Vscan",
        "Vldseg",
        "Vstseg",
        "Vzip",
        "Vunzip",
        "Vinterleave",
        "Vdeinterleave",
        "Saturating",
        "Saturate",
        "VaddSat",
        "SatAdd",
        "FixedPoint",
        "Vclip",
        "Vavg",
        "Widen",
        "Narrow",
        "Vwadd",
        "Vn",
        "Vcvt",
        "VdotAccum",
        "VdotBlockscale",
        "VdotWideI16",
        "VdotWideI32",
        "Blockscale",
        "BlockScaledDot",
        "DotAccum",
        "DscSub",
        "DscMin",
        "DscMax",
        "DscAbsDiff",
        "DscClamp",
        "DscConvert",
        "DscCompare",
        "DscSelect",
        "DscReduceSum",
        "DscReduceMin",
        "DscReduceMax",
        "DscReduceAnd",
        "DscReduceOr",
        "DscReduceXor",
        "DscShape",
        "DmaStreamComputeSub",
        "DmaStreamComputeConvert",
        "DmaStreamComputeReduceSum",
        "DscPoll",
        "DscWait",
        "DscCancel",
        "DscFence",
        "DscCommit",
        "DscQueryBackend",
        "DscQueryShape",
        "Dsc2",
        "DmaStreamComputePoll",
        "DmaStreamComputeWait",
        "DmaStreamComputeCancel",
        "DmaStreamComputeFence",
        "DmaStreamComputeCommit",
        "DmaStreamComputeQueryBackend",
        "DmaStreamComputeQueryShape",
        "DmaStreamComputeQueue",
        "DmaStreamComputeDsc2",
        "AcceleratorStatus",
        "AcceleratorGetResult",
        "AcceleratorReset",
        "AcceleratorQueryAbi",
        "AcceleratorQueryTopology",
        "AcceleratorTopology",
        "AcceleratorTensor",
        "AcceleratorOpen",
        "AcceleratorClose",
        "AcceleratorBindQueue",
        "AcceleratorUnbindQueue",
        "AcceleratorFft",
        "AcceleratorDsp",
        "AcceleratorCrypto",
        "AcceleratorCryptoHash",
        "AcceleratorHash",
        "AcceleratorCompression",
        "AcceleratorSparse",
        "AcceleratorSparseGraph",
        "AcceleratorGraph",
        "AcceleratorMedia",
        "Vmx",
        "Vmcs",
        "VmFunc",
        "VmRead",
        "VmWrite",
        "VmPtr",
        "VmLaunch",
        "VmResume",
        "DirtyLog",
        "Checkpoint",
        "Migration",
        "Observability",
        "DebugTrace",
        "TraceHandle",
        "Vmcs12",
        "Vmcs02"
    ];

    private static readonly (string ContourName, string[] PublicHelperFragments, string[] CompilerSourceFragments)[] RuntimeOnlyVectorContours =
    [
        ("VGATHER",
            ["VGather", "VectorGather", "GatherIndexed", "IndexedGather"],
            ["InstructionsEnum.VGATHER", "IsaOpcodeValues.VGATHER", "OpcodeValues.VGATHER", "VGATHER", "VectorGather", "GatherIndexed"]),
        ("VSCATTER",
            ["VScatter", "VectorScatter", "ScatterIndexed", "IndexedScatter"],
            ["InstructionsEnum.VSCATTER", "IsaOpcodeValues.VSCATTER", "OpcodeValues.VSCATTER", "VSCATTER", "VectorScatter", "ScatterIndexed"]),
        ("VMSBF",
            ["Vmsbf", "VectorMaskPrefix", "MaskPrefix", "MaskSetBeforeFirst"],
            ["InstructionsEnum.VMSBF", "IsaOpcodeValues.VMSBF", "OpcodeValues.VMSBF", "VMSBF", "VectorMaskPrefix", "MaskSetBeforeFirst"]),
        ("VZEXT",
            ["Vzext", "VectorZeroExtend", "VectorWiden", "VectorNarrow", "VectorConvert"],
            ["InstructionsEnum.VZEXT", "IsaOpcodeValues.VZEXT", "OpcodeValues.VZEXT", "VZEXT", "VectorZeroExtend", "VectorWiden", "VectorNarrow", "VectorConvert"]),
        ("VSCAN.SUM",
            ["Vscan", "VscanSum", "VectorScanSum", "ScanSum"],
            ["InstructionsEnum.VSCAN_SUM", "IsaOpcodeValues.VSCAN_SUM", "OpcodeValues.VSCAN_SUM", "VSCAN.SUM", "VSCAN_SUM", "VectorScanSum", "ScanSum"]),
        ("VADD.SAT",
            ["VaddSat", "VectorAddSaturating", "SaturatingAdd", "SaturatingVectorAdd"],
            ["VADD.SAT", "VaddSat", "VectorAddSaturating", "SaturatingAdd", "SaturatingVectorAdd"]),
        ("VSLIDE1UP",
            ["Vslide1Up", "Slide1Up", "SlideOneUp", "VectorSlide1Up"],
            ["InstructionsEnum.VSLIDE1UP", "IsaOpcodeValues.VSLIDE1UP", "OpcodeValues.VSLIDE1UP", "VSLIDE1UP", "SlideOneUp", "VectorSlide1Up"]),
        ("VDOT.WIDE",
            ["VdotWide", "DotWide", "VectorDotWide", "MixedPrecisionDot", "BlockScaledDot"],
            ["InstructionsEnum.VDOT_WIDE", "IsaOpcodeValues.VDOT_WIDE", "OpcodeValues.VDOT_WIDE", "VDOT.WIDE", "VDOT_WIDE", "DotWide", "MixedPrecisionDot", "BlockScaledDot"]),
        ("VDOT.BLOCKSCALE",
            ["VdotBlockscale", "VdotBlockScale", "BlockscaleDot", "BlockScaledDot"],
            ["VDOT.BLOCKSCALE", "VDOT_BLOCKSCALE", "VdotBlockscale", "VdotBlockScale", "BlockscaleDot", "BlockScaledDot"]),
        ("VDOT.ACCUM",
            ["VdotAccum", "DotAccum", "VectorDotAccum"],
            ["VDOT.ACCUM", "VDOT_ACCUM", "VdotAccum", "DotAccum", "VectorDotAccum"]),
        ("VDOT.WIDE.I16",
            ["VdotWideI16", "DotWideI16", "VectorDotWideI16"],
            ["VDOT.WIDE.I16", "VDOT_WIDE_I16", "VdotWideI16", "DotWideI16", "VectorDotWideI16"]),
        ("VDOT.WIDE.I32",
            ["VdotWideI32", "DotWideI32", "VectorDotWideI32"],
            ["VDOT.WIDE.I32", "VDOT_WIDE_I32", "VdotWideI32", "DotWideI32", "VectorDotWideI32"]),
        ("VSLIDE1DOWN",
            ["Vslide1Down", "Slide1Down", "SlideOneDown", "VectorSlide1Down"],
            ["InstructionsEnum.VSLIDE1DOWN", "IsaOpcodeValues.VSLIDE1DOWN", "OpcodeValues.VSLIDE1DOWN", "VSLIDE1DOWN", "SlideOneDown", "VectorSlide1Down"]),
        ("VPERM2",
            ["Vperm2", "Perm2", "VectorPermute2", "Permute2"],
            ["InstructionsEnum.VPERM2", "IsaOpcodeValues.VPERM2", "OpcodeValues.VPERM2", "VPERM2", "VectorPermute2", "Permute2"]),
        ("VTRANSPOSE",
            ["Vtranspose", "VectorTranspose", "Transpose"],
            ["InstructionsEnum.VTRANSPOSE", "IsaOpcodeValues.VTRANSPOSE", "OpcodeValues.VTRANSPOSE", "VTRANSPOSE", "VectorTranspose"])
    ];

    private static readonly (string ContourName, string[] PublicHelperFragments, string[] CompilerSourceFragments)[] RuntimeOnlyLane6DescriptorContours =
    [
        ("DmaStreamCompute.SUB",
            ["DscSub", "DmaStreamComputeSub"],
            ["DmaStreamCompute.SUB", "DmaStreamCompute_SUB", "DscSub", "DmaStreamComputeSub"]),
        ("DmaStreamCompute.MIN",
            ["DscMin", "DmaStreamComputeMin"],
            ["DmaStreamCompute.MIN", "DmaStreamCompute_MIN", "DscMin", "DmaStreamComputeMin"]),
        ("DmaStreamCompute.MAX",
            ["DscMax", "DmaStreamComputeMax"],
            ["DmaStreamCompute.MAX", "DmaStreamCompute_MAX", "DscMax", "DmaStreamComputeMax"]),
        ("DmaStreamCompute.ABSDIFF",
            ["DscAbsDiff", "DmaStreamComputeAbsDiff"],
            ["DmaStreamCompute.ABSDIFF", "DmaStreamCompute_ABSDIFF", "DscAbsDiff", "DmaStreamComputeAbsDiff"]),
        ("DmaStreamCompute.CLAMP",
            ["DscClamp", "DmaStreamComputeClamp"],
            ["DmaStreamCompute.CLAMP", "DmaStreamCompute_CLAMP", "DscClamp", "DmaStreamComputeClamp"]),
        ("DmaStreamCompute.CONVERT",
            ["DscConvert", "DmaStreamComputeConvert"],
            ["DmaStreamCompute.CONVERT", "DmaStreamCompute_CONVERT", "DscConvert", "DmaStreamComputeConvert"]),
        ("DmaStreamCompute.COMPARE",
            ["DscCompare", "DmaStreamComputeCompare"],
            ["DmaStreamCompute.COMPARE", "DmaStreamCompute_COMPARE", "DscCompare", "DmaStreamComputeCompare"]),
        ("DmaStreamCompute.SELECT",
            ["DscSelect", "DmaStreamComputeSelect"],
            ["DmaStreamCompute.SELECT", "DmaStreamCompute_SELECT", "DscSelect", "DmaStreamComputeSelect"]),
        ("DmaStreamCompute.REDUCE_SUM",
            ["DscReduceSum", "DmaStreamComputeReduceSum"],
            ["DmaStreamCompute.REDUCE_SUM", "DmaStreamCompute_REDUCE_SUM", "DscReduceSum", "DmaStreamComputeReduceSum"]),
        ("DmaStreamCompute.REDUCE_MIN",
            ["DscReduceMin", "DmaStreamComputeReduceMin"],
            ["DmaStreamCompute.REDUCE_MIN", "DmaStreamCompute_REDUCE_MIN", "DscReduceMin", "DmaStreamComputeReduceMin"]),
        ("DmaStreamCompute.REDUCE_MAX",
            ["DscReduceMax", "DmaStreamComputeReduceMax"],
            ["DmaStreamCompute.REDUCE_MAX", "DmaStreamCompute_REDUCE_MAX", "DscReduceMax", "DmaStreamComputeReduceMax"]),
        ("DmaStreamCompute.REDUCE_AND",
            ["DscReduceAnd", "DmaStreamComputeReduceAnd"],
            ["DmaStreamCompute.REDUCE_AND", "DmaStreamCompute_REDUCE_AND", "DscReduceAnd", "DmaStreamComputeReduceAnd"]),
        ("DmaStreamCompute.REDUCE_OR",
            ["DscReduceOr", "DmaStreamComputeReduceOr"],
            ["DmaStreamCompute.REDUCE_OR", "DmaStreamCompute_REDUCE_OR", "DscReduceOr", "DmaStreamComputeReduceOr"]),
        ("DmaStreamCompute.REDUCE_XOR",
            ["DscReduceXor", "DmaStreamComputeReduceXor"],
            ["DmaStreamCompute.REDUCE_XOR", "DmaStreamCompute_REDUCE_XOR", "DscReduceXor", "DmaStreamComputeReduceXor"]),
        ("DSC_SHAPE_*",
            ["DscShape", "DscStrided", "DscTiled", "DscScatterGather", "DscMultiRange"],
            ["DSC_SHAPE_", "DscShape", "DscStrided", "DscTiled", "DscScatterGather", "DscMultiRange"])
    ];

    private static readonly (string ContourName, string[] PublicHelperFragments, string[] CompilerSourceFragments)[] RuntimeOnlyLane6QueueQueryDsc2Contours =
    [
        ("DSC_POLL",
            ["DscPoll", "DmaStreamComputePoll"],
            ["DSC_POLL", "DscPoll", "DmaStreamComputePoll"]),
        ("DSC_WAIT",
            ["DscWait", "DmaStreamComputeWait"],
            ["DSC_WAIT", "DscWait", "DmaStreamComputeWait"]),
        ("DSC_CANCEL",
            ["DscCancel", "DmaStreamComputeCancel"],
            ["DSC_CANCEL", "DscCancel", "DmaStreamComputeCancel"]),
        ("DSC_FENCE",
            ["DscFence", "DmaStreamComputeFence"],
            ["DSC_FENCE", "DscFence", "DmaStreamComputeFence"]),
        ("DSC_COMMIT",
            ["DscCommit", "DmaStreamComputeCommit"],
            ["DSC_COMMIT", "DscCommit", "DmaStreamComputeCommit"]),
        ("DSC_QUERY_BACKEND",
            ["DscQueryBackend", "DmaStreamComputeQueryBackend"],
            ["DSC_QUERY_BACKEND", "DscQueryBackend", "DmaStreamComputeQueryBackend"]),
        ("DSC_QUERY_SHAPE",
            ["DscQueryShape", "DmaStreamComputeQueryShape"],
            ["DSC_QUERY_SHAPE", "DscQueryShape", "DmaStreamComputeQueryShape"]),
        ("DSC2",
            ["Dsc2", "DmaStreamComputeDsc2"],
            ["DSC2", "Dsc2", "DmaStreamComputeDsc2"])
    ];

    [Fact]
    public void PublicFacadeSurfaces_DoNotExposeClosedAtomicFenceMatrixCacheOrTlbHelpers()
    {
        string[] publicFacadeMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade))
        ];

        foreach (string methodName in publicFacadeMethods.Distinct(StringComparer.Ordinal))
        {
            Assert.DoesNotContain(
                ClosedHelperNameFragments,
                fragment => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Phase02UnaryBitCountCompilerContours_DoNotOpenPopcntAliasOrSelectRows()
    {
        string[] publicFacadeMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade))
        ];

        Assert.Contains(nameof(IAppAsmFacade.CountTrailingZeros), publicFacadeMethods);
        Assert.Contains(nameof(AppAsmFacade.CountTrailingZeros), publicFacadeMethods);
        Assert.Contains(nameof(IAppAsmFacade.CountSetBits), publicFacadeMethods);
        Assert.Contains(nameof(AppAsmFacade.CountSetBits), publicFacadeMethods);

        string[] forbiddenPublicFragments =
        [
            "Popcnt",
            "PopulationCount",
            "CountPopulation",
            "Seqz",
            "Snez",
            "Csel"
        ];
        foreach (string fragment in forbiddenPublicFragments)
        {
            Assert.DoesNotContain(
                publicFacadeMethods,
                methodName => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        string compilerSource = ReadAllCompilerSource();
        string[] forbiddenCompilerFragments =
        [
            "POPCNT",
            "CountPopulation",
            "PopulationCount",
            "SEQZ",
            "SNEZ",
            "CSEL"
        ];
        foreach (string fragment in forbiddenCompilerFragments)
        {
            Assert.DoesNotContain(fragment, compilerSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PlatformVectorFacade_RemainsRawTransportPlusScopedVsetvliOnly()
    {
        string[] platformMethods = PublicMethodNames(typeof(IPlatformAsmFacade));

        Assert.Contains(nameof(IPlatformAsmFacade.VectorOp), platformMethods);
        Assert.Contains(nameof(IPlatformAsmFacade.VectorOpImm), platformMethods);
        Assert.Contains(nameof(IPlatformAsmFacade.VSetVli), platformMethods);

        MethodInfo vectorOp = typeof(IPlatformAsmFacade).GetMethod(nameof(IPlatformAsmFacade.VectorOp))
            ?? throw new InvalidOperationException("IPlatformAsmFacade.VectorOp was not found.");
        MethodInfo vectorOpImm = typeof(IPlatformAsmFacade).GetMethod(nameof(IPlatformAsmFacade.VectorOpImm))
            ?? throw new InvalidOperationException("IPlatformAsmFacade.VectorOpImm was not found.");
        Assert.Equal(typeof(IsaOpcode), vectorOp.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(IsaOpcode), vectorOpImm.GetParameters()[0].ParameterType);

        Assert.DoesNotContain(platformMethods, name => name.Contains("Gather", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Scatter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Indexed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("2D", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("VectorLoad", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("VectorStore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("MaskPrefix", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Vmsbf", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Select", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Merge", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Vzext", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Widen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Narrow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(platformMethods, name => name.Contains("Convert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThreadCompilerPublicSurface_KeepsLane6AndLane7DescriptorCarrierBoundaries()
    {
        string[] threadMethods = PublicMethodNames(typeof(HybridCpuThreadCompilerContext));

        string[] lane6Methods = threadMethods
            .Where(name => name.Contains("DmaStreamCompute", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [nameof(HybridCpuThreadCompilerContext.CompileDmaStreamCompute),
             nameof(HybridCpuThreadCompilerContext.CompileDmaStreamComputeDescriptor)],
            lane6Methods);

        string[] lane7Methods = threadMethods
            .Where(name => name.Contains("Accelerator", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal([nameof(HybridCpuThreadCompilerContext.CompileAcceleratorSubmit)], lane7Methods);

        Assert.DoesNotContain(threadMethods, name => name.Contains("Production", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Dsc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("DmaStreamStatus", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("DmaStreamQuery", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("DmaStreamQueue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("DmaStreamToken", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("DmaStreamFence", StringComparison.OrdinalIgnoreCase));
        string[] closedLane7ThreadFragments =
        [
            "AcceleratorQuery",
            "AcceleratorPoll",
            "AcceleratorWait",
            "AcceleratorCancel",
            "AcceleratorFence",
            "AcceleratorStatus",
            "AcceleratorResult",
            "AcceleratorReset",
            "AcceleratorBackend",
            "AcceleratorFallback",
            "AcceleratorToken",
            "AcceleratorHandle",
            "AcceleratorTensor",
            "AcceleratorTopology",
            "AcceleratorFft",
            "AcceleratorCrypto",
            "AcceleratorHash",
            "AcceleratorSparse",
            "AcceleratorGraph"
        ];
        foreach (string fragment in closedLane7ThreadFragments)
        {
            Assert.DoesNotContain(
                threadMethods,
                name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(threadMethods, name => name.Contains("Fallback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Backend", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(threadMethods, name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuntimeOnlyVectorContours_DoNotPublishTypedCompilerHelperBackendOrFallbackSurface()
    {
        string[] publicCompilerMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade)),
            .. PublicMethodNames(typeof(HybridCpuThreadCompilerContext))
        ];

        foreach ((string contourName, string[] publicHelperFragments, _) in RuntimeOnlyVectorContours)
        {
            foreach (string fragment in publicHelperFragments)
            {
                Assert.DoesNotContain(
                    publicCompilerMethods,
                    methodName => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
            }

            Assert.DoesNotContain(
                publicCompilerMethods,
                methodName => methodName.Contains($"{contourName}Backend", StringComparison.OrdinalIgnoreCase) ||
                              methodName.Contains($"{contourName}Fallback", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void CompilerSources_DoNotReferenceRuntimeOnlyVectorContoursAsLoweringAuthority()
    {
        string compilerSource = ReadAllCompilerSource();

        foreach ((_, _, string[] compilerSourceFragments) in RuntimeOnlyVectorContours)
        {
            foreach (string fragment in compilerSourceFragments)
            {
                Assert.DoesNotContain(
                    fragment,
                    compilerSource,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void RuntimeOnlyLane6DescriptorContours_DoNotPublishTypedCompilerHelpersOrLoweringAuthority()
    {
        string[] publicCompilerMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade)),
            .. PublicMethodNames(typeof(HybridCpuThreadCompilerContext))
        ];
        string compilerSource = ReadAllCompilerSource();

        foreach ((_, string[] publicHelperFragments, string[] compilerSourceFragments) in RuntimeOnlyLane6DescriptorContours)
        {
            foreach (string fragment in publicHelperFragments)
            {
                Assert.DoesNotContain(
                    publicCompilerMethods,
                    methodName => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
            }

            foreach (string fragment in compilerSourceFragments)
            {
                Assert.DoesNotContain(
                    fragment,
                    compilerSource,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void RuntimeOnlyLane6QueueQueryDsc2Contours_DoNotPublishTypedCompilerHelpersOrLoweringAuthority()
    {
        string[] publicCompilerMethods =
        [
            .. PublicMethodNames(typeof(IAppAsmFacade)),
            .. PublicMethodNames(typeof(AppAsmFacade)),
            .. PublicMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicMethodNames(typeof(PlatformAsmFacade)),
            .. PublicMethodNames(typeof(HybridCpuThreadCompilerContext))
        ];
        string compilerSource = ReadAllCompilerSource();

        foreach ((_, string[] publicHelperFragments, string[] compilerSourceFragments) in RuntimeOnlyLane6QueueQueryDsc2Contours)
        {
            foreach (string fragment in publicHelperFragments)
            {
                Assert.DoesNotContain(
                    publicCompilerMethods,
                    methodName => methodName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
            }

            foreach (string fragment in compilerSourceFragments)
            {
                Assert.DoesNotContain(
                    fragment,
                    compilerSource,
                    StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CompilerSources_DoNotReferenceLane7RuntimeOnlyTaxonomyOrBackendSurfacesAsAuthority()
    {
        string compilerSource = ReadAllCompilerSource();
        string[] forbiddenFragments =
        [
            "ACCEL_QUERY_CAPS",
            "ACCEL_POLL",
            "ACCEL_WAIT",
            "ACCEL_CANCEL",
            "ACCEL_FENCE",
            "ACCEL_STATUS",
            "ACCEL_GET_RESULT",
            "ACCEL_RESET",
            "ACCEL_QUERY_ABI",
            "ACCEL_QUERY_TOPOLOGY",
            "ACCEL_BIND_QUEUE",
            "ACCEL_UNBIND_QUEUE",
            "ACCEL_FFT",
            "ACCEL_CRYPTO",
            "ACCEL_HASH",
            "ACCEL_SPARSE",
            "ACCEL_GRAPH",
            "ExternalAcceleratorRuntime",
            "AcceleratorTokenStore",
            "AcceleratorCommandQueue",
            "AcceleratorFenceCoordinator",
            "AcceleratorRegisterAbi",
            "IExternalAcceleratorBackend",
            "FakeMatMulExternalAcceleratorBackend",
            "TensorMetadata",
            "TensorContract",
            "TopologyQueue",
            "FftMetadata",
            "CryptoHashMetadata",
            "SparseGraphMetadata"
        ];

        foreach (string forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(
                forbiddenFragment,
                compilerSource,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VmxCompilerAuthorityMatrix_MarksRuntimeOpcodesAsRawTransportOnly()
    {
        InstructionsEnum[] expectedRuntimeVmxOpcodes =
        [
            InstructionsEnum.VMXON,
            InstructionsEnum.VMXOFF,
            InstructionsEnum.VMLAUNCH,
            InstructionsEnum.VMRESUME,
            InstructionsEnum.VMREAD,
            InstructionsEnum.VMWRITE,
            InstructionsEnum.VMCLEAR,
            InstructionsEnum.VMPTRLD,
            InstructionsEnum.VMPTRST,
            InstructionsEnum.VMCALL,
            InstructionsEnum.INVEPT,
            InstructionsEnum.INVVPID,
            InstructionsEnum.VMFUNC,
            InstructionsEnum.VMSAVEX,
            InstructionsEnum.VMRESTX,
        ];

        CompilerVmxOpcodeAuthority[] authority =
            [.. CompilerVmxAuthority.GetRuntimeOpcodeAuthority()];

        Assert.Equal(
            expectedRuntimeVmxOpcodes,
            authority.Select(static item => item.Opcode).ToArray());

        foreach (CompilerVmxOpcodeAuthority opcodeAuthority in authority)
        {
            Assert.Equal(CompilerVmxAuthorityKind.RuntimeExecutable, opcodeAuthority.Authority);
            Assert.True(opcodeAuthority.RuntimeExecutable);
            Assert.True(opcodeAuthority.RawTransportOnly);
            Assert.True(opcodeAuthority.RootRuntimePolicyGated);
            Assert.False(opcodeAuthority.CompilerHelperEmittable);
        }
    }

    [Fact]
    public void VmcsV2CompilerAuthority_SeparatesGuestVisibleFieldsRootPolicyAndHostEvidence()
    {
        VmcsV2BlockDirectory directory = VmcsV2BlockDirectory.CreateDefault();

        Assert.True(directory.TryGetField(
            (ushort)VmcsField.GuestPc,
            out VmcsV2FieldDescriptor guestPc));
        CompilerVmcsV2FieldAuthority guestPcAuthority =
            CompilerVmxAuthority.ClassifyVmcsV2Field(guestPc);
        Assert.Equal(CompilerVmxAuthorityKind.GuestVmreadVmwriteVisible, guestPcAuthority.Authority);
        Assert.True(guestPcAuthority.IsVmReadVisible);
        Assert.True(guestPcAuthority.IsVmWriteVisible);
        Assert.False(guestPcAuthority.ContainsHostEvidence);
        Assert.False(guestPcAuthority.CanAttachToExecutableCompilerInstruction);

        Assert.True(directory.TryGetField(
            VmcsV2BlockDirectory.ShadowVmcsBlockFieldId,
            out VmcsV2FieldDescriptor shadowVmcs));
        CompilerVmcsV2FieldAuthority shadowAuthority =
            CompilerVmxAuthority.ClassifyVmcsV2Field(shadowVmcs);
        Assert.Equal(CompilerVmxAuthorityKind.HostEvidenceOnly, shadowAuthority.Authority);
        Assert.False(shadowAuthority.IsVmReadVisible);
        Assert.False(shadowAuthority.IsVmWriteVisible);
        Assert.True(shadowAuthority.ContainsHostEvidence);
        Assert.False(shadowAuthority.CanAttachToExecutableCompilerInstruction);

        Assert.True(directory.TryGetField(
            VmcsV2BlockDirectory.DirtyLogBlockFieldId,
            out VmcsV2FieldDescriptor dirtyLog));
        CompilerVmcsV2FieldAuthority dirtyLogAuthority =
            CompilerVmxAuthority.ClassifyVmcsV2Field(dirtyLog);
        Assert.Equal(CompilerVmxAuthorityKind.RootRuntimeApiOnly, dirtyLogAuthority.Authority);
        Assert.False(dirtyLogAuthority.IsVmReadVisible);
        Assert.False(dirtyLogAuthority.IsVmWriteVisible);
        Assert.False(dirtyLogAuthority.CanAttachToExecutableCompilerInstruction);

        Assert.True(CompilerVmxAuthority.TryCreateVmcsV2ValidationSideband(
            vmcsV2Revision: 1,
            dirtyLog,
            out CompilerVmcsV2DescriptorSideband sideband,
            out string diagnostic));
        Assert.True(sideband.ValidationOnly);
        Assert.False(sideband.CanAttachToExecutableCompilerInstruction);
        Assert.Equal(string.Empty, diagnostic);
        Assert.False(CompilerVmxAuthority.CanAttachVmcsV2SidebandToOpcode(
            InstructionsEnum.VMREAD,
            sideband,
            out string attachDiagnostic));
        Assert.Contains("validation-only", attachDiagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VmxPreflightDiagnostics_FailClosedWhenRootPolicyIsDisabled()
    {
        var request = new CompilerVmxPreflightRequest(
            RequiredVmcsV2Revision: 1,
            CompilerVmxRequestedFeature.NestedVmx |
            CompilerVmxRequestedFeature.Migration |
            CompilerVmxRequestedFeature.DirtyLogging |
            CompilerVmxRequestedFeature.ObservabilityExport);

        CompilerVmxTargetCapability capabilities =
            CompilerVmxTargetCapability.VmcsV2Revision1 |
            CompilerVmxTargetCapability.NestedVmx |
            CompilerVmxTargetCapability.Migration |
            CompilerVmxTargetCapability.DirtyLogging |
            CompilerVmxTargetCapability.ObservabilityExport;

        CompilerVmxPreflightResult disabledPolicyResult =
            CompilerVmxAuthority.EvaluatePreflight(
                request,
                capabilities,
                CompilerVmxRootPolicy.Disabled);

        Assert.False(disabledPolicyResult.Succeeded);
        Assert.Equal(CompilerVmxPreflightRejectionKind.RootPolicyDisabled, disabledPolicyResult.RejectionKind);
        Assert.Equal(CompilerVmxRequestedFeature.NestedVmx, disabledPolicyResult.RejectedFeature);
        Assert.DoesNotContain("VMCS02", disabledPolicyResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VPID", disabledPolicyResult.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", disabledPolicyResult.Diagnostic, StringComparison.OrdinalIgnoreCase);

        CompilerVmxPreflightResult unsupportedCapabilityResult =
            CompilerVmxAuthority.EvaluatePreflight(
                request,
                CompilerVmxTargetCapability.VmcsV2Revision1,
                new CompilerVmxRootPolicy(
                    NestedVmxEnabled: true,
                    VectorStreamRestoreEnabled: false,
                    Lane6RestoreEnabled: false,
                    Lane7RestoreEnabled: false,
                    MigrationEnabled: true,
                    DirtyLoggingEnabled: true,
                    ObservabilityExportEnabled: true));

        Assert.False(unsupportedCapabilityResult.Succeeded);
        Assert.Equal(
            CompilerVmxPreflightRejectionKind.UnsupportedTargetCapability,
            unsupportedCapabilityResult.RejectionKind);
        Assert.Equal(CompilerVmxRequestedFeature.NestedVmx, unsupportedCapabilityResult.RejectedFeature);
    }

    [Fact]
    public void CompilerSources_DoNotPublishVmxPhase8GuestAbiOrHostEvidenceHelpers()
    {
        string compilerSource = ReadAllCompilerSource();
        string[] forbiddenFragments =
        [
            "VMDIRTYLOG",
            "CompileVmx",
            "CompileVmcs",
            "CompileVmFunc",
            "CompileCheckpoint",
            "CompileMigration",
            "CompileDirtyLog",
            "CompileObservability",
            "CompileDebugTrace",
            "HostOwnedVmcs02Pointer",
            "HostOwnedVpid",
            "HostOwnedVmid",
            "IotlbCache",
            "NativeToken",
            "BackendBinding",
            "TraceHandle",
        ];

        foreach (string forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(
                forbiddenFragment,
                compilerSource,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SupportCatalogClosedRows_DoNotBecomeCompilerEmissionAuthority()
    {
        InstructionSupportStatus[] closedRows =
        [
            .. InstructionSupportStatusCatalog.ExplicitStatuses
                .Where(static status => IsNoEmissionStatus(status.Status)),
            .. IsaV4Surface.ProhibitedOpcodes.Select(InstructionSupportStatusCatalog.GetStatus),
            .. IsaV4Surface.ReservedOpcodes.Select(InstructionSupportStatusCatalog.GetStatus),
            .. IsaV4Surface.OptionalDisabledOpcodes.Select(InstructionSupportStatusCatalog.GetStatus),
            .. IsaV4Surface.ParserOnlyOpcodes.Select(InstructionSupportStatusCatalog.GetStatus),
            .. IsaV4Surface.DescriptorOnlyOpcodes.Select(InstructionSupportStatusCatalog.GetStatus),
            .. IsaV4Surface.CarrierOnlyOpcodes.Select(InstructionSupportStatusCatalog.GetStatus)
        ];

        closedRows = closedRows
            .GroupBy(static status => status.Mnemonic, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static status => status.Mnemonic, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(closedRows);
        Assert.Contains(
            closedRows,
            static status =>
                status.HasNumericOpcode ||
                status.HasRuntimeOpcodeMetadata ||
                status.HasCanonicalDecoderAcceptance ||
                status.HasRegistryFactory);

        foreach (InstructionSupportStatus status in closedRows)
        {
            Assert.True(
                IsNoEmissionStatus(status.Status),
                $"{status.Mnemonic} must stay in a no-emission status.");
            Assert.False(
                status.IsExecutableClaim,
                $"{status.Mnemonic} metadata/catalog presence must not grant execution authority.");
            Assert.False(
                status.HasExecutionSemantics,
                $"{status.Mnemonic} must not publish compiler/runtime execution semantics while closed.");
        }
    }

    [Fact]
    public void ThreadCompilerIngress_RejectsStreamLengthOverflowBeforeCarrierEmission()
    {
        var context = new HybridCpuThreadCompilerContext(virtualThreadId: 0);
        ulong overflowingStreamLength = (ulong)uint.MaxValue + 1UL;

        ArgumentOutOfRangeException compileException = Assert.Throws<ArgumentOutOfRangeException>(
            () => context.CompileInstruction(
                (uint)InstructionsEnum.ADDI,
                (byte)DataTypeEnum.INT32,
                predicate: 0,
                immediate: 1,
                destSrc1: VLIW_Instruction.PackArchRegs(1, 2, 0),
                src2: 0,
                streamLength: overflowingStreamLength,
                stride: 0,
                stealabilityPolicy: StealabilityPolicy.NotStealable));
        Assert.Equal("streamLength", compileException.ParamName);
        Assert.Equal(0, context.InstructionCount);

        ArgumentOutOfRangeException insertException = Assert.Throws<ArgumentOutOfRangeException>(
            () => context.InsertInstruction(
                instructionIndex: 0,
                opCode: (uint)InstructionsEnum.ADDI,
                dataType: (byte)DataTypeEnum.INT32,
                predicate: 0,
                immediate: 1,
                destSrc1: VLIW_Instruction.PackArchRegs(1, 2, 0),
                src2: 0,
                streamLength: overflowingStreamLength,
                stride: 0,
                stealabilityPolicy: StealabilityPolicy.NotStealable));
        Assert.Equal("streamLength", insertException.ParamName);
        Assert.Equal(0, context.InstructionCount);
    }

    [Fact]
    public void LegacyCompilerBridge_RejectsStreamLengthOverflowBeforeCarrierEmission()
    {
        var bridge = new ProcessorCompilerBridge();
        bridge.DeclareCompilerContractVersion(
            CompilerContract.Version,
            producerSurface: "vliw-bundle-audit-test");

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => bridge.Add_VLIW_Instruction(
                opCode: (uint)InstructionsEnum.ADDI,
                dataType: (byte)DataTypeEnum.INT32,
                predicate: 0,
                immediate: 1,
                destSrc1: VLIW_Instruction.PackArchRegs(1, 2, 0),
                src2: 0,
                streamLength: (ulong)uint.MaxValue + 1UL,
                stride: 0));

        Assert.Equal("streamLength", exception.ParamName);
        Assert.Equal(0, bridge.InstructionCount);
    }

    private static string[] PublicMethodNames(Type type)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Select(static method => method.Name)
            .ToArray();
    }

    private static string ReadAllCompilerSource()
    {
        string compilerDirectory = Path.Combine(
            CompatFreezeScanner.FindRepoRoot(),
            "HybridCPU_Compiler");

        return string.Join(
            Environment.NewLine,
            Directory
                .EnumerateFiles(compilerDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(static path => !CompatFreezeScanner.IsGeneratedPath(path))
                .Select(File.ReadAllText));
    }

    private static bool IsNoEmissionStatus(IsaInstructionStatus status) =>
        status is
            IsaInstructionStatus.OptionalDisabled or
            IsaInstructionStatus.Reserved or
            IsaInstructionStatus.LegacyRetained or
            IsaInstructionStatus.ParserOnly or
            IsaInstructionStatus.DescriptorOnly or
            IsaInstructionStatus.Prohibited or
            IsaInstructionStatus.CarrierOnly;
}
