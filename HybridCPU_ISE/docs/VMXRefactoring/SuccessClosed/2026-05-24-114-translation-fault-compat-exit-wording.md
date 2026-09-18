# Closed task: translation fault compatibility-exit wording

Date: 2026-05-24

Rule / basis:
- Memory-domain descriptors and translation results are substrate-owned.
- VM-exit / NPT vocabulary may remain where it is compatibility-visible, but substrate summaries should not make VMX the authority.
- Descriptor sideband and compatibility projection are the intended transport boundary.

Changed:
- Updated `Core/VMX/Substrate/Memory/Translation/TranslationViolationInfo.cs` summary from VM-exit state ownership to compatibility-exit projection wording.
- Updated `Core/VMX/Substrate/Memory/Translation/NestedTranslationResult.cs` summary from VMX nested translation / VM-exit population wording to second-stage translation / compatibility projection wording.
- Kept all record fields, statuses, factory names, and behavior unchanged.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
