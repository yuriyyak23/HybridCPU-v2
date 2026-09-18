# Phase 38 - First Production VMCALL Slice Repository-Owner ADR

Status date: 2026-08-11.

Status: `ACCEPTED ARCHITECTURE DECISION / PR-A..PR-J COMMITTED / DEVELOPMENT-LOCAL EXACT PROFILE CLOSED / COMPILER CLOSED-BY-DEFAULT / COMPATIBILITY FAULT-ONLY`.

Current-state authority: `VirtualizationActivationStatusV1.json` is normative for current stage/gate state. Historical closure addenda remain provenance and cannot override it.

Historical milestone retained verbatim for static guards: `PR-A..PR-G COMMITTED`.

## 2026-08-10 PR-I Closure Addendum

Committed PR-H `89e193b4f1247baaaf1c4188ad121897360a8c75` opened the final
authorized pool. PR-I implements the already accepted E7 profile without
changing D2: a domain drain gate closes E2; issuing-owner registries prove
E2/E3/E5/E6 zero or cancel them; the checkpoint carries versioned policy
identity only; restore is one-shot, advances neutral generation, invalidates E1
and all older runtime authorities, and reloads exact local D2/O1 by SpecDigest.
Two-run and FSP/SMT matrix evidence has zero register, memory, VM-state and
redirect effects. No compiler or release authority follows.

## 2026-08-09 PR-F Closure Addendum

Committed PR-E at `8a36b89af279ec8f108d458621ad8e37d89a8c6d`
preserved its isolated default-off executor and opened PR-F. PR-F introduces the
accepted distinct `InvokeHypercall` operation as `RuntimeService`,
`NoStateExecution`, typed-capability-required and non-projection. Only the
canonical scheduler seam after E1 and immutable operand capture may issue E2 and
attach a private carrier-bound execution dispatch. Only `VmxMicroOp.Execute`
consumes that dispatch into E3.

There is no default binding, and disable/replay/grant invalidation before execute
denies E3. Compatibility frontend and dispatcher have no composition/executor
call. PR-F creates no completion record, E5, E6 or retire success; the carrier
still returns the established `SecurityPolicyViolation` retire effect. PR-G is
conditional on one clean green PR-F commit and is limited to the separately
owned completion route/publication boundary.

## 2026-08-10 PR-G Closure Addendum

Committed PR-F `7f529cd4f9699701b0b2bfdcc8bd90eaf82af781`
preserved its exclusive E4 contour and opened PR-G. PR-G implements the exact
completion policy already fixed here: the neutral completion owner validates and
consumes one live E3, after split route and pure fence policy, and atomically
publishes one neutral event record plus opaque E5. E5 carries the required
attempt/E3/decision/owner/VT/domain/sequence/effect/evidence/migration/restore
bindings. It is not capability or retire authority. The old public-boolean fence
is unchanged scaffolding, compatibility remains disconnected, and all VMX retire
effects still fault. PR-H is limited to canonical retire-window E6 review after
one clean green PR-G containing commit.

## 2026-08-09 PR-E Closure Addendum

Committed PR-D at `992c0cc2895b444ebc92c4b48d91175567f48076`
preserved the fault-only rollback and opened PR-E under the bounded authority.
PR-E implements the normative exact owner service only: a default-off
`DomainHypercallRuntimeExecutor` atomically consumes one live exact E2 and emits
one opaque, owner-bound E3 with canonical non-zero no-effect/no-result digests.
The receipt is restore-generation-bound, non-serializable, private-constructor
and grants neither completion nor retire.

The executor has no decode, pipeline, compatibility, dispatcher, completion or
retire composition. PR-E adds no `InvokeHypercall`, allowed backend DTO,
`CompletionRecord`, E5 or E6. Thus service-level E3 is structurally real, but
production VMCALL remains fault-only. PR-F is limited to proving the exclusive
canonical E4 composition and opens only after PR-E has one containing commit
and unchanged green exit/negative/static/rollback evidence.

## 2026-08-09 PR-A Closure Addendum

The repository now contains immutable SpecV2, separate AcceptanceRecordV2 and
append-only revocation/supersession types, a versioned deterministic binary
canonical encoder, fail-closed validator, logical owner/architecture review and
CODEOWNERS gates, complete negative tests and a governance-negative diagnostic.
No repository CODEOWNERS file or completed review evidence was fabricated.

This changes only the machine-substrate column: the Phase 38 profile can be
structurally validated by a test-local fixture. It does not create an
attributable accepted instance, production operation/owner/capability registry,
O1, operand snapshot, E2, executor, completion or retire authority. PR-A closure
does not automatically open PR-B and must not add a populated accepted record.

