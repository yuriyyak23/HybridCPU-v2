namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct EventDeliveryResult(
    DomainValidationResult Validation,
    EventInjectionDescriptor Event,
    InterruptPostDisposition Disposition)
{
    public bool IsQueued =>
        Validation.IsValid &&
        (Disposition == InterruptPostDisposition.Queued ||
         Disposition == InterruptPostDisposition.RemappedQueued ||
         Disposition == InterruptPostDisposition.Coalesced ||
         Disposition == InterruptPostDisposition.RemappedCoalesced);

    public static EventDeliveryResult Fail(
        DomainValidationFailureReason reason,
        EventInjectionDescriptor evt,
        InterruptPostDisposition disposition,
        string message) =>
        new(DomainValidationResult.Fail(reason, message), evt, disposition);
}

public sealed partial class EventDeliveryService
{
    public EventDeliveryResult Post(
        EventQueueDescriptor? descriptor,
        PostedEventQueue queue,
        EventInjectionDescriptor evt,
        InterruptRemapTable? remapTable = null)
    {
        if (descriptor is null)
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.MissingEventQueueDescriptor,
                evt,
                InterruptPostDisposition.DroppedInvalid,
                "Event delivery requires a runtime-owned event queue descriptor.");
        }

        if (!descriptor.IsRuntimeAuthoritative)
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.RuntimeAuthorityMissing,
                evt,
                InterruptPostDisposition.DroppedInvalid,
                "Compatibility projection cannot own event delivery.");
        }

        if (!evt.IsValid)
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                evt,
                InterruptPostDisposition.DroppedInvalid,
                "Event delivery rejected an invalid event descriptor.");
        }

        EventInjectionDescriptor routed = evt;
        bool remapped = false;
        bool coalescePosted = descriptor.AllowsEventCoalescing && evt.Posted;
        bool remapCoalesces = true;
        InterruptPostDisposition disposition = InterruptPostDisposition.None;
        if (remapTable is not null &&
            !remapTable.TryRoute(
                evt,
                out routed,
                out remapped,
                out remapCoalesces,
                out disposition))
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                routed,
                disposition,
                "Event delivery was dropped by the runtime remap policy.");
        }

        coalescePosted &= remapTable is null || remapCoalesces;
        if (!descriptor.AcceptsPendingCount(queue.Count))
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                routed,
                InterruptPostDisposition.DroppedQueueFull,
                "Event queue descriptor denies additional pending events.");
        }

        if (coalescePosted &&
            queue.TryFindCoalescingMatch(routed, out EventInjectionDescriptor existing))
        {
            return new(
                DomainValidationResult.Passed,
                existing,
                remapped
                    ? InterruptPostDisposition.RemappedCoalesced
                    : InterruptPostDisposition.Coalesced);
        }

        if (!queue.TryEnqueue(routed, out EventInjectionDescriptor queued, out disposition))
        {
            return EventDeliveryResult.Fail(
                DomainValidationFailureReason.EventQueueRejected,
                routed,
                disposition,
                "Posted event queue rejected the event descriptor.");
        }

        return new(
            DomainValidationResult.Passed,
            queued,
            remapped
                ? InterruptPostDisposition.RemappedQueued
                : InterruptPostDisposition.Queued);
    }
}
