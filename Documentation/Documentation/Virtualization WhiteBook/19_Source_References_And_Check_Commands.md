# Source References And Check Commands

PR-I anchors: `DomainHypercallDrainLifecycleOwner.cs`, the authoritative live
registry methods in SafetyVerifier/executor/completion/retire owners,
`VmxPrIDrainRestoreDeterminismTests.cs`, and diagnostic
`pri-drain-restore-determinism`. They prove exact DrainOnly E7 and no-state trace
determinism only; they do not prove compiler or release authority.

PR-G anchors: `DomainHypercallCompletionOwner.cs`,
`TrapCompletionPublicationFence.EvaluateOwnerPolicy`, the optional neutral owner
binding in `DomainHypercallCanonicalComposition.cs`,
`VmxPrGCompletionOwnerE5Tests.cs`, and diagnostic
`prg-atomic-completion-e5`. These prove completion/E5 only; scans must continue
to show no compatibility caller and no positive VMX retire.

This file lists the source documents, code anchors, and useful check commands for maintaining the Virtualization WhiteBook.

## Exact Compiler Boundary Anchors

- `\HybridCPU ISE\HybridCPU_Compiler\Core\IR\Model\CompilerExactProbeEmissionDecisionV1.cs`
- `\HybridCPU ISE\HybridCPU_Compiler\Core\IR\Construction\CompilerExactProbeEmissionLowerer.cs`
- `\HybridCPU ISE\HybridCPU_Compiler\Core\IR\Construction\CompilerVirtualizationIngressValidator.cs`
- `\HybridCPU ISE\HybridCPU_Compiler\Core\IR\Construction\HybridCpuIrBuilder.cs`
- `\HybridCPU ISE\HybridCPU_Compiler\Core\IR\Model\IrInstruction.cs`
- `\HybridCPU ISE\HybridCPU_Compiler\API\Threading\ThreadCompilerContext.ExactProbe.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxExactProbeCompilerEmissionGateTests.cs`

