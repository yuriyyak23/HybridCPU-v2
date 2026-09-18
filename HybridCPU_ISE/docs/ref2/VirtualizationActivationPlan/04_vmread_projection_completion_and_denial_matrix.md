# Phase 04 - VMREAD Projection Completion And Denial Matrix

Status: projection matrix and expansion gate. No new VMREAD fields opened by this document.

## 2026-06-11 Audit Contract

- File name: `04_vmread_projection_completion_and_denial_matrix.md`.
- Purpose: keep VMREAD field-by-field and tied to neutral owner/value source only.
- Status: existing projected set documented; all new fields are future-gated.
- Scope: completion-owned, memory-owned, execution-owned projected fields, guarded privileged CR0/CR4 projection, and denied host aliases, controls, unknowns, writes.
- No-goals: no schema-as-availability, no VMCS scalar fallback, no completion/retire publication, no VMWRITE.
- Code anchors: `VmcsReadOnlyValueProjectionService.cs`, `VmcsFieldProjectionSchema.cs`, `ExecutionDomainReadOnlyStateView.cs`, `MemoryDomainReadOnlyTranslationView.cs`, `CompatibilityControlDescriptor.cs`.
- Authority owner: only the neutral owner named by the schema entry plus implemented value source; the schema is vocabulary, not authority.
- Required RFC/ADR: every denied-to-projected transition requires owner-specific RFC/ADR or accepted owner contract with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: every schema entry has status/value-source-or-denial/evidence/migration/test anchor; missing value source is `не доказано` and `должно оставаться denied`.
- Tests/static scans: generated VMREAD projection tests, denied-field tests, no fallback scans for `TryReadScalarField`, `ReadFieldValue`, `WriteFieldValue`, VMCS store markers.
- Risks: misreading schema presence, completion-owned values, or read-only view fields as field availability.
- Next-gate dependency: Phase 05 records the CR0/CR4 guarded projection closure; Phase 10 governs writes.

## Phase Goal

Complete the VMREAD field matrix and preserve the rule that schema membership is not field availability. Any expansion remains field-by-field and owner-specific.

## Historical Baseline (2026-06-11)

Projected fields:

- Completion-owned/recomputed: `ExitReason`, `ExitQualification`, `GuestPhysicalAddress`, `EptViolationQualification`.
- Memory-owned/descriptor-owned: `GuestCr3`, `EptPointer`, `Vpid`, `Cr3TargetCount`.
- Execution-owned/descriptor-owned: `GuestPc`, `GuestSp`, `GuestFlags`.
- Privileged execution-state descriptor-owned and revalidated after restore: `GuestCr0`, `GuestCr4`, only when owner, read-only source, secure visibility, migration classification, and conformance proof all pass.

Denied fields/categories:

- `HostPc`, `HostSp`, `HostFlags`, `HostCr0`, `HostCr3`;
- compatibility-control fields;
- unknown fields;
- every write path.

## ISE-VMREAD-DENIED-SURFACES-09 - Closure Record

Closure date: 2026-06-18.

Closure state: closed `DENIED-BASELINE / NO-PROJECTION-WIDENING`.

This closure records the current denied VMREAD surfaces that follow the implemented guarded `GuestCr0`/`GuestCr4` projection. It closes the audit assumption only; it does not create host owners, host address-space owners, control-bit value contracts, write owners, completion publication, or retire publication.

Verified denied surfaces:

| surface | code evidence | current result | reopen requirement |
| --- | --- | --- | --- |
| host execution aliases: `HostPc`, `HostSp`, `HostFlags`, `HostCr0` | `VmcsReadOnlyValueProjectionService` returns `HostExecutionStateOwnerMissing` | denied | neutral host-execution owner RFC/ADR |
| `HostCr3` | `VmcsReadOnlyValueProjectionService` returns `HostAddressSpaceOwnerMissing` before any memory translation fallback | denied | neutral host-address-space owner RFC/ADR |
| compatibility-control VMREAD values | `CompatibilityControlValueProjectionDenied` and `KeepsControlValuesUnprojected` | denied | accepted neutral control-bit value contract |
| unknown fields | generated schema lookup denial | denied | generated schema entry plus owner/value/evidence/migration/test package |
| writes through VMREAD/VMCS vocabulary | no value projection may imply write permission | denied | Phase 10 write-owner RFC/ADR |

