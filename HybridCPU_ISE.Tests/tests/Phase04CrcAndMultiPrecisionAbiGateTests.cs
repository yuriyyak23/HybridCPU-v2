using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HybridCPU.Compiler.Core.API.Facade;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using HybridCPU_ISE.Tests.TestHelpers;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using CloseToRtlCsel = YAKSys_Hybrid_CPU.CloseToRTL.Core.ISA.Instructions.NonVmx.Lanes00_03Scalar.ConditionalSelect.CselInstruction;
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
    [Fact]
    public void CselRow_ClosesFourRegisterCarrierGateFailClosed()
    {
        Assert.Equal("CSEL", CloseToRtlCsel.Mnemonic);
        Assert.Equal("rd, rs_true, rs_false, rs_cond", CloseToRtlCsel.OperandShape);
        Assert.Equal("ScalarSelectAbiDeferredNoEmission", CloseToRtlCsel.EvidenceBoundary);
        Assert.Equal("Phase01ECarrierGateClosedNoApprovedCarrier", CloseToRtlCsel.CarrierGateDecision);
        Assert.True(CloseToRtlCsel.RequiresFourRegisterCarrierAbi);
        Assert.True(CloseToRtlCsel.FourSourceCarrierDecisionClosed);
        Assert.True(CloseToRtlCsel.ExternalCarrierGateClosed);
        Assert.True(CloseToRtlCsel.RequiresExternalCarrierAbi);
        Assert.False(CloseToRtlCsel.ApprovedFourSourceCarrier);
        Assert.False(CloseToRtlCsel.ExternalCarrierApprovedInPhase01);
        Assert.False(CloseToRtlCsel.CurrentPackedScalarIrSupportsCarrier);

        AssertDeferredNoEmissionSurface(typeof(CloseToRtlCsel));
        AssertReservedNoAllocationStatus("CSEL", "ScalarSelectCzero");
        AssertCompilerCselAbiContract();
    }

    [Fact]
    public void CselRow_DoesNotInventCzeroAliasCarrierRuntimeOrCompilerAuthority()
    {
        AssertNoEnumOrRegistryMnemonic("CSEL");
        Assert.False(IsaV4Surface.OptionalEnabledOpcodes.Contains("CSEL"));
        Assert.False(InstructionRegistry.IsRegistered(uint.MaxValue));

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerDeferredScalarAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("NoAllocationUntilFourRegisterCarrierConditionResultAliasPolicyAbi", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.CSEL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.CSEL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.CSEL", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CompileCsel", compilerSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EmitCsel", compilerSource, StringComparison.OrdinalIgnoreCase);

        AssertNoPublicFacadeHelpers(
            "Csel",
            "ConditionalSelect",
            "SelectConditional",
            "CompileCsel",
            "EmitCsel");
    }

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
        AssertCompilerCrcAbiContract(mnemonic, expectedExtension);
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

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("CompilerDeferredScalarAbiContract", compilerSource, StringComparison.Ordinal);
        Assert.Contains("NoAllocationUntilPolynomialReflectionSeedFinalXorEndianDataResultAbi", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.CRC32", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InstructionsEnum.CRC64", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.CRC32", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IsaOpcodeValues.CRC64", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.CRC32", compilerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("OpcodeValues.CRC64", compilerSource, StringComparison.Ordinal);
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
        AssertCompilerMultiPrecisionAbiContract(
            mnemonic,
            requiredMarker,
            requiredPublicationMarker,
            optionalInputMarker,
            requiredOutputMarker);
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

        string compilerSource = CompilerSourceScanner.ReadAllCompilerSource();
        Assert.Contains("NoAllocationUntilExplicitCarryBorrowTransportRetirePublicationAbi", compilerSource, StringComparison.Ordinal);

        foreach (string mnemonic in new[] { "ADC", "SBC", "ADDC", "SUBC" })
        {
            Assert.DoesNotContain($"InstructionsEnum.{mnemonic}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"IsaOpcodeValues.{mnemonic}", compilerSource, StringComparison.Ordinal);
            Assert.DoesNotContain($"OpcodeValues.{mnemonic}", compilerSource, StringComparison.Ordinal);
        }

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

    private static void AssertCompilerCselAbiContract()
    {
        CompilerDeferredScalarAbiContract contract = Assert.Single(
            CompilerDeferredScalarAbiContract.ScalarSelectCarrierRows,
            row => row.Mnemonic == "CSEL");

        Assert.Equal("ScalarSelectCzero", contract.ExtensionName);
        Assert.Equal("ScalarSelectAbiDeferredNoEmission", contract.EvidenceBoundary);
        Assert.Equal("NoAllocationUntilFourRegisterCarrierConditionResultAliasPolicyAbi", contract.AbiDecision);
        Assert.Equal("rd, rs_true, rs_false, rs_cond", contract.OperandShape);
        Assert.Equal(64, contract.ResultBits);
        Assert.Equal(CompilerDeferredScalarAbiClass.ScalarSelectCarrier, contract.AbiClass);
        Assert.True(contract.RequiresFourRegisterCarrierAbi);
        Assert.True(contract.RequiresExternalCarrierAbi);
        Assert.True(contract.RequiresConditionRegisterAbi);
        Assert.True(contract.RequiresSelectResultAbi);
        Assert.True(contract.RequiresNoCzeroAliasPolicy);
        Assert.True(contract.FourSourceCarrierDecisionClosed);
        Assert.False(contract.ApprovedFourSourceCarrier);
        Assert.False(contract.CurrentPackedScalarIrSupportsCarrier);
        Assert.True(contract.RejectCzeroAliasLowering);
        Assert.True(contract.RejectHiddenMultiOpSelectLowering);
        Assert.False(contract.HasOpcodeAllocation);
        Assert.False(contract.CompilerEmissionAllowed);
        Assert.Contains("FourRegisterCarrierAbi", contract.RequiredPolicyDecisions);
        Assert.Contains("ConditionRegisterTransport", contract.RequiredPolicyDecisions);
        Assert.Contains("SelectResultSemantics", contract.RequiredPolicyDecisions);
        Assert.Contains("RetireOwnedRegisterWriteback", contract.RequiredPolicyDecisions);
        Assert.Contains("ReplayRollbackEvidence", contract.RequiredPolicyDecisions);
        Assert.Contains("NoCzeroAliasLowering", contract.RequiredPolicyDecisions);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            contract.RequireCompilerEmissionAuthority);
        Assert.Contains("compiler emission is blocked", exception.Message, StringComparison.Ordinal);
        Assert.Contains("four-register carrier", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no-CZERO-alias", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertCompilerCrcAbiContract(
        string mnemonic,
        string expectedExtension)
    {
        CompilerDeferredScalarAbiContract contract = Assert.Single(
            CompilerDeferredScalarAbiContract.ScalarCrcChecksumRows,
            row => row.Mnemonic == mnemonic);

        Assert.Equal(expectedExtension, contract.ExtensionName);
        Assert.Equal("CrcPolynomialAbiDeferredNoEmission", contract.EvidenceBoundary);
        Assert.Equal("NoAllocationUntilPolynomialReflectionSeedFinalXorEndianDataResultAbi", contract.AbiDecision);
        Assert.Equal("rd, rs_seed, rs_data", contract.OperandShape);
        Assert.Equal(mnemonic == "CRC32" ? 32 : 64, contract.ResultBits);
        Assert.Equal(CompilerDeferredScalarAbiClass.ScalarCrcChecksum, contract.AbiClass);
        Assert.True(contract.RequiresPolynomialAbi);
        Assert.True(contract.RequiresReflectionAbi);
        Assert.True(contract.RequiresSeedFinalXorAbi);
        Assert.True(contract.RequiresEndianPolicyAbi);
        Assert.True(contract.RequiresDataWidthAbi);
        Assert.True(contract.RequiresResultSemanticsAbi);
        Assert.True(contract.RejectImplicitPolynomialSelection);
        Assert.False(contract.HasOpcodeAllocation);
        Assert.False(contract.CompilerEmissionAllowed);
        Assert.Contains("Polynomial", contract.RequiredPolicyDecisions);
        Assert.Contains("InputReflection", contract.RequiredPolicyDecisions);
        Assert.Contains("OutputReflection", contract.RequiredPolicyDecisions);
        Assert.Contains("SeedInitialization", contract.RequiredPolicyDecisions);
        Assert.Contains("FinalXor", contract.RequiredPolicyDecisions);
        Assert.Contains("EndianIngestionOrder", contract.RequiredPolicyDecisions);
        Assert.Contains("DataWidth", contract.RequiredPolicyDecisions);
        Assert.Contains("ResultWidthAndExtension", contract.RequiredPolicyDecisions);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            contract.RequireCompilerEmissionAuthority);
        Assert.Contains("compiler emission is blocked", exception.Message, StringComparison.Ordinal);
        Assert.Contains("result semantics", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertCompilerMultiPrecisionAbiContract(
        string mnemonic,
        string requiredMarker,
        string requiredPublicationMarker,
        string optionalInputMarker,
        string requiredOutputMarker)
    {
        CompilerDeferredScalarAbiContract contract = Assert.Single(
            CompilerDeferredScalarAbiContract.ScalarMultiPrecisionRows,
            row => row.Mnemonic == mnemonic);

        Assert.Equal("ScalarMultiPrecision", contract.ExtensionName);
        Assert.Equal("MultiPrecisionCarryAbiDeferredNoEmission", contract.EvidenceBoundary);
        Assert.Equal("NoAllocationUntilExplicitCarryBorrowTransportRetirePublicationAbi", contract.AbiDecision);
        Assert.Equal(64, contract.ResultBits);
        Assert.Equal(CompilerDeferredScalarAbiClass.ScalarMultiPrecision, contract.AbiClass);
        Assert.False(contract.RequiresPolynomialAbi);
        Assert.False(contract.RequiresReflectionAbi);
        Assert.False(contract.RequiresSeedFinalXorAbi);
        Assert.False(contract.RequiresEndianPolicyAbi);
        Assert.True(contract.RequiresCarryBorrowPublicationAbi);
        Assert.True(contract.RequiresRetireOwnedPublicationAbi);
        Assert.True(contract.NoImplicitFlags);
        Assert.True(contract.RejectHiddenArchitecturalFlags);
        Assert.False(contract.HasOpcodeAllocation);
        Assert.False(contract.CompilerEmissionAllowed);
        Assert.Contains("RetireOwnedCarryBorrowPublication", contract.RequiredPolicyDecisions);
        Assert.Contains("CarryBorrowConsumerAbi", contract.RequiredPolicyDecisions);
        Assert.Contains("NoImplicitFlags", contract.RequiredPolicyDecisions);
        Assert.True(GetCompilerContractFlag(contract, requiredMarker), requiredMarker);
        Assert.True(GetCompilerContractFlag(contract, requiredPublicationMarker), requiredPublicationMarker);
        Assert.True(GetCompilerContractFlag(contract, requiredOutputMarker), requiredOutputMarker);

        if (!string.IsNullOrEmpty(optionalInputMarker))
        {
            Assert.True(GetCompilerContractFlag(contract, optionalInputMarker), optionalInputMarker);
        }

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            contract.RequireCompilerEmissionAuthority);
        Assert.Contains("compiler emission is blocked", exception.Message, StringComparison.Ordinal);
        Assert.Contains("carry/borrow", exception.Message, StringComparison.Ordinal);
        Assert.Contains("retire-owned", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no-implicit-flags", exception.Message, StringComparison.Ordinal);
    }

    private static bool GetCompilerContractFlag(
        CompilerDeferredScalarAbiContract contract,
        string marker) =>
        marker switch
        {
            "RequiresCarryInAbi" => contract.RequiresCarryInAbi,
            "RequiresCarryOutAbi" => contract.RequiresCarryOutAbi,
            "RequiresBorrowInAbi" => contract.RequiresBorrowInAbi,
            "RequiresBorrowOutAbi" => contract.RequiresBorrowOutAbi,
            "RequiresCarryBorrowPublicationAbi" => contract.RequiresCarryBorrowPublicationAbi,
            "RequiresExplicitCarryInputTransportAbi" => contract.RequiresExplicitCarryInputTransportAbi,
            "RequiresExplicitCarryOutputTransportAbi" => contract.RequiresExplicitCarryOutputTransportAbi,
            "RequiresExplicitBorrowInputTransportAbi" => contract.RequiresExplicitBorrowInputTransportAbi,
            "RequiresExplicitBorrowOutputTransportAbi" => contract.RequiresExplicitBorrowOutputTransportAbi,
            _ => throw new ArgumentOutOfRangeException(nameof(marker), marker, null)
        };

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

}
