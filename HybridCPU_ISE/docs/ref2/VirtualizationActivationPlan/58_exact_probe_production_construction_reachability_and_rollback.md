# Phase 58 — exact probe production construction, reachability and rollback

## 2026-06-11 Audit Contract

- File name: `58_exact_probe_production_construction_reachability_and_rollback.md`.
- Purpose: Close production construction, exact reachability, rollback and release-SHA evidence preparation for the existing exact probe contour.
- Status: Implementation subject pending full gates and later non-self-referential evidence.
- Scope: `PROBE_NO_STATE_V1` construction through the existing canonical scheduler and existing PR-J kill switch only.
- No-goals: Another profile release, VMREAD/VMWRITE, compiler, nested, SecureCompute, IOMMU/DMA/device or broad virtualization.
- Code anchors: `CpuCorePlatformContext.cs`, `CPU_Core.Pipeline.cs`, `CPU_Core.PipelineExecution.VmxRetire.cs`, and `DomainHypercallExactRuntimeProfile.cs`.
- Authority owner: Existing neutral domain, capability, admission, execution, completion and retire owners only.
- Required RFC/ADR: Repository-owner bounded Phase-58 authorization and the already accepted exact Phase-38/PR-J contour.
- Acceptance criteria: Explicit immutable opt-in, reviewed contour SHA binding, real production caller, default-off equivalence, fail-closed construction and deterministic rollback without implicit reactivation.
- Tests/static scans: Focused construction/negative/race tests, PR-J regressions, plan guards, full VMX/SecureCompute matrices, Release without hooks, forbidden scans, clean-source verification and `git diff --check`.
- Risks: Accidental default activation, scheduler replacement, rollback/reactivation race, SHA drift, compatibility authority escalation or combining later release pools.
- Next-gate dependency: Green subject and later evidence, then `NextOpenPool=None`; a separate exact-profile release selection is required.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Verdict

`CLOSED GREEN SUBJECT AND LATER NON-SELF-REFERENTIAL EVIDENCE`.

This pool closes only the production construction gap for the already-proven
neutral `PROBE_NO_STATE_V1` contour. It does not release another profile, open
VMREAD, authorize compiler emission, or broaden virtualization.

## Authorization and exact scope

The repository owner explicitly authorized, in order:

1. real production construction/reachability, rollback and a release SHA for
   the already-proven contour;
2. reset `NextOpenPool` to `None` before considering any later release pool.

The selected contour is the existing Phase-38/PR-J exact profile:

- decision `D2-HV-VMCALL-RUNTIME-V1-PROBE-0001`;
- operation namespace `HybridCPU.VMCALL.Runtime.v1`;
- leaf `0x0001`;
- operation `PROBE_NO_STATE_V1`;
- reviewed contour subject
  `bcd2d7f4654d4dab17c7a6705cb885fdd572510d`.

Phase 57 VMREAD compositions are not part of this pool. They remain separately
default-disabled and require the later ordered read-only VMREAD release pool.

## Factual preflight

Baseline:

- HEAD `87f1f5884d35a66ce24c905eab9fa18198d4017a`;
- tree `f7e47eaea108886e8058d30d3b334d94d4c6c534`;
- branch `refactor/compiler-core-authority-boundaries`;
- staged files: none;
- pre-existing user changes and untracked artifacts preserved;
- focused Phase-51/status/documentation gate: `63/63`;
- untracked or ignored C# under `HybridCPU_ISE/CloseToHSL`: `0/0`.

Caller-graph audit found that `DomainHypercallExactRuntimeProfile` construction
and activation had only test callers. The canonical production scheduler seam
already existed in `CPU_Core.InitializePipeline`, and the exact profile already
owned activation, E2–E7 lifecycle, drain and deterministic kill-switch rollback.
No new scheduler, completion owner, retire owner or compatibility authority was
needed.

## Bounded correction

`CpuExactProbeRuntimeConstructionProfile` is an immutable explicit opt-in bound
to one non-zero neutral domain tag and the reviewed contour subject SHA. The
default `CpuCorePlatformContext` contains no profile and remains fault-only.

The production construction chain is:

```text
CpuCorePlatformContext(exact profile or absent)
  -> CPU_Core construction
  -> CPU_Core.InitializePipeline
  -> existing canonical MicroOpScheduler
  -> DomainHypercallExactRuntimeProfile
  -> exact Phase-38 activation request
```

`InitializePipeline` preserves its already-selected scheduler across repeated
initialization. It cannot silently replace or reactivate an exact binding.
Activation failure invokes the existing kill switch and fails construction
closed.

The operational rollback entry point
`CPU_Core.DisableConfiguredExactProbeRuntimeProfile` delegates to the existing
kill-switch linearization:

```text
close new E2
  -> drain transitions and E2/E3/E5/E6 registries
  -> revoke exact binding and grant
  -> restore deterministic fault-only fallback
```

Rollback does not clear the immutable construction request and therefore cannot
be followed by implicit reactivation. A later initialization attempt fails
closed. A new core construction is required for any future explicit activation.

## Authority boundary

- Runtime authority remains with the existing neutral domain, capability,
  admission, execution, completion and retire owners.
- The recorded Git SHA is evidence binding only.
- VMX/VMCS remains compatibility vocabulary and supplies no construction,
  admission, completion or retire authority.
- No VMREAD, VMWRITE, nested, IOMMU/DMA/device, SecureCompute or compiler path is
  opened.
- No frontend trap, VMCALL-created completion, VMCS backing state or diagnostic
  artifact is used as surrogate authority.

## Tests and exit criteria

Focused tests cover default-disabled construction, exact reviewed-SHA
construction, malformed/foreign SHA and domain denial, production caller
presence, repeated initialization, rollback, no implicit reactivation and the
initialize-versus-rollback race. Existing PR-J activation/rollback tests remain
part of the focused gate.

The immutable release-candidate subject is
`e29d6b2150bf10c136ba3897eb17a9cab03c9967`, tree
`b3cd41275e548dc39fc7c0c7c5d411c339a0f29e`. The later
`2026-08-13-phase58-exact-probe-production-construction-clean-evidence.json`
record binds its exact source bytes and does not name its own containing commit.

Final gates were focused `82/82`, initialize/rollback race five runs of `1/1`,
worktree VMX `565/565`, worktree SecureCompute `406/406`, clean staged VMX
`547/547`, clean staged SecureCompute `395/395`, and Release without `TESTING`
or internal hooks at `0 warnings / 0 errors`. Canonical `CloseToHSL` contained
zero untracked and zero ignored C# dependencies.

After evidence closure:

```text
NextOpenPool = None
NextCandidatePool = NoneUntilSeparateExactProfileReleaseSelection
```

The next permitted decision is selection of one existing exact profile for a
separate release pool. It is not opened by Phase 58 itself, and Phase 58 makes
no release claim.
