using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU.Compiler.Core.IR.Authority;
using HybridCPU.Compiler.Core.IR.Contours;
using HybridCPU_ISE.Arch;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal sealed record CompilerToIseParitySnapshot(
    string CarrierBytesHash,
    string ReencodedBytesHash,
    IReadOnlyList<string> DecodedOpcodeIdentity,
    IReadOnlyList<string> TypedSlotFactFingerprint,
    IReadOnlyList<int> OccupiedSlots,
    IReadOnlyList<CompilerToIseParitySlot> Slots);

internal sealed record CompilerToIseParitySlot(
    int BundleIndex,
    int SlotIndex,
    uint Opcode,
    SlotClass SlotClass,
    SlotPinningKind PinningKind,
    byte EligibleLaneMask);

/// <summary>
/// Phase 05 test-only harness. It checks compiler carrier bytes against the
/// canonical ISE bundle read/write and decode/project path. Lane and pinning
/// comparisons are structural evidence only; this helper never executes a
/// projected MicroOp or treats typed-slot facts as runtime legality.
/// </summary>
internal static class CompilerToIseParityHarness
{
    internal static CompilerToIseParitySnapshot Capture(
        CompilerEmissionPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        CompilerArtifactSeparationProof separation = package.SeparationProof;
        if (!separation.CarrierSeparatedFromSideband ||
            !separation.DescriptorSeparatedFromAuthority ||
            !separation.TypedSlotFactsSeparatedFromLegality ||
            !separation.EvidenceSeparatedFromProductionLowering ||
            !separation.BridgeSeparatedFromExecution)
        {
            throw new InvalidOperationException(
                "Parity requires separated compiler artifact envelopes.");
        }

        VliwCarrierEnvelope carrier = package.Carrier ??
            throw new InvalidOperationException("Parity requires a compiler carrier envelope.");
        byte[] image = carrier.Image.SerializedImage;
        if (image.Length == 0 || image.Length % HybridCpuBundleSerializer.BundleSizeBytes != 0)
        {
            throw new InvalidOperationException(
                "Parity requires a non-empty carrier image aligned to the ISE bundle size.");
        }

        byte[] compilerImage = new HybridCpuBundleSerializer().SerializeProgram(carrier.Image.Bundles);
        Assert.Equal(compilerImage, image);

        if (package.RuntimeBridgeInput is not
            {
                RuntimeLegalityAStillRequired: true,
                RuntimeLegalityBStillRequired: true,
                RuntimeCommitStillRequired: true,
                RuntimeRetireStillRequired: true,
                RuntimePublicationStillRequired: true
            })
        {
            throw new InvalidOperationException(
                "Parity requires the complete runtime authority dependency map.");
        }

        var decodedOpcodes = new List<string>();
        var factFingerprints = new List<string>();
        var occupiedSlots = new List<int>();
        var paritySlots = new List<CompilerToIseParitySlot>();
        var reencoded = new byte[image.Length];
        var decoder = new VliwDecoderV4();

        int bundleCount = image.Length / HybridCpuBundleSerializer.BundleSizeBytes;
        for (int bundleIndex = 0; bundleIndex < bundleCount; bundleIndex++)
        {
            int bundleOffset = bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes;
            var bundle = new VLIW_Bundle();
            if (!bundle.TryReadBytes(image, bundleOffset))
            {
                throw new InvalidOperationException(
                    $"ISE failed to read compiler carrier bundle {bundleIndex}.");
            }

            if (!bundle.TryWriteBytes(
                    reencoded.AsSpan(bundleOffset, HybridCpuBundleSerializer.BundleSizeBytes)))
            {
                throw new InvalidOperationException(
                    $"ISE failed to re-encode carrier bundle {bundleIndex}.");
            }

            VLIW_Instruction[] rawSlots = ToRawSlots(bundle);
            VliwBundleAnnotations? annotations = package.Sideband is { } sideband &&
                sideband.BundleAnnotations.Count > bundleIndex
                ? sideband.BundleAnnotations[bundleIndex]
                : null;
            DecodedInstructionBundle decoded = decoder.DecodeInstructionBundle(
                rawSlots,
                annotations,
                bundleAddress: (ulong)bundleOffset,
                bundleSerial: (ulong)bundleIndex);

            MicroOp?[] projected =
                DecodedBundleTransportProjector.BuildCanonicalCarrierBundleForTesting(
                    rawSlots,
                    decoded);
            MicroOp?[] structuralProjected = projected.ToArray();
            for (int slotIndex = 0; slotIndex < rawSlots.Length; slotIndex++)
            {
                if (rawSlots[slotIndex].OpCode == 0)
                {
                    structuralProjected[slotIndex] = null;
                }
            }

            TypedSlotBundleFacts iseFacts = TypedSlotBundleFacts.FromBundle(structuralProjected);
            if (package.TypedSlotFacts is { } typedFacts &&
                typedFacts.Facts.Count > bundleIndex)
            {
                AssertTypedSlotFactsEqual(
                    typedFacts.Facts[bundleIndex],
                    iseFacts,
                    bundleIndex);
            }

            factFingerprints.Add(Fingerprint(iseFacts));
            for (int slotIndex = 0; slotIndex < rawSlots.Length; slotIndex++)
            {
                VLIW_Instruction raw = rawSlots[slotIndex];
                DecodedInstruction decodedSlot = decoded.GetDecodedSlot(slotIndex);
                if (raw.OpCode == 0)
                {
                    Assert.False(decodedSlot.IsOccupied, $"Empty slot {slotIndex} decoded as occupied.");
                    continue;
                }

                Assert.True(decodedSlot.IsOccupied, $"Occupied slot {slotIndex} decoded as empty.");
                Assert.Equal(raw.OpCode, (uint)decodedSlot.CanonicalOpcode);
                decodedOpcodes.Add($"{bundleIndex}:{slotIndex}:{raw.OpCode:X4}");
                occupiedSlots.Add(bundleIndex * rawSlots.Length + slotIndex);

                MicroOp projectedCarrier = projected[slotIndex] ??
                    throw new InvalidOperationException(
                        $"ISE projection dropped occupied compiler slot {bundleIndex}:{slotIndex}.");
                byte laneMask = SlotClassLaneMap.GetLaneMask(
                    projectedCarrier.Placement.RequiredSlotClass);
                Assert.NotEqual(0, laneMask);
                if (projectedCarrier.Placement.PinningKind == SlotPinningKind.HardPinned)
                {
                    Assert.True(
                        (laneMask & (1 << projectedCarrier.Placement.PinnedLaneId)) != 0,
                        $"Pinned lane {projectedCarrier.Placement.PinnedLaneId} is outside the ISE class mask.");
                }

                paritySlots.Add(
                    new(
                        bundleIndex,
                        slotIndex,
                        raw.OpCode,
                        projectedCarrier.Placement.RequiredSlotClass,
                        projectedCarrier.Placement.PinningKind,
                        laneMask));
            }
        }

        Assert.Equal(image, reencoded);
        return new(
            Hash(image),
            Hash(reencoded),
            decodedOpcodes,
            factFingerprints,
            occupiedSlots,
            paritySlots);
    }