Closure invariants:

- `ExecutionDomainReadOnlyStateView` is not a host execution value source.
- `MemoryDomainReadOnlyTranslationView` is not a `HostCr3` value source.
- `CompatibilityControlDescriptor` is fail-closed vocabulary and not a control-bit projector.
- Projection denial is not backend execution, completion publication, retire publication, migration authority, or owner acceptance.
- Any future widening remains field-specific and requires owner/value/evidence/migration/test evidence before implementation.

## Owner Of Authority

Only the neutral owner named by `VmcsFieldProjectionSchemaEntry.Owner` can provide values, and only when the value source is explicitly implemented and evidence policy allows it. The schema itself is not the authority owner, and schema membership is not field availability.

## What Can Be Implemented

- A table for every generated schema entry with columns: field, owner, value source, access policy, evidence class, migration policy, current result, denial reason, and test anchor.
- Additional negative tests for denied fields and no fallback paths.
- Optional future field work only after owner-specific RFC/ADR when a neutral value source exists.

## What Remains Denied/Future-Gated

- Any `GuestCr0`/`GuestCr4` request missing a materialized owner, canonical field kind, legal/required bits, matching domain/address-space/epoch, read-only source, secure visibility, `RevalidatedAfterRestore`, or conformance proof.
- Host aliases until separate neutral host owners exist.
- Compatibility controls until a neutral control-bit value contract exists.
- Unknown fields.
- All writes.

## Forbidden Shortcuts

- Reusing `ExecutionDomainReadOnlyStateView` for CR0/CR4 or host execution aliases; the implemented CR0/CR4 value source is the separate neutral privileged execution-state descriptor.
- Reusing `MemoryDomainReadOnlyTranslationView` for `HostCr3`.
- Mapping compatibility controls to fake zero values.
- Falling back to `TryReadScalarField`, raw VMCS scalars, `ReadFieldValue`, or mutable stores.
- Treating `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as read authorization.

## Required RFC/ADR

No RFC/ADR is required for the matrix. Every denied-to-projected transition requires a field owner RFC/ADR or an already accepted owner contract.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Descriptors/ExecutionDomain/ExecutionDomainReadOnlyStateView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Memory/Translation/MemoryDomainReadOnlyTranslationView.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/04_vmread_field_by_field_projection_plan.md`
- `Documentation/Virtualization WhiteBook/10_VMCS_Projection_And_Field_Access.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`

## Required Tests

- Existing: generated read-only, memory-owned, execution-owned, privileged CR0/CR4 read-only projection, and control-like VMREAD tests.
- Add: generated schema full-coverage test requiring every entry to have current status, value source or denial reason, evidence class, migration policy, and test anchor.
- Add: no fallback test for `TryReadScalarField`, `ReadFieldValue`, `WriteFieldValue`, control mapper, and VMCS store markers.

## Required Static/Source Scans

```powershell
rg -n "TryReadScalarField|ReadFieldValue|WriteFieldValue|VmcsFieldStore|ProjectCompatibilityControl|case VmcsField.GuestCr0|case VmcsField.GuestCr4" HybridCPU_ISE/CloseToHSL/Core/Virtualization
```

Expected result: no production value-source fallback outside explicit denial text or tests.

## Migration/Evidence Classification

- Completion-owned fields: `RecomputedCompletion`, never checkpoint authority, and never backend success permission.
- Descriptor-owned fields: migration only through neutral descriptor state, never through VMREAD output.
- Projection-only/host aliases/control fields: not migration authority.
- Unknown/write fields: denied.

## Completion/Retire Implications

VMREAD projection does not publish completion or retire effects. Completion-owned VMREAD values require a neutral `CompletionRecord` source that was produced by a valid publication path; the VMREAD itself is not publication permission.

## Exit Criteria

- Every generated schema field has an explicit matrix row.
- Current allowed and denied sets are documented and tested.
- No field can move from denied to projected without owner/evidence/migration/test package.

## Dependency On Previous/Next Phase

Depends on Phases 01-03. Phase 05 is the candidate owner RFC for `GuestCr0`/`GuestCr4`; Phase 10 covers writes.
