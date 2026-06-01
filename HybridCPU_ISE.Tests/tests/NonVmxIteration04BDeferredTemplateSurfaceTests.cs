using System;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using CloseToRtlVdeinterleave = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VdeinterleaveInstruction;
using CloseToRtlVall = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VallInstruction;
using CloseToRtlVany = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VanyInstruction;
using CloseToRtlVcvtF = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtFInstruction;
using CloseToRtlVcvtI = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtIInstruction;
using CloseToRtlVcvtU = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtUInstruction;
using CloseToRtlVfirst = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VfirstInstruction;
using CloseToRtlVgatherIndexed2D = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VgatherIndexed2DContour;
using CloseToRtlVinterleave = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VinterleaveInstruction;
using CloseToRtlVldseg2 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg2Instruction;
using CloseToRtlVldseg4 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg4Instruction;
using CloseToRtlVldseg8 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg8Instruction;
using CloseToRtlVload2D = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vload2DContour;
using CloseToRtlVmerge = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmergeInstruction;
using CloseToRtlVmsif = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsifInstruction;
using CloseToRtlVmsof = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsofInstruction;
using CloseToRtlVnsra = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsraInstruction;
using CloseToRtlVnsrl = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsrlInstruction;
using CloseToRtlVavg = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgInstruction;
using CloseToRtlVavgR = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgRInstruction;
using CloseToRtlVclip = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VclipInstruction;
using CloseToRtlMtileLoad = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileLoadInstruction;
using CloseToRtlMtileMacc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileMaccInstruction;
using CloseToRtlMtileStore = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileStoreInstruction;
using CloseToRtlMtranspose = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtransposeInstruction;
using CloseToRtlVscatterIndexed2D = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VscatterIndexed2DContour;
using CloseToRtlVscanMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMaxInstruction;
using CloseToRtlVscanMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMinInstruction;
using CloseToRtlVselect = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VselectInstruction;
using CloseToRtlVsext = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VsextInstruction;
using CloseToRtlVmulSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VmulSatInstruction;
using CloseToRtlVsllSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsllSatInstruction;
using CloseToRtlVsraSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsraSatInstruction;
using CloseToRtlVsrlSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsrlSatInstruction;
using CloseToRtlVstseg2 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg2Instruction;
using CloseToRtlVstseg4 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg4Instruction;
using CloseToRtlVstseg8 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg8Instruction;
using CloseToRtlVstore2D = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vstore2DContour;
using CloseToRtlVsubSat = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsubSatInstruction;
using CloseToRtlVunzip = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VunzipInstruction;
using CloseToRtlVdotAccum = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotAccumInstruction;
using CloseToRtlVdotBlockscale = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotBlockscaleInstruction;
using CloseToRtlVdotWideI16 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI16Instruction;
using CloseToRtlVdotWideI32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI32Instruction;
using CloseToRtlVwadd = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwaddInstruction;
using CloseToRtlVwaddu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwadduInstruction;
using CloseToRtlVwmacc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmaccInstruction;
using CloseToRtlVwmul = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmulInstruction;
using CloseToRtlVwmulu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmuluInstruction;
using CloseToRtlVwsub = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubInstruction;
using CloseToRtlVwsubu = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubuInstruction;
using CloseToRtlVzip = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VzipInstruction;
using CloseToRtlDsc2DShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.Dsc2DShapeContour;
using CloseToRtlDscAbsDiff = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscAbsDiffDescriptorOp;
using CloseToRtlDscClamp = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscClampDescriptorOp;
using CloseToRtlDscCompare = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscCompareDescriptorOp;
using CloseToRtlDscConvert = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.TypeConversion.DscConvertDescriptorOp;
using CloseToRtlDscMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMaxDescriptorOp;
using CloseToRtlDscMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMinDescriptorOp;
using CloseToRtlDscMultiRangeShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscMultiRangeShapeContour;
using CloseToRtlDscReduceAnd = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceAndDescriptorOp;
using CloseToRtlDscReduceMax = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMaxDescriptorOp;
using CloseToRtlDscReduceMin = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMinDescriptorOp;
using CloseToRtlDscReduceOr = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceOrDescriptorOp;
using CloseToRtlDscReduceSum = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceSumDescriptorOp;
using CloseToRtlDscReduceXor = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceXorDescriptorOp;
using CloseToRtlDscScatterGatherShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscScatterGatherShapeContour;
using CloseToRtlDscSelect = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscSelectDescriptorOp;
using CloseToRtlDscStridedShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscStridedShapeContour;
using CloseToRtlDscSub = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscSubDescriptorOp;
using CloseToRtlDscTiledShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscTiledShapeContour;
using CloseToRtlDsc2 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.CarrierV2.Dsc2DescriptorCarrier;
using CloseToRtlDscCancel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCancelInstruction;
using CloseToRtlDscCommit = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCommitInstruction;
using CloseToRtlDscFence = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscFenceInstruction;
using CloseToRtlDscPoll = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscPollInstruction;
using CloseToRtlDscQueryBackend = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryBackendInstruction;
using CloseToRtlDscQueryShape = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryShapeInstruction;
using CloseToRtlDscWait = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscWaitInstruction;
using CloseToRtlPause = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Hints.PauseInstruction;
using CloseToRtlRdinstret = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Counters.RdinstretInstruction;
using CloseToRtlRdtime = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Counters.RdtimeInstruction;
using CloseToRtlAdc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AdcInstruction;
using CloseToRtlAddc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AddcInstruction;
using CloseToRtlAddUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.AddUwInstruction;
using CloseToRtlCrc32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc32Instruction;
using CloseToRtlCrc64 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc64Instruction;
using CloseToRtlCsel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect.CselInstruction;
using CloseToRtlSbc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SbcInstruction;
using CloseToRtlSeqz = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SeqzInstruction;
using CloseToRtlSh1addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh1addUwInstruction;
using CloseToRtlSh2add = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addInstruction;
using CloseToRtlSh2addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addUwInstruction;
using CloseToRtlSh3add = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addInstruction;
using CloseToRtlSh3addUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addUwInstruction;
using CloseToRtlSlliUw = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.SlliUwInstruction;
using CloseToRtlSnez = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SnezInstruction;
using CloseToRtlSubc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SubcInstruction;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxIteration04BDeferredTemplateSurfaceTests
{
    private const string NonVmxNamespacePrefix =
        "YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.";

    [Fact]
    public void Iteration04B_AllDeferredTemplates_DoNotExposeOpcodeOrExecuteAuthority()
    {
        Type[] templateTypes = GetDeferredTemplateTypes();

        Assert.Equal(105, templateTypes.Length);
        foreach (Type templateType in templateTypes)
        {
            Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
            Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "EvidenceBoundary")));
            Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
            Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
        }
    }

    [Fact]
    public void Iteration04B_VectorAndMemoryTemplates_RemainVectorLegalityMatrixGated()
    {
        Type[] vectorTypes = GetDeferredTemplateTypes()
            .Where(static type =>
                type.Namespace!.Contains(".Lanes00_03Vector.", StringComparison.Ordinal) ||
                type.Namespace!.Contains(".Lanes04_05Memory.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(52, vectorTypes.Length);
        foreach (Type templateType in vectorTypes)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        }
    }

    [Fact]
    public void Iteration04B_Lane6Templates_RemainDescriptorOrQueueOwnedWithoutScalarOpcodeAuthority()
    {
        Type[] lane6Types = GetDeferredTemplateTypes()
            .Where(static type => type.Namespace!.Contains(".Lane06DmaStream.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(27, lane6Types.Length);
        foreach (Type templateType in lane6Types)
        {
            Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscPoll), "DSC_POLL", "RequiresRetireOwnedPublication", "")]
    [InlineData(typeof(CloseToRtlDscWait), "DSC_WAIT", "RequiresCommandScopeAbi", "RequiresRetireOwnedPublication")]
    [InlineData(typeof(CloseToRtlDscCancel), "DSC_CANCEL", "RequiresCommandScopeAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToRtlDscFence), "DSC_FENCE", "RequiresQueueOrderingAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToRtlDscCommit), "DSC_COMMIT", "RequiresStagedCommitAuthority", "RequiresRetireOwnedSideEffect")]
    public void Iteration13A_Lane6QueueLifecycleLeafTemplates_RemainQueueAuthorityGatedNoExecution(
        Type templateType,
        string mnemonic,
        string requiredMarker,
        string optionalMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("Lane6QueueControlNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "IsQueueControlOwned"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenNamespaceAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresQueueHandleAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRollbackPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalMarker))
        {
            Assert.True(GetConstant<bool>(templateType, optionalMarker), templateType.FullName);
        }

        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscQueryBackend), "DSC_QUERY_BACKEND", "RequiresBackendCapabilityAbi")]
    [InlineData(typeof(CloseToRtlDscQueryShape), "DSC_QUERY_SHAPE", "RequiresShapeQueryAbi")]
    public void Iteration13A_Lane6QueryLeafTemplates_RemainReadOnlyCapabilityQueries(
        Type templateType,
        string mnemonic,
        string queryMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("Lane6CapabilityQueryNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "IsCapabilityQuery"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsReadOnlyQuery"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresCapabilityQueryAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, queryMarker), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBoundedResultFootprint"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireOwnedPublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayStableResult"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostEvidenceLeak"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void Iteration13A_Dsc2LeafTemplate_RemainsParserOnlyCarrierWithoutRuntimeAuthority()
    {
        Type templateType = typeof(CloseToRtlDsc2);

        Assert.Equal("DSC2", GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane06DmaStream", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal("ParserOnlyCarrierNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "IsDescriptorOwned"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsCarrierOnly"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsParserOnly"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2Adr"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorV2ParserManifest"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresBackwardCompatibleDecoder"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRuntimeAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireCommitAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoDsc2ExecutionBeforeAdr"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscSub), "DmaStreamCompute.SUB", "RequiresArithmeticPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscMin), "DmaStreamCompute.MIN", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscMax), "DmaStreamCompute.MAX", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscAbsDiff), "DmaStreamCompute.ABSDIFF", "RequiresOverflowPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscClamp), "DmaStreamCompute.CLAMP", "RequiresBoundsPolicyAbi", "")]
    [InlineData(typeof(CloseToRtlDscConvert), "DmaStreamCompute.CONVERT", "RequiresConversionPolicyAbi", "RequiresRoundingSaturationTrapPolicy")]
    [InlineData(typeof(CloseToRtlDscCompare), "DmaStreamCompute.COMPARE", "RequiresPredicateFootprintAbi", "")]
    [InlineData(typeof(CloseToRtlDscSelect), "DmaStreamCompute.SELECT", "RequiresPredicateFootprintAbi", "RequiresSelectResultFootprintAbi")]
    [InlineData(typeof(CloseToRtlDscReduceSum), "DmaStreamCompute.REDUCE_SUM", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceMin), "DmaStreamCompute.REDUCE_MIN", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceMax), "DmaStreamCompute.REDUCE_MAX", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceAnd), "DmaStreamCompute.REDUCE_AND", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceOr), "DmaStreamCompute.REDUCE_OR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToRtlDscReduceXor), "DmaStreamCompute.REDUCE_XOR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    public void Iteration12A_DescriptorOpLeafTemplates_RemainDescriptorOwnedNoScalarOpcode(
        Type templateType,
        string mnemonic,
        string requiredPolicyMarker,
        string optionalPolicyMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane6DescriptorOwnedNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "IsDescriptorOwned"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorOpTypeAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorParserValidation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresOwnerDomainGuard"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresStagedCommit"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireCommitAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
        Assert.True(GetConstant<bool>(templateType, requiredPolicyMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalPolicyMarker))
        {
            Assert.True(GetConstant<bool>(templateType, optionalPolicyMarker), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlDscStridedShape), "DSC_SHAPE_STRIDED", "RequiresStrideAbi")]
    [InlineData(typeof(CloseToRtlDscTiledShape), "DSC_SHAPE_TILED", "RequiresTileShapeAbi")]
    [InlineData(typeof(CloseToRtlDscScatterGatherShape), "DSC_SHAPE_SCATTER_GATHER", "RequiresIndexSurfaceAbi")]
    [InlineData(typeof(CloseToRtlDsc2DShape), "DSC_SHAPE_2D", "Requires2DShapeAbi")]
    [InlineData(typeof(CloseToRtlDscMultiRangeShape), "DSC_SHAPE_MULTI_RANGE", "RequiresMultiRangeAbi")]
    public void Iteration12A_ShapeRangeLeafTemplates_RemainDescriptorShapeOwnedNoScalarOpcode(
        Type templateType,
        string mnemonic,
        string requiredShapeMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("Lane6ShapeContourNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "IsDescriptorOwned"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasScalarOpcodeAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresShapeEnumAllocation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresDescriptorParserValidation"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresOwnerDomainGuard"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTokenAdmission"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresNormalizedFootprintAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresPartialCompletionPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresStagedCommit"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireCommitAuthority"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoGuestVisibleHostEvidence"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
        Assert.True(GetConstant<bool>(templateType, requiredShapeMarker), templateType.FullName);
    }

    [Fact]
    public void Iteration04B_Lane7Templates_RemainControlPlaneNoEmission()
    {
        Type[] lane7Types = GetDeferredTemplateTypes()
            .Where(static type => type.Namespace!.Contains(".Lane07SystemControl.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(16, lane7Types.Length);
        foreach (Type templateType in lane7Types)
        {
            Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlRdtime), "RDTIME", "Lane7CounterReplayDeferred", "RequiresReplayStableCounterModel", true, true)]
    [InlineData(typeof(CloseToRtlRdinstret), "RDINSTRET", "Lane7CounterReplayDeferred", "RequiresRetireAccountingModel", true, true)]
    [InlineData(typeof(CloseToRtlPause), "PAUSE", "Lane7HintNoExecutionGuarantee", "NoArchitecturalProgressGuarantee", false, false)]
    public void Iteration14A_Lane7CounterAndHintLeafTemplates_RemainReplayAndNoEmissionGated(
        Type templateType,
        string mnemonic,
        string evidenceBoundary,
        string requiredMarker,
        bool expectsCounter,
        bool expectsFutureVirtualizationPolicy)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lane07SystemControl", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal(evidenceBoundary, GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresImmediateVmxProjection"), templateType.FullName);
        Assert.Equal(expectsFutureVirtualizationPolicy, GetConstant<bool>(templateType, "RequiresFutureVirtualizationBoundaryPolicy"));
        Assert.False(GetConstant<bool>(templateType, "HasOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredMarker), templateType.FullName);

        if (expectsCounter)
        {
            Assert.True(GetConstant<bool>(templateType, "IsSystemCounter"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireOwnedPublication"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireRegisterWriteback"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresPrivilegePolicy"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedRdcycle"), templateType.FullName);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "IsSchedulingHint"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "RequiresRetireOwnedPublication"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "RequiresRetireRegisterWriteback"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresNoArchitecturalStateLeakage"), templateType.FullName);
        }

        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Fact]
    public void Iteration04C_RemainingScalarTemplates_CloseAnchorOnlySurfaceWithoutExecution()
    {
        Type[] scalarTypes = GetDeferredTemplateTypes()
            .Where(static type => type.Namespace!.Contains(".Lanes00_03Scalar.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(10, scalarTypes.Length);
        foreach (Type templateType in scalarTypes)
        {
            Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
            Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
            Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
            Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlSeqz), "SEQZ", "FacadeOnlyNoEmissionClosed", "FacadeDecisionClosed", "")]
    [InlineData(typeof(CloseToRtlSnez), "SNEZ", "FacadeOnlyNoEmissionClosed", "FacadeDecisionClosed", "")]
    [InlineData(typeof(CloseToRtlCsel), "CSEL", "ScalarSelectAbiDeferredNoEmission", "ExternalCarrierGateClosed", "RequiresFourRegisterCarrierAbi")]
    [InlineData(typeof(CloseToRtlCrc32), "CRC32", "CrcPolynomialAbiDeferredNoEmission", "RequiresPolynomialAbi", "RequiresEndianPolicyAbi")]
    [InlineData(typeof(CloseToRtlCrc64), "CRC64", "CrcPolynomialAbiDeferredNoEmission", "RequiresPolynomialAbi", "RequiresEndianPolicyAbi")]
    [InlineData(typeof(CloseToRtlAdc), "ADC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresCarryInAbi", "RequiresCarryOutAbi")]
    [InlineData(typeof(CloseToRtlSbc), "SBC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresBorrowInAbi", "RequiresBorrowOutAbi")]
    [InlineData(typeof(CloseToRtlAddc), "ADDC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresCarryOutAbi", "NoImplicitFlags")]
    [InlineData(typeof(CloseToRtlSubc), "SUBC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresBorrowOutAbi", "NoImplicitFlags")]
    public void MetadataPass01A_ScalarDeferredLeafTemplates_CarryLocalDescriptorMetadata(
        Type templateType,
        string mnemonic,
        string evidenceBoundary,
        string requiredMarker,
        string optionalMarker)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "OperandShape")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "ParameterDescriptor")));
        Assert.False(string.IsNullOrWhiteSpace(GetConstant<string>(templateType, "MicroOpShape")));
        Assert.Equal("Lanes00_03Scalar", GetConstant<string>(templateType, "ExecutionLaneBinding"));
        Assert.Equal(evidenceBoundary, GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDecoderEncoderAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresInstructionIrProjection"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRegistryMaterializer"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireRegisterWriteback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayRollbackEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoVmxFrontendIntegrationRequired"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "RequiresVmxProjection"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "HasOpcodeAllocation"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, requiredMarker), templateType.FullName);

        if (!string.IsNullOrEmpty(optionalMarker))
        {
            Assert.True(GetConstant<bool>(templateType, optionalMarker), templateType.FullName);
        }

        if (mnemonic == "SLLI.UW")
        {
            Assert.Equal(6, GetConstant<int>(templateType, "ImmediateBits"));
        }

        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVmerge), "VMERGE", "VectorContourFailClosed", false, false)]
    [InlineData(typeof(CloseToRtlVselect), "VSELECT", "VectorContourFailClosed", false, false)]
    [InlineData(typeof(CloseToRtlVfirst), "VFIRST", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToRtlVany), "VANY", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToRtlVall), "VALL", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToRtlVmsif), "VMSIF", "VectorPredicateOnlyContourFailClosed", false, true)]
    [InlineData(typeof(CloseToRtlVmsof), "VMSOF", "VectorPredicateOnlyContourFailClosed", false, true)]
    public void Iteration07A_PredicateMaskLeafTemplates_RemainVlmGatedNoEmission(
        Type templateType,
        string mnemonic,
        string evidenceBoundary,
        bool expectsScalarResultAbi,
        bool expectsPredicateOnlyPublication)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal(evidenceBoundary, GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresPredicateMaskSideband"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsScalarResultAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresScalarResultAbi"), templateType.FullName);
        }

        if (expectsPredicateOnlyPublication)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresPredicateOnlyPublication"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVwadd), "VWADD", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwaddu), "VWADDU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwsub), "VWSUB", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwsubu), "VWSUBU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwmul), "VWMUL", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwmulu), "VWMULU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVwmacc), "VWMACC", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToRtlVnsrl), "VNSRL", true, false, false, true, false, true)]
    [InlineData(typeof(CloseToRtlVnsra), "VNSRA", true, false, false, true, false, true)]
    [InlineData(typeof(CloseToRtlVsext), "VSEXT", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVcvtI), "VCVT.I", false, false, false, false, true, true)]
    [InlineData(typeof(CloseToRtlVcvtU), "VCVT.U", false, false, false, false, true, true)]
    [InlineData(typeof(CloseToRtlVcvtF), "VCVT.F", false, false, false, false, true, true)]
    public void Iteration08A_WidenNarrowConvertLeafTemplates_RemainVlmGatedNoEmission(
        Type templateType,
        string mnemonic,
        bool expectsWidthSideband,
        bool expectsSignednessAbi,
        bool expectsAccumulatorAbi,
        bool expectsNarrowingPolicyAbi,
        bool expectsConversionPolicyAbi,
        bool expectsRoundingSaturationTrapPolicy)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorWidenNarrowConvertFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsWidthSideband)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSourceDestinationWidthSideband"), templateType.FullName);
        }

        if (expectsSignednessAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"), templateType.FullName);
        }

        if (expectsAccumulatorAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorAbi"), templateType.FullName);
        }

        if (expectsNarrowingPolicyAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresNarrowingPolicyAbi"), templateType.FullName);
        }

        if (expectsConversionPolicyAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresConversionPolicyAbi"), templateType.FullName);
        }

        if (expectsRoundingSaturationTrapPolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRoundingSaturationTrapPolicy"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVzip), "VZIP")]
    [InlineData(typeof(CloseToRtlVunzip), "VUNZIP")]
    [InlineData(typeof(CloseToRtlVinterleave), "VINTERLEAVE")]
    [InlineData(typeof(CloseToRtlVdeinterleave), "VDEINTERLEAVE")]
    public void Iteration09A_StructureMovementLeafTemplates_BlockHiddenStreamFallback(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorStructureMovementFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresStructureShapeAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHiddenStreamEngineFallback"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVldseg2), "VLDSEG2", 2, true)]
    [InlineData(typeof(CloseToRtlVldseg4), "VLDSEG4", 4, true)]
    [InlineData(typeof(CloseToRtlVldseg8), "VLDSEG8", 8, true)]
    [InlineData(typeof(CloseToRtlVstseg2), "VSTSEG2", 2, false)]
    [InlineData(typeof(CloseToRtlVstseg4), "VSTSEG4", 4, false)]
    [InlineData(typeof(CloseToRtlVstseg8), "VSTSEG8", 8, false)]
    public void Iteration09A_SegmentMemoryLeafTemplates_RemainFaultReplayAndRetireGated(
        Type templateType,
        string mnemonic,
        int segmentCount,
        bool expectsLoad)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorSegmentMemoryFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.Equal(segmentCount, GetConstant<int>(templateType, "SegmentCount"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMemoryShapeAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFaultReplayPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsLoad)
        {
            Assert.True(GetConstant<bool>(templateType, "IsSegmentLoad"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "IsSegmentStore"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedCommit"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVload2D), "VLOAD", "2D", true, false)]
    [InlineData(typeof(CloseToRtlVstore2D), "VSTORE", "2D", false, false)]
    [InlineData(typeof(CloseToRtlVgatherIndexed2D), "VGATHER", "Indexed2D", true, true)]
    [InlineData(typeof(CloseToRtlVscatterIndexed2D), "VSCATTER", "Indexed2D", false, true)]
    public void Iteration09A_VectorMemoryContourLeafTemplates_DoNotDuplicateBaseOpcodes(
        Type templateType,
        string mnemonic,
        string contour,
        bool expectsLoadLikePublication,
        bool expectsIndexedContour)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal(contour, GetConstant<string>(templateType, "Contour"));
        Assert.Equal("VectorMemoryContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresMemoryShapeAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFaultReplayPolicy"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoBaseOpcodeDuplication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsIndexedContour)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresIndexed2DShapeSideband"), templateType.FullName);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "Requires2DShapeSideband"), templateType.FullName);
        }

        if (expectsLoadLikePublication)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedCommit"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVscanMin), "VSCAN.MIN")]
    [InlineData(typeof(CloseToRtlVscanMax), "VSCAN.MAX")]
    public void Iteration10A_PrefixScanLeafTemplates_RemainPolicyAndVlmGated(
        Type templateType,
        string mnemonic)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorScanContourFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresPrefixScanPolicyAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresElementTypeSideband"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTailPolicyAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedVscanSum"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresReplayDeterminism"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVsubSat), "VSUB.SAT", true, false, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVmulSat), "VMUL.SAT", true, false, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVsllSat), "VSLL.SAT", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToRtlVsrlSat), "VSRL.SAT", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToRtlVsraSat), "VSRA.SAT", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToRtlVavg), "VAVG", false, false, false, true, false, false)]
    [InlineData(typeof(CloseToRtlVavgR), "VAVG.R", false, false, false, true, true, false)]
    [InlineData(typeof(CloseToRtlVclip), "VCLIP", false, false, false, false, false, true)]
    public void Iteration10A_SaturatingFixedPointLeafTemplates_RemainPolicyAndVlmGated(
        Type templateType,
        string mnemonic,
        bool expectsSaturatingPolicy,
        bool expectsShiftMeaningDecision,
        bool expectsRightShiftReservationDecision,
        bool expectsAveragePolicy,
        bool expectsRoundingPolicy,
        bool expectsClipPolicy)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorFixedPointSaturatingFailClosed", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresSignednessAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresRetireStagedPublication"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsSaturatingPolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSaturatingPolicyAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresClampPolicyAbi"), templateType.FullName);
        }

        if (mnemonic == "VSUB.SAT")
        {
            Assert.True(GetConstant<bool>(templateType, "SeparateFromClosedVaddSat"), templateType.FullName);
        }

        if (expectsShiftMeaningDecision)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSaturatingShiftMeaningDecision"), templateType.FullName);
        }

        if (expectsRightShiftReservationDecision)
        {
            Assert.True(GetConstant<bool>(templateType, "MayRemainReservedIfNonMeaningful"), templateType.FullName);
        }

        if (expectsAveragePolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAveragePolicyAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresElementWidthAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresRoundingTruncationPolicyAbi"), templateType.FullName);
        }

        if (expectsRoundingPolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresRoundingPolicyAbi"), templateType.FullName);
        }

        if (expectsClipPolicy)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresClipBoundsAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresNarrowingPolicyAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "RequiresResultWidthAbi"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlVdotBlockscale), "VDOT.BLOCKSCALE", true, false, false)]
    [InlineData(typeof(CloseToRtlVdotAccum), "VDOT.ACCUM", false, true, false)]
    [InlineData(typeof(CloseToRtlVdotWideI16), "VDOT.WIDE.I16", false, false, true)]
    [InlineData(typeof(CloseToRtlVdotWideI32), "VDOT.WIDE.I32", false, false, true)]
    public void Iteration11A_DotMixedPrecisionLeafTemplates_DoNotExtendScopedVdotWideByName(
        Type templateType,
        string mnemonic,
        bool expectsScaleMetadata,
        bool expectsAccumulatorResultFootprint,
        bool expectsWiderIntegerContour)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorDotMatrixDeferredNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "RequiresDotAbiDecision"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorPrecisionAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "NoHostOwnedEvidencePublication"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsScaleMetadata)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresScaleMetadataAbi"), templateType.FullName);
        }

        if (expectsAccumulatorResultFootprint)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorResultFootprintAbi"), templateType.FullName);
        }

        if (expectsWiderIntegerContour)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresWiderIntegerContourAbi"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "SeparateFromScopedVdotWide"), templateType.FullName);
            Assert.True(GetConstant<bool>(templateType, "NoNameOnlyVdotWideExtension"), templateType.FullName);
        }
        else
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresSeparateResultSurfaceAbi"), templateType.FullName);
        }
    }

    [Theory]
    [InlineData(typeof(CloseToRtlMtileLoad), "MTILE_LOAD", true, false, false)]
    [InlineData(typeof(CloseToRtlMtileStore), "MTILE_STORE", true, false, false)]
    [InlineData(typeof(CloseToRtlMtileMacc), "MTILE_MACC", false, true, false)]
    [InlineData(typeof(CloseToRtlMtranspose), "MTRANSPOSE", false, false, true)]
    public void Iteration11A_MatrixTileLeafTemplates_RemainOptionalDisabledNoExecution(
        Type templateType,
        string mnemonic,
        bool expectsMemoryShapeFaultModel,
        bool expectsAccumulatorTileAbi,
        bool expectsTransposePolicyAbi)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("VectorDotMatrixDeferredNoExecution", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.True(GetConstant<bool>(templateType, "OptionalDisabledInIsaV4"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTileExecutionModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTileDescriptorAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
        Assert.False(GetConstant<bool>(templateType, "CompilerHelperAllowed"), templateType.FullName);
        Assert.Null(templateType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(templateType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));

        if (expectsMemoryShapeFaultModel)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresTileMemoryShapeFaultModel"), templateType.FullName);
        }

        if (expectsAccumulatorTileAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresAccumulatorTileAbi"), templateType.FullName);
        }

        if (expectsTransposePolicyAbi)
        {
            Assert.True(GetConstant<bool>(templateType, "RequiresTransposeTilePolicyAbi"), templateType.FullName);
        }
    }

    private static Type[] GetDeferredTemplateTypes() =>
        typeof(Processor.CPU_Core).Assembly
            .GetTypes()
            .Where(static type =>
                type.IsClass &&
                type.Namespace is not null &&
                type.Namespace.StartsWith(NonVmxNamespacePrefix, StringComparison.Ordinal) &&
                type.GetField("EvidenceBoundary", BindingFlags.Public | BindingFlags.Static) is not null &&
                type.GetField("IsExecutable", BindingFlags.Public | BindingFlags.Static) is not null &&
                GetConstant<bool>(type, "IsExecutable") == false)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const template marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }
}
