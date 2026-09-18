# Phase 35 - a594 Clean-SHA Evidence And Disabled Repository-Owner Review Workflow

Status date: 2026-08-08

Current-state precedence: this is historical clean-SHA/workflow evidence. Current D2/O1/E1-E7 and gate status is governed only by `VirtualizationActivationStatusV1.json`.

Status: `CLOSED/EVIDENCE-ONLY AND FAIL-CLOSED WORKFLOW`; its historical workflow cannot accept D2. Later PR-B closes attributable machine D2 independently.

## 2026-08-09 Current-State Reconciliation

This phase remains evidence for clean subject `a594d10...` and for the disabled
v1 review workflow only. It is not the current D2 acceptance mechanism. PR-A
later adds v2 governance, PR-B supplies real CODEOWNERS/review artifacts and an
accepted exact spec/record pair, and PR-C derives fault-only O1/operand identity.
Those later facts do not retroactively turn this disabled workflow into authority.

## 2026-06-11 Audit Contract

- File name: `35_a594_clean_sha_evidence_and_disabled_review_workflow.md`.
- Purpose: record local CI reproduction for the Phase 34 commit and the disabled repository-owner review workflow.
- Status: clean-SHA Baseline evidence and additional negative validation are closed; no review may accept a decision or appoint an owner.
- Scope: `a594d10` local provenance, malformed-SHA/reviewer/duplicate-leaf/incomplete-map denials, and review workflow preparation.
- No-goals: no remote Git operation, CODEOWNERS creation, owner appointment, RFC/ADR acceptance, leaf allocation, E2 issuer, backend, canonical wiring, completion or retire.
- Code anchors: `VirtualizationOperationDecisionManifest.cs`, `DisabledVirtualizationRepositoryOwnerReviewWorkflow.cs`, `VmxD2DecisionAndE2NegativeSubstrateTests.cs`.
- Authority owner: `UNASSIGNED`; repository review workflow is a non-authoritative denied-state helper, not an owner plane.
- Required RFC/ADR: an attributable neutral runtime owner must still accept one SHA-bound D2 artifact with the complete field/operation, owner, value source, capability policy, evidence class, migration class, denial reason map.
- Acceptance criteria: local Baseline provenance binds `a594d10`; malformed SHA, reviewer mismatch, duplicate leaves and incomplete map deny; workflow has no approve/accept/appoint/execute operation and returns no authority.
- Tests/static scans: Baseline provenance, `VmxD2DecisionAndE2NegativeSubstrateTests`, plan guards, full VMX-refactoring suite and shortcut/candidate scans.
- Risks: mistaking a locally green Baseline, a matching reviewer name, a disabled workflow or `Accepted` vocabulary for owner acceptance.
- Next-gate dependency: attributable D2 was later closed independently by PR-B. Production E2 remains blocked on a separately authorized live SafetyVerifier/capability/root/restore contour; evidence/negative checks and the closed Phase 36/37 TESTING-only research lane remain non-authority.

## Clean-SHA Evidence

Clean local commit `a594d10abcbe8593d23fed16310af30706893452` (tree `8f6643d2fb4628284f93aa0ae9a927f26668674e`) was used for `Baseline`. The local artifact `artifacts/validation/20260808-023251-Baseline/provenance.json` reports the same SHA and `outcome: passed` for restore, build, test discovery, focused fault-tail test and authority inventory. The manifest is local-only and records no remote lookup or external file substitution.

This evidence proves the D2/E2 negative substrate is reproducible at that commit. It does not create a D2 artifact, authorize a real reviewer, validate a remote service, or alter VMX execution.

## Additional Fail-Closed Validation

The validator now rejects malformed commit SHA before attribution, rejects an approval list that does not cover every required reviewer, distinguishes duplicate leaves from general wrong cardinality, and rejects incomplete owner-map fields before any leaf evaluation. Duplicate-leaf tests use only an unallocated default-shaped structural array; they do not introduce a selected, reserved or positive leaf value.

## Disabled Review Workflow

`DisabledVirtualizationRepositoryOwnerReviewWorkflow` accepts only a review request for diagnosis. It returns `DeniedCodeOwnersAttributionAbsent` when no matching CODEOWNERS evidence is supplied, and `DeniedWorkflowDisabled` otherwise. It exposes no approve, accept, appoint or execute method. Its result reports owner appointment, decision acceptance and backend execution as false.

No `CODEOWNERS` file or mapping is created. Thus the workflow is explicitly unable to establish the attribution it checks, and it cannot become a self-approval mechanism.

## Remaining Blocker And Next Permitted Pool

The blocker recorded by this phase was later closed by the attributable PR-B SpecV2/AcceptanceRecordV2 and review artifacts; PR-C subsequently materializes only O1/operand identity. The remaining blocker is production E2: no live grant, root-authority epoch, real restore lifecycle integration or SafetyVerifier-only D2-bound issuer exists. E3 backend work, production execution connection, completion and retire remain forbidden.
