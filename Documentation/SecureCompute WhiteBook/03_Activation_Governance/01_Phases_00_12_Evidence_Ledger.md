# Activation Evidence Ledger

## Audit Result

The activation tasks are complete for the bounded closure classes stated below.
No row claims production activation.

| Gate | Verified status | Primary proof | Remains closed |
| --- | --- | --- | --- |
| 00 | index and ordering established | activation index and release corpus | runtime authority |
| 01 | current-state/gap matrix verified | code/test anchor matrix | activation claim |
| 02 | forbidden-regression guards implemented | release regex/source guards | exceptions without RFC |
| 03 | owner-specific RFC/ADR process implemented | accepted privileged-state owner RFC plus process rules | RFC as runtime authority |
| 04 | ordinary no-effect gate revalidated | descriptor no-effect and Stage B tests | non-ordinary bypass |
| 05 | descriptor materialization/completeness gate implemented | root/subdescriptor fail-closed tests | materialization as execution |
| 06 | Stage B routing gate implemented; P0 bypass closed | runtime routing, taxonomy tests and source scan | admission as publication |
| 07 | descriptor grant/epoch discipline implemented | provenance, bounds, epoch, revocation and derivation tests | CHERI or ISA capabilities |
| 08 | measurement/evidence visibility gate implemented | measurement, publication, restore and source tests | evidence as authority |
| 09 | privileged execution-state owner proof implemented | descriptor/policy tests and source guard | projection from owner alone |
| 10 | `GuestCr0`/`GuestCr4` read-only projection implemented | all-gate positive/negative tests | VMWRITE, broad fields, side effects |
| 11 | memory/private-domain policy admission implemented | memory and migration tests/source guards | hardware tags, backend execution |
| 12 | secure I/O/shared-buffer policy admission implemented | current binding tests and publication-zero guards | secure I/O backend execution |
| 13 | secure hypercall backend-owner contract and identifier allocation implemented | `SecureHypercallBackendOwnerAbiRegistry`, proof-only admission tests and VMX zero-authority tests | backend execution, completion publication, retire publication |
| 14 | completion/retire publication authority implemented fail-closed | `SecureCompletionRetirePublicationAuthorityPolicy`, focused tests and scoped source scans | backend execution, production activation, compiler secure emission |
| 15 | migration/checkpoint/restore output-manifest classification implemented fail-closed | `SecureOutputManifestClassificationPolicy`, focused tests and scoped source scans | live migration, backend execution, completion publication, retire publication, compiler secure emission |
| 16 | debug/attestation visibility classification implemented fail-closed | `SecureDebugAttestationVisibilityPolicy`, focused tests and scoped source scans | backend execution, VMX authority, migration authority, completion publication, retire publication, compiler secure emission |
| 17 | VMX boundary zero-authority classification implemented fail-closed | `SecureComputeNamedPathVmxZeroAuthorityPolicy`, focused tests and scoped source scans | VMX activation, VMCS authority, backend execution, completion publication, retire publication, compiler secure emission |
| 19 | compiler no-emission to controlled-emission decision implemented fail-closed | `SecureComputeControlledEmissionGatePolicy`, focused tests and scoped source scans | secure compiler emission, backend execution, new secure opcodes, capability-aware memory instructions |

## Audit Corrections Applied

- the WhiteBook no longer describes the former enabled-descriptor Stage B bypass;
- ordinary no-effect and non-ordinary fail-closed behavior are separated;
- `GuestCr0` / `GuestCr4` status is updated from blanket denial to gated read-only projection;
- No-effect through measurement/evidence gates are marked as implemented/revalidated gates in the ActivationPlan.
- Current-next wording now points through the closed publication, output-manifest
  classification, debug/attestation visibility, VMX zero-authority, and compiler
  decision gates to positive runtime execution activation planning.
- Memory/private-domain policy admission has explicit exit-status wording.
- Secure I/O/shared-buffer policy admission is kept separate from backend and publication authority.
- Secure hypercall identifier allocation is kept separate from backend execution and publication authority.
- Publication authority is closed fail-closed for current proof-only/admitted-denied/registry-backed paths and remains separate from backend execution.
- Migration/output-manifest classification is closed fail-closed for future request/result/completion/guest-output/retire/recomputed entries and remains separate from backend execution, completion publication and retire publication.
- Debug/attestation visibility is closed fail-closed and remains separate from runtime authority, VMX authority, migration authority, completion publication and retire publication.
- VMX boundary zero-authority is closed fail-closed for named positive-looking paths and remains separate from backend execution, publication and compiler emission.
- Compiler no-emission is closed as an explicit no-compiler-change decision and remains separate from backend execution, production activation and controlled compiler emission.

## Residual Risk

The repository worktree contains unrelated parallel changes. Verification for this ledger must use focused SecureCompute and VMX boundary slices and must not treat unrelated suite failures as SecureCompute evidence.
