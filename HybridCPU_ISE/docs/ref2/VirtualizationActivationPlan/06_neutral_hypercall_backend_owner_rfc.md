# Phase 06 - Neutral Hypercall Backend Owner RFC

Status: exact first-slice architecture/ABI accepted by Phase 38 and attributable machine D2 closed by PR-B; backend implementation remains blocked. No backend execution is opened by this document.

Current-state precedence: this file is an historical RFC/ADR record. Statements below that later stages are blocked describe their dated checkpoint; current status is governed only by `VirtualizationActivationStatusV1.json`.

## 2026-06-11 Audit Contract

- File name: `06_neutral_hypercall_backend_owner_rfc.md`.
- Purpose: define the first candidate owner-specific RFC/ADR for one no-state, domain-local VMCALL leaf.
- Status: `future-gated`; no backend execution is opened by this document.
- Scope: neutral hypercall backend owner, leaf ABI, capability/evidence/domain checks, deterministic result, migration class, rollback, denied adjacent leaves.
- No-goals: no current VMCALL success, no completion publication, no retire publication, no memory/I/O/lane/SecureCompute side effects, no VMCS/VMWRITE.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: future owner-approved neutral hypercall backend owner under runtime events, independent from the VMX/VMCS compatibility authority plane; this document does not appoint it.
- Required RFC/ADR: mandatory before code with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: `MissingNeutralOwner` remains production behavior until accepted RFC/ADR and implementation tests exist; otherwise requires owner-specific RFC/ADR.
- Tests/static scans: missing-owner denial, VMX frontend still calls `MissingNeutralOwner`, no `RuntimeOwnedPublication` in frontend, no `BackendExecutionAuthorized: true` without owner.
- Risks: replacing missing owner with a nominal descriptor, or treating trap projection/admission as backend success.
- Next-gate dependency: Phase 07 future implementation plan, then Phases 08/09 publication gates.

## Current RFC/ADR Snapshot

Accepted architecture ABI from Phase 38:

- Operation: `PROBE_NO_STATE_V1` through compatibility opcode `VMCALL`.
- Namespace/leaf: `HybridCPU.VMCALL.Runtime.v1`, 16-bit, invalid `0x0000`, exact `0x0001`.
- Inputs: actual full architectural `Rs1` value; `Rs2=x0`; `Rd=x0`/no result.
- Effect/result/migration: `NoStateNoPayload`, `NoPayload`, `DrainOnly`.

Phase 38 accepts `DomainHypercallRuntimeOwner` as the neutral architecture role and the exact operation above. This is not a runtime instance, non-zero OwnerId, accepted machine record or runtime capability. The older `HCPU_HV_PROBE_V1` candidate remains rejected; only `PROBE_NO_STATE_V1` is the accepted operation ID.

PR-C captures the full runtime `Rs1` leaf value exactly once after live E1 validation, rejects non-zero high bits and non-exact leaves, binds exact D2/O1 and requires `Rs2` and `Rd` to be x0. Compatibility selector/qualification values remain non-authoritative.

The 16-bit choice is accepted by the repository-owner ADR, not inferred from compatibility `ushort`. `VmxExitQualification.Leaf` and `VmxRetireEffect.VmCall` remain frozen compatibility vocabulary and cannot grant neutral runtime authority. Silent truncation is forbidden.

Future E2 must consume an immutable canonical operand snapshot containing both register selectors and their actual values for the live E1 attempt. The backend must not read the register file again and must not use compatibility `VmxExitQualification` as operand authority.

## ISE-HV-LEAF-DECISION-04 - Historical Verified ABI And Leaf Inventory

Decision state: decision-ready inventory only. Not accepted. No leaf is selected or reserved by this inventory.

Verified frozen/compatibility facts:

