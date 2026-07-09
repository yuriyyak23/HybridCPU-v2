# 06 — NativeVliwScalar production provider

## Goal

Introduce the first real production-lowering provider for `ExecutionContourKind.NativeVliwScalar`.

This should be the first production provider because it has the smallest sideband/descriptor surface and the clearest ISE lane ownership: scalar ALU operations belong to the native VLIW scalar/ALU contour and still require runtime Legality A/B and runtime execution.

## What is produced

A scalar production provider may produce:

- carrier words/bytes for an explicitly supported scalar opcode subset;
- structural typed-slot facts;
- no-fallback proof;
- evidence envelope;
- telemetry snapshot;
- runtime-authority-pending header.

It must not produce:

- runtime execution;
- architectural publication;
- commit;
- retire;
- final runtime legality;
- VMX/SecureCompute authority;
- fallback scalarization for non-scalar contours.

## Required gates

- `ProductionProfile.EnablesProductionLowering == true`.
- `EnabledContours` contains `NativeVliwScalar`.
- Intent classifier covers scalar opcode family.
- Contour selector returns `NativeVliwScalar`.
- ISE opcode identity is exact.
- Carrier encoding has golden artifact coverage.
- ISE decode/encode parity exists.
- Slot facts are structural only.
- Runtime Legality A/B dependency is preserved.
- No-fallback proof forbids cross-contour fallback.

## Tests

### Positive

- scalar ADD/SUB/MUL-like carrier golden snapshots;
- scalar immediate variants where supported;
- carrier -> ISE decode parity;
- ISE encode -> golden carrier parity;
- production package header still declares runtime authority pending;
- telemetry contains contour, intent, opcode family, producer surface, gate id, artifact ids.

### Negative

- vector opcode rejected by scalar provider;
- load/store opcode rejected by scalar provider;
- branch/control opcode rejected by scalar provider;
- MatrixTile/DSC/L7/VMX/SecureCompute rejected by scalar provider;
- missing production profile gate rejects;
- missing parity/golden gate rejects.

## Caller migration

Do not migrate public callers immediately. First run shadow compare:

```text
legacy/raw carrier construction -> production package carrier
```

The two must match on carrier words for the supported scalar subset, while only the production path carries the new authority/evidence metadata.

## Rollback

Turn off the scalar production profile gate. Shell provider and compatibility carrier paths remain intact.

## Merge gate

- Golden and parity tests green.
- No new fallback path.
- Existing shell provider still rejects production lowering when the production provider is not explicitly used.
