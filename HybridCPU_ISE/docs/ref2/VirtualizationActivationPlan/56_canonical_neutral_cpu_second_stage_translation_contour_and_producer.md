# Phase 56 — canonical neutral CPU second-stage translation contour and producer

Status: `CLOSED GREEN / SUBJECT AND LATER NON-SELF-REFERENTIAL EVIDENCE`.

## Authorization and bounded scope

The repository owner authorized one bounded extension of the existing
`CpuInstructionTranslationOwner` for production instruction fetch and scalar
load/store. The explicit address model is VA → GPA → CPU physical address.
Identity and Phase 54 single-stage behavior remain the default. Two-stage mode
exists only when an immutable policy is explicitly created from an
issuer-sealed live `MemoryDomainRuntime`/`MemoryDomainDescriptor` binding.

This phase grants no VMX, VMCS, EPT compatibility, VMREAD, VMWRITE, nested,
IOMMU, DMA, device, SecureCompute, or compiler authority. It does not reuse a
nested/IOMMU result and does not reinterpret a Phase 54 physical result as GPA.

## 2026-06-11 Audit Contract

- File name: `56_canonical_neutral_cpu_second_stage_translation_contour_and_producer.md`.
- Purpose: Implement and re-audit one canonical neutral CPU second-stage fetch/load/store contour and one exact completion producer.
- Status: Implementation and required worktree gates are green; clean subject and later evidence provenance pending.
- Scope: Existing CPU translation owner, canonical MemoryDomain source, precise-fault arbitration and completion owner only.
- No-goals: VMX/VMCS/EPT authority, VMREAD/VMWRITE, nested/IOMMU/DMA/device reuse, SecureCompute/compiler changes, or adjacent activation.
- Code anchors: `CpuInstructionTranslationContour.cs`, `CpuSecondStageTranslationMechanism.cs`, `MemoryDomainRuntime.CpuSecondStage.cs`, `CPU_Core.PipelineExecution.CpuTranslation.cs`, and `ArchitecturalCompletionCommitOwner.cs`.
- Authority owner: Existing `CpuInstructionTranslationOwner`, `MemoryDomainRuntime`/`MemoryDomainDescriptor`, stage-aware precise-fault arbitration and `ArchitecturalCompletionCommitOwner`.
- Required RFC/ADR: Repository-owner explicit bounded authorization recorded by this phase; compatibility projection remains a later independent E0/D2.
- Acceptance criteria: Explicit VA→GPA→CPU-physical semantics, identity default, exact production callers, deterministic arbitration, complete provenance, stale/squash/replay closure, one exact producer, cross-gates and clean evidence.
- Tests/static scans: Phase 56 positives/negatives, five rebind races, Phase 52–55 regressions, full VMX/SecureCompute matrices, Release, forbidden authority and ignored/untracked source scans, and `git diff --check`.
- Risks: Reinterpreting Phase 54 physical values as GPA, accepting stale MemoryDomain roots, leaking nested/IOMMU authority, emitting VMX-shaped neutral facts, or opening compatibility reads early.
- Next-gate dependency: Green subject and later non-self-referential Phase 56 evidence before the separately authorized exact GPA/EPT VMREAD E0/D2.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## E0 production reachability

- `PipelineStage_Fetch` uses the existing fetch identity and the same
  `TranslateInstructionFetch` call before physical instruction-memory access.
- `LoadMicroOp` and `StoreMicroOp` use the existing scalar translation call
  before memory-controller or bound-main-memory access.
- `CpuInstructionTranslationPolicy.CreateTwoStageBoundedRegions` requires a
  distinct `CpuInstructionStageOneRegion`, so its `GuestPhysicalBase` has an
  explicit stage-one semantic.
- `MemoryDomainRuntime.BindCanonicalCpuSecondStageTranslation` validates the
  runtime-owned memory policy, runtime-owned bounded address space, owned
  second-stage authority, non-zero root, domain identity, address-space
  identity and live runtime generations.
- `CpuSecondStageTranslationMechanism` is stateless and CPU-neutral. It reads
  page tables only through the bound CPU physical-memory surface. It returns
  typed neutral success, violation, permission, range, or misconfiguration
  facts; it returns no VMX-shaped value.

## Producer, arbitration, and provenance

The existing stage-aware precise-fault winner remains the only arbitration
owner: WB precedes MEM, MEM precedes EX, and the established lane order selects
same-stage candidates. The exact
`CanonicalCpuSecondStageTranslationFaultProducer` is registered with the
existing `ArchitecturalCompletionCommitOwner` for `TranslationFault`, required
reason/qualification, `GuestPhysicalAddress`, and
`SecondStageTranslationViolation` only.

The committed snapshot and issuer-sealed receipt carry exact domain, context,
VT, attempt, event, memory-operation identity, CPU translation-owner epoch,
MemoryDomain owner epoch, address-space generation and address-space identity.
GPA zero remains a present value and is distinct from an absent fact.

## Lifecycle and races

Every walk captures one exact MemoryDomain owner/root/generation binding and
validates it again after the stateless walk. MemoryDomain replace, restore
rebind, unbind, or new binding invokes the existing completion owner's exact
scope rebind invalidation before changing the source. This clears observation,
live receipt and replay identity. An in-flight walk that loses freshness is
squashed for replay and cannot publish a completion. Speculative translation
faults remain silent squash. Architectural restore and owner replacement retain
the Phase 52 completion invalidation linearization.

## Compatibility coverage

This phase opens no read contour. It provides the neutral prerequisites for a
separate later E0/D2:

- exact GPA comes only from the explicit stage-one result;
- second-stage auxiliary is a typed neutral encoding, not an EPT qualification;
- absent, stale, foreign producer, or semantic mismatch must deny;
- compatibility materialization may occur only from the exact committed
  `DomainCompletionObservationOwner` snapshot.

## Verification boundary

The closed boundary passed Phase 56 `15/15`, combined Phase 54–56 and plan
guards `100/100`, five separate rebind-vs-walk race runs of `1/1`, full VMX
`545/545`, full SecureCompute `406/406`, and Release without `TESTING` or
internal hooks at `0 warnings / 0 errors`. Forbidden authority scans found no
nested/IOMMU/VMX/VMCS/EPT-shaped production dependency. Canonical ignored and
foreign untracked C# are zero. Clean staged-source verification, subject commit
passed VMX `528/528`, SecureCompute `395/395`, Release `0 warnings / 0 errors`,
and a clean Git status in detached verification tree
`8b1ee7ee0c4ebc7202e239700d08826a16c29ae4`. The production subject is
`eedc5379bf498ed109559faac8d221677d143904`, tree
`e0caf650b2c13dfeddd0de6af534250a75c4e0ab`; the separate Phase 56 evidence
record closes non-self-referential provenance.

## Next candidate

After clean subject and evidence closure, the only next pool is the separately
authorized exact compatibility/read-only GPA/EPT-violation VMREAD E0/D2 over
the exact committed second-stage producer snapshot.
