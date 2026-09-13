# Code Test Document Map

| Area | Production anchors | Production reachability | Evidence class | ActivationPlan anchors |
| --- | --- | --- | --- | --- |
| root/no-effect | `SecureComputeDomainDescriptor.cs`, `DomainRuntimeContext.cs` | direct construction/two carriers; registry absent | policy/unit tests | `04`, `05`, `24` |
| generic admission | `RuntimeBoundaryAdmissionService.cs`, `SecureDomainAdmissionPolicy.cs` | VMX callers use default ordinary class; CPU path not proven | direct policy tests | `06`, `24` |
| SafetyVerifier certificate | no SecureCompute certificate anchor | absent | open C0 | `06`, `24` |
| grants | `SecureGrantAuthorityPolicy.cs` | validation only; mint/revoke ledger absent | direct policy tests | `07`, `24` |
| measurement/evidence | measurement/publication policy types | no single production publisher found | direct policy tests | `08`, `16`, `24` |
| privileged projection | privileged-state policy/projection services | named VMREAD path, read-only | executable narrow-path tests | `09`, `10`, `17` |
| memory | `SecureMemoryDomainDescriptor.cs`, `SecureMemoryAdmissionPolicy.cs` | no production load/store/DMA caller found | direct policy tests; map open | `11`, `24` |
| secure I/O | `SecureIoDomainDescriptor.cs`, `SecureIoHypercallAdmissionPolicy.cs` | no device/IOMMU effect caller found | direct policy tests; map open | `12`, `24` |
| hypercall contract | contract and ABI registry | proof-only; no executor/result owner | direct policy tests | `13`, `24` |
| completion/retire | `SecureCompletionRetirePublicationAuthorityPolicy.cs` | no production caller found | negative classifier tests | `14`, `24` |
| migration/output | migration/checkpoint/manifest policies | no protocol owner; unknown classes default allow | classifier tests | `15`, `24` |
| compiler | compiler contour/lowering gate | no-emission enforced | executable negative tests; artifact hashes open | `19`, `24` |
| release | Phase 20/21/22 classifiers | none creates authority | negative classifier plus source/doc guards | `20`, `21`, `22`, `24` |

Paths are relative to the repository areas:

- production: `HybridCPU_ISE/CloseToHSL/Core/`;
- tests: `HybridCPU_ISE.Tests/`;
- plan: `HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`.

The numeric plan labels in this table are navigation aids. Full filenames and current status are authoritative in `00_securecompute_activation_refactoring_index.md`; the current revalidation record is `24_audit_revalidation_and_dependency_order.md`.
