using System.Buffers.Binary;
using System.Numerics;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;

namespace YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Vector.MatrixTile;

public enum MatrixTileMaccArithmeticFaultKind : byte
{
    None = 0,
    InvalidNumericPolicy = 1,
    InvalidLayoutPolicy = 2,
    InvalidSnapshot = 3,
    ArithmeticOverflow = 4,
}

public static class MatrixTileMaccArithmeticAbi
{
    public const string FormalFunctionDecision =
        "AccumulatorSnapshotThenAscendingKMultiplyAddThenFinalize";
    public const string IntegerDecision =
        "ExactIntegerProductAndUnboundedSumWithFinalEncodingTrap";
    public const string FloatingDecision =
        "SoftwareIeee754SeparateMultiplyAndAddRoundToNearestTiesToEven";
    public const string ByteOrderDecision = "CanonicalLittleEndianElementCodec";
    public const bool UsesHostFloatingPointArithmetic = false;
    public const bool UsesFusedMultiplyAdd = false;
    public const bool UsesHostMatrixLibrary = false;

    public static bool TryCompute(
        MatrixTileMaccSemanticContract contract,
        MatrixTileTileImage left,
        MatrixTileTileImage right,
        MatrixTileTileImage accumulator,
        out MatrixTileTileImage resultImage,
        out MatrixTileMaccArithmeticFaultKind faultKind)
    {
        resultImage = default;
        faultKind = MatrixTileMaccArithmeticFaultKind.None;

        if (!MatrixTileNumericPolicyAbi.Validate(contract.NumericPolicy).IsRuntimeOwnedNumericPolicyAccepted)
        {
            faultKind = MatrixTileMaccArithmeticFaultKind.InvalidNumericPolicy;
            return false;
        }

        if (!MatrixTileLayoutPolicyAbi.Validate(
                contract.LayoutPolicy,
                MatrixTileProjectedOperationKind.Macc).IsRuntimeOwnedLayoutPolicyAccepted ||
            !MatrixTileLayoutPolicyAbi.ValidateDescriptors(
                contract.LayoutPolicy,
                contract.Left,
                contract.Right,
                contract.Accumulator).IsRuntimeOwnedLayoutPolicyAccepted)
        {
            faultKind = MatrixTileMaccArithmeticFaultKind.InvalidLayoutPolicy;
            return false;
        }

        if (!left.IsCanonicalPacked ||
            !right.IsCanonicalPacked ||
            !accumulator.IsCanonicalPacked ||
            !left.Descriptor.Equals(contract.Left) ||
            !right.Descriptor.Equals(contract.Right) ||
            !accumulator.Descriptor.Equals(contract.Accumulator))
        {
            faultKind = MatrixTileMaccArithmeticFaultKind.InvalidSnapshot;
            return false;
        }

        byte[] result = (byte[])accumulator.Data.Clone();
        bool success = DataTypeUtils.IsFloatingPoint(contract.NumericPolicy.ElementType)
            ? TryComputeFloating(contract, left.Data, right.Data, result)
            : TryComputeInteger(contract, left.Data, right.Data, result);
        if (!success)
        {
            faultKind = MatrixTileMaccArithmeticFaultKind.ArithmeticOverflow;
            return false;
        }

        resultImage = MatrixTileTileImage.Create(
            accumulator.TileId,
            contract.Accumulator,
            result);
        return true;
    }