| ABI fact | Code evidence | Decision consequence |
| --- | --- | --- |
| `VMCALL` opcode | `IsaOpcodeValues.VMCALL == 259`; `VmxOpcodeAliasSet` contains `259 / VMCALL` | `259` is an instruction opcode, not a hypercall leaf ID |
| Operand form | `VmxOperandForm.HypercallLeafAndDescriptor` | the compatibility form has two source-register roles |
| Decode inputs | `Rs1 = HypercallLeafRegister`, `Rs2 = DescriptorRegister` | current frontend carries register selectors, not an accepted numeric leaf contract |
| Exit qualification | `VmxExitQualification(Leaf, Scope, Descriptor)` receives `rs1` and `rs2` from decode | the current admitted-denied projection records compatibility qualification; it does not read a runtime leaf value or authorize execution |
| Result register | `AdmitVmCallTrapProjection` passes `Rd: 0`; `VmxInstructionPayload` has no VMCALL result field | no VMCALL result-register ABI is proven by the production frontend |
| ISA metadata | `OpcodeRegistry` declares `VMCALL` privileged, two-operand, VMX-serial | operand count and scheduling class are proven; backend/result semantics are not |
| Machine-readable VMX baseline | `VmxSpecTable.Vmx8Opcodes` covers `250..257`; `VmxV2DraftsAreCompatibilityTargets == false` | a single cross-layer frozen VMCALL register/leaf specification is not proven |
| Numeric VMCALL leaf namespace | no production `VmxHypercallLeaf` enum, decode table, constant set, or exact-leaf admission table exists | exact numeric leaf ID is `не доказано`, `future-gated`, and `требует owner-specific RFC/ADR` |

Verified adjacent namespaces that must not be reused as VMCALL leaf authority:

- `VmxFunctionLeaf.CapabilityQuery == 1`, `Lane7QueryCaps == 7`, and `Lane7Submit == 8` are exact `VMFUNC` leaves, not `VMCALL` leaves.
- `VmxExitQualification.None.Leaf == 0` is a default/sentinel value, not a declared VMCALL leaf.
- `VmExitReason.VmCall == 18` is an exit-reason projection value, not a leaf.
- `NeutralHypercallBackendOwnerDescriptor` test owner ID `0x060A` is an owner identity, not a leaf.
- SecureCompute tests use `0x10` as a local allowlist fixture. Production `SecureHypercallDescriptor` exposes policy-supplied IDs and declares no frozen `0x10` leaf.
- Test request values `HypercallLeafRegister: 2` and `DescriptorRegister: 3` are register selectors. They do not prove leaf IDs `2` or `3`.

Verified neutral runtime operation classes:

- `ProjectCompatibilityTrap` is the only class consumed by the current VMCALL frontend path, and it is projection-only.
- `InvokeCapability` exists as a neutral runtime operation class but has no exact VMCALL leaf mapping, owner, executor, result source, or publication contract.
- `ReadCompatibilityProjection`, `WriteCompatibilityProjection`, `InvalidateTranslation`, `SaveDomainState`, and `RestoreDomainState` are separate operation classes and cannot be inferred as VMCALL leaf semantics.
- An operation class, leaf class, trap class, or compatibility opcode is not an exact numeric leaf.

Verified domain intersections:

- SecureCompute has policy-supplied hypercall IDs and argument classes (`Immediate`, `ExplicitSharedBuffer`, `OpaqueHandle`, raw private pointer denied), but secure admission/proof is not virtualization backend success.
- Memory, I/O, DMA/IOMMU, Lane6, Lane7, and Stream operations have their own neutral owners and may carry state, buffers, handles, tokens, evidence, or completion effects. They are excluded from the first no-state candidate.
- Migration/checkpoint has no authority to select a leaf. `NoPayload` is only a candidate migration class until an accepted owner-specific RFC/ADR proves exact semantics.

## ISE-HV-LEAF-DECISION-04 - Candidate Decision Matrix

The matrix lists only code-observed namespaces and the unresolved candidate class. It does not allocate a new ID.

