# Phase 14 - Compiler No Emission To Controlled Emission Gate

Status: no-emission preserved. Controlled emission is future-gated.

## 2026-06-11 Audit Contract

- File name: `14_compiler_no_emission_to_controlled_emission_gate.md`.
- Purpose: prevent compiler metadata or examples from becoming backend emission/authority.
- Status: no-emission preserved; controlled emission is `future-gated`.
- Scope: compiler/ISA/runtime boundary, opcode metadata, generated artifacts, future controlled-emission RFC template.
- No-goals: no compiler-generated virtualization backend, no VMWRITE emission, no SecureCompute VMX helper activation, no lane/nested emission.
- Code anchors: `HybridCPU_Compiler/**`, `CloseToHSL/Core/Virtualization/Compatibility/**`, `VmxCompilerIsaRuntimeNoEmissionContractTests.cs`.
- Authority owner: neutral runtime owner at execution time; compiler metadata is intent/ABI only.
- Required RFC/ADR: mandatory before controlled emission, tied to runtime owner RFC/ADR, with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: compiler no-emission remains green; backend emission without owner is `не доказано` and `должно оставаться denied`.
- Tests/static scans: no `Emit.*VMCALL`, no `Emit.*VMWRITE`, no `RuntimeOwnedPublication`, no `HypercallBackend` emission path without RFC marker.
- Risks: decode support, examples, or goldens being treated as execution support.
- Next-gate dependency: accepted runtime owner RFC/ADR plus separate controlled-emission RFC.

## Phase Goal

Keep the compiler/ISA/runtime boundary from becoming backend emission before neutral runtime owners, admission, evidence, migration, completion, and retire gates exist.

## Historical Baseline (2026-06-11)

The current closure corpus states that compiler/no-emission surfaces are documentation/readiness/test/static-gate work only. Compiler classification, opcode metadata, examples, and generated artifacts do not authorize backend execution.

## ISE-COMPILER-NOEMISSION-GATE-14 - Closure Record

Closure date: 2026-06-18.

State: closed `DENIED/FUTURE-GATED BASELINE / NO-COMPILER-EMISSION-AUTHORITY`.

This closure records the current compiler boundary only. It does not approve a controlled-emission RFC/ADR, does not make compiler metadata an execution authority, does not add VMX/SecureCompute/lane/stream emission authority, and does not connect compiler output to backend execution, completion publication, or retire publication.

| Surface | Current result | Owner/value source | Evidence class | Migration class | Denial or boundary reason |
| --- | --- | --- | --- | --- | --- |
| compiler VMX opcode metadata | diagnostic/raw transport vocabulary only | frozen VMX compatibility ABI | metadata evidence | not migration authority | `CompilerHelperEmittable == false`; opcode visibility is not backend execution |
| VMCS descriptor sideband | validation-only sideband | generated compatibility descriptors | descriptor validation evidence | not checkpoint authority | `CanAttachToExecutableCompilerInstruction == false`; sideband cannot lower into VMREAD/VMWRITE mutation |
| virtualization no-emission regression gate | allows generated compatibility projection only | compatibility frontend projection owner | regression-gate evidence | not migration authority | direct substrate emission, host-owned evidence, native lane token, and unvalidated descriptor emission are denied |
| virtualization lowering boundary | denied unless no-emission gate, descriptor, capability, runtime, and intent gates all pass | future neutral runtime owner | lowering-readiness evidence | path-specific and absent | `NoEmissionDenied` and `DirectHandlerEmissionDenied` prevent direct VMX handler emission |
| VMX compatibility decode boundary | decode requires no-emission validation | frozen VMX frontend | decode-readiness evidence | not execution authority | `NoEmissionValidationDenied` proves decode vocabulary is not compiler emission authority |
| SecureCompute controlled-emission gate | no-emission preserved or denied/future-gated | SecureCompute runtime owner plus separate compiler RFC | proof/denial evidence | not checkpoint authority | `CompilerEmissionAuthorized: false`; missing owner/RFC/release/backend execution denies controlled emission |
| Lane6/Lane7/Stream compiler metadata | non-authoritative for virtualization | lane/stream runtime owners only | metadata/readiness evidence | not VMCS or SecureCompute migration authority | helper, token, telemetry, replay, or descriptor metadata cannot become VMX/SecureCompute passthrough |

