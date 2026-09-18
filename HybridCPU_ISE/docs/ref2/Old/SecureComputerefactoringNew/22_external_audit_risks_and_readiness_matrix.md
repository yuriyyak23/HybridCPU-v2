# Phase 22 - External Audit Risks And Readiness Matrix

## Goal

Import the external architecture audit into the SecureCompute plan as explicit risk, blocker and readiness-matrix guidance. This phase does not open implementation and does not change any closure class from denied, proof-only, admitted-denied, design-fence or future into implemented.

## Current Code Baseline

The external audit references a neighboring virtualization corpus and may use repository or GitHub paths. For this SecureCompute plan, paths are interpreted repo-relative from `HybridCPU_ISE/` when they point into the ISE project. The audit's relevant local anchors are:

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Generated/VmcsProjection/VmcsFieldProjectionSchema.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/VmcsRead/VmcsReadOnlyValueProjectionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`

## Already Closed / Must Not Reopen

- This plan remains a readiness/refactoring corpus, not an activation work order.
- SecureCompute is not activated and is not product-ready.
- VMX/VMCS/VmxCaps remain non-authoritative.
- VMCALL remains admitted-denied/fail-closed before backend success.
- VMCS writes and compatibility-control value projection remain denied unless a separate neutral owner/RFC exists.
- Nested execution remains design-fenced.
- Stream/Lane6/L7 bounded contours are not virtualization or SecureCompute authority.

## Required Code/Doc Anchors

- `HybridCPU_ISE/docs/ref2/Risks/Архитектурный-ревизор SecureCompute HybridCPU-v2.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/18_vmx_compatibility_deny_projection.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/15_hypercall_and_trap_policy.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/16_completion_and_retire_publication.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/20_positive_runtime_execution_rfc_gate.md`
- `HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew/21_release_gate_and_activation_checklist.md`

## Work Items

Audit-derived readiness matrix:

| Priority | Risk / Blocker | Required Plan Handling |
|---|---|---|
| P0 | SecureCompute activation or product-ready wording | Keep blocked until Phase 20 and Phase 21 have real code/test proof for every activation prerequisite. |
| P0 | VMCALL or secure hypercall backend execution | Keep admitted-denied/fail-closed; do not use VMCALL as first positive path. |
| P0 | VMX/VMCS/VmxCaps authority | Keep all activation, grant, materialization, checkpoint, migration, publication and ownership denied. |
| P0 | Mutable VMCS state or legacy VMX authority return | Treat `VmcsManager`, active VMCS pointer, VMCS field store or VMX-owned runtime manager as architectural regressions. |
| P0 | Schema entry read as current VMREAD value | Require neutral owner, value source, policy, migration class and tests; schema alone is insufficient. |
| P1 | `GuestCr0`/`GuestCr4` opening | Require neutral privileged execution-state owner RFC/ADR, semantics, visibility, migration and negative tests. |
| P1 | Compatibility-control fields | Keep denied until a neutral frozen control-bit value contract exists. |
| P1 | Route/fence classes read as publication permission | Require runtime admission, neutral trap/backend authorization, completion publication and retire publication decisions. |
| P1 | Stream/Lane6/L7 overclaim | Keep bounded contours separate from SecureCompute and virtualization authority. |
| P1 | Compiler emission creep | Keep unrelated compiler work and future ISA/tag/grant-register work outside SecureCompute activation. |
| P2 | Path hygiene | Normalize executable references to repo-relative paths from `HybridCPU_ISE/`, while preserving factual directory spellings. |
| P2 | Forbidden-name scan false positives | Allow forbidden tokens only in denied, non-goal, blocker or static-check vocabulary. |

- Add the audit blockers to Phase 21 release gate:
  - no SecureCompute activation or production-ready claim;
  - no VMCALL backend execution;
  - no VMX/VMCS/VmxCaps authority;
  - no mutable VMCS state or legacy VMX authority return;
  - schema entry is not a readable value;
  - route/fence class existence is not publication permission;
  - Stream/Lane6/L7 bounded execution is not SecureCompute authority.
- Add VMREAD field-by-field readiness requirements:
  - currently projected slices must be named separately from denied slices;
  - `GuestCr0` and `GuestCr4` require a neutral privileged execution-state owner RFC before opening;
  - compatibility-control fields require a neutral frozen control-bit value contract before opening;
  - all writes remain denied.
- Add path hygiene:
  - `CloseToHSL/...` means `HybridCPU_ISE/CloseToHSL/...`;
  - `NonRTL/...` means `HybridCPU_ISE/NonRTL/...`;
  - `docs/ref2/...` means `HybridCPU_ISE/docs/ref2/...`;
  - `Documentation/...` stays repository-root relative.

## Explicit Non-Goals

- No implementation based solely on this audit.
- No hypercall backend implementation.
- No GuestCr0/GuestCr4 opening.
- No VMCS write owner.
- No compatibility-control value projection.
- No renaming of existing directories based on spelling preference.

## Done Criteria

- The index and release gate reference this external audit.
- The readiness matrix lists P0/P1/P2 audit risks.
- VMREAD schema/value-source separation is explicit.
- Stream/Lane6/L7 overclaim risk is explicit.
- Path normalization rules are documented.

## Required Tests / Static Checks

- `rg -n "GuestCr0|GuestCr4|VmcsFieldProjectionSchema|VmcsReadOnlyValueProjectionService|CompatibilityControlDescriptor|TrapCompletionRoute|TrapCompletionPublicationFence|DmaStreamComputeDescriptorParser|ExecutionEnabled" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `rg -n "production-ready SecureCompute|feature-complete SecureCompute|VMX activates SecureCompute|VMX owns SecureCompute|VmxCaps grants SecureCompute|secure VMCS|CHERI ISA|tagged memory|capability-aware LOAD|capability-aware STORE|capability-aware FETCH" HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew --glob "*.md"`
- `git diff --check -- HybridCPU_ISE/docs/ref2/SecureComputerefactoringNew`

## Residual Risk

The audit was written against a related corpus and contains path/context references that may not target this SecureCompute directory directly. Treat it as an external risk import, not as source-of-truth replacement for live code, current WhiteBooks or this plan's phase order.

## Next Phase Dependency

No implementation opens from this phase. The safest next work order remains a narrow RFC/ADR, with the audit recommending `GuestCr0`/`GuestCr4` neutral privileged execution-state owner as a better first candidate than hypercall backend execution.
