# Phase 12 - Compiler ISA Runtime No Emission Contract

## Goal

Keep compiler, ISA, decoder, encoder, examples, and no-emission surfaces aligned with the runtime authority model. Compatibility vocabulary may exist, but compiler/backend emission must not imply VMX backend, SecureCompute backend, stream/L7 expansion, or VMCS authority.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- VMX compatibility vocabulary is frozen frontend/projection vocabulary.
- Generated projection artifacts are compatibility artifacts, not runtime owners.
- SecureCompute whitebook states no decoder, encoder, ABI, register, bundle, capability-aware ISA, or production secure backend execution changes are currently authorized by SecureCompute.
- Stream whitebook states compiler/backend code may preserve and validate DSC1 descriptor sideband, but must not assume broad production lowering semantics.
- `CompilerVmxAuthority` is diagnostic/preflight vocabulary only: VMX opcodes are raw transport, root-policy gated, and not compiler helper emittable.
- `HybridCpuThreadCompilerContext`, `HybridCpuIrBuilder`, and `HybridCpuBundleLowerer` preserve Lane6/Lane7 descriptor sideband metadata only on their native carriers.
- Current repo has active compiler refactoring changes outside this plan; this phase does not modify them.

## Already Closed / Must Not Reopen

- Do not turn VMX no-emission tests into production backend emission.
- Do not let compiler lowering bypass runtime admission.
- Do not use generated schemas as runtime authority.
- Do not add capability-aware SecureCompute ISA or memory semantics through compiler changes.
- Do not use examples as proof of production runtime authority.

## Required Code/Doc Anchors

- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`
- `Documentation/SecureCompute WhiteBook/SecureCompute HybridCPU-v2 WhiteBook.md`
- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/*`
- `HybridCPU_Compiler/Legacy/VMX-2/Core/IR/Model/VmxCompilerAuthority.cs`
- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- `HybridCPU_Compiler/Core/IR/Construction/HybridCpuIrBuilder.cs`
- `HybridCPU_Compiler/Core/IR/Bundling/HybridCpuBundleLowerer.cs`
- `HybridCPU_Compiler/Core/IR/Model/CompilerBackendLoweringContract.cs`
- `HybridCPU_ISE.Tests/CompilerTests/DmaStreamComputeCompilerContractTests.cs`
- Existing compiler no-emission and backend lowering tests.
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxCompilerIsaRuntimeNoEmissionContractTests.cs`

## Work Items

- Document the no-emission rule for VMX backend authority and SecureCompute backend authority.
- Document compiler sideband preservation as metadata handling, not runtime authorization.
- Define required compiler checks for any future runtime owner path.
- Require examples to label projection/helper/model behavior without claiming authority.
- Keep this docs-only plan isolated from current unrelated compiler worktree changes.

## Closure Decision - ADR-VIRT-COMPILER-NOEMISSION-2026-06-05

Phase 12 is closed as compiler/ISA/runtime no-emission hardening only. The closure added a focused conformance fixture and documentation/static gates; it did not modify compiler production code, ISA encoding, decoder/encoder ABI, runtime execution, VMCS mutation, SecureCompute backend execution, completion publication, or retire publication.

| Surface | Current authority | Allowed now | Denied / forbidden |
| --- | --- | --- | --- |
| Public compiler facades | `IAppAsmFacade`, `IPlatformAsmFacade`, `IExpertBackendFacade`, `HybridCpuThreadCompilerContext` | ordinary app/platform helpers plus existing Lane6/Lane7 typed descriptor carriers | VMX/VMCS/SecureCompute helper APIs, backend-execution helpers, completion/retire publication helpers |
| VMX compiler vocabulary | `CompilerVmxAuthority` diagnostics and preflight | raw transport classification, target/root-policy diagnostics | compiler-helper emission, root-policy bypass, target-capability bypass |
| VMCS compiler sideband | `CompilerVmcsV2DescriptorSideband` | validation-only diagnostics | attachment to executable compiler instructions, VMREAD/VMWRITE/VMCALL sideband authority |
| Lane6 DSC sideband | `DmaStreamComputeDescriptor` metadata on native `DmaStreamCompute` carrier | descriptor preservation and validation before native carrier emission | fallback lowering, VMX authority, SecureCompute authority, backend publication |
| Lane7 L7-SDC sideband | `AcceleratorCommandDescriptor` metadata on native `ACCEL_SUBMIT` carrier | explicit accelerator intent plus descriptor/owner guard before native carrier emission | direct system-device command emission, runtime fallback promise, VMX/SecureCompute/publication authority |
| Compiler emission surface and NonVmx ISA | API, IR construction, bundling, NonVmx instruction sources | no-emission proof and native non-VMX contours | VMX activation/mutation opcodes, SecureCompute authority imports, VMCS manager shortcuts |

`VmxCompilerIsaRuntimeNoEmissionContractTests` is the focused closure fixture for this phase. It proves public facades expose no VMX/VMCS/SecureCompute helper authority, VMX diagnostic authority remains raw transport only, VMCS sidebands are validation-only, preflight cannot bypass root policy or target capabilities, and compiler emission surfaces plus NonVmx ISA sources do not emit virtualization activation or SecureCompute authority.

## Explicit Non-Goals

- Do not change compiler code.
- Do not change ISA encoding.
- Do not add new opcodes.
- Do not add capability operands, tags, grant registers, or bundle metadata.
- Do not declare examples as conformance authority.

## Done Criteria

- Compiler/ISA boundaries are documented as downstream of neutral runtime owners.
- SecureCompute no-ISA-change constraints are restated.
- Stream/L7 sideband handling is classified correctly.
- Future compiler work requires an owner/admission/evidence/publication proof chain first.
- `VmxCompilerIsaRuntimeNoEmissionContractTests` passes and proves Phase 12 did not open compiler/backend production authority.

## Required Tests / Static Checks

- Existing compiler no-emission tests.
- Existing DmaStreamCompute compiler contract tests.
- `FullyQualifiedName~VmxCompilerIsaRuntimeNoEmissionContractTests`
- Static scans for VMX backend emission claims.
- Static scans for SecureCompute capability-aware ISA imports.

## Residual Risk

Compiler work is currently active in the repository. This phase must avoid touching those files and must not overwrite unrelated user changes.

## External Audit Risk Update

Compiler, ISA, examples, and no-emission tests must not become a back door to runtime authority. Passing compiler tests or adding examples cannot open VMX backend execution, SecureCompute backend execution, VMCS mutation, or capability-aware memory semantics without the full runtime owner/admission/evidence/publication chain.

## Next Phase Dependency

Phase 13 uses this no-emission contract to define conformance, golden artifacts, and static gates.
