# 03. Child/Secure Composition, Events And Publication

## Goal

Compose executable child virtualization with SecureCompute while keeping the authorities separate and making guest-visible effects obey publication ordering.

## Secure execution composition

Add an external composition operation that creates an `ExternalSecureExecutionBinding` from exact current parent domain, child domain and secure domain leases. The binding records all relevant epochs/generations and proven policy class.

It does not merge or replace the underlying leases. Closing/reconfiguring either child or secure domain invalidates/drains the composed binding first.

## Secure guest memory

A secure guest-region binding names:

- exact secure execution binding;
- exact child domain epoch;
- exact guest mapping epoch;
- exact parent mapping epoch;
- private/shared secure class.

The mapping lineage must already exist. No duplicate external memory mapping is created by the secure layer.

## Virtual I/O

Secure virtual I/O requires exact child VirtualIo binding plus secure-I/O admission. Rights remain bounded by the parent device authority supplied by SingNextOS.

## Event forwarding

Complete the executable adapter's virtual event path by forwarding neutral events to ExternalRuntime V3 child-event injection instead of returning `Unsupported` once the runner owns the operation.

Preserve monotonic sequence/replay rejection. Event receipts remain observations/publication receipts, not authority.

## Publication order

A guest event representing completion may be injected only after the external operation is visible, secure/child generations are revalidated, and publication is authorized. Physical completion or an interrupt alone is insufficient.

## Fault behavior

Stale child/secure/mapping generation after provider effect but before publication causes close/containment or quarantine. The guest event is suppressed.

## Required tests

- exact child + secure composition succeeds only for the same parent;
- stale child/secure lease cannot compose or close current binding;
- secure guest memory rejects mismatched guest/parent mapping epochs;
- event forwarding preserves sequence and exact child identity;
- event injection before publication is impossible in the adapter path;
- ambiguous composed-binding close remains tracked and blocks parent closure.
