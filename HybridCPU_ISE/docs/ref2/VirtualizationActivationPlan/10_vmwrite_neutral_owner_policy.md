# Phase 10 - VMWRITE Neutral Owner Policy

Status: deny-by-default policy. No VMWRITE activation.

## 2026-06-11 Audit Contract

- File name: `10_vmwrite_neutral_owner_policy.md`.
- Purpose: preserve all VMWRITE denial until a neutral write owner exists for an exact state class.
- Status: deny-by-default; no VMWRITE activation.
- Scope: generated schema `CanWrite == false`, VMCS non-store discipline, compatibility-control/write denial, future write RFC requirements.
- No-goals: no VMCS field store, no descriptor mutation from VMX frontend, no SecureCompute/nested/memory/I/O/lane writes through VMWRITE.
- Code anchors: `VmcsFieldProjectionSchema.cs`, `RuntimeBoundaryAdmissionService.cs`, `CompatibilityControlDescriptor.cs`, VMCS/VMWRITE tests under `HybridCPU_ISE.Tests/VmxRefactoring/`.
- Authority owner: future neutral write owner per state class; none exists now.
- Required RFC/ADR: mandatory for any write with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: all generated fields deny writes; missing write owner is `не доказано` and `должно оставаться denied`.
- Tests/static scans: `CanWrite` all-false tests, no `WriteFieldValue`, no `VmWrite`, no `VmcsFieldStore`, no direct mutation markers.
- Risks: treating read projection or schema `ReadWrite` vocabulary as current write permission.
- Next-gate dependency: independent future write-owner RFC/ADR; not part of first no-state VMCALL path.

## Phase Goal

Document the conditions under which VMWRITE could ever open, and preserve current denial until a neutral write owner exists for an exact state class.

## Historical Baseline (2026-06-11)

`VmcsFieldProjectionSchema.CanWrite(...)` returns `false` for every field. Runtime admission denies compatibility frontend authoritative mutation. VMCS is not a mutable state store.

## ISE-VMWRITE-DENY-BASELINE-10 - Closure Record

Closure date: 2026-06-18.

Closure state: closed `DENIED-BASELINE / NO-WRITE-OWNER`.

This closure records the current all-write denial baseline. It closes the audit assumption that VMWRITE must remain denied; it does not implement a neutral write owner and does not authorize mutation through VMX, VMCS, SecureCompute, nested, memory, I/O, lane, compiler, migration, completion, or retire paths.

Verified denial facts:

| write surface | code/test evidence | current result |
| --- | --- | --- |
| generated VMCS schema writes | `VmcsFieldProjectionSchema.CanWrite(...)` is false for every entry | denied |
| compatibility-control writes | write alias validation returns `VmcsFieldAliasDecision.WriteDenied` | denied |
| VMWRITE decode vocabulary | decode can name operand form but creates no write authority | vocabulary only |
| scalar VMCS writes | no `TryWriteScalarField`/mutable scalar store path | absent |
| SecureCompute-sensitive VMWRITE | secure compatibility policy denies mutation | denied |

Closure invariants:

- `ReadOnly` schema access is not write permission.
- VMWRITE opcode compatibility is not a backend mutation path.
- No VMCS field store, active VMCS pointer, runtime manager, or descriptor mutation path may be inferred from this closure.
- Any future write path requires a neutral write-owner RFC/ADR with mutation semantics, rollback, capability, evidence, migration, completion, retire, and adjacent denial tests.

## Owner Of Authority

A future neutral write owner for a specific state class. There is no current write owner for VMCS fields, compatibility controls, execution state, memory state, host aliases, SecureCompute, nested state, or lanes.

## What Can Be Implemented

- Negative tests that all VMWRITE paths remain denied.
- RFC/ADR template for future neutral writes.
- Static scans for field-store and direct mutation markers.
- Documentation stating that read projection does not imply write permission.

## What Remains Denied/Future-Gated

- All VMWRITE.
- Control-register writes.
- Compatibility-control writes.
- SecureCompute writes through VMX.
- Nested state writes through Shadow VMCS/VMCS12/VMCS02.
- Memory/I/O/lane writes through VMCS fields.

## Forbidden Shortcuts

- Creating VMCS field store to support writes.
- Treating `ReadWrite` access policy in schema as current permission.
- Writing into neutral descriptors from VMX frontend.
- Using VMWRITE as SecureCompute or nested activation.

## Required RFC/ADR

Required for any write. It must include neutral owner, mutation semantics, legality A/B interaction, capability grant, evidence class, migration/checkpoint handling, completion/retire publication, rollback, and negative tests.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Services/RuntimeBoundaryAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/CompatibilityControls/CompatibilityControlDescriptor.cs`
- VMCS/VMWRITE tests under `HybridCPU_ISE.Tests/VmxRefactoring/`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/06_vmcs_write_and_compatibility_control_policy.md`
- `Documentation/Virtualization WhiteBook/10_VMCS_Projection_And_Field_Access.md`
- `Documentation/Virtualization WhiteBook/15_Security_Invariants.md`

## Required Tests

- All generated fields deny `CanWrite`.
- VMWRITE cannot mutate execution, memory, I/O, capability, evidence, migration, SecureCompute, nested, completion, or retire state.
- Compatibility-control descriptor keeps writes denied.
- SecureCompute boundary denies VMWRITE.
- No VMCS field store exists.

## Required Static/Source Scans

```powershell
rg -n "CanWrite\\(.*=> true|WriteFieldValue|VmWrite|VMWRITE|VmcsFieldStore|SetField|HardwareWrite|DirectWrite" HybridCPU_ISE/CloseToHSL/Core
```

Any future match must cite an owner-specific RFC/ADR.

## Migration/Evidence Classification

Writes change authoritative state and therefore require a migration/checkpoint story before they are allowed. Without a neutral migration class, writes remain denied.

## Completion/Retire Implications

Any future write path requires completion and retire rules. A write cannot be "fire and forget" through VMX frontend.

## Exit Criteria

- Current all-write denial is test-backed.
- Future write requirements are explicit.
- No active VMWRITE path exists.

## Dependency On Previous/Next Phase

Depends on Phases 02-04. It is independent of the recommended VMCALL no-state activation path and should remain denied for that path.
