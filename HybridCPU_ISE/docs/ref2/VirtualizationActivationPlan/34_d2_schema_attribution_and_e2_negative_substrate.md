# Phase 34 - D2 Schema, Attribution And E2 Negative Substrate

Status date: 2026-08-07

Current-state precedence: historical blocked-stage wording in this phase records the substrate checkpoint. Current status is governed only by `VirtualizationActivationStatusV1.json`.

PR-I changes no D2, ABI, owner allocation or policy value. Its E7 lifecycle
materializes only Phase-38 `DrainOnly` and `HostOwnedNonMigratable`, reloads O1
from the exact local accepted SpecDigest after restore, and treats checkpoint
identity as non-authority.

PR-J likewise changes none of those values. Its per-domain lifecycle gate is
quiescence state only, not an operation owner or grant issuer. Its exact profile
accepts only the existing DecisionId/SpecDigest/namespace/leaf/operation/OwnerId/
policy version/epoch and provisions the existing neutral domain/capability
owners. The default-disabled and kill-switch states cannot amend D2.

PR-G consumes no new D2 choice. It materializes the already accepted Phase 38
`AtomicE3ToCompletionRecordAndE5`, host-owned evidence,
`HostOwnedNonMigratable` completion and `DrainOnly` operation policies. Any
change to those values still stops the authorized sequence.

Status: `CLOSED/HISTORICAL FAIL-CLOSED V1 + PR-A/B/C + PR-D..PR-J EXACT PROFILE`; the Phase-34 boolean request remains disabled, while the separately authorized v2 SafetyVerifier contour can issue admission-only E2. PR-J's default-disabled exact profile is the only accepted neutral composition and preserves VMX compatibility fault-only behavior outside that explicit profile.

PR-F later composes only the distinct typed v2 certificate through the canonical
lane-7 scheduler/execute seam. The Phase-34 boolean request remains always denied
and is not consulted by `InvokeHypercall`, the composition binding or executor.

## 2026-08-09 PR-E Non-Promotion Addendum

PR-E does not promote the Phase-34 boolean request and does not reinterpret its
negative substrate as authority. The exact executor accepts only the distinct
private PR-D certificate and an unforgeable consumer seal, consumes that E2
once, and returns an opaque E3. No compatibility, dispatcher, completion or
retire caller reaches the executor. Disabling it restores the PR-D boundary.

## 2026-08-09 PR-D Superseding Addendum

PR-D does not promote the Phase-34 boolean DTO. It adds a distinct typed request
whose inputs are live objects: exact D2-derived O1, canonical E1 and operand,
neutral domain/root context, a non-forgeable generation-bearing capability
lease and the live restore-generation owner. Only `SafetyVerifier` issues the
opaque v2 certificate and owns its liveness/revocation registry. The legacy
`EvaluateVirtualizationOperationAdmission` method still returns no certificate.
No executor or publication authority is introduced.

The older manifest/validator remains v1 negative substrate and is not the final acceptance contract. The 2026-08-08 audit found a valid provenance concern: `AcceptedCommitSha` is carried by the same manifest whose state may be `Accepted`, so the object cannot non-circularly name the commit that first contains itself. PR-A adds the separate v2 contract; v1 still must not be used to close D2.

## 2026-08-09 PR-A V2 Closure

PR-A adds the separate immutable SpecV2/AcceptanceRecordV2 and append-only
revocation/supersession records under neutral runtime governance. The canonical
encoder uses a versioned binary envelope, fixed field order and type tags,
big-endian integers, explicit lengths, UTF-8 strings and raw SHA bytes; SHA-256
is computed only over canonical payload bytes excluding the corresponding digest
field. Serializer JSON, whitespace and property order are irrelevant.

`VirtualizationDecisionValidatorV2` now fails closed on canonical bytes/digests,
resolved spec SHA and exact bytes, identity/owner/ABI/policy profile, namespace
and collision rules, complete owner map, logical review roles, CODEOWNERS
matching, acceptance state and immutable lineage. Its positive type is policy
metadata, not a capability. The repository contains no populated production
AcceptanceRecordV2, CODEOWNERS file, completed review evidence, accepted registry,
O1 or E2 issuer. Thus PR-A structural validation is closed while attributable
machine D2 remains blocked.

## 2026-08-09 PR-B Attribution/Materialization Closure

