using System;

namespace YAKSys_Hybrid_CPU.Core.Registers
{
    public readonly record struct ArchRegId
    {
        /// <summary>
        /// Decode-side dependency summaries currently encode architectural register usage
        /// into 64-bit read/write masks. If the architectural register space grows beyond
        /// this boundary, transport/dependency summaries must migrate to a wider bitset
        /// rather than silently truncating published register facts.
        /// </summary>
        public const int DependencyMaskBitCount = 64;

        public const int RegisterCount = 32;
        public const byte MaxValue = RegisterCount - 1;

        public static ArchRegId Zero { get; } = new(0, skipValidation: true);

        public byte Value { get; }

        public ArchRegId(byte value)
        {
            if (value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Architectural register id must be in [0, {MaxValue}].");

            Value = value;
        }

        private ArchRegId(byte value, bool skipValidation) => Value = value;

        public static ArchRegId Create(int value)
        {
            if (!TryCreate(value, out ArchRegId regId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Architectural register id must be in [0, {MaxValue}].");

            return regId;
        }

        public static bool TryCreate(int value, out ArchRegId regId)
        {
            if ((uint)value < RegisterCount)
            {
                regId = new ArchRegId((byte)value, skipValidation: true);
                return true;
            }

            regId = default;
            return false;
        }

        public override string ToString() => $"x{Value}";

        public static implicit operator int(ArchRegId regId) => regId.Value;
        public static explicit operator byte(ArchRegId regId) => regId.Value;
        public static explicit operator ArchRegId(int value) => Create(value);
    }

    public readonly record struct VtId
    {
        public const int SmtWayCount = 4;
        public const byte MaxValue = SmtWayCount - 1;

        public byte Value { get; }

        public VtId(byte value)
        {
            if (value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"VT id must be in [0, {MaxValue}].");

            Value = value;
        }

        private VtId(byte value, bool skipValidation) => Value = value;

        public static VtId Create(int value)
        {
            if (!TryCreate(value, out VtId vtId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"VT id must be in [0, {MaxValue}].");

            return vtId;
        }

        public static bool TryCreate(int value, out VtId vtId)
        {
            if ((uint)value < SmtWayCount)
            {
                vtId = new VtId((byte)value, skipValidation: true);
                return true;
            }

            vtId = default;
            return false;
        }

        public override string ToString() => $"vt{Value}";

        public static implicit operator int(VtId vtId) => vtId.Value;
        public static explicit operator byte(VtId vtId) => vtId.Value;
        public static explicit operator VtId(int value) => Create(value);
    }

    public readonly record struct PhysRegId
    {
        public const int RegisterCount = PhysicalRegisterFile.TotalPhysRegs;
        public static readonly PhysRegId Zero = new(0, skipValidation: true);

        public ushort Value { get; }

        public PhysRegId(ushort value)
        {
            if (value >= RegisterCount)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Physical register id must be in [0, {RegisterCount - 1}].");

            Value = value;
        }

        private PhysRegId(ushort value, bool skipValidation) => Value = value;

        public static PhysRegId Create(int value)
        {
            if (!TryCreate(value, out PhysRegId regId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Physical register id must be in [0, {RegisterCount - 1}].");

            return regId;
        }

        public static bool TryCreate(int value, out PhysRegId regId)
        {
            if ((uint)value < RegisterCount)
            {
                regId = new PhysRegId((ushort)value, skipValidation: true);
                return true;
            }

            regId = default;
            return false;
        }

        public override string ToString() => $"p{Value}";

        public static implicit operator int(PhysRegId regId) => regId.Value;
        public static explicit operator ushort(PhysRegId regId) => regId.Value;
        public static explicit operator PhysRegId(int value) => Create(value);
    }
}
