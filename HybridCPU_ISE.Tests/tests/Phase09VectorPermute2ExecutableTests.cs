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

public sealed class VectorPermute2ExecutableTests
{
    [Fact]
    public void Vperm2_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong sourceA = 0x4000UL;
        const ulong sourceB = 0x4040UL;
        const ushort immediate = 0b_11_10;

        Assert.Equal(325, (int)InstructionsEnum.VPERM2);
        Assert.Equal((ushort)InstructionsEnum.VPERM2, IsaOpcodeValues.VPERM2);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VPERM2");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorPermute2Publication", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VPERM2", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VPERM2"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VPERM2));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VPERM2));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VPERM2));
        Assert.Equal("VPERM2", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.True((info.Flags & InstructionFlags.UsesImmediate) != 0);
        Assert.False((info.Flags & InstructionFlags.Indexed) != 0);
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VPERM2));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VPERM2);
        Assert.Equal("VectorPermute2Publication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VPERM2));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VPERM2);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVperm2Instruction(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VPERM2, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.Equal(immediate, (ushort)ir.Imm);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(sourceA, payload.PrimaryPointer);
        Assert.Equal(sourceB, payload.SecondaryPointer);
        Assert.Equal(2U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT16, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);

        VectorPermute2MicroOp permute = Assert.IsType<VectorPermute2MicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VPERM2, permute.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, permute.InstructionClass);
        Assert.Equal(SerializationClass.Free, permute.SerializationClass);
        Assert.Equal(SlotClass.AluClass, permute.Placement.RequiredSlotClass);
        Assert.Equal(SlotPinningKind.ClassFlexible, permute.Placement.PinningKind);
        Assert.Empty(permute.ReadRegisters);
        Assert.Empty(permute.WriteRegisters);
        Assert.Contains(permute.ReadMemoryRanges, range => range.Address == sourceA && range.Length == 4);
        Assert.Contains(permute.ReadMemoryRanges, range => range.Address == sourceB && range.Length == 4);
        Assert.Contains(permute.WriteMemoryRanges, range => range.Address == sourceA && range.Length == 4);

        BundleLegalityDescriptor legality = DecodeLegality(instruction);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.AluClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.LsuClassMask);
        Assert.Equal(0, legality.TypedSlotFacts.PinnedSlotMask);
        Assert.Equal(0b_0000_1000, legality.TypedSlotFacts.FlexibleSlotMask);
    }

    [Fact]
    public void Vperm2_TypedSlotCapacity_RemainsAluFlexibleAndShowsLanePressure()
    {
        var bundle = new MicroOp?[BundleMetadata.BundleSlotCount];
        for (int i = 0; i < 5; i++)
        {
            bundle[i] = MaterializeVperm2(
                DataTypeEnum.UINT8,
                sourceA: 0x4100UL + (ulong)(i * 0x20),
                sourceB: 0x4200UL + (ulong)(i * 0x20));
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
    public void Vperm2_GoldenVector_PublishesTwoSourceSelectionOnlyAtWriteBack()
    {
        const ulong sourceA = 0x4300UL;
        const ulong sourceB = 0x4340UL;

        InitializeMemorySubsystem();
        SeedUInt16(sourceA, 10, 20);
        SeedUInt16(sourceB, 30, 40);

        var core = new Processor.CPU_Core(0);
        VectorPermute2MicroOp permute = MaterializeVperm2(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate: 0b_11_10);

        Assert.True(permute.Execute(ref core));
        Assert.Equal(new ushort[] { 10, 20 }, ReadUInt16(sourceA, 2));
        Assert.Equal(new[] { sourceA, sourceA + 2 },
            permute.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(permute, ref core);
        Assert.Equal(new ushort[] { 30, 40 }, ReadUInt16(sourceA, 2));
        Assert.Equal(new ushort[] { 30, 40 }, ReadUInt16(sourceB, 2));
    }

    [Fact]
    public void Vperm2_MaskAndTailPolicy_ProjectAndKeepUndisturbedLanes()
    {
        const ulong sourceA = 0x4380UL;
        const ulong sourceB = 0x43C0UL;

        InitializeMemorySubsystem();
        SeedUInt16(sourceA, 10, 20, 50);
        SeedUInt16(sourceB, 30, 40);

        VLIW_Instruction instruction = CreateVperm2Instruction(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate: 0b_11_10,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: false);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.False(ir.VectorPayload!.Value.MaskAgnostic);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b_01UL);
        VectorPermute2MicroOp permute = Assert.IsType<VectorPermute2MicroOp>(microOp);

        Assert.True(permute.Execute(ref core));
        Publish(permute, ref core);

        Assert.Equal(new ushort[] { 30, 20, 50 }, ReadUInt16(sourceA, 3));
    }

    [Fact]
    public void Vperm2_ReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong sourceA = 0x4400UL;
        const ulong sourceB = 0x4440UL;

        InitializeMemorySubsystem(mappedBytes: 0x6000UL);
        SeedUInt16(sourceA, 1, 2);
        SeedUInt16(sourceB, 3, 4);

        var core = new Processor.CPU_Core(0);
        VectorPermute2MicroOp discarded = MaterializeVperm2(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate: 0b_11_10);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 1, 2 }, ReadUInt16(sourceA, 2));

        SeedUInt16(sourceA, 10, 20);
        SeedUInt16(sourceB, 30, 40);
        VectorPermute2MicroOp replayed = MaterializeVperm2(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate: 0b_11_10);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 30, 40 }, ReadUInt16(sourceA, 2));

        VectorPermute2MicroOp faulting = MaterializeVperm2(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB: 0x10000000UL,
            immediate: 0b_11_10);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => faulting.Execute(ref core));
        Assert.Contains("VectorPermute2MicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 30, 40 }, ReadUInt16(sourceA, 2));
    }

    [Fact]
    public void Vperm2_FailClosedBoundaries_RejectAdjacentContoursSidebandsAndRawStreamFallback()
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
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(streamLength: 3));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(stride: 2));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(immediate: 0x10));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVperm2(dataType: (DataTypeEnum)0xFF));

        InitializeMemorySubsystem();
        const ulong sourceA = 0x4480UL;
        const ulong sourceB = 0x44C0UL;
        SeedUInt16(sourceA, 1, 2);
        SeedUInt16(sourceB, 3, 4);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVperm2Instruction(
            DataTypeEnum.UINT16,
            sourceA,
            sourceB,
            immediate: 0b_11_10);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("VPERM2", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 1, 2 }, ReadUInt16(sourceA, 2));
    }

    [Fact]
    public void Vperm2_CompilerSurfaceRemainsNoEmissionForDeltaHelpers()
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
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Perm2", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, IsClosedVectorTransposeHelperName);
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("DotWide", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("QueryAbi", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Topology", StringComparison.OrdinalIgnoreCase));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerVectorHelperClosedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VPERM2", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VPERM2", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VPERM2", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VPERM2", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVperm2", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVperm2", compilerSource, StringComparison.OrdinalIgnoreCase);
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

    private static VectorPermute2MicroOp MaterializeVperm2(
        DataTypeEnum dataType = DataTypeEnum.UINT16,
        ulong sourceA = 0x4000UL,
        ulong sourceB = 0x4040UL,
        ushort immediate = 0b_11_10,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort stride = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVperm2Instruction(
            dataType,
            sourceA,
            sourceB,
            immediate,
            streamLength,
            predicateMask,
            tailAgnostic: false,
            maskAgnostic: false);
        instruction.Stride = stride;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorPermute2MicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VPERM2,
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5B00, bundleSerial: 1111);
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
            new VliwDecoderV4().DecodeInstructionBundle(rawSlots, bundleAddress: 0x5B40, bundleSerial: 1112);
        return new BundleLegalityAnalyzer().Analyze(decoded);
    }

    private static VLIW_Instruction CreateVperm2Instruction(
        DataTypeEnum dataType,
        ulong sourceA,
        ulong sourceB,
        ushort immediate,
        uint streamLength = 2,
        byte predicateMask = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false) =>
        InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VPERM2,
            dataType,
            sourceA,
            src2Ptr: sourceB,
            streamLength,
            stride: 0,
            predicateMask,
            immediate,
            tailAgnostic,
            maskAgnostic);

    private static void Publish(VectorPermute2MicroOp microOp, ref Processor.CPU_Core core)
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
