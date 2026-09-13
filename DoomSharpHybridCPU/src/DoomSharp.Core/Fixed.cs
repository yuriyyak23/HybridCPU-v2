namespace DoomSharp.Core;

public readonly struct Fixed : IComparable<Fixed>
{
    private readonly int _value;

    public Fixed(int value)
    {
        _value = value;
    }

    public static readonly Fixed MinValue = new(int.MinValue);
    public static readonly Fixed MaxValue = new(int.MaxValue);
    public static readonly Fixed Zero = new(0);
    public static readonly Fixed Unit = new(Constants.FracUnit);

    public int Value => _value;

    public static int RawValue(Fixed value) => value._value;

    public Fixed AddRadius(int radius)
    {
        return new Fixed(_value + radius);
    }

    public static Fixed FromInt(int value)
    {
        return new Fixed(value << Constants.FracBits);
    }

    public static Fixed FromRaw(int value) => new(value);

    public static Fixed Abs(Fixed value)
    {
        if (value._value < 0)
        {
            return -value;
        }

        return value;
    }

    public static Fixed operator +(Fixed a, Fixed b)
    {
        return new Fixed(a._value + b._value);
    }

    public static Fixed operator -(Fixed a, Fixed b)
    {
        return new Fixed(a._value - b._value);
    }
    
    public static Fixed operator -(Fixed a)
    {
        return new Fixed(-a._value);
    }

    public static Fixed operator *(Fixed a, Fixed b)
    {
        return new Fixed((int)(((long)a._value * b._value) >> Constants.FracBits));
    }

    public static Fixed operator *(int a, Fixed b)
    {
        return new Fixed(a * b._value);
    }

    public static Fixed operator *(Fixed a, int b)
    {
        return new Fixed(a._value * b);
    }

    public static Fixed operator /(Fixed a, Fixed b)
    {
        if (b._value == 0)
        {
            DoomGame.Error("FixedDiv: divide by zero");
            return new Fixed(0);
        }

        if ((Math.Abs((long)a._value) >> 14) >= Math.Abs((long)b._value))
        {
            return new Fixed((a._value ^ b._value) < 0 ? int.MinValue : int.MaxValue);
        }

        return Div2(a, b);
    }

    private static Fixed Div2(Fixed a, Fixed b)
    {
        if (b._value == 0)
        {
            DoomGame.Error("FixedDiv: divide by zero");
            return new Fixed(0);
        }

        var quotient = ((long)a._value << Constants.FracBits) / b._value;
        if (quotient > int.MaxValue)
        {
            return MaxValue;
        }

        if (quotient < int.MinValue)
        {
            return MinValue;
        }

        return new Fixed((int)quotient);
    }

    public static Fixed operator /(Fixed a, int b)
    {
        return new Fixed(a._value / b);
    }

    public static Fixed operator <<(Fixed a, int b)
    {
        return new Fixed(a._value << b);
    }

    public static Fixed operator >>(Fixed a, int b)
    {
        return new Fixed(a._value >> b);
    }

    public static bool operator ==(Fixed a, Fixed b)
    {
        return a._value == b._value;
    }

    public static bool operator !=(Fixed a, Fixed b)
    {
        return a._value != b._value;
    }

    public static bool operator <(Fixed a, Fixed b)
    {
        return a._value < b._value;
    }

    public static bool operator >(Fixed a, Fixed b)
    {
        return a._value > b._value;
    }

    public static bool operator <=(Fixed a, Fixed b)
    {
        return a._value <= b._value;
    }

    public static bool operator >=(Fixed a, Fixed b)
    {
        return a._value >= b._value;
    }
    
    public bool Equals(Fixed other)
    {
        return _value == other._value;
    }

    public override bool Equals(object? obj)
    {
        return obj is Fixed other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }

    public override string ToString()
    {
        return (_value / Constants.FracUnit).ToString();
    }

    public int CompareTo(Fixed other)
    {
        return _value.CompareTo(other._value);
    }
}
