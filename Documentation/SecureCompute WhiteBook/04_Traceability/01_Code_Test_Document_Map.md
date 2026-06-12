# Code Test Document Map

| Area | Production anchors | Test anchors | ActivationPlan anchors |
| --- | --- | --- | --- |
| root/no-effect | `SecureComputeDomainDescriptor.cs` | `SecureComputeDomainDescriptorNoEffectTests.cs` | `04`, `05` |
| Stage B | `RuntimeBoundaryAdmissionService.cs`, `SecureDomainAdmissionPolicy.cs` | `SecureRuntimeBoundaryAdmissionHookTests.cs` | `06` |
| grants | `SecureGrantAuthorityPolicy.cs`, `SecureAuthorityBounds.cs`, `SecureRevocationEpoch.cs` | `SecureAuthorityDisciplineTests.cs` | `07` |
| measurement/evidence | `DomainMeasurementDescriptor.cs`, `SecureMeasurementAdmissionPolicy.cs`, `SecureEvidencePublicationPolicy.cs` | `SecureMeasurementEvidencePolicyTests.cs`, `SecureEvidencePublicationPolicyTests.cs` | `08` |
| privileged owner | `PrivilegedExecutionStateDescriptor.cs`, `PrivilegedExecutionStateOwnerPolicy.cs` | `PrivilegedExecutionStateOwnerPolicyTests.cs` | `09` |
| read-only projection | `PrivilegedExecutionStateProjectionService.cs`, `VmcsReadOnlyValueProjectionService.cs` | `GuestCr0Cr4ReadOnlyProjectionTests.cs` | `10` |
| memory | `SecureMemoryDomainDescriptor.cs`, `SecureMemoryAdmissionPolicy.cs` | `SecureMemoryDomainPolicyTests.cs` | `11` |
| secure I/O | `SecureIoDomainDescriptor.cs`, `SecureIoHypercallAdmissionPolicy.cs` | `SecureIoHypercallPolicyTests.cs` | `12` |
| migration | `SecureMigrationAdmissionPolicy.cs`, `SecureCheckpointPayloadPolicy.cs` | `SecureMigrationPolicyTests.cs` | `15`, with current baseline recorded in `01`/`21` |
| VMX boundary | `SecureComputeCompatibilityBoundaryMatrixPolicy.cs` and deny/projection fences | `SecureComputeVmx*Tests.cs`, `GuestCr0Cr4ReadOnlyProjectionTests.cs` | `10`, `17` |
| release proof | source/doc guards | `SecureComputePhase10ReleaseGateTests.cs` | `21`, `22`, `23` |

Paths are relative to the repository areas:

- production: `HybridCPU_ISE/CloseToRTL/Core/`;
- tests: `HybridCPU_ISE.Tests/`;
- plan: `HybridCPU_ISE/docs/ref2/SecureComputeActivationPlan/`.

The numeric plan labels in this table are navigation aids. Full filenames and current status are authoritative in `00_securecompute_activation_refactoring_index.md`.
