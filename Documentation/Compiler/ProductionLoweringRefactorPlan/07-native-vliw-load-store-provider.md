# 07 — NativeVliwLoadStore production provider

## Goal

Introduce a production-lowering provider for `ExecutionContourKind.NativeVliwLoadStore` after scalar production lowering is proven.

Load/store production lowering is more authority-sensitive than scalar lowering because memory publication, ordering, fault behavior, and address-space semantics are runtime-owned. The compiler may construct a carrier package only when all required runtime dependencies remain explicit.

## What is produced

A load/store production provider may produce:

- LSU carrier words/bytes for an explicitly supported opcode subset;
- memory-shape structural facts;
- optional memory/fault sideband envelope when the contour requires it;
- typed-slot facts as structural evidence;
- no-fallback proof;
- evidence and telemetry;
- runtime-authority-pending header.

It must not produce:

- memory publication;
- commit or retire;
- completed load/store effects;
- cache/order authority;
- runtime fault resolution;
- fallback into vector stream, DSC, L7, or scalar helper paths.

## Required gates

- Scalar production phase completed and green.
- Production profile explicitly enables `NativeVliwLoadStore`.
- Intent classifier has complete load/store coverage for the chosen subset.
- ISE LSU opcode identity is exact.
- Address width/field truncation checks are explicit.
- Memory ordering and fault expectations are represented as runtime requirements.
- Carrier golden artifacts exist.
- ISE decode/encode parity exists.
- No hidden vector/stream/DSC fallback exists.

## Tests

### Positive

- LB/LH/LW/LD-like load carrier snapshots, where supported.
- SB/SH/SW/SD-like store carrier snapshots, where supported.
- address/immediate field encoding parity.
- runtime dependency includes Legality A/B, execution, publication, commit, retire.
- sideband/facts are separated from carrier.

### Negative

- branch/control opcodes rejected;
- vector transfer special helper opcodes rejected unless explicitly routed to stream/vector scoped provider;
- DSC and L7 descriptor-backed carriers rejected;
- malformed address/stride/immediate rejected before emission;
- production gate missing rejects;
- cache/order contract missing rejects.

## Caller migration

Use shadow compare before changing callers:

```text
existing raw VLIW compatibility carrier -> load/store production package carrier
```

Only after parity and authority metadata pass should internal callers migrate.

## Rollback

Disable `NativeVliwLoadStore` in the production profile. Existing raw compatibility facades remain available.

## Merge gate

- No memory publication claim.
- No commit/retire claim.
- No fallback route.
- Decode/encode parity and golden artifacts green.
