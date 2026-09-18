using System.Text.Json;
using HybridCPU.Compiler.Cil;

ScalarControlFlowV2ProfileContractV1 profile = ScalarControlFlowV2ProfileContractV1.Default;
var artifact = new
{
    schema = ScalarControlFlowV2ProfileContractV1.SchemaId,
    profile = ScalarControlFlowV2ProfileContractV1.ProfileId,
    version = $"{ScalarControlFlowV2ProfileContractV1.ProfileMajor}.{ScalarControlFlowV2ProfileContractV1.ProfileMinor}",
    profile.ContractDigest,
    profile.DefaultEnabled,
    profile.AllowsHostFallback,
    profile.AllowsLlvmFallback,
    profile.HasRuntimeAuthority,
    profile.HasPhase1Blocker,
    scalarRows = profile.Scalars.Count,
    featureRows = profile.Features.Count,
    diagnosticFamilies = profile.Diagnostics.Count,
    verifiedAssumptions = profile.Assumptions.Count(static row => row.Status == ScalarControlFlowV2AssumptionStatus.Verified),
    unverifiedAssumptions = profile.Assumptions
        .Where(static row => row.Status == ScalarControlFlowV2AssumptionStatus.Unverified)
        .Select(static row => new { row.Identity, row.ConsumingPhase, row.BlocksConsumingPhase })
};
Console.WriteLine(JsonSerializer.Serialize(artifact, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
}));
