using System;
using System.Buffers.Binary;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests;

public sealed class MatrixTileFormalArithmeticAndLayoutTests
{
    [Fact]
    public void LayoutPolicies_AreVersionedStableAndOperationSpecific()
    {
        MatrixTileLayoutPolicy macc = MatrixTileLayoutPolicyAbi.CreateMaccPolicy();
        MatrixTileLayoutPolicy transpose =
            MatrixTileLayoutPolicyAbi.CreateTransposePolicy();

        Assert.True(MatrixTileLayoutPolicyAbi.Validate(
            macc,
            MatrixTileProjectedOperationKind.Macc).IsValid);
        Assert.True(MatrixTileLayoutPolicyAbi.Validate(
            transpose,
            MatrixTileProjectedOperationKind.Transpose).IsValid);
        Assert.NotEqual(0UL, macc.Fingerprint);
        Assert.NotEqual(macc.Fingerprint, transpose.Fingerprint);
        Assert.Equal(
            MatrixTileKIterationOrderKind.AscendingZeroToKMinusOne,
            macc.KIterationOrder);

        MatrixTileLayoutPolicy columnMajor = macc with
        {
            SourceAddressing = MatrixTileElementAddressingKind.ColumnMajor,
            Fingerprint = 0
        };
        columnMajor = columnMajor with
        {
            Fingerprint = MatrixTileLayoutPolicyAbi.ComputeFingerprint(columnMajor)
        };
        Assert.Equal(
            MatrixTileLayoutPolicyFaultKind.ContradictoryRule,
            MatrixTileLayoutPolicyAbi.Validate(
                columnMajor,
                MatrixTileProjectedOperationKind.Macc).FaultKind);
    }

    [Fact]
    public void RuntimeProjection_RequiresLayoutForMaccAndTranspose()
    {
        MatrixTileNumericPolicy numeric =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(
                MatrixTileNumericProfileId.SignedInt8ToInt32);
        VectorInstructionPayload payload = CreatePayload(DataTypeEnum.INT8) with
        {
            MatrixTileNumericPolicy = numeric
        };

        MatrixTileInstructionIrProjection missingMacc =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                InstructionsEnum.MTILE_MACC,
                payload,
                immediate: 2,
                requireExplicitNumericPolicy: true);
        MatrixTileInstructionIrProjection missingTranspose =
            MatrixTileIrProjectionAndMaterializer.ProjectDecodedVectorPayload(
                InstructionsEnum.MTRANSPOSE,
                CreatePayload(DataTypeEnum.INT8),
                immediate: 2,
                requireExplicitNumericPolicy: true);

