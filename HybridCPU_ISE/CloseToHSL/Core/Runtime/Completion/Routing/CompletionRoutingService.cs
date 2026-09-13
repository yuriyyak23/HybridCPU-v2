namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct CompletionRoutingAdmissionResult(
    DomainValidationResult Validation,
    LaneCompletionRoutingResult Routing)
{
    public bool IsRouted =>
        Validation.IsValid &&
        Routing.Routed;

    public static CompletionRoutingAdmissionResult Fail(
        DomainValidationFailureReason reason,
        LaneCompletionDescriptor completion,
        LaneCompletionRouteStatus status,
        string message) =>
        new(
            DomainValidationResult.Fail(reason, message),
            LaneCompletionRoutingResult.Create(
                status,
                completion,
                default,
                InterruptPostDisposition.None,
                routingEpoch: 0,
                message));
}

public sealed partial class CompletionRoutingService
{
    public CompletionRoutingAdmissionResult RoutePostedCompletion(
        CompletionRouteDescriptor? descriptor,
        LaneCompletionRouter router,
        LaneCompletionDescriptor completion)
    {
        if (descriptor is null)
        {
            return CompletionRoutingAdmissionResult.Fail(
                DomainValidationFailureReason.MissingCompletionRouteDescriptor,
                completion,
                LaneCompletionRouteStatus.DroppedInvalid,
                "Completion routing requires a runtime-owned completion route descriptor.");
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            return CompletionRoutingAdmissionResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                completion,
                LaneCompletionRouteStatus.DroppedInvalid,
                "Compatibility projection cannot own completion routing.");
        }

        if (!completion.IsValid)
        {
            return CompletionRoutingAdmissionResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                completion,
                LaneCompletionRouteStatus.DroppedInvalid,
                "Completion routing rejected an invalid lane completion descriptor.");
        }

        if (!descriptor.AllowsSource(completion.SourceKind))
        {
            return CompletionRoutingAdmissionResult.Fail(
                DomainValidationFailureReason.CompletionSourceDenied,
                completion,
                LaneCompletionRouteStatus.IgnoredRouteDisabled,
                "Completion route descriptor denies the completion source.");
        }

        bool routed = router.TryBuildPostedCompletion(
            completion,
            out _,
            out LaneCompletionRoutingResult routing);

        return new(
            routed ? DomainValidationResult.Passed : DomainValidationResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                routing.Message),
            routing);
    }
}
