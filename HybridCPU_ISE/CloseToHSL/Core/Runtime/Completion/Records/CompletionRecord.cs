namespace YAKSys_Hybrid_CPU.Core;

public enum CompletionRecordClass : byte
{
    None = 0,
    Trap = 1,
    Event = 2,
    MemoryTranslationFault = 3,
    DmaFault = 4,
    VectorStreamFault = 5,
    LaneFault = 6,
    SecurityViolation = 7,
    CompatibilityExit = 8,
}

public sealed partial class CompletionRecord
{
    public CompletionRecord()
        : this(
            CompletionRecordClass.None,
            reasonCode: 0,
            qualification: 0,
            faultAddress: 0,
            faultAux: 0)
    {
    }

    public CompletionRecord(
        CompletionRecordClass recordClass,
        uint reasonCode,
        ulong qualification,
        ulong faultAddress,
        ulong faultAux)
    {
        RecordClass = recordClass;
        ReasonCode = reasonCode;
        Qualification = qualification;
        FaultAddress = faultAddress;
        FaultAux = faultAux;
    }

    public static CompletionRecord None { get; } = new();

    public CompletionRecordClass RecordClass { get; }

    public uint ReasonCode { get; }

    public ulong Qualification { get; }

    public ulong FaultAddress { get; }

    public ulong FaultAux { get; }

    public bool IsEmpty =>
        RecordClass == CompletionRecordClass.None &&
        ReasonCode == 0 &&
        Qualification == 0 &&
        FaultAddress == 0 &&
        FaultAux == 0;

}
