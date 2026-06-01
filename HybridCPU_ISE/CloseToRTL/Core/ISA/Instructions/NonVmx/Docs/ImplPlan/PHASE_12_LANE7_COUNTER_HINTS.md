# Phase 12 - Lane7 Counter Hints

## Goal

Promote deterministic Lane7 counter and hint rows without exposing host
time/evidence or promising progress guarantees beyond the explicit ABI.

## Production Path Overlay

Use the `Lane7 System / Control-Plane Rows` path in `README.md`. Counter rows
need deterministic source policy, retire-owned publication, replay/migration
semantics, and guest-visible policy if virtualized. `PAUSE` needs a no-state
hint contract and scheduling legality. VMX-compatible projection is required
for guest-visible counters, host evidence, migration/checkpoint, or nested
virtualization policy.

## Instructions / Contours

- Counters: `RDTIME`, `RDINSTRET`.
- Hint: `PAUSE`.

## Existing Partial Files

- `Lane07SystemControl\Counters\RdtimeInstruction.cs`
- `Lane07SystemControl\Counters\RdinstretInstruction.cs`
- `Lane07SystemControl\Hints\PauseInstruction.cs`

## New Partial Files Allowed

- `RdtimeInstruction.CounterContract.cs`
- `RdinstretInstruction.CounterContract.cs`
- `PauseInstruction.HintContract.cs`
- Optional `*.ReplayContract.cs` for deterministic replay notes.

## Local CloseToRTL Logic

Production/local partials may document counter source requirements, replay determinism, retire-owned publication shape, and that `PAUSE` is a scheduling hint with no architectural progress guarantee. Execution opens only when the Lane7 production path closes deterministic source and replay policy.

## Production Evidence Gates

Counter ABI, privilege policy, decoder/encoder ABI, `InstructionIR` projection, materializer, typed Lane7 MicroOp, runtime source, retire publication, replay determinism, virtualization policy if guest-visible counter behavior is exposed, conformance, and golden/no-emission tests.

## Metadata Constants

Preserve `Lane7CounterReplayDeferred`, `Lane7HintNoExecutionGuarantee`, `RequiresCounterAbi`, `RequiresReplayDeterminism`, `RequiresRetireOwnedPublication`, `NoHostEvidenceLeak`, `HasOpcodeAllocation=false`, `IsExecutable=false`, `CompilerHelperAllowed=false`, VMX-neutral markers, and future virtualization flags.

## Evidence Chain Stories

- Decoder/encoder ABI: production gate for opcode opening.
- InstructionIR/projection: production gate; counter/hint shapes need typed IR.
- Typed MicroOp/materializer: production gate; Lane7 counter/hint MicroOps.
- Execute/capture semantics: future deterministic source capture; no host wall-clock leakage.
- Retire/writeback/side effects: counters publish scalar result at retire; `PAUSE` has no architectural state.
- Replay/rollback/conformance: deterministic replay for counters, retire-count boundary, rollback stability, no progress guarantee tests for `PAUSE`.

## Boundaries

- Vector VLM: not applicable.
- Lane6: not applicable.
- Lane7/VMX: guest-visible counters may require virtualization policy; VMX sees only generic runtime projection unless boundary is crossed.
- No-emission: compiler helpers closed.

## Risks

- Host wall-clock leakage through `RDTIME`.
- `RDINSTRET` counting before retire is finalized.
- `PAUSE` being treated as a synchronization primitive.

## Closure Criteria

- A production package may promote each row only after deterministic counter source, typed Lane7 MicroOp, retire publication, replay, golden, and virtualization-boundary policy close where needed.
- Local partials without that package describe deterministic contracts only.

## Prohibited Actions

- Do not publish host-owned time/counter evidence.
- Add VMX-compatible projection only if guest-visible counter virtualization crosses the boundary; otherwise stay on the shared Lane7 runtime path.
