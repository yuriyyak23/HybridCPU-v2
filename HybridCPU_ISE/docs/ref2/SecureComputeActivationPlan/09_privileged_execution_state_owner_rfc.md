# Privileged Execution State Owner RFC

## Phase Metadata

- File name: `09_privileged_execution_state_owner_rfc.md`
- Decision ID: `ADR-SC-PES-GuestCr0Cr4`.
- Phase goal: define and implement neutral `GuestCr0` / `GuestCr4` ownership without opening compatibility projection.
- Status: RFC/ADR accepted; neutral owner-proof contract implemented and covered by tests; Phase 10 later implemented as a separate projection-only gate.
- Scope: privileged execution-state owner, bit legality, value source, evidence, migration and compatibility projection boundary.
- No-goals: no VMREAD value opening, no VMWRITE, no VMCS store, no backend execution and no completion/retire publication.

## Accepted Decision

`PrivilegedExecutionStateDescriptor` is the neutral runtime-owned source for `GuestCr0` and `GuestCr4` owner validation. `PrivilegedExecutionStateOwnerPolicy` validates owner materialization and returns owner acceptance only. It does not authorize projection, mutation, backend execution, completion publication or retire publication.

The generated compatibility schema remains vocabulary only. Existing execution read-only views remain separate and do not own privileged control-register values.

## Owner Map

| Concern | Neutral owner/source | Accepted rule |
| --- | --- | --- |
| domain identity | `PrivilegedExecutionStateDescriptor.DomainTag` | non-zero and equal to runtime domain tag |
| address-space identity | `PrivilegedExecutionStateDescriptor.AddressSpaceTag` | non-zero and equal to runtime address-space tag |
| stale-state boundary | `PrivilegedExecutionStateEpoch` | materialized descriptor epoch equals current owner-policy epoch |
| `GuestCr0` value | `PrivilegedControlRegisterValue` with `GuestCr0` kind | descriptor-owned value only |
| `GuestCr4` value | `PrivilegedControlRegisterValue` with `GuestCr4` kind | descriptor-owned value only |
| reserved and required bits | `PrivilegedControlRegisterLegalityPolicy` | values must stay inside allowed masks and include required masks |
| evidence | `GuestVisibleReadOnlyProjection` classification | eligibility classification only; no projection permission in Phase 09 |
| migration/restore | `RevalidatedAfterRestore` classification | restore must rerun domain, address-space, epoch, kind and legality checks |

## Admission Order

`PrivilegedExecutionStateOwnerPolicy` fails closed in this order:

1. descriptor presence;
2. descriptor materialization;
3. domain-tag binding;
4. address-space-tag binding;
5. current epoch;
6. canonical register kinds;
7. evidence classification;
8. migration classification;
9. `GuestCr0` reserved and required bits;
10. `GuestCr4` reserved and required bits.

The allowed result is `AllowedOwnerMaterializedProjectionClosed`. Only `OwnerAccepted` is true. All side-effect and projection authority flags remain false.

## Bit Legality And Memory-Mode Coupling

The RFC does not hardcode an external CR0/CR4 architecture profile. The descriptor carries explicit allowed and required masks, and malformed mask policy is unmaterialized. Address-space coupling is enforced through `AddressSpaceTag`; any future mode-specific coupling beyond this binding requires a separate owner-contract extension and tests.

## Evidence And Migration

- host-owned evidence is not accepted as guest-visible privileged execution state;
- compatibility aliases are not owner authority;
- owner state is classified for revalidation after restore;
- restore reuses the same owner policy and cannot trust stale epoch or illegal bits;
- raw compatibility projection metadata is not owner state and is not migration authority.

## Production Code Anchors

- `PrivilegedExecutionStateDescriptor.cs`
- `PrivilegedExecutionStateOwnerPolicy.cs`
- `PrivilegedExecutionStateEpoch`
- `PrivilegedControlRegisterLegalityPolicy`

The Phase 09 owner sources intentionally have no compatibility or virtualization dependency. Phase 10 later consumes the typed owner result through a separate projection service.

## Required Tests

Implemented:

- missing descriptor denied;
- unmaterialized descriptor denied;
- domain-tag mismatch denied;
- address-space-tag mismatch denied;
- stale epoch denied;
- non-canonical register kind denied;
- reserved bits denied for both registers;
- required bits denied for both registers;
- host-owned, alias and unclassified evidence denied;
- unclassified/domain-local migration classes denied;
- accepted owner keeps projection, mutation, backend, completion and retire authority false;
- accepted owner alone, without Phase 10 projection inputs and conformance proof, does not open `GuestCr0` / `GuestCr4`;
- owner production sources have no compatibility or virtualization authority dependency.

## Required Static/Source Scans

- owner source contains no compatibility state manager or virtualization execution-unit dependency;
- owner source contains no scalar compatibility read/write fallback;
- owner source contains no runtime publication shortcut;
- Phase 10 projection source references the owner only through typed descriptor/policy inputs;
- missing Phase 10 inputs retain privileged execution-state projection denial.

## Completion/Retire Implications

Phase 09 produces no backend result and no completion or retire publication.

## SecureCompute Activation Implications

Phase 09 closes the neutral privileged execution-state owner RFC gate only. It is not production SecureCompute activation. The later Phase 10 projection path requires its own visibility, migration and conformance gates.

## Exit Criteria

- `ADR-SC-PES-GuestCr0Cr4` accepted;
- owner map implemented in production descriptor/policy types;
- bit legality, stale-state, evidence and restore classification enforced;
- negative and source-guard tests pass;
- owner admission alone cannot project `GuestCr0` / `GuestCr4`.

Exit status: satisfied by code and tests. Phase 10 was subsequently closed by its separate projection-only implementation.

## Dependency

Previous: `08_measurement_evidence_visibility_activation_plan.md`. Next: `10_guestcr0_guestcr4_readonly_projection_plan.md`.
