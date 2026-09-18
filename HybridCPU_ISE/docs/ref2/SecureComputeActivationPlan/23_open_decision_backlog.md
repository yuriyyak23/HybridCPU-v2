# Open Decision Backlog

## Phase Metadata

- File name: `23_open_decision_backlog.md`
- Phase goal: quarantine unresolved owner, path and release decisions so they cannot leak into activation claims.
- Status: open backlog; not implementation or activation approval.
- Scope: C0/C1/C2 decisions, future RFCs and deny-by-default boundaries.

## Current Baseline

The current revalidation is recorded in `24_audit_revalidation_and_dependency_order.md`. No pool is considered closed merely because policy vocabulary, a direct unit test, a source-string check or a documentation assertion exists.

## Ranked Backlog

### C0 — Authority and canonical path

| Decision | Current evidence | Required decision/owner |
| --- | --- | --- |
| canonical legality authority and operation taxonomy | caller-selected enum; non-exhaustive generic positive branch | accepted recommendation: generated registry owns vocabulary, decoder freezes identity, SafetyVerifier derives exhaustive `SecureOperationKind`; `Unknown = 0` denies |
| production composition root | no decode → certificate → issue → execute → result → completion → retire call graph | CPU pipeline composition owner |
| descriptor lifecycle | public constructors and two full-descriptor carriers | accepted recommendation: `SecureDomainRegistry` sole lifecycle owner and opaque `(RegistryId, DomainId, Generation, PolicyDigest, Seal)` binding |
| immutable admission identity | no `SecureAdmissionCertificate` | accepted recommendation: SafetyVerifier sole issuer; operation/domain/VT/lane/bundle/FSP/replay/grant/effect-envelope binding |
| grant authority | caller handles and materialization booleans | accepted recommendation: `SecureGrantLedger` owns mint/reserve/consume/revoke and non-replay nonce/use state |
| operation-specific admission | special dispatch only for I/O and hypercall | exhaustive deny-by-default policy table |
| result/publication chain | classifiers only; no production callers | accepted separation: request -> execution receipt -> one-shot completion record -> architectural retire, each with a named owner |

### C1 — Effect domains and state protocols

| Decision | Current evidence | Required decision/owner |
| --- | --- | --- |
| region/shared-buffer maps | first-match lookup; no overlap/duplicate rejection | accepted recommendation: checked, sorted, disjoint canonical maps with stable IDs/digests |
| private memory | direct policy tests only | production memory/translation/cache owner |
| DMA/IOMMU | no device effect or revoke path | device-context/mapping/submission/completion owners; first release uses copy-in/copy-out staging |
| hypercall | proof-only IDs and admission | neutral hypercall executor/result owner after neutral probe |
| checkpoint/restore | classifiers, caller booleans, unknown payload default allow | denied for first register-local contour; later serializer, key, anti-replay and atomic restore owners implement the atomic-reopen protocol |
| evidence | visibility/publication classifiers | one exclusive production evidence publisher after retire; it owns projection only, never measurement/effect/retire |

### C2 — Equivalence, reproducibility and release evidence

| Decision | Current evidence | Required decision/owner |
| --- | --- | --- |
| disabled equivalence | admission-level tests | paired pipeline/SMT/FSP/memory/I/O/exception/retire traces |
| compiler no-emission artifacts | contour/lowering rejection | clean generated inventory and artifact hashes |
| reproducible audit | audit SHA unavailable; dirty checkout; requested SDK differs from actual | immutable SHA, clean-tree disclosure, exact toolchain/generators/filters/hashes |
| limited release | Phase 22 always denies | accepted recommendation: separate offline `SecureComputeReleaseEvidenceVerifier` consumes immutable signed evidence and emits a SHA-bound build-profile artifact |

## Dependency Order

1. reproducible audit baseline at an immutable clean revision;
2. end-to-end disabled observational-equivalence harness;
3. canonical operation taxonomy with caller authority removed;
4. descriptor registry and opaque binding;
5. grant ledger;
6. SafetyVerifier-issued certificate;
7. production certificate carrier through SMT/FSP/Stage B/issue;
8. hermetic test-only controlled-carrier profile, unavailable to production APIs;
9. public canonical decode -> certificate -> issue -> expected-deny slice;
10. one side-effect-free named neutral backend probe, transport-only, with no memory, I/O, VMX, nested or product compiler-emission effect;
11. one register-local deterministic operation with execution receipt;
12. separate completion and retire owners;
13. one publisher after retire;
14. independent offline release-evidence verifier;
15. later, separately connect canonical maps, memory, IOMMU/DMA and hypercall effects;
16. keep checkpoint/restore denied for the first contour and implement a later protocol;
17. future product compiler-emission RFC only after named limited runtime release;
18. separate future nested-execution RFC.

Exact owners, input/output types, reachability, dependencies, forbidden bypasses, tests, migration/replay requirements and bounded rollback for each change are specified in Phase `24`.

## Permanent Boundaries

- Phase 18 remains unconditionally future/design-fenced.
- Phase 20 remains a pre-activation evidence classifier.
- Phase 21 remains a negative/future-gated conformance matrix.
- Phase 22 remains permanently deny-only; independent named-path release proof is consumed by a separate offline verifier, not by changing the classifier to allow.
- VMX/VMCS/`VmxCaps` remain zero-authority.
- Product compiler secure emission remains forbidden until after a named limited runtime release and a separate RFC. The earlier hermetic test-only carrier profile is conformance tooling, not product emission or runtime authority.
- Unknown operation and migration payload classes must default deny.

## Authority And Publication

This backlog owns no authority, migration payload, evidence publication, completion or retire effect. A decision can move out of the backlog only through an owner-specific RFC/ADR, production code, executable path tests, scoped reachability scans and release evidence.

## Dependency

Previous: `22_limited_securecompute_release_gate.md`. Current audit ledger: `24_audit_revalidation_and_dependency_order.md`. Reconciled recommended decisions: `25_external_analysis_reconciliation_and_recommended_decisions.md`.
