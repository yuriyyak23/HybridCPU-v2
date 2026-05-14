# Non-Executable Contours And Future Gates

Updated: 2026-05-14.

## Purpose

Значительная часть рефакторинга была не про включение новых возможностей, а про
честное закрытие границ: что исполняется, что является transport-only, а что
должно отказать до execution. Этот файл собирает такие границы.

## DescriptorOnly

DescriptorOnly означает: descriptor transport существует, но это не execution
approval.

Current DescriptorOnly contours:

| Contour | Boundary |
|---|---|
| `VGATHER` | Descriptor-only, non-executable. |
| `VSCATTER` | Descriptor-only, non-executable. |
| `DmaStreamCompute` | Descriptor-required Lane6 carrier, direct execute fail closed. |
| `ACCEL_SUBMIT` production execution | Descriptor-required Lane7 carrier, direct execute fail closed. |

DescriptorOnly rows must not get fallback execution. Если future work opens one
of them, it must start with a runtime-owned model and focused positive/negative
tests.

## CarrierOnly

CarrierOnly означает: runtime carrier exists, but backend execution/commit is
disabled.

Current carrier-only L7-SDC commands:

```text
ACCEL_QUERY_CAPS
ACCEL_POLL
ACCEL_WAIT
ACCEL_CANCEL
ACCEL_FENCE
```

Carrier-only commands must not imply capability lifecycle, token progress,
device-side commit or memory coherence until these are modeled end to end.

## ParserOnly

`DSC2` remains parser/model awareness only. Parser acceptance is not runtime
execution authority and must not be used to open materialization.

## OptionalDisabled

Matrix rows remain optional-disabled and decoder-rejected:

```text
MTILE_LOAD
MTILE_STORE
MTILE_MACC
MTRANSPOSE
```

Future matrix enablement requires:

- tile register file or explicit memory-backed tile model;
- descriptor ABI;
- load/store footprints;
- MAC and transpose semantics;
- MicroOp classes;
- resource and safety masks;
- lane placement and serialization;
- fault model;
- replay/rollback behavior;
- retire/writeback semantics;
- compiler lowering;
- positive and negative conformance tests.

Enum values or planning rows are not evidence.

## Reserved

Current reserved rows:

```text
SFENCE.VMA
DCACHE_CLEAN
DCACHE_INVAL
DCACHE_FLUSH
ICACHE_INVAL
```

They are not opened by Phase 10. `FENCE` and `FENCE_I` deliberately stop at a
bounded current-runtime model. A real cache/TLB/coherency contour requires its
own definition of:

- memory visibility domain;
- cache hierarchy state;
- instruction fetch interaction;
- TLB/VM state when relevant;
- DMA interaction when relevant;
- lane effects;
- rollback/replay behavior;
- ordering guarantees and unsupported masks;
- focused tests that prove both positive and fail-closed behavior.

## Future Vector Gates

Future vector work remains explicitly scoped:

- executable `VGATHER` / `VSCATTER`;
- vector indexed/2D ABI;
- future dot-product destination ABI variants;
- broad compiler vector lowering.

Opening any of these must preserve the current runtime-owned legality matrix.
Compiler or descriptor facts cannot bypass vector fail-closed rows.

## Future Lane6 Gate

Production Lane6 execution needs:

- backend execution contract;
- descriptor ownership and domain model;
- token lifecycle;
- staged commit;
- memory visibility;
- coherency interaction;
- replay/rollback;
- lane6 resource scheduling;
- direct-execute negative tests;
- compiler lowering only after runtime closure.

Until then, Lane6 remains descriptor-required and direct-execute fail closed.

## Future Lane7 Gate

Production Lane7 execution needs:

- backend/device contract;
- capability and token lifecycle;
- staged writeback;
- commit semantics;
- system-device memory visibility;
- fence interaction;
- replay/rollback;
- lane7 singleton scheduling;
- negative tests for direct execution and malformed descriptors;
- compiler lowering only after runtime closure.

Current `ACCEL_SUBMIT` is not production execution approval.

## Future Compiler Emission Gate

Compiler work must stay in `Documentation\CompilerRefactor` unless the runtime
task explicitly scopes compiler emission. For ISE continuation, compiler facts
are only relevant when the live compiler already emits the selected opcode/form.

Currently opened compiler facts:

- scalar tail through `ZEXT.W`;
- scoped `VSETVLI`;
- canonical zero-payload coordinator `FENCE`;
- descriptor carriers for Lane6/Lane7.

Currently closed compiler emission:

- atomic lowering;
- masked/general fence lowering;
- `FENCE_I` helper lowering;
- broad vector production lowering;
- matrix lowering;
- cache/TLB/coherency lowering;
- production Lane6/Lane7 lowering.

