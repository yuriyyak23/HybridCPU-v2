# 01. ExternalRuntime SecureCompute V1

## Goal

Create the missing production-positive SecureCompute external ABI so SingNextOS can implement `IPlatformSecureComputeProvider` without inventing enforcement locally.

## Feature model

Add versioned external feature families for at least:

- secure-domain lifecycle;
- secure-region binding;
- secure execution composition;
- secure evidence publication;
- secure I/O admission where externally materialized.

The manifest must distinguish ordinary `Executable` support from `ProductionSecure`. A provider may advertise `ProductionSecure` only when the exact secure contract, enforcement and closure semantics are implemented.

## Contract identities

Define opaque generation-bound identities for:

- `ExternalSecureDomainLease`;
- `ExternalSecureRegionBinding`;
- `ExternalSecureExecutionBinding` linking an exact child domain when virtualization is requested;
- secure evidence context/receipt;
- exact terminal secure-region/domain closure receipts.

Creation receipts include proven secure properties. Destructive operations compare exact identity + generation and stale handles return stale without destroying the current generation.

## Memory binding

Secure region requests bind to an exact already-admitted external parent mapping. For a secure guest, the binding also names the exact child/guest mapping lineage. Do not create an independent memory owner or expose CXL memory identity.

## Lifecycle

Required lifecycle shape:

```text
Created -> Configured -> Running <-> Parked -> Draining -> Closed
                                      \-> Quarantined/Faulted
```

Ambiguous creation/close must remain tracked with a recovery/quarantine handle. `Unavailable` is never treated as `Closed`.

## Evidence

Evidence receipts contain identity, policy/evidence generation, producer classification and assurance. They are read-only evidence and never convey memory, execution or I/O authority.

## Compatibility

Keep V1/V2/V3 child contracts source-compatible where practical. SecureCompute should be an additive interface/family, not an incompatible reinterpretation of child authority bits.

## Required tests

- stale destructive handles cannot close current secure resources;
- malformed success + failed compensation remains tracked;
- child/secure parent mismatch is rejected;
- unsupported feature manifests do not advertise ProductionSecure;
- evidence cannot be used as an authority token;
- exact terminal close is required before local adapter state is removed.
