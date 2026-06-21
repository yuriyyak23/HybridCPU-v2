using System;

namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class CompletionRecord
{
    public bool IsCompatibilityProjectionSource =>
        RecordClass == CompletionRecordClass.CompatibilityExit;

    public static CompletionRecord FromCompatibilityExit(
        TrapCompletionPublicationFenceResult publicationFence,
        VmExitReason reason,
        VmxExitQualification qualification,
        ulong faultAddress = 0,
        ulong faultAux = 0)
    {
        if (!TryFromCompatibilityExit(
                publicationFence,
                reason,
                qualification,
                out CompletionRecord record,
                faultAddress,
                faultAux))
        {
            throw new InvalidOperationException(
                "Compatibility exit completion publication was denied by the neutral trap publication fence.");
        }

        return record;
    }

    public static bool TryFromCompatibilityExit(
        TrapCompletionPublicationFenceResult publicationFence,
        VmExitReason reason,
        VmxExitQualification qualification,
        out CompletionRecord record,
        ulong faultAddress = 0,
        ulong faultAux = 0)
    {
        if (!publicationFence.CompletionPublicationAllowed)
        {
            record = None;
            return false;
        }

        record = new CompletionRecord(
            CompletionRecordClass.CompatibilityExit,
            (uint)reason,
            qualification.Encode(),
            faultAddress,
            faultAux);
        return true;
    }
}
