# Phase 09 - Nested Virtualization Child Intent Plan

## Goal

Define nested virtualization future work around neutral child intent and composition owners. Mutable shadow VMCS, VMCS12, VMCS02, or compatibility bridge objects must stay compatibility vocabulary only.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- Current whitebooks classify nested expansion as future-gated through neutral child domain intent descriptors, capability filters, nested memory composition owners, and evidence policies.
- SecureCompute has a nested design-fence model for secure child intent, but it does not authorize nested secure backend success.
- VMCS/Shadow VMCS vocabulary is compatibility bridge vocabulary only.
- `ChildDomainIntentDescriptor` is a read-only compatibility projection and cannot store mutable child intent fields.
- `ShadowVmcsNestedProjectionService.TryEnable(...)` fails closed with `CompatibilityProjectionFailed`; `NestedDomainController.TryEnable(...)` maps that failure to `InvalidVmcs12`.
- `SecureNestedDomainAdmissionPolicy` denies `Vmcs12Authority`, `Vmcs02Authority`, and mutable shadow authority while keeping `BackendSuccessAuthorized: false` and `MutableNestedStateAuthorized: false`.
- Runtime nested projection remains split across neutral owner surfaces: descriptor authority, capability filter, evidence policy, projection checkpoint service, memory composition owner, and runtime admission.

## Already Closed / Must Not Reopen

- Do not use shadow VMCS as mutable nested runtime state.
- Do not use VMCS12 or VMCS02 as authority payloads.
- Do not derive nested readiness from VMREAD projected values.
- Do not infer nested memory composition from host/guest compatibility aliases.

## Required Code/Doc Anchors

- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/Nested/SecureChildDomainIntentDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/Nested/SecureNestedDomainAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested/Projection/NestedProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/ShadowVmcsNestedProjectionService.cs`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-24-141-shadow-vmcs-bridge-retirement-contract.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-27-234-vmcsv2-header-child-intent-authority-removal.md`
- `HybridCPU_ISE/docs/VMXRefactoring/SuccessClosed/2026-05-30-253-descriptor-readiness-policy-audit.md`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxNestedChildIntentHardeningTests.cs`

## Work Items

- Define a neutral nested child-intent owner and required fields.
- Define parent/child domain tag binding, capability filters, evidence visibility, and migration class.
- Define nested memory composition owner requirements.
- Define compatibility projection limits back to VMX vocabulary.
- Define denial tests for missing owner, stale child intent, parent/child bound violation, shadow VMCS authority, and VMCS12/VMCS02 payload attempts.

## Closure Decision - ADR-VIRT-NESTED-CHILD-INTENT-2026-06-04

Phase 09 is closed as owner-first denial/readiness only.

The current implementation is deliberately fail-closed:

| Surface | Current owner | Current result | Publication/execution result |
| --- | --- | --- | --- |
| Compatibility child intent | `ChildDomainIntentDescriptor` | read-only projection; field reads require neutral runtime-owned nested intent state | no nested execution, no backend success, no completion/retire publication |
| Shadow VMCS bridge | `ShadowVmcsNestedProjectionService` | `CompatibilityProjectionFailed`; retirement fenced | no mutable nested state, no VMCS mutation |
| VMCS12/VMCS02 payload attempts | `SecureNestedDomainAdmissionPolicy` | `DeniedNestedVmcsAuthority` | no backend success, no mutable nested state |
| Mutable shadow payload attempts | `SecureNestedDomainAdmissionPolicy` | `DeniedMutableShadowVmcsAuthority` | no backend success, no mutable nested state |
| Runtime nested projection | `NestedProjectionService` plus runtime filters | requires neutral descriptor, capability, evidence, runtime admission, and optional completion mapping | no compatibility shortcut to execution or publication |

Future nested enablement requires a separate RFC/ADR with a neutral child-intent owner, parent/child binding, capability grant policy, evidence visibility policy, nested memory composition owner, migration/checkpoint policy, backend execution owner, completion publication fence, retire publication rule, and negative tests for every missing or stale proof. Compatibility VMX names are allowed only as projection vocabulary.

## Explicit Non-Goals

- Do not implement nested execution.
- Do not create mutable shadow VMCS state.
- Do not make nested SecureCompute active through VMX.
- Do not publish nested completion/retire effects without Phase 08 gates.

## Done Criteria

- Nested work is future-gated behind neutral child intent.
- Shadow VMCS and VMCS12/VMCS02 are documented as compatibility bridge vocabulary only.
- Required owner, evidence, migration, and denial tests are specified.
- The phase does not claim nested backend success.
- `VmxNestedChildIntentHardeningTests` proves the current source/static gates and fail-closed decisions.

## Required Tests / Static Checks

- Existing SecureCompute nested design-fence tests.
- Existing VMX nested fence tests.
- `FullyQualifiedName~VmxNestedChildIntentHardeningTests`
- Static scan for shadow VMCS authority language.
- Static scan for VMCS12/VMCS02 used as mutable state vocabulary.
- Source anchor scan for neutral nested owner/readiness surfaces and denied SecureCompute nested authority decisions.
- Forbidden source scan for nested execution units, mutable shadow VMCS stores, VMCS12/VMCS02 managers, backend-success authorization, mutable nested-state authorization, compatibility completion publication, and direct retire publication.
- Documentation overclaim scan for current nested backend success, nested execution, mutable shadow state, VMCS12/VMCS02 authority, child-intent completion publication, or child-intent retire publication.

## Residual Risk

Nested vocabulary is historically VMX-heavy. The plan must keep compatibility bridge objects separate from neutral nested runtime owners.

## External Audit Risk Update

Nested virtualization must remain neutral child intent. Shadow VMCS, VMCS12, and VMCS02 must not reappear as runtime state stores. Future nested work requires child intent descriptors, capability filtering, nested memory composition owner, and evidence policy.

## Next Phase Dependency

Phase 10 depends on the nested owner discipline when classifying memory, I/O, lane, stream, and L7 surfaces.
