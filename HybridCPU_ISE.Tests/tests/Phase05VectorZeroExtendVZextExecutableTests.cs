using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.Threading;
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
using HybridCPU_ISE.CloseToRTL.Memory.MMU;

namespace HybridCPU_ISE.Tests;

public sealed class VectorZeroExtendVzextExecutableTests
{
    [Fact]
    public void Vzext_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong destinationBase = 0x800UL;
        const ulong sourceBase = 0x400UL;

        Assert.Equal(122, (int)InstructionsEnum.VMSBF);
        Assert.Equal(123, (int)InstructionsEnum.VZEXT);
        Assert.Equal(136, (int)InstructionsEnum.VCOMPRESS);
        Assert.Equal((ushort)InstructionsEnum.VZEXT, IsaOpcodeValues.VZEXT);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VZEXT");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorZeroExtendPublication", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VZEXT", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VZEXT"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VZEXT));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VZEXT));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VZEXT));
        Assert.Equal("VZEXT", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.True(OpcodeRegistry.IsWidenNarrowConvertOp((uint)InstructionsEnum.VZEXT));
        Assert.False(OpcodeRegistry.IsMaskManipOp((uint)InstructionsEnum.VZEXT));
        Assert.False(OpcodeRegistry.IsComparisonOp((uint)InstructionsEnum.VZEXT));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)InstructionsEnum.VZEXT));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VZEXT);
        Assert.Equal("VectorZeroExtendPublication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VZEXT));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VZEXT);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVzextInstruction(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBase,
            streamLength: 3);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VZEXT, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(destinationBase, payload.PrimaryPointer);
        Assert.Equal(sourceBase, payload.SecondaryPointer);
        Assert.Equal(3U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT16, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);
        Assert.False(payload.TailAgnostic);
        Assert.False(payload.MaskAgnostic);

        VectorZeroExtendMicroOp vzext = Assert.IsType<VectorZeroExtendMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VZEXT, vzext.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, vzext.InstructionClass);
        Assert.Equal(SerializationClass.Free, vzext.SerializationClass);
        Assert.Equal(SlotClass.AluClass, vzext.Placement.RequiredSlotClass);
        Assert.Empty(vzext.ReadRegisters);
        Assert.Empty(vzext.WriteRegisters);
        Assert.Contains(vzext.ReadMemoryRanges, range => range.Address == sourceBase && range.Length == 6);
        Assert.Contains(vzext.WriteMemoryRanges, range => range.Address == destinationBase && range.Length == 12);
    }

    [Fact]
    public void Vzext_ExecuteStagesUInt8ToUInt16AndPublishesDestinationOnlyAtWriteBack()
    {
        const ulong sourceBase = 0x420UL;
        const ulong destinationBase = 0x820UL;

        InitializeMemorySubsystem();
        WriteBytes(sourceBase, [0x00, 0x01, 0x80, 0xFF]);
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB, 0xCCCC, 0xDDDD);

        VectorZeroExtendMicroOp vzext = MaterializeVzext(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBase,
            streamLength: 4);

        var core = new Processor.CPU_Core(0);
        Assert.True(vzext.Execute(ref core));

        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB, 0xCCCC, 0xDDDD }, ReadUInt16(destinationBase, 4));
        Assert.Equal(new[] { destinationBase, destinationBase + 2, destinationBase + 4, destinationBase + 6 },
            vzext.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(vzext, ref core);
        Assert.Equal(new ushort[] { 0, 1, 0x80, 0xFF }, ReadUInt16(destinationBase, 4));
    }

    [Fact]
    public void Vzext_UInt16AndUInt32BoundaryCases_UseNextUnsignedDestinationWidth()
    {
        InitializeMemorySubsystem();

        const ulong source16 = 0x440UL;
        const ulong destination32 = 0x840UL;
        SeedUInt16(source16, 0x0000, 0x8000, 0xFFFF);
        SeedUInt32(destination32, 0xAAAA_AAAAU, 0xBBBB_BBBBU, 0xCCCC_CCCCU);
        var core = new Processor.CPU_Core(0);
        VectorZeroExtendMicroOp extend16 = MaterializeVzext(
            DataTypeEnum.UINT16,
            destination32,
            source16,
            streamLength: 3);
        Assert.True(extend16.Execute(ref core));
        Publish(extend16, ref core);
        Assert.Equal(new uint[] { 0U, 0x8000U, 0xFFFFU }, ReadUInt32(destination32, 3));

        const ulong source32 = 0x480UL;
        const ulong destination64 = 0x880UL;
        SeedUInt32(source32, 0x0000_0000U, 0x8000_0000U, 0xFFFF_FFFFU);
        SeedUInt64(destination64, 0xAAAA_AAAA_AAAA_AAAAUL, 0xBBBB_BBBB_BBBB_BBBBUL, 0xCCCC_CCCC_CCCC_CCCCUL);
        VectorZeroExtendMicroOp extend32 = MaterializeVzext(
            DataTypeEnum.UINT32,
            destination64,
            source32,
            streamLength: 3);
        Assert.True(extend32.Execute(ref core));
        Publish(extend32, ref core);
        Assert.Equal(new ulong[] { 0UL, 0x8000_0000UL, 0xFFFF_FFFFUL }, ReadUInt64(destination64, 3));
    }

    [Fact]
    public void Vzext_MaskAndTailPolicy_ProjectAndKeepInactiveOrOutOfLengthDestinationUndisturbed()
    {
        const ulong sourceBase = 0x4C0UL;
        const ulong destinationBase = 0x8C0UL;

        InitializeMemorySubsystem();
        WriteBytes(sourceBase, [1, 2, 3, 4, 5]);
        SeedUInt16(destinationBase, 100, 101, 102, 103, 104);

        VLIW_Instruction instruction = CreateVzextInstruction(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBase,
            streamLength: 4,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: true);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.True(ir.VectorPayload!.Value.MaskAgnostic);

        VectorZeroExtendMicroOp vzext = Assert.IsType<VectorZeroExtendMicroOp>(microOp);
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);

        Assert.True(vzext.Execute(ref core));
        Publish(vzext, ref core);

        Assert.Equal(new ushort[] { 1, 101, 3, 103, 104 }, ReadUInt16(destinationBase, 5));
    }

    [Fact]
    public void Vzext_FaultAndReplay_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong sourceBase = 0x500UL;
        const ulong destinationBase = 0x900UL;

        InitializeMemorySubsystem(mappedBytes: 0x1000UL);
        WriteBytes(sourceBase, [11, 22]);
        SeedUInt16(destinationBase, 0, 0);

        var core = new Processor.CPU_Core(0);
        VectorZeroExtendMicroOp discarded = MaterializeVzext(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBase,
            streamLength: 2);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 0, 0 }, ReadUInt16(destinationBase, 2));

        WriteBytes(sourceBase, [33, 44]);
        VectorZeroExtendMicroOp replayed = MaterializeVzext(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBase,
            streamLength: 2);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 33, 44 }, ReadUInt16(destinationBase, 2));

        const ulong faultingSource = ulong.MaxValue - 1UL;
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB);
        VectorZeroExtendMicroOp sourceFault = MaterializeVzext(
            DataTypeEnum.UINT8,
            destinationBase,
            faultingSource,
            streamLength: 2);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => sourceFault.Execute(ref core));
        Assert.Contains("VectorZeroExtendMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB }, ReadUInt16(destinationBase, 2));
    }

    [Fact]
    public void Vzext_FailClosedBoundaries_RejectUnselectedFormsSidebandsAndRawStreamFallback()
    {
        string[] unselected =
        [
            "VWADD", "VWADDU", "VWSUB", "VWSUBU", "VWMUL", "VWMULU", "VWMACC",
            "VNSRL", "VNSRA", "VSEXT", "VCVT.I", "VCVT.U", "VCVT.F"
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

        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(stride: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(dataType: DataTypeEnum.INT8));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVzext(dataType: DataTypeEnum.FLOAT32));

        InitializeMemorySubsystem();
        const ulong sourceBase = 0x600UL;
        const ulong destinationBase = 0xA00UL;
        WriteBytes(sourceBase, [1, 2]);
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVzextInstruction(
            DataTypeEnum.UINT8,
            destinationBase,
            sourceBase,
            streamLength: 2);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("raw StreamEngine.Execute", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB }, ReadUInt16(destinationBase, 2));
    }

    [Fact]
    public void Vzext_CompilerSurfaceRemainsNoEmissionForTypedWidenNarrowConvertHelpers()
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
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Vzext", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Widen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Narrow", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Convert", StringComparison.OrdinalIgnoreCase));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerVectorHelperClosedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("CompilerVectorVlmBlockedAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VZEXT", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VWADD", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VNSRL", compilerSource, StringComparison.Ordinal);
        Assert.Contains("VCVT.I", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.VZEXT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VZEXT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VZEXT", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVzext", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVzext", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.VWADD", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VWADD", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VWADD", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVwadd", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVwadd", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.VNSRL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VNSRL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VNSRL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVnsrl", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVnsrl", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.VCVT_I", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VCVT_I", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VCVT_I", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVcvtI", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVcvtI", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.VCVT_U", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VCVT_U", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VCVT_U", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVcvtU", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVcvtU", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InstructionsEnum.VCVT_F", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.VCVT_F", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.VCVT_F", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileVcvtF", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitVcvtF", compilerSource, StringComparison.OrdinalIgnoreCase);
    }

    private static VectorZeroExtendMicroOp MaterializeVzext(
        DataTypeEnum dataType = DataTypeEnum.UINT8,
        ulong destinationBase = 0x800UL,
        ulong sourceBase = 0x400UL,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort immediate = 0,
        ushort stride = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVzextInstruction(
            dataType,
            destinationBase,
            sourceBase,
            streamLength,
            predicateMask,
            immediate,
            tailAgnostic: false,
            maskAgnostic: false);
        instruction.Stride = stride;
        instruction.Indexed = indexed;
        instruction.Is2D = is2D;

        return Assert.IsType<VectorZeroExtendMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VZEXT,
                CreateVectorContext(instruction)));
    }

    private static (InstructionIR Ir, MicroOp MicroOp) DecodeAndMaterialize(
        VLIW_Instruction instruction,
        int slotIndex = 3)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[slotIndex] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5400, bundleSerial: 105);
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
            MaskAgnostic = instruction.MaskAgnostic
        };

    private static VLIW_Instruction CreateVzextInstruction(
        DataTypeEnum dataType,
        ulong destinationBase,
        ulong sourceBase,
        uint streamLength,
        byte predicateMask = 0,
        ushort immediate = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false) =>
        InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VZEXT,
            dataType,
            destinationBase,
            sourceBase,
            streamLength,
            stride: 0,
            predicateMask,
            immediate,
            tailAgnostic,
            maskAgnostic);

    private static void Publish(VectorZeroExtendMicroOp microOp, ref Processor.CPU_Core core)
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

    private static void SeedUInt16(ulong address, params ushort[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ushort)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ushort));
        }

        WriteBytes(address, data);
    }

    private static void SeedUInt32(ulong address, params uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(uint));
        }

        WriteBytes(address, data);
    }

    private static void SeedUInt64(ulong address, params ulong[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ulong)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ulong));
        }

        WriteBytes(address, data);
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

    private static uint[] ReadUInt32(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(uint)],
            address,
            (ulong)(count * sizeof(uint)));
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToUInt32(bytes, index * sizeof(uint)))
            .ToArray();
    }

    private static ulong[] ReadUInt64(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(ulong)],
            address,
            (ulong)(count * sizeof(ulong)));
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToUInt64(bytes, index * sizeof(ulong)))
            .ToArray();
    }

}
