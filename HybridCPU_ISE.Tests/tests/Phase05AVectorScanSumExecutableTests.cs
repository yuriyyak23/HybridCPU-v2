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

namespace HybridCPU_ISE.Tests.Phase05A;

public sealed class Phase05AVectorScanSumExecutableTests
{
    [Fact]
    public void VscanSum_OpcodeStatusClassifierDecoderProjectionAndMaterializer_CloseSelectedRuntimeChain()
    {
        const ulong destinationBase = 0x900UL;
        const ulong sourceBase = 0x500UL;

        Assert.Equal(123, (int)InstructionsEnum.VZEXT);
        Assert.Equal(124, (int)InstructionsEnum.VSCAN_SUM);
        Assert.Equal(136, (int)InstructionsEnum.VCOMPRESS);
        Assert.Equal((ushort)InstructionsEnum.VSCAN_SUM, IsaOpcodeValues.VSCAN_SUM);

        InstructionSupportStatus status = InstructionSupportStatusCatalog.GetStatus("VSCAN.SUM");
        Assert.Equal(IsaInstructionStatus.OptionalEnabled, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.ConformanceTested, status.RuntimeEvidence);
        Assert.Equal("VectorScanPrefixPublication", status.ExtensionName);
        Assert.True(status.IsExecutableClaim);
        Assert.True(status.HasNumericOpcode);
        Assert.True(status.HasRuntimeOpcodeMetadata);
        Assert.True(status.HasCanonicalDecoderAcceptance);
        Assert.True(status.HasRegistryFactory);
        Assert.True(status.HasExecutionSemantics);

        Assert.Contains("VSCAN.SUM", IsaV4Surface.OptionalEnabledOpcodes);
        Assert.Equal("ALU", IsaV4Surface.PipelineClassMap["VSCAN.SUM"]);
        Assert.Equal(InstructionClass.ScalarAlu, InstructionClassifier.GetClass(InstructionsEnum.VSCAN_SUM));
        Assert.Equal(SerializationClass.Free, InstructionClassifier.GetSerializationClass(InstructionsEnum.VSCAN_SUM));

        OpcodeInfo info = Assert.NotNull(OpcodeRegistry.GetInfo((uint)InstructionsEnum.VSCAN_SUM));
        Assert.Equal("VSCAN.SUM", info.Mnemonic);
        Assert.True(info.IsVector);
        Assert.True(info.SupportsMasking);
        Assert.True(OpcodeRegistry.IsScanOp((uint)InstructionsEnum.VSCAN_SUM));
        Assert.False(OpcodeRegistry.IsReductionOp((uint)InstructionsEnum.VSCAN_SUM));
        Assert.False(OpcodeRegistry.IsWidenNarrowConvertOp((uint)InstructionsEnum.VSCAN_SUM));
        Assert.True(OpcodeRegistry.RequiresVectorPayloadProjection((uint)InstructionsEnum.VSCAN_SUM));

        VectorLegalityMatrixRow row = VectorLegalityMatrix.GetRow(InstructionsEnum.VSCAN_SUM);
        Assert.Equal("VectorScanPrefixPublication", row.FamilyName);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.OneDimensional);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.IndexedAddressing);
        Assert.Equal(VectorContourLegalityStatus.FailClosed, row.TwoDimensionalAddressing);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.Masked);
        Assert.Equal(VectorContourLegalityStatus.Executable, row.TailMaskPolicy);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.Reduction);
        Assert.Equal(VectorContourLegalityStatus.NotApplicable, row.DescriptorBacked);

        Assert.True(InstructionRegistry.IsRegistered((uint)InstructionsEnum.VSCAN_SUM));
        MicroOpDescriptor descriptor = InstructionRegistry.GetDescriptor((uint)InstructionsEnum.VSCAN_SUM);
        Assert.False(descriptor.WritesRegister);
        Assert.False(descriptor.IsMemoryOp);

        VLIW_Instruction instruction = CreateVscanSumInstruction(
            DataTypeEnum.UINT32,
            destinationBase,
            sourceBase,
            streamLength: 4);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);

        Assert.Equal((ushort)InstructionsEnum.VSCAN_SUM, ir.CanonicalOpcode.Value);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rd);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs1);
        Assert.Equal(VLIW_Instruction.NoArchReg, ir.Rs2);
        Assert.NotNull(ir.VectorPayload);
        VectorInstructionPayload payload = ir.VectorPayload!.Value;
        Assert.Equal(destinationBase, payload.PrimaryPointer);
        Assert.Equal(sourceBase, payload.SecondaryPointer);
        Assert.Equal(4U, payload.StreamLength);
        Assert.Equal((byte)DataTypeEnum.UINT32, payload.DataType);
        Assert.False(payload.Indexed);
        Assert.False(payload.Is2D);
        Assert.False(payload.TailAgnostic);
        Assert.False(payload.MaskAgnostic);

        VectorScanSumMicroOp scan = Assert.IsType<VectorScanSumMicroOp>(microOp);
        Assert.Equal((uint)InstructionsEnum.VSCAN_SUM, scan.OpCode);
        Assert.Equal(InstructionClass.ScalarAlu, scan.InstructionClass);
        Assert.Equal(SerializationClass.Free, scan.SerializationClass);
        Assert.Equal(SlotClass.AluClass, scan.Placement.RequiredSlotClass);
        Assert.Empty(scan.ReadRegisters);
        Assert.Empty(scan.WriteRegisters);
        Assert.Contains(scan.ReadMemoryRanges, range => range.Address == sourceBase && range.Length == 16);
        Assert.Contains(scan.WriteMemoryRanges, range => range.Address == destinationBase && range.Length == 16);
    }

    [Fact]
    public void VscanSum_GoldenVectors_PublishInclusivePrefixOnlyAtWriteBack()
    {
        const ulong sourceBase = 0x540UL;
        const ulong destinationBase = 0x940UL;

        InitializeMemorySubsystem();
        SeedUInt32(sourceBase, 1, 2, 3, 4);
        SeedUInt32(destinationBase, 0xAAAA_AAAAU, 0xBBBB_BBBBU, 0xCCCC_CCCCU, 0xDDDD_DDDDU);

        var core = new Processor.CPU_Core(0);
        VectorScanSumMicroOp scan = MaterializeVscanSum(
            DataTypeEnum.UINT32,
            destinationBase,
            sourceBase,
            streamLength: 4);

        Assert.True(scan.Execute(ref core));
        Assert.Equal(new uint[] { 0xAAAA_AAAAU, 0xBBBB_BBBBU, 0xCCCC_CCCCU, 0xDDDD_DDDDU },
            ReadUInt32(destinationBase, 4));
        Assert.Equal(new[] { destinationBase, destinationBase + 4, destinationBase + 8, destinationBase + 12 },
            scan.GetStagedWrites().Select(write => write.Address).ToArray());

        Publish(scan, ref core);
        Assert.Equal(new uint[] { 1, 3, 6, 10 }, ReadUInt32(destinationBase, 4));

        const ulong signedSource = 0x580UL;
        const ulong signedDestination = 0x980UL;
        SeedInt16(signedSource, -2, 5, -1, 3);
        SeedInt16(signedDestination, 11, 12, 13, 14);
        VectorScanSumMicroOp signedScan = MaterializeVscanSum(
            DataTypeEnum.INT16,
            signedDestination,
            signedSource,
            streamLength: 4);
        Assert.True(signedScan.Execute(ref core));
        Publish(signedScan, ref core);
        Assert.Equal(new short[] { -2, 3, 2, 5 }, ReadInt16(signedDestination, 4));
    }

    [Fact]
    public void VscanSum_MaskAndTailPolicy_ProjectAndKeepInactiveOrOutOfLengthDestinationUndisturbed()
    {
        const ulong sourceBase = 0x5C0UL;
        const ulong destinationBase = 0x9C0UL;

        InitializeMemorySubsystem();
        SeedUInt16(sourceBase, 1, 2, 3, 4, 5);
        SeedUInt16(destinationBase, 100, 101, 102, 103, 104);

        VLIW_Instruction instruction = CreateVscanSumInstruction(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBase,
            streamLength: 4,
            predicateMask: 1,
            tailAgnostic: true,
            maskAgnostic: true);
        (InstructionIR ir, MicroOp microOp) = DecodeAndMaterialize(instruction);
        Assert.True(ir.VectorPayload!.Value.TailAgnostic);
        Assert.True(ir.VectorPayload!.Value.MaskAgnostic);

        VectorScanSumMicroOp scan = Assert.IsType<VectorScanSumMicroOp>(microOp);
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(1, 0b0101UL);

        Assert.True(scan.Execute(ref core));
        Publish(scan, ref core);

        Assert.Equal(new ushort[] { 1, 101, 4, 103, 104 }, ReadUInt16(destinationBase, 5));
    }

    [Fact]
    public void VscanSum_ReplayRollbackAndFaultAbort_DoNotPublishDiscardedOrFailedStagedResults()
    {
        const ulong sourceBase = 0x600UL;
        const ulong destinationBase = 0xA00UL;

        InitializeMemorySubsystem(mappedBytes: 0x1000UL);
        SeedUInt16(sourceBase, 1, 2, 3);
        SeedUInt16(destinationBase, 0, 0, 0);

        var core = new Processor.CPU_Core(0);
        VectorScanSumMicroOp discarded = MaterializeVscanSum(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBase,
            streamLength: 3);
        Assert.True(discarded.Execute(ref core));
        Assert.Equal(new ushort[] { 0, 0, 0 }, ReadUInt16(destinationBase, 3));

        SeedUInt16(sourceBase, 10, 20, 30);
        VectorScanSumMicroOp replayed = MaterializeVscanSum(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBase,
            streamLength: 3);
        Assert.True(replayed.Execute(ref core));
        Publish(replayed, ref core);
        Assert.Equal(new ushort[] { 10, 30, 60 }, ReadUInt16(destinationBase, 3));

        const ulong faultingSource = ulong.MaxValue - 1UL;
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB, 0xCCCC);
        VectorScanSumMicroOp sourceFault = MaterializeVscanSum(
            DataTypeEnum.UINT16,
            destinationBase,
            faultingSource,
            streamLength: 3);
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => sourceFault.Execute(ref core));
        Assert.Contains("VectorScanSumMicroOp.Execute", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB, 0xCCCC }, ReadUInt16(destinationBase, 3));
    }

    [Fact]
    public void VscanSum_FailClosedBoundaries_RejectAdjacentFormsSidebandsAndRawStreamFallback()
    {
        string[] unselected =
        [
            "VSCAN.MIN", "VSCAN.MAX",
            "VLDSEG2", "VLDSEG4", "VLDSEG8",
            "VSTSEG2", "VSTSEG4", "VSTSEG8",
            "VZIP", "VUNZIP", "VINTERLEAVE", "VDEINTERLEAVE"
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

        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(indexed: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(indexed: true, is2D: true));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(immediate: 1));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(stride: 2));
        Assert.Throws<DecodeProjectionFaultException>(() => MaterializeVscanSum(dataType: DataTypeEnum.FLOAT32));

        InitializeMemorySubsystem();
        const ulong sourceBase = 0x640UL;
        const ulong destinationBase = 0xA40UL;
        SeedUInt16(sourceBase, 1, 2);
        SeedUInt16(destinationBase, 0xAAAA, 0xBBBB);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction rawInstruction = CreateVscanSumInstruction(
            DataTypeEnum.UINT16,
            destinationBase,
            sourceBase,
            streamLength: 2);
        InvalidOperationException rawEx = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in rawInstruction));
        Assert.Contains("raw StreamEngine.Execute", rawEx.Message, StringComparison.Ordinal);
        Assert.Equal(new ushort[] { 0xAAAA, 0xBBBB }, ReadUInt16(destinationBase, 2));
    }

    [Fact]
    public void VscanSum_CompilerSurfaceRemainsNoEmissionForScanSegmentAndStructureHelpers()
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
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Vscan", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Scan", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Segment", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Vldseg", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Vstseg", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Zip", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicMethodNames, name => name.Contains("Interleave", StringComparison.OrdinalIgnoreCase));

        string compilerSource = ReadAllCompilerSource();
        Assert.DoesNotContain("VSCAN", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VSCAN_SUM", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VLDSEG", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VSTSEG", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VZIP", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VUNZIP", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VINTERLEAVE", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("VDEINTERLEAVE", compilerSource, StringComparison.Ordinal);
    }

    private static VectorScanSumMicroOp MaterializeVscanSum(
        DataTypeEnum dataType = DataTypeEnum.UINT16,
        ulong destinationBase = 0x900UL,
        ulong sourceBase = 0x500UL,
        uint streamLength = 2,
        byte predicateMask = 0,
        ushort immediate = 0,
        ushort stride = 0,
        bool indexed = false,
        bool is2D = false)
    {
        VLIW_Instruction instruction = CreateVscanSumInstruction(
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

        return Assert.IsType<VectorScanSumMicroOp>(
            InstructionRegistry.CreateMicroOp(
                (uint)InstructionsEnum.VSCAN_SUM,
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
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x5500, bundleSerial: 1051);
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

    private static VLIW_Instruction CreateVscanSumInstruction(
        DataTypeEnum dataType,
        ulong destinationBase,
        ulong sourceBase,
        uint streamLength,
        byte predicateMask = 0,
        ushort immediate = 0,
        bool tailAgnostic = false,
        bool maskAgnostic = false) =>
        InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VSCAN_SUM,
            dataType,
            destinationBase,
            sourceBase,
            streamLength,
            stride: 0,
            predicateMask,
            immediate,
            tailAgnostic,
            maskAgnostic);

    private static void Publish(VectorScanSumMicroOp microOp, ref Processor.CPU_Core core)
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

    private static void SeedInt16(ulong address, params short[] values)
    {
        byte[] data = new byte[values.Length * sizeof(short)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(short));
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

    private static short[] ReadInt16(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(short)],
            address,
            (ulong)(count * sizeof(short)));
        return Enumerable.Range(0, count)
            .Select(index => BitConverter.ToInt16(bytes, index * sizeof(short)))
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
