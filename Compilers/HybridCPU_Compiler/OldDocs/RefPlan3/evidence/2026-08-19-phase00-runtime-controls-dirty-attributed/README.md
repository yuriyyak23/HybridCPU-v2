# Phase 00 runtime controls — dirty-attributed local evidence

## Subject

- Base commit: `d4eeec71fa7d9e3a0efa8042dc667422f46181ac`
- Branch: `refactor/compiler-core-authority-boundaries`
- Disposition: `DirtyAttributed`; this is verification input, not release evidence.
- Exact source files: `1397`
- Source-manifest fingerprint: `c8050281fb26c9241399348c9a0535205a002415ef04ff120f2ab61a0d8b8bb0`
- Configuration: `Debug`
- Tool: `HybridCPU.Compiler.Phase00RuntimeControls/v1`

The manifest hashes compiler sources, the read-only runtime sources, the unchanged
`TestAssemblerConsoleApps` runner sources, the Phase 00 compiler tests and root build inputs.
Evidence output directories and `bin`/`obj` are excluded.

## Commands

```powershell
dotnet build .\HybridCPU_Compiler\Tools\Phase00RuntimeControls\HybridCPU.Compiler.Phase00RuntimeControls.csproj --no-restore --verbosity minimal

dotnet run --project .\HybridCPU_Compiler\Tools\Phase00RuntimeControls\HybridCPU.Compiler.Phase00RuntimeControls.csproj --no-build -- `
  --repo-root "." `
  --output-dir ".\HybridCPU_Compiler\RefPlan3\evidence\2026-08-19-phase00-runtime-controls-dirty-attributed" `
  --configuration Debug `
  --repetitions 3 `
  --allow-dirty-attributed `
  --overwrite
```

Build result: `0` warnings, `0` errors. Capture exit code: `0`.

The negative probe without `--allow-dirty-attributed` exited `1` before creating its output
directory, proving that an unattributed dirty subject fails closed.

## Results

| Profile | Iterations per run | Repetitions | All passed | Distinct normalized outcomes | Normalized fingerprint | Process median ms | CV |
| --- | ---: | ---: | --- | ---: | --- | ---: | ---: |
| replay | 1,000,000 | 3 | yes | 1 | `d34c2febab6f1f45c0ee915a9727af3fc70a050ecfe23d91d00380e84802c7f6` | 50,167.884 | 0.004329 |
| safety | 1 | 3 | yes | 1 | `98ebc95fb105b9a0c8f276b79fdc45370aaf2b1101ede817be869a7a5bbfc964` | 256.170 | 0.001414 |
| stream-vector | 250 | 3 | yes | 1 | `6d0c64e7f5d001fc0813cca44e06c5dc875201757d0588426736aa8dee90da61` | 40,528.914 | 0.027788 |
| matrix-tile | 500 | 3 | yes | 1 | `a2aff43167d984412a2b6b16ffcfbf0be42cbc8a72bf0b965e679226a0b0f2eb` | 5,695.493 | 0.038398 |

Representative semantic counters from each byte-identical normalized outcome:

- replay stable phase: `1,000,000` cycles, `3,000,000` ready hits, `0` misses;
  rotating phase: `1,000,000` cycles and `3,999,999` invalidations;
- safety: `5/5` controls passed, with one owner, domain, boundary, invalid-replay and
  stale-witness rejection each;
- stream-vector: `6/6` scenarios passed, `19,500` dynamic instructions,
  checksum `16005502929113034529`;
- matrix-tile: `17/17` scenarios passed, `6,026` runtime instructions, `34` compiler
  emissions, `6,016` retire publications, `4,018` replay round trips, `53` fail-closed
  rejections, checksum `17588030958249760693`.

Canonicalization removes only `StartedUtc`, `FinishedUtc`, elapsed fields and properties ending
in `PerMillisecond`. Workload identity, pass/failure state, checksums and all architectural,
compiler-emission, replay, publication and rejection counters remain fingerprinted. Raw JSON,
stdout and stderr remain under `raw/<profile>/run-NNN/`; physical time is reported separately and
never selects compiler code.

## Artifact hashes

- `compiler_phase00_runtime_controls_v1.json`:
  `bf07ee0e8a2d4f257980cc23b387fe48d3494885577579b4ea8268ac2bb13424`
- `compiler_phase00_runtime_control_resources_v1.json`:
  `f56a71b6de1be30d55883593eed48105d4c402497897204a81aef19bf4a93bd2`
- `compiler_phase00_runtime_control_invocation_v1.json`:
  `1dc4bb5537a282457ab71b9eff89358e4e537ef1c54497a46e9b8f91b71fb180`

## Gate disposition

The missing Phase 00 runtime controls are reproduced and attributable. Phase 00 nevertheless
remains `Implemented`, not `Verified`, because `RefPlan3StatusV1` requires a clean pinned subject.
The current user-owned dirty worktree was preserved; no reset, commit or push was performed.