## 2026-08-09 PR-B Closure Addendum

The repository owner separately and explicitly authorized PR-B and confirmed
that legitimate developer accounts are interchangeable attribution principals,
not architecture constants. Commit A `1061eaa8bc45d598e1fe7b3fead71cf017ad81a6`
contains the exact SpecV2 and `.github/CODEOWNERS`; its canonical SpecDigest is
`33076e430fcbc05cf0774d08baadc6d7840f88029fcfb28a458558af82f93ca8`.
The later AcceptanceRecordV2 binds those existing bytes, the CODEOWNERS blob
`aafa6f65565345e18621eea0cc890711f64c3dbb` and completed logical owner and
architecture review receipts for `@yaksysdev`. The same attributable principal
may satisfy both roles; compatibility authority may satisfy neither.

PR-B also materializes stable owner/capability allocation metadata and a
generated lookup for exactly `HybridCPU.VMCALL.Runtime.v1`/`0x0001`. These close
machine D2 as accepted immutable policy only. They create no `CapabilityGrant`,
loaded owner/O1, operand snapshot, E2, executor, allowed backend, completion or
retire authority.

## 2026-08-09 PR-C Closure Addendum

The repository owner separately authorized PR-C. The current worktree implements
the accepted execution-only common-legality rule and `NoStateExecution` authority
class, loads one immutable non-capability `VirtualizationOperationOwnerSnapshot`
only from the exact accepted D2 object, and captures one immutable full-value
`VirtualizationOperandSnapshot` after live E1 validation at the canonical
materialization seam. Exact/adjacent/high-bit, carrier, digest, duplicate and
stale restore-generation cases deny. VMX execution remains fault-only.

PR-C creates no live `CapabilityGrant`, executable owner service, E2 issuer or
certificate, allowed backend, executor, `InvokeHypercall`, completion record or
retire authority. The restore generation is captured and validated; advancing it
from checkpoint restore remains a later lifecycle integration. Closing PR-C does
not automatically open E2.

## 2026-08-09 PR-D Closure Addendum

The repository owner explicitly authorized the bounded PR-D -> PR-I sequence.
PR-D introduces the first production-compiled operation-specific E2, but only
as SafetyVerifier-owned admission. Its immutable certificate binds the exact
accepted D2/O1, E1 attempt and issuer generation, canonical operand digest,
VT/context/domain, bundle/replay, HCOWNR policy, exact namespace/leaf, live typed
grant identity/generation, non-mutating runtime-root epoch and live restore
generation. Address-space and evidence identities are absent by construction.

The capability lifecycle owner and restore-generation owner retain liveness;
revocation or generation advancement invalidates E2. SafetyVerifier runs common
execution-only runtime admission internally and owns certificate state
(`Issued`/reserved `ConsumedByExecutor`/`Revoked`). PR-D exposes no executor
consume method, E3, allowed backend, production dispatcher composition,
completion or retire path. The historical Phase-34 boolean request stays denied
and every VMCALL still follows the frozen fault-only VMX behavior.

## 2026-06-11 Audit Contract

