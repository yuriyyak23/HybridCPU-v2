# Phase 55 — completion-backed ExitReason/ExitQualification VMREAD E0/D2

Status: `CLOSED GREEN / EXACT TWO-FIELD PRODUCTION COMPOSITION`.

## Authorization and boundary

The repository owner separately authorized one compatibility/read-only VMREAD
projection contour over the existing neutral `DomainCompletionObservationOwner`.
The authorization is limited to `ExitReason` and reason-bound
`ExitQualification`; it creates no completion, VMX, VMCS, translation, fault,
admission, execution, or retire authority.

`GuestPhysicalAddress` remains denied because the exact CPU producer carries a
neutral `VirtualAddress`, not a guest-physical address. `EptViolationQualification`
remains denied because the producer carries `TranslationFault` auxiliary
semantics, not `SecondStageTranslationViolation` semantics.

## 2026-06-11 Audit Contract

- File name: `55_completion_reason_qualification_vmread_projection_e0_d2.md`.
- Purpose: Close exact completion-backed ExitReason/ExitQualification E0 and record the immutable D2 policy shape.
- Status: E0 closed; immutable SpecV2 present; later acceptance and production composition absent.
- Scope: Compatibility-only read projection of one exact neutral CPU translation-fault semantic tuple.
- No-goals: Completion creation, VMX/VMCS authority, GPA/EPT qualification, VMWRITE, adjacent subsystems or fields.
- Code anchors: `NeutralCompletionReasonQualificationProjection.cs`, `Phase55CompletionReasonQualificationVmReadE0Contract.cs`, and `Phase55CompletionReasonQualificationVmReadDecisionSpecV2.cs`.
- Authority owner: Existing `DomainCompletionObservationOwner` remains the sole neutral snapshot source; the mapper owns compatibility values only.
- Required RFC/ADR: Repository-owner explicit bounded authorization plus later non-self-referential D2 acceptance before production composition.
- Acceptance criteria: Exact producer/reason/qualification mapping, explicit incomplete-field denial, lifecycle-bound receipt design, cross-gates and forbidden scans.
- Tests/static scans: Phase 55 positives/negatives, plan/status guards, VMX/SecureCompute matrices, Release, clean-source and forbidden dependency scans.
- Risks: Inference, zero fallback, stale snapshot reuse, foreign producer admission, VMCS backing authority, or accidental GPA/EPT expansion.
- Next-gate dependency: Later acceptance binding this earlier immutable spec commit and digest.

Owner map completeness is recorded at: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.

## Exact owner-approved mapping

The mapping accepts only snapshots from the exact registered
`CanonicalCpuInstructionTranslationFaultProducer`, with the exact neutral
`TranslationFault` class and present reason, qualification, virtual-address and
translation-auxiliary semantics.

- `AccessDenied` maps to frozen compatibility `SecurityPolicyViolation`.
- `OwnerScopeMismatch` maps to frozen compatibility `SecurityPolicyViolation`.
- `UnmappedAddress`, `AddressOverflow`, absent facts, malformed qualification,
  foreign producer/class, stale observation, and every unlisted tuple deny.
- The qualification must encode the same reason in bits 63..56, one exact
  fetch/load/store access kind in bits 55..48, a non-zero access size in bits
  47..32, and zero reserved low bits. The accepted value is returned unchanged.
- Reason and qualification form one inseparable semantic tuple; neither field
  may be projected by inference or zero fallback when the tuple is incomplete.

The mapper is stateless compatibility materialization. It reads no VMCS backing
state and cannot install, clear, replace, or commit completion state.

## E0 reachability and lifecycle prerequisites

The source is the Phase 54 production CPU instruction fetch/scalar load/store
producer after the existing precise-fault arbitration boundary and the Phase 52
canonical completion lifecycle owner. A later production composition must bind
the exact observation snapshot, completion generation, restore generation,
producer identity/epoch, domain/context/VT, attempt/event, completion identity,
digest, canonical order and commit sequence into the existing single-use scalar
receipt and canonical PRF/writeback/`RetireRecord.RegisterWrite` route.

Restore, rebind, owner replacement, replay change, squash, duplicate consumption,
cross-owner, cross-domain, cross-context, cross-VT, cross-attempt/event and stale
snapshot must deny. GPA and EPT-violation qualification must be routed to explicit
field denial and may not fall through to another VMREAD composition.

## D2 state

`Phase55CompletionReasonQualificationVmReadDecisionSpecV2` is an immutable
governance-only policy shape with exact field IDs 96 and 97 and authority plane
`CompletionObservationReadProjection`. The source/value owner map is
`DomainCompletionObservationOwner` to the exact neutral observation snapshot.
The later `Phase55CompletionReasonQualificationVmReadDecisionAcceptanceV2`
binds immutable spec commit `9497d6152bee14e3743e99edc6c4c451b869ee8a`,
tree `60d9d11f8ef783466d7d05e03a52ccdb43ecb007`, and the exact spec digest.
It remains governance-only and grants no runtime authority.

The exact production composition is constructed per CPU core from
`CpuCorePlatformContext`; activation is explicit and defaults off. Canonical
VMREAD materialization routes all four completion-owned selectors exclusively
to this contour, so denied GPA/EPT fields cannot fall through. Accepted reason
and qualification values use the existing E1 carrier, single-use scalar
receipt, PRF/writeback, `RetireRecord.RegisterWrite`, and `RetireCoordinator`.

The receipt binds the exact neutral observation snapshot and its completion and
restore generations, producer identity/epoch, domain/context/VT,
attempt/event, completion identity, digest, canonical order and commit sequence.
Restore or owner replacement makes the old observation owner/snapshot fail
validation; duplicate consumption remains denied by the existing receipt.

## Forbidden expansion

No VMCS/backing-store authority, generic completion registry, broad
completion-class admission, caller-created completion, trap/VMCALL surrogate,
VMWRITE, GPA/EPT inference, IOMMU/DMA/device/nested/SecureCompute/compiler, or
adjacent field expansion is authorized.

## Next candidate

No automatic field expansion remains. GPA and EPT-violation qualification
require a separate future authorization and a canonical neutral second-stage
translation-fault producer.