Focused check:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" -c Debug --filter "FullyQualifiedName~VmxExactProbeCompilerEmissionGateTests|FullyQualifiedName~VmxCompilerIsaRuntimeNoEmissionContractTests"
```

Required source-scan result: the compiler contains no direct reference to `SafetyVerifier`, runtime admission, `RuntimeCapabilityGrantOwner`, `VirtualizationRestoreGenerationOwner`, `DomainHypercallRuntimeExecutor`, `DomainHypercallCompletionOwner` or `DomainHypercallRetireOwner`; runtime sources contain no compiler decision, intent, legality facts, metadata, plan or binding consumer.

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

- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Services\RuntimeBoundaryAdmissionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Domains\Services\DomainRuntimeOperation.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Domains\Descriptors\ExecutionDomain\ExecutionDomainDescriptor.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Domains\Descriptors\ExecutionDomain\ExecutionDomainReadOnlyStateView.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Domains\Descriptors\MemoryDomain\MemoryDomainDescriptor.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Memory\Translation\MemoryDomainReadOnlyTranslationView.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Traps\TrapRequest.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Traps\TrapPolicyBitmap.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Traps\NeutralTrapResult.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\HypercallBackendAdmissionPolicy.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Completion\Routing\TrapCompletionRoutePolicy.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Completion\Records\TrapCompletionPublicationFence.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Completion\Records\CompletionRecord.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Domains\SecureCompute\**`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Governance\Virtualization\VirtualizationDecisionContractsV2.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Governance\Virtualization\VirtualizationDecisionCanonicalEncoderV2.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Governance\Virtualization\VirtualizationDecisionValidatorV2.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Capabilities\Grants\RuntimeCapabilityGrantLease.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Pipeline\Safety\SafetyVerifier.VirtualizationOperationAdmission.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxPrDProductionE2AdmissionTests.cs`
- `\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PrdE2AdmissionFaultOnlyScenario.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\Governance\Phase38VirtualizationDecisionSpecV2.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\Governance\Phase38VirtualizationDecisionAcceptanceV2.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\Governance\HypercallRuntimeOwnerRegistry.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\Governance\Phase38AcceptedVirtualizationDecisionRegistry.g.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\Governance\VirtualizationOperationOwnerSnapshot.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls\VirtualizationOperandSnapshot.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Capabilities\Governance\RuntimeCapabilityIds.Virtualization.cs`

## Compatibility Code Anchors

- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Handlers\VmxCompatibilityAdmissionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Handlers\VmxCompatibilityAdmissionService.Traps.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Projection\VmcsRead\VmcsReadOnlyValueProjectionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Projection\Events\VmxTrapProjectionMapper.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Projection\Events\TrapDecision.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Projection\Completion\CompletionProjectionService.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Projection\Completion\CompletionRecordCompatibilityProjection.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend\Retire\VmxRetireModel.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Generated\AliasMaps\CompatAliasMap.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Generated\VmcsProjection\VmcsFieldProjectionSchema.cs`
- `\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\SecureCompute\Compatibility\Projection\SecureComputeCompatibilityBoundaryMatrixPolicy.cs`

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
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxD2V2GovernanceNegativeSubstrateTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxD2V2MaterializedSpecTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxD2V2MaterializedAcceptanceTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxPrCExecutionOnlyDomainLegalityTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxPrCOwnerPolicySnapshotTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\VmxPrCCanonicalOperandSnapshotTests.cs`
- `\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PraD2GovernanceNegativeScenario.cs`
- `\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PrbD2AttributableMaterializationScenario.cs`
- `\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PrcO1OperandFaultOnlyScenario.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase8BoundaryMatrixTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase9NestedFenceTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring\SecureComputeVmxPhase10ReleaseGateTests.cs`
- `\HybridCPU ISE\HybridCPU_ISE.Tests\SecureComputeRefactoring\**`

## Useful Static Checks

PR-A governance checks:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxD2V2GovernanceNegativeSubstrateTests|FullyQualifiedName~VirtualizationActivationPlanAuditGuardTests"
dotnet run --project "\HybridCPU ISE\VirtualizationDiagnosticsConsole" -- pra-d2-governance-negative --iterations 50
rg -n "BackendExecutionAuthorized: true|HypercallBackendAdmissionDecision\.Allowed|DomainHypercallRuntimeExecutor|InvokeHypercall|new CompletionRecord\(" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Governance\Virtualization" "\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PraD2GovernanceNegativeScenario.cs"
```

The scan must return no matches. A structurally accepted policy fixture is
allowed only in the v2 validator test; diagnostics must remain negative and
production must contain no populated acceptance record or accepted-operation
registry.

PR-C fault-only identity checks:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxPrC|FullyQualifiedName~VirtualizationActivationPlanAuditGuardTests"
dotnet run --project "\HybridCPU ISE\VirtualizationDiagnosticsConsole" -- prc-o1-operand-fault-only --iterations 50
rg -n "BackendExecutionAuthorized: true|HypercallBackendAdmissionDecision\.Allowed|InvokeHypercall|new CompletionRecord\(" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Events\Hypercalls" "\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PrcO1OperandFaultOnlyScenario.cs"
```

The PR-C scenario must report O1/operand/adjacent-denial/fault counters and zero
E2/backend/completion/retire counters. Its test/diagnostic grant fixture is
evidence only; production creates no live grant or executor.

PR-E isolated exact-executor checks:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxPrEExactProbeExecutorTests|FullyQualifiedName~VirtualizationActivationPlanAuditGuardTests"
dotnet run --project "\HybridCPU ISE\VirtualizationDiagnosticsConsole" -- pre-exact-probe-executor-no-publication --iterations 50
rg -n "InvokeHypercall|BackendExecutionAuthorized: true|HypercallBackendAdmissionDecision\.Allowed|new CompletionRecord\(" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core" "\HybridCPU ISE\VirtualizationDiagnosticsConsole\Scenarios\PreExactProbeExecutorNoPublicationScenario.cs"
```