    private static bool TryComputeInteger(
        MatrixTileMaccSemanticContract contract,
        byte[] left,
        byte[] right,
        byte[] result)
    {
        bool signed = contract.NumericPolicy.Signedness ==
            MatrixTileNumericSignedness.Signed;
        MatrixTileLayoutPolicy layout = contract.LayoutPolicy;

        for (ushort row = 0; row < contract.Left.Rows; row++)
        {
            for (ushort column = 0; column < contract.Right.Columns; column++)
            {
                BigInteger sum = ReadInteger(
                    result,
                    contract.Accumulator,
                    row,
                    column,
                    layout.DestinationAddressing,
                    signed);

                for (ushort k = 0; k < contract.Left.Columns; k++)
                {
                    BigInteger lhs = ReadInteger(
                        left,
                        contract.Left,
                        row,
                        k,
                        layout.SourceAddressing,
                        signed);
                    BigInteger rhs = ReadInteger(
                        right,
                        contract.Right,
                        k,
                        column,
                        layout.SecondaryAddressing,
                        signed);
                    sum += lhs * rhs;
                }

                if (!TryWriteInteger(
                        result,
                        contract.Accumulator,
                        row,
                        column,
                        layout.DestinationAddressing,
                        sum,
                        signed))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryComputeFloating(
        MatrixTileMaccSemanticContract contract,
        byte[] left,
        byte[] right,
        byte[] result)
    {
        BinaryFormat format = contract.NumericPolicy.ElementType switch
        {
            DataTypeEnum.FLOAT32 => BinaryFormat.Binary32,
            DataTypeEnum.FLOAT64 => BinaryFormat.Binary64,
            _ => default,
        };
        if (format.TotalBits == 0)
        {
            return false;
        }

        MatrixTileLayoutPolicy layout = contract.LayoutPolicy;
        for (ushort row = 0; row < contract.Left.Rows; row++)
        {
            for (ushort column = 0; column < contract.Right.Columns; column++)
            {
                ulong accumulatorBits = ReadBits(
                    result,
                    contract.Accumulator,
                    row,
                    column,
                    layout.DestinationAddressing,
                    format);

                for (ushort k = 0; k < contract.Left.Columns; k++)
                {
                    ulong lhsBits = ReadBits(
                        left,
                        contract.Left,
                        row,
                        k,
                        layout.SourceAddressing,
                        format);
                    ulong rhsBits = ReadBits(
                        right,
                        contract.Right,
                        k,
                        column,
                        layout.SecondaryAddressing,
                        format);
                    ulong productBits = Multiply(lhsBits, rhsBits, format);
                    accumulatorBits = Add(accumulatorBits, productBits, format);
                }

                WriteBits(
                    result,
                    contract.Accumulator,
                    row,
                    column,
                    layout.DestinationAddressing,
                    format,
                    accumulatorBits);
            }
        }

        return true;
    }

    private static BigInteger ReadInteger(
        byte[] data,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ushort row,
        ushort column,
        MatrixTileElementAddressingKind addressing,
        bool signed)
    {
        int offset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
            descriptor,
            row,
            column,
            addressing);
        ReadOnlySpan<byte> source = data.AsSpan(offset, descriptor.ElementSizeBytes);
        return descriptor.ElementSizeBytes switch
        {
            1 => signed ? unchecked((sbyte)source[0]) : source[0],
            2 => signed
                ? BinaryPrimitives.ReadInt16LittleEndian(source)
                : BinaryPrimitives.ReadUInt16LittleEndian(source),
            4 => signed
                ? BinaryPrimitives.ReadInt32LittleEndian(source)
                : BinaryPrimitives.ReadUInt32LittleEndian(source),
            8 => signed
                ? BinaryPrimitives.ReadInt64LittleEndian(source)
                : BinaryPrimitives.ReadUInt64LittleEndian(source),
            _ => throw new InvalidOperationException(
                $"Unsupported MatrixTile integer element size {descriptor.ElementSizeBytes}.")
        };
    }

    private static bool TryWriteInteger(
        byte[] data,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ushort row,
        ushort column,
        MatrixTileElementAddressingKind addressing,
        BigInteger value,
        bool signed)
    {
        if (!FitsInteger(value, descriptor.ElementSizeBytes, signed))
        {
            return false;
        }

        int offset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
            descriptor,
            row,
            column,
            addressing);
        Span<byte> destination = data.AsSpan(offset, descriptor.ElementSizeBytes);
        switch (descriptor.ElementSizeBytes)
        {
            case 1:
                destination[0] = signed
                    ? unchecked((byte)(sbyte)value)
                    : (byte)value;
                return true;
            case 2:
                if (signed)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(destination, (short)value);
                }
                else
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)value);
                }

                return true;
            case 4:
                if (signed)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(destination, (int)value);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)value);
                }

                return true;
            case 8:
                if (signed)
                {
                    BinaryPrimitives.WriteInt64LittleEndian(destination, (long)value);
                }
                else
                {
                    BinaryPrimitives.WriteUInt64LittleEndian(destination, (ulong)value);
                }

                return true;
            default:
                return false;
        }
    }

    private static bool FitsInteger(
        BigInteger value,
        ushort elementSizeBytes,
        bool signed)
    {
        int bitCount = elementSizeBytes * 8;
        if (signed)
        {
            BigInteger min = -(BigInteger.One << (bitCount - 1));
            BigInteger max = (BigInteger.One << (bitCount - 1)) - 1;
            return value >= min && value <= max;
        }

        BigInteger unsignedMax = (BigInteger.One << bitCount) - 1;
        return value >= BigInteger.Zero && value <= unsignedMax;
    }

    private static ulong ReadBits(
        byte[] data,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ushort row,
        ushort column,
        MatrixTileElementAddressingKind addressing,
        BinaryFormat format)
    {
        int offset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
            descriptor,
            row,
            column,
            addressing);
        ReadOnlySpan<byte> source = data.AsSpan(offset, format.TotalBits / 8);
        return format.TotalBits == 32
            ? BinaryPrimitives.ReadUInt32LittleEndian(source)
            : BinaryPrimitives.ReadUInt64LittleEndian(source);
    }

    private static void WriteBits(
        byte[] data,
        MatrixTileCanonicalDescriptorAbi descriptor,
        ushort row,
        ushort column,
        MatrixTileElementAddressingKind addressing,
        BinaryFormat format,
        ulong bits)
    {
        int offset = MatrixTileLayoutPolicyAbi.GetPackedOffset(
            descriptor,
            row,
            column,
            addressing);
        Span<byte> destination = data.AsSpan(offset, format.TotalBits / 8);
        if (format.TotalBits == 32)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, checked((uint)bits));
        }
        else
        {
            BinaryPrimitives.WriteUInt64LittleEndian(destination, bits);
        }
    }

    private static ulong Multiply(ulong leftBits, ulong rightBits, BinaryFormat format)
    {
        BinaryValue left = Decode(leftBits, format);
        BinaryValue right = Decode(rightBits, format);
        if (left.Kind == BinaryValueKind.NaN || right.Kind == BinaryValueKind.NaN)
        {
            return format.CanonicalNaN;
        }

        if ((left.Kind == BinaryValueKind.Infinity && right.IsZero) ||
            (right.Kind == BinaryValueKind.Infinity && left.IsZero))
        {
            return format.CanonicalNaN;
        }

        bool negative = left.Negative ^ right.Negative;
        if (left.Kind == BinaryValueKind.Infinity ||
            right.Kind == BinaryValueKind.Infinity)
        {
            return EncodeInfinity(negative, format);
        }

        if (left.IsZero || right.IsZero)
        {
            return EncodeZero(negative, format);
        }

        return Round(
            BinaryValue.Finite(
                negative,
                left.Significand * right.Significand,
                left.Exponent),
            format,
            exponentAdjustment: right.Exponent);
    }

    private static ulong Add(ulong leftBits, ulong rightBits, BinaryFormat format)
    {
        BinaryValue left = Decode(leftBits, format);
        BinaryValue right = Decode(rightBits, format);
        if (left.Kind == BinaryValueKind.NaN || right.Kind == BinaryValueKind.NaN)
        {
            return format.CanonicalNaN;
        }

        if (left.Kind == BinaryValueKind.Infinity ||
            right.Kind == BinaryValueKind.Infinity)
        {
            if (left.Kind == BinaryValueKind.Infinity &&
                right.Kind == BinaryValueKind.Infinity &&
                left.Negative != right.Negative)
            {
                return format.CanonicalNaN;
            }

            BinaryValue infinity =
                left.Kind == BinaryValueKind.Infinity ? left : right;
            return EncodeInfinity(infinity.Negative, format);
        }

        if (left.IsZero && right.IsZero)
        {
            return EncodeZero(left.Negative && right.Negative, format);
        }

        int commonExponent = Math.Min(left.Exponent, right.Exponent);
        BigInteger leftInteger = left.Significand << (left.Exponent - commonExponent);
        BigInteger rightInteger = right.Significand << (right.Exponent - commonExponent);
        if (left.Negative)
        {
            leftInteger = -leftInteger;
        }

        if (right.Negative)
        {
            rightInteger = -rightInteger;
        }

        BigInteger exact = leftInteger + rightInteger;
        if (exact.IsZero)
        {
            return EncodeZero(negative: false, format);
        }

        return Round(
            BinaryValue.Finite(
                exact.Sign < 0,
                BigInteger.Abs(exact),
                commonExponent),
            format);
    }

    private static BinaryValue Decode(ulong bits, BinaryFormat format)
    {
        bool negative = (bits & format.SignMask) != 0;
        ulong exponentField = (bits >> format.FractionBits) & format.ExponentMask;
        ulong fraction = bits & format.FractionMask;
        if (exponentField == format.ExponentMask)
        {
            return fraction == 0
                ? BinaryValue.Infinity(negative)
                : BinaryValue.NaN();
        }

        if (exponentField == 0)
        {
            return BinaryValue.Finite(
                negative,
                fraction,
                1 - format.Bias - format.FractionBits);
        }

        return BinaryValue.Finite(
            negative,
            (BigInteger.One << format.FractionBits) + fraction,
            checked((int)exponentField) - format.Bias - format.FractionBits);
    }

    private static ulong Round(
        BinaryValue value,
        BinaryFormat format,
        int exponentAdjustment = 0)
    {
        if (value.Significand.IsZero)
        {
            return EncodeZero(value.Negative, format);
        }

        int exponent = checked(value.Exponent + exponentAdjustment);
        int bitLength = checked((int)value.Significand.GetBitLength());
        int topExponent = checked(bitLength - 1 + exponent);
        int minimumNormalExponent = 1 - format.Bias;
        int maximumNormalExponent = format.Bias;

        if (topExponent >= minimumNormalExponent)
        {
            int precision = format.FractionBits + 1;
            int shift = bitLength - precision;
            BigInteger rounded = shift > 0
                ? RoundShiftRightToEven(value.Significand, shift)
                : value.Significand << -shift;

            if (rounded.GetBitLength() > precision)
            {
                rounded >>= 1;
                topExponent++;
            }

            if (topExponent > maximumNormalExponent)
            {
                return EncodeInfinity(value.Negative, format);
            }

            ulong exponentField = checked((ulong)(topExponent + format.Bias));
            BigInteger hiddenBit = BigInteger.One << format.FractionBits;
            ulong fraction = checked((ulong)(rounded - hiddenBit));
            return Sign(value.Negative, format) |
                   (exponentField << format.FractionBits) |
                   fraction;
        }

        int subnormalQuantumExponent =
            minimumNormalExponent - format.FractionBits;
        int subnormalShift = subnormalQuantumExponent - exponent;
        BigInteger subnormal = subnormalShift > 0
            ? RoundShiftRightToEven(value.Significand, subnormalShift)
            : value.Significand << -subnormalShift;
        if (subnormal.IsZero)
        {
            return EncodeZero(value.Negative, format);
        }

        BigInteger smallestNormal = BigInteger.One << format.FractionBits;
        if (subnormal >= smallestNormal)
        {
            return Sign(value.Negative, format) |
                   (1UL << format.FractionBits);
        }

        return Sign(value.Negative, format) | checked((ulong)subnormal);
    }

    private static BigInteger RoundShiftRightToEven(
        BigInteger value,
        int shift)
    {
        if (shift <= 0)
        {
            return value << -shift;
        }

        BigInteger quotient = value >> shift;
        BigInteger remainder = value - (quotient << shift);
        BigInteger halfway = BigInteger.One << (shift - 1);
        if (remainder > halfway ||
            (remainder == halfway && !quotient.IsEven))
        {
            quotient += BigInteger.One;
        }

        return quotient;
    }

    private static ulong EncodeInfinity(bool negative, BinaryFormat format) =>
        Sign(negative, format) |
        (format.ExponentMask << format.FractionBits);

    private static ulong EncodeZero(bool negative, BinaryFormat format) =>
        Sign(negative, format);

    private static ulong Sign(bool negative, BinaryFormat format) =>
        negative ? format.SignMask : 0UL;

    private enum BinaryValueKind : byte
    {
        Finite = 0,
        Infinity = 1,
        NaN = 2,
    }

    private readonly record struct BinaryValue(
        BinaryValueKind Kind,
        bool Negative,
        BigInteger Significand,
        int Exponent)
    {
        public bool IsZero =>
            Kind == BinaryValueKind.Finite && Significand.IsZero;

        public static BinaryValue Finite(
            bool negative,
            BigInteger significand,
            int exponent) =>
            new(BinaryValueKind.Finite, negative, significand, exponent);

        public static BinaryValue Infinity(bool negative) =>
            new(BinaryValueKind.Infinity, negative, BigInteger.Zero, 0);

        public static BinaryValue NaN() =>
            new(BinaryValueKind.NaN, false, BigInteger.Zero, 0);
    }

    private readonly record struct BinaryFormat(
        int TotalBits,
        int ExponentBits,
        int FractionBits,
        int Bias,
        ulong CanonicalNaN)
    {
        public static BinaryFormat Binary32 { get; } =
            new(32, 8, 23, 127, 0x7FC00000UL);

        public static BinaryFormat Binary64 { get; } =
            new(64, 11, 52, 1023, 0x7FF8000000000000UL);

        public ulong SignMask => 1UL << (TotalBits - 1);

        public ulong ExponentMask => (1UL << ExponentBits) - 1;

        public ulong FractionMask => (1UL << FractionBits) - 1;
    }
}
