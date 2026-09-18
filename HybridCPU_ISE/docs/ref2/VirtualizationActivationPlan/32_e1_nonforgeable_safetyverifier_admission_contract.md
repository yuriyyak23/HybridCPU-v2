# E1 Non-Forgeable SafetyVerifier Admission Contract

Status date: 2026-08-06

Status: `E1 IMPLEMENTED / FAULT-ONLY TRANSPORT / NO BACKEND AUTHORITY`.

Current-state precedence: this E1 contract remains fault-only, but later exact-probe stages are no longer evaluated from historical blocker wording in this file. Use `VirtualizationActivationStatusV1.json` for current D2/O1/E2-E7 and gate status.

This document records the implemented E1 boundary. Production now carries an opaque, attempt-bound `SafetyVerifier.VirtualizationAdmissionCertificate` from canonical issue-packet lane-7 materialization to `VmxMicroOp`. The certificate cannot authorize backend execution, completion publication or retire publication, and every VMX operation still executes and retires through the pre-existing deterministic fault path.

## 2026-06-11 Audit Contract

- File name: `32_e1_nonforgeable_safetyverifier_admission_contract.md`.
- Purpose: define and record the non-forgeability, identity binding, invalidation and authority-separation contract implemented by E1.
- Status: E1 certificate, exclusive issuer/validator and canonical fault-only transport implemented; no backend authority.
- Scope: SafetyVerifier-exclusive issuance, live-state validation, canonical transport, invalidation, fail-closed denial and absence guards.
- No-goals: no backend admission, exact leaf, completion, retire, migration authority or activation.
- Code anchors: `SafetyVerifier.VirtualizationAdmission.cs`, `ReplayPhaseSubstrate.Interfaces.cs`, `ReplayPhaseSubstrate.Implementations.cs`, `MicroOpScheduler.SMT.cs`, `CPU_Core.PipelineExecution.Materialization.cs`, `MicroOp.IO.cs`.
- Authority owner: existing neutral canonical legality/SafetyVerifier owner; this document appoints nobody and grants no runtime authority.
- Required RFC/ADR: E2 and every later positive implementation still require the full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: every required identity is bound to issuer-owned live state, every mismatch denies, current boolean inputs remain non-authoritative, and production VMX remains fault-only.
- Tests/static scans: issuer exclusivity and live-registry validation; public/default-constructor absence; stale, foreign, mutated, cross-owner and slot mismatch denial; frontend SafetyVerifier isolation; backend/completion/retire shortcut absence; canonical `CloseToHSL` layout.
- Risks: publicly constructible value tokens, identity reconstruction, stale/replay reuse, boolean-to-authority promotion, or interpreting a positive fixture as execution.
- Next-gate dependency: PR-B later closed attributable exact-leaf D2 and PR-C later closed O1/operand identity. Production D2-bound E2 remains blocked on separate authorization and live capability/root/attempt/restore bindings; the Phase 36 TESTING-only prototype certificate does not satisfy this gate.

## Authority Boundary

The issuer is the existing neutral canonical legality/SafetyVerifier owner reached through the scheduler's internal `IVirtualizationAdmissionService`. This plan appoints nobody. The public `IRuntimeLegalityService` does not expose issuance. VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, generated code, documentation, tests and fixtures cannot create a certificate that validates against issuer-owned live state.

The current `VmxCompatDecodeRequest` validation booleans are compatibility/readiness inputs only. `DescriptorValidated`, `CapabilityValidated`, `SchedulingValidated`, `NoEmissionValidated`, `ProjectionEvidenceValidated` and similarly named flags are not SafetyVerifier authority and cannot be promoted, combined, copied or hashed into authority.

## Implemented Shape

The certificate is an opaque attempt-bound capability, not a publicly/default constructible record or caller-authored DTO. Its current E1 payload binds without later reconstruction:

- schema/version and exact frozen opcode identity;
- operation identity and an explicit absent state for the still-unaccepted exact numeric leaf;
- VT identity and neutral runtime domain tag, with address-space identity explicitly absent;
- source slot and verified working slot;
- bundle identity, issue-attempt identity and attempt epoch;
- replay epoch and issuer invalidation generation; rollback/restore identity remains explicitly absent and therefore backend-incapable;
- explicit absent states for descriptor identity/digest and descriptor epoch;
- explicit absent states for capability-grant identity, scope and revocation epoch;
- explicit absent states for evidence-policy identity/digest and evidence epoch;
- the issuer identity `SafetyVerifier` and an opaque issuance identity validated against issuer-owned live state.

An unassigned leaf is a typed absence state, never a numeric placeholder. A certificate with an absent owner decision or absent exact leaf cannot authorize VMCALL backend execution.

## Non-Forgeability Requirements

- Construction is inaccessible to compatibility frontend, compiler, tests and ordinary callers; no public constructor, public factory, default-valid value or deserialize-to-valid path is allowed.
- Validity requires issuer-owned live issuance state, not only equality of public fields, a checksum, validation booleans or a caller-provided digest.
- Validation compares every bound identity against live canonical pipeline state and returns a typed fail-closed denial on any mismatch.
- The authoritative instance is transported from issuance to consumption. A later stage cannot rebuild it from opcode, registers, metadata, DTOs or projection results.
- Squash, retry, reschedule to a different working slot, capability revocation, descriptor/evidence epoch change, rollback, restore or replay-generation change invalidates the certificate.
- Consumption cannot mint backend-result, completion or retire authority. Those remain E3, E5 and E6 owner tokens respectively.

## Implemented Negative Checks

The E1 tests deny public/default construction, foreign issuer, mutation after issuance, invalidated issuance, cross-owner identity, wrong source/working slot and duplicate attachment. Validation binds the live issuer registry, issuer generation, opcode/operation/carrier digest, VT, owner context, domain, source/working slot, bundle identity and replay epoch. Identities that require E2 or later owners are typed absent and cannot authorize execution.

Static guards require all of the following:

- certificate references are confined to the SafetyVerifier/scheduler transport allowlist and `VmxMicroOp` carrier;
- no VMX handler reference to `SafetyVerifier` as a substitute for a transported certificate;
- no backend allowed enum state, backend executor or `BackendExecutionAuthorized: true`;
- dispatcher, completion and retire paths remain fail closed;
- `CloseToHSL` is the only canonical active source tree; `CloseToRTL` is obsolete and cannot satisfy evidence or source scans.

## Implemented Positive Check

The positive E1 test proves only that SafetyVerifier issues and validates one correctly bound certificate after canonical issue-packet lane-7 materialization while `VmxMicroOp.Execute`, its retire effect and canonical retire application remain fault-only. It is not evidence of E2 owner acceptance, a numeric leaf, backend execution, completion, successful retire or activation.

## Closure And Next Gate

The contract, implementation and adjacent static guards are closed. E1 is `CLOSED/FAULT-ONLY`. Later PR-B accepts the exact D2 leaf and PR-C binds O1/operand identity without changing E1. E2 remains `BLOCKED`: no live generation-bearing grant or D2-bound SafetyVerifier E2 issuer exists. E1 grants no authority to begin E3 backend work.
