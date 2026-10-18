using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Decoder;

namespace HybridCPU_ISE.Tests.Architecture;

/// <summary>
/// RF-05 executable differential ledger. It records every known legacy decoder
/// reject family as either a migrated typed decode failure or an explicitly
/// non-decode authority. The latter may not be silently treated as a parity gap.
/// </summary>
public sealed class DecoderDifferentialCoverageTests
{
    private enum Disposition
    {
        DecodeFailure,
        RuntimeStructuralLegality,
        PostDecodeArchitecturalFault,
    }

    private sealed record LedgerRow(
        string LegacyGuard,
        Disposition Disposition,
        DecodeFailureCode? FailureCode,
        string Owner);

    private static readonly LedgerRow[] Ledger =
    [
        Decode("IsProhibited", DecodeFailureCode.ProhibitedOpcode),
        Decode("RejectUnknownOpcode", DecodeFailureCode.UnknownOpcode),
        Decode("RejectUnsupportedResidualContour", DecodeFailureCode.UnsupportedOpcode),
        Decode("RejectReservedWord0", DecodeFailureCode.ReservedEncoding),
        Decode("RejectLegacyPolicyGap", DecodeFailureCode.ReservedEncoding),
        Decode("ValidateOpcodeFlagLegality", DecodeFailureCode.ReservedEncoding),
        Decode("RejectUnsupportedFencePayload", DecodeFailureCode.ReservedEncoding),
        Decode("DecodeRegisterOperands", DecodeFailureCode.OperandEncoding),
        Decode("RejectScalarImmediateRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarUnaryRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarAddressGenerationRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarAddressGenerationImmediateRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarCarryLessRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarRotateRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarRotateImmediateRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarBooleanInvertRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarBitfieldRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarBitfieldImmediateRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarMinMaxRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectScalarZeroingSelectRegisterFormAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectCounterReadOperandAlias", DecodeFailureCode.ReservedEncoding),
        Decode("RejectControlFlowLegacyTargetTransport", DecodeFailureCode.ReservedEncoding),
        Decode("RejectNonCanonicalEmptySlot", DecodeFailureCode.BundleShape),
        Decode("RejectDescriptorSidebandOnEmptySlot", DecodeFailureCode.Sideband),
        Decode("ValidateAcceleratorCommandDescriptorNativeCarrier", DecodeFailureCode.Sideband),
        Decode("ValidateDmaStreamComputeNativeCarrier", DecodeFailureCode.Sideband),
        Decode("ValidateDmaStreamComputeStatusNativeCarrier", DecodeFailureCode.Sideband),
        Decode("ValidateDmaStreamComputeQueryCapsNativeCarrier", DecodeFailureCode.Sideband),
        Runtime("RejectUnsupportedCustomAcceleratorContour", "InstructionRegistry runtime extension authority"),
        Runtime("DmaStreamCompute owner/domain guard and descriptor-reference binding", "runtime owner/domain authority"),
        Runtime("Accelerator placement metadata and descriptor admission binding", "runtime structural legality"),
        Fault("MatrixTileIrProjectionFaultKind", "matrix projection/materialization fault authority"),
    ];

    [Fact]
    public void DifferentialLedger_HasNoUnexplainedLegacyRejectFamily()
    {
        Assert.NotEmpty(Ledger);
        Assert.Equal(
            Ledger.Length,
            Ledger.Select(static row => row.LegacyGuard).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Ledger, static row => Assert.False(string.IsNullOrWhiteSpace(row.Owner)));
        Assert.All(
            Ledger.Where(static row => row.Disposition == Disposition.DecodeFailure),
            static row => Assert.NotNull(row.FailureCode));
        Assert.All(
            Ledger.Where(static row => row.Disposition != Disposition.DecodeFailure),
            static row => Assert.Null(row.FailureCode));
    }

    [Fact]
    public void PublicFacade_ContainsNoRemovedLegacyDecodeGuard()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "HybridCPU_ISE",
            "CloseToHSL",
            "Core",
            "Frontend",
            "Decode",
            "VliwDecoderV4Bridge",
            "VliwDecoderV4.cs"));

        string[] removedLegacyGuards =
        [
            "RejectUnsupportedCustomAcceleratorContour",
            "RejectUnknownOpcode",
            "RejectUnsupportedResidualContour",
            "RejectReservedWord0",
            "RejectLegacyPolicyGap",
            "ValidateOpcodeFlagLegality",
            "RejectUnsupportedFencePayload",
            "ValidateAcceleratorCommandDescriptorNativeCarrier",
            "ValidateDmaStreamComputeNativeCarrier",
            "ValidateDmaStreamComputeStatusNativeCarrier",
            "ValidateDmaStreamComputeQueryCapsNativeCarrier",
            "DecodeRegisterOperands",
            "RejectScalarImmediateRegisterFormAlias",
            "RejectScalarUnaryRegisterFormAlias",
            "RejectScalarAddressGenerationRegisterFormAlias",
            "RejectScalarAddressGenerationImmediateRegisterFormAlias",
            "RejectScalarCarryLessRegisterFormAlias",
            "RejectScalarRotateRegisterFormAlias",
            "RejectScalarRotateImmediateRegisterFormAlias",
            "RejectScalarBooleanInvertRegisterFormAlias",
            "RejectScalarBitfieldRegisterFormAlias",
            "RejectScalarBitfieldImmediateRegisterFormAlias",
            "RejectScalarMinMaxRegisterFormAlias",
            "RejectScalarZeroingSelectRegisterFormAlias",
            "RejectCounterReadOperandAlias",
            "RejectControlFlowLegacyTargetTransport",
            "RejectNonCanonicalEmptySlot",
            "RejectDescriptorSidebandOnEmptySlot",
        ];

        var ledgerGuards = new HashSet<string>(
            Ledger.Select(static row => row.LegacyGuard),
            StringComparer.Ordinal);
        foreach (string guard in removedLegacyGuards)
        {
            Assert.Contains(guard, ledgerGuards);
            Assert.DoesNotContain(guard, source, StringComparison.Ordinal);
        }
    }

    private static LedgerRow Decode(string guard, DecodeFailureCode failureCode) =>
        new(guard, Disposition.DecodeFailure, failureCode, "RF-05 declarative decoder");

    private static LedgerRow Runtime(string guard, string owner) =>
        new(guard, Disposition.RuntimeStructuralLegality, null, owner);

    private static LedgerRow Fault(string guard, string owner) =>
        new(guard, Disposition.PostDecodeArchitecturalFault, null, owner);

    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "HybridCPU v2.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("HybridCPU repository root was not found.");
    }
}
