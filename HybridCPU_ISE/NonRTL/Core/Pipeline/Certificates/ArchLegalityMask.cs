using System;

namespace YAKSys_Hybrid_CPU.Core
{
    /// <summary>
    /// Architectural legality mask — encodes ISA-level hazards detectable from
    /// architectural register IDs alone (RAW, WAR, WAW via arch-reg groups).
    ///
    /// Blueprint §7 / Checklist: "Развести ArchLegalityMask и BackendHazardMask."
    /// </summary>
    public readonly struct ArchLegalityMask : IEquatable<ArchLegalityMask>
    {
        public readonly SafetyMask128 Mask;

        public ArchLegalityMask(SafetyMask128 mask) => Mask = mask;
        public ArchLegalityMask(ulong low, ulong high = 0) => Mask = new SafetyMask128(low, high);

        public bool IsZero => Mask.IsZero;

        public bool ConflictsWith(ArchLegalityMask other) => Mask.ConflictsWith(other.Mask);

        public static ArchLegalityMask operator |(ArchLegalityMask a, ArchLegalityMask b)
            => new ArchLegalityMask(a.Mask | b.Mask);

        public static ArchLegalityMask operator &(ArchLegalityMask a, ArchLegalityMask b)
            => new ArchLegalityMask(a.Mask & b.Mask);

        public static implicit operator SafetyMask128(ArchLegalityMask m) => m.Mask;
        public static explicit operator ArchLegalityMask(SafetyMask128 m) => new ArchLegalityMask(m);

        public bool Equals(ArchLegalityMask other) => Mask.Equals(other.Mask);
        public override bool Equals(object? obj) => obj is ArchLegalityMask m && Equals(m);
        public override int GetHashCode() => Mask.GetHashCode();
        public override string ToString() => $"ArchLegalityMask({Mask})";

        public static readonly ArchLegalityMask Zero = new ArchLegalityMask(SafetyMask128.Zero);
    }

    /// <summary>
    /// Backend hazard mask — encodes physical-register bank and execution-unit
    /// contention hazards at the microarchitecture level.
    ///
    /// Blueprint §7 / Checklist: "Развести ArchLegalityMask и BackendHazardMask."
    /// </summary>
    public readonly struct BackendHazardMask : IEquatable<BackendHazardMask>
    {
        public readonly SafetyMask128 Mask;

        public BackendHazardMask(SafetyMask128 mask) => Mask = mask;
        public BackendHazardMask(ulong low, ulong high = 0) => Mask = new SafetyMask128(low, high);

        public bool IsZero => Mask.IsZero;

        public bool ConflictsWith(BackendHazardMask other) => Mask.ConflictsWith(other.Mask);

        public static BackendHazardMask operator |(BackendHazardMask a, BackendHazardMask b)
            => new BackendHazardMask(a.Mask | b.Mask);

        public static BackendHazardMask operator &(BackendHazardMask a, BackendHazardMask b)
            => new BackendHazardMask(a.Mask & b.Mask);

        public static implicit operator SafetyMask128(BackendHazardMask m) => m.Mask;
        public static explicit operator BackendHazardMask(SafetyMask128 m) => new BackendHazardMask(m);

        public bool Equals(BackendHazardMask other) => Mask.Equals(other.Mask);
        public override bool Equals(object? obj) => obj is BackendHazardMask m && Equals(m);
        public override int GetHashCode() => Mask.GetHashCode();
        public override string ToString() => $"BackendHazardMask({Mask})";

        public static readonly BackendHazardMask Zero = new BackendHazardMask(SafetyMask128.Zero);
    }
}
