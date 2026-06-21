# Memory And Private Domains

## Descriptor Model

`SecureMemoryDomainDescriptor` binds:

- domain tag;
- address-space tag;
- policy epoch;
- region descriptors;
- host-inspection policy;
- DMA policy;
- runtime-mutable classification.

Regions are classified as private, shared, measured or runtime-mutable policy regions.

## Admission Rules

- missing, unmaterialized or stale memory descriptor -> denied;
- private host read -> denied;
- private DMA -> denied, including hypercall argument paths;
- shared host inspection -> allowed only for explicit shared regions;
- shared DMA -> requires memory policy, explicit I/O shared-buffer policy, current binding and typed grant;
- measured access -> requires a measured region;
- runtime-mutable touch -> requires dirty and migration classification.

Measured memory admission is not private-domain activation evidence.

## Private Migration

Private-memory migration remains denied without a complete sealed/encrypted payload contract and restore validation. The contract is policy validation only. It does not introduce raw sealing keys, architectural sealed capabilities, hardware tags or pointer authority.

## Explicit Non-Goals

The memory policy does not implement:

- tagged memory;
- hardware memory tags;
- CHERI semantics;
- capability-bearing pointers;
- capability-aware memory instructions;
- EPT, NPT or VPID authority;
- VMREAD or VMWRITE memory authority.
