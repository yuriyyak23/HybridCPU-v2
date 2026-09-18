# Closed task: compatibility frontend projection authority wording

Date: 2026-05-24

Rule / basis:
- VMX must stay a compatibility frontend, not the architectural authority.
- Runtime/domain descriptors are authoritative; compatibility projections are not.
- Frozen compatibility ABI names may remain stable when behavior is unchanged.

Changed:
- Updated `Core/VMX/Compatibility/Frontend/Handlers/VmxCompatFrontend.cs` denial reason for direct VMCS authority attempts.
- Kept `DirectVmcsAuthorityDenied` enum name and admission behavior unchanged.
- Reworded the diagnostic to deny making compatibility projection state authoritative.

Verified:
- Ran `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

Build result:
- Succeeded.
- 0 errors.
- 54 pre-existing warnings.
