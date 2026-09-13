using System;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Memory;

namespace YAKSys_Hybrid_CPU.Core.Execution;

/// <summary>
/// The single RF-07 terminal outcome family for one issued execution attempt.
/// These values neither select retirement nor publish architectural effects.
/// </summary>
public enum ExecutionOutcomeKind : byte
{
    Completed = 1,
    ArchitecturalFault = 2,
    Retryable = 3,
    StructuralBlocked = 4,
    BackendUnavailable = 5,
    FatalInvariantViolation = 6,
}

/// <summary>
/// Typed diagnostic codes carried by non-completed outcomes.  A code has one
/// fixed disposition and therefore cannot be reclassified as retry by a caller.
/// </summary>
public enum ExecutionDiagnosticCode : byte
{
    PageFault = 1,
    AlignmentFault = 2,
    ResourceWait = 3,
    StructuralHazard = 4,
    RuntimeBackendUnavailable = 5,
    ExistingExecutionFault = 6,
    UnknownException = 7,
    OutcomeContractViolation = 8,
    SpeculativeFaultSuppressed = 9,
}

/// <summary>
/// Immutable diagnostic payload. Exception objects are deliberately not retained;
/// only stable type/category/message evidence crosses this contract.
/// </summary>
public sealed class ExecutionDiagnostic
{
    private ExecutionDiagnostic(
        ExecutionOutcomeKind disposition,
        ExecutionDiagnosticCode code,
        string reason,
        ulong? faultAddress,
        bool? faultIsWrite,
        ExecutionFaultCategory? legacyFaultCategory,
        string? exceptionType)
    {
        Disposition = disposition;
        Code = code;
        Reason = RequireReason(reason);
        FaultAddress = faultAddress;
        FaultIsWrite = faultIsWrite;
        LegacyFaultCategory = legacyFaultCategory;
        ExceptionType = exceptionType;
    }

    public ExecutionOutcomeKind Disposition { get; }
    public ExecutionDiagnosticCode Code { get; }
    public string Reason { get; }
    public ulong? FaultAddress { get; }
    public bool? FaultIsWrite { get; }
    public ExecutionFaultCategory? LegacyFaultCategory { get; }
    public string? ExceptionType { get; }

    public static ExecutionDiagnostic PageFault(PageFaultException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ExecutionDiagnostic(
            ExecutionOutcomeKind.ArchitecturalFault,
            ExecutionDiagnosticCode.PageFault,
            exception.Message,
            exception.FaultAddress,
            exception.IsWrite,
            legacyFaultCategory: null,
            exception.GetType().FullName);
    }

