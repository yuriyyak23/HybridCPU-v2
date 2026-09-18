# Phase 05 - Privileged Execution State Owner Decision

## Goal

Define the decision gate for `GuestCr0` and `GuestCr4`. These fields remain denied until a neutral privileged execution-state owner exists with real semantics, visibility policy, migration classification, and tests.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `VmcsFieldProjectionSchema` marks `GuestCr0` and `GuestCr4` as execution-domain-owned read-only aliases.
- `VmcsReadOnlyValueProjectionService` returns `PrivilegedExecutionStateProjectionDenied` for `GuestCr0` and `GuestCr4`.
- `ExecutionDomainReadOnlyStateView` currently materializes only `GuestPc`, `GuestSp`, and `GuestFlags`.
- Current execution read-only state is not a full guest CPU state model.

## Decision Record - Neutral Privileged Execution State Owner

Decision id: `ADR-VIRT-PES-2026-06-04`.

Status: accepted as a readiness/authority decision only. Production owner implementation remains future-gated. This record does not open `GuestCr0`, `GuestCr4`, broad execution-owned VMREAD categories, VMWRITE, SecureCompute authority, VMCALL backend success, completion publication, or retire publication.

Owner name: `PrivilegedExecutionStateDescriptor`.

Owner boundary: a future neutral runtime-owned execution sub-descriptor, referenced from the execution domain only after runtime-owned legality admits it. It is not the existing `ExecutionDomainReadOnlyStateView`, not VMCS storage, not generated schema metadata, not a compatibility-control descriptor, not `VmxCaps`, and not a VMX frontend manager.

Current production status: not implemented. The only correct current VMREAD result for `GuestCr0` and `GuestCr4` is `ReadOnlyProjectionDenied` with `VmcsReadOnlyValueProjectionDecision.PrivilegedExecutionStateProjectionDenied`.

### Why the Current Guest View Is Insufficient

`ExecutionDomainReadOnlyStateView` is intentionally a narrow PC/SP/flags snapshot with a neutral epoch. It does not define control-register bit ownership, reserved-bit masks, protection-mode state, paging-mode state, memory-translation coupling, secure/evidence visibility, migration/checkpoint classification, or restore validation. Reusing it for `GuestCr0` or `GuestCr4` would turn ordinary guest architectural state projection into privileged execution-state authority without a legality chain.

### Values Owned By the Future Descriptor

The future descriptor owns only privileged execution state that changes execution legality or address-translation semantics. The initial candidate set is:

- `GuestCr0` compatibility value.
- `GuestCr4` compatibility value.
- a state epoch for stale projection denial.
- explicit validity/materialization bits for each value.
- owner-defined masks for defined, reserved, must-be-zero, must-be-one, and runtime-policy-controlled bits.
- derived mode facts needed by runtime legality, such as protection enabled, paging enabled, extension enabled, or other architecture-specific execution-mode flags.

The future descriptor must not own guest PC/SP/flags, host PC/SP/flags, host control registers, compatibility-control fields, memory translation roots, SecureCompute descriptors, Stream/Lane6/Lane7 helper state, or backend completion/retire records.

### Legality And Reserved Bits

Runtime-owned legality remains final. The compatibility frontend may decode field ids and request projection, but it cannot legalize a CR0/CR4 value. Any future owner must:

- define masks for every implemented CR0/CR4 bit before projection opens;
- reject unknown, reserved, stale, partial, or policy-forbidden bits fail-closed;
- validate must-be-zero and must-be-one constraints before read-only projection;
- bind any mode-changing bit to the runtime operation that materialized it;
- deny projection when the runtime cannot prove that the value is current for the execution domain epoch.

Generated VMCS schema presence is vocabulary only. It cannot satisfy bit legality, materialization, or ownership.

### Paging And Protection Mode Implications

`GuestCr0` and `GuestCr4` are privileged because they can affect execution mode and memory-translation behavior. Future projection must cross-check the privileged execution-state descriptor against the neutral `MemoryDomainDescriptor` and execution domain state. At minimum, projection must deny:

- paging/protection claims that conflict with the memory translation view;
- second-stage translation claims that are not owned by the memory domain;
- stale CR values after execution-domain or address-space epoch changes;
- partial state where CR0 is materialized but CR4 is not, or vice versa, when a derived mode fact depends on both;
- any attempt to infer CR values from `GuestPc`, `GuestSp`, `GuestFlags`, host aliases, compatibility controls, VMCS scalar fallback, or generated schema entries.

### Evidence Visibility Policy

The future descriptor may expose read-only projection as `GuestArchitecturalState` only when the runtime evidence policy permits it. Host-owned runtime evidence, debug traces, telemetry, SecureCompute evidence, migration payloads, tests, generated artifacts, and compatibility aliases cannot satisfy this visibility policy. SecureCompute visibility requires its own neutral secure-domain policy; VMX/VMCS/`VmxCaps` cannot activate or grant it.

### Migration And Checkpoint Classification