- File name: `38_first_production_vmcall_slice_repository_owner_adr.md`.
- Purpose: record the repository owner's acceptance of the verified architectural decisions proposed by `HybridCPU-v2 Virtualization Activation 3.md`, including the first exact VMCALL ABI slice, without mistaking documentation for runtime authority.
- Status: architecture/ABI decision and machine-verifiable D2 accepted; PR-C closes O1 and canonical operand identity, PR-D closes SafetyVerifier-only E2 admission, and PR-E closes isolated default-off E3. E4-E7 remain sequentially gated.
- Scope: D2 v2 artifact split, neutral hypercall role, exact namespace/leaf/operand/result/effect ABI, O1 policy snapshot, operand snapshot, E2-E7 authority separation and first-slice exclusions.
- No-goals: at the ADR/PR-C boundary these were an authority-changing production path, live grant, E2 or later execution/publication. Separately authorized PR-D now adds only a per-attempt live grant lease and E2 admission; executable owner service, allowed backend, completion/retire connection and broad activation remain absent.
- Code anchors: `VmxInstructionPayload.cs`, `VmxExitQualification.cs`, `VmxFunctionLeaf.cs`, `VirtualizationOperationDecisionManifest.cs`, `SafetyVerifier.VirtualizationOperationAdmission.cs`, `HypercallBackendAdmissionPolicy.cs`, `DisabledNeutralVirtualizationOperationOwner.cs`, `DomainRuntimeOperation.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`, `VmxRetireModel.cs`.
- Authority owner: the accepted architecture role is `DomainHypercallRuntimeOwner`; its stable architecture allocation is `OwnerId=0x4843_4F57_4E52` (`HCOWNR`), `OwnerPolicyVersion=1`, `OwnerEpoch=1`. SafetyVerifier, neutral completion owner, canonical retire owner and checkpoint/restore owner retain their independent authorities. This document does not instantiate any of them or create runtime permission.
- Required RFC/ADR: this file is the repository-owner architecture/ABI/policy ADR for the exact first slice; machine D2 additionally requires byte-exact `VirtualizationDecisionSpecV2` plus later `VirtualizationDecisionAcceptanceRecordV2`, production registries for the accepted owner/capability allocations and attributable review mapping with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: the accepted values are unambiguous and code-compatible; every runtime stage remains denied until its typed dependency exists, is live and is consumed in order.
- Tests/static scans: plan guard asserts exact namespace/width/leaf/ABI, distinct VMFUNC namespace, selector-not-value warning, v1 substrate non-promotion and absence of production owner/executor/backend/completion/retire shortcuts.
- Risks: treating this ADR as AcceptedVirtualizationDecision, treating compatibility `ushort` or `VmxExitQualification` as value authority, or implementing E3 before D2/O1/operand/E2.
- Next-gate dependency: PR-A through PR-D are closed at their exact boundaries. PR-E opens conditionally only after PR-D has a clean containing commit and all exit/rollback gates remain green.

## Decision Source And Verification Verdict

The repository owner explicitly requested that the recommendations in `docs/ref2/1/VRT/HybridCPU-v2 Virtualization Activation 3.md` be accepted as ADR decisions if they withstand current-code and documentation review. They do, with the corrections and non-claims below.

| Proposed decision | Verdict | Verified disposition |
| --- | --- | --- |
| Separate `VirtualizationDecisionSpecV2` and `VirtualizationDecisionAcceptanceRecordV2` | **accepted** | avoids the v1 self-referential acceptance problem; `AcceptanceRecord`, not `Attestation`, is normative terminology |
| Neutral role `DomainHypercallRuntimeOwner` | **accepted as architecture role** | repository-local placement matches the WhiteBook if independent from VMX/VMCS compatibility; stable OwnerId, reviewer mapping and runtime service are not yet materialized |
| namespace `HybridCPU.VMCALL.Runtime.v1`, 16-bit width, invalid `0x0000`, first leaf `0x0001` | **accepted** | `0x0001` is collision-free only inside this exact namespace; VMFUNC already uses numeric 1 in the distinct frozen `VmxFunctionLeaf` namespace |
| operation `PROBE_NO_STATE_V1` | **accepted** | exact no-state/no-payload/no-result semantics only; it grants no adjacent operation |
| `Rs1` value is leaf; `Rs2=x0`; `Rd=x0` | **accepted with mandatory capture rule** | current payload stores register selectors, not values; the actual full `Rs1` value must be read once after E1, upper bits must be zero and silent truncation is denied |
| O1 as immutable owner-policy snapshot | **accepted and supersedes prior O1 naming** | `VirtualizationOperationOwnerSnapshot` is materialized only from an accepted D2; `VirtualizationOperandSnapshot` is a separate unnumbered runtime object |
| E2/E3/E5/E6 opaque, attempt-bound and consume-once | **accepted** | D2/O1/E1 are not runtime capabilities; E3 is not completion; E5 is not retire |
| E4 uses neutral `InvokeHypercall`-equivalent operation | **accepted for future implementation** | current `DomainRuntimeOperationKind` has `InvokeCapability` and projection-only `ProjectCompatibilityTrap`, but no `InvokeHypercall`; the future operation must be new, use `Source=RuntimeService`, set `IsProjectionOnly=false`, and preserve the existing denial of authoritative compatibility-frontend mutation |
| `DrainOnly`, restore invalidation and architectural-trace determinism | **accepted** | live E1/E2/E3/E5/E6 are non-serializable; timing/lane identity is not an architectural equivalence requirement |
| VMREAD/VMWRITE/nested/SecureCompute/memory/IOMMU/device/lane/compiler exclusions | **accepted** | they remain separate owner-specific future gates and do not block the exact no-state probe |
| `HCOWNR`, capability bit 41 and exact first-slice policies | **accepted as architecture allocations** | local production scan finds no collision; the values create no authority until SpecV2/AcceptanceRecordV2, CODEOWNERS evidence and the generated registry are machine-validated |
| execution-only domain and no-state root authority | **accepted; PR-C implemented** | exact `ExecutionOnly` requirements now flow through admission/authority/common legality; `NoStateExecution` requires runtime-root authority but not mutation privilege, while compatibility overloads retain `FullDomainRuntime` |
| host-owned/nonmigratable completion plus E5/E6 | **accepted with a separate positive contour** | current fence cannot be switched positive because it trusts booleans, constructs the record itself and denies host-owned evidence at retire; route policy, atomic completion ownership and canonical E6 retire must remain distinct |

