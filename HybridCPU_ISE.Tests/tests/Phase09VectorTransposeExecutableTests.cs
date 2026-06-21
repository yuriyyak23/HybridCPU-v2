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

namespace HybridCPU_ISE.Tests;

public sealed class VectorTransposeExecutableTests
{
    [Fact]
    public void Vtranspose_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong matrixBase = 0x4800UL;

        Assert.Equal(326, (int)InstructionsEnum.VTRANSPOSE);
        Assert.Equal((ushort)InstructionsEnum.VTRANSPOSE, IsaOpcodeValues.VTRANSPOSE);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VTRANSPOSE");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorTransposePublication", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VTRANSPOSE", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VTRANSPOSE"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VTRANSPOSE));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VTRANSPOSE));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VTRANSPOSE));
        Assert.Equal("VTRANSPOSE", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.False((info.Flags & InstructionFlags.UsesImmediate) != 0);
        Assert.False((info.Flags & InstructionFlags.Indexed) != 0);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VTRANSPOSE));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VTRANSPOSE);
        Assert.Equal("VectorTransposePublication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.Reduction);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VTRANSPOSE));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VTRANSPOSE);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVtransposeInstruction(
            DataTypeEnum.UINT16,
            matrixBase);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VTRANSPOSE, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.Equal(0, ir.Imm);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(matrixBase, payload.PrimaryPointer);
        Assert.Equal(0UL, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT16, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);

        VectorTransposeMicroOp transpose = Assert.IsType<VectorTransposeMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VTRANSPOSE, transpose.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, transpose.InstructionClass);
        Assert.Equal(SerializationClass.Free, transpose.SerializationClass);
        Assert.Equal(SlotClass.AluClass, transpose.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, transpose.Placement.PinningKind);
        Assert.Empty(transpose.ReadRegisters);
        Assert.Empty(transpose.WriteRegisters);
        Assert.Contains(transpose.ReadMemoryRanges, range => range.Address == matrixBase && range.Length == 8);
        Assert.Contains(transpose.WriteMemoryRanges, range => range.Address == matrixBase && range.Length == 8);

        BundleLegalityDescriptor legality = DecodeLegality(instruction);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.AluClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.FlexibleSlotMask);
    }

    [Fact]
    public void Vtranspose_TypedSlotCapacity_RemainsAluFlexibleAndShowsLanePressure()
    {
        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        for (int i = 0; i < 5; i++)
        {
            bundle[i] = MaterializeVtranspose(
                DataTypeEnum.UINT8,
                matrixBase: 0x4900UL + (ulong)(i * 0x20));
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
    public void Vtranspose_GoldenVector_PublishesInPlaceTwoByTwoTransposeOnlyAtWriteBack()
    {
        const ulong matrixBase = 0x4A00UL;

        InitializeMemorySubsystem();
        SeedUInt16(matrixBase, 10, 20, 30, 40);

        var core = new Processor.CPU_Core(0);
        VectorTransposeMicroOp transpose = MaterializeVtranspose(
            DataTypeEnum.UINT16,
            matrixBase);

        Assert.True(transpose.Execute(ref core));
        Assert.Equal(new ushort[] { 10, 20, 30, 40 }, ReadUInt16(matrixBase, 4));
        Assert.Equal(new[] { matrixBase + 2, matrixBase + 4 },
            transpose.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(transpose, ref core);
        Assert.Equal(new ushort[] { 10, 30, 20, 40 }, ReadUInt16(matrixBase, 4));
    }

    [Fact]
    public void Vtranspose_MaskAndTailPolicy_ProjectAndKeepUndisturbedLanes()
    {
        const ulong matrixBase = 0x4A80UL;

        InitializeMemorySubsystem();
        SeedUInt16(matrixBase, 10, 20, 30, 40, 50);

        VLIW_Instruction instruction = CreateVtransposeInstruction(
            DataTypeEnum.UINT16,
            matrixBase,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: false);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.False(ir.VectorPayload!.Value.MaskAgnostic);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b_0010UL);
        VectorTransposeMicroOp transpose = Assert.IsType<VectorTransposeMicroOp>(microOp);

        Assert.True(transpose.Execute(ref core));
        Publish(transpose, ref core);

        Assert.Equal(new ushort[] { 10, 30, 30, 40, 50 }, ReadUInt16(matrixBase, 5));
    }

    [Fact]
    public void Vtranspose_ReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong matrixBase = 0x4B00UL;

        InitializeMemorySubsystem(mappedBytes: 0x6000UL);
        SeedUInt16(matrixBase, 1, 2, 3, 4);

        var core = new Processor.CPU_Core(0);
        VectorTransposeMicroOp discarded = MaterializeVtranspose(
            DataTypeEnum.UINT16,
            matrixBase);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, ReadUInt16(matrixBase, 4));

        SeedUInt16(matrixBase, 10, 20, 30, 40);
        VectorTransposeMicroOp replayed = MaterializeVtranspose(
            DataTypeEnum.UINT16,
            matrixBase);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 10, 30, 20, 40 }, ReadUInt16(matrixBase, 4));

        VectorTransposeMicroOp faulting = MaterializeVtranspose(
            DataTypeEnum.UINT16,
            matrixBase: 0x10000000UL);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => faulting.Execute(ref core));
        Assert.Contains("VectorTransposeMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 10, 30, 20, 40 }, ReadUInt16(matrixBase, 4));
    }

    [Fact]
    public void Vtranspose_FailClosedBoundaries_RejectAdjacentContoursSidebandsAndRawStreamFallback()
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

        Assert.Equal(IsaInstructionStatus.OptionalEnabled, InstructionSupportStatusCatalog.GetStatus("MTRANSPOSE").Status);
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(streamLength: 3));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(stride: 2));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(secondaryPointer: 0x4C00UL));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVtranspose(dataType: (DataTypeEnum)0xFF));

        InitializeMemorySubsystem();
        const ulong matrixBase = 0x4B80UL;
        SeedUInt16(matrixBase, 1, 2, 3, 4);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVtransposeInstruction(
            DataTypeEnum.UINT16,
            matrixBase);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("VTRANSPOSE", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, ReadUInt16(matrixBase, 4));
    }

    [Fact]
    public void Vtranspose_CompilerSurfaceRemainsNoEmissionForDeltaHelpers()
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
        Assert.DoesNotContain(publicMethodNames, IsClosedVectorTransposeHelperName);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Perm2", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("DotWide", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("QueryAbi", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Topology", StringComparison.OrdinalIgnoreCase));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerVectorHelperClosedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VTRANSPOSE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VTRANSPOSE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VTRANSPOSE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VTRANSPOSE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVtranspose", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVtranspose", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileVperm2", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVperm2", compilerSource, StringComparison.OrdinalIgnoreCase);
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

    private static VectorTransposeMicroOp MaterializeVtranspose(
        DataTypeEnum dataType = DataTypeEnum.UINT16,
        ulong matrixBase = 0x4800UL,
        uint streamLength = 4,
        byte predicateMask = 0,
        ushort stride = 0,
        ushort immediate = 0,
        ulong secondaryPointer = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVtransposeInstruction(
            dataType,
            matrixBase,
            streamLength,
            predicateMask,
            tailAgnostic: false,
            maskAgnostic: false);
        instruction.Stride = stride;
        instruction.Immediate = immediate;
        instruction.Src2Pointer = secondaryPointer;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorTransposeMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VTRANSPOSE,
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5C00, bundleSerial: 1211);
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
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x5C40, bundleSerial: 1212);
        return new BundleLegalityAnalyzer().Analyze(decoded);
    }

    private static VLIW_Instruction CreateVtransposeInstruction(
        DataTypeEnum dataType,
        ulong matrixBase,
        uint streamLength = 4,
        byte predicateMask = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false) =>
        InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VTRANSPOSE,
            dataType,
            matrixBase,
            src2Ptr: 0,
            streamLength,
            stride: 0,
            predicateMask,
            immediate: 0,
            tailAgnostic,
            maskAgnostic);

    private static void Publish(VectorTransposeMicroOp microOp, ref Processor.CPU_Core core)
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