    public static ExecutionDiagnostic AlignmentFault(
        MemoryAlignmentException exception,
        bool isWrite)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new ExecutionDiagnostic(
            ExecutionOutcomeKind.ArchitecturalFault,
            ExecutionDiagnosticCode.AlignmentFault,
            exception.Message,
            exception.Address,
            isWrite,
            legacyFaultCategory: null,
            exception.GetType().FullName);
    }

    public static ExecutionDiagnostic Retryable(string reason) =>
        new(
            ExecutionOutcomeKind.Retryable,
            ExecutionDiagnosticCode.ResourceWait,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            legacyFaultCategory: null,
            exceptionType: null);

    public static ExecutionDiagnostic StructuralBlocked(string reason) =>
        new(
            ExecutionOutcomeKind.StructuralBlocked,
            ExecutionDiagnosticCode.StructuralHazard,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            legacyFaultCategory: null,
            exceptionType: null);

    public static ExecutionDiagnostic SpeculativeFaultSuppressed(string reason) =>
        new(
            ExecutionOutcomeKind.StructuralBlocked,
            ExecutionDiagnosticCode.SpeculativeFaultSuppressed,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            legacyFaultCategory: null,
            exceptionType: null);

    public static ExecutionDiagnostic BackendUnavailable(string reason) =>
        new(
            ExecutionOutcomeKind.BackendUnavailable,
            ExecutionDiagnosticCode.RuntimeBackendUnavailable,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            legacyFaultCategory: null,
            exceptionType: null);

    public static ExecutionDiagnostic Fatal(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        string reason = string.IsNullOrWhiteSpace(exception.Message)
            ? $"Unhandled exception of type {exceptionType}."
            : exception.Message;
        bool hasLegacyCategory = ExecutionFaultContract.TryGetCategory(
            exception,
            out ExecutionFaultCategory legacyCategory);
        return new ExecutionDiagnostic(
            ExecutionOutcomeKind.FatalInvariantViolation,
            hasLegacyCategory
                ? ExecutionDiagnosticCode.ExistingExecutionFault
                : ExecutionDiagnosticCode.UnknownException,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            hasLegacyCategory ? legacyCategory : null,
            exceptionType);
    }

    internal static ExecutionDiagnostic ContractViolation(string reason) =>
        new(
            ExecutionOutcomeKind.FatalInvariantViolation,
            ExecutionDiagnosticCode.OutcomeContractViolation,
            reason,
            faultAddress: null,
            faultIsWrite: null,
            ExecutionFaultCategory.InvalidInternalOp,
            typeof(ExecutionOutcomeContractViolationException).FullName);

    private static string RequireReason(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return reason.Trim();
    }
}

/// <summary>
/// Immutable result contract required even when a completed instruction has no
/// scalar result. Architectural-effect count is evidence only: this object does
/// not own retire payloads or publication authority.
/// </summary>
public sealed class ExecutionResultContract
{
    private ExecutionResultContract(
        bool hasScalarResult,
        ulong scalarResult,
        int architecturalEffectCount)
    {
        if (architecturalEffectCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(architecturalEffectCount));
        }

        HasScalarResult = hasScalarResult;
        ScalarResult = scalarResult;
        ArchitecturalEffectCount = architecturalEffectCount;
    }

    public bool HasScalarResult { get; }
    public ulong ScalarResult { get; }
    public int ArchitecturalEffectCount { get; }
    public bool HasArchitecturalEffects => ArchitecturalEffectCount != 0;

    public static ExecutionResultContract WithoutScalarResult(
        int architecturalEffectCount = 0) =>
        new(false, 0, architecturalEffectCount);

    public static ExecutionResultContract Scalar(
        ulong value,
        int architecturalEffectCount = 0) =>
        new(true, value, architecturalEffectCount);
}

/// <summary>
/// Immutable, internally coherent terminal outcome payload.
/// </summary>
public sealed class ExecutionOutcome
{
    private ExecutionOutcome(
        ExecutionOutcomeKind kind,
        ExecutionResultContract? result,
        ExecutionDiagnostic? diagnostic)
    {
        Kind = kind;
        Result = result;
        Diagnostic = diagnostic;
    }

    public ExecutionOutcomeKind Kind { get; }
    public ExecutionResultContract? Result { get; }
    public ExecutionDiagnostic? Diagnostic { get; }
    public bool HasArchitecturalEffects => Result?.HasArchitecturalEffects == true;

    public static ExecutionOutcome Create(
        ExecutionOutcomeKind kind,
        ExecutionResultContract? result = null,
        ExecutionDiagnostic? diagnostic = null)
    {
        if (!Enum.IsDefined(typeof(ExecutionOutcomeKind), kind))
        {
            throw ContractViolation($"Unknown execution outcome kind value {(byte)kind}.");
        }

        if (kind == ExecutionOutcomeKind.Completed)
        {
            if (result is null)
            {
                throw ContractViolation("Completed requires an explicit ExecutionResultContract.");
            }

            if (diagnostic is not null)
            {
                throw ContractViolation("Completed cannot carry a fault/block/retry diagnostic.");
            }

            return new ExecutionOutcome(kind, result, diagnostic: null);
        }

        if (result is not null)
        {
            string effectDetail = result.HasArchitecturalEffects
                ? " with architectural effects"
                : string.Empty;
            throw ContractViolation($"{kind} cannot carry a result contract{effectDetail}.");
        }

        if (diagnostic is null)
        {
            throw ContractViolation($"{kind} requires a typed ExecutionDiagnostic.");
        }

        if (diagnostic.Disposition != kind)
        {
            throw ContractViolation(
                $"Diagnostic {diagnostic.Code} has disposition {diagnostic.Disposition} and cannot be used as {kind}.");
        }

        return new ExecutionOutcome(kind, result: null, diagnostic);
    }