## Accepted Exact ABI Decision

The repository-owner architecture decision is:

```text
SchemaVersion       = 2
DecisionId          = D2-HV-VMCALL-RUNTIME-V1-PROBE-0001
OperationNamespace  = HybridCPU.VMCALL.Runtime.v1
LeafWidth           = 16
InvalidLeaf         = 0x0000
NumericLeaf         = 0x0001
OperationId         = PROBE_NO_STATE_V1
OwnerClass          = NeutralRuntimeOwner
OwnerId             = 0x4843_4F57_4E52  // "HCOWNR"
OwnerPolicyVersion  = 1
OwnerEpoch          = 1
OperandAbiVersion   = 1
Rs1                 = architectural register containing the full numeric leaf value
Rs2                 = x0
Rd                  = x0 / no result
ResultAbi           = NoPayload
EffectClass         = NoStateNoPayload
OperationMigrationPolicy = DrainOnly
```

`0x0001` is accepted only as the leaf in `HybridCPU.VMCALL.Runtime.v1`. It does not alter or alias `VmxFunctionLeaf.CapabilityQuery = 1`, does not reserve value 1 in any other namespace, and does not make the frozen compatibility enum a runtime registry.

The operation has no architectural register write, memory, I/O, DMA/IOMMU, device, SecureCompute, nested, scheduler-visible, explicit PC-redirect or payload effect. Physical placement may retain the existing serializing VMX/SystemSingleton-compatible contour; placement is not authority.

## Mandatory Operand Correction

Current code proves that `VmxInstructionPayload.FromDecodedRegisters` constructs `VmxExitQualification(rs1, ..., rs2)` from encoded register identifiers. Therefore:

- `VmxExitQualification.Leaf` is a compatibility selector projection, not the runtime numeric leaf value;
- the full architectural value of `Rs1` is captured exactly once after the live E1-bound canonical materialization;
- `VirtualizationOperandSnapshot` binds selector, full value, attempt, VT/domain/context, source/working slots, bundle/replay and restore generation;
- `fullRs1Value & ~0xFFFFUL` must equal zero before conversion to `ushort`;
- E2/E3/E5/E6 may not re-read the register file or reconstruct the value from compatibility payload;
- nonzero `Rs2`, nonzero/result-bearing `Rd`, zero leaf, high bits, unknown leaf and adjacent leaf are denied.

The decoder can represent `x0` as register id zero and VMCALL has no registered writeback opcode. PR-C enforces the exact selector/value ABI at the canonical post-E1 materialization seam; decode and compatibility qualification still do not become value authority.

## Normative D2 And O1 Contract

`VirtualizationDecisionSpecV2` contains the exact operation/owner/ABI/effect/capability/evidence/domain/cancellation/replay/operation-migration/completion-migration/completion/retire/adjacent-leaf policy and a deterministic digest. It cannot mark itself accepted. `VirtualizationDecisionCanonicalEncoderV2` defines a fixed field order and byte representation; hashing arbitrary serializer JSON, property order or whitespace is forbidden.

```text
SpecDigest = SHA256(CanonicalEncode(spec excluding SpecDigest))
AcceptanceDigest = SHA256(CanonicalEncode(record excluding AcceptanceDigest))
```

`VirtualizationDecisionAcceptanceRecordV2` references the exact `DecisionId`, `SpecDigest` and `SpecCommitSha`, records `AcceptanceState`, `AcceptedBy`, `AcceptancePolicyVersion`, `OwnerReviewEvidence`, `ArchitectureReviewEvidence`, `CodeOwnersBlobSha`, optional superseded decision/digest lineage and its own digest. `SpecCommitSha` names the commit already containing the immutable SpecV2 bytes; the later acceptance record must not claim the SHA of its own containing commit. Revocation or supersession is a new immutable record, never an edit of an accepted record.

