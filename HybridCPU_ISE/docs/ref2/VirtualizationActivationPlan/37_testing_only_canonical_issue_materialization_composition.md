# Phase 37 - TESTING-Only Canonical Issue/Materialization Composition

Status date: 2026-08-08

Status: `CLOSED/PROTOTYPE-P2/TESTING-ONLY`; production stages later closed independently through PR-I, while P2 remains non-authority and compatibility VMX remains fault-only.

Current-state precedence: this file records the historical P2 checkpoint. Use `VirtualizationActivationStatusV1.json` for current production stages and gates.

## 2026-06-11 Audit Contract

- File name: `37_testing_only_canonical_issue_materialization_composition.md`.
- Purpose: close P2 by composing the Phase 36 no-state/no-payload probe at the real canonical issue/materialization seam without opening a production execution or publication path.
- Status: closed prototype evidence only; P2 satisfies no production D2/E2-E7 gate. D2 is later closed independently by PR-B; compiler/release gates remain unopened.
- Scope: default-off TESTING-only seam, live E1, typed runtime context lease, SafetyVerifier-issued prototype admission, exact-once receipt, replay/squash/slot/identity/generation/owner/policy/revocation/duplicate diagnostics.
- No-goals: no owner appointment, accepted RFC/ADR, numeric leaf, production backend, compatibility frontend/dispatcher connection, architectural result, completion, retire, VMWRITE, nested, SecureCompute, lane/stream, compiler emission or activation claim.
- Code anchors: `MicroOpScheduler.SMT.cs`, `MicroOpScheduler.ResearchVirtualizationCanonicalComposition.cs`, `RuntimeLegalityService.ResearchVirtualizationCanonicalComposition.cs`, `ResearchVirtualizationRuntimeProbe.cs`, `SafetyVerifier.ResearchVirtualizationProbeAdmission.cs`.
- Authority owner: canonical SafetyVerifier owns prototype admission; the TESTING-only neutral runtime owner owns only probe policy/execution; the scheduler hook, diagnostics, VMX and VMCS own no authority.
- Required RFC/ADR: none for P2; later PR-B's attributable exact operation/leaf D2 contains the complete `field/operation, owner, value source, capability policy, evidence class, migration class, denial reason` map, remains independent and does not promote P2.
- Acceptance criteria: P2 is absent from non-TESTING output, disabled by default under TESTING, consumes only a live canonical E1, materializes one typed exact-once receipt, denies every required negative scenario, and leaves production fault/retire behavior unchanged.
- Tests/static scans: focused P1/P2/E1 tests, full `FullyQualifiedName~VmxRefactoring`, Release without TESTING, 150-iteration diagnostics matrix, diff check and shortcut/caller/leaf scans.
- Risks: treating the canonical location or a green runtime profile as production composition authority, production E2/E3, architectural execution, completion or retire.
- Next-gate dependency: no P3 is authorized by the current register. After later PR-B/PR-C, production remains blocked at separately authorized E2. Only documentation, governance/evidence maintenance and fail-closed negative work are safe without new authority.

## Closed Scope

The canonical materialization path already attaches E1 in `TryAttachVirtualizationAdmissionAfterCanonicalLaneMaterialization`. P2 adds one declaration-only partial hook after successful E1 validation. Its implementation exists only under `TESTING`; when TESTING is absent the compiler removes the unimplemented partial call. When TESTING is present the composition object is still null until an internal test/diagnostics caller explicitly arms it.

The armed composition receives the exact replay phase, bundle identity, carrier, source slot, working slot and E1 observed by the canonical seam. A pre-issue typed context lease binds VT, owner context, domain, address space, capability generation, evidence generation and restore generation. SafetyVerifier revalidates E1 and issues the existing prototype certificate; the neutral research owner consumes it exactly once and returns the existing opaque no-state/no-payload receipt.

P2 does not capture VMCALL operand register values. The carrier/compatibility payload retains `rs1`/`rs2` selector identities, and P2 deliberately has no numeric leaf. Therefore P2 is not the production O1/operand snapshot and cannot satisfy E2. Later PR-C separately binds actual values to the live E1 attempt and forbids backend/retire register-file re-read, without promoting P2 or issuing E2.

No production dispatcher, compatibility frontend, completion service or retire path calls a research type. The production `VmxMicroOp.Execute` and canonical retire outcome remain fault-only with no register writeback.

