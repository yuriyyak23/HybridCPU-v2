using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.Numerics;
using System.Reflection;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using YAKSys_Hybrid_CPU.Execution;
using YAKSys_Hybrid_CPU.Memory;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.Phase09;

public sealed class Phase09StreamEngineDeferredParityTests
{
    public static IEnumerable<object[]> RawIndexedOr2DContours()
    {
        foreach (object[] testCase in DeferredVectorBatchTestHelper.ExtendedNonRepresentableContours())
        {
            if ((DeferredVectorAddressingFamily)testCase[0] == DeferredVectorAddressingFamily.Transfer)
            {
                continue;
            }

            yield return testCase;
        }
    }

    public static IEnumerable<object[]> DirectVpopcAddressingContours()
    {
        yield return new object[] { false, "indexed" };
        yield return new object[] { true, "2D" };
    }

    public static IEnumerable<object[]> OptionalScalarRawOpcodes()
    {
        yield return new object[] { 45u, "XSQRT" };
        yield return new object[] { 52u, "NOT" };
    }

    public static IEnumerable<object[]> RawUnaryParityOpcodes()
    {
        yield return new object[] { InstructionsEnum.VREVERSE };
        yield return new object[] { InstructionsEnum.VPOPCNT };
        yield return new object[] { InstructionsEnum.VCLZ };
        yield return new object[] { InstructionsEnum.VCTZ };
        yield return new object[] { InstructionsEnum.VBREV8 };
    }