`VirtualizationDecisionValidatorV2` validates canonical bytes and both digests; exact spec bytes at `SpecCommitSha`; identity, owner class/id/policy; namespace, width, non-zero unique leaf and cross-namespace rules; ABI and every policy field; CODEOWNERS mapping and logical review roles; acceptance/supersession/revocation lineage; and deny-all-except-exact adjacent-leaf policy. Its only positive output is `AcceptedVirtualizationDecision`, an immutable policy object and never a capability.

PR-A closure includes denials for wrong digest/SHA/DecisionId, self-referential SHA, zero owner/leaf, duplicate or cross-namespace collision, missing policy, unknown enum, missing CODEOWNERS, reviewer-role mismatch, compatibility-only review, revoked/superseded spec, noncanonical bytes, incomplete owner map and adjacent leaves.

`VirtualizationOperationOwnerSnapshot` (O1) is an immutable runtime-loaded policy snapshot derived only from one machine-validated `AcceptedVirtualizationDecision`. It binds DecisionId/digest, OwnerId/policy version, namespace/operation/leaf/ABI/effect and policy classes. O1 is not a runtime capability.

Architecture and exact-policy acceptance in this ADR alone did not close machine D2. PR-A plus separately authorized PR-B now close machine D2 because all of the following exist together:

- v2 spec, acceptance-record and validator implementation (**structurally closed by PR-A**);
- production `HypercallRuntimeOwnerRegistry` materializes the accepted `HCOWNR` allocation without caller-supplied identity or reuse;
- attributable required owner and architecture reviewer mapping/CODEOWNERS policy;
- a byte-exact accepted spec and later acceptance record passing the validator;
- revocation/supersession policy and exact generated runtime lookup inputs.

The exact first-slice policy is accepted, encoded and bound by the attributable populated machine artifacts:

```text
CapabilityRequirement       = DomainGranted / VmCallProbeNoStateV1
CapabilityMask              = 1UL << 41 = 0x0000_0200_0000_0000
RequiresTypedGrant          = true
DelegationPolicy            = NonDelegable
RevocationPolicy            = RuntimeRevocable
CapabilityMigrationClass    = DomainLocal
EvidenceVisibility          = HostOnly
FrontendProjectionPolicy    = NeverProject

ExecutionEvidenceRequirement = None

DomainRequirement           = ExecutionDomainBound
RequireNonZeroDomainTag     = true
RequiresMemoryDomain        = false
RequiresIoDomain            = false
AddressSpaceRequirement     = None
SecureDomainPolicy          = Deny

CancellationPolicy          = DenyBeforeExecution
ReplayPolicy                = DenyAttemptReplay
OperationMigrationPolicy    = DrainOnly

CompletionEvidenceClass     = HostOwnedRuntimeEvidence
CompletionMigrationClass    = HostOwnedNonMigratable
CompletionProjectionPolicy  = NeverProject
CompletionPolicy            = AtomicE3ToCompletionRecordAndE5
RetirePolicy                = PreciseE5BoundNoStateRetire
AdjacentLeafPolicy          = DenyAllExceptExact
```

Local source verification found bit 41 only in VMCALL readiness tests and no production capability collision. The production allocation still requires `RuntimeCapabilityIds`, a whole-source collision gate and generation-bearing typed grants. The short `CapabilityGrant(mask, scope, isGranted)` overload defaults projection to `ProjectIfCompatible` and is therefore forbidden for this allocation; the full policy must explicitly encode `NeverProject`. `CapabilityDescriptorSet.Generation`, or an equivalent non-zero `CapabilityGrantId` plus `CapabilityGrantEpoch`, must advance whenever the grant set changes and must be bound into E2.

Repository-level CODEOWNERS now exists at the exact scopes below and is bound by blob SHA in the acceptance record. `@yaksysdev` is the attributable principal for this materialization. Account spelling is repository attribution rather than architecture policy: the validator requires consistency between CODEOWNERS, `AcceptedBy` and both completed logical roles, while `CompatibilityFrontend` can satisfy neither.

The future repository-level mapping must cover at least:

```text
/HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/  @yaksysdev
/HybridCPU_ISE/CloseToHSL/Core/Runtime/Capabilities/      @yaksysdev
/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Safety/           @yaksysdev
/HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/        @yaksysdev
/HybridCPU_ISE/CloseToHSL/Core/Pipeline/Retire/            @yaksysdev
/HybridCPU_ISE/docs/ref2/VirtualizationActivationPlan/     @yaksysdev
```

This block is now materialized in `.github/CODEOWNERS` and bound by the PR-B acceptance record; changing any scope, principal or blob requires new attributable governance evidence.

