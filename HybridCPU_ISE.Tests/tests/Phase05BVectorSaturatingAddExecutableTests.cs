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
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase05B;

public sealed class Phase05BVectorSaturatingAddExecutableTests
{
    [Fact]
    public void VaddSat_StatusDecoderProjectionAndMaterializer_CloseSelectedPolicyBitChain()
    {
        const ulong destinationBase = 0xB00UL;
        const ulong sourceBBase = 0x700UL;

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VADD.SAT");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorSaturatingAddPolicy", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VADD));
        Assert.Equal("VADD", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.True(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VADD));
        Assert.False(OpcodeRegistry.SupportsSaturatingAddPolicy((uint)InstructionsEnum.VSUB));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VADD));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VADD);
        Assert.Equal("VectorBinaryComputeCarrier", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);

        VLIW_Instruction instruction = CreateVaddInstruction(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBBase,
            streamLength: 4,
            saturating: true);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VADD, ir.CanonicalOpcode.Value);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.True(payload.Saturating);
        Assert.Equal(destinationBase, payload.PrimaryPointer);
        Assert.Equal(sourceBBase, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT8, payload.DataType);

        VectorSaturatingAddMicroOp sat = Assert.IsType<VectorSaturatingAddMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VADD, sat.OpCode);
        Assert.True(sat.Instruction.Saturating);
        Assert.Equal(InstructionClass.ScalarAlu, sat.InstructionClass);
        Assert.Equal(SerializationClass.Free, sat.SerializationClass);
        Assert.Equal(SlotClass.AluClass, sat.Placement.RequiredSlotClass);
        Assert.Empty(sat.ReadRegisters);
        Assert.Empty(sat.WriteRegisters);
        Assert.Contains(sat.ReadMemoryRanges, range => range.Address == destinationBase && range.Length == 4);
        Assert.Contains(sat.ReadMemoryRanges, range => range.Address == sourceBBase && range.Length == 4);
        Assert.Contains(sat.WriteMemoryRanges, range => range.Address == destinationBase && range.Length == 4);
    }

    [Fact]
    public void VaddSat_GoldenBoundaryVectors_PublishClampedResultsOnlyAtWriteBack()
    {
        const ulong unsignedDestination = 0xB40UL;
        const ulong unsignedSourceB = 0x740UL;

        InitializeMemorySubsystem();
        SeedUInt8(unsignedDestination, 250, 1, 255, 10);
        SeedUInt8(unsignedSourceB, 10, 2, 1, 20);

        var core = new Processor.CPU_Core(0);
        VectorSaturatingAddMicroOp unsignedSat = MaterializeVaddSat(
            DataTypeEnum.UINT8,
            unsignedDestination,
            unsignedSourceB,
            streamLength: 4);

        Assert.True(unsignedSat.Execute(ref core));
        Assert.Equal(new byte[] { 250, 1, 255, 10 }, ReadUInt8(unsignedDestination, 4));
        Assert.Equal(new[] { unsignedDestination, unsignedDestination + 1, unsignedDestination + 2, unsignedDestination + 3 },
            unsignedSat.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(unsignedSat, ref core);
        Assert.Equal(new byte[] { 255, 3, 255, 30 }, ReadUInt8(unsignedDestination, 4));
        Assert.True(core.ExceptionStatus.OverflowCount >= 2);

        const ulong signedDestination = 0xB80UL;
        const ulong signedSourceB = 0x780UL;
        SeedInt8(signedDestination, 120, -120, 5, -5);
        SeedInt8(signedSourceB, 20, -20, -6, 1);

        VectorSaturatingAddMicroOp signedSat = MaterializeVaddSat(
            DataTypeEnum.INT8,
            signedDestination,
            signedSourceB,
            streamLength: 4);
        Assert.True(signedSat.Execute(ref core));
        Publish(signedSat, ref core);
        Assert.Equal(new sbyte[] { 127, -128, -1, -4 }, ReadInt8(signedDestination, 4));
    }

    [Fact]
    public void VaddSat_MaskAndTailPolicy_ProjectAndKeepInactiveOrOutOfLengthDestinationUndisturbed()
    {
        const ulong destinationBase = 0xBC0UL;
        const ulong sourceBBase = 0x7C0UL;

        InitializeMemorySubsystem();
        SeedUInt8(destinationBase, 250, 250, 250, 250, 42);
        SeedUInt8(sourceBBase, 10, 10, 10, 10, 10);

        VLIW_Instruction instruction = CreateVaddInstruction(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBBase,
            streamLength: 4,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: true,
            saturating: true);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.True(ir.VectorPayload!.Value.MaskAgnostic);
        Assert.True(ir.VectorPayload!.Value.Saturating);

        VectorSaturatingAddMicroOp sat = Assert.IsType<VectorSaturatingAddMicroOp>(microOp);
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);

        Assert.True(sat.Execute(ref core));
        Publish(sat, ref core);

        Assert.Equal(new byte[] { 255, 250, 255, 250, 42 }, ReadUInt8(destinationBase, 5));
    }

    [Fact]
    public void VaddSat_ReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong destinationBase = 0xC00UL;
        const ulong sourceBBase = 0x800UL;

        InitializeMemorySubsystem(mappedBytes: 0x1000UL);
        SeedUInt16(destinationBase, 1, 2);
        SeedUInt16(sourceBBase, 10, 20);

        var core = new Processor.CPU_Core(0);
        VectorSaturatingAddMicroOp discarded = MaterializeVaddSat(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBBase,
            streamLength: 2);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 1, 2 }, ReadUInt16(destinationBase, 2));

        SeedUInt16(destinationBase, 65000, 2);
        SeedUInt16(sourceBBase, 1000, 20);
        VectorSaturatingAddMicroOp replayed = MaterializeVaddSat(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBBase,
            streamLength: 2);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 65535, 22 }, ReadUInt16(destinationBase, 2));

        const ulong faultingSourceB = ulong.MaxValue - 1UL;
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB);
        VectorSaturatingAddMicroOp sourceFault = MaterializeVaddSat(
            DataTypeEnum.UINT16,
            destinationBase,
            faultingSourceB,
            streamLength: 2);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => sourceFault.Execute(ref core));
        Assert.Contains("VectorSaturatingAddMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB }, ReadUInt16(destinationBase, 2));
    }

    [Fact]
    public void VaddSat_FailClosedBoundaries_RejectAdjacentPoliciesSidebandsAndRawStreamFallback()
    {
        string[] unselected =
        [
            "VSUB.SAT", "VMUL.SAT", "VSLL.SAT", "VSRL.SAT", "VSRA.SAT",
            "VAVG", "VAVG.R", "VCLIP"
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

        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeSaturatingBinary(InstructionsEnum.VSUB));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeSaturatingBinary(InstructionsEnum.VMUL));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeSaturatingBinary(InstructionsEnum.VSLL));

        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(stride: 1, dataType: DataTypeEnum.UINT16));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVaddSat(dataType: DataTypeEnum.FLOAT32));

        MicroOp nonSaturating = MaterializeBinary(InstructionsEnum.VADD, saturating: false);
        Assert.IsType<VectorBinaryOpMicroOp>(nonSaturating);

        InitializeMemorySubsystem();
        const ulong destinationBase = 0xC40UL;
        const ulong sourceBBase = 0x840UL;
        SeedUInt8(destinationBase, 250, 1);
        SeedUInt8(sourceBBase, 10, 2);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVaddInstruction(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBBase,
            streamLength: 2,
            saturating: true);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("saturating policy bit", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new byte[] { 250, 1 }, ReadUInt8(destinationBase, 2));
    }

    [Fact]
    public void VaddSat_NonSaturatingBaselineAndCompilerNoEmissionBoundariesRemainClosed()
    {
        const ulong destinationBase = 0xC80UL;
        const ulong sourceBBase = 0x880UL;

        InitializeMemorySubsystem();
        SeedUInt8(destinationBase, 250, 1);
        SeedUInt8(sourceBBase, 10, 2);

        var core = new Processor.CPU_Core(0);
        VectorBinaryOpMicroOp baseline = Assert.IsType<VectorBinaryOpMicroOp>(
            MaterializeBinary(InstructionsEnum.VADD, DataTypeEnum.UINT8, destinationBase, sourceBBase, 2, saturating: false));
        Assert.True(baseline.Execute(ref core));
        Assert.Equal(new byte[] { 4, 3 }, ReadUInt8(destinationBase, 2));

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
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Saturating", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Saturate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("VaddSat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("SatAdd", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("FixedPoint", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Clip", StringComparison.OrdinalIgnoreCase));

        string compilerSource = ReadAllCompilerSource();
        Assert.DoesNotContain("VADD.SAT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VADDSAT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VSUB.SAT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VMUL.SAT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VAVG", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VCLIP", compilerSource, StringComparison.Ordinal);
    }

    private static VectorSaturatingAddMicroOp MaterializeVaddSat(
        DataTypeEnum dataType = DataTypeEnum.UINT8,
        ulong destinationBase = 0xB00UL,
        ulong sourceBBase = 0x700UL,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort immediate = 0,
        ushort stride = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVaddInstruction(
            dataType,
            destinationBase,
            sourceBBase,
            streamLength,
            predicateMask,
            immediate,
            tailAgnostic: false,
            maskAgnostic: false,
            saturating: true);
        instruction.Stride = stride;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorSaturatingAddMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VADD,
                CreateVectorContext(instruction)));
    }

    private static MicroOp MaterializeSaturatingBinary(InstructionsEnum op) =>
        MaterializeBinary(op, saturating: true);

    private static MicroOp MaterializeBinary(
        InstructionsEnum op,
        DataTypeEnum dataType = DataTypeEnum.UINT8,
        ulong destinationBase = 0xB00UL,
        ulong sourceBBase = 0x700UL,
        uint streamLength = 2,
        bool saturating = false)
    {
        VLIW_Instruction instruction = CreateVaddInstruction(
            dataType,
            destinationBase,
            sourceBBase,
            streamLength,
            saturating: saturating);
        instruction.OpCode = (uint)op;
        return InstructionRegistry.CreateMicroOp((uint)op, CreateVectorContext(instruction));
    }

    private static (InstructionIR Ir, MicroOp MicroOp) DecodeAndMaterialize(
        VLIW_Instruction instruction,
        int slotIndex = 3)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5600, bundleSerial: 1052);
        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        InstructionIR ir = canonicalBundle.GetDecodedSlot(slotIndex).RequireInstruction();
        return (ir, Assert.IsAssignableFrom<MicroOp>(carrierBundle[slotIndex]));
    }

    private static DecoderContext CreateVectorContext(in VLIW_Instruction instruction) =>
        new()
        {
            OpCode = instruction.OpCode,
            HasDataType = true,
            DataType = instruction.DataType,
            HasImmediate = true,
            Immediate = instruction.Immediate,
            HasVectorAddressingContour = true,
            IndexedAddressing = instruction.Indexed,
            Is2DAddressing = instruction.Is2D,
            HasVectorPayload = true,
            VectorPrimaryPointer = instruction.DestSrc1Pointer,
            VectorSecondaryPointer = instruction.Src2Pointer,
            VectorStreamLength = instruction.StreamLength,
            VectorStride = instruction.Stride,
            VectorRowStride = instruction.RowStride,
            PredicateMask = instruction.PredicateMask,
            TailAgnostic = instruction.TailAgnostic,
            MaskAgnostic = instruction.MaskAgnostic,
            Saturating = instruction.Saturating
        };

    private static VLIW_Instruction CreateVaddInstruction(
        DataTypeEnum dataType,
        ulong destinationBase,
        ulong sourceBBase,
        uint streamLength,
        byte predicateMask = 0,
        ushort immediate = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false,
        bool saturating = false)
    {
        VLIW_Instruction instruction = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VADD,
            dataType,
            destinationBase,
            sourceBBase,
            streamLength,
            stride: 0,
            predicateMask,
            immediate,
            tailAgnostic,
            maskAgnostic);
        instruction.Saturating = saturating;
        return instruction;
    }

    private static void Publish(VectorSaturatingAddMicroOp microOp, ref Processor.CPU_Core core)
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

    private static void WriteBytes(ulong address, byte[] data) =>
        Processor.MainMemory.WriteToPosition(data, address);

    private static void SeedUInt8(ulong address, params byte[] values) =>
        WriteBytes(address, values);

    private static void SeedInt8(ulong address, params sbyte[] values) =>
        WriteBytes(address, values.Select(static value => unchecked((byte)value)).ToArray());

    private static void SeedUInt16(ulong address, params ushort[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ushort)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ushort));
        }

        WriteBytes(address, data);
    }

    private static byte[] ReadUInt8(ulong address, int count) =>
        Processor.MainMemory.ReadFromPosition(new byte[count], address, (ulong)count);

    private static sbyte[] ReadInt8(ulong address, int count) =>
        ReadUInt8(address, count)
            .Select(static value => unchecked((sbyte)value))
            .ToArray();

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

    private static string ReadAllCompilerSource()
    {
        string compilerRoot = Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler");
        IEnumerable<string> files = Directory.EnumerateFiles(
                compilerRoot,
                "*.cs",
                SearchOption.AllDirectories)
            .Where(filePath => !CompatFreezeScanner.IsGeneratedPath(filePath));

        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }
}
