# Closed: fail-closed CompletionSidebandEnvelope

Date: 2026-05-24

## Source rule

Completion routing and completion evidence belong to the runtime substrate. Host-owned evidence and native transport metadata must not become guest-visible VMX/VMCS state.

## Completed change

- Replaced the empty `CompletionSidebandEnvelope` placeholder with a minimal completion transport envelope.
- Added completion payload, route id, sequence, host-owned evidence flag, and evidence visibility class.
- Added host-handling and guest-exposure predicates.
- Gated guest exposure through `EvidencePolicyDescriptor` so completion transport remains fail-closed by default.

## Validation

- `dotnet build HybridCPU_ISE.csproj --no-restore`
- Result: passed with existing project warnings, `0 Error(s)`.
