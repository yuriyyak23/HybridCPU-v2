# Compiler No Emission To Controlled Emission ADR/Plan

## Phase Metadata

- File name: `19_compiler_no_emission_to_controlled_emission_gate.md`
- Phase goal: close the compiler decision gate by preserving no-emission now and making controlled emission future-gated.
- Status: product compiler no-emission confirmed; generated-artifact reproducibility remains C2-open. A hermetic test-only carrier profile is recommended before the neutral transport probe, while product controlled emission remains a separate future RFC after limited runtime release.
- Scope: compiler, ISA, VLIW, secure backend helper emission, secure hypercall helper emission, sideband metadata and Lane6/Lane7/Stream carriers.
- No-goals: no current secure backend emission, no CHERI ISA, no capability operands, no capability-aware LOAD/STORE/FETCH and no VMX secure-mode emission.

## ADR-SC-COMPILER-NOEMISSION-CONTROLLED-GATE

Phase 19 adds `SecureComputeControlledEmissionGatePolicy` as a fail-closed compiler decision gate. The only allowed current result is `NoEmissionPreserved` for `NoCompilerChange`. Any request for SecureCompute compiler emission is denied.

Even a hypothetical request with positive runtime owner, controlled-emission RFC, release approval and backend execution flags is still denied in Phase 19 as `DeniedCompilerEmissionFutureGated`. This phase records the controlled-emission gate; it does not implement controlled emission.

The reconciled analysis introduces a separate concept: a future hermetic test-only controlled-carrier profile. It is not a new branch in `SecureComputeControlledEmissionGatePolicy`, is not reachable from public product compiler/assembler APIs, is not shipped, owns no runtime authority and must remain denied by the default runtime profile. Its sole purpose is to produce a frozen conformance vector that enters through the public canonical decoder so decode -> SafetyVerifier -> issue reachability is not “proven” with handcrafted post-decode objects.

## Current Baseline

`SecureComputeNoEmissionContract` denies new instruction encodings, new operand formats, capability-aware load/store/fetch and VMX secure-mode emission. Existing compiler/ISA conformance treats generated artifacts and sidebands as non-authoritative.

External audit clarification, 2026-06-11: the no-emission boundary must be evidenced as an enforceable compiler/runtime gate, not only as a policy document. Activation requires exact negative tests and source scans proving no secure backend emission is reachable before a controlled-emission RFC/ADR.

## Authority Owner

The compiler owns only emitted program representation after runtime/legal gates permit it. It does not own SecureCompute authority, backend execution, completion publication, retire publication or activation evidence.

## Request/Result Vocabulary

`SecureComputeControlledEmissionRequest` classifies:

- no compiler change;
- secure backend helper;
- secure hypercall helper;
- secure sideband metadata;
- future controlled emission.

The request explicitly carries prerequisite flags for positive neutral runtime owner, controlled-emission RFC, release approval and backend execution authorization. It also carries no-emission violation flags for new instruction encoding, new operand format, capability-aware memory instructions and VMX secure-mode emission.

`SecureComputeControlledEmissionResult` publishes only classification fields. All emission and backend authority bits are false in Phase 19.

## Current Allowed Decision

`NoCompilerChange` with `RequestsCompilerEmission: false` is allowed as `NoEmissionPreserved`. It means the first restricted SecureCompute path must use existing runtime/compatibility transport with no compiler secure-emission change.

This remains the only current production decision. The recommended test-only profile is an open conformance-tooling change after taxonomy, certificate and production carrier work; it does not alter this current result.

## Recommended Early Test-Only Carrier Profile

Before the named neutral transport probe, add a separately built test profile with all of these properties:

- explicit test-only build/profile and non-production signer;
- no public compiler, assembler or runtime configuration switch;
- no production package or generated artifact inclusion;
- only one frozen operation/vector already present in the accepted taxonomy;
- carrier bytes and sideband pass through the public canonical decoder;
- runtime default remains hard deny;
- artifact inventory proves the profile and signer are absent from product output;
- mutation, unknown-operation, direct post-decode construction and production-profile attempts fail closed.

The compiler-side `TestOnlyHarness`, model-only and scoped-test authority categories are compatible precedents: their outputs are evidence/transport only and cannot satisfy runtime legality. An ADR must still name the exact profile owner, artifact root and exclusion mechanism before implementation.

## Denied Shortcuts

Phase 19 denies:

