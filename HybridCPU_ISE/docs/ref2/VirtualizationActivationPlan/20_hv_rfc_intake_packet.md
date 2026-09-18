# Phase 20 - Hypercall Owner RFC Intake Packet

Status: owner-facing RFC/ADR intake packet only. Draft/readiness evidence. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `20_hv_rfc_intake_packet.md`.
- Purpose: prepare the owner-facing intake packet for `RFC-HV-VMCALL-NO-STATE-OWNER-0001` / `Minimal VMCALL backend owner` after Phase 19 selected it as the next intake queue item.
- Status: intake packet only; no RFC/ADR is accepted and no exact VMCALL leaf is selected, allocated, reserved, or recommended by this document.
- Scope: owner decision questions, verified frozen ABI facts, not-a-leaf denials, owner map template, capability/evidence/migration/completion/retire questions, adjacent denied surfaces, and response checklist.
- No-goals: no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for the exact hypercall backend path; VMX frontend, VMCS/`VmxCaps`, SecureCompute admission, compiler metadata, migration evidence, lanes, streams, tests, docs, and this intake packet are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response is required with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: accepted, rejected, or amended only by an attributable external neutral-runtime-owner artifact; until then `MissingNeutralOwner` remains production behavior and Phase 06B/Phase 07 remain blocked/future-gated.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, and no production source diff.
- Risks: owner-facing packet wording being mistaken for acceptance, leaf class being mistaken for exact numeric leaf, SecureCompute proof-only/admission being mistaken for execution, backend success being mistaken for completion publication, or completion publication being mistaken for retire publication.
- Next-gate dependency: external owner response; only an accepted exact-leaf RFC/ADR can reopen Phase 06B implementation review, and that acceptance still does not open completion or retire.

## ISE-HV-RFC-INTAKE-PACKET-20 - Closure Record

Closure date: 2026-06-18.

State: closed `OWNER-FACING PACKET PREPARED / DRAFT-ONLY / OWNER-ACCEPTANCE-REQUIRED / NO-PRODUCTION-CHANGE`.

Prepared packet: `RFC-HV-VMCALL-NO-STATE-OWNER-0001` for `Minimal VMCALL backend owner`.

This packet is ready to hand to neutral runtime owners. It does not accept the RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, release, or backlog wording.

## Owner Response Requested

Neutral runtime owners are asked to return exactly one of:

| Response | Required content | Repository consequence |
| --- | --- | --- |
| accept | exact numeric VMCALL leaf, argument ABI, result ABI, neutral owner service, value/result source, capability policy, evidence class, migration class, deterministic no-state/no-payload statement, denial reasons, adjacent denied leaves, secure-domain behavior, completion rule, retire rule, rollback rule, and required tests | may reopen Phase 06B implementation review only; does not by itself authorize backend execution, completion publication, or retire publication |
| reject | rejection reason and whether the minimal no-state VMCALL path should remain denied or be replaced by another owner-specific intake | keeps Phase 06B/Phase 07 blocked; no production behavior change |
| amend | exact missing fields and requested changes to this packet | keeps Phase 06B/Phase 07 blocked until a later accepted exact-leaf artifact exists |

Silence, lack of rejection, repository readiness, green tests, static scans, closure records, handoff records, response audits, rollout order, release gate, operation class names, draft descriptors, or this packet are not owner acceptance.

## Verified Frozen ABI Facts For Owner Review

| Fact | Current evidence | Owner decision needed |
| --- | --- | --- |
| instruction opcode | `VMCALL` opcode is `259` | confirm this is only ingress vocabulary, not a leaf ID |
| operand form | `VmxOperandForm.HypercallLeafAndDescriptor` | define whether runtime leaf and descriptor values are read from registers named by `rs1`/`rs2`, or another owner-approved value source |
| decode roles | `Rs1 = HypercallLeafRegister`, `Rs2 = DescriptorRegister` | define exact runtime argument ABI and any result ABI |
| result register | no production VMCALL result-register ABI is proven | either define a result ABI or explicitly require `NoPayload`/no result |
| current owner state | `NeutralHypercallBackendOwnerDescriptor` is draft-only and `CandidateOnlyNoNumericLeaf` | accept, reject, or amend the owner descriptor shape |
| current frontend behavior | VMX frontend calls `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` | this must remain until accepted exact-leaf implementation review |
| current route behavior | completion route remains projection-only denied | backend success, if later approved, must still pass separate completion and retire gates |

## Not-A-Leaf Denial List

These values and classes are expressly non-authoritative for exact VMCALL leaf allocation:

| Source | Value/class | Denial reason |
| --- | --- | --- |
| `VMCALL` opcode | `259` | instruction opcode, not hypercall leaf ID |
| VM exit reason | `VmExitReason.VmCall == 18` | exit classification, not leaf ID |
| VMFUNC leaves | `1`, `7`, `8` | different opcode namespace |
| owner descriptor fixture | `0x060A` | owner/test identity, not leaf ID |
| test request register selector | `2` / `3` | register selectors, not runtime leaf values |
| SecureCompute fixture | `0x10` | test-local secure allowlist value, not frozen VMCALL leaf |
| operation class | `NoStateNoPayloadDomainLocal` | candidate class, not exact numeric leaf |
| leaf selection class | `CandidateOnlyNoNumericLeaf` | explicit absence of exact numeric leaf |

