using System;
using HybridCPU.Compiler.Core.IR;
using Xunit;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class CompilerLane7DeferredAbiAssertions
{
    public static void AssertNoEmissionAuthority(
        CompilerLane7DeferredAbiContract contract,
        string expectedMnemonic,
        CompilerLane7DeferredAbiClass expectedAbiClass,
        string expectedExtensionName,
        string expectedEvidenceBoundary)
    {
        Assert.Equal(expectedMnemonic, contract.Mnemonic);
        Assert.Equal(expectedAbiClass, contract.AbiClass);
        Assert.Equal(expectedExtensionName, contract.ExtensionName);
        Assert.Equal(expectedEvidenceBoundary, contract.EvidenceBoundary);
        Assert.False(contract.HasOpcodeAllocation);
        Assert.False(contract.IsExecutable);
        Assert.False(contract.CompilerEmissionAllowed);
        Assert.False(string.IsNullOrWhiteSpace(contract.OperandShape));
        Assert.False(string.IsNullOrWhiteSpace(contract.DataSemantics));
        Assert.False(string.IsNullOrWhiteSpace(contract.ResultSemantics));
    }

    public static void AssertHiddenLoweringBlocked(CompilerLane7DeferredAbiContract contract)
    {
        Assert.True(contract.NoHostEvidenceLeak);
        Assert.True(contract.NoHiddenScalarLowering);
        Assert.True(contract.NoMultiOpEmission);
    }

    public static void AssertPolicyDecisions(
        CompilerLane7DeferredAbiContract contract,
        params string[] requiredPolicyDecisions)
    {
        foreach (string decision in requiredPolicyDecisions)
        {
            Assert.Contains(decision, contract.RequiredPolicyDecisions);
        }
    }

    public static void AssertEmissionAuthorityThrows(CompilerLane7DeferredAbiContract contract)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            contract.RequireCompilerEmissionAuthority);
        Assert.Contains($"{contract.Mnemonic} compiler emission is blocked", exception.Message, StringComparison.Ordinal);
    }

    public static void AssertCompilerSourceCarriesMetadataButNoEmission(
        string compilerSource,
        params (string EnumCandidate, string FacadeHelperFragment)[] deferredRows)
    {
        foreach ((string enumCandidate, string facadeHelperFragment) in deferredRows)
        {
            Assert.Contains(enumCandidate, compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"InstructionsEnum.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"IsaOpcodeValues.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"OpcodeValues.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"Compile{facadeHelperFragment}", compilerSource, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"Emit{facadeHelperFragment}", compilerSource, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void AssertCompilerSourceCarriesMetadataButNoEmission(
        string compilerSource,
        params (string MetadataToken, string EnumCandidate, string FacadeHelperFragment)[] deferredRows)
    {
        foreach ((string metadataToken, string enumCandidate, string facadeHelperFragment) in deferredRows)
        {
            Assert.Contains(metadataToken, compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"InstructionsEnum.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"IsaOpcodeValues.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"OpcodeValues.{enumCandidate}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"Compile{facadeHelperFragment}", compilerSource, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"Emit{facadeHelperFragment}", compilerSource, StringComparison.OrdinalIgnoreCase);
        }
    }
}
