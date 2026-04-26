using HybridCPU_ISE.Arch;
using System;
using Xunit;
using HybridCPU_ISE.Tests.TestHelpers;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09VectorDeferredProofTests
{
    [Fact]
    public void MaterializedVectorBinaryMicroOp_WhenInstructionCarriesMaskAgnostic_ThenMaskedOffLanesFollowInstructionPolicy()
    {
        const ulong destAddress = 0x1000;
        const ulong srcAddress = 0x1100;

        uint[] preserved = ExecuteMaterializedVectorBinary(maskAgnostic: false, destAddress, srcAddress);
        uint[] agnostic = ExecuteMaterializedVectorBinary(maskAgnostic: true, destAddress, srcAddress);

        Assert.Equal(new uint[] { 11U, 20U, 33U, 40U }, preserved);
        Assert.Equal(new uint[] { 11U, 22U, 33U, 44U }, agnostic);
    }

    [Fact]
    public void MaterializedVectorUnaryMicroOp_WhenInstructionCarriesMaskAgnostic_ThenMaskedOffLanesFollowInstructionPolicy()
    {
        const ulong destAddress = 0x1200;

        uint[] preserved = ExecuteMaterializedVectorUnary(maskAgnostic: false, destAddress);
        uint[] agnostic = ExecuteMaterializedVectorUnary(maskAgnostic: true, destAddress);

        Assert.Equal(new uint[] { ~0U, 1U, ~2U, 3U }, preserved);
        Assert.Equal(new uint[] { ~0U, ~1U, ~2U, ~3U }, agnostic);
    }

    [Fact]
    public void MaterializedAndRawVexpand_WhenMaskAgnosticMaskedLaneContourExecutes_ThenStayInParity()
    {
        const ulong vectorAddress = 0x1400;

        uint[] materialized = ExecuteMaterializedSingleSurfaceWordOp(
            InstructionsEnum.VEXPAND,
            vectorAddress,
            src2Address: 0,
            offset: 0,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });
        uint[] raw = ExecuteRawSingleSurfaceWordOp(
            InstructionsEnum.VEXPAND,
            vectorAddress,
            src2Address: 0,
            offset: 0,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });

        Assert.Equal(new uint[] { 10U, 0U, 20U, 30U }, materialized);
        Assert.Equal(materialized, raw);
    }

    [Theory]
    [InlineData(InstructionsEnum.VPERMUTE)]
    [InlineData(InstructionsEnum.VRGATHER)]
    public void MaterializedAndRawPermutationFamily_WhenMaskAgnosticMaskedLaneContourExecutes_ThenStayInParity(
        InstructionsEnum opcode)
    {
        const ulong vectorAddress = 0x1600;
        const ulong indexAddress = 0x1700;

        uint[] materialized = ExecuteMaterializedSingleSurfaceWordOp(
            opcode,
            vectorAddress,
            src2Address: indexAddress,
            offset: 0,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U },
            src2Vector: new uint[] { 2U, 0U, 3U, 2U });
        uint[] raw = ExecuteRawSingleSurfaceWordOp(
            opcode,
            vectorAddress,
            src2Address: indexAddress,
            offset: 0,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U },
            src2Vector: new uint[] { 2U, 0U, 3U, 2U });

        Assert.Equal(new uint[] { 30U, 10U, 40U, 30U }, materialized);
        Assert.Equal(materialized, raw);
    }

    [Fact]
    public void MaterializedAndRawSlideUp_WhenMaskAgnosticMaskedLaneContourExecutes_ThenStayInParity()
    {
        const ulong vectorAddress = 0x1800;

        uint[] materialized = ExecuteMaterializedSingleSurfaceWordOp(
            InstructionsEnum.VSLIDEUP,
            vectorAddress,
            src2Address: 0,
            offset: 1,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });
        uint[] raw = ExecuteRawSingleSurfaceWordOp(
            InstructionsEnum.VSLIDEUP,
            vectorAddress,
            src2Address: 0,
            offset: 1,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });

        Assert.Equal(new uint[] { 10U, 10U, 20U, 30U }, materialized);
        Assert.Equal(materialized, raw);
    }

    [Fact]
    public void MaterializedAndRawSlideDown_WhenMaskAgnosticMaskedLaneContourExecutes_ThenStayInParity()
    {
        const ulong vectorAddress = 0x1A00;

        uint[] materialized = ExecuteMaterializedSingleSurfaceWordOp(
            InstructionsEnum.VSLIDEDOWN,
            vectorAddress,
            src2Address: 0,
            offset: 1,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });
        uint[] raw = ExecuteRawSingleSurfaceWordOp(
            InstructionsEnum.VSLIDEDOWN,
            vectorAddress,
            src2Address: 0,
            offset: 1,
            predicateRegisterId: 5,
            predicateMaskValue: 0b1101UL,
            maskAgnostic: true,
            initialVector: new uint[] { 10U, 20U, 30U, 40U });

        Assert.Equal(new uint[] { 20U, 30U, 40U, 0U }, materialized);
        Assert.Equal(materialized, raw);
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD, 7f, 12f, 19f, 28f)]
    [InlineData(InstructionsEnum.VFMSUB, -3f, 0f, 5f, 12f)]
    public void MaterializedVectorFmaMicroOp_WhenDescriptorBackedContourExecutes_ThenPublishesAndProducesAccumulatorTruth(
        InstructionsEnum opcode,
        float lane0,
        float lane1,
        float lane2,
        float lane3)
    {
        const ulong destAddress = 0x1C00;
        const ulong descriptorAddress = 0x1D00;
        const ulong srcAAddress = 0x1E00;
        const ulong srcBAddress = 0x1F00;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedTriOpDescriptor(descriptorAddress, srcAAddress, srcBAddress, strideA: 4, strideB: 4);
        SeedVectorFloatMemory(destAddress, 1f, 2f, 3f, 4f);
        SeedVectorFloatMemory(srcAAddress, 2f, 3f, 4f, 5f);
        SeedVectorFloatMemory(srcBAddress, 5f, 6f, 7f, 8f);

        VLIW_Instruction inst = CreateDescriptorBackedFmaInstruction(opcode, destAddress, descriptorAddress);
        VectorFmaMicroOp microOp = Assert.IsType<VectorFmaMicroOp>(MaterializeSingleSlotMicroOp(inst));

        Assert.Equal(3, microOp.AdmissionMetadata.ReadMemoryRanges.Count);
        Assert.Equal((destAddress, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[0]);
        Assert.Equal((srcAAddress, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[1]);
        Assert.Equal((srcBAddress, 16UL), microOp.AdmissionMetadata.ReadMemoryRanges[2]);
        Assert.Single(microOp.AdmissionMetadata.WriteMemoryRanges);
        Assert.Equal((destAddress, 16UL), microOp.AdmissionMetadata.WriteMemoryRanges[0]);

        var core = new Processor.CPU_Core(0);
        Assert.True(microOp.Execute(ref core));

        Assert.Equal(new[] { lane0, lane1, lane2, lane3 }, ReadVectorFloatMemory(destAddress, 4));
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFMSUB)]
    public void MaterializedAndRawVectorFma_WhenDescriptorBackedContourExecutes_ThenStayInParity(
        InstructionsEnum opcode)
    {
        const ulong destAddress = 0x2000;
        const ulong descriptorAddress = 0x2100;
        const ulong srcAAddress = 0x2200;
        const ulong srcBAddress = 0x2300;

        float[] materialized = ExecuteMaterializedDescriptorBackedFma(
            opcode,
            destAddress,
            descriptorAddress,
            srcAAddress,
            srcBAddress);
        float[] raw = ExecuteRawDescriptorBackedFma(
            opcode,
            destAddress,
            descriptorAddress,
            srcAAddress,
            srcBAddress);

        Assert.Equal(materialized, raw);
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFMSUB)]
    [InlineData(InstructionsEnum.VFNMADD)]
    [InlineData(InstructionsEnum.VFNMSUB)]
    public void MaterializedVectorFmaMicroOp_WhenDescriptorlessContourIsDecoded_ThenFailsClosedBeforeRuntime(
        InstructionsEnum opcode)
    {
        const ulong destAddress = 0x2380;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorFloatMemory(destAddress, 1f, 2f, 3f, 4f);

        VLIW_Instruction inst = new()
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = destAddress,
            Src2Pointer = 0,
            StreamLength = 4,
            Stride = 4
        };

        TrapMicroOp trap = Assert.IsType<TrapMicroOp>(MaterializeSingleSlotMicroOp(inst));

        Assert.Equal((uint)opcode, trap.OpCode);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, ReadVectorFloatMemory(destAddress, 4));
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFMSUB)]
    [InlineData(InstructionsEnum.VFNMADD)]
    [InlineData(InstructionsEnum.VFNMSUB)]
    public void ExecuteStreamInstruction_WhenDescriptorlessFmaReachesDirectSurface_ThenFailsClosedWithoutMemoryMutation(
        InstructionsEnum opcode)
    {
        const ulong destAddress = 0x2400;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorFloatMemory(destAddress, 1f, 2f, 3f, 4f);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = new()
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = destAddress,
            Src2Pointer = 0,
            StreamLength = 4,
            Stride = 4
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => core.ExecuteDirectStreamCompat(inst));

        Assert.Contains("direct stream compat executor", ex.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative retire/apply contour for memory-visible publication", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, ReadVectorFloatMemory(destAddress, 4));
    }

    private static uint[] ExecuteMaterializedVectorBinary(bool maskAgnostic, ulong destAddress, ulong srcAddress)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(destAddress, 10U, 20U, 30U, 40U);
        SeedVectorWordMemory(srcAddress, 1U, 2U, 3U, 4U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0b0101UL);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VADD,
            DataTypeEnum.UINT32,
            destSrc1Ptr: destAddress,
            src2Ptr: srcAddress,
            streamLength: 4,
            stride: sizeof(uint));
        inst.PredicateMask = 5;
        inst.MaskAgnostic = maskAgnostic;

        VectorBinaryOpMicroOp microOp = Assert.IsType<VectorBinaryOpMicroOp>(MaterializeSingleSlotMicroOp(inst));
        Assert.True(microOp.Execute(ref core));

        return ReadVectorWordMemory(destAddress, 4);
    }

    private static uint[] ExecuteMaterializedVectorUnary(bool maskAgnostic, ulong destAddress)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(destAddress, 0U, 1U, 2U, 3U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0b0101UL);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VNOT,
            DataTypeEnum.UINT32,
            destSrc1Ptr: destAddress,
            src2Ptr: 0,
            streamLength: 4,
            stride: sizeof(uint));
        inst.PredicateMask = 5;
        inst.MaskAgnostic = maskAgnostic;

        VectorUnaryOpMicroOp microOp = Assert.IsType<VectorUnaryOpMicroOp>(MaterializeSingleSlotMicroOp(inst));
        Assert.True(microOp.Execute(ref core));

        return ReadVectorWordMemory(destAddress, 4);
    }

    private static uint[] ExecuteMaterializedSingleSurfaceWordOp(
        InstructionsEnum opcode,
        ulong vectorAddress,
        ulong src2Address,
        ushort offset,
        byte predicateRegisterId,
        ulong predicateMaskValue,
        bool maskAgnostic,
        uint[] initialVector,
        uint[]? src2Vector = null)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, initialVector);
        if (src2Vector != null)
        {
            SeedVectorWordMemory(src2Address, src2Vector);
        }

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(predicateRegisterId, predicateMaskValue);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: src2Address,
            streamLength: (ulong)initialVector.Length,
            stride: sizeof(uint));
        inst.PredicateMask = predicateRegisterId;
        inst.Immediate = offset;
        inst.MaskAgnostic = maskAgnostic;

        VectorMicroOp microOp = Assert.IsAssignableFrom<VectorMicroOp>(MaterializeSingleSlotMicroOp(inst));
        Assert.True(microOp.Execute(ref core));

        return ReadVectorWordMemory(vectorAddress, initialVector.Length);
    }

    private static uint[] ExecuteRawSingleSurfaceWordOp(
        InstructionsEnum opcode,
        ulong vectorAddress,
        ulong src2Address,
        ushort offset,
        byte predicateRegisterId,
        ulong predicateMaskValue,
        bool maskAgnostic,
        uint[] initialVector,
        uint[]? src2Vector = null)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, initialVector);
        if (src2Vector != null)
        {
            SeedVectorWordMemory(src2Address, src2Vector);
        }

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(predicateRegisterId, predicateMaskValue);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: src2Address,
            streamLength: (ulong)initialVector.Length,
            stride: sizeof(uint));
        inst.PredicateMask = predicateRegisterId;
        inst.Immediate = offset;
        inst.MaskAgnostic = maskAgnostic;

        StreamEngine.Execute(ref core, in inst);
        return ReadVectorWordMemory(vectorAddress, initialVector.Length);
    }

    private static float[] ExecuteMaterializedDescriptorBackedFma(
        InstructionsEnum opcode,
        ulong destAddress,
        ulong descriptorAddress,
        ulong srcAAddress,
        ulong srcBAddress)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedTriOpDescriptor(descriptorAddress, srcAAddress, srcBAddress, strideA: 4, strideB: 4);
        SeedVectorFloatMemory(destAddress, 1f, 2f, 3f, 4f);
        SeedVectorFloatMemory(srcAAddress, 2f, 3f, 4f, 5f);
        SeedVectorFloatMemory(srcBAddress, 5f, 6f, 7f, 8f);

        var core = new Processor.CPU_Core(0);
        VectorFmaMicroOp microOp = Assert.IsType<VectorFmaMicroOp>(
            MaterializeSingleSlotMicroOp(CreateDescriptorBackedFmaInstruction(opcode, destAddress, descriptorAddress)));
        Assert.True(microOp.Execute(ref core));
        return ReadVectorFloatMemory(destAddress, 4);
    }

    private static float[] ExecuteRawDescriptorBackedFma(
        InstructionsEnum opcode,
        ulong destAddress,
        ulong descriptorAddress,
        ulong srcAAddress,
        ulong srcBAddress)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedTriOpDescriptor(descriptorAddress, srcAAddress, srcBAddress, strideA: 4, strideB: 4);
        SeedVectorFloatMemory(destAddress, 1f, 2f, 3f, 4f);
        SeedVectorFloatMemory(srcAAddress, 2f, 3f, 4f, 5f);
        SeedVectorFloatMemory(srcBAddress, 5f, 6f, 7f, 8f);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = CreateDescriptorBackedFmaInstruction(opcode, destAddress, descriptorAddress);
        StreamEngine.Execute(ref core, in inst);
        return ReadVectorFloatMemory(destAddress, 4);
    }

    private static VLIW_Instruction CreateDescriptorBackedFmaInstruction(
        InstructionsEnum opcode,
        ulong destAddress,
        ulong descriptorAddress)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = destAddress,
            Src2Pointer = descriptorAddress,
            StreamLength = 4,
            Stride = 4
        };
    }

    private static MicroOp MaterializeSingleSlotMicroOp(VLIW_Instruction instruction)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x6400, bundleSerial: 117);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: 0,
            physicalAddress: 0,
            size: 0x100000000UL,
            permissions: IOMMUAccessPermissions.ReadWrite);
    }

    private static void InitializeMemorySubsystem()
    {
        Processor proc = default;
        Processor.Memory = new MemorySubsystem(ref proc);
    }

    private static void SeedTriOpDescriptor(
        ulong descriptorAddress,
        ulong srcAPointer,
        ulong srcBPointer,
        ushort strideA,
        ushort strideB)
    {
        byte[] descriptor = new byte[20];
        BitConverter.GetBytes(srcAPointer).CopyTo(descriptor, 0);
        BitConverter.GetBytes(srcBPointer).CopyTo(descriptor, 8);
        BitConverter.GetBytes(strideA).CopyTo(descriptor, 16);
        BitConverter.GetBytes(strideB).CopyTo(descriptor, 18);
        Processor.MainMemory.WriteToPosition(descriptor, descriptorAddress);
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

    private static void SeedVectorFloatMemory(ulong address, params float[] values)
    {
        byte[] data = new byte[values.Length * sizeof(float)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(float));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static uint[] ReadVectorWordMemory(ulong address, int elementCount)
    {
        byte[] data = Processor.MainMemory.ReadFromPosition(new byte[elementCount * sizeof(uint)], address, (ulong)(elementCount * sizeof(uint)));
        uint[] values = new uint[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            values[i] = BitConverter.ToUInt32(data, i * sizeof(uint));
        }

        return values;
    }

    private static float[] ReadVectorFloatMemory(ulong address, int elementCount)
    {
        byte[] data = Processor.MainMemory.ReadFromPosition(new byte[elementCount * sizeof(float)], address, (ulong)(elementCount * sizeof(float)));
        float[] values = new float[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            values[i] = BitConverter.ToSingle(data, i * sizeof(float));
        }

        return values;
    }
}


