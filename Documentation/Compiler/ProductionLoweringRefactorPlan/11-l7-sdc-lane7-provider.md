# 11 — L7SdcLane7 descriptor-backed production provider

## Goal

Introduce a descriptor-backed production provider for `ExecutionContourKind.L7SdcLane7` after DSC lane6 and parity infrastructure are proven.

L7-SDC production lowering must remain distinct from DSC, branch/control, VMX, and system singleton contours. The compiler may package an `ACCEL_SUBMIT` carrier with descriptor sideband, but descriptor ownership, submit guard acceptance, token lifecycle, completion, publication, commit, and retire remain runtime-owned.

## What is produced

An L7-SDC production provider may produce:

- lane7 `ACCEL_SUBMIT` carrier words/bytes;
- accelerator command descriptor envelope;
- descriptor reference/identity evidence;
- source/destination normalized footprint sideband;
- owner/domain guard observation evidence;
- submit guard observation evidence;
- token destination structural facts;
- no-fallback proof;
- runtime bridge handoff envelope, if approved by gates;
- evidence and telemetry;
- runtime-authority-pending header.

It must not produce:

- token authority;
- virtual-handle authority;
- runtime backend protocol authority;
- result publication;
- partial completion success;
- commit or retire;
- VMX execution;
- branch/control authority;
- fallback to DSC, stream/vector, scalar, or descriptorless submit.

## Required gates

- `L7SdcLane7` production gate enabled.
- Descriptor ABI/header version accepted.
- Descriptor reference covers accepted payload.
- Descriptor identity hash matches reference when provided.
- Owner/domain guard observation exists and remains evidence-only.
- Submit guard observation exists and remains evidence-only.
- Guard owner binding matches descriptor owner binding.
- Partial completion policy is AllOrNone.
- Source/destination footprints and normalized footprint hash are non-empty.
- Token destination is represented as structural handoff, not token authority.
- Future L7 backend requirements are satisfied or visible as runtime gates.
- Descriptorless submit fail-closed test exists.
- Golden descriptor-backed package exists.
- ISE lane7 decode/encode/lane/slot parity exists.

## Tests

### Positive

- ACCEL_SUBMIT carrier golden snapshot;
- descriptor sideband snapshot;
- token destination structural fact snapshot;
- owner guard and submit guard observations remain evidence-only;
- runtime dependency includes Legality A/B, execution, publication, commit, retire;
- L7 completion/routing compatibility does not become compiler authority.

### Negative

- descriptorless submit rejects;
- parser-only descriptor rejects;
- owner guard failure rejects;
- submit guard failure rejects;
- missing normalized footprint rejects;
- non-AllOrNone completion rejects;
- DSC descriptor routed to L7 provider rejects;
- VMX/system singleton/branch opcodes reject;
- runtime fallback promise rejects;
- production gate missing rejects.

## Migration risk

The existing typed `CompileAcceleratorSubmit` facade already validates descriptor and guard inputs before appending an `ACCEL_SUBMIT` carrier. It must remain a compatibility/typed facade until wrapped by an explicit production package. Do not reclassify its current `CompilerAcceleratorLoweringDecision` as production authority.

## Rollback

Disable the `L7SdcLane7` production gate. Existing typed/compat facades remain unchanged.

## Merge gate

- Descriptorless submit remains fail-closed.
- DSC and L7 remain distinct contours.
- Token/certificate/descriptor evidence is not execution authority.
- Runtime backend protocol gates remain explicit.
