# Source References And Check Commands

This file lists the source documents, code anchors, and useful check commands for maintaining the Virtualization WhiteBook.

## Required Source Documents

- `\HybridCPU ISE\HybridCPU_ISE\docs\ref2\VirtualizationActivationPlan\` - development source of truth for current/future classification and phase sequencing.
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\2026-05-24-vmx-current-model-completion-audit.md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\audit3.md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\audit4.md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\audit5.md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\SuccessClosed\`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\deep-research-report (6).md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\ОСНОВЫ и ПРАВИЛА VMX.md`
- `\HybridCPU ISE\HybridCPU_ISE\docs\VMXRefactoring\Оценка рефакторинга VMX security-centric.md`

## Runtime Code Anchors

- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Services\RuntimeBoundaryAdmissionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Domains\Services\DomainRuntimeOperation.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Domains\Descriptors\ExecutionDomain\ExecutionDomainDescriptor.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Domains\Descriptors\ExecutionDomain\ExecutionDomainReadOnlyStateView.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Domains\Descriptors\MemoryDomain\MemoryDomainDescriptor.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Memory\Translation\MemoryDomainReadOnlyTranslationView.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Events\Traps\TrapRequest.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Events\Traps\TrapPolicyBitmap.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Events\Traps\NeutralTrapResult.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Events\Hypercalls\HypercallBackendAdmissionPolicy.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Completion\Routing\TrapCompletionRoutePolicy.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Completion\Records\TrapCompletionPublicationFence.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Completion\Records\CompletionRecord.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Domains\SecureCompute\**`

## Compatibility Code Anchors

- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Handlers\VmxCompatibilityAdmissionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Handlers\VmxCompatibilityAdmissionService.Traps.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Projection\VmcsRead\VmcsReadOnlyValueProjectionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Projection\Events\VmxTrapProjectionMapper.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Projection\Events\TrapDecision.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Projection\Completion\CompletionProjectionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Projection\Completion\CompletionRecordCompatibilityProjection.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend\Retire\VmxRetireModel.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Generated\AliasMaps\CompatAliasMap.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Generated\VmcsProjection\VmcsFieldProjectionSchema.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\SecureCompute\Compatibility\Projection\SecureComputeCompatibilityBoundaryMatrixPolicy.cs`

## Test Anchors

- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxProjectionSchemaAndQuarantineTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\ActiveVmxCompatibilityConformanceTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\RuntimeBoundaryAdmissionTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxFirstAdmittedCompatibilityPathTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxGeneratedReadOnlyVmReadValueProjectionTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxMemoryOwnedVmReadValueProjectionTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxExecutionOwnedVmReadValueProjectionTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxControlLikeVmReadDenialTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxDescriptorReadinessPolicyAuditTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxMigrationEvidenceRecomputedCompatibilityFieldTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxNeutralTrapResultSplitTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxAdmittedDeniedVmCallTrapPathTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxTrapProjectionPublicationFenceTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxTrapCompletionRouteOwnerTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxTrapCompletionRouteRetirePublicationHardeningTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxHypercallBackendAdmissionPolicyTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VirtualizationActivationPlanAuditGuardTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase8BoundaryMatrixTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase9NestedFenceTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase10ReleaseGateTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\SecureComputeRefactoring\**`

## Useful Static Checks

```powershell
rg -n "NeutralTrapResult|TrapRequest|TrapPolicyBitmap|TrapCompletionPublicationFence|TrapCompletionRouteService|HypercallBackendAdmissionService|RuntimeBoundaryAdmissionService" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg -n "RuntimeOwnedCompletionPublication|RuntimeOwnedPublication|CompletionPublicationAuthorizedOnly|IsFullyRetirable|CompletionPublishedRetireDenied|TrapCompletionMigrationClass" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Runtime\Completion" `
  "\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring"
```

```powershell
rg -n "TrapCompletionRouteDescriptor\.(RuntimeOwnedCompletionPublication|RuntimeOwnedPublication)" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility\Frontend"
```

The second scan must have no matches until an accepted owner-specific RFC/ADR and the corresponding completion/retire implementation gates exist.

```powershell
rg -n "VmcsReadOnlyValueProjectionService|ExecutionDomainReadOnlyStateView|MemoryDomainReadOnlyTranslationView|PrivilegedExecutionStateProjectionDenied|HostAddressSpaceOwnerMissing|HostExecutionStateOwnerMissing|CompatibilityControlValueProjectionDenied" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg -n "VmExitReason|TrapDecision|VmxTrapProjectionMapper|CompletionProjectionService|VmxRetireEffect" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization\Compatibility" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg --files "\HybridCPU ISE\HybridCPU_ISE" `
  --glob "VmxExecutionUnit.cs" `
  --glob "VmcsManager.cs" `
  --glob "IVmcsManager.cs" `
  --glob "!CloseToRTL/**"
```

```powershell
rg -n "VmxExecutionUnit|VmcsManager|IVmcsManager|VmcsManagerAdapter|VmxRuntimeManager|VmcsProjectionRuntimeManager|VmcsV2RuntimeManager|ReadFieldValue\(|WriteFieldValue\(|HardwareWrite\(|DirectWrite\(" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToRTL\Core\Virtualization" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg -n "Virtualization\\Substrate|Virtualization/Substrate" `
  "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj"
```

## Useful Test Filters

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxFirstAdmittedCompatibilityPathTests|FullyQualifiedName~RuntimeBoundaryAdmissionTests|FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests|FullyQualifiedName~VmxCompatibilityProjectionInventoryTests"
```

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxGeneratedReadOnlyVmReadValueProjectionTests|FullyQualifiedName~VmxMemoryOwnedVmReadValueProjectionTests|FullyQualifiedName~VmxExecutionOwnedVmReadValueProjectionTests|FullyQualifiedName~VmxControlLikeVmReadDenialTests"
```

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore -p:BuildProjectReferences=false --filter "FullyQualifiedName~VmxTrapCompletionRouteOwnerTests|FullyQualifiedName~VmxTrapCompletionRouteRetirePublicationHardeningTests|FullyQualifiedName~VmxHypercallBackendAdmissionPolicyTests|FullyQualifiedName~VmxAdmittedDeniedVmCallTrapPathTests|FullyQualifiedName~VmxTrapProjectionPublicationFenceTests|FullyQualifiedName~VirtualizationActivationPlanAuditGuardTests"
```

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxDescriptorReadinessPolicyAuditTests|FullyQualifiedName~VmxMigrationEvidenceRecomputedCompatibilityFieldTests"
```

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~SecureComputeVmxPhase8BoundaryMatrixTests|FullyQualifiedName~SecureComputeVmxPhase9NestedFenceTests|FullyQualifiedName~SecureComputeVmxPhase10ReleaseGateTests"
```

## Broad VMX Caveat

Prefer:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Vmx&FullyQualifiedName!~NonVmx"
```

Raw `FullyQualifiedName~Vmx` can also match `NonVmx`; failures isolated to known NonVmx instruction inventory counters should be classified separately.
