# Phase 05 - Privileged Execution State Owner RFC

Status: implemented guarded owner and read-only projection closure. Mutation, backend execution, completion publication, and retire publication remain denied.

## 2026-06-11 Audit Contract

- File name: `05_privileged_execution_state_owner_rfc.md`.
- Purpose: record the implemented neutral privileged execution-state owner and the narrower field-specific read-only `GuestCr0`/`GuestCr4` projection contract.
- Status: owner and guarded read-only projection are implemented; every mutation/backend/completion/retire path remains denied.
- Scope: owner materialization, domain/address-space/epoch binding, bit legality, reserved/required bits, evidence, `RevalidatedAfterRestore`, conformance, projection-only output, host-alias/write denial.
- No-goals: no writes, no host aliases, no compatibility-control values, no VMCS scalar source, no SecureCompute activation, no backend execution or publication authority.
- Code anchors: `PrivilegedExecutionStateDescriptor.cs`, `PrivilegedExecutionStateOwnerPolicy.cs`, `PrivilegedExecutionStateProjectionService.cs`, `VmcsReadOnlyValueProjectionService.cs`.
- Authority owner: neutral privileged execution-state descriptor/policy; VMCS, VMREAD schema, SecureCompute admission, and PC/SP/flags view are not authority.
- Required RFC/ADR: `ADR-SC-PES-GuestCr0Cr4` is the existing owner/projection contract; any widening requires a new owner-specific RFC/ADR with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: projection is allowed only after every owner/source/visibility/migration/conformance gate; all missing gates and all mutation/backend/publication effects remain denied.
- Tests/static scans: `PrivilegedExecutionStateOwnerPolicyTests`, `GuestCr0Cr4ReadOnlyProjectionTests`, VMCS fallback/write scans, host-alias and unsupported-field denials.
- Risks: inferring control-register state from CR3, flags, paging mode, zero defaults, or compatibility controls.
- Next-gate dependency: Phase 04 matrix; any widening requires a new owner-specific RFC/ADR and Phase 10 for writes.

## Phase Goal

Record the implemented contract that exposes only read-only `GuestCr0` and `GuestCr4` values from a neutral privileged execution-state descriptor without using VMCS, guest PC/SP/flags view, or compatibility aliases as authority.

## Historical Baseline (2026-06-11)

`PrivilegedExecutionStateOwnerPolicy` accepts a materialized, domain/address-space/epoch-bound descriptor only as `AllowedOwnerMaterializedProjectionClosed`; its result keeps `ReadOnlyProjectionAuthorized`, mutation, backend execution, completion publication, and retire publication false. A separate `PrivilegedExecutionStateProjectionService` may return `AllowedReadOnlyProjection` only after owner admission plus read-only source, secure visibility, `RevalidatedAfterRestore`, and conformance proof. `VmcsReadOnlyValueProjectionService` then projects only `GuestCr0` or `GuestCr4`.

## Owner Of Authority

The neutral `PrivilegedExecutionStateDescriptor` and `PrivilegedExecutionStateOwnerPolicy`, separate from VMCS and from the PC/SP/flags read-only view. Projection permission belongs to the separate field-specific projection service and is not implied by owner acceptance.

## What Can Be Implemented

- Maintenance of the existing descriptor, owner policy, field-specific projection service, legality checks, evidence/migration classification, and adjacent denials.
- Additional denial tests for stale epoch, binding mismatch, invalid masks, unsupported fields, absent visibility/migration/conformance proof, host aliases, and writes.

## What Remains Denied/Future-Gated

- VMREAD of `GuestCr0`/`GuestCr4` whenever any required owner/source/visibility/migration/conformance fact is absent.
- Any VMWRITE of control registers.
- Host control aliases.
- Compatibility-control VMREAD values.
- Any inference that read-only CR0/CR4 projection authorizes SecureCompute activation, backend execution, completion publication, or retire publication.

## Forbidden Shortcuts

- Reading CR0/CR4 from VMCS scalar fields.
- Inferring CR0/CR4 from `GuestFlags`, paging mode, `GuestCr3`, EPT pointer, or compatibility controls.
- Treating zero/default value as safe projection.
- Opening writes or backend mutation through the read-only owner.

## Required RFC/ADR

The existing owner/projection contract covers:

- exact bit model and reserved-bit policy;
- paging/protection interaction;
- extension feature interaction;
- guest-visible vs host-private evidence;
- migration/checkpoint class;
- no-mutation/no-backend/no-publication behavior;
- compatibility projection shape;
- negative tests for host aliases and writes.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Descriptors/ExecutionState/PrivilegedExecutionStateDescriptor.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Policies/ExecutionState/PrivilegedExecutionStateOwnerPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/SecureCompute/Compatibility/Projection/PrivilegedExecutionStateProjectionService.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/05_privileged_execution_state_owner_decision.md`
- `Documentation/Virtualization WhiteBook/05_Runtime_Domain_Owners.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit4.md`
- `HybridCPU_ISE/docs/VMXRefactoring/audit5.md`

## Required Tests

- Positive read-only tests only after all gates pass.
- Reserved-bit and dependency negative tests.
- Migration classification tests.
- No VMCS fallback and no write path tests.
- Host alias non-reuse tests.
- Explicit assertions that backend success, mutation, completion publication, and retire publication remain false.

## Required Static/Source Scans

```powershell
rg -n "GuestCr0|GuestCr4|PrivilegedExecution|CR0|CR4" HybridCPU_ISE/CloseToHSL/Core
rg -n "case VmcsField.GuestCr0|case VmcsField.GuestCr4|ReadFieldValue|TryReadScalarField|WriteFieldValue" HybridCPU_ISE/CloseToHSL/Core/Virtualization
```

## Migration/Evidence Classification

The owner must mark control state as neutral descriptor-owned state if serializable. VMREAD output is never migration authority. Host-private derived evidence must be host-owned and non-migratable unless a neutral migration policy says otherwise.

## Completion/Retire Implications

Read-only CR0/CR4 projection has no completion or retire publication by itself. Any future write/mutation path requires Phase 10-style neutral write owner and separate publication policy.

## Exit Criteria

- Owner and projection contracts remain field-specific and guarded.
- Owner acceptance alone remains projection-closed.
- Positive tests cover only read-only projection after every gate.
- Adjacent denials and all mutation/backend/publication denials remain green.

## Dependency On Previous/Next Phase

Depends on Phases 03 and 04. It is not required for the recommended first VMCALL activation path unless that path depends on privileged control state.