    public static ExecutionOutcome Completed(ExecutionResultContract result) =>
        Create(ExecutionOutcomeKind.Completed, result);

    public static ExecutionOutcome ArchitecturalFault(ExecutionDiagnostic diagnostic) =>
        Create(ExecutionOutcomeKind.ArchitecturalFault, diagnostic: diagnostic);

    public static ExecutionOutcome Retryable(ExecutionDiagnostic diagnostic) =>
        Create(ExecutionOutcomeKind.Retryable, diagnostic: diagnostic);

    public static ExecutionOutcome StructuralBlocked(ExecutionDiagnostic diagnostic) =>
        Create(ExecutionOutcomeKind.StructuralBlocked, diagnostic: diagnostic);

    public static ExecutionOutcome BackendUnavailable(ExecutionDiagnostic diagnostic) =>
        Create(ExecutionOutcomeKind.BackendUnavailable, diagnostic: diagnostic);

    public static ExecutionOutcome FatalInvariantViolation(ExecutionDiagnostic diagnostic) =>
        Create(ExecutionOutcomeKind.FatalInvariantViolation, diagnostic: diagnostic);

    private static ExecutionOutcomeContractViolationException ContractViolation(string message) =>
        new(message);
}

/// <summary>
/// Immutable terminal-transition command. It repeats identity only so an
/// ExecutionRecord can fail closed on cross-attempt or reconstructed-binding use.
/// </summary>
public sealed class ExecutionTransition
{
    public ExecutionTransition(
        VliwOperationId operationId,
        GeneratedStaticBinding generatedBinding,
        ExecutionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(generatedBinding);
        ArgumentNullException.ThrowIfNull(outcome);
        OperationId = operationId;
        GeneratedBinding = generatedBinding;
        Outcome = outcome;
    }

    public VliwOperationId OperationId { get; }
    public GeneratedStaticBinding GeneratedBinding { get; }
    public ExecutionOutcome Outcome { get; }
}

public enum ExecutionRecordState : byte
{
    Issued = 1,
    Terminal = 2,
}

/// <summary>
/// One issued-attempt execution state owner. This is not a ROB entry and owns no
/// rename allocation, physical-register lifetime, commit-map/free-list action,
/// ordering, retirement selection, checkpoint, squash, or recovery authority.
/// </summary>
public sealed class ExecutionRecord
{
    private readonly object _transitionGate = new();
    private ExecutionRecordState _state;
    private ExecutionOutcome? _outcome;

    private ExecutionRecord(ScheduledOperation scheduledOperation)
    {
        ScheduledOperation = scheduledOperation;
        OperationId = scheduledOperation.OperationId;
        GeneratedBinding = scheduledOperation.Admission.ExecutionContract.GeneratedBinding;
        _state = ExecutionRecordState.Issued;
    }

    public ScheduledOperation ScheduledOperation { get; }
    public VliwOperationId OperationId { get; }
    public GeneratedStaticBinding GeneratedBinding { get; }

    public ExecutionRecordState State
    {
        get
        {
            lock (_transitionGate)
            {
                return _state;
            }
        }
    }

    public ExecutionOutcome? Outcome
    {
        get
        {
            lock (_transitionGate)
            {
                return _outcome;
            }
        }
    }

