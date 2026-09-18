# Phase 00 compiler baseline — current dirty-attributed subject

- Base SHA: `d4eeec71fa7d9e3a0efa8042dc667422f46181ac`
- Branch: `refactor/compiler-core-authority-boundaries`
- Disposition: `DirtyAttributed`; this is local compiler evidence, not release evidence.
- Exact source files: `1292`, including compiler/build sources, read-only referenced ISE
  build sources, Phase 00 tests and the historical snapshot.
- Runtime execution/publication/admission/FSP/replay/commit/retire: not invoked.

## Capture command

```powershell
dotnet run --project HybridCPU_Compiler/Tools/Phase00Baseline/HybridCPU.Compiler.Phase00Baseline.csproj --no-build --no-restore -- --repo-root "C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE" --output-dir "C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\HybridCPU_Compiler\RefPlan3\evidence\2026-08-19-phase00-current-dirty-attributed" --configuration Debug --allow-dirty-attributed
```

Exit code: `0`.

The command was repeated with `--overwrite`; deterministic artifact SHA-256 was identical across
both runs. The resource artifact is intentionally non-deterministic telemetry.

## Artifacts

| File | SHA-256 | Meaning |
| --- | --- | --- |
| `compiler_phase00_baseline_v1.json` | `64fb8cf54d74935088cd50ff0b0431b4bd3968a620d276e763c56f79d4330360` | Deterministic manifests and metrics for six compiler profiles |
| `compiler_phase00_invocation_v1.json` | `9ef7debe351ebccbda6a0f12bf25b46bf605fcfa0ebfd8f00a0fbe779c6c21d2` | Capture identity and source scope |
| `compiler_phase00_resources_v1.json` | `f0d0b5e45c4c86bfbfd1f06fb0a8725914fa6c533c8126a49eaa50c7ea4cf4aa` | Wall-clock/managed-memory observations; never code-selection input |

## Compiler KPI reconciliation

| Profile | Instructions | `schedule_cycles` | cycle groups / bundles | Average width | Historical group delta |
| --- | ---: | ---: | ---: | ---: | ---: |
| `alu` | 185 | 75 | 60 | 3.0833 | 0 |
| `novt` | 186 | 75 | 61 | 3.0492 | 0 |
| `vt` | 164 | 48 | 36 | 4.5556 | 0 |
| `max` | 165 | 49 | 37 | 4.4595 | 0 |
| `lk` | 164 | 48 | 36 | 4.5556 | 0 |
| `bnmcz` | 164 | 48 | 36 | 4.5556 | 0 |

The historical report exposed non-empty cycle groups, not latency-inclusive makespan. Therefore
historical `schedule_cycles` is `Unavailable`, while historical group/bundle/width deltas are zero.

## Remaining gate

This artifact closes the six-profile compiler reproduction. Replay, safety, stream-vector and
matrix-tile are captured separately in the sibling runtime-control archive without changing
runtime sources. The remaining `Verified` gate is a rerun on a clean pinned subject; this
dirty-attributed archive is not release evidence.
