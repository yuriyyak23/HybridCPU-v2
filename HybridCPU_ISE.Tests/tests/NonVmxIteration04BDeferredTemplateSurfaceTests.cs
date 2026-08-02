using System;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using CloseToHSLVdeinterleave = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VdeinterleaveInstruction;
using CloseToHSLVall = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VallInstruction;
using CloseToHSLVany = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VanyInstruction;
using CloseToHSLVcvtF = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtFInstruction;
using CloseToHSLVcvtI = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtIInstruction;
using CloseToHSLVcvtU = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VcvtUInstruction;
using CloseToHSLVfirst = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VfirstInstruction;
using CloseToHSLVgatherIndexed2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VgatherIndexed2DContour;
using CloseToHSLVinterleave = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VinterleaveInstruction;
using CloseToHSLVldseg2 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg2Instruction;
using CloseToHSLVldseg4 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg4Instruction;
using CloseToHSLVldseg8 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vldseg8Instruction;
using CloseToHSLVload2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vload2DContour;
using CloseToHSLVmerge = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmergeInstruction;
using CloseToHSLVmsif = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsifInstruction;
using CloseToHSLVmsof = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VmsofInstruction;
using CloseToHSLVnsra = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsraInstruction;
using CloseToHSLVnsrl = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Narrowing.VnsrlInstruction;
using CloseToHSLVavg = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgInstruction;
using CloseToHSLVavgR = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VavgRInstruction;
using CloseToHSLVclip = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VclipInstruction;
using CloseToHSLMtileLoad = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileLoadInstruction;
using CloseToHSLMtileMacc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileMaccInstruction;
using CloseToHSLMtileStore = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtileStoreInstruction;
using CloseToHSLMtranspose = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile.MtransposeInstruction;
using CloseToHSLVscatterIndexed2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Indexed2D.VscatterIndexed2DContour;
using CloseToHSLVscanMax = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMaxInstruction;
using CloseToHSLVscanMin = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PrefixScan.VscanMinInstruction;
using CloseToHSLVselect = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.PredicateMask.VselectInstruction;
using CloseToHSLVsext = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Conversion.VsextInstruction;
using CloseToHSLVmulSat = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VmulSatInstruction;
using CloseToHSLVsllSat = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsllSatInstruction;
using CloseToHSLVsraSat = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsraSatInstruction;
using CloseToHSLVsrlSat = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsrlSatInstruction;
using CloseToHSLVstseg2 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg2Instruction;
using CloseToHSLVstseg4 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg4Instruction;
using CloseToHSLVstseg8 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Segments.Vstseg8Instruction;
using CloseToHSLVstore2D = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes04_05Memory.Shapes2D.Vstore2DContour;
using CloseToHSLVsubSat = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.SaturatingFixedPoint.VsubSatInstruction;
using CloseToHSLVunzip = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VunzipInstruction;
using CloseToHSLVdotAccum = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotAccumInstruction;
using CloseToHSLVdotBlockscale = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotBlockscaleInstruction;
using CloseToHSLVdotWideI16 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI16Instruction;
using CloseToHSLVdotWideI32 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.DotMixedPrecision.VdotWideI32Instruction;
using CloseToHSLVwadd = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwaddInstruction;
using CloseToHSLVwaddu = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwadduInstruction;
using CloseToHSLVwmacc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmaccInstruction;
using CloseToHSLVwmul = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmulInstruction;
using CloseToHSLVwmulu = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwmuluInstruction;
using CloseToHSLVwsub = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubInstruction;
using CloseToHSLVwsubu = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.Widening.VwsubuInstruction;
using CloseToHSLVzip = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.StructureMovement.VzipInstruction;
using CloseToHSLDsc2DShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.Dsc2DShapeContour;
using CloseToHSLDscAbsDiff = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscAbsDiffDescriptorOp;
using CloseToHSLDscClamp = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscClampDescriptorOp;
using CloseToHSLDscCompare = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscCompareDescriptorOp;
using CloseToHSLDscConvert = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.TypeConversion.DscConvertDescriptorOp;
using CloseToHSLDscMax = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMaxDescriptorOp;
using CloseToHSLDscMin = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscMinDescriptorOp;
using CloseToHSLDscMultiRangeShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscMultiRangeShapeContour;
using CloseToHSLDscReduceAnd = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceAndDescriptorOp;
using CloseToHSLDscReduceMax = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMaxDescriptorOp;
using CloseToHSLDscReduceMin = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceMinDescriptorOp;
using CloseToHSLDscReduceOr = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceOrDescriptorOp;
using CloseToHSLDscReduceSum = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceSumDescriptorOp;
using CloseToHSLDscReduceXor = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Reduction.DscReduceXorDescriptorOp;
using CloseToHSLDscScatterGatherShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscScatterGatherShapeContour;
using CloseToHSLDscSelect = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Predicate.DscSelectDescriptorOp;
using CloseToHSLDscStridedShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscStridedShapeContour;
using CloseToHSLDscSub = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.Arithmetic.DscSubDescriptorOp;
using CloseToHSLDscTiledShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.DescriptorOps.ShapeRange.DscTiledShapeContour;
using CloseToHSLDsc2 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.CarrierV2.Dsc2DescriptorCarrier;
using CloseToHSLDscCancel = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCancelInstruction;
using CloseToHSLDscCommit = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscCommitInstruction;
using CloseToHSLDscFence = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscFenceInstruction;
using CloseToHSLDscPoll = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscPollInstruction;
using CloseToHSLDscQueryBackend = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryBackendInstruction;
using CloseToHSLDscQueryShape = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.Queries.DscQueryShapeInstruction;
using CloseToHSLDscWait = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane06DmaStream.QueueLifecycle.DscWaitInstruction;
using CloseToHSLPause = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Hints.PauseInstruction;
using CloseToHSLRdinstret = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Counters.RdinstretInstruction;
using CloseToHSLRdtime = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lane07SystemControl.Counters.RdtimeInstruction;
using CloseToHSLAdc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AdcInstruction;
using CloseToHSLAddc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AddcInstruction;
using CloseToHSLAddUw = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.AddUwInstruction;
using CloseToHSLCrc32 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc32Instruction;
using CloseToHSLCrc64 = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc64Instruction;
using CloseToHSLCsel = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect.CselInstruction;
using CloseToHSLSbc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SbcInstruction;
using CloseToHSLSeqz = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SeqzInstruction;
using CloseToHSLSh1addUw = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh1addUwInstruction;
using CloseToHSLSh2add = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addInstruction;
using CloseToHSLSh2addUw = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh2addUwInstruction;
using CloseToHSLSh3add = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addInstruction;
using CloseToHSLSh3addUw = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.Sh3addUwInstruction;
using CloseToHSLSlliUw = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.AddressGeneration.SlliUwInstruction;
using CloseToHSLSnez = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.FacadeCandidates.ZeroCompare.SnezInstruction;
using CloseToHSLSubc = YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SubcInstruction;

