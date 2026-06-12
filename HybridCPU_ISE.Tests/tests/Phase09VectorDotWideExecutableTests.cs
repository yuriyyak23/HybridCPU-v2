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

public sealed class Phase09VectorDotWideExecutableTests
{
    [Fact]
    public void VdotWide_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong sourceA = 0xF00UL;
        const ulong sourceB = 0xF40UL;

        Assert.Equal(323, (int)InstructionsEnum.VDOT_WIDE);
        Assert.Equal((ushort)InstructionsEnum.VDOT_WIDE, IsaOpcodeValues.VDOT_WIDE);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VDOT.WIDE");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorDotProductWideScalarFootprint", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VDOT.WIDE", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VDOT.WIDE"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VDOT_WIDE));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VDOT_WIDE));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VDOT_WIDE));
        Assert.Equal("VDOT.WIDE", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.True((info.Flags & InstructionFlags.Reduction) != 0);
        Assert.True((info.Flags & InstructionFlags.FloatingPoint) != 0);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VDOT_WIDE));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VDOT_WIDE);
        Assert.Equal("VectorDotProductWideScalarFootprint", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Reduction);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VDOT_WIDE));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VDOT_WIDE);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVdotWideInstruction(
            DataTypeEnum.FLOAT16,
            sourceA,
            sourceB,
            streamLength: 4);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VDOT_WIDE, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(sourceA, payload.PrimaryPointer);
        Assert.Equal(sourceB, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.FLOAT16, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);

        VectorDotWideMicroOp wide = Assert.IsType<VectorDotWideMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VDOT_WIDE, wide.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, wide.InstructionClass);
        Assert.Equal(SerializationClass.Free, wide.SerializationClass);
        Assert.Equal(SlotClass.AluClass, wide.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, wide.Placement.PinningKind);
        Assert.Empty(wide.ReadRegisters);
        Assert.Empty(wide.WriteRegisters);
        Assert.Contains(wide.ReadMemoryRanges, range => range.Address == sourceA && range.Length == 8);
        Assert.Contains(wide.ReadMemoryRanges, range => range.Address == sourceB && range.Length == 8);
        Assert.Contains(wide.WriteMemoryRanges, range => range.Address == sourceA && range.Length == 4);

        VLIW_Instruction fp8Instruction = CreateVdotWideInstruction(
            DataTypeEnum.FLOAT8_E4M3,
            sourceA + 0x100UL,
            sourceB + 0x100UL,
            streamLength: 4);
        (_, MicroOp fp8MicroOp) = DecodeAndMaterialize(fp8Instruction);

        VectorDotWideMicroOp fp8Wide = Assert.IsType<VectorDotWideMicroOp>(fp8MicroOp);
        Assert.Contains(fp8Wide.ReadMemoryRanges, range => range.Address == sourceA + 0x100UL && range.Length == 4);
        Assert.Contains(fp8Wide.ReadMemoryRanges, range => range.Address == sourceB + 0x100UL && range.Length == 4);
        Assert.Contains(fp8Wide.WriteMemoryRanges, range => range.Address == sourceA + 0x100UL && range.Length == 4);

        VLIW_Instruction int8Instruction = CreateVdotWideInstruction(
            DataTypeEnum.INT8,
            sourceA + 0x200UL,
            sourceB + 0x200UL,
            streamLength: 4);
        (_, MicroOp int8MicroOp) = DecodeAndMaterialize(int8Instruction);

        VectorDotWideMicroOp int8Wide = Assert.IsType<VectorDotWideMicroOp>(int8MicroOp);
        Assert.Contains(int8Wide.ReadMemoryRanges, range => range.Address == sourceA + 0x200UL && range.Length == 4);
        Assert.Contains(int8Wide.ReadMemoryRanges, range => range.Address == sourceB + 0x200UL && range.Length == 4);
        Assert.Contains(int8Wide.WriteMemoryRanges, range => range.Address == sourceA + 0x200UL && range.Length == 4);

        BundleLegalityDescriptor legality = DecodeLegality(instruction);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.AluClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.FlexibleSlotMask);
    }

    [Fact]
    public void VdotWide_TypedSlotCapacity_RemainsAluFlexibleAndShowsLanePressure()
    {
        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        for (int i = 0; i < 5; i++)
        {
            bundle[i] = MaterializeVdotWide(
                DataTypeEnum.FLOAT16,
                sourceA: 0x1000UL + (ulong)(i * 0x40),
                sourceB: 0x1800UL + (ulong)(i * 0x40),
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

    [Theory]
    [InlineData(DataTypeEnum.FLOAT16, 5.0)]
    [InlineData(DataTypeEnum.BFLOAT16, 5.0)]
    [InlineData(DataTypeEnum.FLOAT8_E4M3, 5.0)]
    [InlineData(DataTypeEnum.FLOAT8_E5M2, 5.0)]
    public void VdotWide_GoldenVector_PublishesFp32ScalarOnlyAtWriteBack(
        DataTypeEnum dataType,
        double expected)
    {
        const ulong sourceA = 0x2000UL;
        const ulong sourceB = 0x2040UL;

        InitializeMemorySubsystem();
        SeedFloat(sourceA, dataType, 1.5, 2.0, -1.0);
        SeedFloat(sourceB, dataType, 2.0, 3.0, 4.0);
        byte[] before = ReadBytes(sourceA, 6);

        var core = new Processor.CPU_Core(0);
        VectorDotWideMicroOp wide = MaterializeVdotWide(
            dataType,
            sourceA,
            sourceB,
            streamLength: 3);

        Assert.True(wide.Execute(ref core));
        Assert.Equal(before, ReadBytes(sourceA, 6));
        Assert.NotNull(wide.GetStagedResult());
        Assert.Equal(4, wide.GetStagedResult()!.Value.Data.Length);

        Publish(wide, ref core);
        Assert.Equal(expected, ReadFloat32(sourceA), precision: 5);
    }

    [Fact]
    public void VdotWide_IntegerByteWideDot_PublishesInt32OrUInt32ScalarOnlyAtWriteBack()
    {
        const ulong signedSourceA = 0x2080UL;
        const ulong signedSourceB = 0x20C0UL;
        const ulong unsignedSourceA = 0x2180UL;
        const ulong unsignedSourceB = 0x21C0UL;

        InitializeMemorySubsystem();
        SeedInteger(signedSourceA, DataTypeEnum.INT8, 2, -3, 4);
        SeedInteger(signedSourceB, DataTypeEnum.INT8, 5, 6, -7);
        byte[] signedBefore = ReadBytes(signedSourceA, 3);

        var core = new Processor.CPU_Core(0);
        VectorDotWideMicroOp signed = MaterializeVdotWide(
            DataTypeEnum.INT8,
            signedSourceA,
            signedSourceB,
            streamLength: 3);

        Assert.True(signed.Execute(ref core));
        Assert.Equal(signedBefore, ReadBytes(signedSourceA, 3));
        Assert.NotNull(signed.GetStagedResult());
        Assert.Equal(4, signed.GetStagedResult()!.Value.Data.Length);

        Publish(signed, ref core);
        Assert.Equal(-36, ReadInt32(signedSourceA));

        SeedUnsignedInteger(unsignedSourceA, DataTypeEnum.UINT8, 2, 3, 4);
        SeedUnsignedInteger(unsignedSourceB, DataTypeEnum.UINT8, 5, 6, 7);
        byte[] unsignedBefore = ReadBytes(unsignedSourceA, 3);

        VectorDotWideMicroOp unsigned = MaterializeVdotWide(
            DataTypeEnum.UINT8,
            unsignedSourceA,
            unsignedSourceB,
            streamLength: 3);

        Assert.True(unsigned.Execute(ref core));
        Assert.Equal(unsignedBefore, ReadBytes(unsignedSourceA, 3));
        Assert.NotNull(unsigned.GetStagedResult());
        Assert.Equal(4, unsigned.GetStagedResult()!.Value.Data.Length);

        Publish(unsigned, ref core);
        Assert.Equal(56U, ReadUInt32(unsignedSourceA));
    }

    [Fact]
    public void VdotWide_MaskReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedResults()
    {
        const ulong sourceA = 0x2100UL;
        const ulong sourceB = 0x2140UL;

        InitializeMemorySubsystem(mappedBytes: 0x3000UL);
        SeedFloat(sourceA, DataTypeEnum.FLOAT16, 1.0, 2.0, 3.0);
        SeedFloat(sourceB, DataTypeEnum.FLOAT16, 10.0, 10.0, 10.0);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);

        VectorDotWideMicroOp discarded = MaterializeVdotWide(
            DataTypeEnum.FLOAT16,
            sourceA,
            sourceB,
            streamLength: 3,
            predicateMask: 1);
        Assert.True(discarded.Execute(ref core));
        Assert.NotEqual(40.0, ReadFloat32(sourceA));

        SeedFloat(sourceA, DataTypeEnum.FLOAT16, 2.0, 4.0, 6.0);
        SeedFloat(sourceB, DataTypeEnum.FLOAT16, 1.0, 1.0, 1.0);
        VectorDotWideMicroOp replayed = MaterializeVdotWide(
            DataTypeEnum.FLOAT16,
            sourceA,
            sourceB,
            streamLength: 3,
            predicateMask: 1);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(8.0, ReadFloat32(sourceA), precision: 5);

        VectorDotWideMicroOp faulting = MaterializeVdotWide(
            DataTypeEnum.FLOAT16,
            sourceA: 0x10000000UL,
            sourceB,
            streamLength: 2);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => faulting.Execute(ref core));
        Assert.Contains("VectorDotWideMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(8.0, ReadFloat32(sourceA), precision: 5);
    }

    [Fact]
    public void VdotWide_FailClosedBoundaries_RejectAdjacentContoursSidebandsAndRawStreamFallback()
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
            Assert.DoesNotContain(OpcodeRegistry.Opcodes, info => info.Mnemonic == mnemonic);
        }

        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(DataTypeEnum.FLOAT32));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(DataTypeEnum.INT16));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(DataTypeEnum.UINT16));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(DataTypeEnum.INT32));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(DataTypeEnum.UINT32));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(stride: 2));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(sourceA: 0xF02UL));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVdotWide(sourceB: 0xF41UL));
        VectorDotWideMicroOp fp8ByteAligned = MaterializeVdotWide(
            DataTypeEnum.FLOAT8_E5M2,
            sourceB: 0xF41UL);
        Assert.Contains(fp8ByteAligned.ReadMemoryRanges, range => range.Address == 0xF41UL && range.Length == 2);
        VectorDotWideMicroOp int8ByteAligned = MaterializeVdotWide(
            DataTypeEnum.INT8,
            sourceB: 0xF41UL);
        Assert.Contains(int8ByteAligned.ReadMemoryRanges, range => range.Address == 0xF41UL && range.Length == 2);
        Assert.Throws<InvalidOperationException>(() =>
            InstructionEncoder.EncodeDotProduct(
                (uint)InstructionsEnum.VDOT_WIDE,
                DataTypeEnum.FLOAT16,
                destPtr: 0x3000UL,
                src1Ptr: 0x3010UL,
                src2Ptr: 0x3040UL,
                streamLength: 2));

        InitializeMemorySubsystem();
        const ulong sourceA = 0x2200UL;
        const ulong sourceB = 0x2240UL;
        SeedFloat(sourceA, DataTypeEnum.FLOAT16, 1.0, 2.0);
        SeedFloat(sourceB, DataTypeEnum.FLOAT16, 3.0, 4.0);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVdotWideInstruction(
            DataTypeEnum.FLOAT16,
            sourceA,
            sourceB,
            streamLength: 2);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("VDOT.WIDE", rawEx.Message, StringComparison.Ordinal);
        Assert.NotEqual(11.0, ReadFloat32(sourceA));
    }

    [Fact]
    public void VdotWide_CompilerSurfaceRemainsNoEmissionForMixedPrecisionDotHelpers()
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
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("DotWide", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("MixedPrecision", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("BlockScaled", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Int8Dot", StringComparison.OrdinalIgnoreCase));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerVectorHelperClosedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VDOT.WIDE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VDOT_WIDE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VDOT_WIDE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VDOT_WIDE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVdotWide", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVdotWide", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileDotWide", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitDotWide", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileBlockScaled", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Int8Dot", compilerSource, StringComparison.Ordinal);
    }

    private static VectorDotWideMicroOp MaterializeVdotWide(
        DataTypeEnum dataType = DataTypeEnum.FLOAT16,
        ulong sourceA = 0xF00UL,
        ulong sourceB = 0xF40UL,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort immediate = 0,
        ushort stride = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVdotWideInstruction(
            dataType,
            sourceA,
            sourceB,
            streamLength,
            predicateMask,
            immediate);
        instruction.Stride = stride;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorDotWideMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VDOT_WIDE,
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5A00, bundleSerial: 1101);
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
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x5A40, bundleSerial: 1102);
        return new BundleLegalityAnalyzer().Analyze(decoded);
    }

    private static VLIW_Instruction CreateVdotWideInstruction(
        DataTypeEnum dataType,
        ulong sourceA,
        ulong sourceB,
        uint streamLength,
        byte predicateMask = 0,
        ushort immediate = 0)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeDotProduct(
            (uint)InstructionsEnum.VDOT_WIDE,
            dataType,
            destPtr: sourceA,
            src1Ptr: sourceA,
            src2Ptr: sourceB,
            streamLength,
            stride: 0,
            predicateMask);
        instruction.Immediate = immediate;
        return instruction;
    }

    private static void Publish(VectorDotWideMicroOp microOp, ref Processor.CPU_Core core)
    {
        Span<RetireRecord> retireRecords = stackalloc RetireRecord[1];
        int retireRecordCount = 0;
        microOp.EmitWriteBackRetireRecords(ref core, retireRecords, ref retireRecordCount);
        Assert.Equal(0, retireRecordCount);
    }

    private static void InitializeMemorySubsystem(ulong mappedBytes = 0x8000UL)
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

    private static void SeedFloat(ulong address, DataTypeEnum dataType, params double[] values)
    {
        int elementSize = DataTypeUtils.SizeOf(dataType);
        byte[] data = new byte[values.Length * elementSize];
        for (int i = 0; i < values.Length; i++)
        {
            ElementCodec.StoreF(data, i * elementSize, dataType, values[i]);
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static void SeedInteger(ulong address, DataTypeEnum dataType, params long[] values)
    {
        int elementSize = DataTypeUtils.SizeOf(dataType);
        byte[] data = new byte[values.Length * elementSize];
        for (int i = 0; i < values.Length; i++)
        {
            ElementCodec.StoreI(data, i * elementSize, dataType, values[i]);
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static void SeedUnsignedInteger(ulong address, DataTypeEnum dataType, params ulong[] values)
    {
        int elementSize = DataTypeUtils.SizeOf(dataType);
        byte[] data = new byte[values.Length * elementSize];
        for (int i = 0; i < values.Length; i++)
        {
            ElementCodec.StoreU(data, i * elementSize, dataType, values[i]);
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static double ReadFloat32(ulong address)
    {
        byte[] data = ReadBytes(address, sizeof(float));
        return ElementCodec.LoadF(data, 0, DataTypeEnum.FLOAT32);
    }

    private static int ReadInt32(ulong address)
    {
        byte[] data = ReadBytes(address, sizeof(int));
        return (int)ElementCodec.LoadI(data, 0, DataTypeEnum.INT32);
    }

    private static uint ReadUInt32(ulong address)
    {
        byte[] data = ReadBytes(address, sizeof(uint));
        return (uint)ElementCodec.LoadU(data, 0, DataTypeEnum.UINT32);
    }

    private static byte[] ReadBytes(ulong address, int count) =>
        Processor.MainMemory.ReadFromPosition(new byte[count], address, (ulong)count);

}
