# Phase 05 - Owner/domain and mapping-epoch guard integration

Status: closed.

Goal:

- Make owner/domain guard the first authority check for every L7-SDC acceptance
  step.
- Add mapping epoch/IOMMU-domain binding for detachable or suspendable external
  accelerator tokens.

ISE files likely touched:

- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Auth/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Descriptors/*`
- `HybridCPU_ISE/Core/Execution/ExternalAccelerators/Tokens/*`
- `HybridCPU_ISE/Core/Decoder/VliwDecoderV4.cs`
- `HybridCPU_ISE/Core/Decoder/DecodedBundleTransportProjector.cs`
- existing DmaStreamCompute owner/domain guard helpers
- VM/domain and memory mapping support files used by safety verifier
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.Guards.cs`
- `HybridCPU_ISE/Core/Pipeline/Safety/SafetyVerifier.MemoryRanges.cs`

Compiler files likely touched:

- `HybridCPU_Compiler/API/Threading/HybridCpuThreadCompilerContext.cs`
- compiler descriptor builder code once accelerator descriptors are emitted

New ISE types:

- `AcceleratorOwnerDomainGuard`
- `AcceleratorGuardEvidence`
- `AcceleratorOwnerBinding`
- `AcceleratorDomainTag`
- `AcceleratorMappingEpoch`
- `AcceleratorIommuDomainEpoch`
- `AcceleratorGuardFault`

Methods to design:

- `AcceleratorOwnerDomainGuard.EnsureBeforeDescriptorAcceptance(...)`
- `AcceleratorOwnerDomainGuard.EnsureBeforeCapabilityAcceptance(...)`
- `AcceleratorOwnerDomainGuard.EnsureBeforeSubmit(...)`
- `AcceleratorOwnerDomainGuard.EnsureBeforeDeviceExecution(...)`
- `AcceleratorOwnerDomainGuard.EnsureBeforeCommit(...)`
- `AcceleratorOwnerDomainGuard.EnsureBeforeExceptionPublication(...)`
- `AcceleratorOwnerDomainGuard.CaptureMappingEpoch(...)`
- `AcceleratorOwnerDomainGuard.ValidateMappingEpoch(...)`
- `AcceleratorOwnerDomainGuard.MarkAbandonedOnInvalidOwner(...)`

Authority requirements:

- raw VT hint is never owner authority
- telemetry is never owner/domain authority
- replay/certificate identity is never authority
- token handle is never authority
- registry success is never authority
- mapping epoch drift prevents commit
- owner/domain invalid at completion forbids user-visible commit

Invalid-owner completion policy:

- token transitions to `Faulted` or privileged `Abandoned`
- staged writes are discarded or held only in privileged diagnostics
- user-visible architectural publication to the old context is forbidden
- privileged runtime/OS diagnostics may record the device fault

Tests to add:

- `L7SdcOwnerDomainGuardTests`
- `L7SdcMappingEpochGuardTests`
- `L7SdcInvalidOwnerCompletionTests`

Test cases:

- descriptor parse without guard rejects
- capability acceptance without guard rejects
- submit without guard rejects
- device execution authorization without guard rejects
- commit after owner drift rejects
- commit after mapping epoch drift rejects
- fault publication to invalid owner is forbidden
- privileged diagnostic channel records invalid-owner device completion

Must not break:

- existing DmaStreamCompute owner/domain guard tests
- VM/domain transition safety tests
- safety verifier guard semantics

Phase closure validation:

- Run focused tests:

```powershell
dotnet test --filter "L7SdcOwnerDomainGuard|L7SdcMappingEpochGuard|L7SdcInvalidOwnerCompletion"
```

- Run affected baseline filters:

```powershell
dotnet test --filter "DmaStreamComputeDomainGuard|Phase12|AssistRuntime"
```

- Run diagnostics console:

```powershell
dotnet run --project TestAssemblerConsoleApps/TestAssemblerConsoleApps.csproj -- matrix-smoke --iterations 200 --telemetry-logs minimal
```

- Compare with `Documentation/AsmAppTestResults.md`; owner/domain guard changes
  must not change baseline diagnostic behavior outside intentional reject
  counters.

Definition of done:

- guard calls exist before every descriptor/capability/submit/execution/commit
  acceptance point
- mapping epoch drift blocks detachable token commit

Rollback rule:

- reject all L7-SDC submits if guard coverage is incomplete
