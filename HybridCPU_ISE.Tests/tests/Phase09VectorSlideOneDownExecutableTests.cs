using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.CloseToRTL.Memory.MMU;
using HybridCPU_ISE.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Legality;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorSlideOneDownExecutableTests
{
    [Fact]
    public void Vslide1Down_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong vectorBase = 0x1240UL;

        Assert.Equal(324, (int)InstructionsEnum.VSLIDE1DOWN);
        Assert.Equal((ushort)InstructionsEnum.VSLIDE1DOWN, IsaOpcodeValues.VSLIDE1DOWN);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VSLIDE1DOWN");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorSlideOnePublication", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VSLIDE1DOWN", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VSLIDE1DOWN"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VSLIDE1DOWN));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VSLIDE1DOWN));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VSLIDE1DOWN));
        Assert.Equal("VSLIDE1DOWN", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.False((info.Flags & InstructionFlags.UsesImmediate) != 0);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VSLIDE1DOWN));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VSLIDE1DOWN);
        Assert.Equal("VectorSlideOnePublication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VSLIDE1DOWN));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VSLIDE1DOWN);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVslide1DownInstruction(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 4);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VSLIDE1DOWN, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(vectorBase, payload.PrimaryPointer);
        Assert.Equal(0UL, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT16, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);

        VectorSlideOneDownMicroOp slide = Assert.IsType<VectorSlideOneDownMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VSLIDE1DOWN, slide.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, slide.InstructionClass);
        Assert.Equal(SerializationClass.Free, slide.SerializationClass);
        Assert.Equal(SlotClass.AluClass, slide.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, slide.Placement.PinningKind);
        Assert.Empty(slide.ReadRegisters);
        Assert.Empty(slide.WriteRegisters);
        Assert.Contains(slide.ReadMemoryRanges, range => range.Address == vectorBase && range.Length == 8);
        Assert.Contains(slide.WriteMemoryRanges, range => range.Address == vectorBase && range.Length == 8);

        BundleLegalityDescriptor legality = DecodeLegality(instruction);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.AluClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.FlexibleSlotMask);
    }

    [Fact]
    public void Vslide1Down_TypedSlotCapacity_RemainsAluFlexibleAndShowsLanePressure()
    {
        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        for (int i = 0; i < 5; i++)
        {
            bundle[i] = MaterializeVslide1Down(
                DataTypeEnum.UINT8,
                vectorBase: 0x1280UL + (ulong)(i * 0x20),
                streamLength: 4);
        }

        SlotClassCapacityState capacity = SlotClassCapacity.ComputeFromBundle(bundle);
        TypedSlotBundleFacts facts = TypedSlotBundleFacts.FromBundle(bundle);

        Assert.Equal(5, capacity.AluOccupied);
        Assert.Equal(4, capacity.AluTotal);
        Assert.False(capacity.HasFreeCapacity(SlotClass.AluClass));
        Assert.Equal(5, facts.AluCount);
        Assert.Equal(5, facts.FlexibleOpCount);
        Assert.Equal(0, facts.PinnedOpCount);
    }

    [Fact]
    public void Vslide1Down_GoldenVector_PublishesFixedOneLaneSlideOnlyAtWriteBack()
    {
        const ulong vectorBase = 0x12C0UL;

        InitializeMemorySubsystem();
        SeedUInt16(vectorBase, 10, 20, 30, 40);

        var core = new Processor.CPU_Core(0);
        VectorSlideOneDownMicroOp slide = MaterializeVslide1Down(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 4);

        Assert.True(slide.Execute(ref core));
        Assert.Equal(new ushort[] { 10, 20, 30, 40 }, ReadUInt16(vectorBase, 4));
        Assert.Equal(new[] { vectorBase, vectorBase + 2, vectorBase + 4 },
            slide.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(slide, ref core);
        Assert.Equal(new ushort[] { 20, 30, 40, 40 }, ReadUInt16(vectorBase, 4));
    }

    [Fact]
    public void Vslide1Down_MaskAndTailPolicy_ProjectAndKeepUndisturbedLanes()
    {
        const ulong vectorBase = 0x1300UL;

        InitializeMemorySubsystem();
        SeedUInt16(vectorBase, 10, 20, 30, 40, 50);

        VLIW_Instruction instruction = CreateVslide1DownInstruction(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 4,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: false);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.False(ir.VectorPayload!.Value.MaskAgnostic);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);
        VectorSlideOneDownMicroOp slide = Assert.IsType<VectorSlideOneDownMicroOp>(microOp);

        Assert.True(slide.Execute(ref core));
        Publish(slide, ref core);

        Assert.Equal(new ushort[] { 20, 20, 40, 40, 50 }, ReadUInt16(vectorBase, 5));
    }

    [Fact]
    public void Vslide1Down_ReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong vectorBase = 0x1340UL;

        InitializeMemorySubsystem(mappedBytes: 0x2000UL);
        SeedUInt16(vectorBase, 1, 2, 3);

        var core = new Processor.CPU_Core(0);
        VectorSlideOneDownMicroOp discarded = MaterializeVslide1Down(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 3);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 1, 2, 3 }, ReadUInt16(vectorBase, 3));

        SeedUInt16(vectorBase, 10, 20, 30);
        VectorSlideOneDownMicroOp replayed = MaterializeVslide1Down(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 3);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 20, 30, 30 }, ReadUInt16(vectorBase, 3));

        const ulong faultingVectorBase = 0x10000000UL;
        VectorSlideOneDownMicroOp destinationFault = MaterializeVslide1Down(
            DataTypeEnum.UINT16,
            faultingVectorBase,
            streamLength: 2);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => destinationFault.Execute(ref core));
        Assert.Contains("VectorSlideOneDownMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 20, 30, 30 }, ReadUInt16(vectorBase, 3));
    }

    [Fact]
    public void Vslide1Down_FailClosedBoundaries_RejectAdjacentContoursSidebandsAndRawStreamFallback()
    {
        string[] unselected =
        [
            "ACCEL_QUERY_ABI", "ACCEL_QUERY_TOPOLOGY"
        ];

        foreach (string mnemonic in unselected)
        {
            InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus(mnemonic);
            Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
            Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
            Assert.False(status.IsExecutableClaim);
            Assert.False(Enum.TryParse(mnemonic.Replace(".", "_"), out InstructionsEnum _));
            Assert.DoesNotContain(OpcodeRegistry.Opcodes, info => info.Mnemonic == mnemonic);
        }

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, InstructionSupportStatusCatalog.GetStatus("MTRANSPOSE").Status);
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(stride: 2));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(secondaryPointer: 0x100UL));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVslide1Down(dataType: (DataTypeEnum)0xFF));

        InitializeMemorySubsystem();
        const ulong vectorBase = 0x1380UL;
        SeedUInt16(vectorBase, 1, 2);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVslide1DownInstruction(
            DataTypeEnum.UINT16,
            vectorBase,
            streamLength: 2);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("VSLIDE1DOWN", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 1, 2 }, ReadUInt16(vectorBase, 2));
    }

    [Fact]
    public void Vslide1Down_CompilerSurfaceRemainsNoEmissionForDeltaHelpers()
    {
        Type[] publicCompilerSurfaceTypes =
        [
            typeof(HybridCpuThreadCompilerContext),
            typeof(IPlatformAsmFacade),
            typeof(PlatformAsmFacade)
        ];

        string[] publicMethodNames = publicCompilerSurfaceTypes
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.DeclaredOnly))
            .Select(method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("VectorOp", publicMethodNames);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Slide1", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, IsClosedVectorTransposeHelperName);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("DotWide", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("QueryAbi", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Topology", StringComparison.OrdinalIgnoreCase));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerVectorHelperClosedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VSLIDE1DOWN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VSLIDE1DOWN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VSLIDE1DOWN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VSLIDE1DOWN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVslide1Down", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVslide1Down", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileVtranspose", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVtranspose", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.ACCEL_QUERY_ABI", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.ACCEL_QUERY_TOPOLOGY", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileAccelQueryAbi", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileAccelQueryTopology", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitAccelQueryAbi", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitAccelQueryTopology", compilerSource, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClosedVectorTransposeHelperName(string methodName) =>
        methodName.Contains("Vtranspose", StringComparison.OrdinalIgnoreCase) ||
        methodName.Contains("VectorTranspose", StringComparison.OrdinalIgnoreCase);

    private static VectorSlideOneDownMicroOp MaterializeVslide1Down(
        DataTypeEnum dataType = DataTypeEnum.UINT16,
        ulong vectorBase = 0x1240UL,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort immediate = 0,
        ushort stride = 0,
        ulong secondaryPointer = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVslide1DownInstruction(
            dataType,
            vectorBase,
            streamLength,
            predicateMask,
            immediate,
            tailAgnostic: false,
            maskAgnostic: false);
        instruction.Stride = stride;
        instruction.Src2Pointer = secondaryPointer;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorSlideOneDownMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VSLIDE1DOWN,
                ProjectedVectorDecoderContextBuilder.Create(in instruction)));
    }

    private static (InstructionIR Ir, MicroOp MicroOp) DecodeAndMaterialize(
        VLIW_Instruction instruction,
        int slotIndex = 3)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5980, bundleSerial: 1093);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(slotIndex).RequireInstruction();
        return (ir, Assert.IsAssignableFrom<MicroOp>(carrierBundle[slotIndex]));
    }

    private static BundleLegalityDescriptor DecodeLegality(
        VLIW_Instruction instruction,
        int slotIndex = 3)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;
        DecodedInstructionBundle decoded =
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x59C0, bundleSerial: 1094);
        return new BundleLegalityAnalyzer().Analyze(decoded);
    }

    private static VLIW_Instruction CreateVslide1DownInstruction(
        DataTypeEnum dataType,
        ulong vectorBase,
        uint streamLength,
        byte predicateMask = 0,
        ushort immediate = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false) =>
        InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VSLIDE1DOWN,
            dataType,
            vectorBase,
            src2Ptr: 0,
            streamLength,
            stride: 0,
            predicateMask,
            immediate,
            tailAgnostic,
            maskAgnostic);

    private static void Publish(VectorSlideOneDownMicroOp microOp, ref Processor.CPU_Core core)
    {
        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        Assert.Equal(0, retireRecordCount);
    }

    private static void InitializeMemorySubsystem(ulong mappedBytes = 0x4000UL)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: mappedBytes,
            permissions: IOMMUAccessPermissions.ReadWrite);

        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedUInt16(ulong address, params ushort[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ushort)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ushort));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static ushort[] ReadUInt16(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(ushort)],
            address,
            (ulong)(count * sizeof(ushort)));
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToUInt16(bytes, index * sizeof(ushort)))
            .ToArray();
    }

}
