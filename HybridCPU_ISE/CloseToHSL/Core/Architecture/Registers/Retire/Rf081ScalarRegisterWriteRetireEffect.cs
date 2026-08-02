using System;

namespace YAKSys_Hybrid_CPU.Core.Registers.Retire;

/// <summary>
/// Immutable RF-08.1 carrier for exactly one scalar architectural-register write.
/// It binds the legacy payload to the already-frozen issued-attempt identity and
/// deliberately owns no retire selection, prevalidation, or publication operation.
/// </summary>
public sealed class ScalarRegisterWriteRetireEffect
{
    private ScalarRegisterWriteRetireEffect(RetireRecordIdentityProjection projection)
    {
        Projection = projection;
    }

    public RetireRecordIdentityProjection Projection { get; }
    public RetireRecord RetireRecord => Projection.RetireRecord;
    public RetireVisibleEffectIdentity Identity => Projection.Identity;
    public int VirtualThreadId => Identity.VirtualThreadId;
    public int ArchitecturalRegisterId => RetireRecord.ArchReg;
    public ulong Value => RetireRecord.Value;

    public static ScalarRegisterWriteRetireEffect Freeze(
        in RetireRecord retireRecord,
        RetireVisibleEffectIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (retireRecord.Kind != RetireRecordKind.RegisterWrite ||
            identity.EffectKind != RetireVisibleEffectKind.RegisterWrite)
        {
            throw new RetireEffectIdentityContractViolationException(
                "Scalar RegisterWrite effect requires matching RegisterWrite payload and identity kinds.");
        }

        RetireRecordIdentityProjection projection =
            RetireRecordIdentityProjection.Create(retireRecord, identity);
        return new ScalarRegisterWriteRetireEffect(projection);
    }
}
