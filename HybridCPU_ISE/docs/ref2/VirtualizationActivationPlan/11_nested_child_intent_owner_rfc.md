# Phase 11 - Nested Child Intent Owner RFC

Status: future owner-specific RFC. Nested execution remains denied.

## 2026-06-11 Audit Contract

- File name: `11_nested_child_intent_owner_rfc.md`.
- Purpose: define the future neutral child-intent owner required before nested execution can open.
- Status: `future-gated`; nested execution remains denied.
- Scope: parent/child intent, authority bounds, capability filtering, evidence policy, memory composition, migration, completion mapping, rollback.
- No-goals: no Shadow VMCS/VMCS12/VMCS02 authority, no mutable nested VMCS state, no nested SecureCompute execution, no nested completion/retire publication.
- Code anchors: `CloseToHSL/Core/Runtime/Nested/**`, `NestedProjectionService.cs`, runtime nested policies, Shadow VMCS compatibility bridge tests.
- Authority owner: future neutral child-intent owner; Shadow VMCS/VMCS12/VMCS02 are compatibility vocabulary only.
- Required RFC/ADR: mandatory before implementation with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: missing child-intent owner means `не доказано`, `future-gated`, and `должно оставаться denied`.
- Tests/static scans: Shadow VMCS authority denial, VMCS12/VMCS02 checkpoint denial, child-intent/capability/evidence/completion/restore denials.
- Risks: importing compatibility nested state as parent/child runtime state or checkpoint authority.
- Next-gate dependency: deferred behind the first neutral hypercall path and accepted nested RFC/ADR.

## Phase Goal

Define the requirements for a neutral nested child-intent owner before any nested virtualization path can open.

## Historical Baseline (2026-06-11)

Nested projection/checkpoint services and design fences exist in neutral runtime namespaces, but active nested execution is not open. Shadow VMCS, VMCS12, and VMCS02 are compatibility vocabulary/bridge concepts only and cannot own nested runtime state.

## ISE-NESTED-CHILD-INTENT-BASELINE-11 - Closure Record

Closure date: 2026-06-18.

Closure state: closed `DENIED/FUTURE-GATED BASELINE / NO-NESTED-ACTIVATION`.

This closure records the current nested child-intent baseline. It closes the audit assumption that nested activation must remain denied; it does not accept a nested owner RFC/ADR, does not implement nested execution, and does not give Shadow VMCS, VMCS12, or VMCS02 runtime authority.

Verified baseline:

| surface | code/test evidence | current result |
| --- | --- | --- |
| child intent descriptor | read-only compatibility projection; field reads require neutral runtime-owned nested intent state | no mutable child state |
| Shadow VMCS bridge | `ShadowVmcsNestedProjectionService` fails closed with `CompatibilityProjectionFailed` and maps through `InvalidVmcs12` | no nested enablement |
| VMCS12/VMCS02 payloads | `SecureNestedDomainAdmissionPolicy` returns `DeniedNestedVmcsAuthority` | no authority |
| mutable shadow authority | `DeniedMutableShadowVmcsAuthority` | no mutable nested state |
| missing neutral child-intent owner | runtime nested policies deny missing descriptor/compatibility authority | denied |
| capability/evidence/completion mapping | neutral runtime filters can deny independently | no publication shortcut |
| production callers | VMX admission/dispatch/retire paths do not call nested enablement or Shadow VMCS projection | no production activation |

Closure invariants:

- Shadow VMCS, VMCS12, and VMCS02 remain compatibility vocabulary/bridge only.
- `AllowedDesignFence` is not backend success, mutable nested-state authorization, completion publication, or retire publication.
- Runtime nested descriptors, capability filters, evidence policy, memory composition, projection checkpoint, completion mapping, and restore validation are necessary but not sufficient without an accepted owner RFC/ADR.
- VMREAD/VMWRITE projection, SecureCompute admission, compiler metadata, migration images, lane/stream helpers, and VmxCaps cannot become nested authority.
- Any future nested work must start with an accepted neutral child-intent owner RFC/ADR and keep VMCS12/VMCS02/Shadow VMCS denial tests as adjacent negatives.

## Owner Of Authority

A future neutral child-intent owner under runtime nested descriptors and policies. It must own parent/child domain intent, authority bounds, capability filtering, evidence policy, memory composition, migration, and completion mapping.

## What Can Be Implemented

- RFC/ADR for child-intent owner.
- Parent/child authority map.
- Nested memory composition contract.
- Evidence visibility and non-leak policy.
- Migration/checkpoint/restore class.
- Completion mapping and rollback plan.
- Negative tests for Shadow VMCS/VMCS12/VMCS02 authority denial.

## What Remains Denied/Future-Gated

- Nested execution.
- VMCS12/VMCS02 mutable state.
- Shadow VMCS runtime owner.
- Nested SecureCompute execution.
- Nested completion/retire publication.
- Nested migration/checkpoint as VMCS projection authority.

## Forbidden Shortcuts

- Treating Shadow VMCS as neutral child state.
- Treating nested VMCS field projection as parent/child authority.
- Using VMREAD/VMWRITE to materialize child state.
- Reusing host evidence or debug traces as child guest-visible evidence.

## Required RFC/ADR

Required before implementation. It must include parent owner, child owner, capability filter, evidence policy, memory composition, migration class, restore validation, completion mapping, retire rules, and denial of compatibility state stores.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested/**`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested/Projection/NestedProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Nested/Policies/**`
- Shadow VMCS compatibility bridge tests under `HybridCPU_ISE.Tests/VmxRefactoring/`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/09_nested_virtualization_child_intent_plan.md`
- `Documentation/Virtualization WhiteBook/08_Nested_Virtualization.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- Shadow VMCS cannot own nested state.
- VMCS12/VMCS02 cannot be checkpoint authority.
- Missing child-intent owner denies.
- Capability filter denial.
- Evidence policy denial.
- Completion mapping denial.
- Restore rejects compatibility projection metadata.

## Required Static/Source Scans

```powershell
rg -n "ShadowVmcs|VMCS12|VMCS02|Nested.*Vmcs|Vmcs.*Nested|ChildIntent" HybridCPU_ISE/CloseToHSL/Core HybridCPU_ISE.Tests/VmxRefactoring
```

Matches must be compatibility bridge, denial, or neutral owner code.

## Migration/Evidence Classification

Nested state must be neutral child-intent or child-domain state. VMCS projection, Shadow VMCS, host evidence, debug traces, scheduler evidence, and native handles are excluded from migration authority.

## Completion/Retire Implications

Nested traps/completions require neutral completion mapping and explicit retire rule. Parent/child completion projection cannot bypass the route/fence model.

## Exit Criteria

- RFC/ADR accepted before code.
- Shadow VMCS authority denial remains test-backed.
- Nested remains denied until full owner chain exists.

## Dependency On Previous/Next Phase

Depends on Phases 02 and 03. It is intentionally deferred behind the first VMCALL path.
