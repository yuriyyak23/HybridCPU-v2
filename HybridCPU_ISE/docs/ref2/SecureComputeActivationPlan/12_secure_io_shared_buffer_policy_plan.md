# Secure IO Shared Buffer Policy Plan

## Phase Metadata

- File name: `12_secure_io_shared_buffer_policy_plan.md`
- Phase goal: keep SecureCompute I/O limited to explicit descriptor-owned shared buffers with typed grants.
- Status: partial policy class; shared-buffer canonicalization and device/IOMMU/DMA enforcement are open.
- Scope: shared-buffer descriptors, I/O owner materialization, direction, lifetime epoch, evidence class, typed grants and raw pointer denial.
- No-goals: no device pointer authority, no raw private pointer admission, no VMCALL authority and no backend execution proof.

## Current Baseline

`SecureIoDomainDescriptor` allows DMA only through explicit shared buffers. Its current-buffer lookup requires an explicit shared-buffer policy, a validated nonzero owner domain, a materialized policy epoch, a materialized descriptor, matching owner domain, current lifetime, allowed evidence class and a buffer grant matching the current epoch.

`SecureIoHypercallAdmissionPolicy` validates I/O owner materialization, memory policy, direction/range, shared-buffer binding, typed capability/argument grants and required publication-fence prerequisites. Successful I/O admission returns policy admission only: backend execution, completion publication and retire publication remain false.

## Authority Owner

The descriptor/policy class owns only admission classification. No device context, PASID/IOMMU mapping, stream, DMA submit or DMA completion owner is connected. Future authority must be split between a device-context owner and IOMMU mapping owner and carried in a one-shot typed DMA intent.

Accepted recommended design: canonical shared-buffer maps reject zero-length, overflow, duplicate and overlap and publish stable buffer IDs, digests and generations. `DeviceContextOwner`, `IommuMappingOwner`, `DmaSubmissionOwner` and `DmaCompletionOwner` are separate neutral owners bound by one-shot intent identity. The first separately released shared-buffer contour uses bounded copy-in/copy-out staging; direct shared host/device mapping, raw pointers and buffer IDs alone remain forbidden.

## What Can Be Implemented

- shared-buffer policy matrix;
- direction and range validation;
- owner, lifetime, evidence and buffer-grant epoch validation;
- typed capability and hypercall-argument grant validation;
- negative tests for raw private pointers and stale buffers.

## What Remains Denied/Future-Gated

- backend execution success;
- device-side side-effect success as proof of SecureCompute execution;
- raw guest pointer authority;
- VMX/VMCALL authority;
- completion/retire publication from admitted-denied paths.

## Forbidden Shortcuts

- shared-buffer descriptor as host pointer;
- buffer ID as capability by itself;
- I/O admission as backend success;
- completion or retire fence as backend, completion-publication or retire-publication authority;
- Lane6/Lane7 token as secure grant.

## Required RFC/ADR

No RFC/ADR for shared-buffer admission. Positive I/O backend execution requires owner-specific backend RFC/ADR.

## Code Anchors

- `SecureIoDomainDescriptor.cs`
- `SecureHypercallDescriptor.cs`
- `SecureIoHypercallAdmissionPolicy.cs`
- `SecureGrantAuthorityPolicy.cs`
- `SecureCompletionPublicationFence.cs`

## Documentation Anchors

- `SecureComputerefactoringNew/14_io_lane_boundary.md`
- `Documentation/Stream WhiteBook/`
- `Documentation/Virtualization WhiteBook/07_Memory_IO_Lanes.md`

## Required Tests

- missing I/O owner denied;
- missing shared-buffer descriptor denied;
- stale lifetime epoch denied;
- wrong owner domain denied;
- denied evidence class denied;
- stale shared-buffer grant denied;
- wrong direction denied;
- missing typed grant denied;
- raw private pointer denied;
- successful policy admission leaves backend/completion/retire authority false even when fences are present;
- Lane6/Lane7/Stream tokens cannot migrate as guest state or SecureCompute authority.

## Required Static/Source Scans

- `raw pointer.*allowed`
- `device pointer.*authority`
- `VMCALL.*I/O authority`
- `Lane6.*SecureCompute authority`
- `Lane7.*SecureCompute authority`
- `Stream.*SecureCompute authority`
- `AllowsSharedBuffer(` ID-only admission helper;
- propagation of `CanPublishCompletion` or `CanPublishRetire` into an allowed I/O result.

## Migration/Evidence Classification

Shared-buffer metadata may be policy state only when classified. Native tokens, backend bindings, device handles and host pointers are denied as migration authority.

## Completion/Retire Implications

I/O admission can require fences as prerequisites, but fences do not prove backend side effects and do not set completion or retire publication authority. Publication requires Phase 14 plus a separately proven positive backend owner.

## SecureCompute Activation Implications

I/O shared-buffer admission can be a prerequisite for a future backend path, not activation evidence.

## Exit Criteria

- explicit shared-buffer matrix is implemented with owner, lifetime, evidence and buffer-grant binding;
- raw pointer, stale epoch, wrong owner, denied evidence and missing typed-grant tests deny;
- successful admission is proven policy-only with backend/completion/retire flags false;
- source scans reject ID-only shared-buffer authority, fence-derived publication and VMX/VMCS/`VmxCaps` authority;
- Stream/Lane boundaries stay separate from SecureCompute authority.

Exit status: open. `TryFindSharedBuffer` is first-match and duplicate/overlapping buffers are not rejected; production DMA/IOMMU paths do not call the policy. Closure requires canonical maps, device/PASID/VT/direction/epoch/sequence binding and reset/rebind/replay tests.

## Dependency

Previous: `11_secure_memory_private_domain_policy_activation_plan.md`. Next: `13_secure_hypercall_backend_owner_rfc.md`.