The first slice is explicitly denied in a `SecureCompute` domain. An ordinary VMCALL capability, evidence item, D2 policy or E2 certificate cannot transitively authorize SecureCompute. Any future secure-domain operation restarts at a separate SecureCompute owner decision and authority chain.

## Normative Runtime Authority Chain

The implementation/readiness dependency and the per-attempt runtime chain are different graphs. PR order is not runtime authority, and a readiness node is not consumed as though it were a live certificate.

### Build / Readiness Dependency

```text
VirtualizationDecisionSpecV2
  + VirtualizationDecisionAcceptanceRecordV2
  -> D2 accepted
  -> O1 loaded

E0 reproducible evidence
  + E1 implementation readiness
  -> production composition may be implemented
```

D2 and O1 must exist before a concrete runtime attempt. E0/E1 readiness permits the implementation pool to be considered; it does not authorize an attempt or skip D2/O1.

### Per-Attempt Runtime Chain

```text
canonical decode
  -> generic legality / owner-domain guards
  -> Stage A
  -> Stage B
  -> E1
  -> one-time VirtualizationOperandSnapshot
  -> D2/O1 exact operation resolution
  -> RuntimeBoundaryAdmissionService
  -> neutral TrapRequest / TrapPolicy
  -> NeutralTrapResult
  -> SafetyVerifier E2
  -> DomainHypercallRuntimeExecutor
  -> E3
```

`E4` is the proof/property that the chain above is the only canonical production composition that can reach E3. E4 is not a runtime authority object and is not a stage consumed after E3. PR-F must introduce a new neutral runtime operation such as `InvokeHypercall` or an exact owner-approved equivalent with `Source=RuntimeService` and `IsProjectionOnly=false`; it must not reuse `ProjectCompatibilityTrap`.

For `PROBE_NO_STATE_V1`, AddressSpaceIdentity is absent by contract because the operation has no memory effect. The Phase 34 v1 denied E2 request currently requires `AddressSpaceIdentityPresent`; it is historical fail-closed substrate and must not be promoted. The v2 certificate contract makes identity requirements operation-specific while preserving all domain/capability/evidence/attempt/restore bindings.

Only SafetyVerifier may issue E2 after the existing legality, Stage A, Stage B and E1 chain. `DomainHypercallRuntimeExecutor` may consume one live E2 and return one opaque E3; it may not publish completion or retire.

### Operation-Specific Domain And Root Authority

PR-C adds `DomainBoundaryDescriptor.ExecutionOnly` and passes the exact operation boundary through `RuntimeBoundaryAdmissionService` into `DomainRuntimeAuthority.Validate`. Validation uses `operationDomainRequirement.IsSatisfiedBy(context)`, not unconditional `context.HasRequiredDomains`. Retained compatibility overloads default to `FullDomainRuntime`; canonical common-legality consumers no longer silently reimpose Memory+IO domains.

PR-C also adds the authority class:

```text
ProjectionOnly
NoStateExecution
AuthoritativeMutation
```

The new `InvokeHypercall(PROBE_NO_STATE_V1)` is `Source=RuntimeService`, `AuthorityClass=NoStateExecution`, non-projection and typed-capability-required. It requires `RuntimeRoot` plus the exact live grant, but not `AllowAuthoritativeStateMutation`. Existing projection and mutation checks remain unchanged. This prevents a no-state probe from acquiring domain-state mutation authority.

### One-Time Operands, E2 And E3

O1 is singleton/read-only policy loaded from validated local D2, not per-attempt state and not checkpoint state. The one-time `VirtualizationOperandSnapshot` binds `AttemptId`, VT, owner context, non-zero domain tag, `Rs1` selector/full value, fixed-zero `Rs2`/`Rd`, source/working slots, bundle/replay identity, restore generation, capture sequence and a non-zero canonical digest. Architectural x0 is already hardwired zero and ignores writes; no later stage may dynamically reread `Rs1` or `Rs2`.

SafetyVerifier issues E2 from live E1, O1, the snapshot and the real domain/root/capability context. It must invoke or validate `RuntimeBoundaryAdmissionService` inside the issuance contour; a caller-supplied `Allowed` DTO is not authority. E2 binds attempt/E1 issuer generation, VT/owner/domain, bundle/replay, D2 digest, owner identity/policy/epoch, operation/exact leaf, operand digest, capability identity/generation, root-authority epoch and restore generation. Address-space and evidence identities are absent by the first operation contract. E2 is `Issued`, `ConsumedByExecutor` or `Revoked`; only the exact executor may perform the one consume.