namespace HybridCPU_ISE.Tests.InstructionsRefactor;

public sealed class NonVmxIteration04BDeferredTemplateSurfaceTests
{
    private const string NonVmxNamespacePrefix =
        "YAKSys_Hybrid_CPU.CloseToHSL.Core.ISA.Instructions.NonVmx.";

    [Fact]
    public void Iteration04B_AllDeferredTemplates_DoNotExposeOpcodeOrExecuteAuthority()
    {
        Type[] templateTypes = GetDeferredTemplateTypes();

        Assert.Equal(101, templateTypes.Length);
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

        Assert.Equal(48, vectorTypes.Length);
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
    [InlineData(typeof(CloseToHSLDscPoll), "DSC_POLL", "RequiresRetireOwnedPublication", "")]
    [InlineData(typeof(CloseToHSLDscWait), "DSC_WAIT", "RequiresCommandScopeAbi", "RequiresRetireOwnedPublication")]
    [InlineData(typeof(CloseToHSLDscCancel), "DSC_CANCEL", "RequiresCommandScopeAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToHSLDscFence), "DSC_FENCE", "RequiresQueueOrderingAbi", "RequiresRetireOwnedSideEffect")]
    [InlineData(typeof(CloseToHSLDscCommit), "DSC_COMMIT", "RequiresStagedCommitAuthority", "RequiresRetireOwnedSideEffect")]
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
    [InlineData(typeof(CloseToHSLDscQueryBackend), "DSC_QUERY_BACKEND", "RequiresBackendCapabilityAbi")]
    [InlineData(typeof(CloseToHSLDscQueryShape), "DSC_QUERY_SHAPE", "RequiresShapeQueryAbi")]
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
        Type templateType = typeof(CloseToHSLDsc2);

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
    [InlineData(typeof(CloseToHSLDscSub), "DmaStreamCompute.SUB", "RequiresArithmeticPolicyAbi", "")]
    [InlineData(typeof(CloseToHSLDscMin), "DmaStreamCompute.MIN", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToHSLDscMax), "DmaStreamCompute.MAX", "RequiresSignednessTypePolicyAbi", "")]
    [InlineData(typeof(CloseToHSLDscAbsDiff), "DmaStreamCompute.ABSDIFF", "RequiresOverflowPolicyAbi", "")]
    [InlineData(typeof(CloseToHSLDscClamp), "DmaStreamCompute.CLAMP", "RequiresBoundsPolicyAbi", "")]
    [InlineData(typeof(CloseToHSLDscConvert), "DmaStreamCompute.CONVERT", "RequiresConversionPolicyAbi", "RequiresRoundingSaturationTrapPolicy")]
    [InlineData(typeof(CloseToHSLDscCompare), "DmaStreamCompute.COMPARE", "RequiresPredicateFootprintAbi", "")]
    [InlineData(typeof(CloseToHSLDscSelect), "DmaStreamCompute.SELECT", "RequiresPredicateFootprintAbi", "RequiresSelectResultFootprintAbi")]
    [InlineData(typeof(CloseToHSLDscReduceSum), "DmaStreamCompute.REDUCE_SUM", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToHSLDscReduceMin), "DmaStreamCompute.REDUCE_MIN", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToHSLDscReduceMax), "DmaStreamCompute.REDUCE_MAX", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToHSLDscReduceAnd), "DmaStreamCompute.REDUCE_AND", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToHSLDscReduceOr), "DmaStreamCompute.REDUCE_OR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
    [InlineData(typeof(CloseToHSLDscReduceXor), "DmaStreamCompute.REDUCE_XOR", "RequiresReductionResultFootprintAbi", "RequiresScalarOrSurfaceResultPolicy")]
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
    [InlineData(typeof(CloseToHSLDscStridedShape), "DSC_SHAPE_STRIDED", "RequiresStrideAbi")]
    [InlineData(typeof(CloseToHSLDscTiledShape), "DSC_SHAPE_TILED", "RequiresTileShapeAbi")]
    [InlineData(typeof(CloseToHSLDscScatterGatherShape), "DSC_SHAPE_SCATTER_GATHER", "RequiresIndexSurfaceAbi")]
    [InlineData(typeof(CloseToHSLDsc2DShape), "DSC_SHAPE_2D", "Requires2DShapeAbi")]
    [InlineData(typeof(CloseToHSLDscMultiRangeShape), "DSC_SHAPE_MULTI_RANGE", "RequiresMultiRangeAbi")]
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
    [InlineData(typeof(CloseToHSLRdtime), "RDTIME", "Lane7CounterReplayDeferred", "RequiresReplayStableCounterModel", true, true)]
    [InlineData(typeof(CloseToHSLRdinstret), "RDINSTRET", "Lane7CounterReplayDeferred", "RequiresRetireAccountingModel", true, true)]
    [InlineData(typeof(CloseToHSLPause), "PAUSE", "Lane7HintNoExecutionGuarantee", "NoArchitecturalProgressGuarantee", false, false)]
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
    [InlineData(typeof(CloseToHSLSeqz), "SEQZ", "FacadeOnlyNoEmissionClosed", "FacadeDecisionClosed", "")]
    [InlineData(typeof(CloseToHSLSnez), "SNEZ", "FacadeOnlyNoEmissionClosed", "FacadeDecisionClosed", "")]
    [InlineData(typeof(CloseToHSLCsel), "CSEL", "ScalarSelectAbiDeferredNoEmission", "ExternalCarrierGateClosed", "RequiresFourRegisterCarrierAbi")]
    [InlineData(typeof(CloseToHSLCrc32), "CRC32", "CrcPolynomialAbiDeferredNoEmission", "RequiresPolynomialAbi", "RequiresEndianPolicyAbi")]
    [InlineData(typeof(CloseToHSLCrc64), "CRC64", "CrcPolynomialAbiDeferredNoEmission", "RequiresPolynomialAbi", "RequiresEndianPolicyAbi")]
    [InlineData(typeof(CloseToHSLAdc), "ADC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresCarryInAbi", "RequiresCarryOutAbi")]
    [InlineData(typeof(CloseToHSLSbc), "SBC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresBorrowInAbi", "RequiresBorrowOutAbi")]
    [InlineData(typeof(CloseToHSLAddc), "ADDC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresCarryOutAbi", "NoImplicitFlags")]
    [InlineData(typeof(CloseToHSLSubc), "SUBC", "MultiPrecisionCarryAbiDeferredNoEmission", "RequiresBorrowOutAbi", "NoImplicitFlags")]
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
    [InlineData(typeof(CloseToHSLVmerge), "VMERGE", "VectorContourFailClosed", false, false)]
    [InlineData(typeof(CloseToHSLVselect), "VSELECT", "VectorContourFailClosed", false, false)]
    [InlineData(typeof(CloseToHSLVfirst), "VFIRST", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToHSLVany), "VANY", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToHSLVall), "VALL", "VectorScalarResultContourFailClosed", true, false)]
    [InlineData(typeof(CloseToHSLVmsif), "VMSIF", "VectorPredicateOnlyContourFailClosed", false, true)]
    [InlineData(typeof(CloseToHSLVmsof), "VMSOF", "VectorPredicateOnlyContourFailClosed", false, true)]
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
    [InlineData(typeof(CloseToHSLVwadd), "VWADD", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwaddu), "VWADDU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwsub), "VWSUB", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwsubu), "VWSUBU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwmul), "VWMUL", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwmulu), "VWMULU", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVwmacc), "VWMACC", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToHSLVnsrl), "VNSRL", true, false, false, true, false, true)]
    [InlineData(typeof(CloseToHSLVnsra), "VNSRA", true, false, false, true, false, true)]
    [InlineData(typeof(CloseToHSLVsext), "VSEXT", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVcvtI), "VCVT.I", false, false, false, false, true, true)]
    [InlineData(typeof(CloseToHSLVcvtU), "VCVT.U", false, false, false, false, true, true)]
    [InlineData(typeof(CloseToHSLVcvtF), "VCVT.F", false, false, false, false, true, true)]
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
    [InlineData(typeof(CloseToHSLVzip), "VZIP")]
    [InlineData(typeof(CloseToHSLVunzip), "VUNZIP")]
    [InlineData(typeof(CloseToHSLVinterleave), "VINTERLEAVE")]
    [InlineData(typeof(CloseToHSLVdeinterleave), "VDEINTERLEAVE")]
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
    [InlineData(typeof(CloseToHSLVldseg2), "VLDSEG2", 2, true)]
    [InlineData(typeof(CloseToHSLVldseg4), "VLDSEG4", 4, true)]
    [InlineData(typeof(CloseToHSLVldseg8), "VLDSEG8", 8, true)]
    [InlineData(typeof(CloseToHSLVstseg2), "VSTSEG2", 2, false)]
    [InlineData(typeof(CloseToHSLVstseg4), "VSTSEG4", 4, false)]
    [InlineData(typeof(CloseToHSLVstseg8), "VSTSEG8", 8, false)]
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
    [InlineData(typeof(CloseToHSLVload2D), "VLOAD", "2D", true, false)]
    [InlineData(typeof(CloseToHSLVstore2D), "VSTORE", "2D", false, false)]
    [InlineData(typeof(CloseToHSLVgatherIndexed2D), "VGATHER", "Indexed2D", true, true)]
    [InlineData(typeof(CloseToHSLVscatterIndexed2D), "VSCATTER", "Indexed2D", false, true)]
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
    [InlineData(typeof(CloseToHSLVscanMin), "VSCAN.MIN")]
    [InlineData(typeof(CloseToHSLVscanMax), "VSCAN.MAX")]
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
    [InlineData(typeof(CloseToHSLVsubSat), "VSUB.SAT", true, false, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVmulSat), "VMUL.SAT", true, false, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVsllSat), "VSLL.SAT", true, true, false, false, false, false)]
    [InlineData(typeof(CloseToHSLVsrlSat), "VSRL.SAT", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToHSLVsraSat), "VSRA.SAT", true, true, true, false, false, false)]
    [InlineData(typeof(CloseToHSLVavg), "VAVG", false, false, false, true, false, false)]
    [InlineData(typeof(CloseToHSLVavgR), "VAVG.R", false, false, false, true, true, false)]
    [InlineData(typeof(CloseToHSLVclip), "VCLIP", false, false, false, false, false, true)]
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
    [InlineData(typeof(CloseToHSLVdotBlockscale), "VDOT.BLOCKSCALE", true, false, false)]
    [InlineData(typeof(CloseToHSLVdotAccum), "VDOT.ACCUM", false, true, false)]
    [InlineData(typeof(CloseToHSLVdotWideI16), "VDOT.WIDE.I16", false, false, true)]
    [InlineData(typeof(CloseToHSLVdotWideI32), "VDOT.WIDE.I32", false, false, true)]
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
    [InlineData(typeof(CloseToHSLMtileLoad), "MTILE_LOAD", true, false, false)]
    [InlineData(typeof(CloseToHSLMtileStore), "MTILE_STORE", true, false, false)]
    [InlineData(typeof(CloseToHSLMtileMacc), "MTILE_MACC", false, true, false)]
    [InlineData(typeof(CloseToHSLMtranspose), "MTRANSPOSE", false, false, true)]
    public void Iteration11A_MatrixTileLeafTemplates_RecordPhase13RuntimeExecution(
        Type templateType,
        string mnemonic,
        bool expectsMemoryShapeFaultModel,
        bool expectsAccumulatorTileAbi,
        bool expectsTransposePolicyAbi)
    {
        Assert.Equal(mnemonic, GetConstant<string>(templateType, "Mnemonic"));
        Assert.Equal("MatrixTileRuntimeExecutableAuthority", GetConstant<string>(templateType, "EvidenceBoundary"));
        Assert.False(GetConstant<bool>(templateType, "OptionalDisabledInIsaV4"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "OptionalEnabledInIsaV4"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTileExecutionModel"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresTileDescriptorAbi"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresVectorLegalityMatrixClosure"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "RequiresFutureRetireReplayEvidence"), templateType.FullName);
        Assert.True(GetConstant<bool>(templateType, "IsExecutable"), templateType.FullName);
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
