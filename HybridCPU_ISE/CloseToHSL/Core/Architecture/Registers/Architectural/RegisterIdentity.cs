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
        public const byte MinValue = 0;
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

        /// <summary>
        /// Returns whether <paramref name="value"/> is representable as an
        /// architectural-register identity. This is not an operand-legality check.
        /// </summary>
        public static bool IsRepresentable(int value) =>
            (uint)value < RegisterCount;

        /// <summary>
        /// Reconstructs a checked architectural-register identity from its
        /// retained byte representation.
        /// </summary>
        public static ArchRegId FromRawValue(byte value) => new(value);

        /// <summary>
        /// Projects this valid architectural-register identity to its retained
        /// byte representation.
        /// </summary>
        public byte ToRawValue() => Value;

        public static ArchRegId Create(int value)
        {
            if (!TryCreate(value, out ArchRegId regId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Architectural register id must be in [0, {MaxValue}].");

            return regId;
        }

        public static bool TryCreate(int value, out ArchRegId regId)
        {
            if (IsRepresentable(value))
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
        public const byte MinValue = 0;
        public const byte MaxValue = SmtWayCount - 1;

        public byte Value { get; }

        [System.Text.Json.Serialization.JsonConstructor]
        public VtId(byte value)
        {
            if (value > MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"VT id must be in [0, {MaxValue}].");

            Value = value;
        }

        private VtId(byte value, bool skipValidation) => Value = value;

        /// <summary>
        /// Returns whether <paramref name="value"/> is representable as an SMT
        /// virtual-thread identity. This is not a legality or ownership check.
        /// </summary>
        public static bool IsRepresentable(int value) =>
            (uint)value < SmtWayCount;

        /// <summary>
        /// Reconstructs a checked SMT identity from its retained byte wire form.
        /// </summary>
        public static VtId FromRawValue(byte value) => new(value);

        /// <summary>
        /// Projects this valid SMT identity to its retained byte wire form.
        /// </summary>
        public byte ToRawValue() => Value;

        public static VtId Create(int value)
        {
            if (!TryCreate(value, out VtId vtId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"VT id must be in [0, {MaxValue}].");

            return vtId;
        }

        public static bool TryCreate(int value, out VtId vtId)
        {
            if (IsRepresentable(value))
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
        public const ushort MinValue = 0;
        public const ushort MaxValue = RegisterCount - 1;
        public static readonly PhysRegId Zero = new(0, skipValidation: true);

        public ushort Value { get; }

        public PhysRegId(ushort value)
        {
            if (value >= RegisterCount)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Physical register id must be in [0, {RegisterCount - 1}].");

            Value = value;
        }

        private PhysRegId(ushort value, bool skipValidation) => Value = value;

        /// <summary>
        /// Returns whether <paramref name="value"/> is representable as a
        /// physical-register identity. This is not an allocation or mapping check.
        /// </summary>
        public static bool IsRepresentable(int value) =>
            (uint)value < RegisterCount;

        /// <summary>
        /// Reconstructs a checked physical-register identity from its retained
        /// unsigned-short representation.
        /// </summary>
        public static PhysRegId FromRawValue(ushort value) => new(value);

        /// <summary>
        /// Projects this valid physical-register identity to its retained
        /// unsigned-short representation.
        /// </summary>
        public ushort ToRawValue() => Value;

        public static PhysRegId Create(int value)
        {
            if (!TryCreate(value, out PhysRegId regId))
                throw new ArgumentOutOfRangeException(nameof(value), value, $"Physical register id must be in [0, {RegisterCount - 1}].");

            return regId;
        }

        public static bool TryCreate(int value, out PhysRegId regId)
        {
            if (IsRepresentable(value))
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
