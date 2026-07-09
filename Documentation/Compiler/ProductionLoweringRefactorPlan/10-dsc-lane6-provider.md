# 10 — DmaStreamComputeLane6 descriptor-backed production provider

## Goal

Introduce a descriptor-backed production provider for `ExecutionContourKind.DmaStreamComputeLane6` only after the generic production package model, golden artifacts, and ISE parity harness are green.

DSC lane6 production lowering must stay distinct from stream/vector, MatrixTile, and L7-SDC. The compiler may package a lane6 carrier with descriptor/footprint sideband, but runtime ownership of legality, queueing, ordering, completion, publication, commit, and retire remains mandatory.

## What is produced

A DSC production provider may produce:

- lane6 `DmaStreamCompute` carrier words/bytes;
- DSC descriptor envelope;
- descriptor reference/identity evidence;
- normalized read/write footprint sideband;
- owner/domain guard observation evidence;
- no-fallback proof;
- runtime bridge handoff envelope, if approved by the gate model;
- evidence and telemetry;
- runtime-authority-pending header.

It must not produce:

- queue token authority;
- runtime token fence authority;
- cache/order/fault authority;
- partial completion success;
- memory publication;
- commit or retire;
- fallback to L7, stream/vector, MatrixTile, or scalar.

## Required gates

- `DmaStreamComputeLane6` production gate enabled.
- Descriptor ABI/header version accepted.
- Descriptor reference covers accepted payload.
- Descriptor identity hash matches reference when provided.
- Owner/domain guard observation is accepted but remains runtime-owned evidence.
- Device id is canonical lane6 DMA/stream device id.
- Operation, element type, and shape are supported.
- Range encoding is inline contiguous or explicitly approved.
- Partial completion policy is AllOrNone.
- Non-empty normalized read/write footprints exist.
- Future DSC backend requirements are satisfied or explicitly represented as missing runtime gates.
- Golden descriptor-backed package exists.
- ISE lane6 decode/encode/lane/slot parity exists.

## Tests

### Positive

- descriptor-backed lane6 carrier golden snapshot;
- descriptor identity/reference parity;
- normalized footprint hash snapshot;
- owner guard evidence remains evidence-only;
- runtime dependency includes Legality A/B, execution, publication, commit, retire;
- lane6 completion route compatibility does not become compiler authority.

### Negative

- descriptorless DSC submit rejects;
- descriptor parser success without owner guard rejects;
- wrong device id rejects;
- missing normalized footprint rejects;
- non-AllOrNone completion rejects;
- unsupported operation/type/shape rejects;
- L7 descriptor routed to DSC provider rejects;
- production gate missing rejects.

## Migration risk

The existing `CompileDmaStreamComputeDescriptor` and `CompileDmaStreamCompute` facades already append a lane6 carrier after descriptor checks. They are compatibility surfaces, not production providers. Migration must wrap them through a production package and shadow compare rather than silently reclassifying those facades.

## Rollback

Disable the `DmaStreamComputeLane6` production gate. Compatibility facades and descriptor parser behavior remain unchanged.

## Merge gate

- No hidden L7/stream/scalar fallback.
- Descriptor ABI success is not production authority by itself.
- Runtime queue/fence/order/cache-fault requirements remain visible.
