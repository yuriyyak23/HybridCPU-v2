# Phase 57 — exact completion-backed GPA/EPT VMREAD projection

## Boundary

This phase implements only a compatibility/read-only projection for
`VmcsField.GuestPhysicalAddress` and
`VmcsField.EptViolationQualification`. It creates no completion, VMX, VMCS,
translation, memory-domain, nested or IOMMU authority.

The only admitted source is the committed
`DomainCompletionObservationOwner` snapshot issued by the exact
`CanonicalCpuSecondStageTranslationFaultProducer` registration.

## 2026-06-11 Audit Contract

- File name: `57_exact_completion_backed_gpa_ept_vmread_projection.md`.
- Purpose: Govern and implement one exact completion-backed compatibility read contour for GPA and second-stage violation qualification.
- Status: Implementation pending required full gates, clean subject and later non-self-referential evidence.
- Scope: Two frozen VMREAD selectors, exact committed second-stage producer snapshot and existing scalar register-result delivery only.
- No-goals: Completion/translation/VMX/VMCS authority, adjacent fields, VMWRITE, nested/IOMMU/DMA/device, SecureCompute or compiler changes.
- Code anchors: `NeutralCompletionSecondStageProjection.cs`, `CompletionSecondStageVmReadScalarDeliveryCanonicalComposition.cs`, `CPU_Core.PipelineExecution.Materialization.cs`, and `DomainCompletionObservationOwner.cs`.
- Authority owner: Existing neutral CPU translation, completion commit/observation, VMREAD admission and canonical retire owners only.
- Required RFC/ADR: Immutable Phase 57 SpecV2 and later AcceptanceRecordV2 bound to the repository-owner authorization.
- Acceptance criteria: Exact producer and semantic tuple, present-zero distinction, full provenance/freshness denial, default-off construction, existing PRF/writeback/retire delivery, cross-gates and clean evidence.
- Tests/static scans: Focused positive/negative/lifecycle/race tests, Phase 55/56 regressions, full VMX/SecureCompute matrices, Release, forbidden authority and ignored/untracked source scans, and `git diff --check`.
- Risks: Auxiliary pass-through, GPA inference, nested/IOMMU authority leakage, zero fallback, stale receipt publication or selector fallthrough.
- Next-gate dependency: Green production subject and later non-self-referential Phase 57 evidence; no automatic adjacent pool.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Exact mapping

- `GuestPhysicalAddress` is copied only from a present
  `NeutralFaultAddressSemantic.GuestPhysicalAddress` fact. Present zero remains
  zero and is distinct from absence.
- `EptViolationQualification` is materialized only after exact decoding of a
  present `SecondStageTranslationViolation` auxiliary and consistency checks
  against the neutral reason and reason-bound qualification.
- The frozen compatibility value is explicitly reconstructed from access kind,
  page-walk level and misconfiguration kind. The neutral auxiliary is never
  returned unchanged.
- `SecondStageSourceStale`, foreign producers, missing second-stage provenance,
  semantic mismatch and malformed tuples are denied.

The mapper does not call `TranslationViolationInfo`, nested translation,
IOMMU/DMA/device helpers, generic exceptions, VMCS backing state or exit
factories.

## Delivery and lifecycle

The production caller is the canonical VMREAD lane-7 materialization path. The
two exact selectors route exclusively into
`CompletionSecondStageVmReadScalarDeliveryCanonicalComposition`; Phase 55
continues to own only `ExitReason` and `ExitQualification`.

The contour reuses the existing single-use `VmReadScalarResultReceipt`, PRF
rename/writeback, `RetireRecord.RegisterWrite` and `RetireCoordinator`. Receipt
validation binds the exact snapshot, completion/restore generations, scope,
attempt, event, digest and full second-stage translation provenance. Restore,
rebind and observation-owner replacement therefore revoke stale reads before
architectural consumption.

Activation is a separate immutable `CpuCorePlatformContext` opt-in and defaults
to disabled. VMWRITE and all adjacent fields remain denied.

## Governance chain

- SpecV2 subject: `0865929ae2ab5ee95b0912eebd63ae7af2494f4c`, tree
  `a0108c9e035ff9c57c0167a6efd04788535a1ebd`, digest
  `c68adf194f0452c15f4d11e3a44199b50a0a6168923915e280c62fcbe683c4ab`.
- Later AcceptanceRecordV2: `890f8aa` (full SHA recorded in machine status),
  acceptance digest
  `a6622a79ff3278227a9a39b4e51b20345366ffba24d47cb107b53a0301d3e955`.

## Worktree verification

The bounded implementation passed focused Phase 55–57 and plan guards
`95/95`, five separate restore-revocation runs `1/1`, the canonical VMX matrix
`556/556`, the SecureCompute cross-matrix `406/406`, and Release without
`TESTING` or internal hooks at `0 warnings / 0 errors`. Forbidden scans found
no nested/IOMMU/VMCS backing/VM-exit/VMCALL/VMWRITE authority dependency in the
new contour. The sole untracked C# file under canonical production source is
the bounded new Phase 57 composition itself; ignored C# remains zero.

Clean staged-source results and the production subject/evidence chain are
recorded after their independent gates complete. The detached staged tree
`697cfec88398e2fb7d519e41d2e2b695b1952528` passed canonical VMX `538/538`,
SecureCompute `395/395`, and Release `0 warnings / 0 errors`; generated restore
metadata was copied locally only and no source side-load was used.

The production subject is
`875b472abe8286ad72c7157c075f2866e4e75f15`, tree
`5518800241ce3dd7697c9cf9d05a29cc748cbde8`. The separate
`2026-08-13-phase57-exact-completion-second-stage-vmread-clean-evidence.json`
record binds exact subject blobs and contains no reference to its own commit.
