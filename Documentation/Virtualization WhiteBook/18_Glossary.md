# Glossary

## ABI

Stable externally visible binary or semantic contract. In this pack, VMX ABI stability means names and numeric projection values can remain stable even when runtime authority is neutral.

## Admitted-Denied

A path that passes some admission gates but intentionally denies backend execution or publication. Current VMCALL trap projection is admitted-denied.

## Compatibility Alias

A VMX/VMCS/CSR/opcode name that maps to a neutral owner or projection service. Alias status does not grant authority.

## Compatibility Frontend

The frozen VMX-facing layer that decodes VMX-compatible operations, validates aliases, calls runtime admission, and projects neutral facts.

## Compatibility-Control Field

VMX control-field compatibility alias owned by neutral compatibility-control descriptor metadata. Current VMREAD values for these fields remain denied until a field-by-field neutral control-bit value contract exists.

## CompletionRecord

Neutral runtime completion record. VMX-compatible completion projection may be derived from it only through the appropriate projection service and publication fence.

## ExecutionDomainReadOnlyStateView

Neutral execution-domain snapshot that can expose `GuestPc`, `GuestSp`, and `GuestFlags` for read-only projection when materialized. It is not a source for `GuestCr0`, `GuestCr4`, or host execution state.

## Evidence Policy

Runtime policy deciding what evidence can be guest-visible, compatibility-visible, migration-visible, debug-visible, or host-only.

## Fail-Closed

Default denial behavior when authority, capability, evidence, descriptor ownership, or publication proof is missing.

## Frozen ABI

Compatibility vocabulary retained for stability. Frozen does not mean authoritative.

## Host-Owned Evidence

Evidence useful to the host runtime or diagnostics but not automatically visible to the guest, migration format, or VMX projection.

## Neutral Runtime Owner

The runtime descriptor or service that owns the actual fact. Examples: execution domain descriptor, memory domain descriptor, capability grant collection, trap policy bitmap, completion fence.

## NeutralTrapResult

Runtime-owned trap result with no VMX vocabulary dependency. It records whether a neutral trap should happen and why in neutral terms.

## PrivilegedExecutionStateProjectionDenied

VMREAD denial for `GuestCr0` and `GuestCr4` while there is no neutral privileged execution-state owner/value source.

## Projection

A read-only or denied compatibility view derived from neutral runtime facts.

## Publication Fence

A neutral gate that determines whether a result can be published as a completion or retire effect. `TrapCompletionPublicationFence` is the current trap publication fence.

## SecureCompute Compatibility

Projection/denial surface that keeps VMX from becoming SecureCompute authority. VMX can be a compatibility view only after secure runtime descriptors, visibility policy, migration classification, and conformance proof exist.

## TrapCompletionRouteService

Neutral completion-routing policy that must authorize route publication before a trap/intercept can become a published completion or retire effect. The current VMX frontend uses projection-only denial.

## RuntimeBoundaryAdmissionService

Neutral service that joins domain, capability, evidence, and root-authority gates before a compatibility request may cross into runtime-owned territory.

## TrapDecision

VMX-facing projection record containing VMX exit vocabulary. It is projection output, not runtime trap authority.

## VMCSv2

Generated compatibility projection surface over neutral descriptors and completion records. It is not a mutable runtime state store.

## VmExitReason

VMX-compatible exit reason vocabulary. It may be projected after neutral trap/completion decisions. It must not be used as runtime authority.

## VmxRetireEffect

Retained VMX-compatible retire vocabulary. It can model fail-closed outcomes and compatibility effects. Successful intercept exit requires neutral publication authorization.
