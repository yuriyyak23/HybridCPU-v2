using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09PartialTransferAbortProofTests
{
    [Fact]
    public void MainlineVectorBinaryCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeMemoryPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VADD,
            expectedMicroOpType: typeof(VectorBinaryOpMicroOp));
    }

    [Fact]
    public void MainlineVectorUnaryCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeMemoryPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VNOT,
            expectedMicroOpType: typeof(VectorUnaryOpMicroOp));
    }

    [Fact]
    public void MainlineVectorReductionCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeScalarPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VREDSUM,
            expectedMicroOpType: typeof(VectorReductionMicroOp));
    }

    [Fact]
    public void MainlineVectorComparisonCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforePredicatePublication()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();
        SeedVectorWordMemory(0xFFC, 1U);
        SeedVectorWordMemory(0x100, 2U, 3U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0xA5UL);

        VLIW_Instruction inst = CreateVectorInstruction(
            InstructionsEnum.VCMPEQ,
            destSrc1Pointer: 0xFFC,
            src2Pointer: 0x100,
            immediate: 5,
            streamLength: 2,
            stride: 4);

        MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
        Assert.IsType<VectorComparisonMicroOp>(microOp);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: 0x2000));

        Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Contains(nameof(BurstIO.BurstRead), ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Equal(0xA5UL, core.GetPredicateRegister(5));
    }

    [Fact]
    public void MainlineVectorPermutationCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeMemoryPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VPERMUTE,
            expectedMicroOpType: typeof(VectorPermutationMicroOp),
            src2Pointer: 0x100,
            immediate: 0,
            seedSrc2: true);
    }

    [Fact]
    public void MainlineVectorSlideCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeMemoryPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VSLIDEUP,
            expectedMicroOpType: typeof(VectorSlideMicroOp),
            immediate: 1);
    }

    [Fact]
    public void MainlineVectorDotProductCarrier_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeMemoryPublication()
    {
        AssertMainlineVectorComputeAbortBeforePublication(
            InstructionsEnum.VDOT,
            expectedMicroOpType: typeof(VectorDotProductMicroOp),
            src2Pointer: 0x100,
            seedSrc2: true);
    }

    [Fact]
    public void RawStreamEngine1D_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeWriteBack()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();
        SeedVectorWordMemory(0xFFC, 10U);
        SeedVectorWordMemory(0x100, 1U, 2U);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VADD,
            DataTypeEnum.UINT32,
            destSrc1Ptr: 0xFFC,
            src2Ptr: 0x100,
            streamLength: 2,
            stride: sizeof(uint));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StreamEngine.Execute(ref core, in inst));
        Assert.Contains(nameof(BurstIO.BurstRead), ex.Message, StringComparison.Ordinal);
        Assert.Equal(10U, BitConverter.ToUInt32(Processor.MainMemory.ReadFromPosition(new byte[4], 0xFFC, 4), 0));
    }

    [Fact]
    public void RawStreamEngineDoubleBuffered1D_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeWriteBack()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();
        SeedVectorWordMemory(0xF00, 10U, 20U, 30U, 40U);
        SeedVectorWordMemory(0x100, 1U, 2U, 3U, 4U);

        var core = new Processor.CPU_Core(0);
        core.SetPipelineMode(true);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VADD,
            DataTypeEnum.UINT32,
            destSrc1Ptr: 0xF00,
            src2Ptr: 0x100,
            streamLength: 80,
            stride: sizeof(uint));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StreamEngine.Execute(ref core, in inst));
        Assert.Contains(nameof(BurstIO.BurstRead), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine2D_WhenBurstReadFailsMidTransfer_ThenFailsClosedBeforeWriteBack()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();
        SeedVectorWordMemory(0xFFC, 10U);
        SeedVectorWordMemory(0x100, 1U, 2U);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = new()
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0xFFC,
            Src2Pointer = 0x100,
            StreamLength = 2,
            Stride = 4,
            Is2D = true,
            Immediate = 1,
            RowStride = 4
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StreamEngine.Execute(ref core, in inst));
        Assert.Contains("StreamEngine", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngineIndexed_WhenBurstGatherFailsMidTransfer_ThenFailsClosedBeforeWriteBack()
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();

        const ulong descriptorAddress = 0x200;
        const ulong srcBase = 0x0;
        const ulong indexBase = 0x300;

        SeedVectorWordMemory(srcBase, 10U);
        byte[] indices = new byte[8];
        BitConverter.GetBytes(0U).CopyTo(indices, 0);
        BitConverter.GetBytes(1024U).CopyTo(indices, 4);
        Processor.MainMemory.WriteToPosition(indices, indexBase);
        WriteIndexedDescriptor(descriptorAddress, srcBase, indexBase, 4, 0, 0);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = new()
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = 0x400,
            Src2Pointer = descriptorAddress,
            StreamLength = 2,
            Stride = 4,
            Indexed = true
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => StreamEngine.Execute(ref core, in inst));
        Assert.Contains("StreamEngine", ex.Message, StringComparison.Ordinal);
    }

    private static void AssertMainlineVectorComputeAbortBeforePublication(
        InstructionsEnum opcode,
        Type expectedMicroOpType,
        ulong src2Pointer = 0x100,
        ushort immediate = 0,
        bool seedSrc2 = false)
    {
        InitializeCpuMainMemoryIdentityMap(0x1000);
        InitializeMemorySubsystem();
        SeedVectorWordMemory(0xFFC, 10U);
        if (seedSrc2)
        {
            SeedVectorWordMemory(src2Pointer, 1U, 2U);
        }

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = CreateVectorInstruction(
            opcode,
            destSrc1Pointer: 0xFFC,
            src2Pointer: src2Pointer,
            immediate: immediate,
            streamLength: 2,
            stride: 4,
            dataType: DataTypeEnum.UINT32);

        MicroOp microOp = MaterializeSingleSlotMicroOp(inst);
        Assert.IsType(expectedMicroOpType, microOp);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestRunExecuteStageWithDecodedInstruction(
                inst,
                microOp,
                isVectorOp: true,
                isMemoryOp: false,
                pc: 0x2000));

        Assert.Contains("reference raw execute fallback", ex.Message, StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.Contains(nameof(BurstIO.BurstRead), ex.InnerException!.Message, StringComparison.Ordinal);
        Assert.Equal(10U, BitConverter.ToUInt32(Processor.MainMemory.ReadFromPosition(new byte[4], 0xFFC, 4), 0));
    }

    private static void InitializeCpuMainMemoryIdentityMap(ulong size)
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: size,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedVectorWordMemory(ulong address, params uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(uint));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static VLIW_Instruction CreateVectorInstruction(
        InstructionsEnum opcode,
        ulong destSrc1Pointer = 0,
        ulong src2Pointer = 0,
        ushort immediate = 0,
        uint streamLength = 1,
        ushort stride = 0,
        byte predicateMask = 0xFF,
        DataTypeEnum dataType = DataTypeEnum.INT32)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = predicateMask,
            DestSrc1Pointer = destSrc1Pointer,
            Src2Pointer = src2Pointer,
            Immediate = immediate,
            StreamLength = streamLength,
            Stride = stride
        };
    }

    private static MicroOp MaterializeSingleSlotMicroOp(VLIW_Instruction instruction)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2400, bundleSerial: 73);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);
    }

    private static void WriteIndexedDescriptor(
        ulong descriptorAddress,
        ulong src2Base,
        ulong indexBase,
        ushort indexStride,
        byte indexType,
        byte indexIsByteOffset)
    {
        byte[] descriptor = new byte[32];
        BitConverter.GetBytes(src2Base).CopyTo(descriptor, 0);
        BitConverter.GetBytes(indexBase).CopyTo(descriptor, 8);
        BitConverter.GetBytes(indexStride).CopyTo(descriptor, 16);
        descriptor[18] = indexType;
        descriptor[19] = indexIsByteOffset;
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
    }
}