Cancellation and replay are attempt-bound:

- cancellation before E3 invalidates E2, does not enter the executor and produces no receipt;
- squash after E3 but before E5 drains/discards E3 and publishes no E5;
- squash after E5 but before E6 invalidates/consumes E5 as cancelled and forbids E6;
- after E6 the instruction is retired and cancellation no longer applies;
- replay/rollback invalidates old AttemptId, E1/E2/E3/E5/E6 and starts a new canonical execution with a new snapshot and E2; a stall within one attempt is not a second backend execution.

`DomainHypercallRuntimeExecutor` accepts only one live exact E2. E3 binds the E2 identity digest, D2/owner/operation/leaf, execution sequence, restore generation, `NoStateNoPayload` effect and `NoPayload` result. Empty effect/result digests use non-zero canonical constants; zero remains the fail-closed sentinel. E3 is opaque, owner-issued, one-per-E2, non-serializable, and is neither completion nor retire.

The future positive completion lifecycle is:

```text
E3
  -> TrapCompletionRouteService
  -> TrapCompletionPublicationFence policy decision
  -> neutral completion owner
  -> atomic publication:
       CompletionRecord
       + opaque E5 CompletionPublicationToken
```

E5 is the non-forgeable proof of exactly one already-published completion. It is emitted atomically with that record, cannot grant its own publication, and may be consumed only by the canonical retire owner. The current `TrapCompletionPublicationFence` accepts caller-provided authorization booleans and directly constructs `CompletionRecord`; that shape is policy/compatibility scaffolding and must be replaced, not promoted, for a positive E5 path. E6 is issued only at canonical retire eligibility and ordering from one live E5. Restore increments generation and invalidates all pre-restore live authority.

`OperationMigrationPolicy` and `CompletionMigrationClass` are separate D2 fields:

```text
OperationMigrationPolicy = DrainOnly
CompletionMigrationClass = HostOwnedNonMigratable
```

The existing fence's `TrapCompletionMigrationClass` classification is not the operation checkpoint policy. For the first slice, checkpoint is allowed only after drain, so no live E5 is migrated or serialized. The current fence rejects host-owned evidence for retire; PR-G/PR-H must not weaken that non-leak rule or flip its booleans. They introduce an owner-bound positive contour in which the neutral completion owner consumes one live E3 and atomically publishes exactly one `CompletionRecord` plus sealed E5 in one critical section. E5 binds attempt/E3 identity, decision/owner, VT/domain, execution/completion sequences, effect digest, host-owned evidence/migration classes and restore generation. Either both record and E5 exist or neither does.

The retire owner issues E6 only for the canonical retire head with matching live E5, attempt/VT/effect, retire-window identity, order epoch and restore generation, and only when not squashed or already retired. The first slice has zero register/memory/VM-state writes and no explicit redirect. Positive retirement must be integrated into the existing canonical retire-window/head/order contour; an illustrative `ApplyAuthorizedVirtualizationRetire` name must not become a parallel retire system. It consumes E6 and permits normal precise instruction retirement without fabricating a compatibility effect or x0 write. The current `VmxRetireEffect` path remains fault-only for every operation without E6.

### E7 Drain, Restore And Determinism

Checkpoint closes new E2 admission for the domain, drains or cancels live work, and asserts live E2/E3/E5 and pending E6 counts are all zero before capture. A ledger is bookkeeping only; each neutral owner's live registry is authoritative. Restore advances a neutral domain-owned restore generation and invalidates E1 issuer generations plus E2/E3/E5/E6. O1 is reloaded from local accepted D2; a local SpecDigest mismatch leaves virtualization denied.

E1, operand snapshots, E2/E3/E5/E6, capability handles, owner seals and receipts are never serialized. This matches the current checkpoint code, which rejects host-owned runtime, scheduler, backend-binding and native-token evidence.

The required determinism matrix covers FSP off/on, SMT 1-way/4-way and different legal schedules, replay off/on/invalidation, checkpoint before/after retired VMCALL and cancellation/squash at E2-E3/E3-E5/E5-E6 boundaries. Compare retired/fault sequences, architectural writes, PC/domain transitions and completion/retire multiplicity; do not compare cycles, physical lane placement or queue timing. A successful probe has exactly one E3/E5/E6 and zero register, memory, VM-state, redirect or guest-visible completion effects.

Compiler emission and release are governance gates, not authority stages:

```text
Compiler Gate = optional
Release Gate  = exact-scope
```

