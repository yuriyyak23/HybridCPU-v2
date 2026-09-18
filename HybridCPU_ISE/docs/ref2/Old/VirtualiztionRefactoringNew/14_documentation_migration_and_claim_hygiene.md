# Phase 14 - Documentation Migration And Claim Hygiene

## Goal

Migrate the old activation-oriented research plan into current readiness language and prevent stale claims from reentering the docs. The output is clean documentation that distinguishes closed, implemented, projection-only, denied, model/helper-only, future-gated, and forbidden work.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `deep-research-report (6).md` contains useful phase structure and owner-first principles, but it also carries older activation framing and stale placement assumptions.
- Current Virtualization WhiteBook chapter 16 is the stronger current-state baseline.
- SecureCompute WhiteBook is the stronger SecureCompute baseline.
- Stream WhiteBook is the stronger Stream/Lane6/Lane7 baseline and supersedes stale fail-closed assumptions for the bounded DSC1 contour.
- Phase 13 closed executable static gates and focused owner-specific tests; Phase 14 consumes those gates for documentation migration only.

## Already Closed / Must Not Reopen

- Do not move old activation text forward without current-state correction.
- Do not preserve stale folder placement from the old report; this corpus lives in `HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew`.
- Do not claim closed baseline work as new implementation work.
- Do not delete unrelated artifacts from the target folder if future runs find them there.
- Do not convert documentation cleanup into production runtime, compiler, VMCS, SecureCompute, Lane6/Lane7, completion, or retire changes.

## Required Code/Doc Anchors

- `HybridCPU_ISE/docs/ref2/deep-research-report (6).md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/11_DmaStreamCompute_And_Assist_Separation.md`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxDocumentationFinalClosureTests.cs`

## Work Items

- Replace "activate now" language with readiness and future-gate language.
- Add claim boundaries to every phase.
- Mark current VMREAD projections as implemented projection-only.
- Mark VMCALL backend, privileged/control/host VMREAD fields, VMCS writes, nested execution, SecureCompute backend execution, and broad Stream/Lane7 expansion as denied or future-gated.
- Record source-of-truth precedence when old prompt text conflicts with current whitebooks/code.
- Close this phase as documentation/readability/static-claim safety only.

## Explicit Non-Goals

- Do not edit production code.
- Do not rewrite the full whitebooks.
- Do not remove historical documents.
- Do not stage or commit unrelated repository changes.
- Do not issue a production work order from claim hygiene alone.

## Done Criteria

- Every phase file uses the required section skeleton.
- The index records source corpus, reading order, dependency graph, current state, full file list, forbidden regressions, and classification rules.
- New docs avoid overclaim phrasing.
- New docs explicitly cite current code anchors.
- Old activation-oriented recommendations are superseded or rewritten as future-gated owner RFC material.
- Phase 15 records Phase 14 as closed for documentation claim hygiene only.

## Required Tests / Static Checks

- `FullyQualifiedName~VmxDocumentationMigrationClaimHygieneTests`
- Documentation skeleton scan:
  - `rg -n "^# Phase|^## Goal|^## Activation Authority|^## Current Code Baseline|^## Already Closed / Must Not Reopen|^## Required Code/Doc Anchors|^## Work Items|^## Explicit Non-Goals|^## Done Criteria|^## Required Tests / Static Checks|^## Residual Risk|^## External Audit Risk Update|^## Next Phase Dependency" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md"`
- Claim status vocabulary scan:
  - `rg -n "implemented|projection-only|denied|model/helper-only|future-gated|forbidden" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md"`
- Documentation overclaim scan, expected `NO_MATCH` outside static-gate command text:
  - `rg -n "runtime activation approved|activation approved|production ready|SecureCompute supported via VMX|VMWRITE.*allowed|VMCALL backend success.*allowed|completion publication.*allowed|retire publication.*allowed|examples.*production authority" HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew --glob "*.md" --glob "!13_conformance_golden_artifacts_and_static_gates.md"`
- `git diff --check -- "HybridCPU_ISE/docs/ref2/VirtualiztionRefactoringNew"`

## Closure Decision - ADR-VIRT-DOC-CLAIM-HYGIENE-2026-06-05

Phase 14 is closed as documentation migration and claim hygiene only. It updates the review corpus to consume the Phase 13 gates and preserve current-state language; it does not modify production runtime code, compiler code, VMCS mutation code, SecureCompute backend code, Lane6/Lane7 runtime code, Stream backend code, completion publication code, or retire publication code.

The migrated corpus uses the following status vocabulary:

| Status | Meaning in this corpus |
| --- | --- |
| `implemented` | Production code exists behind the named neutral owner and tests document the authority boundary. |
| `projection-only` | Compatibility vocabulary can expose a read-only/generated value after runtime admission, but it owns no state or mutation. |
| `denied` | The current correct behavior is explicit denial or fail-closed result with a named reason. |
| `model/helper-only` | Helper, evidence, telemetry, diagnostic, parser, or whitebook model surface exists but does not authorize runtime behavior. |
| `future-gated` | The shape is known, but an owner, policy, evidence class, route, publication rule, or conformance gate is missing. |
| `forbidden` | The item would regress a closed authority boundary and must not be reintroduced. |

No remaining Phase 14 work requires ISE CPU production implementation. Any future production work must start from a new owner-specific RFC/ADR with negative tests and cannot be inferred from migrated documentation.

## Residual Risk

The source corpus contains Russian, English, and some mojibake artifacts. This new plan uses ASCII-only English to avoid adding another encoding problem. The main ongoing risk is stale activation language being copied back into current docs without status classification.

## External Audit Risk Update

All code anchors must use concrete repo-root paths such as `HybridCPU_ISE/CloseToHSL/Core/Runtime`; reviewers must not infer roots manually. The folder spelling `VirtualiztionRefactoringNew` intentionally matches the repository path. Every claim must carry one of the plan statuses: implemented, projection-only, denied, model/helper-only, future-gated, or forbidden.

## Next Phase Dependency

Phase 15 depends on this clean corpus for final readiness review and final NO-GO/GO classification. Phase 15 must not issue production runtime work unless a new owner-specific RFC/ADR is created outside this closure.
