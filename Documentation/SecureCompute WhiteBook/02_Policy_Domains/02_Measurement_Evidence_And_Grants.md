# Measurement Evidence And Grants

## Measurement

`DomainMeasurementDescriptor` binds measurement identity, policy digest, memory digest, epoch, creator domain and evidence/debug classification.

Admission rejects missing, pending, revoked, stale, unmaterialized or mismatched measurements. A materialized measurement is evidence input, not backend execution proof.

## Evidence Visibility

Evidence classes separate:

- denied;
- guest-visible;
- migration-serializable;
- compatibility alias;
- recomputed after restore;
- debug-only;
- host-owned quarantined.

Visibility does not become runtime authority. Host-owned or recomputed evidence cannot silently become guest publication, VMREAD authority or checkpoint authority.

Completion and retire fences remain separate. Evidence approval cannot replace either fence and cannot establish backend side effects.

## Descriptor-Level Grants

Layer 2 grant authority uses:

- `SecureGrantHandle`;
- `SecureAuthorityBounds`;
- `SecureGrantAuthorityPolicy`;
- `SecurePolicyDerivationRecord`;
- `SecureRevocationEpoch`.

Validation requires provenance, current epoch, bounded scope and monotonic derivation. Guest scalar values and compatibility projection cannot materialize secure grants.

These grants are runtime descriptors. They are not CPU capability registers, pointer provenance tags or CHERI capabilities.