| candidate source | exact numeric leaf ID | purpose and argument ABI | neutral owner | value/result source | capability policy | evidence class | migration class | deterministic no-state/no-payload semantics | denial reasons | adjacent denied leaves | secure-domain behavior |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| First no-state VMCALL candidate | `не доказано` | exact purpose is not proven; compatibility inputs are `rs1` leaf-register selector and `rs2` descriptor-register selector; no result-register ABI is proven | future neutral hypercall backend owner | no backend executor/result type exists; `VmExitReason` and trap projection are not result sources | not proven; decode `CapabilityValidated` is not an owner-specific backend grant | `CompatibilityAlias` exists for projection only; backend evidence is not proven | `NoPayload` candidate only | candidate constraint only; no executor exists to prove determinism, no state change, or no payload | exact ID absent; accepted RFC/ADR absent; runtime register-value source absent; owner/executor/result absent | all numeric VMCALL leaves remain denied because no VMCALL leaf namespace is frozen | accepted RFC/ADR must classify the exact leaf as non-secure or secure-no-effect |
| `VmxExitQualification.None` sentinel | `0` | no operation; default qualification only | none | none | none | none | none | not an executable leaf | sentinel is not a declared leaf | all VMCALL leaves denied | no secure effect |
| `VmxFunctionLeaf.CapabilityQuery` | `1` | `VMFUNC` capability query with function-leaf/descriptor form | VMFUNC-specific future/runtime policy, not VMCALL owner | capability projection vocabulary | VMFUNC capability validation | compatibility projection | not proven for VMCALL | not a VMCALL no-state proof | wrong opcode and operation namespace; capability projection cannot own backend execution | `VMFUNC` leaves `7` and `8`; all VMCALL leaves | cannot grant SecureCompute authority |
| `VmxFunctionLeaf.Lane7QueryCaps` | `7` | `VMFUNC` Lane7 capability query | Lane7/runtime owner, not VMCALL owner | Lane7 capability projection | explicit Lane7 grant | lane/runtime evidence, not VMCALL evidence | host/model-specific; not VMCALL `NoPayload` proof | lane intersection prevents use as the first neutral no-state VMCALL leaf | wrong opcode; Lane7 authority intersection; forbidden owner source | `VMFUNC` leaves `1` and `8`; all VMCALL leaves | non-secure VMCALL classification not proven |
| `VmxFunctionLeaf.Lane7Submit` | `8` | `VMFUNC` Lane7 submit | Lane7 runtime owner, not VMCALL owner | Lane7 backend/token/completion surfaces | explicit Lane7 grant | host/lane evidence | native token/state is non-migratable or separately owned | submission is not no-state/no-payload | wrong opcode; state/payload/backend side effects; lane authority forbidden | `VMFUNC` leaves `1` and `7`; all VMCALL leaves | secure use requires a separate secure/lane RFC and remains denied here |
| SecureCompute test allowlist fixture | `0x10` | test-local secure hypercall ID; argument ABI may include shared buffers or handles | SecureCompute runtime policies only | secure policy/admission result; never virtualization backend result | secure typed grant policy | secure evidence policy | secure migration policy | not proven as deterministic no-state/no-payload | test fixture only; no production frozen constant; secure admission/proof is not execution | every VMCALL leaf remains denied | secure-only policy surface; cannot be consumed by Phase 07 |

Decision outcome:

- No exact first VMCALL leaf is proven by current code or frozen ABI.
- No numeric ID is selected, reserved, or recommended by this packet.
- The first leaf remains `future-gated` and `требует owner-specific RFC/ADR`.
- Phase 06B and Phase 07 must remain blocked until neutral runtime owners accept an RFC/ADR that introduces or cites an already-frozen exact VMCALL leaf and completes every matrix column.

## ISE-HV-RFC-OWNER-DECISION-05 - Closure Decision

Decision date: 2026-06-12.

Decision state: closed `NO-GO` for Phase 06B implementation and Phase 07 backend execution. This is an audit/process closure, not an owner acceptance or RFC rejection on behalf of neutral runtime owners.

Verified decision evidence:

- `ADR-VIRT-HYPERCALL-BACKEND-2026-06-04` is accepted only as a denial/readiness hardening decision. It explicitly does not implement or authorize a hypercall backend owner.
- `RFC-HV-VMCALL-NO-STATE-OWNER-0001` remains draft-only.
- No accepted owner-specific RFC/ADR artifact names an exact numeric VMCALL leaf.
- `NeutralHypercallBackendOwnerRfcAdrState` has no accepted state.
- `NeutralHypercallBackendLeafSelection` has no exact numeric leaf state.
- `HypercallBackendAdmissionDecision` has no allowed backend execution decision.
- Production VMCALL still passes `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.

Closure classification:

| decision question | verified answer | closure result |
| --- | --- | --- |
| Did neutral runtime owners accept the draft RFC/ADR? | not proven | `NO-GO` |
| Is an exact numeric VMCALL leaf frozen? | no | `NO-GO` |
| Is purpose and argument/result ABI accepted? | no | `NO-GO` |
| Is the neutral owner service accepted and implemented? | no | `NO-GO` |
| Is capability/evidence/migration policy accepted for the exact leaf? | no | `NO-GO` |
| Is deterministic no-state/no-payload behavior proven by an executor? | no executor exists | `NO-GO` |
| Is secure-domain behavior accepted as non-secure or secure-no-effect? | no | `NO-GO` |
| May Phase 06B add positive backend admission? | no | remains blocked |
| May Phase 07 replace `MissingNeutralOwner(...)`? | no | remains blocked |
| May Phase 08 be wired to VMCALL? | no | remains future-gated |

Reopen requirements:

- an externally accepted neutral-runtime-owner RFC/ADR artifact;
- one exact numeric VMCALL leaf backed by a production neutral-runtime ABI symbol or table;
- complete owner/value/result/capability/evidence/migration/denial/secure-domain map;
- adjacent denial test list;
- explicit statement that acceptance authorizes Phase 06B implementation review but does not itself authorize backend execution, completion publication, or retire publication.

This decision task is closed. The implementation gate is not open.

## ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06 - Closure Record

Handoff date: 2026-06-12.

Handoff state: closed `NO-DECISION / RETURNED-BLOCKED`.

This closure proves that the decision-ready packet was handed to the external neutral runtime owner gate and that the repository was re-audited for an owner response. It does not record acceptance or rejection on behalf of neutral runtime owners.

Handoff result:

| required owner response | repository evidence | handoff result |
| --- | --- | --- |
| accept or reject an owner-specific RFC/ADR | no external owner acceptance or rejection artifact found | `NO-DECISION` |
| freeze one exact numeric VMCALL leaf | no production neutral-runtime leaf symbol or table entry found | `RETURNED-BLOCKED` |
| accept purpose and argument/result ABI | no accepted exact-leaf ABI contract found | `RETURNED-BLOCKED` |
| accept neutral owner and runtime value/result sources | draft descriptor only; no accepted service or executor | `RETURNED-BLOCKED` |
| accept capability, evidence, migration, and secure-domain policy | candidate classifications only | `RETURNED-BLOCKED` |
| authorize Phase 06B implementation review | not granted | remains closed |
| authorize Phase 07 consumption | not granted | remains closed |

Closure consequences:

- `RFC-HV-VMCALL-NO-STATE-OWNER-0001` remains draft.
- No exact numeric leaf is selected, allocated, or reserved.
- `ISE-HV-RFC-OWNER-DECISION-05` remains the applicable `NO-GO` decision.
- Phase 06B and Phase 07 remain blocked/future-gated.
- Production VMCALL must continue to use `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.
- The handoff closure is not owner acceptance, backend execution authorization, completion publication authorization, or retire publication authorization.

The handoff may be reopened only by a new external artifact that is attributable to the neutral runtime owner and explicitly accepts or rejects the complete exact-leaf packet. Silence, repository readiness, a draft descriptor, an operation class, or this closure record is not an owner decision.

## ISE-HV-OWNER-RESPONSE-07 - Closure Record

Response audit date: 2026-06-12.

Response state: closed `NO-RESPONSE / EXTERNAL-BLOCKED`.

The repository was checked after `ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06` for a new attributable neutral-runtime-owner response. No acceptance, rejection, amendment, exact-leaf allocation, or owner-signed replacement RFC/ADR was found.

Response matrix:

| expected response evidence | verified state | closure result |
| --- | --- | --- |
| attributable neutral-runtime-owner acceptance | absent | no implementation permission |
| attributable neutral-runtime-owner rejection | absent | draft remains unresolved |
| accepted exact numeric VMCALL leaf | absent | all leaves remain denied |
| accepted register argument/result ABI | absent | no runtime value/result contract |
| accepted owner/capability/evidence/migration map | absent | Phase 06B remains closed |
| accepted non-secure or secure-no-effect classification | absent | secure boundary remains denied |
| accepted executor review scope | absent | no executor may be added |

Closure rules:

- No response is not acceptance.
- No rejection is not acceptance.
- Repository-local readiness, tests, draft descriptors, and closure records cannot answer for the neutral runtime owner.
- This response audit does not supersede `ISE-HV-RFC-OWNER-DECISION-05`; its `NO-GO` remains applicable.
- Phase 06B and Phase 07 remain blocked/future-gated.
- Phase 08 completion publication and Phase 09 retire publication remain separate closed gates.

This response pool is closed because the audit is complete, not because the external owner dependency is satisfied.

Owner map:

| field/operation | owner | value source | capability policy | evidence class | migration class | denial reason |
| --- | --- | --- | --- | --- | --- | --- |
| VMCALL leaf execution | NeutralHypercallBackendOwner | runtime backend descriptor + leaf arguments + validated runtime domain | typed capability grant required; compatibility projection cannot own backend execution | `HostOwnedRuntimeEvidence` until completion publication exists | `NoPayload` | missing neutral owner, missing capability, missing evidence, invalid domain, unsupported leaf |
| Completion publication | TrapCompletionPublicationFence | neutral trap result + backend authorization | route must authorize completion explicitly after backend success | `CompatibilityAlias` | `RecomputedAfterRestore` | backend denied, route denied, fence denied |
| Retire publication | NeutralRetirePolicy | explicit retire rule + completion fence result | retire permission must be explicit and separate | `CompatibilityAlias` | `RecomputedAfterRestore` | completion insufficient, rollback failed, host evidence present |

Semantics ladder:

- admission != execution.
- backend success != completion publication.
- completion publication != retire publication.
- proof-only != activation approval.
- admitted-denied != backend success.

## RFC/ADR Packet - Draft v0

Identifier: `RFC-HV-VMCALL-NO-STATE-OWNER-0001`.

Decision state: draft only. Not accepted. No implementation permission. No backend execution path is opened by this packet.

Current code state:

- VMX frontend decodes `VMCALL` as `HypercallLeafAndDescriptor`.
- VMX frontend validates compatibility projection for `"VMCALL"`.
- Runtime boundary admission is projection-only through `DomainRuntimeOperationKind.ProjectCompatibilityTrap`.
- Neutral trap policy may produce `NeutralTrapResultKind.CompatibilityOperationIntercept`.
- Backend admission is invoked through `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.
- Current backend result is `HypercallBackendAdmissionDecision.MissingBackendDescriptor`.
- Phase 06A introduces `NeutralHypercallBackendOwnerDescriptor` as a draft-only runtime-side skeleton for `RFC-HV-VMCALL-NO-STATE-OWNER-0001`; it is not wired into the VMX frontend.
- A materialized-looking draft owner descriptor is still denied by `DeniedNeutralBackendOwnerRfcAdr`; no accepted owner semantics exist in code.
- `HypercallBackendAdmissionService` has no `Allowed` decision and emits no `BackendExecutionAuthorized: true`.
- Completion route remains `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)`.
- Completion fence remains denied until route and retire gates are separately satisfied.

Draft candidate scope:

- Candidate leaf class: no-state, no-payload, domain-local hypercall.
- Numeric leaf ID: not proven; future-gated.
- Leaf semantics: not proven beyond no-state/no-payload candidate constraints.
- Backend executor: not proven; requires owner-specific RFC/ADR acceptance.
- Capability bit: not proven; must be owner-specific if the leaf is not globally harmless.
- Evidence payload: `NoPayload` candidate only; any host-owned evidence must stay non-migratable and non-guest-visible.
- Migration payload: `NoPayload` candidate only; checkpoint/restore cannot be authority.

Current denied owner map:

| field/operation | owner | value source | capability policy | evidence class | migration class | current result | denial reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| VMCALL compatibility decode | VMX compatibility frontend vocabulary only | opcode + `HypercallLeafRegister` + `DescriptorRegister` | decode checks only; not backend capability | `CompatibilityAlias` | none | admitted to projection pipeline when decode checks pass | backend authority not implied |
| VMCALL projection admission | `RuntimeBoundaryAdmissionService` | `DomainRuntimeContext`, `RootAuthorityDescriptor`, `EvidencePolicyDescriptor` | `CapabilityBoundaryRequirement.None` for projection-only trap admission | `CompatibilityAlias` | `RecomputeAfterRestore` / projection-only | projection admission may be allowed | admission != execution |
| Neutral trap result | runtime-owned trap policy descriptor | `TrapPolicyDescriptor` + `TrapPolicyBitmap` + validated domain | trap policy, not backend capability | `CompatibilityAlias` | none | neutral trap may be produced | neutral trap != backend success |
| Backend admission request | neutral hypercall backend owner | current code passes `MissingNeutralOwner(...)` | missing owner descriptor; `CapabilityBoundaryRequirement` defaults to none | evidence policy passed through but no owner exists | none | `MissingBackendDescriptor` | no neutral runtime backend descriptor is materialized |
| Materialized-looking descriptor | neutral hypercall backend owner | `HypercallBackendDescriptor` shape | typed grant/evidence checks may pass in tests | policy-dependent | none | `DeniedNeutralBackendOwnerMissing` | neutral execution owner is still not explicitly admitted |
| Phase 06A draft owner skeleton | neutral hypercall backend owner | `NeutralHypercallBackendOwnerDescriptor.DraftNoStateCandidate(...)` | typed grant/evidence checks may pass in tests | policy-dependent; no payload only | `NoPayload` candidate only | `DeniedNeutralBackendOwnerRfcAdr` | `RFC-HV-VMCALL-NO-STATE-OWNER-0001` is draft only and has no accepted owner semantics |
| Completion route | `TrapCompletionRouteService` | neutral trap + backend authorization | route must see backend authorization | `CompatibilityAlias` after route only | `RecomputedAfterRestore` after route only | `DeniedBackendExecution` | backend execution is not authorized |
| Completion publication | `TrapCompletionPublicationFence` | route result + neutral reason/payload | fence-only; no VMX frontend authority | `CompatibilityAlias` after fence only | `RecomputedAfterRestore` after fence only | denied / empty completion | completion publication requires route authorization |
| Retire publication | future neutral retire policy | completion fence result + explicit retire rule | retire permission is separate | not proven | not proven | denied | completion publication is not retire publication |

Historical acceptance blockers at the June closure (superseded for role/ABI choice by Phase 38, still valid for missing machine/runtime artifacts):

- accepted owner-specific RFC/ADR is absent.
- exact numeric leaf is not selected.
- candidate wording such as "no-state, domain-local" is not an exact leaf ID and cannot be consumed by Phase 07.
- neutral backend executor is absent.
- backend execution result type is absent.
- `HypercallBackendAdmissionDecision` has no allowed backend execution decision.
- no code path may set `BackendExecutionAuthorized: true`.
- Phase 08 split completion-only route descriptor is future-gated and must remain unused by VMX frontend.
- Phase 09 retire rule is absent.
- migration and evidence proof is absent for any non-empty payload.
- SecureCompute `AllowedSecureOperation`, `AllowedProofOnlyNoExecution`, descriptor materialization, and privileged read-only projection cannot satisfy this virtualization backend owner gate.

Implementation split:

- PR-06A: introduce draft-only owner descriptor skeleton and denial tests while backend execution remains denied by default.
- PR-06B: add exact-leaf positive backend admission and adjacent denials behind accepted RFC/ADR marker.
- PR-07A: connect VMX compatibility frontend to the neutral owner only for the exact accepted leaf.
- PR-08/09: publish completion and retire only through their separate gates.

No implementation split may merge if it opens VMWRITE, VMCS state storage, active VMCS pointer, SecureCompute via VMX, nested authority, lane/stream authority, compiler emission, or migration/checkpoint authority.

Governance/runtime split for the implementation series:

- D2: attributable owner-approved architecture/ABI manifest, including exactly one numeric leaf; governance evidence only;
- E2: live operation-specific SafetyVerifier certificate bound to the D2 leaf and current operand/attempt identities;
- E3: backend consumes E2 and returns an opaque attempt-bound execution receipt with no completion/retire permission;
- E5/E6: separate completion token and canonical retire grant.

Before D2, only manifest schema/validator work, opaque denied-state E2 types, a disabled/null owner interface and negative tests are permitted. An `Allowed` decision, executor, positive registry, composition, completion or retire is prohibited.

## Historical Phase 06B Closure - Blocked By Draft RFC

Phase 06B was closed as blocked/future-gated in the June repository state. Phase 38 later supersedes the role/ABI-choice part of this closure; the absent machine D2, executor and runtime-authority findings remain current.

`ISE-HV-RFC-OWNER-DECISION-05` records the 2026-06-12 `NO-GO` closure. It prevents repeated implementation attempts from treating the older denial/readiness ADR, this draft packet, or the decision-ready leaf inventory as owner acceptance.

Current Phase 06B closure facts:

- `NeutralHypercallBackendOwnerRfcAdrState` has no accepted state.
- `HypercallBackendAdmissionDecision` has no allowed backend execution decision.
- A draft owner skeleton still returns `DeniedNeutralBackendOwnerRfcAdr`.
- VMX frontend production behavior remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.
- No code path may set `BackendExecutionAuthorized: true`.
- No Phase 06B work may replace `MissingNeutralOwner(...)`, add a neutral executor, publish completion, publish retire, or use VMX/VMCS/VmxCaps/migration/lane/SecureCompute/compiler as authority.

Phase 06B may reopen only after neutral runtime owners accept `RFC-HV-VMCALL-NO-STATE-OWNER-0001` or a replacement owner-specific RFC/ADR with the complete owner map, exact leaf, capability policy, evidence policy, migration class, executor contract, adjacent denials, and tests.

The accepted packet must also classify the exact leaf as non-secure or secure-no-effect. Reusing a SecureCompute admission/proof/projection result as the neutral VMCALL backend owner is forbidden and must remain denied.

## 2026-08-09 Repository-Owner ADR Addendum

Phase 38 supersedes the earlier current-state wording that the role, namespace, width, operation and numeric leaf are unselected. The repository-owner architecture decision now accepts:

```text
owner role = DomainHypercallRuntimeOwner
owner id = 0x4843_4F57_4E52 (HCOWNR)
owner policy version = 1
owner epoch = 1
namespace = HybridCPU.VMCALL.Runtime.v1
width = 16
invalid = 0x0000
leaf = 0x0001
operation = PROBE_NO_STATE_V1
ABI = Rs1 full architectural value, Rs2=x0, Rd=x0/no result
effect = NoStateNoPayload
migration = DrainOnly
capability = DomainGranted typed VmCallProbeNoStateV1, bit 41, NeverProject
evidence = None
domain = ExecutionDomainBound, non-zero tag, no Memory/IO/address space
secure domain = Deny
completion = HostOwnedRuntimeEvidence / HostOwnedNonMigratable / NeverProject
```

This addendum accepts an architecture role and stable numeric allocation, not an executable owner service or live authority. PR-A provides v2 contracts/digests/validation, PR-B provides attributable machine D2 and allocation/exact lookup metadata, and PR-C provides immutable O1 plus operand identity only. The production descriptor remains draft/denied, `MissingNeutralOwner(...)` remains the current frontend input, and no live grant, E2, executor/backend/completion/retire work is authorized.

The numeric value is scoped only to the new VMCALL namespace. `VmxFunctionLeaf.CapabilityQuery = 1` is a distinct frozen VMFUNC namespace and is not a collision or authority source. `VmxExitQualification.Leaf` contains a register selector for current VMCALL decode, so it must never be consumed as the runtime leaf value.

## Phase Goal

Define the first production-oriented owner-specific RFC/ADR: a minimal neutral hypercall backend owner for one no-state, domain-local VMCALL leaf.

## Historical Baseline (2026-06-11)

VMCALL currently passes decode, frozen alias projection validation, `RuntimeBoundaryAdmissionService`, neutral trap policy, `NeutralTrapResult`, backend admission evaluation, route evaluation, and publication fence evaluation. It still uses `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`, so backend execution remains denied before completion/retire publication.

## Owner Of Authority

`DomainHypercallRuntimeOwner` is the accepted architecture role. Neutrality means independent from the compatibility authority plane, not necessarily external to this repository. VMX frontend is only the compatibility ingress and projection surface.

PR-B materializes machine-validated D2 plus repository allocation metadata. PR-C now loads a read-only O1 policy snapshot and captures canonical operands, but no executable owner service, live capability grant, E2 or executor authority exists. D2/O1/snapshot metadata grants no runtime permission.

## What Can Be Implemented

RFC/ADR only at first. The recommended owner scope is deliberately small:

- one domain-local leaf;
- no host-private evidence exposure;
- no memory/I/O/lane/SecureCompute side effects;
- no scheduler or debug trace serialization;
- typed grant required if the leaf is not globally harmless;
- deterministic neutral completion payload;
- explicit rollback/no-state semantics.

After RFC/ADR approval, implementation may add a materialized neutral backend descriptor, neutral executor, injected backend admission request, and owner-specific tests.

## What Remains Denied/Future-Gated

- All hypercall leaves outside the RFC.
- Any leaf with memory, I/O, Lane6/Lane7/Stream, SecureCompute, scheduler, native handle, or host evidence side effects.
- Any completion/retire publication before Phases 08 and 09 gates.
- Any VMCS/VMWRITE involvement.

## Forbidden Shortcuts

- Treating `VMCALL`, `VmExitReason`, `TrapDecision`, or `VmxTrapProjectionMapper` as backend success.
- Setting `NeutralBackendOwnerMaterialized = true` without actual neutral semantics.
- Passing `RuntimeOwnedPublication` only because backend admission returned an allowed-looking value.
- Using compatibility projection as `HypercallBackendAuthority`.

## Required RFC/ADR

Required before code. The ADR must name:

- leaf ID and argument ABI;
- neutral owner descriptor/service;
- capability requirement;
- evidence requirement;
- domain validation;
- backend execution result type;
- completion route requirement;
- retire rule;
- migration class;
- rollback and denial behavior;
- negative tests for all adjacent denied leaves.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/07_hypercall_backend_owner_and_vmcall_decision.md`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`
- `HybridCPU_ISE/docs/VMXRefactoring/2026-05-24-vmx-current-model-completion-audit.md`

## Required Tests

Before implementation:

- keep `MissingNeutralHypercallBackendOwner_KeepsBackendExecutionDenied`;
- add static test that VMX frontend still calls `MissingNeutralOwner` until RFC implementation PR.

After RFC implementation:

- positive admission for exactly the approved leaf;
- capability denial;
- evidence denial;
- domain validation denial;
- unknown leaf denial;
- no host evidence leakage;
- no completion/retire until Phases 08/09 conditions are met.

## Required Static/Source Scans

```powershell
rg -n "HypercallBackendAuthority.CompatibilityProjection|TrapDecision.*Backend|VmExitReason.*Backend|NeutralBackendOwnerMaterialized: true|RuntimeOwnedPublication" HybridCPU_ISE/CloseToHSL/Core
```

Every match must be explained by RFC/ADR and tests.

## Migration/Evidence Classification

Recommended first leaf should be `NoPayload` or `RecomputedAfterRestore`. Backend handles, host evidence, native tokens, scheduler evidence, and debug traces must be `HostOwnedNonMigratable` and absent from guest-visible projection.

## Completion/Retire Implications

Backend owner RFC must not imply publication. It only creates the possibility of backend execution authorization. Completion route and retire publication require Phases 08 and 09.

## Exit Criteria

- RFC/ADR accepted or explicitly rejected.
- Owner map complete for the chosen leaf.
- Negative tests listed.
- No code path opened in this phase alone.

## Dependency On Previous/Next Phase

Depends on Phases 01-03. Phase 07 consumes the accepted owner to plan VMCALL success path.