If implemented, the privileged execution-state descriptor is descriptor-owned runtime state, not VMCS checkpoint authority. Migration/checkpoint requires an explicit owner payload class, state epoch, restore validation, reserved-bit revalidation, memory/execution consistency checks, and evidence classification. VMREAD projection remains a recomputed read-only compatibility view and must not be serialized as authoritative checkpoint state.

Current classification for `GuestCr0` and `GuestCr4`: future-gated, not migratable through VMCS, not checkpoint-restorable through VMCS, and denied by value projection.

### VMREAD Projection Preconditions

No positive `GuestCr0` or `GuestCr4` projection can open until all of the following exist:

- generated schema row for the exact field;
- explicit VMREAD matrix row for the exact field;
- runtime boundary admission for read-only compatibility projection;
- materialized `PrivilegedExecutionStateDescriptor` for the current execution domain;
- owner-defined CR0/CR4 legality and reserved-bit masks;
- memory/execution consistency validation;
- evidence visibility approval for guest architectural state;
- migration/checkpoint classification;
- stale/partial/missing-owner denial rules;
- negative tests for host alias, compatibility-control, generated-schema, scalar fallback, VMWRITE, SecureCompute authority, and publication/retire side-effect attempts;
- source/static gates proving no VMCS store, no VMX runtime manager, and no category-wide VMREAD opening.

Opening one field must not open the other field or any execution-owned category.

### Current Denial Reasons

`GuestCr0` and `GuestCr4` remain denied because:

- `PrivilegedExecutionStateDescriptor` is not implemented;
- CR0/CR4 bit legality and reserved-bit policy are not defined in production;
- paging/protection mode coupling is not validated against the memory domain;
- migration/checkpoint classification is not implemented;
- SecureCompute visibility is not a VMX/VMCS authority path;
- the current guest read-only state view intentionally covers only PC/SP/flags;
- generated schema availability is not value availability;
- VMCS scalar fallback and all write paths remain denied.

## Already Closed / Must Not Reopen

- Do not infer control register values from guest PC/SP/flags.
- Do not map CR0/CR4 from VMCS storage.
- Do not use tests or schema entries as privileged execution-state authority.
- Do not open host execution aliases as part of guest privileged-state work.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainDescriptor.cs`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-29-250-execution-owned-vmread-value-projection.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-29-252-control-like-vmread-fields-kept-denied.md`

## Work Items

- Record the owner decision as the future `PrivilegedExecutionStateDescriptor`, a neutral execution sub-descriptor.
- Keep `GuestCr0` and `GuestCr4` denied until that descriptor and its legality chain exist.
- Define CR0/CR4 bit legality, reserved-bit behavior, paging/protection interactions, and guest-visible snapshot semantics before any future code work.
- Define evidence visibility and migration/checkpoint class before any future projection.
- Define denial behavior for unmaterialized, partial, stale, or policy-inconsistent state.
- Add conformance proving no VMCS scalar fallback, no generated-schema availability shortcut, no compatibility-control shortcut, no host alias leakage, no VMWRITE opening, no SecureCompute authority shortcut, and no publication/retire side effect.

## Explicit Non-Goals

- Do not implement privileged-state projection in this docs phase.
- Do not open host PC/SP/flags/control aliases.
- Do not create a VMCS control-register store.
- Do not use `ExecutionDomainReadOnlyStateView` as more than its current PC/SP/flags slice.

## Done Criteria

- The future owner decision is documented as `PrivilegedExecutionStateDescriptor`.
- The current correct state remains explicit denial: `PrivilegedExecutionStateProjectionDenied`.
- Required tests and denial cases are enumerated before any code work.
- Evidence and migration classification requirements are documented before projection can be reconsidered.
- `GuestCr0` and `GuestCr4` no longer appear as merely absent fields; they have explicit authority, readiness, denial, and test-gate status.

## Required Tests / Static Checks

- Existing VMREAD denial tests for `GuestCr0` and `GuestCr4`.
- `FullyQualifiedName~VmxGuestControlRegisterOwnerDecisionTests`
- Static scan proving no VMCS scalar fallback.
- Static scan proving no VMCS field store, active VMCS pointer owner, or VMX runtime manager was introduced.
- Future implementation tests must cover reserved bits, stale state, missing owner, partial materialization, paging/protection conflicts, no host leakage, migration classification, restore revalidation, and no category-wide VMREAD opening.

## Residual Risk

Opening privileged fields too early would turn compatibility aliases into architectural CPU state authority. This remains a high-risk future gate.

## External Audit Risk Update

`GuestCr0` and `GuestCr4` are the next highest technical risk. They must not reuse `ExecutionDomainReadOnlyStateView` or any current guest-only view as privileged control-state authority. Future opening requires a separate neutral privileged execution-state owner with bit legality, reserved-bit rules, paging/protection interaction, state epoch, evidence visibility, migration/checkpoint class, and negative tests.

## Next Phase Dependency

Phase 06 depends on this denial boundary when deciding write and compatibility-control policy.