The repository owner explicitly opened PR-B after committing PR-A. Commit A
`1061eaa8bc45d598e1fe7b3fead71cf017ad81a6` contains the exact SpecV2 and
CODEOWNERS blob. The later immutable AcceptanceRecordV2 binds that SHA,
SpecDigest `33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8`,
matching logical owner/architecture review receipts and the CODEOWNERS blob.
Repository account spelling is not architectural policy; validator acceptance
requires one consistent attributable principal across the acceptance, both
roles and every required scope. Stable owner/capability allocation metadata and
one exact generated policy lookup now exist. None is a grant, O1, admission,
execution, completion or retire authority.

## 2026-06-11 Audit Contract

- File name: `34_d2_schema_attribution_and_e2_negative_substrate.md`.
- Purpose: record the machine-readable D2 shape, repository attribution gate, disabled neutral-owner interface and opaque unissued E2 substrate.
- Status: v1 negative substrate, PR-A v2 structural gates and PR-B attributable accepted machine D2 implemented. Later PR-C adds immutable O1 policy loading and canonical operand identity only; no loaded executable owner service, live capability grant, certificate issuance or runtime activation exists.
- Scope: D2 states, exact-one-leaf structural validation, commit/reviewer/CODEOWNERS attribution, forbidden compatibility self-approval, disabled owner resolution and E2 denial.
- No-goals: no live executable runtime owner, capability grant, E2 issuer, backend executor, positive canonical production connection, completion, retire, migration payload or compiler emission. Later O1 remains policy only.
- Code anchors: `VirtualizationOperationDecisionManifest.cs`, `DisabledNeutralVirtualizationOperationOwner.cs`, `SafetyVerifier.VirtualizationOperationAdmission.cs`, `virtualization-operation-decision-manifest.schema.json`.
- Authority owner: Phase 38 and PR-B accept/materialize `DomainHypercallRuntimeOwner` policy attribution and stable allocation metadata. No loaded service exists. The existing SafetyVerifier owner controls only the negative E2 evaluation boundary and cannot manufacture runtime authority from machine D2.
- Required RFC/ADR: an attributable neutral owner must supply an accepted SHA-bound artifact and the complete field/operation, owner, value source, capability policy, evidence class, migration class, denial reason map.
- Acceptance criteria: all absent/draft/withdrawn/unattributed/self-approved/leaf-less inputs deny; the owner interface has no execution method; no E2 certificate can be constructed or issued; current VMX execution and retire stay fault-only.
- Tests/static scans: D2 v1/v2 negative tests, materialized spec/acceptance tests, plan guards, VMX-refactoring suite, CODEOWNERS blob verification and backend/completion/retire shortcut scans.
- Risks: treating schema or Phase 38 ADR validity as machine D2/runtime authority, treating an `Accepted` enum value as an accepted artifact, inserting an unvalidated registry entry, using CODEOWNERS presence without matched review, or attaching E2 to production.
- Next-gate dependency: PR-A, explicitly authorized PR-B and later separately authorized PR-C are closed at their respective boundaries. No E2 pool opens automatically. The separate TESTING-only research lane remains governed by Phase 36/37.

## Clean E1 Provenance

E1 is contained by clean local commit `55807df77978a960382fa913dda4e7ace0093a6b`, tree `3f356900b4c534e965a69640e88e0c5ecf902b7c`. The local Baseline run at `artifacts/validation/20260807-181740-Baseline/provenance.json` reports the same SHA and `outcome: passed`. The repository-local evidence manifest records that observation. This proves reproducibility of E1 only; it grants no D2 or runtime authority.

## Implemented D2 Boundary

The JSON schema and C# validator define `Absent`, `Draft`, `Accepted` and `Withdrawn`. `Accepted` is vocabulary, not a repository decision. Validation requires all of the following before returning a structurally valid governance result:

- one decision and operation identity;
- a neutral-runtime owner source, matching attributed owner and a matching 40-hex accepted commit SHA;
- a matching CODEOWNERS rule and completed required review;
- no compatibility-frontend self-approval;
- exactly one numeric leaf supplied by the future owner artifact;
- the complete value-source, capability, evidence, migration and denial map.

No populated decision manifest, CODEOWNERS appointment, owner name, operation name or leaf value is included. The validator performs no file I/O and its positive structural result explicitly keeps backend, completion and retire authority false.