The scenario must prove default-off denial, exact-once E2 consumption, opaque
E3, duplicate/restore denial and zero production composition/completion/retire
counters. The executor may appear only as an isolated neutral service and in
its tests/diagnostic; PR-E must not add a VMX, dispatcher or pipeline caller.

PR-F canonical-composition checks:

```powershell
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxPrFCanonicalHypercallCompositionTests|FullyQualifiedName~VirtualizationActivationPlanAuditGuardTests"
dotnet run --project "\HybridCPU ISE\VirtualizationDiagnosticsConsole" -- prf-canonical-hypercall-composition --iterations 50
rg -n "HypercallBackendAdmissionDecision\.Allowed|BackendExecutionAuthorized: true|new CompletionRecord\(|VmxRetireEffect\.VmCall\(" "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core"
```

The only executor call site may be the neutral canonical composition reached
from the scheduler/execute carrier. Compatibility frontend sources must contain
no composition or executor reference. The diagnostic must report E4/E3 plus
default/adjacent/rollback denials and zero completion/retire publications.

```powershell
rg -n "NeutralTrapResult|TrapRequest|TrapPolicyBitmap|TrapCompletionPublicationFence|TrapCompletionRouteService|HypercallBackendAdmissionService|RuntimeBoundaryAdmissionService" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg -n "RuntimeOwnedCompletionPublication|RuntimeOwnedPublication|CompletionPublicationAuthorizedOnly|IsFullyRetirable|CompletionPublishedRetireDenied|TrapCompletionMigrationClass" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Runtime\Completion" `
  "\HybridCPU ISE\HybridCPU_ISE.Tests\VmxRefactoring"
```

```powershell
rg -n "TrapCompletionRouteDescriptor\.(RuntimeOwnedCompletionPublication|RuntimeOwnedPublication)" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility\Frontend"
```

The second scan must have no matches until an accepted owner-specific RFC/ADR and the corresponding completion/retire implementation gates exist.

```powershell
rg -n "VmcsReadOnlyValueProjectionService|ExecutionDomainReadOnlyStateView|MemoryDomainReadOnlyTranslationView|PrivilegedExecutionStateProjectionDenied|HostAddressSpaceOwnerMissing|HostExecutionStateOwnerMissing|CompatibilityControlValueProjectionDenied" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg -n "VmExitReason|TrapDecision|VmxTrapProjectionMapper|CompletionProjectionService|VmxRetireEffect" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization\Compatibility" `
  --glob "*.cs" --glob "!bin/**" --glob "!obj/**"
```

```powershell
rg --files "\HybridCPU ISE\HybridCPU_ISE" `
  --glob "VmxExecutionUnit.cs" `
  --glob "VmcsManager.cs" `
  --glob "IVmcsManager.cs" `
  --glob "!CloseToHSL/**"
```

```powershell
rg -n "VmxExecutionUnit|VmcsManager|IVmcsManager|VmcsManagerAdapter|VmxRuntimeManager|VmcsProjectionRuntimeManager|VmcsV2RuntimeManager|ReadFieldValue\(|WriteFieldValue\(|HardwareWrite\(|DirectWrite\(" `
  "\HybridCPU ISE\HybridCPU_ISE\CloseToHSL\Core\Virtualization" `
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
PR-H anchors: `DomainHypercallRetireOwner.cs`,
`CPU_Core.PipelineExecution.VmxRetire.cs`, the selected-prefix call in
`CPU_Core.PipelineExecution.cs`, `VmxPrHCanonicalRetireE6Tests.cs`, and the
`prh-canonical-retire-e6` diagnostic. These prove exact no-state E6 only; they do
not prove E7, migration, release or compatibility authority.
