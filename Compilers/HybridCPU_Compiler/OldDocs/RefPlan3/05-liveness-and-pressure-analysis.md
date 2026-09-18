# 05 — Region liveness and register-pressure analysis

## Goal and motivation

Make live-in/live-out, interval and pressure evidence explicit over the BB-only region contract before any rename or cross-block expansion.

**Status:** `Planned`.

## Confirmed starting state

- IR dependencies recognize packed architectural register operands through normalized `IrOperand(Kind, Value, Name)` and VT-qualified keys.
- There is no first-class virtual-value namespace, live-interval model or local architectural-carrier allocator.
- Existing scheduler uses coarse profile-backed register/certificate pressure heuristics; runtime owns physical PRF, rename and retire state.

## In scope

- Static region liveness, architectural register access normalization, live intervals and pressure summaries.
- PRF read/write port and register-group demand estimates through Phase 03 resource facts.
- Pressure-aware schedule cost and rejection/telemetry in analysis/shadow mode.

## Explicit non-goals

- No WAR/WAW removal, operand rewriting, carrier allocation or SSA conversion.
- No compiler claim over physical PRF/rename/retire state.

## Canonical types/components

Proposed `IrArchitecturalRegisterRef`, `IrLiveInterval`, `IrRegionLiveness`, `HybridCpuRegisterPressureReport`; `IrSchedulingRegion`, dependency graph and machine resource model are inputs.

## Target architecture and data flow

```text
BB-only region + normalized defs/uses + boundary edges
 -> architectural register normalization
 -> live-in/live-out + live intervals
 -> per-cycle/region pressure and PRF/group demand
 -> scheduling cost/diagnostics only
```

## Invariants and authority boundaries

- Unknown operand encoding is pressure-unknown and prevents transformations; it is never assumed free.
- Profile may rank profitable regions but cannot change defs/uses or liveness.
- Runtime remains owner of physical registers, ports, rename, replay, commit and retire.
- Analysis output is deterministic and version/model bound.

## Dependencies

Phase 04 verified.

## Implementation backlog

1. Specify architectural register extraction for every current operand encoding, including packed tuples and predicate registers.
2. Compute region live-in/live-out from intra/inter-block dependencies.
3. Build deterministic half-open live intervals and peak pressure by register class/group.
4. Estimate per-cycle PRF reads/writes and group conflicts from a candidate schedule.
5. Calibrate predictions against runtime counters while keeping actual/predicted labels separate.
6. Add pressure to lexicographic cost only after correctness/cycles and initially in shadow mode.
7. Freeze scalar/vector/predicate/VT-local/cross-VT negative corpora.

## Migration and compatibility strategy

Analysis is additive. Unknown/unsupported operands disable pressure-based decisions and preserve the current schedule. No serialized ABI or public compatibility API changes.

## Tests

### Positive/property/determinism

- Chains, diamonds, dead defs, loop-shaped BBs, packed rd/rs tuples, predicate uses and VT-qualified names.
- Dataflow fixed-point and interval overlap properties.
- Stable reports across insertion/map order and repeated runs.

### Negative

- Malformed packed operand, ambiguous register kind, boundary mismatch, special/system register and overflow.

### Static

- No operand rewrite or runtime PRF/rename owner construction.
- Profile values cannot enter liveness transfer functions.

## Benchmarks and KPI

- Liveness agrees with exhaustive reference on generated <=32-op regions.
- Report peak live values, peak PRF read/write ports and register-group conflicts predicted.
- Analysis time/memory p95 <=1.05x Phase 04; codegen bytes unchanged.

## Diagnostics and telemetry

Region/value/register class, interval endpoints, live-in/out, peak pressure, PRF port/group demand, unknown reason and model digest.

## Bounded-search, timeout and fallback policy

No wall-clock cutoff. Deterministic caps: <=4096 instructions, <=8192 values and <=32768 use/def edges per region; cap yields `PressureUnknown` and unchanged scheduling.

## Risks and forbidden shortcuts

- Do not call runtime reject counts foreground compiler illegality.
- Do not infer a virtual namespace from `IrOperand.Name` strings.
- Do not remove WAR/WAW without Phase 06 allocation proof.

## Rollback / kill switch

Disable pressure-cost consumption; liveness reporting may remain observational.

## Acceptance and merge gates

- Exhaustive/property/reference checks and operand inventory complete.
- Byte-identical output in analysis-only mode.
- Prediction calibration reports foreground and donor/FSP paths separately.

## Status criteria

- `implemented`: liveness/interval/pressure reports exist.
- `verified`: properties, calibration and overhead gates pass.
- `default-enabled`: analysis may default on; cost use separately qualified.
- `release-authorized`: no register allocation or runtime authority.

## Residual work / next gate

Phase 06 may add a bounded local value/carrier layer; Phase 07 uses liveness at EBB boundaries.
