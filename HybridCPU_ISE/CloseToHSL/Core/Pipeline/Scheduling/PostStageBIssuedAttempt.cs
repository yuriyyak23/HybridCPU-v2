using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Execution;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Immutable RF-08.3d companion prepared before nomination and consumed only by
/// the existing successful Stage-B commit arm. It has no scheduler authority.
/// </summary>
internal sealed class PostStageBIdentityTemplate
{
    internal PostStageBIdentityTemplate(
        AdmissionRecord admission,
        ulong workingBundleSequence,
        OperationAttemptIssuer attemptIssuer)
    {
        Admission = admission ?? throw new ArgumentNullException(nameof(admission));
        WorkingBundleSequence = workingBundleSequence;
        AttemptIssuer = attemptIssuer ?? throw new ArgumentNullException(nameof(attemptIssuer));
    }

    internal AdmissionRecord Admission { get; }
    internal ulong WorkingBundleSequence { get; }
    internal OperationAttemptIssuer AttemptIssuer { get; }
}

/// <summary>
/// Exact result of one successful existing Stage-B lane materialization. This
/// carrier owns neither execution decisions nor retirement/publication.
/// </summary>
public sealed class PostStageBIssuedAttempt
{
    private ScalarRegisterWriteRetireEffect? _scalarRegisterWriteEffect;

    private PostStageBIssuedAttempt(ScheduledOperation scheduledOperation)
    {
        ScheduledOperation = scheduledOperation;
        ExecutionRecord = YAKSys_Hybrid_CPU.Core.Execution.ExecutionRecord.Create(scheduledOperation);
        GeneratedBinding = scheduledOperation.Admission.ExecutionContract.GeneratedBinding;
    }

    public ScheduledOperation ScheduledOperation { get; }
    public ExecutionRecord ExecutionRecord { get; }
    public GeneratedStaticBinding GeneratedBinding { get; }
    public ScalarRegisterWriteRetireEffect? ScalarRegisterWriteEffect => _scalarRegisterWriteEffect;

    internal static PostStageBIssuedAttempt CreateAfterSuccessfulStageB(
        PostStageBIdentityTemplate template,
        int physicalLane)
    {
        ArgumentNullException.ThrowIfNull(template);
        ScheduledOperation scheduledOperation = ScheduledOperation.CreateAfterStageB(
            template.Admission,
            template.WorkingBundleSequence,
            physicalLane,
            physicalLane,
            template.AttemptIssuer);
        return new PostStageBIssuedAttempt(scheduledOperation);
    }

    internal void CompleteScalarRegisterWrite(in RetireRecord retireRecord)
    {
        if (retireRecord.Kind != RetireRecordKind.RegisterWrite ||
            retireRecord.VtId != ScheduledOperation.OperationId.VirtualThreadId)
        {
            throw new RetireEffectIdentityContractViolationException(
                "Issued scalar attempt does not match the live RegisterWrite retire record.");
        }

        if (ExecutionRecord.State == ExecutionRecordState.Issued)
        {
            int effectCount = retireRecord.ArchReg == 0 ? 0 : 1;
            ExecutionResultContract result = ExecutionResultContract.Scalar(retireRecord.Value, effectCount);
            ExecutionRecord.ApplyTerminalTransition(
                ExecutionRecord.CreateTerminalTransition(ExecutionOutcome.Completed(result)));
        }

        if (retireRecord.ArchReg == 0)
            return;

        if (_scalarRegisterWriteEffect is not null)
        {
            throw new RetireEffectIdentityContractViolationException(
                "Issued scalar attempt produced more than one RegisterWrite retire effect.");
        }

        RetireVisibleEffectIdentity identity = RetireVisibleEffectIdentity.Freeze(
            ExecutionRecord,
            RetireVisibleEffectKind.RegisterWrite,
            effectOrdinal: 0,
            retireRecord.VtId,
            retireRecord.ArchReg);
        _scalarRegisterWriteEffect = ScalarRegisterWriteRetireEffect.Freeze(retireRecord, identity);
    }
}