    public static IEnumerable<object[]> RawUnaryIndexedOr2DContours()
    {
        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VSQRT,
                     InstructionsEnum.VNOT,
                     InstructionsEnum.VREVERSE,
                     InstructionsEnum.VPOPCNT,
                     InstructionsEnum.VCLZ,
                     InstructionsEnum.VCTZ,
                     InstructionsEnum.VBREV8
                 })
        {
            yield return new object[] { opcode, false, "indexed" };
            yield return new object[] { opcode, true, "2D" };
        }
    }

    public static IEnumerable<object[]> PredicateStateDirectIndexedOr2DContours()
    {
        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VCMPEQ,
                     InstructionsEnum.VCMPNE,
                     InstructionsEnum.VCMPLT,
                     InstructionsEnum.VCMPLE,
                     InstructionsEnum.VCMPGT,
                     InstructionsEnum.VCMPGE
                 })
        {
            yield return new object[] { DeferredVectorAddressingFamily.Comparison, opcode, false, "indexed" };
            yield return new object[] { DeferredVectorAddressingFamily.Comparison, opcode, true, "2D" };
        }

        foreach (InstructionsEnum opcode in new[]
                 {
                     InstructionsEnum.VMAND,
                     InstructionsEnum.VMOR,
                     InstructionsEnum.VMXOR,
                     InstructionsEnum.VMNOT
                 })
        {
            yield return new object[] { DeferredVectorAddressingFamily.Mask, opcode, false, "indexed" };
            yield return new object[] { DeferredVectorAddressingFamily.Mask, opcode, true, "2D" };
        }
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

    private static void SeedVectorByteMemory(ulong address, params byte[] values)
    {
        Processor.MainMemory.WriteToPosition(values, address);
    }

    private static void SeedVectorUlongMemory(ulong address, params ulong[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ulong)];
        for (int i = 0; i < values.Length; i++)
        {
            BitConverter.GetBytes(values[i]).CopyTo(data, i * sizeof(ulong));
        }

        Processor.MainMemory.WriteToPosition(data, address);
    }

    private static ulong[] ReadVectorUlongMemory(ulong address, int elementCount)
    {
        byte[] data = Processor.MainMemory.ReadFromPosition(
            new byte[elementCount * sizeof(ulong)],
            address,
            (ulong)(elementCount * sizeof(ulong)));

        ulong[] values = new ulong[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            values[i] = BitConverter.ToUInt64(data, i * sizeof(ulong));
        }

        return values;
    }

    [Theory]
    [InlineData(InstructionsEnum.VLOAD)]
    [InlineData(InstructionsEnum.VSTORE)]
    public void RawStreamEngine_WhenTransferOpcodeReachesLegacyRawContour_ThenFailsClosed(
        InstructionsEnum opcode)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(
                DeferredVectorAddressingFamily.Transfer,
                opcode,
                is2D: false);
        inst.Indexed = false;
        inst.Is2D = false;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("legacy raw VLOAD/VSTORE contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("generic compute-style memory traffic", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RawIndexedOr2DContours))]
    public void RawStreamEngine_WhenExtendedVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenRejectsCompatSuccessShell(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            DeferredVectorBatchTestHelper.GetRawAddressingLabel(family),
            ex.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VFMADD)]
    [InlineData(InstructionsEnum.VFMSUB)]
    [InlineData(InstructionsEnum.VFNMADD)]
    [InlineData(InstructionsEnum.VFNMSUB)]
    public void RawStreamEngine_WhenDescriptorlessFmaContourReaches1DPath_ThenFailsClosed(
        InstructionsEnum opcode)
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        var inst = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.FLOAT32,
            PredicateMask = 0x07,
            DestSrc1Pointer = 0x280,
            Src2Pointer = 0,
            Immediate = 0x2C0,
            StreamLength = 4,
            Stride = 4
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("descriptor-less raw tri-operand FMA contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("legacy Immediate-address fallback", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DirectVpopcAddressingContours))]
    public void ExecuteStreamInstruction_WhenIndexedOr2dVpopcReachesDirectHelperContour_ThenRejectsCompatRetireSurface(
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(3, 0b1011_0101UL);

        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(
                DeferredVectorAddressingFamily.MaskPopCount,
                InstructionsEnum.VPOPC,
                is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteDirectStreamCompat(inst));

        Assert.Contains("direct stream retire-window publication", ex.Message, StringComparison.Ordinal);
        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains("vector-mask-popcount addressing", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DirectVpopcAddressingContours))]
    public void ResolveDirectCompatRetireTransaction_WhenIndexedOr2dVpopcReachesDirectHelperContour_ThenRejectsCompatRetireSurface(
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(3, 0b1011_0101UL);

        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(
                DeferredVectorAddressingFamily.MaskPopCount,
                InstructionsEnum.VPOPC,
                is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestApplyStreamRetireWindowPublications(inst));

        Assert.Contains("direct stream retire-window publication", ex.Message, StringComparison.Ordinal);
        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains("vector-mask-popcount addressing", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PredicateStateDirectIndexedOr2DContours))]
    public void ExecuteStreamInstruction_WhenIndexedOr2dPredicateStateContourReachesDirectHelper_ThenRejectsCompatRetireSurface(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        if (family == DeferredVectorAddressingFamily.Mask)
        {
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);
        }
        else
        {
            core.SetPredicateRegister(5, 0xA5UL);
        }

        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteDirectStreamCompat(inst));

        Assert.Contains("direct stream retire-window publication", ex.Message, StringComparison.Ordinal);
        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            family == DeferredVectorAddressingFamily.Comparison
                ? "vector-comparison addressing"
                : "vector-mask addressing",
            ex.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PredicateStateDirectIndexedOr2DContours))]
    public void ResolveDirectCompatRetireTransaction_WhenIndexedOr2dPredicateStateContourReachesDirectHelper_ThenRejectsCompatRetireSurface(
        DeferredVectorAddressingFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        if (family == DeferredVectorAddressingFamily.Mask)
        {
            core.SetPredicateRegister(1, 0b1010UL);
            core.SetPredicateRegister(2, 0b1100UL);
            core.SetPredicateRegister(3, 0x5AUL);
        }
        else
        {
            core.SetPredicateRegister(5, 0xA5UL);
        }

        VLIW_Instruction inst =
            DeferredVectorBatchTestHelper.CreateAddressingInstruction(family, opcode, is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestApplyStreamRetireWindowPublications(inst));

        Assert.Contains("direct stream retire-window publication", ex.Message, StringComparison.Ordinal);
        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            family == DeferredVectorAddressingFamily.Comparison
                ? "vector-comparison addressing"
                : "vector-mask addressing",
            ex.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(OptionalScalarRawOpcodes))]
    public void ExecuteStreamInstruction_WhenRawOptionalScalarOpcodeReachesScalarRegisterContour_ThenRejectsWithoutRegisterMutation(
        uint rawOpcode,
        string contourLabel)
    {
        const int vtId = 0;
        const byte destinationRegister = 1;
        const ulong originalDestinationValue = 0x7788UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(vtId, destinationRegister, originalDestinationValue);

        VLIW_Instruction inst =
            CreateRawScalarRegisterInstruction(rawOpcode, destinationRegister, rs1: 2, rs2: 3);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteDirectStreamCompat(inst));

        Assert.Contains("scalar direct stream helper contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative scalar retire/apply contract", ex.Message, StringComparison.Ordinal);
        Assert.Equal(originalDestinationValue, core.ReadArch(vtId, destinationRegister));
        Assert.False(string.IsNullOrWhiteSpace(contourLabel));
    }

    [Theory]
    [MemberData(nameof(OptionalScalarRawOpcodes))]
    public void ResolveDirectCompatRetireTransaction_WhenRawOptionalScalarOpcodeReachesScalarRegisterContour_ThenRejectsFailClosed(
        uint rawOpcode,
        string contourLabel)
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst =
            CreateRawScalarRegisterInstruction(rawOpcode, rd: 1, rs1: 2, rs2: 3);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestApplyStreamRetireWindowPublications(inst));

        Assert.Contains("scalar direct stream helper contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative scalar retire/apply contract", ex.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(contourLabel));
    }

    [Theory]
    [InlineData(InstructionsEnum.VADD)]
    [InlineData(InstructionsEnum.VSQRT)]
    public void ResolveDirectCompatRetireTransaction_WhenScalarizedVectorOpcodeReachesScalarRegisterContour_ThenRejectsSyntheticGprRetireTruth(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = InstructionEncoder.EncodeScalar(
            (uint)opcode,
            DataTypeEnum.INT32,
            9,
            1,
            2);
        inst.StreamLength = 1;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestApplyStreamRetireWindowPublications(inst));

        Assert.Contains("scalar direct stream helper contour", ex.Message, StringComparison.Ordinal);
        Assert.Contains("authoritative scalar retire/apply contract", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteStreamInstruction_WhenScalarRegisterContourUsesNonCanonicalSourceRegisterEncoding_ThenRejectsSyntheticZeroWriteback()
    {
        const byte destinationRegister = 9;
        const ulong originalDestinationValue = 0x55UL;

        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(0, destinationRegister, originalDestinationValue);
        core.WriteCommittedArch(0, 2, 7UL);

        VLIW_Instruction inst =
            CreateMalformedScalarRegisterInstruction(
                (uint)InstructionsEnum.Addition,
                rd: destinationRegister,
                rs1: 40,
                rs2: 2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.ExecuteDirectStreamCompat(inst));

        Assert.Contains("non-canonical architectural register fields", ex.Message, StringComparison.Ordinal);
        Assert.Contains("synthetic zero-valued helper success", ex.Message, StringComparison.Ordinal);
        Assert.Equal(originalDestinationValue, core.ReadArch(0, destinationRegister));
    }

    [Fact]
    public void ResolveDirectCompatRetireTransaction_WhenScalarRegisterContourUsesNonCanonicalDestinationRegisterEncoding_ThenRejectsSilentNoOpFallback()
    {
        var core = new Processor.CPU_Core(0);
        core.WriteCommittedArch(0, 1, 5UL);
        core.WriteCommittedArch(0, 2, 7UL);

        VLIW_Instruction inst =
            CreateMalformedScalarRegisterInstruction(
                (uint)InstructionsEnum.Addition,
                rd: 40,
                rs1: 1,
                rs2: 2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => core.TestApplyStreamRetireWindowPublications(inst));

        Assert.Contains("non-canonical architectural register fields", ex.Message, StringComparison.Ordinal);
        Assert.Contains("silent no-op", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstructionsEnum.VSLL)]
    [InlineData(InstructionsEnum.VSRL)]
    [InlineData(InstructionsEnum.VSRA)]
    public void RawStreamEngine_WhenShiftFamilyExecutes1D_ThenReadsSecondSourceVectorInsteadOfImmediateShadow(
        InstructionsEnum opcode)
    {
        const ulong lhsAddress = 0x400;
        const ulong rhsAddress = 0x480;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        DataTypeEnum dataType;
        ushort conflictingImmediate;

        switch (opcode)
        {
            case InstructionsEnum.VSLL:
                SeedVectorWordMemory(lhsAddress, 1U, 2U);
                SeedVectorWordMemory(rhsAddress, 1U, 2U);
                dataType = DataTypeEnum.UINT32;
                conflictingImmediate = 7;
                break;

            case InstructionsEnum.VSRL:
                SeedVectorWordMemory(lhsAddress, 16U, 32U);
                SeedVectorWordMemory(rhsAddress, 1U, 2U);
                dataType = DataTypeEnum.UINT32;
                conflictingImmediate = 3;
                break;

            default:
                SeedVectorWordMemory(lhsAddress, 0xFFFF_FFF0U, 0xFFFF_FFC0U);
                SeedVectorWordMemory(rhsAddress, 1U, 2U);
                dataType = DataTypeEnum.INT32;
                conflictingImmediate = 4;
                break;
        }

        var inst = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = 0xFF,
            DestSrc1Pointer = lhsAddress,
            Src2Pointer = rhsAddress,
            Immediate = conflictingImmediate,
            StreamLength = 2,
            Stride = 4
        };

        StreamEngine.Execute(ref core, in inst);

        byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
        switch (opcode)
        {
            case InstructionsEnum.VSLL:
                Assert.Equal(2U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(8U, BitConverter.ToUInt32(resultBytes, 4));
                break;

            case InstructionsEnum.VSRL:
                Assert.Equal(8U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(8U, BitConverter.ToUInt32(resultBytes, 4));
                break;

            default:
                Assert.Equal(0xFFFF_FFF8U, BitConverter.ToUInt32(resultBytes, 0));
                Assert.Equal(0xFFFF_FFF0U, BitConverter.ToUInt32(resultBytes, 4));
                break;
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.VDOT)]
    [InlineData(InstructionsEnum.VDOTU)]
    [InlineData(InstructionsEnum.VDOTF)]
    [InlineData(InstructionsEnum.VDOT_FP8)]
    public void RawStreamEngine_WhenVdotFamilyExecutes1D_ThenRoutesToDedicatedDotProductSemantics(
        InstructionsEnum opcode)
    {
        const ulong lhsAddress = 0x520;
        const ulong rhsAddress = 0x5A0;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        var core = new Processor.CPU_Core(0);
        DataTypeEnum dataType;
        ushort stride;
        byte predicateMask;

        if (opcode == InstructionsEnum.VDOTF)
        {
            SeedVectorFloatMemory(lhsAddress, 1.0f, 2.0f);
            SeedVectorFloatMemory(rhsAddress, 2.0f, 4.0f);
            dataType = DataTypeEnum.FLOAT32;
            stride = 4;
            predicateMask = 0xFF;
        }
        else if (opcode == InstructionsEnum.VDOT_FP8)
        {
            SeedVectorByteMemory(lhsAddress, 0x38, 0x40, 0xAA, 0xBB, 0x11, 0x22, 0x33, 0x44);
            SeedVectorByteMemory(rhsAddress, 0x38, 0x40, 0xCC, 0xDD);
            dataType = DataTypeEnum.FLOAT8_E4M3;
            stride = 1;
            predicateMask = 0x0F;
        }
        else
        {
            SeedVectorWordMemory(lhsAddress, 3U, 7U);
            SeedVectorWordMemory(rhsAddress, 2U, 5U);
            dataType = opcode == InstructionsEnum.VDOTU
                ? DataTypeEnum.UINT32
                : DataTypeEnum.INT32;
            stride = 4;
            predicateMask = 0xFF;
        }

        var inst = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = dataType,
            PredicateMask = predicateMask,
            DestSrc1Pointer = lhsAddress,
            Src2Pointer = rhsAddress,
            StreamLength = 2,
            Stride = stride
        };

        StreamEngine.Execute(ref core, in inst);

        if (opcode == InstructionsEnum.VDOTF)
        {
            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(10.0f, BitConverter.ToSingle(resultBytes, 0));
            Assert.Equal(2.0f, BitConverter.ToSingle(resultBytes, 4));
            return;
        }

        if (opcode == InstructionsEnum.VDOT_FP8)
        {
            byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
            Assert.Equal(5.0f, BitConverter.ToSingle(resultBytes, 0));
            Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, resultBytes[4..8]);
            return;
        }

        byte[] intResultBytes = Processor.MainMemory.ReadFromPosition(new byte[8], lhsAddress, 8);
        Assert.Equal(41U, BitConverter.ToUInt32(intResultBytes, 0));
        Assert.Equal(7U, BitConverter.ToUInt32(intResultBytes, 4));
    }

    [Theory]
    [MemberData(nameof(RawUnaryParityOpcodes))]
    public void RawStreamEngine_WhenUnaryBitManipFamilyExecutes1D_ThenFollowsDedicatedUnarySemantics(
        InstructionsEnum opcode)
    {
        const ulong baseAddress = 0x640;
        ulong[] input = CreateUnaryParityInputs(elementCount: 3);
        ulong[] expected = ComputeExpectedUnaryResults(opcode, input);

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorUlongMemory(baseAddress, input);

        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT64,
            destSrc1Ptr: baseAddress,
            src2Ptr: 0,
            streamLength: (ulong)input.Length,
            stride: sizeof(ulong));

        StreamEngine.Execute(ref core, in inst);

        Assert.Equal(expected, ReadVectorUlongMemory(baseAddress, input.Length));
    }

    [Theory]
    [MemberData(nameof(RawUnaryParityOpcodes))]
    public void RawStreamEngine_WhenUnaryBitManipFamilyExecutesDoubleBuffered1D_ThenFollowsDedicatedUnarySemantics(
        InstructionsEnum opcode)
    {
        const ulong baseAddress = 0x840;
        ulong[] input = CreateUnaryParityInputs(elementCount: 40);
        ulong[] expected = ComputeExpectedUnaryResults(opcode, input);

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorUlongMemory(baseAddress, input);

        var core = new Processor.CPU_Core(0);
        core.SetPipelineMode(true);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT64,
            destSrc1Ptr: baseAddress,
            src2Ptr: 0,
            streamLength: (ulong)input.Length,
            stride: sizeof(ulong));

        StreamEngine.Execute(ref core, in inst);

        Assert.Equal(expected, ReadVectorUlongMemory(baseAddress, input.Length));
        Assert.True(core.GetPipelineControl().OverlappedCycles > 0);
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS)]
    [InlineData(InstructionsEnum.VEXPAND)]
    public void RawStreamEngine_WhenPredicativeMovementFamilyExecutesRepresentable1D_ThenFollowsAuthoritativeSingleSurfaceSemantics(
        InstructionsEnum opcode)
    {
        const ulong vectorAddress = 0x940;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0b1101UL);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: 0,
            streamLength: 4,
            stride: sizeof(uint));
        inst.PredicateMask = 5;

        StreamEngine.Execute(ref core, in inst);

        byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
        if (opcode == InstructionsEnum.VCOMPRESS)
        {
            Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 12));
        }
        else
        {
            Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
            Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 8));
            Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
        }
    }

    [Theory]
    [InlineData(InstructionsEnum.VCOMPRESS)]
    [InlineData(InstructionsEnum.VEXPAND)]
    public void RawStreamEngine_WhenPredicativeMovementFamilyExceedsScratchBackedFootprint_ThenFailsClosed(
        InstructionsEnum opcode)
    {
        var core = new Processor.CPU_Core(0);
        int maxRepresentableElements = Math.Min(
            core.GetScratchA().Length / sizeof(uint),
            core.GetScratchDst().Length / sizeof(uint));

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT32,
            destSrc1Ptr: 0x980,
            src2Ptr: 0,
            streamLength: (ulong)(maxRepresentableElements + 1),
            stride: sizeof(uint));
        inst.PredicateMask = 5;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("raw 1D stream/vector execution", ex.Message, StringComparison.Ordinal);
        Assert.Contains("must fail closed instead of publishing partial compaction/expansion truth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_WhenComparisonExecutesAcrossMultipleChunks_ThenAccumulatesSingleFinalPredicateMaskLikeAuthoritativeMainline()
    {
        const ulong lhsAddress = 0xD40;
        const ulong rhsAddress = 0xF40;
        const int elementCount = 40;

        uint[] lhs = new uint[elementCount];
        uint[] rhs = new uint[elementCount];
        ulong expectedMask = 0;
        for (int lane = 0; lane < elementCount; lane++)
        {
            lhs[lane] = (uint)(0x100 + lane);
            bool equal = lane is not (5 or 12 or 33);
            rhs[lane] = equal ? lhs[lane] : lhs[lane] + 1U;
            if (equal)
            {
                expectedMask |= 1UL << lane;
            }
        }

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(lhsAddress, lhs);
        SeedVectorWordMemory(rhsAddress, rhs);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VCMPEQ,
            DataTypeEnum.UINT32,
            destSrc1Ptr: lhsAddress,
            src2Ptr: rhsAddress,
            streamLength: elementCount,
            stride: sizeof(uint));
        inst.Immediate = 5;

        var rawCore = new Processor.CPU_Core(0);
        StreamEngine.Execute(ref rawCore, in inst);
        ulong rawMask = rawCore.GetPredicateRegister(5);

        var mainlineCore = new Processor.CPU_Core(0);
        VectorComparisonMicroOp microOp = Assert.IsType<VectorComparisonMicroOp>(MaterializeSingleSlotMicroOp(inst));
        Assert.True(microOp.Execute(ref mainlineCore));
        ulong mainlineMask = mainlineCore.GetPredicateRegister(5);

        Assert.Equal(expectedMask, rawMask);
        Assert.Equal(expectedMask, mainlineMask);
        Assert.Equal(mainlineMask, rawMask);
    }

    [Theory]
    [InlineData(InstructionsEnum.VPERMUTE)]
    [InlineData(InstructionsEnum.VRGATHER)]
    public void RawStreamEngine_WhenPermutationFamilyUsesMaskedUndisturbedLaneContour_ThenPreservesDestinationSurface(
        InstructionsEnum opcode)
    {
        const ulong vectorAddress = 0xA40;
        const ulong indexAddress = 0xB40;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);
        SeedVectorWordMemory(indexAddress, 2U, 0U, 3U, 2U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0b1101UL);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)opcode,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: indexAddress,
            streamLength: 4,
            stride: sizeof(uint));
        inst.PredicateMask = 5;

        StreamEngine.Execute(ref core, in inst);

        byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
        Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 0));
        Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
        Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
        Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
    }

    [Fact]
    public void RawStreamEngine_WhenSlideDownUsesMaskedUndisturbedLaneContour_ThenPreservesDestinationSurface()
    {
        const ulong vectorAddress = 0xC40;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

        var core = new Processor.CPU_Core(0);
        core.SetPredicateRegister(5, 0b1101UL);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VSLIDEDOWN,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: 0,
            streamLength: 4,
            stride: sizeof(uint));
        inst.Immediate = 1;
        inst.PredicateMask = 5;

        StreamEngine.Execute(ref core, in inst);

        byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
        Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 0));
        Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 4));
        Assert.Equal(40U, BitConverter.ToUInt32(resultBytes, 8));
        Assert.Equal(0U, BitConverter.ToUInt32(resultBytes, 12));
    }

    [Fact]
    public void RawStreamEngine_WhenSlideUpUsesRepresentativeActiveLaneContour_ThenMatchesAuthoritativeDestinationSurface()
    {
        const ulong vectorAddress = 0xC80;

        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();
        SeedVectorWordMemory(vectorAddress, 10U, 20U, 30U, 40U);

        var core = new Processor.CPU_Core(0);

        VLIW_Instruction inst = InstructionEncoder.EncodeVector1D(
            (uint)InstructionsEnum.VSLIDEUP,
            DataTypeEnum.UINT32,
            destSrc1Ptr: vectorAddress,
            src2Ptr: 0,
            streamLength: 4,
            stride: sizeof(uint));
        inst.Immediate = 1;

        StreamEngine.Execute(ref core, in inst);

        byte[] resultBytes = Processor.MainMemory.ReadFromPosition(new byte[16], vectorAddress, 16);
        Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 0));
        Assert.Equal(10U, BitConverter.ToUInt32(resultBytes, 4));
        Assert.Equal(20U, BitConverter.ToUInt32(resultBytes, 8));
        Assert.Equal(30U, BitConverter.ToUInt32(resultBytes, 12));
    }

    [Theory]
    [MemberData(
        nameof(VectorNonRepresentableAddressingTestHelper.RepresentativeContours),
        MemberType = typeof(VectorNonRepresentableAddressingTestHelper))]
    public void RawStreamEngine_WhenRepresentativeVectorFamilyUsesNonRepresentableIndexedOr2DContour_ThenRejectsCompatSuccessShell(
        VectorNonRepresentableFamily family,
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst =
            VectorNonRepresentableAddressingTestHelper.CreateInstruction(family, opcode, is2D);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            VectorNonRepresentableAddressingTestHelper.GetFactoryAddressingLabel(family),
            ex.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RawUnaryIndexedOr2DContours))]
    public void RawStreamEngine_WhenUnaryBitManipOrVsqrtContourUsesNonRepresentableIndexedOr2DAddressing_ThenRejectsCompatSuccessShell(
        InstructionsEnum opcode,
        bool is2D,
        string addressingContour)
    {
        var core = new Processor.CPU_Core(0);
        var inst = new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.UINT64,
            PredicateMask = 0x0F,
            DestSrc1Pointer = 0x280,
            Src2Pointer = 0x380,
            StreamLength = 4,
            Stride = sizeof(ulong),
            Indexed = !is2D,
            Is2D = is2D,
            Immediate = is2D ? (ushort)2 : (ushort)0,
            RowStride = is2D ? (ushort)16 : (ushort)0
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains(addressingContour, ex.Message, StringComparison.Ordinal);
        Assert.Contains("vector-unary addressing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_When1DContourLacksScratchBuffers_ThenFailsClosed()
    {
        var core = new Processor.CPU_Core(0);
        SetPrivateField(ref core, "ScratchA", Array.Empty<byte>());

        VLIW_Instruction inst = CreateRawMemoryContourInstruction(
            InstructionsEnum.Addition,
            streamLength: 4,
            indexed: false,
            is2D: false);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("raw 1D stream/vector execution", ex.Message, StringComparison.Ordinal);
        Assert.Contains("initialized scratch buffers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_WhenDoubleBuffered1DContourLacksScratchBuffers_ThenFailsClosed()
    {
        var core = new Processor.CPU_Core(0);
        core.SetPipelineMode(true);
        SetPrivateField(ref core, "ActiveBufferSet", 0);
        SetPrivateField(ref core, "ScratchA_DB0", Array.Empty<byte>());

        VLIW_Instruction inst = CreateRawMemoryContourInstruction(
            InstructionsEnum.Addition,
            streamLength: 40,
            indexed: false,
            is2D: false);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("double-buffered raw 1D stream/vector execution", ex.Message, StringComparison.Ordinal);
        Assert.Contains("initialized scratch buffers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_When2DContourLacksScratchBuffers_ThenFailsClosed()
    {
        var core = new Processor.CPU_Core(0);
        SetPrivateField(ref core, "ScratchA", Array.Empty<byte>());

        VLIW_Instruction inst = CreateRawMemoryContourInstruction(
            InstructionsEnum.Addition,
            streamLength: 4,
            indexed: false,
            is2D: true);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("raw 2D stream/vector execution", ex.Message, StringComparison.Ordinal);
        Assert.Contains("initialized scratch buffers", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_WhenIndexedContourLacksMaterializedPrerequisites_ThenFailsClosed()
    {
        InitializeCpuMainMemoryIdentityMap();
        InitializeMemorySubsystem();

        const ulong descriptorAddress = 0xA00;
        WriteIndexedDescriptor(
            descriptorAddress,
            src2Base: 0xB00,
            indexBase: 0xC00,
            indexStride: 4,
            indexType: 0,
            indexIsByteOffset: 0);

        var core = new Processor.CPU_Core(0);
        SetPrivateField(ref core, "ScratchIndex", Array.Empty<byte>());

        VLIW_Instruction inst = CreateRawMemoryContourInstruction(
            InstructionsEnum.Addition,
            streamLength: 4,
            indexed: true,
            is2D: false);
        inst.Src2Pointer = descriptorAddress;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("indexed StreamEngine.Execute(...)", ex.Message, StringComparison.Ordinal);
        Assert.True(
            ex.Message.Contains("unreadable descriptor", StringComparison.Ordinal) ||
            ex.Message.Contains("data/index scratch surfaces", StringComparison.Ordinal),
            $"Expected indexed contour to fail closed on descriptor materialization or scratch-readiness gate, but got: {ex.Message}");
    }

    [Fact]
    public void BurstRead2D_WhenRowLengthIsZeroOnNonZeroTransfer_ThenFailsClosed()
    {
        byte[] buffer = new byte[16];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstRead2D(
                baseAddr: 0x100,
                buffer,
                elementCount: 4,
                elementSize: sizeof(uint),
                rowLength: 0,
                rowStride: 16,
                colStride: sizeof(uint)));

        Assert.Contains(nameof(BurstIO.BurstRead2D), ex.Message, StringComparison.Ordinal);
        Assert.Contains("rowLength == 0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BurstWrite2D_WhenRowLengthIsZeroOnNonZeroTransfer_ThenFailsClosed()
    {
        byte[] buffer = new byte[16];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => BurstIO.BurstWrite2D(
                baseAddr: 0x100,
                buffer,
                elementCount: 4,
                elementSize: sizeof(uint),
                rowLength: 0,
                rowStride: 16,
                colStride: sizeof(uint)));

        Assert.Contains(nameof(BurstIO.BurstWrite2D), ex.Message, StringComparison.Ordinal);
        Assert.Contains("rowLength == 0", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RawStreamEngine_When2DContourUsesZeroRowLengthOnNonZeroRequest_ThenFailsClosed()
    {
        var core = new Processor.CPU_Core(0);
        VLIW_Instruction inst = CreateRawMemoryContourInstruction(
            InstructionsEnum.Addition,
            streamLength: 4,
            indexed: false,
            is2D: true);
        inst.Immediate = 0;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StreamEngine.Execute(ref core, in inst));

        Assert.Contains("raw 2D stream/vector execution", ex.Message, StringComparison.Ordinal);
        Assert.Contains("rowLength == 0", ex.Message, StringComparison.Ordinal);
    }

    private static VLIW_Instruction CreateRawScalarRegisterInstruction(
        uint rawOpcode,
        byte rd,
        byte rs1,
        byte rs2)
    {
        return InstructionEncoder.EncodeScalar(
            rawOpcode,
            DataTypeEnum.INT32,
            rd,
            rs1,
            rs2);
    }

    private static VLIW_Instruction CreateMalformedScalarRegisterInstruction(
        uint rawOpcode,
        ushort rd,
        ushort rs1,
        ushort rs2)
    {
        ulong packedRegisters = rd |
                                ((ulong)rs1 << 16) |
                                ((ulong)rs2 << 32);

        return new VLIW_Instruction
        {
            OpCode = rawOpcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0xFF,
            DestSrc1Pointer = packedRegisters,
            StreamLength = 1
        };
    }

    private static VLIW_Instruction CreateRawMemoryContourInstruction(
        InstructionsEnum opcode,
        ulong streamLength,
        bool indexed,
        bool is2D)
    {
        return new VLIW_Instruction
        {
            OpCode = (uint)opcode,
            DataTypeValue = DataTypeEnum.INT32,
            PredicateMask = 0x0F,
            DestSrc1Pointer = 0x220,
            Src2Pointer = 0x320,
            StreamLength = (uint)streamLength,
            Stride = sizeof(uint),
            Indexed = indexed,
            Is2D = is2D,
            Immediate = is2D ? (ushort)2 : (ushort)0,
            RowStride = is2D ? (ushort)16 : (ushort)0
        };
    }

    private static MicroOp MaterializeSingleSlotMicroOp(
        VLIW_Instruction instruction)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        rawSlots[0] = instruction;

        var decoder = new VliwDecoderV4();
        DecodedInstructionBundle canonicalBundle =
            decoder.DecodeInstructionBundle(rawSlots, bundleAddress: 0x2400, bundleSerial: 901);

        MicroOp?[] carrierBundle =
            DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(rawSlots, canonicalBundle);

        return Assert.IsAssignableFrom<MicroOp>(carrierBundle[0]);
    }

    private static ulong[] CreateUnaryParityInputs(int elementCount)
    {
        ulong[] values = new ulong[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            values[i] = unchecked(0x0102_0304_0506_0708UL + ((ulong)i * 0x1111_1111_1111_1111UL));
        }

        return values;
    }

    private static ulong[] ComputeExpectedUnaryResults(
        InstructionsEnum opcode,
        ulong[] input)
    {
        ulong[] expected = new ulong[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            expected[i] = ComputeExpectedUnaryResult(opcode, input[i]);
        }

        return expected;
    }

    private static ulong ComputeExpectedUnaryResult(
        InstructionsEnum opcode,
        ulong value)
    {
        return opcode switch
        {
            InstructionsEnum.VREVERSE => ReverseBits(value),
            InstructionsEnum.VPOPCNT => (ulong)BitOperations.PopCount(value),
            InstructionsEnum.VCLZ => (ulong)BitOperations.LeadingZeroCount(value),
            InstructionsEnum.VCTZ => value == 0
                ? 64UL
                : (ulong)BitOperations.TrailingZeroCount(value),
            InstructionsEnum.VBREV8 => BinaryPrimitives.ReverseEndianness(value),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode), opcode, null)
        };
    }

    private static ulong ReverseBits(ulong value)
    {
        ulong result = 0;
        for (int bit = 0; bit < 64; bit++)
        {
            result <<= 1;
            result |= value & 1UL;
            value >>= 1;
        }

        return result;
    }

    private static void SetPrivateField<TValue>(
        ref Processor.CPU_Core core,
        string fieldName,
        TValue value)
    {
        FieldInfo field = typeof(Processor.CPU_Core).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValueDirect(__makeref(core), value);
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


