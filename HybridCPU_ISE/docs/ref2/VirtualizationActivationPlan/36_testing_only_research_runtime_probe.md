# Phase 36 - TESTING-Only Research Runtime Probe

Status date: 2026-08-08

Status: `CLOSED/PROTOTYPE-P1/TESTING-ONLY`; production stages later closed independently through PR-I, while P1 remains non-authority and compatibility VMX remains fault-only.

Current-state precedence: this file records the historical P1 checkpoint. Use `VirtualizationActivationStatusV1.json` for current production stages and gates.

## 2026-06-11 Audit Contract

- File name: `36_testing_only_research_runtime_probe.md`.
- Purpose: close the first research prototype pool with a neutral, state-minimal/no-payload operation while preserving every production activation gate.
- Status: prototype execution exists only when `TESTING` is defined; it is not production E2/E3 and does not approve activation.
- Scope: live E1 revalidation, typed runtime identities, SafetyVerifier-only prototype operation admission, neutral exact-once execution, opaque receipt, negative invalidation tests and diagnostics.
- No-goals: this prototype does not create or consume accepted D2, a numeric VMCALL leaf, production backend, dispatcher composition, architectural result, completion, retire, VMREAD execution, VMWRITE, nested, SecureCompute, lane/stream, migration payload, compiler emission or release claim.
- Code anchors: `SafetyVerifier.ResearchVirtualizationProbeAdmission.cs`, `ResearchVirtualizationRuntimeProbe.cs`, `ResearchVirtualizationRuntimeProbeTests.cs`, `VirtualizationDiagnosticsConsole/Scenarios/ResearchRuntimeProbeScenario.cs`.
- Authority owner: existing SafetyVerifier owns prototype admission; a TESTING-only neutral runtime object owns only probe policy and execution; VMX/VMCS compatibility objects own neither.
- Required RFC/ADR: none for this non-production, no-leaf experiment; later PR-B's accepted owner-specific D2 contains the complete `field/operation, owner, value source, capability policy, evidence class, migration class, denial reason` map but does not promote or authorize this prototype.
- Acceptance criteria: live E1 can produce one non-public prototype certificate; one neutral owner consumes it exactly once; stale/foreign/duplicate paths deny; receipt carries no payload, state mutation, completion or retire authority; non-TESTING builds exclude the feature.
- Tests/static scans: focused prototype tests, 150-iteration diagnostics, full VMX-refactoring suite, Release build without `TESTING`, and scans for compatibility/leaf/publication shortcuts.
- Risks: mistaking a TESTING-only receipt, green diagnostics or the VMCALL-shaped E1 carrier for production E2/E3, an accepted leaf, architectural execution or release authority.
- Next-gate dependency: prototype P2 is closed separately in Phase 37; after later PR-B/PR-C, production work remains blocked on separately authorized E2 rather than D2.

## Adopted Audit Decisions

The two VRT reports are recommendations, not authority. Their compatible recommendations are applied here as follows:

- neutral means outside the VMX/VMCS compatibility authority plane, not necessarily outside the repository;
- SafetyVerifier remains the only admission issuer; the runtime owner cannot self-authorize;
- the first executable experiment is a no-state/no-payload liveness probe, not YIELD, ABI query, SecureCompute or a stateful service;
- E1 admission, operation admission and execution receipt are distinct typed facts;
- all identities are bound to one live attempt: VT, owner context, domain, address space, carrier attempt, replay epoch, capability generation, evidence generation and restore generation;
- E1 revocation before issuance and after operation admission denies execution;
- operation admission and execution are exact-once and authorize neither completion nor retire;
- the audit's illustrative probe name and numeric value are not selected, reserved or copied into code.

## Implemented Prototype Boundary

| Layer | Prototype owner | Implemented fact | Explicit non-authority |
| --- | --- | --- | --- |
| E1 carrier | canonical `SafetyVerifier` | existing live, attempt-bound fault-only certificate | no leaf, backend, completion or retire |
| runtime identity | `ResearchVirtualizationOperationContext` | opaque live snapshot over VT/context/domain/address-space/capability/evidence/restore generations | no VMCS state store and no caller boolean admission |
| prototype operation admission | `SafetyVerifier` | revalidates E1 and all carrier identities, then issues a non-public certificate | not production E2; no numeric leaf or D2 consumption |
| prototype execution | `ResearchVirtualizationRuntimeOwner` | consumes one live certificate exactly once and rejects foreign/stale policy/context/admission | no VMX/VMCS/VMCALL ABI authority |
| execution evidence | opaque `ExecutionReceipt` | deterministic liveness result with zero payload and zero state mutations | not `CompletionRecord`, not retire grant and not architectural success |
| diagnostics | `research-runtime-probe` | repeats positive and negative contracts in an isolated process with structured artifacts | green diagnostics do not prove production activation |

This is a direct research service path. It is not connected to `ExecutionDispatcherV4`, the production VMCALL admission service, completion publication or retire. `VmxMicroOp.Execute`, dispatcher execution and canonical retire behavior remain unconditional fault for VMX.

## Dependencies

- Closed E0 evidence and closed fault-only E1 contract.
- Canonical `CloseToHSL` source tree.
- `DefineTestSupport=true`, which defines `TESTING`; Release/non-test builds exclude both prototype source files.
- Reconciled recommendations in `33_vrt_external_audit_reconciliation_and_blocker_decisions.md`.

Production D2 is deliberately not a dependency because this phase does not implement a VMCALL leaf or production backend. Conversely, this phase cannot satisfy or bypass D2.

## Allowed Changes