        Assert.Equal(
            MatrixTileIrProjectionFaultKind.LayoutPolicyFault,
            missingMacc.FaultKind);
        Assert.Equal(
            MatrixTileLayoutPolicyFaultKind.MissingPolicy,
            missingMacc.LayoutPolicyValidation!.Value.FaultKind);
        Assert.Equal(
            MatrixTileIrProjectionFaultKind.LayoutPolicyFault,
            missingTranspose.FaultKind);
    }

    [Fact]
    public void IntegerMacc_UsesLittleEndianExactSumAndFinalOverflowTrap()
    {
        MatrixTileMaccSemanticContract contract = CreateContract(
            MatrixTileNumericProfileId.SignedInt16ToInt32,
            rows: 1,
            k: 1,
            columns: 1);
        MatrixTileTileImage left = Image(1, contract.Left, [0x02, 0x01]);
        MatrixTileTileImage right = Image(2, contract.Right, [0x01, 0x00]);
        MatrixTileTileImage accumulator =
            Image(3, contract.Accumulator, [0x00, 0x00, 0x00, 0x00]);

        Assert.True(MatrixTileMaccArithmeticAbi.TryCompute(
            contract,
            left,
            right,
            accumulator,
            out MatrixTileTileImage result,
            out MatrixTileMaccArithmeticFaultKind fault));
        Assert.Equal(MatrixTileMaccArithmeticFaultKind.None, fault);
        Assert.Equal(new byte[] { 0x02, 0x01, 0x00, 0x00 }, result.Data);

        MatrixTileMaccSemanticContract overflowContract = CreateContract(
            MatrixTileNumericProfileId.SignedInt64ToInt64,
            rows: 1,
            k: 1,
            columns: 1);
        byte[] max = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(max, long.MaxValue);
        byte[] one = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(one, 1);
        Assert.False(MatrixTileMaccArithmeticAbi.TryCompute(
            overflowContract,
            Image(1, overflowContract.Left, one),
            Image(2, overflowContract.Right, one),
            Image(3, overflowContract.Accumulator, max),
            out _,
            out MatrixTileMaccArithmeticFaultKind overflowFault));
        Assert.Equal(
            MatrixTileMaccArithmeticFaultKind.ArithmeticOverflow,
            overflowFault);
    }

    [Fact]
    public void Binary32Macc_UsesAscendingKAndSeparateRoundAfterMultiply()
    {
        MatrixTileMaccSemanticContract orderContract = CreateContract(
            MatrixTileNumericProfileId.Binary32ToBinary32,
            rows: 1,
            k: 3,
            columns: 1);
        byte[] left = PackUInt32(0x60AD78EC, 0xE0AD78EC, 0x4048F5C3);
        byte[] right = PackUInt32(0x3F800000, 0x3F800000, 0x3F800000);
        byte[] zero = PackUInt32(0x00000000);

        Assert.True(MatrixTileMaccArithmeticAbi.TryCompute(
            orderContract,
            Image(1, orderContract.Left, left),
            Image(2, orderContract.Right, right),
            Image(3, orderContract.Accumulator, zero),
            out MatrixTileTileImage ordered,
            out _));
        Assert.Equal(PackUInt32(0x4048F5C3), ordered.Data);

        MatrixTileMaccSemanticContract separateContract = CreateContract(
            MatrixTileNumericProfileId.Binary32ToBinary32,
            rows: 1,
            k: 1,
            columns: 1);
        Assert.True(MatrixTileMaccArithmeticAbi.TryCompute(
            separateContract,
            Image(1, separateContract.Left, PackUInt32(0x3F800001)),
            Image(2, separateContract.Right, PackUInt32(0x3F7FFFFE)),
            Image(3, separateContract.Accumulator, PackUInt32(0xBF800000)),
            out MatrixTileTileImage separatelyRounded,
            out _));
        Assert.Equal(PackUInt32(0x00000000), separatelyRounded.Data);
    }

    [Theory]
    [InlineData(0x7F800001u, 0x7FC00000u)]
    [InlineData(0x7F800000u, 0x7F800000u)]
    [InlineData(0x00000001u, 0x00000001u)]
    public void Binary32Macc_CanonicalizesNaNAndPreservesInfinityAndSubnormal(
        uint input,
        uint expected)
    {
        MatrixTileMaccSemanticContract contract = CreateContract(
            MatrixTileNumericProfileId.Binary32ToBinary32,
            rows: 1,
            k: 1,
            columns: 1);

        Assert.True(MatrixTileMaccArithmeticAbi.TryCompute(
            contract,
            Image(1, contract.Left, PackUInt32(input)),
            Image(2, contract.Right, PackUInt32(0x3F800000)),
            Image(3, contract.Accumulator, PackUInt32(0)),
            out MatrixTileTileImage result,
            out _));
        Assert.Equal(PackUInt32(expected), result.Data);
    }

    [Fact]
    public void Binary64Macc_ProducesByteExactSoftwareIeeeResult()
    {
        MatrixTileMaccSemanticContract contract = CreateContract(
            MatrixTileNumericProfileId.Binary64ToBinary64,
            rows: 1,
            k: 1,
            columns: 1);

        Assert.True(MatrixTileMaccArithmeticAbi.TryCompute(
            contract,
            Image(1, contract.Left, PackUInt64(0x3FF8000000000000)),
            Image(2, contract.Right, PackUInt64(0x4000000000000000)),
            Image(3, contract.Accumulator, PackUInt64(0)),
            out MatrixTileTileImage result,
            out _));
        Assert.Equal(PackUInt64(0x4008000000000000), result.Data);
    }

    [Fact]
    public void TransposePolicy_RejectsNonSquareInPlaceAndTamperedLayout()
    {
        MatrixTileCanonicalDescriptorAbi source =
            MatrixTileCanonicalDescriptorAbi.Create(2, 3, 1, 3);
        MatrixTileCanonicalDescriptorAbi destination =
            MatrixTileCanonicalDescriptorAbi.Create(3, 2, 1, 2);
        MatrixTileLayoutPolicy layout =
            MatrixTileLayoutPolicyAbi.CreateTransposePolicy();

        MatrixTileTransposeSemanticContract inPlace =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                source,
                destination,
                sourceTileId: 1,
                destinationTileId: 1,
                layoutPolicy: layout);
        Assert.Equal(
            MatrixTileSemanticFaultKind.TransposeInPlaceRequiresSquareShape,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateRuntimeTranspose(
                inPlace).FaultKind);

        MatrixTileLayoutPolicy tampered = layout with
        {
            DestinationAddressing = MatrixTileElementAddressingKind.Blocked,
            Fingerprint = 0
        };
        tampered = tampered with
        {
            Fingerprint = MatrixTileLayoutPolicyAbi.ComputeFingerprint(tampered)
        };
        MatrixTileTransposeSemanticContract invalidLayout =
            MatrixTileAccumulatorAndTransposePolicyAbi.CreateTransposeContract(
                source,
                destination,
                sourceTileId: 1,
                destinationTileId: 2,
                layoutPolicy: tampered);
        Assert.Equal(
            MatrixTileSemanticFaultKind.InvalidLayoutPolicy,
            MatrixTileAccumulatorAndTransposePolicyAbi.ValidateRuntimeTranspose(
                invalidLayout).FaultKind);
    }

    private static MatrixTileMaccSemanticContract CreateContract(
        MatrixTileNumericProfileId profileId,
        ushort rows,
        ushort k,
        ushort columns)
    {
        MatrixTileNumericPolicy numeric =
            MatrixTileNumericPolicyAbi.CreateSupportedPolicy(profileId);
        ushort sourceElementSize = checked((ushort)
            MatrixTileNumericPolicyAbi.GetElementSizeBytes(numeric.ElementType));
        ushort accumulatorElementSize = checked((ushort)
            MatrixTileNumericPolicyAbi.GetElementSizeBytes(numeric.AccumulatorType));
        MatrixTileCanonicalDescriptorAbi left =
            MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                k,
                sourceElementSize,
                checked((uint)(k * sourceElementSize)));
        MatrixTileCanonicalDescriptorAbi right =
            MatrixTileCanonicalDescriptorAbi.Create(
                k,
                columns,
                sourceElementSize,
                checked((uint)(columns * sourceElementSize)));
        MatrixTileCanonicalDescriptorAbi accumulator =
            MatrixTileCanonicalDescriptorAbi.Create(
                rows,
                columns,
                accumulatorElementSize,
                checked((uint)(columns * accumulatorElementSize)));
        return MatrixTileAccumulatorAndTransposePolicyAbi.CreateMaccContract(
            left,
            right,
            accumulator,
            numeric,
            MatrixTileLayoutPolicyAbi.CreateMaccPolicy());
    }

    private static MatrixTileTileImage Image(
        ushort tileId,
        MatrixTileCanonicalDescriptorAbi descriptor,
        byte[] data) =>
        MatrixTileTileImage.Create(tileId, descriptor, data);

    private static byte[] PackUInt32(params uint[] values)
    {
        byte[] data = new byte[values.Length * sizeof(uint)];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                data.AsSpan(index * sizeof(uint), sizeof(uint)),
                values[index]);
        }

        return data;
    }

    private static byte[] PackUInt64(params ulong[] values)
    {
        byte[] data = new byte[values.Length * sizeof(ulong)];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(
                data.AsSpan(index * sizeof(ulong), sizeof(ulong)),
                values[index]);
        }

        return data;
    }

    private static VectorInstructionPayload CreatePayload(DataTypeEnum dataType) =>
        new(
            PrimaryPointer: 1,
            SecondaryPointer: 2,
            StreamLength: 4,
            Stride: 1,
            RowStride: 2,
            Indexed: false,
            Is2D: true,
            TailAgnostic: false,
            MaskAgnostic: false,
            Saturating: false,
            PredicateMask: 0,
            DataType: (byte)dataType);
}
