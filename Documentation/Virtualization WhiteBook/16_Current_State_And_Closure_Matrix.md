# Current State And Closure Matrix

This chapter records the current virtualization status as of 2026-06-12.

## Current Facts

- VMX compatibility frontend freeze is declared.
- VMX is a frozen compatibility frontend, not the virtualization architecture.
- Physical legacy VMX backend authority is absent.
- `Legacy/VMX` has no active production C# authority, or the path is absent.
- `VmxExecutionUnit.cs` is absent.
- `VmcsManager.cs` is absent.
- `IVmcsManager.cs` is absent.
- VMCSv2 mutable helper authority has been removed or fenced.
- Generated/frontend projection inventory is compatibility artifact inventory.
- VMREAD has partial generated/read-only value projection through neutral owners after runtime admission.
- VMCALL trap projection is admitted-denied through neutral trap result, hypercall backend admission, route policy, and publication fences.
- Completion-only and retire-capable route flags are structurally separated, but neither positive descriptor is connected to VMX frontend.
- SecureCompute/VMX compatibility surfaces are projection and denial fences; VMX cannot activate, grant, or own SecureCompute.

## Closure Matrix

| Area | Current Status | Authority Owner | VMX Role |
|---|---|---|---|
| VMX frontend freeze | Closed | Compatibility frontend contract | Frozen ABI/projection |
| Legacy VMX backend | Closed as absent | None | Forbidden return |
| VMCS manager/runtime manager | Closed as absent | None | Forbidden return |
| VMCS fields | Generated read-only/denied | Runtime descriptors / completion | Projection schema |
| VMREAD completion-owned fields | Projected read-only | `CompletionRecord` | Recomputed compatibility value |
| VMREAD memory-owned fields | Projected read-only for admitted slice | `MemoryDomainDescriptor` / translation view | Compatibility value projection |
| VMREAD execution-owned fields | Projected read-only only for `GuestPc`/`GuestSp`/`GuestFlags` | `ExecutionDomainDescriptor` / read-only state view | Compatibility value projection |
| VMREAD privileged/control/host fields | Explicitly denied | Missing neutral privileged/control/host owner | Fail-closed alias |
| VMWRITE | Denied/fail-closed | No admitted neutral owner | Denied alias |
| VMCALL trap projection | Admitted-denied | Neutral trap policy/result | VMX exit projection |
| VMCALL backend admission | Closed as fail-closed | `HypercallBackendAdmissionService`; no backend owner | Missing neutral owner |
| Trap completion route | Split route policy implemented; positive use future-gated | `TrapCompletionRouteService` | Projection-only denied route in VMX frontend |
| Trap result | Closed neutral split | `NeutralTrapResult` | Mapped later |
| Completion publication | Completion-only neutral fence scaffolding implemented; production VMCALL denied | `TrapCompletionPublicationFence` | Projected only after fence |
| Retire intercept exit | Fenced | Publication fence + retire model | Fail-closed VMX effect |
| Descriptor readiness | Closed fail-closed | Neutral materialized descriptors/checkpoints | Not derived from VMREAD |
| Migration/evidence for recomputed fields | Closed | Neutral migration/evidence policy | VMCS completion fields are not payload authority |
| Capabilities | Grant-first | `CapabilityDescriptorSet` | `VmxCaps` projection |
| Host evidence | Non-leak | Evidence policy / host evidence boundary | Compatibility projection only |
| SecureCompute compatibility | Projection/denial matrix | SecureCompute runtime descriptors/policies | VMX cannot activate or own |
| Nested composition | Neutral model | Nested descriptors/services | VMX-compatible bridge |
| Compiler emission | Guarded | Compiler boundary/no-emission gates | Frozen opcode vocabulary |

## Recent Closed Heavy Steps

Generated read-only VMREAD value projection:

- completion-owned fields project from `CompletionRecord`;
- memory-owned fields project from `MemoryDomainReadOnlyTranslationView`;
- `Vpid` is tied to neutral address-space tagging;
- `Cr3TargetCount` is tied to neutral address-space target count;
- execution-owned `GuestPc`, `GuestSp`, and `GuestFlags` project only from `ExecutionDomainReadOnlyStateView`;
- `GuestCr0`, `GuestCr4`, host execution aliases, `HostCr3`, compatibility-control fields, unknown fields, and all writes remain denied.

Neutral trap/completion/backend split:

- `NeutralTrapResult` is runtime vocabulary;
- `VmxTrapProjectionMapper` maps only after the neutral result exists;
- `TrapCompletionRouteService` exists as neutral route policy before any successful publication;
- `RuntimeOwnedCompletionPublication` separates the completion route flag from retire authorization;
- the neutral fence can return `CompletionPublishedRetireDenied` with a completion record and retire false;
- host-owned evidence and missing/unsafe migration classification cannot grant retire;
- production VMCALL uses `HypercallBackendAdmissionRequest.MissingNeutralOwner`;
- no successful VMCALL backend, compatibility-exit completion, or intercept retire publication is opened.

Descriptor readiness and migration/evidence proof:

- VMREAD projection values do not make migration or nested readiness successful;
- recomputed completion-owned compatibility fields are not checkpoint payload authority;
- compatibility projection metadata cannot become restore or evidence authority.

## What Is Still Not Implemented

- Successful VMX backend execution.
- Mutable VMCS field store.
- Active VMCS pointer state.
- Successful VMCALL backend hypercall path.
- Successful VMX intercept/exit retire publication without neutral owner.
- Feature-complete VMREAD backend execution.
- VMREAD values for privileged/control/host/nested fields without explicit neutral owner/value source.
- Neutral privileged execution-state owner for `GuestCr0` and `GuestCr4`.
- Neutral host-address-space owner for `HostCr3`.
- Neutral host-execution owner for host PC/SP/flags/control aliases.
- Control-bit VMREAD mapper over a materialized neutral compatibility-control value contract.

## Residual Interpretation Rule

If a compatibility object looks successful but the neutral owner, value source, backend owner, route, or publication fence is absent, classify it as projection-only or denied. Do not classify it as production backend success.