- `#if TESTING`-guarded typed prototype contracts in neutral runtime and SafetyVerifier namespaces.
- Static and runtime negative checks for forging, identity mismatch, invalidation and duplicate execution.
- Isolated `VirtualizationDiagnosticsConsole` scenario and structured counters/traces.
- Documentation that distinguishes prototype evidence from production authority.

## Forbidden Shortcuts

- Adding any numeric leaf, accepted owner artifact or implied leaf reservation.
- Returning a production allowed backend admission or adding `BackendExecutionAuthorized: true`.
- Calling the research owner from the production dispatcher, VMX frontend, completion route or retire path.
- Constructing `CompletionRecord` or treating `ExecutionReceipt`/`VmxRetireEffect` as completion or architectural success.
- Using VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane or stream as authority.
- Widening `GuestCr0`/`GuestCr4` beyond the existing guarded read-only projection.
- Claiming VMREAD architectural execution or broad VMX/virtualization activation.

## Required Positive Checks

- A live E1 carrier and live typed runtime snapshot cause SafetyVerifier to issue one prototype operation certificate.
- The matching neutral owner consumes the certificate once and returns an opaque zero-payload/zero-state receipt.
- The diagnostics scenario completes 150 iterations and records one execution plus all required denials per iteration.

## Required Negative Checks

- Foreign or invalidated runtime identity snapshot denies issuance.
- Carrier VT/context/domain/attempt/replay mismatch denies issuance.
- Stale E1 denies issuance; E1 revocation after prototype admission denies execution.
- Foreign neutral owner, stale owner policy, stale runtime context and duplicate attempt deny execution; concurrent consumption of one certificate yields exactly one receipt.
- Certificate, policy snapshot and receipt have no public constructor.
- Neutral owner source has no VMX, VMCS, VMCALL, `CompletionRecord`, `VmxRetireEffect` or backend-authorization dependency.
- SafetyVerifier source creates no completion record and cannot authorize completion or retire.

## Exit And Rollback

Exit is closed when the focused tests, 150-iteration scenario, full VMX-refactoring suite, Release build and shortcut scans pass. Closure proves only the TESTING-only research contract.

Rollback is deletion of the two `#if TESTING` source files, their focused tests and the diagnostics scenario. Production behavior is unchanged before, during and after rollback.

## Closed Successor And Next Blocker

Research prototype P2 is closed in Phase 37: the same no-state experiment passes through a default-off TESTING-only canonical issue/materialization composition seam, still with no leaf, architectural result, completion or retire. No P3 is authorized.

The historical next pool was D2. PR-B later closed it with an attributable SHA-bound exact-leaf decision, and PR-C later supplied non-authority O1/operand identity. The current next candidate is a separately authorized production E2 pool; it is not opened by P1/P2 or those closures.

## Owner Of Authority

SafetyVerifier owns prototype admission. The TESTING-only neutral runtime owner owns probe policy and deterministic execution only. The repository plan, diagnostics, VMX frontend and compatibility types are not authority.

## What Can Be Implemented

Only fail-closed maintenance of P1/P2, documentation, governance intake and negative/static checks. A further positive prototype requires a new bounded authorization.

## What Remains Denied/Future-Gated

Production D2 is closed independently by PR-B; this P1 prototype satisfies none of it. Production E2-E7 remain blocked, denied or future-gated according to Phase 17; compiler and release gates remain unopened, as do every unrelated VMX/virtualization surface.

## Required RFC/ADR

No RFC/ADR is consumed by this prototype. The later accepted D2 remains outside P1 and grants it no production meaning.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/SafetyVerifier.VirtualizationAdmission.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/SafetyVerifier.ResearchVirtualizationProbeAdmission.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/ResearchVirtualizationRuntimeProbe.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/ResearchVirtualizationRuntimeProbeTests.cs`
- `VirtualizationDiagnosticsConsole/Scenarios/ResearchRuntimeProbeScenario.cs`

## Documentation Anchors

- `docs/ref2/1/VRT/deep-research-report VRT.md`
- `docs/ref2/1/VRT/Исследование-виртуализации-HybridCPU.md`
- `docs/ref2/VirtualizationActivationPlan/33_vrt_external_audit_reconciliation_and_blocker_decisions.md`

## Required Tests

- `ResearchVirtualizationRuntimeProbeTests`.
- `VirtualizationDiagnosticsConsole research-runtime-probe --iterations 150`.
- Full `FullyQualifiedName~VmxRefactoring` suite.
- Release build with test support disabled.

## Required Static/Source Scans

- No numeric leaf or audit candidate value in production code.
- No production allowed backend admission, backend executor, completion publication or retire shortcut.
- No call from production dispatcher/frontend/retire code to the research types.
- Both prototype source files start with `#if TESTING`.

## Migration/Evidence Classification

The receipt is ephemeral research evidence only. It is not serialized, restored or accepted as migration authority. Restore-generation mismatch invalidates the typed runtime context.

## Completion/Retire Implications

None. Completion and retire flags remain false; no record or grant is created and production VMX still faults.

## Exit Criteria

- All positive and negative checks pass at 150 iterations.
- Release/non-test compilation succeeds with the prototype excluded.
- Static scans find no activation/backend/completion/retire shortcut.
- Documentation explicitly keeps P1 separate from accepted D2 and keeps production E2-E7 plus compiler/release gates unopened.

## Dependency On Previous/Next Phase

Depends on E0/E1 only as a research carrier. It does not depend on, close or bypass D2. P2 is closed in Phase 37; later PR-B/PR-C do not promote either prototype, the next production gate is separately authorized E2, and no P3 is authorized.