    internal static void AssertRuntimeAuthorityPending(CompilerEmissionPackage package)
    {
        Assert.Equal(CompilerExecutionClaim.RuntimeExecutionRequired, package.Carrier!.Header.ExecutionClaim);
        Assert.Equal(CompilerExecutionClaim.NoExecutionClaim, package.Evidence.Header.ExecutionClaim);
        Assert.True(package.TypedSlotFacts?.StructuralEvidenceOnly ?? false);
        Assert.True(package.TypedSlotFacts?.RuntimeLegalityStillRequired ?? false);
        Assert.True(package.RuntimeBridgeInput?.RuntimeLegalityAStillRequired ?? false);
        Assert.True(package.RuntimeBridgeInput?.RuntimeLegalityBStillRequired ?? false);
        Assert.True(package.RuntimeBridgeInput?.RuntimeCommitStillRequired ?? false);
        Assert.True(package.RuntimeBridgeInput?.RuntimeRetireStillRequired ?? false);
        Assert.True(package.RuntimeBridgeInput?.RuntimePublicationStillRequired ?? false);
    }

    internal static CompilerToIseParitySnapshot AssertContourAndOpcode(
        CompilerEmissionPackage package,
        ExecutionContourKind expectedContour,
        InstructionsEnum expectedOpcode)
    {
        Assert.Equal(expectedContour, package.Identity.ContourKind);
        CompilerToIseParitySnapshot snapshot = Capture(package);
        Assert.Contains(
            snapshot.Slots,
            slot => slot.Opcode == (uint)expectedOpcode);
        return snapshot;
    }

    private static VLIW_Instruction[] ToRawSlots(VLIW_Bundle bundle)
    {
        var rawSlots = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        for (int slotIndex = 0; slotIndex < rawSlots.Length; slotIndex++)
        {
            rawSlots[slotIndex] = bundle.GetInstruction(slotIndex);
        }

        return rawSlots;
    }

    private static void AssertTypedSlotFactsEqual(
        TypedSlotBundleFacts expected,
        TypedSlotBundleFacts actual,
        int bundleIndex)
    {
        Assert.Equal(expected.PinningKindMask, actual.PinningKindMask);
        Assert.Equal(expected.FlexibleOpCount, actual.FlexibleOpCount);
        Assert.Equal(expected.PinnedOpCount, actual.PinnedOpCount);
        Assert.Equal(expected.AluCount, actual.AluCount);
        Assert.Equal(expected.LsuCount, actual.LsuCount);
        Assert.Equal(expected.DmaStreamCount, actual.DmaStreamCount);
        Assert.Equal(expected.MatrixTileStreamCount, actual.MatrixTileStreamCount);
        Assert.Equal(expected.BranchControlCount, actual.BranchControlCount);
        Assert.Equal(expected.SystemSingletonCount, actual.SystemSingletonCount);
        for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
        {
            Assert.Equal(
                expected.GetSlotClass(slotIndex),
                actual.GetSlotClass(slotIndex));
            Assert.Equal(
                expected.IsSlotPinned(slotIndex),
                actual.IsSlotPinned(slotIndex));
        }

        _ = bundleIndex;
    }

    private static string Fingerprint(TypedSlotBundleFacts facts) =>
        string.Join(
            ":",
            Enumerable.Range(0, BundleMetadata.BundleSlotCount)
                .Select(index => $"{facts.GetSlotClass(index)}:{facts.IsSlotPinned(index)}")) +
        $"|{facts.AluCount},{facts.LsuCount},{facts.DmaStreamCount},{facts.MatrixTileStreamCount},{facts.BranchControlCount},{facts.SystemSingletonCount}";

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
