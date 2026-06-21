# Secure IO And Shared Buffers

## Phase 12 Contract

Secure I/O uses explicit descriptor-owned shared buffers. Buffer ID alone is not authority.

`SecureIoDomainDescriptor.TryFindCurrentSharedBuffer` requires:

- `ExplicitSharedBuffersOnly` policy;
- validated nonzero owner domain tag;
- materialized current policy epoch;
- materialized shared-buffer descriptor;
- matching owner domain;
- current lifetime;
- allowed evidence class;
- buffer grant matching the policy epoch.

Hypercall shared-buffer arguments additionally require a current typed argument grant.

## DMA Rules

- neutral I/O owner is mandatory;
- private DMA is denied;
- shared DMA requires memory and I/O policy agreement;
- range and direction must match;
- stale owner, lifetime, evidence or grant binding is denied;
- raw private pointers and forged opaque handles are denied.

## Policy-Only Result

An allowed I/O result is explicitly `IsPolicyAdmissionOnly`.

Even when completion or retire fences are present:

- backend execution remains false;
- completion publication remains false;
- retire publication remains false.

Fences are prerequisites for a future publication path, not publication authority and not proof of device side effects.

## Lane Boundary

Lane6, Lane7 and Stream tokens do not become SecureCompute grants, backend handles or migration authority. Native tokens and backend bindings remain host-owned or denied payload classes.

## Hypercall Boundary

Secure hypercall recognition does not establish VMCALL authority. Current backend-success requests fail closed. Phase 13 now defines a neutral typed proof-only owner/service contract and exact decoded-leaf, service-ID and owner-ID allocations in `SecureHypercallBackendOwnerAbiRegistry`. Execution, completion publication and retire publication remain closed.
