# Migration Checkpoint Restore

## Classification First

Every checkpoint candidate requires an explicit payload class and admission decision. Object presence does not make state serializable.

Phase 15 adds output-manifest classification for future positive paths. The manifest names request state, internal backend result, internal completion record, guest-visible output, retire-visible state and recomputed-after-restore state. Internal backend results and completion records are manifest coverage only; they do not create backend execution, checkpoint, restore, completion publication or retire publication authority.

## Denied Payloads

The current policy denies authority from:

- host-owned evidence;
- scheduler evidence;
- backend binding evidence;
- native tokens;
- raw measurement secrets;
- raw sealing keys;
- active host pointers;
- VMCS projection metadata;
- compatibility projection metadata;
- VMCS12, VMCS02 or mutable Shadow VMCS state as nested SecureCompute authority.

## Restore

Restore may require:

- policy epoch validation;
- grant epoch validation;
- measurement epoch validation;
- provenance validation;
- revalidation or re-attestation;
- private-memory sealed/encrypted contract validation.

Recomputed-after-restore values are rebuilt facts. They are not deserialized runtime authority.

Output manifest entries require owner/path/reachability classification. Recomputed-after-restore entries also require restore validation proof.

## Current Limit

The repository contains fail-closed migration, checkpoint and output-manifest classification policy. It does not establish live secure-domain migration, key management, device-handle migration, completion/retire publication or positive backend continuation after restore.
