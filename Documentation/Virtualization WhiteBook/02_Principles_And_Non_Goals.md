# Principles And Non-Goals

## Principles

Virtualization is part of the general runtime legality and admission system. It is not a special VMX-owned privilege island.

The source of truth is neutral:

- Execution domains define execution state and scheduling eligibility.
- Memory domains define address-space, translation, dirty tracking, and invalidation authority.
- I/O domains define DMA and IOMMU authority.
- Capability descriptors and grants define what a domain may request.
- Evidence policy defines what can be exposed to guests, compatibility layers, migration, and diagnostics.
- Trap and completion services define neutral fault, intercept, completion, and retire publication.
- VMX-compatible artifacts project these facts after the neutral owner has authorized them.

## VMX Vocabulary Is Allowed Only In Narrow Roles

VMX/VMCS vocabulary is allowed as:

- frozen ABI names;
- compatibility frontend names;
- generated read-only projection names;
- denied/no-effect alias names;
- fail-closed opcode and retire vocabulary;
- test-local conformance/static evidence.

VMX/VMCS vocabulary is not allowed as:

- runtime state owner;
- execution-domain owner;
- trap policy owner;
- memory/I/O authority;
- active VMCS pointer state;
- VMCS field store;
- migration payload authority;
- host evidence store;
- successful backend execution path without runtime admission and neutral publication.

## Non-Goals

This WhiteBook does not define a legacy VMX architecture. It records the post-freeze architecture where VMX is a compatibility frontend.

The following are explicitly outside the current model:

- restoring `VmxExecutionUnit`;
- restoring `VmcsManager` or `IVmcsManager`;
- introducing `VmcsManagerAdapter`, `VmxRuntimeManager`, `VmcsProjectionRuntimeManager`, or `VmcsV2RuntimeManager`;
- creating a VMCS field store;
- creating active VMCS pointer state;
- using `VmExitReason` as runtime trap authority;
- letting `TrapDecision` decide runtime policy;
- treating tests as production requirements;
- converting denied aliases into successful backend behavior.

## Fail-Closed Rule

If a path cannot prove neutral ownership, it must deny. If a path can prove compatibility projection but not backend publication, it may return a compatibility projection result only as admitted-denied evidence. It still must not retire as a successful VMX backend effect.

## Documentation Rule

Documentation must distinguish four things:

- current implemented behavior;
- frozen ABI/projection vocabulary;
- denied/fail-closed aliases;
- future heavy steps.

Mixing those categories is an architectural bug even when the code still builds.
