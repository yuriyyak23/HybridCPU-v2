# Phase 4 - StreamEngine / DMA / DmaStreamCompute Separation

Status date: 2026-04-29.

Status: closed for the current implemented contract; future integration remains
architecture-gated.

Goal: prevent architectural mixing between `StreamEngine`, `DMAController`,
`DmaStreamCompute`, and external accelerator model APIs.

## Ownership Matrix

| Component | Current execution owner | Current memory publication | Current lifecycle/queue/fence |
|---|---|---|---|
| `StreamEngine` | Stream/vector instructions only | Via stream/vector `BurstIO` helpers | No DSC/L7 token lifecycle. |
| `DmaStreamCompute` | No canonical micro-op execution; explicit runtime helper only | Token commit writes physical memory | DSC token model; no ISA queue/fence. |
| `DMAController` | DMA channel cycles when explicitly driven | Memory-to-memory channel transfers | DMA channel state and controls only. |
| L7-SDC model | Backend/queue/fence helpers in explicit model paths | Commit coordinator can publish staged writes | `AcceleratorToken`, model queue/fence only. |

## Current Separation Claims

- `StreamEngine.Execute(...)` is not a DSC descriptor executor.
- `StreamEngine.CaptureRetireWindowPublications(...)` is not DSC token commit.
- `BurstIO` DMA paths are synchronous helper behavior in current ISE.
- `DmaStreamComputeRuntime` does not call `StreamEngine`.
- `DmaStreamComputeRuntime` does not call `DMAController`.
- DSC token lifecycle is not DMA channel lifecycle.
- DMA channel pause/resume/cancel does not imply DSC ISA controls.
- L7 conflict notifications do not imply a global CPU load/store hook.

## Documentation Result

WhiteBook terminology now uses:

- `DMA`: separate memory transfer controller and channel API.
- `StreamEngine`: in-core stream/vector execution module.
- `DmaStreamCompute`: lane6 descriptor carrier plus explicit runtime helper and
  token model.
- `ExternalAccelerator`: lane7 L7-SDC carrier/model subsystem.
- `backend`: runtime/model executor, not automatic pipeline execution.
- `token`: model state and commit container; not authority by itself.
- `commit`: explicit publication operation; not always retire.
- `retire`: pipeline publication/exception boundary.
- `fence`: model API unless executable instruction path is implemented.
- `queue`: model queue unless pipeline/device protocol is implemented.

## Test Evidence

- StreamEngine stream/vector tests remain separate from DSC tests.
- DSC runtime/helper tests cover physical memory, staged writes, and commit.
- DMAController tests cover channel state and control surface separately.
- DSC token tests prove no pause/resume/reset/fence channel-control surface.
- L7 conflict-manager tests prove optionality and no hidden global hook.

## Future Gate

Any future shared scheduling, async DMA overlap, global ordering, global
conflict manager, or cache/coherency integration must go through Phase 6 and a
separate architecture approval.