## Dependencies

- Phase 36 P1 and the existing E1 fault-only canonical transport.
- Canonical `CloseToHSL` issue/materialization seam.
- `DefineTestSupport=true`/`TESTING` for tests and diagnostics only.
- Phase 33 audit constraints and Phase 19 blocker register.

P2 does not depend on or satisfy D2. It consumes no accepted manifest and carries no leaf.
No P3 is authorized by the current dependency map.
It introduces no numeric leaf or implied leaf reservation.

## Allowed Changes

- A production-neutral declaration-only partial observation point after live E1 validation.
- `#if TESTING` composition, typed context leases, structured results and internal diagnostics access.
- P2 focused tests, structured counters/traces and plan documentation.

## Forbidden Shortcuts

- A numeric leaf or an implied reservation/name selection.
- Production backend admission or `BackendExecutionAuthorized: true`.
- Any connection from compatibility frontend, `ExecutionDispatcherV4`, completion or retire to P2.
- A `CompletionRecord`, register result, state mutation, retire grant or architectural-success interpretation.
- Using VMX/VMCS, diagnostics, tests or the canonical seam location as runtime authority.

## Required Positive Checks

- Default-off composition produces no receipt while E1 attaches normally.
- Armed P2 materializes one live E1, typed context and SafetyVerifier prototype certificate into one opaque receipt.
- Receipt identities exactly preserve VT/context/domain/address-space/capability/evidence/restore/replay/attempt bindings.
- Receipt remains zero-payload, zero-state and completion/retire denied.

## Required Negative Checks

- Stale E1, replay mismatch and explicit squash/revocation deny.
- Wrong source or working slot denies.
- VT, owner-context or domain mismatch denies; foreign address-space/context lease denies.
- Stale capability, evidence or restore generation denies before admission.
- Foreign owner, stale policy, stale context and post-admission E1 revocation deny.
- Duplicate and concurrent consumption yield exactly one receipt.
- Research sources are TESTING-only and absent from production dispatcher/frontend/completion/retire callers.

## Diagnostics Evidence

`research-canonical-composition` is a separate runtime-contract profile. For each requested iteration it records structured counters and an NDJSON trace for the exact identity/generation/slot binding, executes every fail-closed scenario above, checks concurrent exact-once consumption and rechecks production VMX fault/retire behavior. The required run is 150 iterations.

Diagnostics success is model/testing evidence only. It is not production authority or release evidence.

## Proven Facts

- P2 is default-off and TESTING-only.
- The live canonical issue/materialization seam supplies E1 and the exact source/working slots.
- It does not supply an accepted runtime leaf value; compatibility qualification still contains selector numbers only.
- SafetyVerifier remains the prototype admission issuer.
- One typed research receipt can be materialized exactly once with all required identities and generations bound.
- Production VMX execution and retire remain fault-only.

## Model/Testing Evidence Only

- The no-state/no-payload probe demonstrates liveness and invalidation composition under tests.
- The diagnostics counters/traces demonstrate the bounded P2 contract in isolated processes.
- Neither fact proves a numeric ABI, backend semantics, completion, retire, migration or release readiness.

## Blocked Production

Production D2 is later closed independently by PR-B with attributable reviewer/CODEOWNERS evidence and one exact leaf; PR-C later supplies non-authority O1/operand identity. P2 satisfies neither closure and remains TESTING-only. Production E2-E7 remain blocked or future-gated in their strict dependency model; compiler and release gates remain unopened.

## Future-Gated

Any production operation-specific certificate, backend receipt, canonical production composition, completion, retire, migration/determinism, compiler emission or release work. No P3 research pool is authorized by the current dependency map.

## Owner Of Authority

SafetyVerifier owns prototype admission. The neutral TESTING-only runtime owner owns probe policy and execution. The scheduler seam observes composition but owns no operation authority. VMX/VMCS remain frozen compatibility ABI only.

## What Can Be Implemented

Without new authority: documentation, D2 governance intake, evidence refresh and fail-closed negative/static checks. A new positive research pool requires an explicit bounded authorization in the normative register.

## What Remains Denied/Future-Gated

Production E2-E7, separate compiler/release gates, plus VMWRITE, nested, SecureCompute, lane/stream and broad virtualization activation. Accepted D2 and PR-C identity objects grant P2 no production authority.

