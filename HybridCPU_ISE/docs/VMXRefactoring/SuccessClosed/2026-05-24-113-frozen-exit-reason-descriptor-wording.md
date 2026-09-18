# Closed task: frozen exit reason descriptor wording

Date: 2026-05-24

Rule / basis:
- VMX/VMCS names remain allowed in frozen compatibility ABI names.
- Substrate-facing descriptions should not imply VMX-owned authority where descriptor/runtime ownership is intended.
- Lane6/Lane7/vector-stream ownership must remain outside VMX.

Changed:
- Updated `Core/VMX/Compatibility/FrozenAbi/VmcsFieldAliases/VmExitReason.cs` XML summaries only.
- Kept all enum names and numeric VM-exit ABI values unchanged.
- Reworded VMX-owned / VMCSv2-owned descriptions to descriptor/runtime compatibility-exit wording.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
