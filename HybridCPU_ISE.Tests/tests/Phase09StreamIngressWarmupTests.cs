using HybridCPU_ISE.Arch;
using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09StreamIngressWarmupTests
{
    [Fact]
    public void RawStreamEngine_WhenDoubleBuffered1DBinaryExecutesThenSrfBypassTracksWarmConsume()
    {
        const ulong lhsAddress = 0x840;
        const ulong rhsAddress = 0xA40;
        const int elementCount = 40;

        ulong[] lhs = new ulong[elementCount];
        ulong[] rhs = new ulong[elementCount];
        ulong[] expected = new ulong[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            lhs[i] = (ulong)(i + 1);
            rhs[i] = (ulong)(100 + i);
            expected[i] = lhs[i] + rhs[i];
        }

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorUlongMemory(lhsAddress, lhs);
        SeedVectorUlongMemory(rhsAddress, rhs);

        var core = new Processor.CPU_Core(0);
        core.SetPipelineMode(true);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VADD,
            DataTypeEnum.UINT64,
            destSrc1Ptr: lhsAddress,
            src2Ptr: rhsAddress,
            streamLength: elementCount,
            stride: sizeof(ulong));

        StreamEngine.Execute(ref core, in inst);

        Assert.Equal(expected, ReadVectorUlongMemory(lhsAddress, elementCount));
        Assert.Equal(2UL, Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits);

        StreamIngressWarmTelemetry telemetry = Processor.Memory.StreamRegisters.GetIngressWarmTelemetry();
        Assert.Equal(2UL, telemetry.ForegroundWarmAttempts);
        Assert.Equal(2UL, telemetry.ForegroundWarmSuccesses);
        Assert.Equal(0UL, telemetry.ForegroundWarmReuseHits);
        Assert.Equal(2UL, telemetry.ForegroundBypassHits);
        Assert.Equal(0UL, telemetry.AssistWarmAttempts);
        Assert.Equal(0UL, telemetry.TranslationRejects);
    }

    [Fact]
    public void BurstRead2D_WhenContiguousWindowAlreadyWarmedThenConsumesPrefetchedSrfChunk()
    {
        const ulong sourceAddress = 0x1200;
        const int elementCount = 8;
        const ushort stride = sizeof(uint);
        const ushort rowLength = 4;
        const ushort rowStride = rowLength * stride;

        uint[] source = new uint[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            source[i] = (uint)(200 + i);
        }

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceAddress, source);

        Assert.True(StreamEngine.ScheduleLane6AssistPrefetch(
            sourceAddress,
            sizeof(uint),
            (uint)elementCount,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy.Default));

        byte[] destination = new byte[elementCount * sizeof(uint)];
        ulong completed = BurstIO.BurstRead2D(
            sourceAddress,
            destination,
            (ulong)elementCount,
            sizeof(uint),
            rowLength,
            rowStride,
            stride);

        Assert.Equal((ulong)elementCount, completed);
        Assert.Equal(source, ReadUInts(destination));
        Assert.Equal(1UL, Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits);

        StreamIngressWarmTelemetry telemetry = Processor.Memory.StreamRegisters.GetIngressWarmTelemetry();
        Assert.Equal(0UL, telemetry.ForegroundWarmAttempts);
        Assert.Equal(1UL, telemetry.AssistWarmAttempts);
        Assert.Equal(1UL, telemetry.AssistWarmSuccesses);
        Assert.Equal(0UL, telemetry.AssistWarmReuseHits);
        Assert.Equal(1UL, telemetry.AssistBypassHits);
        Assert.Equal(0UL, telemetry.TranslationRejects);
    }

    [Fact]
    public void RawStreamEngine_When2DVectorBinaryContourThenFailsClosedBeforeHiddenIngressExecution()
    {
        const ulong lhsAddress = 0x1A00;
        const ulong rhsAddress = 0x1E00;
        const int elementCount = 8;
        const ushort stride = sizeof(uint);
        const ushort rowLength = 4;
        const ushort rowStride = rowLength * stride;

        uint[] lhs = new uint[elementCount];
        uint[] rhs = new uint[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            lhs[i] = (uint)(i + 1);
            rhs[i] = (uint)(200 + i);
        }

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(lhsAddress, lhs);
        SeedVectorWordMemory(rhsAddress, rhs);

        var core = new Processor.CPU_Core(0);
        var inst = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = lhsAddress,
            Src2Pointer = rhsAddress,
            StreamLength = elementCount,
            Stride = stride,
            Is2D = true,
            Immediate = rowLength,
            RowStride = rowStride
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("unsupported 2D vector-binary addressing", ex.Message, StringComparison.Ordinal);
        Assert.Equal(lhs, ReadVectorWordMemory(lhsAddress, elementCount));
        Assert.Equal(0UL, Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits);
        Assert.Equal(0UL, Processor.Memory.StreamRegisters.GetIngressWarmTelemetry().ForegroundWarmAttempts);
    }

    [Fact]
    public void RawStreamEngine_WhenIndexedVectorBinaryContourThenFailsClosedBeforeHiddenIngressExecution()
    {
        const ulong destAddress = 0x2200;
        const ulong sourceAddress = 0x2800;
        const ulong indexAddress = 0x2C00;
        const ulong descriptorAddress = 0x3000;
        const int elementCount = 40;

        uint[] dest = new uint[elementCount];
        uint[] source = new uint[elementCount];
        uint[] indices = new uint[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            dest[i] = (uint)(1000 + i);
            source[i] = (uint)(2000 + (i * 3));
            indices[i] = (uint)i;
        }

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(destAddress, dest);
        SeedVectorWordMemory(sourceAddress, source);
        SeedVectorWordMemory(indexAddress, indices);
        WriteIndexedDescriptor(
            descriptorAddress,
            src2Base: sourceAddress,
            indexBase: indexAddress,
            indexStride: sizeof(uint),
            indexType: 0,
            indexIsByteOffset: 0);

        var core = new Processor.CPU_Core(0);
        var inst = new VLIW_Instruction
        {
            OpCode = (uint)InstructionsEnum.VADD,
            DataTypeValue = DataTypeEnum.UINT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = destAddress,
            Src2Pointer = descriptorAddress,
            StreamLength = elementCount,
            Stride = sizeof(uint),
            Indexed = true
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("unsupported indexed vector-binary addressing", ex.Message, StringComparison.Ordinal);
        Assert.Equal(dest, ReadVectorWordMemory(destAddress, elementCount));
        Assert.Equal(0UL, Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits);
        Assert.Equal(0UL, Processor.Memory.StreamRegisters.GetIngressWarmTelemetry().ForegroundWarmAttempts);
    }

    [Fact]
    public void BurstGather_WhenContiguousWindowAlreadyWarmedThenConsumesPrefetchedSrfChunk()
    {
        const ulong sourceAddress = 0x3800;
        uint[] source = { 10U, 20U, 30U, 40U, 50U, 60U, 70U, 80U };

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(sourceAddress, source);

        Assert.True(StreamEngine.ScheduleLane6AssistPrefetch(
            sourceAddress,
            sizeof(uint),
            (uint)source.Length,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy.Default));

        byte[] indexBuffer = CreateSequentialIndices(source.Length);
        byte[] destination = new byte[source.Length * sizeof(uint)];

        ulong completed = BurstIO.BurstGather(
            sourceAddress,
            destination,
            (ulong)source.Length,
            sizeof(uint),
            indexBuffer,
            sizeof(uint),
            indexIsByteOffset: 0);

        Assert.Equal((ulong)source.Length, completed);
        Assert.Equal(source, ReadUInts(destination));
        Assert.True(Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits > 0);
    }

    [Fact]
    public void ScheduleLane6AssistPrefetch_WhenTranslationRejectsRead_ThenFailsClosedWithoutPublishingWarmSuccess()
    {
        const ulong writeOnlyAddress = 0x4400;

        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
        IOMMU.Initialize();
        IOMMU.RegisterDevice(0);
        Assert.True(IOMMU.Map(
            deviceID: 0,
            ioVirtualAddress: writeOnlyAddress,
            physicalAddress: writeOnlyAddress,
            size: 0x1000,
            permissions: IOMMUAccessPermissions.Write));
        InitializeMemorySubsystem();

        Assert.False(StreamEngine.ScheduleLane6AssistPrefetch(
            writeOnlyAddress,
            sizeof(uint),
            elemCount: 4,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy.Default));

        byte[] readBuffer = new byte[sizeof(uint) * 4];
        Assert.False(IOMMU.ReadBurst(0, writeOnlyAddress, readBuffer));
        Assert.Equal(0UL, Processor.Memory!.StreamRegisters.GetStatistics().l1BypassHits);

        StreamIngressWarmTelemetry telemetry = Processor.Memory.StreamRegisters.GetIngressWarmTelemetry();
        Assert.Equal(1UL, telemetry.AssistWarmAttempts);
        Assert.Equal(0UL, telemetry.AssistWarmSuccesses);
        Assert.Equal(0UL, telemetry.AssistBypassHits);
        Assert.Equal(1UL, telemetry.TranslationRejects);
        Assert.Equal(0UL, telemetry.BackendRejects);
    }

    [Fact]
    public void ScheduleLane6AssistPrefetch_WhenChunkExceedsResidentBudget_ThenFailsClosedWithoutPublishingWarmAttempt()
    {
        const ulong sourceAddress = 0x4C00;
        const uint requestedElements = 40;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        Assert.Equal(32u, StreamEngine.ResolveSrfResidentChunkBudget(sizeof(ulong), requestedElements));
        Assert.False(StreamEngine.ScheduleLane6AssistPrefetch(
            sourceAddress,
            sizeof(ulong),
            requestedElements,
            YAKSys_Hybrid_CPU.Core.AssistStreamRegisterPartitionPolicy.Default));

        StreamRegisterFile srf = Processor.Memory!.StreamRegisters;
        StreamIngressWarmTelemetry telemetry = srf.GetIngressWarmTelemetry();
        Assert.Equal(0UL, telemetry.AssistWarmAttempts);
        Assert.Equal(0UL, telemetry.AssistWarmSuccesses);
        Assert.Equal(0UL, telemetry.AssistBypassHits);
        Assert.Equal(0UL, telemetry.TranslationRejects);
        Assert.Equal(0, srf.CountAssistOwnedRegisters());
    }

    private static void InitializeCpuMainMemoryIdentityMap()
    {
        Processor.MainMemory = new Processor.MultiBankMemoryArea(4, 0x4000000UL);
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

    private static void SeedVectorWordMemory(ulong address, uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(uint));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static void SeedVectorUlongMemory(ulong address, ulong[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ulong)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ulong));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static uint[] ReadVectorWordMemory(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(uint)],
            address,
            (ulong)(count * sizeof(uint)));
        return ReadUInts(bytes);
    }

    private static ulong[] ReadVectorUlongMemory(ulong address, int count)
    {
        byte[] bytes = Processor.MainMemory.ReadFromPosition(
            new byte[count * sizeof(ulong)],
            address,
            (ulong)(count * sizeof(ulong)));
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = BitConverter.ToUInt64(bytes, i * sizeof(ulong));
        }

        return values;
    }

    private static uint[] ReadUInts(byte[] bytes)
    {
        uint[] values = new uint[bytes.Length / sizeof(uint)];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = BitConverter.ToUInt32(bytes, i * sizeof(uint));
        }

        return values;
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

    private static byte[] CreateSequentialIndices(int count)
    {
        byte[] bytes = new byte[count * sizeof(uint)];
        for (int i = 0; i < count; i++)
        {
            BitConverter.GetBytes((uint)i).CopyTo(bytes, i * sizeof(uint));
        }

        return bytes;
    }
}