Closure invariants:

- Compiler metadata, opcode classification, examples, goldens, and generated artifacts are not runtime authority.
- `CompilerHelperEmittable == false` remains required for VMX opcodes in the compiler-facing authority inventory.
- VMCS descriptor sidebands remain validation-only and cannot attach to executable compiler instructions.
- `VirtualizationNoEmissionRegressionGate` cannot grant direct substrate, host-evidence, native lane-token, or unvalidated descriptor emission.
- `VirtualizationLoweringBoundary` cannot emit direct VMX handler calls.
- `VmxCompatDecodeBoundary` requires no-emission validation; decode success is not backend execution.
- `SecureComputeControlledEmissionGatePolicy` keeps `CompilerEmissionAuthorized: false` for every result.
- Any future controlled compiler emission requires a separate compiler RFC/ADR tied to an accepted runtime owner RFC/ADR, exact operation, capability policy, evidence class, migration class, completion policy, retire policy, denial reasons, and negative tests.

## Owner Of Authority

Compiler metadata can carry intent and ABI shape, but runtime authority belongs to neutral runtime owners. Emission is not authority and cannot bypass Stage B/runtime admission.

## What Can Be Implemented

- Static tests that compiler sources do not emit active VMX backend behavior.
- Controlled emission RFC template for future use.
- Metadata requirements for a future owner-approved path.
- Negative tests that examples/goldens cannot be runtime authority.

## What Remains Denied/Future-Gated

- Compiler-generated active virtualization backend emission.
- VMWRITE emission.
- SecureCompute activation through compiler VMX helpers.
- Lane passthrough emission for virtualization.
- Nested emission.

## Forbidden Shortcuts

- Treating decode support as execution support.
- Treating example programs as production authority.
- Creating helper methods that emit backend VMCALL success path before RFC gates.
- Lowering VMX-compatible opcodes directly into neutral state mutation.

## Required RFC/ADR

Required before any controlled emission. It must reference the owner-specific runtime RFC/ADR and prove that emitted metadata cannot bypass runtime admission.

## Code Anchors

- `HybridCPU_Compiler/**`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/**`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCompilerIsaRuntimeNoEmissionContractTests.cs`
- Compiler tests named in `Documentation/Virtualization WhiteBook/19_Source_References_And_Check_Commands.md`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/12_compiler_isa_runtime_no_emission_contract.md`
- `Documentation/Virtualization WhiteBook/13_Compiler_ISA_Runtime_Contract.md`
- `Documentation/Virtualization WhiteBook/14_Conformance_Golden_Artifacts.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/09_Compiler_Emission_Path.md`

## Required Tests

- No compiler helper emits active VMCALL backend success path.
- No VMWRITE emission.
- No SecureCompute VMX activation emission.
- No lane/stream passthrough emission for virtualization.
- Future controlled emission requires owner RFC marker and metadata tests.

## Required Static/Source Scans

```powershell
rg -n "Emit.*VMCALL|Emit.*VMWRITE|Compile.*VMX|SecureCompute.*Emit|RuntimeOwnedPublication|HypercallBackend" HybridCPU_Compiler HybridCPU_ISE/CloseToHSL/Core/Virtualization
```

Matches must be denied, metadata-only, or tied to a future accepted RFC.

## Migration/Evidence Classification

Compiler artifacts and golden examples are not migration evidence or runtime authority. Any future emission must carry metadata for runtime admission, evidence class, and migration class, but the compiler must not decide them as authority.

## Completion/Retire Implications

Compiler emission cannot publish completion or retire. It may only produce an operation that later passes runtime gates.

## Exit Criteria

- No-emission baseline remains green.
- Controlled emission requirements are explicit.
- No compiler path can activate runtime virtualization by itself.

## Dependency On Previous/Next Phase

Depends on Phases 02 and 03. Controlled emission is not required for the first manual/runtime VMCALL activation path.
