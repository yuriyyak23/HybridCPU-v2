# Phase 11 - Replay/Rollback Conformance

Status: closed/runtime-only

Replay binds replay-stable decoded and materialized instruction identity,
owner/context, opcode, descriptor, operation kind, resource class, slot class,
resource contour fingerprint, and typed stream transfer fingerprint.

Rollback uses:

- core-owned tile and accumulator checkpoints;
- all-or-none memory checkpoint restore;
- deterministic replay of retired faults;
- duplicate replay and stale checkpoint rejection.

Load, store, MACC, and transpose rollback preserve the same architectural
publication boundaries as first execution. SRF valid/dirty state, host-owned
transport evidence, telemetry, and generic StreamEngine completion are not
replay authority.

Compiler scope remains closed to this runtime phase.

## Numeric Revalidation Dependency

Phase 11 remains closed for the existing resource, transfer, checkpoint, and
publication identity. Phase 17 must extend the replay validity contract with
the explicit numeric-policy identity, state epoch, dependency identity, and
publication surface. A matching legacy fingerprint is insufficient when any
of those fields changed.
