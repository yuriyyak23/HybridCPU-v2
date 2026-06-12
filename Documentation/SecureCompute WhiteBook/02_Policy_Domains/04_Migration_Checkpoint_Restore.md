# Migration Checkpoint Restore

## Classification First

Every checkpoint candidate requires an explicit payload class and admission decision. Object presence does not make state serializable.

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

## Current Limit

The repository contains fail-closed migration and checkpoint policy. It does not establish live secure-domain migration, key management, device-handle migration or positive backend continuation after restore.
