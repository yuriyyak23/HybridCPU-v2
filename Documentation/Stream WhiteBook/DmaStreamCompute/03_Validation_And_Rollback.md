# DmaStreamCompute Validation And Rollback

## Focused Validation

```powershell
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~DmaStreamCompute"
dotnet test .\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj --filter "FullyQualifiedName~DmaStreamComputeCompilerContract|FullyQualifiedName~L7SdcDocumentationClaimSafety"
```

## Required Negative Evidence

- non-lane6 placement rejects;
- descriptor/reference mismatch rejects;
- wrong owner/domain rejects;
- unsupported DSC1/DSC2 execution rejects;
- StreamEngine/DMAController/scalar/vector/custom-accelerator fallback rejects;
- memory remains unchanged before commit;
- partial commit failure restores checkpoints;
- replay rejects stale or incomplete identity.

## Rollback Rule

Token-owned staged writes are not architectural memory. Commit snapshots the
destination and restores all previously written ranges if any write fails.
Fault publication occurs through the owning retire contour.

## Documentation Rule

Do not reuse historical pass counts as current evidence. Record the command,
date, checkout, and actual result of each new validation run.