## Owner Map Template

The owner response must fill every `owner-required` cell. Repository-local values below are evidence only.

| field/operation | current repository evidence | owner | value source | capability policy | evidence class | migration class | denial reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| exact VMCALL leaf | owner-required; not proven | owner-required | owner-required | owner-required | owner-required | owner-required | absent exact leaf keeps all leaves denied |
| argument ABI | `rs1`/`rs2` register-selector roles proven only as compatibility decode | owner-required | owner-required runtime value source | owner-required | owner-required | owner-required | register selectors are not argument values without owner contract |
| result ABI | no production result register proven | owner-required | owner-required result source or `NoPayload` | owner-required | owner-required | owner-required | no result publication without owner and completion gates |
| neutral backend owner | draft descriptor skeleton only | owner-required neutral runtime owner | owner-required descriptor/service | owner-required typed grant or explicit harmless classification | owner-required | owner-required | draft-only owner semantics remain denied |
| backend execution result | no executor/result type exists | owner-required | owner-required executor result source | owner-required | owner-required | owner-required | no backend execution until implementation review after acceptance |
| completion route | Phase 08 future-gated route/fence scaffolding | completion route owner required after backend acceptance | neutral backend result only | route policy required | completion evidence only after fence | recomputed/no-payload class required | backend success is not completion publication |
| retire rule | Phase 09 future-gated | retire owner required after completion publication | explicit retire policy | retire policy required | retire evidence only | owner-required | completion publication is not retire publication |
| secure-domain behavior | not proven | owner-required | owner-required | owner-required | owner-required | owner-required | must be non-secure or secure-no-effect; SecureCompute admission/proof cannot substitute |

## Adjacent Denied Surfaces

The owner response must not widen the intake to:

- any VMCALL leaf other than the exact accepted leaf;
- VMREAD widening beyond guarded read-only `GuestCr0`/`GuestCr4`;
- VMWRITE or VMCS mutation;
- host aliases or compatibility-control values;
- nested VMCS12/VMCS02/Shadow VMCS authority;
- SecureCompute activation through VMX/VMCS/`VmxCaps`;
- memory/I/O/IOMMU passthrough;
- Lane6/Lane7/Stream helper, token, telemetry, replay, or backend-binding authority;
- compiler-controlled emission;
- migration/checkpoint/restore payload authority;
- completion publication or retire publication before their separate gates.

## Secure-Domain Question

The owner must classify the exact leaf as one of:

- non-secure: no SecureCompute operation, descriptor, proof, private pointer, shared buffer, handle, or secure policy result participates in backend execution;
- secure-no-effect: secure domain may observe the same denied/no-payload semantics, but `AllowedSecureOperation` and `AllowedProofOnlyNoExecution` remain non-execution evidence.

Any other secure behavior requires a separate SecureCompute owner RFC/ADR and remains out of this packet.

## Required Negative Tests For Any Future Accepted Implementation

- draft RFC only remains denied;
- unknown VMCALL leaf remains denied;
- missing neutral owner remains denied;
- missing domain validation remains denied;
- missing typed grant remains denied when capability is required;
- missing evidence policy remains denied when evidence is required;
- compatibility projection, VMX, VMCS, `VmxCaps`, migration evidence, SecureCompute admission/proof, compiler metadata, lane/stream evidence, and tests/docs cannot source backend authority;
- backend authorized but route denied publishes no completion;
- completion authorized but retire denied publishes no retire;
- all adjacent denied surfaces above remain denied.

## Static Guards To Keep During Intake

```powershell
rg -n "BackendExecutionAuthorized: true|HypercallBackendAdmissionDecision\.Allowed" HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend
rg -n "TrapCompletionRouteDescriptor\.RuntimeOwnedPublication" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend
rg -n "MissingNeutralOwner|ProjectionOnlyDenied|EvaluateFence" HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs
```

All matches or absences must preserve the current denied production path.

## Handoff Payload

Owner-facing packet contents:

- this Phase 20 document;
- Phase 06 draft RFC/ADR snapshot and verified ABI inventory;
- Phase 07 consumption contract and blocked baseline;
- Phase 19 selected intake record;
- required owner response table above.

Handoff result is not acceptance. Only a later attributable external neutral-runtime-owner artifact can accept, reject, or amend this packet.

## Exit Criteria

- one owner-facing packet exists for `Minimal VMCALL backend owner`;
- packet explicitly asks owners for accept/reject/amend;
- exact numeric leaf remains unallocated by the repository;
- production VMCALL remains `MissingNeutralOwner` and `ProjectionOnlyDenied`;
- no backend executor, completion publication, retire publication, or VMX frontend behavior change is introduced.

## Dependency On Previous/Next Phase

Depends on Phase 19 intake selection. The next step is external owner response review for `RFC-HV-VMCALL-NO-STATE-OWNER-0001`; absent that response, Phase 06B and Phase 07 remain blocked/future-gated.
