# Phase 00 clean-subject verification handoff

## Pinned dirty-attributed reference

- Base SHA: `d4eeec71fa7d9e3a0efa8042dc667422f46181ac`
- Branch: `refactor/compiler-core-authority-boundaries`
- Reference disposition: `DirtyAttributed`, never release evidence
- Compiler source manifest: `1292` files,
  `57e392e1bf4e8d490de0c4c88f86ad56920f7d7775b75c5d1b1135fce670a110`
- Runtime-control source manifest: `1397` files,
  `c8050281fb26c9241399348c9a0535205a002415ef04ff120f2ab61a0d8b8bb0`
- Deterministic compiler baseline artifact:
  `64fb8cf54d74935088cd50ff0b0431b4bd3968a620d276e763c56f79d4330360`
- Deterministic runtime-control artifact:
  `bf07ee0e8a2d4f257980cc23b387fe48d3494885577579b4ea8268ac2bb13424`

The current worktree contains user-owned changes outside RefPlan3 scope. Phase 00 cannot be marked
`Verified`, and Phase 01 cannot start, until the intended subject is made clean by an explicitly
authorized user workflow. This handoff does not prescribe or perform commit, reset, checkout,
stash, cleanup, or any remote operation.

## Clean-subject rerun gate

From the repository root, on the exact clean SHA selected by the user:

```powershell
git status --short
git rev-parse HEAD
git branch --show-current

dotnet build .\HybridCPU_Compiler\Tools\Phase00Baseline\HybridCPU.Compiler.Phase00Baseline.csproj --no-restore --verbosity minimal
dotnet run --project .\HybridCPU_Compiler\Tools\Phase00Baseline\HybridCPU.Compiler.Phase00Baseline.csproj --no-build -- `
  --repo-root "." `
  --output-dir ".\HybridCPU_Compiler\RefPlan3\evidence\<clean-subject-compiler>" `
  --configuration Debug

dotnet build .\HybridCPU_Compiler\Tools\Phase00RuntimeControls\HybridCPU.Compiler.Phase00RuntimeControls.csproj --no-restore --verbosity minimal
dotnet run --project .\HybridCPU_Compiler\Tools\Phase00RuntimeControls\HybridCPU.Compiler.Phase00RuntimeControls.csproj --no-build -- `
  --repo-root "." `
  --output-dir ".\HybridCPU_Compiler\RefPlan3\evidence\<clean-subject-runtime-controls>" `
  --configuration Debug `
  --repetitions 3
```

Do not pass `--allow-dirty-attributed`. Both tools must accept the subject as clean and their
invocation artifacts must record that exact SHA and source manifest.

## Required comparisons

1. Phase 00 compiler-focused tests and the full compiler-focused suite pass on the same SHA.
2. Repeated deterministic compiler artifacts are byte-identical; resource telemetry is compared
   statistically and remains excluded from code selection.
3. Six profile bundle/group/width counts remain `60/61/36/37/36/36` and
   `3.0833/3.0492/4.5556/4.4595/4.5556/4.5556`; makespans remain
   `75/75/48/49/48/48`, unless an intentional subject change is separately attributed.
4. Replay, safety, stream-vector and matrix-tile normalized outcomes each have one fingerprint
   across three repetitions. Compare semantic counters and fingerprints to the archived
   dirty-attributed reference; timing and managed memory are telemetry only.
5. `git diff --check` passes and the post-run worktree stays clean except for newly generated,
   intentionally archived evidence.

## Exact next gate

After those checks, update Phase 00 from `Implemented` to `Verified` with the clean SHA and
artifact hashes. Only then may Phase 01 begin, with its contracts/extraction/default-off shadow
slice described in `01-resource-footprint-and-machine-model.md`. No resource decision, placement,
schedule, bundle, emitted byte, annotation, or runtime-authority behavior may change in that slice.