O1 and canonical operand identity are closed by PR-C as non-authority. PR-D through PR-I close exact E2-E7, while PR-J closes cross-registry quiescence with the per-domain lifecycle gate and transition-in-flight accounting. Compiler remains closed by default; the Release Gate is closed for the development-local default-disabled exact profile only.

## Public / Local Provenance Boundary

The external audit names `d3814d1f332f083034d3b245f807a45f97792070` as a public GitHub reference. On 2026-08-09 that object was absent from the local object database, and direct resolution of the origin commit URL returned `404`. The local subjects `ddfffa2d7b86fb3ece21f8d21c6447ae47ee3868` and `a594d10abcbe8593d23fed16310af30706893452` do resolve locally, but their containment in any public master is not proven.

Therefore Phase 34-38 code anchors and implementation claims are evidence for this local refactoring branch/worktree only unless independently found in a named public-reference SHA. The audit-reported public reference, local later subjects and dirty worktree must never be substituted for one another in a manifest or release claim.

## Remaining Production Blockers

1. **PR-J provenance:** subject `bcd2d7f4654d4dab17c7a6705cb885fdd572510d` / tree `2ae54ab2ba4f0da1e9d95fc95dfb1ad83b080e33` follows PR-I dependency provenance and is named by the later evidence record.
2. **PR-J concurrency/quiescence:** closed with the per-domain lifecycle epoch/gate and transition-in-flight count in the quiescence predicate; all required races pass.
3. **PR-J activation/rollback:** closed for development-local activation: the default-disabled exact profile/domain provisioning and ordered kill switch preserve the fault-only fallback.
4. **PR-J release record:** the later non-self-referential development-local record binds exact D2/owner/policy/source/test/diagnostic/toolchain/rollback evidence and claims only `limited scoped activation of PROBE_NO_STATE_V1 only`.
5. **Deferred scope:** Compiler Gate is closed by default; all VMREAD outside the separately accepted exact Phase 42 `GuestCr0`/`GuestCr4` profile, VMWRITE, nested, SecureCompute, memory/IOMMU/device/lane/stream and broad virtualization activation remain denied.

## Explicit Non-Authorization

This ADR plus PR-B creates one `AcceptedVirtualizationDecision` policy entry. PR-C through PR-I materialize only the exact O1/E1-operand/E2/E3/E4/E5/E6/E7 dependency chain. E7 is not compatibility authority or release evidence. The later Phase 42 VMREAD package is independent and authorizes only exact `GuestCr0`/`GuestCr4` scalar delivery under its own D2. Every other VMREAD expansion, VMWRITE, nested virtualization, SecureCompute, memory/IOMMU/device effects, lane/stream authority, compiler emission and broad activation remains unauthorized.

Passing docs, tests, static scans, CI or clean-SHA evidence cannot skip a dependency. The default compatibility frontend/no-binding path remains `MissingNeutralOwner`/fault-only and cannot publish completion or retire. The exact neutral E2-E7 path exists independently under the default-disabled exact profile; its explicit activation and ordered rollback are development-local only and do not authorize broader virtualization.

## Next Open Pool

PR-J closes development-local exact-profile activation evidence at subject `bcd2d7f4654d4dab17c7a6705cb885fdd572510d` and its later record. Exact-profile monitoring is closed for its 200-iteration development baseline: it executes the real activation/rollback, `vmcall-denied`, and `e1-fault-transport` diagnostics with structured counters/traces only. The profile stays default-disabled, compiler gate remains closed by default, and no automatic activation expansion exists; runtime correctness does not depend on compiler VMCALL emission.

Phase 42 separately closes exact `GuestCr0`/`GuestCr4` scalar delivery and likewise opens no automatic next pool. Any further production surface must restart with a named owner-specific E0/D2 package.

## 2026-08-10 Historical PR-H Closure Addendum

After committed PR-G `2dfd76b38c9b9470c3e476c43ec0fc60544e9c22`, PR-H
materializes the already-decided `PreciseE5BoundNoStateRetire` policy. A per-CPU
neutral `DomainHypercallRetireOwner` issues opaque E6 only after the existing WB
stable retire prefix and fault ordering select the exact carrier and the entire
batch prevalidates. E6 binds E1/E5, post-Stage-B identity, VT/domain,
source/working/physical slots, retire-window/order epochs and restore generation,
then is consumed once by the ordinary WB finalizer with zero architectural
effects. `VmxRetireEffect` remains fault-only and is not E6.

This historical addendum closed PR-H only at that checkpoint. PR-I later closed
the exact E7 stage; release remains blocked on PR-J.
