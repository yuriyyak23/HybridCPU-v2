# Secure Nested Child Intent Owner RFC

## Phase Metadata

- File name: `18_secure_nested_child_intent_owner_rfc.md`
- Phase goal: preserve nested secure domain design-fence and define future RFC requirements.
- Status: unconditionally future/design-fenced. No current or first-positive-path prerequisite may weaken this denial.
- Scope: child intent, parent-child monotonicity, nested checkpoint/projection/evidence facts and VMCS12/VMCS02/Shadow VMCS denial.
- No-goals: no nested secure execution, no mutable nested secure state and no Shadow VMCS authority.

## Current Baseline

Nested admission can return `AllowedDesignFence` for validated child intent. Because generic top-level admission does not have exhaustive nested-specific dispatch, production admission must treat `NestedSecureDomain` as an explicit future-gated denial until a separate nested RFC, implementation and release gate exist. `AllowedDesignFence` never authorizes nested backend execution or mutable nested state.

External audit clarification, 2026-06-11: existing nested facts are not enough for activation unless the release package proves parent-child bounds, epoch monotonicity and non-execution behavior with code tests. Until then, nested remains design-fenced even if descriptors and intent records exist.

## Authority Owner

Future neutral nested child-intent owner and nested runtime owner. VMCS12, VMCS02, Shadow VMCS and compatibility projection do not own nested SecureCompute authority.

## What Can Be Implemented

- future RFC/ADR template;
- parent-child authority matrix;
- negative tests for expansion;
- checkpoint/projection/evidence quarantine tests.

## What Remains Denied/Future-Gated

- nested backend execution;
- nested mutable state;
- child intent execution owner;
- VMCS12/VMCS02 authority;
- Shadow VMCS mutable authority;
- nested migration authority from checkpoint facts.

## Forbidden Shortcuts

- child intent equals execution;
- parent-child monotonicity equals nested activation;
- nested checkpoint equals nested state store;
- Shadow VMCS bridge equals runtime authority;
- VMREAD projection cannot open nested readiness.

## Required RFC/ADR

Required before any nested execution:

- neutral child-intent owner;
- nested execution owner;
- nested memory composition owner;
- nested evidence policy;
- nested migration class;
- completion/retire rule;
- VMX bridge denial/projection matrix.

## Code Anchors

- `SecureChildDomainIntentDescriptor.cs`
- `SecureNestedDomainAdmissionPolicy.cs`
- VMX nested child intent tests;
- VMCS12/VMCS02/Shadow VMCS bridge code.

## Documentation Anchors

- `SecureComputerefactoringNew/19_nested_domain_design_fence.md`
- `VirtualiztionRefactoringNew/09_nested_virtualization_child_intent_plan.md`
- `Documentation/Virtualization WhiteBook/08_Nested_Virtualization.md`

## Required Tests

- missing child intent owner denied;
- missing parent secure descriptor denied;
- child authority expansion denied;
- nested projection expansion denied;
- nested migration payload expansion denied;
- host evidence leakage denied;
- VMCS12/VMCS02 authority denied;
- mutable Shadow VMCS authority denied.
- nested child intent cannot create backend execution, mutable child secure state or completion/retire publication.

## Required Static/Source Scans

- `nested.*backend execution`
- `AllowedDesignFence.*execution`
- `VMCS12.*authority`
- `VMCS02.*authority`
- `Shadow VMCS.*mutable`
- `child intent.*activation`

## Migration/Evidence Classification

Nested child intent is design-fence/admission evidence only. Nested checkpoint and telemetry facts are denied as runtime authority until separate RFC/ADR.

## Completion/Retire Implications

No nested completion or retire publication exists.

## SecureCompute Activation Implications

Limited non-nested SecureCompute activation must explicitly state that nested SecureCompute execution remains denied.

## Exit Criteria

- design fence preserved;
- future RFC requirements listed;
- all nested authority shortcuts denied.

## Dependency

Previous: `17_secure_vmx_boundary_zero_authority_plan.md`. Phase 19 has since closed independently as a no-compiler-change decision gate. Phase 18 remains future/design-fenced; any nested execution work still requires this RFC before it can move into code, tests or release evidence.