## Required RFC/ADR

No RFC/ADR is consumed by P2. The later accepted D2 owner-specific record remains outside P2 and cannot promote it.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.SMT.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/Scheduling/Smt/MicroOpScheduler.ResearchVirtualizationCanonicalComposition.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/Certificates/RuntimeLegalityService.ResearchVirtualizationCanonicalComposition.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/ResearchVirtualizationRuntimeProbe.cs`
- `HybridCPU_ISE.Tests/VmxRefactoring/ResearchVirtualizationCanonicalCompositionTests.cs`
- `VirtualizationDiagnosticsConsole/Scenarios/ResearchCanonicalCompositionScenario.cs`

## Documentation Anchors

- `17_phase_rollout_and_pr_order.md`
- `19_open_decision_backlog.md`
- `33_vrt_external_audit_reconciliation_and_blocker_decisions.md`
- `36_testing_only_research_runtime_probe.md`

## Required Tests

- `ResearchVirtualizationCanonicalCompositionTests` plus Phase 36 P1/E1 focused tests.
- `VirtualizationDiagnosticsConsole research-canonical-composition --iterations 150`.
- Full `FullyQualifiedName~VmxRefactoring`.
- Release build with `DefineTestSupport=false` and `EnableInternalTestHooks=false`.

## Required Static/Source Scans

- No numeric VMCALL leaf or candidate value introduced.
- No allowed production backend or `BackendExecutionAuthorized: true` introduced.
- No `CompletionRecord`, completion/retire shortcut or production caller of research types.
- All P2 implementation files begin with `#if TESTING`; the canonical production seam names only a removable partial observation hook.

## Migration/Evidence Classification

Receipt and context lease are ephemeral testing evidence. They are neither serialized nor restored. Capability/evidence/restore generation changes invalidate a pre-issue lease.

## Completion/Retire Implications

None. The receipt authorizes neither. VMX execution still produces `SecurityPolicyViolation`, and retire remains faulted without register writeback.

## Exit Criteria

- Focused and full VMX-refactoring tests pass.
- The dedicated diagnostics profile and matrix pass at 150 iterations.
- Release without TESTING builds successfully.
- Mandatory scans and `git diff --check` pass.
- Phase 17/19 and diagnostics README retain the production blocker verdict.

## Exit And Rollback

Closure proves only P2 testing composition. Rollback deletes the two P2 TESTING-only source files, focused tests/profile and this document, then removes the declaration-only partial hook call. P1 and production fault-only behavior remain intact.

## 2026-08-08 Closure Evidence

Evidence subject is local `HEAD` `ddfffa2d7b86fb3ece21f8d21c6447ae47ee3868` plus the dirty working tree. It is not a clean containing-SHA claim.

- Focused P1/P2/E1 tests: 29 passed, 0 failed.
- Phase-plan/P2 guard selection: 57 passed, 0 failed.
- Full `FullyQualifiedName~VmxRefactoring`: 189 passed, 0 failed.
- Diagnostics matrix: all six profiles passed at 150 iterations in isolated processes.
- P2 diagnostics result: 150/150 iterations, 5,700 assertions, 150 positive receipts, 150 checks for each named denial class, 1,050 duplicate-consumption denials and 150 unchanged production fault/retire checks.
- Release without TESTING: succeeded with 0 errors; scan of `HybridCPU_ISE.dll` found no research/P2 symbol or partial-hook implementation. Assembly SHA-256: `b471301b0252dae977b16467d126da9a3dbf488b91cc452b422434d26925f604`.
- `git diff --check`: passed; line-ending conversion warnings are advisory and no whitespace error was reported.
- Production research-type callers: 0. Production allowed-backend/`BackendExecutionAuthorized: true` matches: 0. P1/P2 numeric-leaf literals: 0. P2 dispatcher/`CompletionRecord`/`VmxRetireEffect`/completion/retire shortcuts: 0.
- Existing completion/retire-true matches remain confined to unchanged neutral `TrapCompletionRoutePolicy.cs`; P2 did not edit or call it.

## Dependency On Previous/Next Phase

Depends only on Phase 36 P1 and live E1 as a research carrier. It does not close or bypass D2. Later PR-B/PR-C close D2/O1/operand independently; the next production gate is separately authorized E2, and no further positive research phase is currently authorized.
