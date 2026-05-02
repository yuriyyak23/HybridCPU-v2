using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Auth;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Commit;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Conflicts;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Memory;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Fences;

public enum AcceleratorFenceActiveTokenDisposition : byte
{
    Reject = 0,
    Cancel = 1,
    Fault = 2
}

public sealed class AcceleratorFenceScope
{
    private AcceleratorFenceScope(
        IReadOnlyList<AcceleratorTokenHandle> tokenHandles,
        bool commitCompletedTokens,
        bool commitConflictPlaceholderAccepted,
        AcceleratorFenceActiveTokenDisposition activeTokenDisposition,
        AcceleratorCancelPolicy activeCancelPolicy)
    {
        TokenHandles = FreezeHandles(tokenHandles);
        CommitCompletedTokens = commitCompletedTokens;
        CommitConflictPlaceholderAccepted = commitConflictPlaceholderAccepted;
        ActiveTokenDisposition = activeTokenDisposition;
        ActiveCancelPolicy = activeCancelPolicy;
    }

    public IReadOnlyList<AcceleratorTokenHandle> TokenHandles { get; }

    public bool CommitCompletedTokens { get; }

    public bool CommitConflictPlaceholderAccepted { get; }

    public AcceleratorFenceActiveTokenDisposition ActiveTokenDisposition { get; }

    public AcceleratorCancelPolicy ActiveCancelPolicy { get; }

    public static AcceleratorFenceScope ForToken(
        AcceleratorTokenHandle tokenHandle,
        bool commitCompletedTokens = false,
        bool commitConflictPlaceholderAccepted = true,
        AcceleratorFenceActiveTokenDisposition activeTokenDisposition =
            AcceleratorFenceActiveTokenDisposition.Reject,
        AcceleratorCancelPolicy? activeCancelPolicy = null) =>
        ForTokens(
            new[] { tokenHandle },
            commitCompletedTokens,
            commitConflictPlaceholderAccepted,
            activeTokenDisposition,
            activeCancelPolicy);

    public static AcceleratorFenceScope ForTokens(
        IReadOnlyList<AcceleratorTokenHandle> tokenHandles,
        bool commitCompletedTokens = false,
        bool commitConflictPlaceholderAccepted = true,
        AcceleratorFenceActiveTokenDisposition activeTokenDisposition =
            AcceleratorFenceActiveTokenDisposition.Reject,
        AcceleratorCancelPolicy? activeCancelPolicy = null) =>
        new(
            tokenHandles ?? Array.Empty<AcceleratorTokenHandle>(),
            commitCompletedTokens,
            commitConflictPlaceholderAccepted,
            activeTokenDisposition,
            activeCancelPolicy ?? AcceleratorCancelPolicy.Cooperative);