- compiler helper as runtime authority;
- compiler helper as secure backend execution;
- secure hypercall helper emission before a positive runtime owner;
- sideband metadata as executable SecureCompute operation;
- new SecureCompute opcode or instruction encoding;
- new SecureCompute operand format;
- capability-bearing operands or capability registers;
- capability-aware LOAD/STORE/FETCH;
- tagged-memory instruction semantics;
- VMX secure-mode emission;
- Lane6/Lane7 native token or descriptor carrier as SecureCompute grant;
- controlled emission without a future separate RFC and release evidence.
- describing the hermetic test-only carrier profile as product compiler emission, backend authority, activation evidence or release approval;
- using a handcrafted `MicroOp`, request DTO or certificate fixture instead of public canonical decode for the end-to-end slice.

## Required Future Controlled-Emission RFC

Controlled-emission work stays future-gated and requires a separate RFC/ADR after a positive neutral runtime owner exists. The RFC must define:

- ISA/ABI scope;
- owner/path binding;
- legality A/B;
- runtime admission;
- evidence/migration classification;
- completion/retire publication behavior if any;
- compiler metadata format;
- negative tests;
- rollback procedure.

## Code Anchors

- `SecureComputeNoEmissionContract.cs`
- `SecureComputeControlledEmissionGatePolicy.cs`
- `VirtualizationNoEmissionContract.cs`
- `HybridCPU_Compiler/**`
- compiler no-emission tests;
- Lane6/Lane7/Stream compiler bridge tests.

## Required Tests

- no-compiler-change decision preserves no-emission and creates no authority;
- secure backend helper emission request is denied;
- secure hypercall helper emission request is denied;
- sideband metadata emission request is denied as executable authority;
- future controlled emission remains denied even with prerequisite flags;
- no new secure opcodes;
- no new secure operand formats;
- no capability-aware LOAD/STORE/FETCH;
- no VMX secure-mode emission;
- compiler source scans prove no production compiler path emits secure backend execution before controlled-emission approval.
- future test-only profile isolation proves no profile symbol, signer, carrier or API is present in production artifacts;
- future controlled test vector reaches public canonical decode and expected runtime denial without a post-decode fixture bypass.

## Required Static/Source Scans

Use scoped source scans over `SecureComputeControlledEmissionGatePolicy.cs`, `SecureComputeNoEmissionContract.cs` and selected compiler API/IR construction surfaces:

- no `CompilerEmissionAuthorized: true`;
- no `BackendExecutionAuthorized: true`;
- no new instruction encoding authorization;
- no new operand format authorization;
- no capability-aware memory instruction authorization;
- no VMX secure-mode authorization;
- no secure backend execution request/result dependency;
- no SecureCompute emit/helper shortcut in compiler API/IR surfaces.

## Migration/Evidence Classification

Compiler artifacts and sidebands are conformance or transport metadata, not migration authority and not runtime evidence unless a future RFC classifies them. Phase 19 adds no manifest payload class.

## Completion/Retire Implications

Compiler emission cannot publish completion or retire effects. Phase 19 adds no completion record and no retire record.

## SecureCompute Activation Implications

Limited activation can occur without product compiler secure emission only if the named path records the explicit no-compiler-change decision and all other gates pass. Product controlled emission remains a later gate. The earlier hermetic test-only profile is required conformance scaffolding only and cannot be cited as a release input beyond its decode/path test artifacts.

If the first restricted path is reached only through existing compatibility/runtime transport, no-emission remains the required compiler decision. Phase 13 RFC acceptance, typed request/result vocabulary, Phase 17 VMX zero-authority or release documentation do not justify compiler changes.

## Bounded Rollback Procedure

Rollback Phase 19 by removing `SecureComputeControlledEmissionGatePolicy.cs`, removing `SecureComputeCompilerPhase19ControlledEmissionGateTests.cs`, reverting this file and restoring Phase 19 to future compiler gate status. Do not reset unrelated compiler work or Phase 13 through Phase 17 closures. Re-run compiler no-emission and release-gate slices after rollback.

## Exit Criteria

- no-emission tests cover SecureCompute activation corpus;
- controlled-emission RFC requirements are explicit;
- no compiler path creates secure backend side effects;
- the first positive path records an explicit no-compiler-change decision unless a future controlled-emission RFC is separately approved.

Exit status: satisfied for current product compiler contour/lowering rejection, not for release evidence. The compiler maps `SecureComputeAdmission` to `SecureComputePolicyAdmissionOnly`/`NoEmission` and excludes that contour from production lowering. Closure of C2 additionally requires clean generated ISA/artifact inventories and reproducible hashes. Product controlled emission remains forbidden until after a named limited runtime release and its own RFC; the test-only carrier profile remains unimplemented and separately fenced.

## Dependency

Previous: `18_secure_nested_child_intent_owner_rfc.md`. Next: `20_positive_secure_runtime_execution_activation_plan.md`.
