# Phase 60 - exact read-only completion reason/qualification VMREAD release

- Status: `CLOSED GREEN SUBJECT AND LATER NON-SELF-REFERENTIAL EVIDENCE`
- Date: `2026-08-13`
- Exact profile: `ExitReason + reason-bound ExitQualification`
- Existing implementation subject: `f5fbdcb16b5fe5859311448dacf7a98e17217b20`
- Existing implementation tree: `2573f9a9a02f51d919e61b3358030ec199109fb3`

## 2026-06-11 Audit Contract

- File name: `60_completion_reason_qualification_exact_vmread_production_release.md`.
- Purpose: Release exactly one existing exact read-only VMREAD profile after Phase 59.
- Status: Release subject pending full gates and later non-self-referential evidence.
- Scope: Phase-55 `ExitReason` and reason-bound `ExitQualification` only, plus its exact operational kill switch.
- No-goals: GPA/EPT qualification release, descriptor-owned VMREAD groups, new fields, VMCALL, VMWRITE, compiler, nested, SecureCompute, IOMMU/DMA/device or broad VMREAD.
- Code anchors: `CpuCorePlatformContext.cs`, `CPU_Core.StateData.cs`, `CPU_Core.PipelineExecution.Materialization.cs`, `CompletionReasonQualificationVmReadScalarDeliveryCanonicalComposition.cs`, and `CPU_Core.CompletionVmReadState.cs`.
- Authority owner: Existing `DomainCompletionObservationOwner` and exact `CanonicalCpuInstructionTranslationFaultProducer`; compatibility mapping owns values only.
- Required RFC/ADR: Repository-owner ordered release authorization and the accepted Phase-55 exact D2 profile.
- Acceptance criteria: Real explicit production construction, exact source/producer mapping, default-off equivalence, receipt invalidation at kill-switch linearization, canonical PRF/writeback/retire and adjacent denial.
- Tests/static scans: Focused mapping/lifecycle/release/race tests, plan guards, VMX/SecureCompute matrices, Release without hooks, forbidden scans, clean-source verification and `git diff --check`.
- Risks: Releasing a test-only profile, stale receipt publication after rollback, inference/zero fallback, VMCS authority, or accidental adjacent-field activation.
- Next-gate dependency: Green subject and later evidence, then `NextOpenPool=None`; any new field group or VMCALL operation requires a separate exact selection.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Candidate selection proof

The repository contains several implemented exact VMREAD groups. The
descriptor-owned `GuestCr0/GuestCr4`, `GuestPc/GuestSp/GuestFlags`, and
`GuestCr3/EptPointer/Vpid/Cr3TargetCount` scheduler configuration functions
have no production callers in the current source graph and therefore cannot
support a release claim.

The Phase-55 and Phase-57 completion-backed profiles are constructed by the
production `CPU_Core` through immutable `CpuCorePlatformContext` opt-in flags.
Phase 55 is the earlier and narrower accepted profile, so this release selects
only:

```text
HybridCPU.VMREAD.ScalarDelivery.v1
  / DELIVER_COMPLETION_REASON_QUALIFICATION_SCALAR_V1
  / ExitReason + reason-bound ExitQualification
```

## Production authority and reachability

```text
explicit CpuCorePlatformContext opt-in
  -> CPU_Core construction
  -> existing DomainCompletionObservationOwner
  -> exact CanonicalCpuInstructionTranslationFaultProducer snapshot
  -> owner-approved compatibility mapping
  -> existing single-use VmReadScalarResultReceipt
  -> canonical PRF/writeback
  -> RetireRecord.RegisterWrite
  -> RetireCoordinator
```

The accepted neutral reasons are exactly `AccessDenied` and
`OwnerScopeMismatch`, mapped to frozen `SecurityPolicyViolation`.
`ExitQualification` is returned only as the exact reason/access-kind/access-size
encoding bound to the same snapshot. Absent, zero-by-fallback, mismatched,
unmapped or stale facts deny.

## Rollback linearization

`CPU_Core.DisableCompletionReasonQualificationVmReadProfile` delegates to the
same composition that owns its receipts. Under one lock it disables future
prepare, advances the profile generation and invalidates every previously
issued receipt before it can pass speculative or retire validation. The
operation is idempotent and cannot reactivate the immutable construction.

Restore, rebind and owner replacement continue to invalidate through the
existing completion observation lifecycle. Migration remains drain-only; no
receipt or compatibility value is serialized as authority.

## Exact exclusions and exit

`GuestPhysicalAddress`, `EptViolationQualification`, every descriptor-owned
field group, VMWRITE and every new VMREAD field remain outside this release.
No VMX, VMCS, frontend, trap, VMCALL, release record or compiler artifact is a
runtime authority source.

After closure:

```text
NextOpenPool = None
NextCandidatePool = NoneUntilSeparateNewExactVmReadFieldGroupOrVmCallOperationSelection
```

## Final evidence

Subject `57b2a1d805bdad547a4535e40a68316f88e0816c`, tree
`41fc1b9c3c8a3a43792c50480164253b66ac0f72`, closes the exact release and
kill-switch correction. The later
`2026-08-13-phase60-completion-reason-qualification-vmread-release-clean-evidence.json`
record binds it without naming its own containing commit.

Final gates were focused `155/155`, prepare/kill-switch race five runs of
`1/1`, worktree VMX `575/575`, worktree SecureCompute `406/406`, clean-staged
VMX `557/557`, clean-staged SecureCompute `395/395`, and hook-free Release at
0 errors. The real clean rebuild reported 43 pre-existing warnings; the
unchanged repeat reported `0 warnings / 0 errors`. Canonical `CloseToHSL`
contained zero untracked and zero ignored C# dependencies.
