using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using CloseToRtlAdc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AdcInstruction;
using CloseToRtlAddc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.AddcInstruction;
using CloseToRtlCrc32 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc32Instruction;
using CloseToRtlCrc64 = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.CRC.Crc64Instruction;
using CloseToRtlSbc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SbcInstruction;
using CloseToRtlSubc = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.MultiPrecision.SubcInstruction;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.Phase04;

public sealed class CrcAndMultiPrecisionAbiGateTests
{
    [Theory]
    [InlineData(typeof(CloseToRtlCrc32), "CRC32", "ScalarCrcChecksum")]
    [InlineData(typeof(CloseToRtlCrc64), "CRC64", "ScalarCrcChecksum")]
    public void CrcRows_ClosePolynomialReflectionSeedFinalXorEndianGateFailClosed(
        Type instructionType,
        string mnemonic,
        string expectedExtension)
    {
        Assert.Equal("CrcPolynomialAbiDeferredNoEmission", GetConstant<string>(instructionType, "EvidenceBoundary"));
        Assert.Equal("NoAllocationUntilPolynomialReflectionSeedFinalXorEndianAbi", GetConstant<string>(instructionType, "AbiDecision"));
        Assert.True(GetConstant<bool>(instructionType, "RequiresPolynomialAbi"));
        Assert.True(GetConstant<bool>(instructionType, "RequiresReflectionAbi"));
        Assert.True(GetConstant<bool>(instructionType, "RequiresSeedFinalXorAbi"));
        Assert.True(GetConstant<bool>(instructionType, "RequiresEndianPolicyAbi"));
        Assert.True(GetConstant<bool>(instructionType, "RejectImplicitPolynomialSelection"));
        Assert.True(GetConstant<bool>(instructionType, "NoHiddenMultiOpEmission"));
        Assert.Contains("Unspecified", GetConstant<string>(instructionType, "PolynomialPolicy"), StringComparison.Ordinal);
        Assert.Contains("Unspecified", GetConstant<string>(instructionType, "ReflectionPolicy"), StringComparison.Ordinal);
        Assert.Contains("Unspecified", GetConstant<string>(instructionType, "SeedFinalXorPolicy"), StringComparison.Ordinal);
        Assert.Contains("Unspecified", GetConstant<string>(instructionType, "EndianPolicy"), StringComparison.Ordinal);

        AssertDeferredNoEmissionSurface(instructionType);
        AssertReservedNoAllocationStatus(mnemonic, expectedExtension);
    }

    [Fact]
    public void CrcRows_DoNotInventDecoderMaterializerRuntimeOrCompilerAuthority()
    {
        foreach (string mnemonic in new[] { "CRC32", "CRC64" })
        {
            AssertNoEnumOrRegistryMnemonic(mnemonic);
            Assert.False(IsaV4Surface.OptionalEnabledOpcodes.Contains(mnemonic));
            Assert.False(InstructionRegistry.IsRegistered(uint.MaxValue));
        }

        string compilerSource = ReadAllCompilerSource();
        Assert.DoesNotContain("CRC32", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CRC64", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileCrc32", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CompileCrc64", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitCrc32", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitCrc64", compilerSource, StringComparison.OrdinalIgnoreCase);

        AssertNoPublicFacadeHelpers(
            "Crc32",
            "Crc64",
            "CompileCrc32",
            "CompileCrc64",
            "EmitCrc32",
            "EmitCrc64");
    }

    [Theory]
    [InlineData(typeof(CloseToRtlAdc), "ADC", "RequiresCarryInAbi", "RequiresCarryOutAbi", "RequiresExplicitCarryInputTransportAbi", "RequiresExplicitCarryOutputTransportAbi")]
    [InlineData(typeof(CloseToRtlSbc), "SBC", "RequiresBorrowInAbi", "RequiresBorrowOutAbi", "RequiresExplicitBorrowInputTransportAbi", "RequiresExplicitBorrowOutputTransportAbi")]
    [InlineData(typeof(CloseToRtlAddc), "ADDC", "RequiresCarryOutAbi", "RequiresCarryBorrowPublicationAbi", "", "RequiresExplicitCarryOutputTransportAbi")]
    [InlineData(typeof(CloseToRtlSubc), "SUBC", "RequiresBorrowOutAbi", "RequiresCarryBorrowPublicationAbi", "", "RequiresExplicitBorrowOutputTransportAbi")]
    public void MultiPrecisionRows_CloseCarryBorrowPublicationGateFailClosed(
        Type instructionType,
        string mnemonic,
        string requiredMarker,
        string requiredPublicationMarker,
        string optionalInputMarker,
        string requiredOutputMarker)
    {
        Assert.Equal("MultiPrecisionCarryAbiDeferredNoEmission", GetConstant<string>(instructionType, "EvidenceBoundary"));
        Assert.Equal("NoAllocationUntilExplicitCarryBorrowPublicationAbi", GetConstant<string>(instructionType, "AbiDecision"));
        Assert.True(GetConstant<bool>(instructionType, requiredMarker), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, requiredPublicationMarker), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RequiresCarryBorrowPublicationAbi"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, requiredOutputMarker), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "NoImplicitFlags"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RejectHiddenArchitecturalFlags"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "NoHiddenMultiOpEmission"), instructionType.FullName);
        Assert.Contains("No implicit architectural flags", GetConstant<string>(instructionType, "CarryBorrowPublicationPolicy"), StringComparison.Ordinal);

