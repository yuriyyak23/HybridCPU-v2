# Closed: minimal CompletionRecord authority

Date: 2026-05-24

## Source rule

Exit/completion state should be a generic runtime completion record. VMX exit reason, exit qualification, guest physical address, and EPT qualification are compatibility projections only.

## Completed change

- Replaced the empty `CompletionRecord` placeholder with a minimal substrate-owned completion record.
- Added `CompletionRecordClass` to distinguish trap, event, memory, DMA, vector-stream, lane, security, and compatibility completion sources.
- Added generic reason, qualification, fault address, and auxiliary fault fields.
- Added a compatibility factory for VMX-facing exit aliases without making VMCS state authoritative.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