    public static ExecutionRecord Create(ScheduledOperation scheduledOperation)
    {
        ArgumentNullException.ThrowIfNull(scheduledOperation);
        return new ExecutionRecord(scheduledOperation);
    }

    public ExecutionTransition CreateTerminalTransition(ExecutionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new ExecutionTransition(OperationId, GeneratedBinding, outcome);
    }

    public void ApplyTerminalTransition(ExecutionTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        lock (_transitionGate)
        {
            if (_state == ExecutionRecordState.Terminal)
            {
                throw new ExecutionOutcomeContractViolationException(
                    "ExecutionRecord already has a terminal outcome; duplicate or post-terminal transition rejected.");
            }

            if (transition.OperationId != OperationId)
            {
                throw new ExecutionOutcomeContractViolationException(
                    "Execution transition operation identity does not match the issued attempt.");
            }

            if (!ReferenceEquals(transition.GeneratedBinding, GeneratedBinding))
            {
                throw new ExecutionOutcomeContractViolationException(
                    "Execution transition does not carry the exact frozen GeneratedStaticBinding instance from ScheduledOperation.");
            }

            _outcome = transition.Outcome;
            _state = ExecutionRecordState.Terminal;
        }
    }
}

/// <summary>
/// Additive RF-07.0 differential projection over legacy observations. Production
/// execute routing does not call this surface in RF-07.0.
/// </summary>
public static class Rf07LegacyOutcomeProjection
{
    public static ExecutionOutcome ProjectSuccessfulExecution(
        bool legacySuccess,
        ExecutionResultContract completedResult)
    {
        ArgumentNullException.ThrowIfNull(completedResult);
        if (!legacySuccess)
        {
            throw new ExecutionOutcomeContractViolationException(
                "A false legacy observation requires contour-specific classification; it cannot be projected as generic success/retry.");
        }

        return ExecutionOutcome.Completed(completedResult);
    }

    public static ExecutionOutcome ProjectKnownRetry(
        bool legacySuccess,
        string retryReason)
    {
        if (legacySuccess)
        {
            throw new ExecutionOutcomeContractViolationException(
                "A completed legacy observation cannot be projected as Retryable.");
        }

        return ExecutionOutcome.Retryable(ExecutionDiagnostic.Retryable(retryReason));
    }

    public static ExecutionOutcome ProjectKnownBackendUnavailable(
        bool legacySuccess,
        string denialReason)
    {
        if (legacySuccess)
        {
            throw new ExecutionOutcomeContractViolationException(
                "A completed legacy observation cannot be projected as BackendUnavailable.");
        }

        return ExecutionOutcome.BackendUnavailable(
            ExecutionDiagnostic.BackendUnavailable(denialReason));
    }

    public static ExecutionOutcome ProjectException(
        Exception exception,
        bool alignmentIsWrite = true)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            PageFaultException pageFault =>
                ExecutionOutcome.ArchitecturalFault(ExecutionDiagnostic.PageFault(pageFault)),
            MemoryAlignmentException alignmentFault =>
                ExecutionOutcome.ArchitecturalFault(
                    ExecutionDiagnostic.AlignmentFault(alignmentFault, alignmentIsWrite)),
            _ => ExecutionOutcome.FatalInvariantViolation(ExecutionDiagnostic.Fatal(exception)),
        };
    }
}

/// <summary>
/// Fail-closed programming/invariant error raised when an outcome or transition
/// violates the frozen RF-07 contract. It is not an architectural instruction fault.
/// </summary>
public sealed class ExecutionOutcomeContractViolationException : InvalidOperationException
{
    public ExecutionOutcomeContractViolationException(string message)
        : base(ExecutionFaultContract.FormatMessage(
            ExecutionFaultCategory.InvalidInternalOp,
            message))
    {
        ExecutionFaultContract.Stamp(this, ExecutionFaultCategory.InvalidInternalOp);
    }
}
