using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public readonly record struct AcceleratorTokenHandle(ulong Value)
{
    public static AcceleratorTokenHandle Invalid => new(0);

    public bool IsValid => Value != 0;

    public static AcceleratorTokenHandle FromOpaqueValue(ulong value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "L7-SDC accelerator token handle value zero is invalid.");
        }

        return new AcceleratorTokenHandle(value);
    }

    public override string ToString() => IsValid
        ? $"0x{Value:X16}"
        : "invalid";
}