        if (!string.IsNullOrEmpty(optionalInputMarker))
        {
            Assert.True(GetConstant<bool>(instructionType, optionalInputMarker), instructionType.FullName);
        }

        AssertDeferredNoEmissionSurface(instructionType);
        AssertReservedNoAllocationStatus(mnemonic, "ScalarMultiPrecision");
    }

    [Fact]
    public void MultiPrecisionRows_DoNotPublishHiddenFlagsOrCompilerHelpers()
    {
        foreach (string mnemonic in new[] { "ADC", "SBC", "ADDC", "SUBC" })
        {
            AssertNoEnumOrRegistryMnemonic(mnemonic);
            Assert.False(IsaV4Surface.OptionalEnabledOpcodes.Contains(mnemonic));
            Assert.False(InstructionRegistry.IsRegistered(uint.MaxValue));
        }

        string compilerSource = ReadAllCompilerSource();
        foreach (string helper in new[]
        {
            "CompileAdc",
            "CompileSbc",
            "CompileAddc",
            "CompileSubc",
            "EmitAdc",
            "EmitSbc",
            "EmitAddc",
            "EmitSubc"
        })
        {
            Assert.DoesNotContain(helper, compilerSource, StringComparison.OrdinalIgnoreCase);
        }

        AssertNoPublicFacadeHelpers(
            "Adc",
            "Sbc",
            "Addc",
            "Subc",
            "AddWithCarry",
            "AddCarry",
            "SubtractWithBorrow",
            "SubWithBorrow",
            "SubBorrow",
            "CompileAdc",
            "CompileSbc",
            "CompileAddc",
            "CompileSubc",
            "EmitAdc",
            "EmitSbc",
            "EmitAddc",
            "EmitSubc");
    }

    private static void AssertDeferredNoEmissionSurface(Type instructionType)
    {
        Assert.True(GetConstant<bool>(instructionType, "RequiresDecoderEncoderAbi"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RequiresInstructionIrProjection"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RequiresRegistryMaterializer"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RequiresRetireRegisterWriteback"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "RequiresReplayRollbackEvidence"), instructionType.FullName);
        Assert.True(GetConstant<bool>(instructionType, "NoVmxFrontendIntegrationRequired"), instructionType.FullName);
        Assert.False(GetConstant<bool>(instructionType, "RequiresVmxProjection"), instructionType.FullName);
        Assert.False(GetConstant<bool>(instructionType, "HasOpcodeAllocation"), instructionType.FullName);
        Assert.False(GetConstant<bool>(instructionType, "IsExecutable"), instructionType.FullName);
        Assert.False(GetConstant<bool>(instructionType, "CompilerHelperAllowed"), instructionType.FullName);
        Assert.Null(instructionType.GetProperty("Opcode", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(instructionType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static));
    }

    private static void AssertReservedNoAllocationStatus(
        string mnemonic,
        string expectedExtension)
    {
        Assert.True(
            InstructionSupportStatusCatalog.TryGetExplicitStatus(
                mnemonic,
                out InstructionSupportStatus status),
            $"{mnemonic} must be an explicit reserved status row.");

        Assert.Equal(IsaInstructionStatus.Reserved, status.Status);
        Assert.Equal(RuntimeInstructionEvidence.None, status.RuntimeEvidence);
        Assert.Equal(expectedExtension, status.ExtensionName);
        Assert.False(status.IsExecutableClaim, mnemonic);
        Assert.False(status.HasNumericOpcode);
        Assert.False(status.HasRuntimeOpcodeMetadata);
        Assert.False(status.HasCanonicalDecoderAcceptance);
        Assert.False(status.HasRegistryFactory);
        Assert.False(status.HasExecutionSemantics);
    }

    private static void AssertNoEnumOrRegistryMnemonic(string mnemonic)
    {
        string enumCandidate = mnemonic.Replace(".", "_", StringComparison.Ordinal);
        Assert.DoesNotContain(enumCandidate, Enum.GetNames<InstructionsEnum>());
        Assert.DoesNotContain(
            OpcodeRegistry.Opcodes,
            info => string.Equals(info.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertNoPublicFacadeHelpers(params string[] closedHelperNames)
    {
        string[] publicFacadeMethods =
        [
            .. PublicDeclaredMethodNames(typeof(IAppAsmFacade)),
            .. PublicDeclaredMethodNames(typeof(AppAsmFacade)),
            .. PublicDeclaredMethodNames(typeof(IPlatformAsmFacade)),
            .. PublicDeclaredMethodNames(typeof(PlatformAsmFacade))
        ];

        foreach (string helperName in closedHelperNames)
        {
            Assert.DoesNotContain(
                publicFacadeMethods,
                methodName => string.Equals(methodName, helperName, StringComparison.Ordinal));
        }
    }

    private static string[] PublicDeclaredMethodNames(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

    private static T GetConstant<T>(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} was not found.");
        Assert.True(field.IsLiteral, $"{type.FullName}.{name} must remain a const metadata marker.");
        return Assert.IsType<T>(field.GetRawConstantValue());
    }

    private static string ReadAllCompilerSource()
    {
        string compilerRoot = Path.Combine(CompatFreezeScanner.FindRepoRoot(), "HybridCPU_Compiler");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(compilerRoot, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
