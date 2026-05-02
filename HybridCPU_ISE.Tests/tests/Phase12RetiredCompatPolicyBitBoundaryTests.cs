using System;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;

namespace HybridCPU_ISE.Tests.Phase12;

public sealed class Phase12RetiredCompatPolicyBitBoundaryTests
{
    [Fact]
    public void RetiredCompatPolicyBit_IdentifiersRemainQuarantinedToIngressBoundaries()
    {
        string repoRoot = CompatFreezeScanner.FindRepoRoot();
        string[] violations = CompatFreezeScanner.ScanProductionFilesForPatterns(
            repoRoot,
            patterns:
            [
                "RetiredPolicyGapMask",
                "GetRetiredPolicyGapViolationMessage",
                "ValidateWord3ForProductionIngress(",
                "ValidateRetiredPolicyGapBitForProductionIngress("
            ],
            allowedRelativePaths:
            [
                RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Bundle.cs"),
                RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.CompatIngress.cs"),
                RelativePath("HybridCPU_ISE", "Arch", "Compat", "VLIW_Instruction.Serialization.cs"),
                RelativePath("HybridCPU_ISE", "Core", "Decoder", "VliwDecoderV4.cs"),
                RelativePath("HybridCPU_ISE", "Core", "Execution", "DmaStreamCompute", "DmaStreamComputeDescriptorParser.cs"),
                RelativePath("HybridCPU_ISE", "Core", "Execution", "ExternalAccelerators", "Descriptors", "AcceleratorDescriptorParser.cs"),
                RelativePath("HybridCPU_ISE", "Core", "Execution", "StreamEngine", "StreamExecutionRequest.cs")
            ]);

        Assert.Empty(violations);
    }

    private static string RelativePath(params string[] parts) =>
        string.Join(System.IO.Path.DirectorySeparatorChar, parts);
}
