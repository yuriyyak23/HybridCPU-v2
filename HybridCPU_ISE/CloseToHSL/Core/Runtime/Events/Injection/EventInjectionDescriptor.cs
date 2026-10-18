namespace YAKSys_Hybrid_CPU.Core
{
    public enum EventInjectionKind : byte
    {
        None = 0,
        ExternalInterrupt = 1,
        Exception = 2,
        Nmi = 3,
        VirtualTimer = 4,
        PostedCompletion = 5,
    }

    public readonly record struct EventInjectionDescriptor(
        EventInjectionKind Kind,
        byte Vector,
        byte Priority,
        byte TargetVtId,
        ushort ExecutionDomainTag,
        ushort AddressSpaceTag,
        ulong Payload,
        ulong Sequence,
        bool Posted)
    {
        public bool IsValid => Kind != EventInjectionKind.None;

        public bool Matches(byte vtId, ushort executionDomainTag) =>
            IsValid && TargetVtId == vtId && ExecutionDomainTag == executionDomainTag;

        public bool CoalescesWith(EventInjectionDescriptor other) =>
            IsValid &&
            other.IsValid &&
            Posted &&
            other.Posted &&
            Kind == other.Kind &&
            Vector == other.Vector &&
            Priority == other.Priority &&
            TargetVtId == other.TargetVtId &&
            ExecutionDomainTag == other.ExecutionDomainTag &&
            AddressSpaceTag == other.AddressSpaceTag &&
            Payload == other.Payload;

        public static EventInjectionDescriptor Create(
            EventInjectionKind kind,
            byte vector,
            byte targetVtId,
            ushort executionDomainTag,
            ushort addressSpaceTag = 0,
            byte priority = 0,
            ulong payload = 0,
            bool posted = false) =>
            new(kind, vector, priority, targetVtId, executionDomainTag, addressSpaceTag, payload, 0, posted);

        internal EventInjectionDescriptor WithSequence(ulong sequence) =>
            this with { Sequence = sequence };
    }
}
