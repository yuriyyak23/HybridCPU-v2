# No Effect Disabled Baseline Revalidation

## Phase Metadata

- File name: `04_no_effect_disabled_baseline_revalidation.md`
- Phase goal: preserve the theorem that absent, disabled, `None` and unmaterialized SecureCompute states do not affect ordinary operations.
- Status: partial admission-level non-regression gate; end-to-end disabled observational equivalence remains open.
- Scope: root descriptor, runtime context, Stage B admission, memory, VMX, compiler and migration surfaces.
- No-goals: no activation flag and no secure backend execution.

## Current Baseline

`SecureComputeSecurityLevel.None` normalizes to `Disabled`. `IsActive` requires enabled and materialized descriptor state. Ordinary operations are not over-denied by absent or disabled secure descriptors.

External audit clarification, 2026-06-11: no-effect applies to ordinary operations only. A non-ordinary secure operation class with an absent, disabled or unmaterialized descriptor must not inherit ordinary no-effect behavior; it must be routed to fail-closed denial before any activation claim.

Reconciled ordering decision, 2026-08-07: the full differential harness is the second implementation change, immediately after audit-baseline freeze and before taxonomy, registry, grants or map semantics. It runs after every subsequent semantic PR so later work cannot redefine the Disabled baseline unnoticed.

## Authority Owner

Neutral runtime admission owns the no-effect decision. A disabled descriptor owns no positive secure authority.

## Implemented Baseline

- cross-suite no-effect tests;
- no-effect documentation over ordinary runtime and authority-sensitive surfaces;
- static guards preventing disabled descriptor state from feeding VMX, migration, grants or compiler emission as authority.

## What Remains Denied/Future-Gated

- secure operation admission from disabled descriptor;
- backend owner admission from disabled descriptor;
- VMX activation by disabled descriptor;
- migration authority from disabled descriptor;
- compiler emission from disabled descriptor.

## Forbidden Shortcuts

- treating `None` as a separate active level;
- hidden Stage A or compiler effects from disabled descriptors;
- VMREAD projection because a disabled descriptor exists;
- checkpointing disabled descriptor metadata as authority.

## Required RFC/ADR

None. This is a regression proof, not a new owner path.

## Code Anchors

- `SecureComputeDomainDescriptor.cs`
- `DomainRuntimeContext.cs`
- `RuntimeBoundaryAdmissionService.cs`
- `SecureComputeNoEmissionContract.cs`
- VMX deny/projection fences.

## Documentation Anchors

- `SecureComputerefactoringNew/05_no_effect_and_disabled_equivalence.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`

## Required Tests

- absent descriptor leaves ordinary behavior unchanged;
- disabled descriptor leaves ordinary behavior unchanged;
- `None` normalizes to `Disabled`;
- unmaterialized enabled descriptor is no-effect for ordinary operations;
- ordinary operations do not call secure memory/hypercall/backend execution;
- disabled state cannot alter VMX/VMCS/`VmxCaps`;
- compiler no-emission remains true.
- non-ordinary secure operation with absent, disabled or unmaterialized descriptor is denied after the Stage B P0 correction.
- paired absent-versus-explicitly-Disabled architectural traces for SMT widths 1, 2 and 4 with FSP off/on;
- equal architectural registers/memory, exceptions, issue/retire order, completion/public evidence, replay digests and memory/I/O contention outcomes;
- exclusion list for non-architectural host observations such as wall-clock, GC timing and host thread IDs unless product semantics expose them.

## Required Static/Source Scans

- `SecureComputeSecurityLevel.None`
- `IsNoEffect`
- `IsActive`
- `SecureOperationClass.Ordinary`
- forbidden uses of disabled descriptor in VMX, migration, compiler and backend owner paths.

## Migration/Evidence Classification

Disabled or absent state has no migration authority. If serialized as metadata, it must be classified as non-authoritative configuration only.

## Completion/Retire Implications

No completion, retire or backend result can derive from disabled/no-effect state.

## SecureCompute Activation Implications

This phase is mandatory before activation but does not activate anything.

## Exit Criteria

- no-effect matrix covers ordinary runtime, memory, VMX, migration and compiler;
- all tests remain negative for authority;
- docs say no-effect, not activation.

Exit status: not satisfied for CPU observational equivalence. Current tests cover policy/admission decisions, not paired architectural traces across pipeline, SMT, FSP, memory, I/O, exceptions, completion and retire. Completion requires the differential trace gate in `24_audit_revalidation_and_dependency_order.md`.

## Dependency

Previous: `03_owner_specific_rfc_adr_process.md`. Next: `05_secure_descriptor_materialization_activation_plan.md`.