    private static IReadOnlyList<AcceleratorTokenHandle> FreezeHandles(
        IReadOnlyList<AcceleratorTokenHandle> tokenHandles)
    {
        if (tokenHandles is null || tokenHandles.Count == 0)
        {
            return Array.Empty<AcceleratorTokenHandle>();
        }

        var copy = new AcceleratorTokenHandle[tokenHandles.Count];
        for (int index = 0; index < tokenHandles.Count; index++)
        {
            copy[index] = tokenHandles[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed record AcceleratorFenceResult
{
    private AcceleratorFenceResult(
        IReadOnlyList<AcceleratorTokenLookupResult> tokenResults,
        IReadOnlyList<AcceleratorCommitResult> commitResults,
        string message)
    {
        TokenResults = FreezeTokenResults(tokenResults);
        CommitResults = FreezeCommitResults(commitResults);
        Message = message;
    }

    public IReadOnlyList<AcceleratorTokenLookupResult> TokenResults { get; }

    public IReadOnlyList<AcceleratorCommitResult> CommitResults { get; }

    public string Message { get; }

    public int RejectedCount => CountRejected(TokenResults) + CountRejected(CommitResults);

    public int CommittedCount => CountCommitted(CommitResults);

    public int CanceledCount => CountState(AcceleratorTokenState.Canceled);

    public int FaultedCount => CountState(AcceleratorTokenState.Faulted);

    public int ObservedCount => TokenResults.Count;

    public bool Succeeded => RejectedCount == 0;

    public bool IsRejected => !Succeeded;

    public bool CanPublishArchitecturalMemory => CommittedCount != 0;

    public bool UserVisiblePublicationAllowed => CommittedCount != 0;

    public AcceleratorTokenFaultCode FaultCode
    {
        get
        {
            for (int index = 0; index < TokenResults.Count; index++)
            {
                if (TokenResults[index].IsRejected)
                {
                    return TokenResults[index].FaultCode;
                }
            }

            for (int index = 0; index < CommitResults.Count; index++)
            {
                if (CommitResults[index].IsRejected)
                {
                    return CommitResults[index].FaultCode;
                }
            }

            return AcceleratorTokenFaultCode.None;
        }
    }

    public ulong PackedFenceStatus
    {
        get
        {
            ulong packed = (ulong)(byte)(Succeeded ? 1 : 0);
            packed |= (ulong)(byte)FaultCode << 8;
            packed |= (ulong)(ushort)Math.Min(ObservedCount, ushort.MaxValue) << 16;
            packed |= (ulong)(ushort)Math.Min(CommittedCount, ushort.MaxValue) << 32;
            packed |= (ulong)(ushort)Math.Min(RejectedCount, ushort.MaxValue) << 48;
            return packed;
        }
    }

    public static AcceleratorFenceResult Complete(
        IReadOnlyList<AcceleratorTokenLookupResult> tokenResults,
        IReadOnlyList<AcceleratorCommitResult> commitResults,
        string message) =>
        new(tokenResults, commitResults, message);

    private int CountState(AcceleratorTokenState state)
    {
        int count = 0;
        for (int index = 0; index < TokenResults.Count; index++)
        {
            if (TokenResults[index].StatusWord.State == state)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRejected(
        IReadOnlyList<AcceleratorTokenLookupResult> results)
    {
        int count = 0;
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].IsRejected)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRejected(
        IReadOnlyList<AcceleratorCommitResult> results)
    {
        int count = 0;
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].IsRejected)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountCommitted(
        IReadOnlyList<AcceleratorCommitResult> results)
    {
        int count = 0;
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<AcceleratorTokenLookupResult> FreezeTokenResults(
        IReadOnlyList<AcceleratorTokenLookupResult> results)
    {
        if (results is null || results.Count == 0)
        {
            return Array.Empty<AcceleratorTokenLookupResult>();
        }

        var copy = new AcceleratorTokenLookupResult[results.Count];
        for (int index = 0; index < results.Count; index++)
        {
            copy[index] = results[index];
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<AcceleratorCommitResult> FreezeCommitResults(
        IReadOnlyList<AcceleratorCommitResult> results)
    {
        if (results is null || results.Count == 0)
        {
            return Array.Empty<AcceleratorCommitResult>();
        }

        var copy = new AcceleratorCommitResult[results.Count];
        for (int index = 0; index < results.Count; index++)
        {
            copy[index] = results[index];
        }

        return Array.AsReadOnly(copy);
    }
}

public sealed class AcceleratorFenceCoordinator
{
    // Model-side fence coordinator. It is not executable ACCEL_FENCE; the
    // lane7 carrier remains fail-closed until a separate architecture decision.
    public AcceleratorFenceResult TryFence(
        AcceleratorTokenStore tokenStore,
        AcceleratorFenceScope scope,
        AcceleratorGuardEvidence? currentGuardEvidence,
        IAcceleratorStagingBuffer? stagingBuffer = null,
        Processor.MainMemoryArea? mainMemory = null,
        AcceleratorCommitCoordinator? commitCoordinator = null,
        AcceleratorCommitInvalidationPlan? invalidationPlan = null,
        ExternalAcceleratorConflictManager? conflictManager = null)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(scope);

        var tokenResults = new List<AcceleratorTokenLookupResult>();
        var commitResults = new List<AcceleratorCommitResult>();
        commitCoordinator ??= new AcceleratorCommitCoordinator();
        invalidationPlan ??= AcceleratorCommitInvalidationPlan.None;

        for (int index = 0; index < scope.TokenHandles.Count; index++)
        {
            AcceleratorTokenHandle handle = scope.TokenHandles[index];
            AcceleratorTokenLookupResult lookup =
                tokenStore.TryLookup(
                    handle,
                    currentGuardEvidence,
                    AcceleratorTokenLookupIntent.Fence);
            if (!lookup.IsAllowed || lookup.Token is null)
            {
                tokenResults.Add(lookup);
                continue;
            }

            AcceleratorToken token = lookup.Token;
            if (token.State is AcceleratorTokenState.DeviceComplete
                or AcceleratorTokenState.CommitPending)
            {
                if (scope.CommitCompletedTokens)
                {
                    if (stagingBuffer is null || mainMemory is null)
                    {
                        tokenResults.Add(
                            AcceleratorTokenLookupResult.Rejected(
                                AcceleratorTokenLookupIntent.Fence,
                                token.State,
                                AcceleratorTokenFaultCode.CommitCoordinatorRequired,
                                "ACCEL_FENCE requested commit of a completed token but no Phase 08 commit surfaces were supplied.",
                                token,
                                lookup.GuardDecision));
                        continue;
                    }

                    AcceleratorCommitResult commit =
                        commitCoordinator.TryCommit(
                            token,
                            token.Descriptor,
                            stagingBuffer,
                            mainMemory,
                            currentGuardEvidence,
                            invalidationPlan,
                            scope.CommitConflictPlaceholderAccepted,
                            conflictManager: conflictManager);
                    commitResults.Add(commit);
                    tokenResults.Add(
                        commit.Succeeded
                            ? AcceleratorTokenLookupResult.Allowed(
                                AcceleratorTokenLookupIntent.Fence,
                                token,
                                lookup.GuardDecision!.Value,
                                "ACCEL_FENCE committed a completed token through the Phase 08 coordinator after fresh guard validation.")
                            : AcceleratorTokenLookupResult.Rejected(
                                AcceleratorTokenLookupIntent.Fence,
                                token.State,
                                commit.FaultCode == AcceleratorTokenFaultCode.None
                                    ? AcceleratorTokenFaultCode.FenceRejected
                                    : commit.FaultCode,
                                commit.Message,
                                token,
                                lookup.GuardDecision));
                    continue;
                }

                tokenResults.Add(
                    AcceleratorTokenLookupResult.Allowed(
                        AcceleratorTokenLookupIntent.Fence,
                        token,
                        lookup.GuardDecision!.Value,
                        "ACCEL_FENCE observed a completed token without commit publication by scope policy."));
                continue;
            }

            if (token.State is AcceleratorTokenState.Created
                or AcceleratorTokenState.Validated
                or AcceleratorTokenState.Queued
                or AcceleratorTokenState.Running)
            {
                tokenResults.Add(
                    ResolveActiveToken(
                        tokenStore,
                        token,
                        handle,
                        lookup,
                        scope,
                        currentGuardEvidence,
                        conflictManager));
                continue;
            }

            tokenResults.Add(
                AcceleratorTokenLookupResult.Allowed(
                    AcceleratorTokenLookupIntent.Fence,
                    token,
                    lookup.GuardDecision!.Value,
                    "ACCEL_FENCE observed a terminal scoped token after fresh guard validation."));
        }

        string message = CountRejected(tokenResults) + CountRejected(commitResults) == 0
            ? "ACCEL_FENCE serialized the scoped token set; any architectural memory publication used only the Phase 08 commit coordinator."
            : "ACCEL_FENCE rejected at least one scoped token conservatively; no rejected token gained commit authority.";
        return AcceleratorFenceResult.Complete(
            tokenResults,
            commitResults,
            message);
    }

    private static AcceleratorTokenLookupResult ResolveActiveToken(
        AcceleratorTokenStore tokenStore,
        AcceleratorToken token,
        AcceleratorTokenHandle handle,
        AcceleratorTokenLookupResult lookup,
        AcceleratorFenceScope scope,
        AcceleratorGuardEvidence? currentGuardEvidence,
        ExternalAcceleratorConflictManager? conflictManager)
    {
        if (scope.ActiveTokenDisposition == AcceleratorFenceActiveTokenDisposition.Reject)
        {
            if (conflictManager is not null)
            {
                AcceleratorConflictDecision boundary =
                    conflictManager.NotifySerializingBoundary(
                        token,
                        currentGuardEvidence);
                return AcceleratorTokenLookupResult.Rejected(
                    AcceleratorTokenLookupIntent.Fence,
                    token.State,
                    boundary.TokenFaultCode == AcceleratorTokenFaultCode.None
                        ? AcceleratorTokenFaultCode.FenceRejected
                        : boundary.TokenFaultCode,
                    boundary.Message,
                    token,
                    boundary.GuardDecision ?? lookup.GuardDecision);
            }

            return AcceleratorTokenLookupResult.Rejected(
                AcceleratorTokenLookupIntent.Fence,
                token.State,
                AcceleratorTokenFaultCode.FenceRejected,
                "ACCEL_FENCE conservatively rejected an active scoped token; Phase 10 overlap tracking is not implemented.",
                token,
                lookup.GuardDecision);
        }

        if (scope.ActiveTokenDisposition == AcceleratorFenceActiveTokenDisposition.Cancel)
        {
            AcceleratorTokenLookupResult cancel = tokenStore.TryCancel(
                handle,
                currentGuardEvidence,
                scope.ActiveCancelPolicy);
            if (cancel.IsAllowed && token.IsTerminal)
            {
                conflictManager?.ReleaseTokenFootprint(
                    token,
                    currentGuardEvidence);
            }

            return cancel;
        }

        AcceleratorTokenTransition fault =
            token.MarkFaulted(
                AcceleratorTokenFaultCode.FenceRejected,
                currentGuardEvidence!);
        if (fault.Succeeded)
        {
            conflictManager?.ReleaseTokenFootprint(
                token,
                currentGuardEvidence);
        }

        return fault.Succeeded
            ? AcceleratorTokenLookupResult.AllowedWithStatus(
                AcceleratorTokenLookupIntent.Fence,
                token,
                lookup.GuardDecision!.Value,
                AcceleratorTokenStatusWord.FromToken(token),
                AcceleratorTokenFaultCode.FenceRejected,
                sideEffectsAllowed: true,
                "ACCEL_FENCE faulted an active scoped token by explicit guarded policy; no commit authority was granted.")
            : AcceleratorTokenLookupResult.Rejected(
                AcceleratorTokenLookupIntent.Fence,
                token.State,
                fault.FaultCode,
                fault.Message,
                token,
                lookup.GuardDecision);
    }

    private static int CountRejected(
        IReadOnlyList<AcceleratorTokenLookupResult> results)
    {
        int count = 0;
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].IsRejected)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRejected(
        IReadOnlyList<AcceleratorCommitResult> results)
    {
        int count = 0;
        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].IsRejected)
            {
                count++;
            }
        }

        return count;
    }
}