### Required v2 Acceptance-Provenance Contract

Before a real D2 can be accepted, replace the single-object acceptance model with two separately versioned artifacts:

- immutable `VirtualizationDecisionSpecV2`: decision/operation identities, owner attribution, the Phase 38 runtime operand ABI including namespace/width/high-bit policy and exact numeric leaf, state/effect class, capability/evidence/migration/completion/retire/denial maps, and a deterministic content digest;
- later `VirtualizationDecisionAcceptanceRecordV2`: referenced spec commit SHA and digest, attributable required-reviewer/CODEOWNERS evidence, acceptance state, and withdrawal/replacement lineage. It must not claim the SHA of its own containing commit. `AcceptanceRecord`, not `Attestation`, is the normative Phase 38 name.

The generated exact-leaf registry may consume only a valid acceptance record whose referenced spec bytes reproduce the recorded digest. Schema validity, `state=Accepted`, a CODEOWNERS file without matched review, a review-workflow result, or the v1 validator result remains governance evidence only and never becomes E2/runtime authority.

Phase 38 accepts the exact architecture vocabulary, PR-A supplies v2 structural code and PR-B supplies the attributable accepted machine instance. The current v1 E2 request still requires `AddressSpaceIdentityPresent`; that is intentionally non-promotable for `PROBE_NO_STATE_V1`, whose v2 operation contract has no address-space identity because it has no memory effect.

Phase 38 supplies the repository-owner architecture decision, but this v2 requirement still does not supply a runtime OwnerId/reviewer identity, accepted machine record or execution permission. Frozen compatibility `ushort` fields did not decide the ABI and remain non-authoritative; truncation is forbidden.

## Disabled Owner And E2 Boundary

`INeutralVirtualizationOperationOwner` exposes only `Resolve`; it has no execution method. The only implementation is disabled and always returns a denial. The opaque E2 certificate has only a private constructor, no issuer and no production carrier. `SafetyVerifier.EvaluateVirtualizationOperationAdmission` maps invalid D2 and disabled-owner state to typed denials and always returns a null certificate.

No compatibility frontend, dispatcher, backend admission path, completion route or retire path references the E2 certificate. E1 remains the last canonical virtualization certificate, and it still carries explicit false authority for backend, completion and retire.

## Closed Checks And Remaining Blocker

Closed in this phase: schema syntax, validator denial taxonomy, SHA/attribution/reviewer/CODEOWNERS gates, compatibility self-approval denial, exact-one-leaf rule without a populated value, disabled owner resolution, opaque unissued E2 type and static shortcut guards.

Closed by PR-B: repository materialization of the Phase 38 `HCOWNR` and capability-number allocations, matching owner/architecture reviewer and CODEOWNERS attribution, an attributable accepted spec/record pair and generated exact lookup. These satisfy machine D2 policy only and do not satisfy runtime authority.

Closed later by PR-C: common execution-only legality correction, immutable exact-D2-derived O1 and one-time full-value canonical operand snapshot at the E1-bound production seam. Still blocked: live capability grants, E2 issuance and every later execution/publication stage. Restore generation is bound and stale generations deny, but checkpoint restore advancement remains a later lifecycle integration.

## Next Permitted Pool

- preserve the closed PR-A/PR-B artifacts and fail-closed lineage/attribution gates;
- continue evidence-manifest and local CI refresh at clean containing SHAs;
- no pool opens automatically; the separately authorized PR-C has addressed
  common-legality corrections, O1 and canonical operand snapshot, without a
  positive backend.

E2 positive issuance, E3 backend work, positive production wiring, completion and retire remain forbidden without their own explicit authorization and complete live authority inputs.

This phase's historical next-safe-pool restriction was satisfied by PR-A and then superseded by explicitly authorized PR-B/PR-C closures. It never authorized E2, a backend executor, completion token or retire grant, and none exists now.

Phase 36 does not contradict this closure: its differently named prototype certificate and receipt exist only under `TESTING`, consume no accepted manifest or numeric leaf and are unreachable from production VMX execution.
PR-H likewise changes no D2/ABI/owner/policy value. It materializes only the
accepted `PreciseE5BoundNoStateRetire` policy as an opaque E6 in the canonical
CPU WB retire contour. E6 is runtime authority for one precise no-state retire,
not a D2 amendment, capability, compatibility effect, migration proof or release.
