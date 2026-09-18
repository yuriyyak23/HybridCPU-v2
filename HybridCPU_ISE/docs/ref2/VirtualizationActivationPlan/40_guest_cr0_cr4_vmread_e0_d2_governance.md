# Phase 40 — GuestCr0/GuestCr4 VMREAD E0/D2 governance

Status: E0 closed; immutable SpecV2 is in commit `4d3b5b97c22661652c94357319e2e6b16615cceb`; the later non-self-referential AcceptanceRecordV2 machine-accepts D2 as governance policy only.

## 2026-06-11 Audit Contract

- File name: `40_guest_cr0_cr4_vmread_e0_d2_governance.md`.
- Purpose: close exact E0 and materialize the separately accepted V2 governance chain for the `GuestCr0`/`GuestCr4` read-only semantic group.
- Status: E0, immutable spec and later machine acceptance are materialized; no production implementation is authorized.
- Scope: exact frozen field IDs 3 and 5, existing privileged execution-state owner/value source, read-only projection policy and adjacent denials.
- No-goals: no production implementation, capability, write, backend, state mutation, completion, retire, SecureCompute activation or adjacent virtualization expansion.
- Code anchors: `Phase40VmReadProjectionE0Contract.cs`, `Phase40VmReadProjectionDecisionSpecV2.cs`, `VmReadProjectionDecisionValidatorV2.cs`, `PrivilegedExecutionStateOwnerPolicy.cs`, `PrivilegedExecutionStateProjectionService.cs`.
- Authority owner: existing neutral `PrivilegedExecutionStateDescriptor` / `PrivilegedExecutionStateOwnerPolicy`; VMCS/frontend/governance artifacts are non-authority.
- Required RFC/ADR: this bounded owner-specific E0/D2 decision; any differing CR0/CR4 semantics or widened field set requires a new decision.
- Acceptance criteria: all twelve E0 findings, exact canonical spec, later non-self-referential acceptance, matching reviews/CODEOWNERS, negative gates and no-authority scans.
- Tests/static scans: `VmxPhase40VmReadE0D2SpecTests`, `VmxPhase40VmReadD2ValidatorNegativeTests`, existing CR0/CR4 projection/owner tests and forbidden-shortcut scans.
- Risks: treating VMCS metadata, a synthetic validator fixture, accepted governance metadata or the VMCALL probe D2 as projection authority.
- Next-gate dependency: a separately authorized production implementation/projection pool only after machine acceptance; none is opened here.

The full owner-map columns are: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Bounded scope

This phase covers only the semantic group `VmcsField.GuestCr0` (`3`) and
`VmcsField.GuestCr4` (`5`) as read-only architectural VMREAD compatibility
projection. It does not implement or activate a new VMREAD path.

The decision identity is
`D2-HV-VMREAD-PROJECTION-V1-GUEST-CR0-CR4-0001`, namespace
`HybridCPU.VMREAD.Projection.v1`, operation `READ_GUEST_CR0_CR4_V1`.
It is distinct from every `PROBE_NO_STATE_V1` D2/O1/E1-E7 identity.

## E0 findings

1. Canonical ingress is `VMREAD` decode followed by
   `VmxCompatibilityAdmissionService.AdmitVmReadProjection`, compatibility
   projection validation, and the projection-only `ReadCompatibilityProjection`
   runtime boundary.
2. The only authority/value source is the existing neutral
   `PrivilegedExecutionStateDescriptor` admitted by
   `PrivilegedExecutionStateOwnerPolicy`; the field-specific service selects
   only `GuestCr0.Value` or `GuestCr4.Value`.
3. Materialized nonzero domain, exact address-space tag, and current nonzero
   policy epoch are mandatory.
4. CR0 and CR4 have field-specific allowed/required masks. The current owner
   policy validates both values for either projection; therefore they form one
   jointly legal semantic group.
5. Runtime admission uses `CapabilityBoundaryRequirement.None`, a zero grant
   mask and no typed grant. The compatibility decode prerequisite named
   `CapabilityValidated` is not a capability allocation.
6. Evidence is guest-visible read-only projection evidence plus compatibility
   projection evidence and field conformance proof.
7. Migration class is exactly `RevalidatedAfterRestore`; a pre-restore epoch is
   not reusable.
8. Visibility is one selected architectural scalar value, read-only.
9. Missing owner/source/materialization/domain/address-space/epoch/kind/bit
   legality/evidence/visibility/migration/conformance produces deterministic
   denial.
10. VMCS identifiers/schema are metadata only. VMCS scalar storage, backing
    store, CR3/flags/paging/control inference, and zero/default fallback are
    forbidden.
11. Write, authoritative mutation, backend execution, completion publication,
    and retire publication/effect are all absent and denied.
12. This D2 denies every field outside `{GuestCr0, GuestCr4}` and always denies
    VMWRITE. Host aliases, HostCr3, compatibility controls, SecureCompute
    activation/backend authority, nested, memory/I/O/device and lane/stream
    expansion remain outside scope.

## One-D2 determination

One D2 is valid because both fields share the same neutral owner, descriptor
value-source class, domain/address-space/epoch binding, guest-visible evidence,
security boundary, restore/migration class, read-only effect contract and
denial semantics. The two field-local descriptor members and bit masks remain
separate owner-map entries. A future change that makes any of those semantics
different requires two new independent decisions rather than widening this one.

## Machine contract

`Phase40VmReadProjectionE0Contract` records all twelve findings without a
projection or authority API. `Phase40VmReadProjectionDecisionSpecV2` is the
immutable canonical policy input with digest
`52ce040b93f54b36a427c4269f2afff77b2e66f83ceda3ece1b1dc917a58241f`.

The V2 encoder adds the projection profile only when present. Its absence
preserves the accepted Phase 38 canonical bytes and digest. The dedicated
`VmReadProjectionDecisionValidatorV2` cannot load O1/E2, issue a capability,
read a value, execute a backend, publish completion, or retire.

## Acceptance/provenance rule

The spec must first exist in an immutable commit. A later commit may add a
`VirtualizationDecisionAcceptanceRecordV2` that binds the earlier spec commit,
exact digest, CODEOWNERS blob, neutral-owner review and architecture review.
The spec SHA must differ from the acceptance record's containing commit SHA.

The later `Phase40VmReadProjectionDecisionAcceptanceV2` binds spec commit
`4d3b5b97c22661652c94357319e2e6b16615cceb`, tree
`0cc6c74cb985b5455d05de89b8b4b6ab28483122`, exact digest, CODEOWNERS blob,
neutral-owner review and architecture review. Its canonical acceptance digest is
`cf99799baba3ce6595fef61b2f53a5ec1a8e1c144d0bccd29df8171f603c34d8`.
It records no containing-commit claim and therefore cannot be self-referential.

After validation, the accepted object remains governance metadata only and does
not authorize production VMREAD implementation or activation.

## Negative boundaries

- No new VMCS/VMREAD state owner.
- No capability/grant/lease or reuse of capability bit 41.
- No reuse of `HybridCPU.VMCALL.Runtime.v1`, `PROBE_NO_STATE_V1`, its owner
  allocation, O1, E1-E7, completion or retire contours.
- No frontend, schema, decision artifact, test or diagnostic as authority.
- No production composition, executor, write, completion or retire changes.
