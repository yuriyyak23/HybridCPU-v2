# 04 — Split shell providers from production providers

## Goal

Separate compatibility/fail-closed shell providers from production-lowering implementation providers.

The current provider registry already routes by contour. The refactor must preserve that responsibility routing while preventing it from becoming a fallback router.

## Current model

Current provider classes such as `ScalarVliwLoweringProvider`, `LoadStoreVliwLoweringProvider`, `BranchControlVliwLoweringProvider`, `StreamVectorLoweringProvider`, `MatrixTileLoweringProvider`, `DmaStreamComputeLoweringProvider`, `L7SdcLoweringProvider`, `VmxProjectionLoweringProvider`, and `SecureComputeAdmissionLoweringProvider` inherit from `ContourLoweringProviderShell`.

That shell currently:

- observes contour capability;
- creates analysis evidence through `ContourAnalyzerShell`;
- rejects cross-contour analysis;
- rejects production lowering as runtime-authority-owned.

This behavior is correct and should not be replaced in place.

## Target model

### Analyzer

Analyzer responsibilities:

- classify contour-specific requirements;
- report missing sideband/descriptor/facts;
- report required runtime gates;
- emit structural evidence;
- never emit carrier or production package.

### Provider shell

Shell responsibilities:

- compatibility wrapper;
- fail-closed guard;
- no-fallback enforcement;
- authority-boundary documentation in code;
- never production package emission.

### Production provider

Production provider responsibilities:

- only place where contour-specific production package construction may occur;
- requires explicit production gate result;
- produces separated artifact envelopes;
- records runtime authority still pending;
- emits no-fallback proof and telemetry/evidence;
- supports shadow compare against legacy/helper/compat paths during migration.

### Registry

Registry responsibilities:

- route by exact contour;
- expose shell provider and production provider resolution separately;
- never infer fallback;
- never promote capability observation to provider availability.

## Suggested contract

```csharp
public interface IContourProductionLoweringProvider
{
    ExecutionContourKind ContourKind { get; }

    CompilerProductionLoweringGateResult EvaluateProductionGates(
        CompilerSemanticIntent intent,
        ContourAnalysisReport analysis,
        CompilerProductionLoweringContext context);

    CompilerProductionLoweringResult TryProduce(
        CompilerSemanticIntent intent,
        ContourAnalysisReport analysis,
        CompilerProductionLoweringContext context);
}
```

`TryProduce` must fail closed when:

- `analysis.ContourKind != ContourKind`;
- any gate is missing;
- sideband/descriptor/facts requirements are missing;
- ISE parity coverage is absent;
- no-fallback proof cannot be constructed;
- runtime dependency map would be weakened.

## Files likely touched in implementation PR

- `HybridCPU_Compiler/Core/IR/Contours/ContourProviderContracts.cs`
- new files under `HybridCPU_Compiler/Core/IR/Lowering/Production/`
- new tests under `HybridCPU_ISE.Tests/CompilerTests/`

## Merge gate

- Shell provider tests still pass unchanged.
- Unknown contour still resolves to rejected provider/analyzer.
- No production provider is returned unless explicit profile gate enables it.
- No current caller is migrated in this phase.

## Rollback

Remove production-provider contract and registry extension. Existing shell path remains intact.
