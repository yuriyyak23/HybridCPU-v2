# Phase 01 - Current State And Scope

## Goal

Define the current SecureCompute scope as an activation-readiness refactoring plan, not a production implementation claim. This phase establishes that the new `SecureComputerefactoringNew` files update `deep-research-report (7).md` against the live codebase while preserving the existing `Plan/00-13` and `Plan2/14` closure history.

## Current Code Baseline

The live baseline contains neutral SecureCompute descriptors, admission hooks, policy objects, VMX compatibility denial/projection guards and tests. The key anchors are `SecureComputeDomainDescriptor`, `DomainRuntimeContext`, `RuntimeBoundaryAdmissionService`, `SecureMemoryDomainDescriptor`, `SecureEvidencePolicy`, `SecureMigrationDescriptor`, `SecureIoHypercallAdmissionPolicy`, `SecureGrantAuthorityPolicy`, `SecureComputeCompatibilityBoundaryMatrixPolicy` and `SecureBackendOwnerAdmissionPolicy`.

The repo-level WhiteBook classifies the current state as a bounded neutral opt-in runtime descriptor/admission baseline with fail-closed policies, negative conformance, design fences and release gates. Positive secure backend runtime execution remains closed.

## Already Closed / Must Not Reopen

- Existing `Plan/00-13` files are historical closure evidence and must not be rewritten as if they did not exist.
- `Plan2/14-securecompute-open-decision-backlog.md` remains an open-decision quarantine, not an implementation phase.
- Phase 10 and Post-Phase10 closure remain release-gate/proof-only closure, not activation.
- VMX remains a compatibility frontend and not a SecureCompute authority source.

## Required Code/Doc Anchors

- `HybridCPU_ISE/docs/ref2/deep-research-report (7).md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/SecureCompute/Plan2/14-securecompute-open-decision-backlog.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputePlanAuditTests.cs`
- `HybridCPU_ISE.Tests/SecureComputeRefactoring/SecureComputePhase10ReleaseGateTests.cs`

## Work Items

- Keep this directory as a docs-only phase plan.
- Reference the current code paths and tests rather than restating old research as future work.
- Separate `implemented`, `shell`, `no-effect`, `fail-closed`, `proof-only`, `admitted-denied`, `design-fence`, `release-gate` and `future` status classes.
- Mark all positive secure backend runtime execution claims as blocked until Phase 20 and Phase 21 criteria are met.

## Explicit Non-Goals

- No production code changes.
- No SecureCompute product-ready or feature-complete claim.
- No reopening of closed VMX deny/projection, migration/evidence or no-emission guards.
- No activation through documentation closure, test evidence, telemetry, VMX projection or proof-only admission.

## Done Criteria

- Scope is bounded to phased refactoring and activation-readiness.
- Source corpus is listed in the index.
- Current-state statements map to live code/test anchors.
- All future work is routed to explicit future/RFC/ADR phases.

## Required Tests / Static Checks

- Run `rg` for SecureCompute anchors in this directory.
- Run forbidden-claim `rg` against this directory and inspect any hits for forbidden/non-goal context only.
- Run `git diff --check -- HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew`.

## Residual Risk

The largest residual risk is that later readers treat this plan as activation itself. Phase 04 and Phase 21 must keep activation definition and release-gate wording explicit.

## Next Phase Dependency

Phase 02 must define invariants and closure taxonomy before any phase-specific work items can be interpreted.
