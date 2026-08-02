using System.Collections.Immutable;
using System.Security.Cryptography;
using HybridCPU.Compiler.Core.IR;
using HybridCPU.Compiler.Core.IR.Artifacts;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Arch.Generated;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal sealed record Rf066CompilerParityProjection(
    string ProjectionSchema,
    string CarrierSha256,
    string CompilerLoweringProviderIdentity,
    int CatalogSchemaVersion,
    string CatalogVersion,
    string CatalogSha256,
    ImmutableArray<Rf066CompilerParityBundle> Bundles,
    ImmutableArray<Rf066CompilerParitySlot> Slots,
    string CanonicalFingerprint);

internal sealed record Rf066CompilerParityBundle(
    int BundleIndex,
    string BundleSidebandSha256,
    string SemanticAnnotationsSha256);

internal sealed record Rf066CompilerParitySlot(
    int BundleIndex,
    int SlotIndex,
    uint Opcode,
    string OperandSchema,
    string OperandFingerprint,
    string SlotSidebandSha256,
    string DescriptorFingerprint,
    GeneratedStaticBinding StaticBinding,
    InstructionClass ExecutionClass,
    SerializationClass Serialization,
    SlotClass PlacementClass,
    SlotPinningKind PlacementPinning,
    string DescriptorSlotConstraints,
    string MemoryStaticCapability,
    string StaticEffectContract,
    string LatencyModelId);

internal sealed record Rf066CompilerBindingEvidence(
    GeneratedStaticBinding Binding,
    string Schema,
    bool IsActive)
{
    internal const string ExpectedSchema = "rf06.6.compiler-parity.binding.v1";
}

/// <summary>
/// RF-06.6a test evidence only. This projection enters through the public
/// canonical decoder facade and carries the exact decoded static binding. It
/// never materializes, admits, schedules, executes, or resolves a runtime
/// provider/materializer from an opcode.
/// </summary>
internal static class Rf066CompilerParityProjector
{
    internal const string ProjectionSchema = "rf06.6.compiler-to-ise-parity.v1";

    internal static Rf066CompilerParityProjection Capture(
        CompilerEmissionPackage package,
        Func<ImmutableArray<Rf066CompilerBindingEvidence>, ImmutableArray<Rf066CompilerBindingEvidence>>? mutateBindingEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        VliwCarrierEnvelope carrier = package.Carrier ??
            throw Fail("missing-carrier", "Compiler parity requires a carrier envelope.");
        byte[] emitted = carrier.Image.SerializedImage;
        byte[] serialized = new HybridCpuBundleSerializer().SerializeProgram(carrier.Image.Bundles);
        if (!serialized.AsSpan().SequenceEqual(emitted))
        {
            throw Fail("carrier-hash-mismatch", "Compiler bundle serialization differs from its emitted carrier bytes.");
        }

        if (emitted.Length == 0 || emitted.Length % HybridCpuBundleSerializer.BundleSizeBytes != 0)
        {
            throw Fail("carrier-shape", "Compiler carrier is empty or not bundle aligned.");
        }

        if (package.TypedSlotFacts is not { StructuralEvidenceOnly: true, RuntimeLegalityStillRequired: true } typedFacts)
        {
            throw Fail("typed-facts", "Compiler parity requires structural-only typed slot facts with runtime legality pending.");
        }

        var bundles = ImmutableArray.CreateBuilder<Rf066CompilerParityBundle>();
        var slots = ImmutableArray.CreateBuilder<Rf066CompilerParitySlot>();
        var decoder = new VliwDecoderV4();
        int bundleCount = emitted.Length / HybridCpuBundleSerializer.BundleSizeBytes;
        if (typedFacts.Facts.Count < bundleCount)
        {
            throw Fail("typed-facts-count", "Compiler parity requires typed slot facts for every emitted bundle.");
        }

        for (int bundleIndex = 0; bundleIndex < bundleCount; bundleIndex++)
        {
            var carrierBundle = new VLIW_Bundle();
            if (!carrierBundle.TryReadBytes(emitted, bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes))
            {
                throw Fail("carrier-read", $"Compiler carrier bundle {bundleIndex} cannot be read by the ISE transport.");
            }

            VLIW_Instruction[] rawSlots = ToRawSlots(carrierBundle);
            VliwBundleAnnotations? annotations = package.Sideband is { } sideband &&
                sideband.BundleAnnotations.Count > bundleIndex
                    ? sideband.BundleAnnotations[bundleIndex]
                    : null;
            DecodedInstructionBundle decoded = decoder.DecodeInstructionBundle(
                rawSlots,
                annotations,
                (ulong)(bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes),
                (ulong)bundleIndex);
            CanonicalBundle canonicalBundle = decoded.CanonicalBundle ??
                throw Fail("canonical-handoff", "Public decoder returned no canonical bundle.");

            bundles.Add(new(
                bundleIndex,
                canonicalBundle.BundleSideband.ContentSha256,
                canonicalBundle.SemanticKey.AnnotationsSha256));

            TypedSlotBundleFacts compilerFacts = typedFacts.Facts[bundleIndex];
            for (int slotIndex = 0; slotIndex < BundleMetadata.BundleSlotCount; slotIndex++)
            {
                VLIW_Instruction raw = rawSlots[slotIndex];
                CanonicalDecodedInstruction canonical = canonicalBundle.GetSlot(slotIndex);
                if (raw.OpCode == 0)
                {
                    if (canonical.IsOccupied)
                    {
                        throw Fail("empty-slot-drift", $"Empty compiler slot {bundleIndex}:{slotIndex} decoded as occupied.");
                    }

                    continue;
                }

                if (!canonical.IsOccupied || canonical.Opcode != raw.OpCode)
                {
                    throw Fail("opcode-drift", $"Compiler opcode and canonical opcode differ at {bundleIndex}:{slotIndex}.");
                }

                GeneratedStaticBinding binding = canonical.StaticBinding ??
                    throw Fail("missing-decoded-binding", $"Canonical slot {bundleIndex}:{slotIndex} has no generated binding.");
                GeneratedIsaDescriptor descriptor = ResolveDescriptorByExactBinding(binding);
                if (descriptor.StaticClass != canonical.InstructionClass ||
                    descriptor.Serialization != canonical.SerializationClass)
                {
                    throw Fail("descriptor-semantics", $"Canonical class/serialization drift at {bundleIndex}:{slotIndex}.");
                }

                SlotClass placementClass = compilerFacts.GetSlotClass(slotIndex);
                SlotPinningKind placementPinning = compilerFacts.IsSlotPinned(slotIndex)
                    ? SlotPinningKind.HardPinned
                    : SlotPinningKind.ClassFlexible;
                IrOpcodeExecutionProfile compilerProfile =
                    HybridCpuHazardModel.GetExecutionProfile((InstructionsEnum)canonical.Opcode);
                if (placementClass != compilerProfile.DerivedSlotClass ||
                    descriptor.ExecutionLatency != compilerProfile.MinimumLatencyCycles)
                {
                    throw Fail("compiler-static-drift", $"Compiler placement/latency differs from generated static evidence at {bundleIndex}:{slotIndex}.");
                }

                if (annotations is not null &&
                    annotations.TryGetInstructionSlotMetadata(slotIndex, out InstructionSlotMetadata metadata) &&
                    (metadata.SlotMetadata.AdmissionMetadata.Placement.RequiredSlotClass != placementClass ||
                     metadata.SlotMetadata.AdmissionMetadata.Placement.PinningKind != placementPinning))
                {
                    throw Fail("annotation-placement-drift", $"Compiler annotation and typed placement differ at {bundleIndex}:{slotIndex}.");
                }

                slots.Add(new(
                    bundleIndex,
                    slotIndex,
                    canonical.Opcode,
                    descriptor.OperandSchema,
                    BuildOperandFingerprint(canonical),
                    canonical.SlotSideband.ContentSha256,
                    binding.DescriptorFingerprint,
                    binding,
                    canonical.InstructionClass!.Value,
                    canonical.SerializationClass!.Value,
                    placementClass,
                    placementPinning,
                    descriptor.SlotConstraints,
                    descriptor.StaticClass is InstructionClass.Memory or InstructionClass.Atomic
                        ? $"{descriptor.StaticClass}:{descriptor.StaticEffectContract}"
                        : "NoMemory",
                    descriptor.StaticEffectContract,
                    binding.LatencyModelId.Value));
            }
        }

        ImmutableArray<Rf066CompilerParitySlot> frozenSlots = slots.ToImmutable();
        ImmutableArray<Rf066CompilerBindingEvidence> bindingEvidence = frozenSlots
            .Select(slot => slot.StaticBinding)
            .Distinct()
            .Select(binding => new Rf066CompilerBindingEvidence(
                binding,
                Rf066CompilerBindingEvidence.ExpectedSchema,
                IsActive: true))
            .ToImmutableArray();
        if (mutateBindingEvidence is not null)
        {
            bindingEvidence = mutateBindingEvidence(bindingEvidence);
        }

        ValidateBindingEvidence(frozenSlots, bindingEvidence);
        ImmutableArray<Rf066CompilerParityBundle> frozenBundles = bundles.ToImmutable();
        string fingerprint = Fingerprint(frozenBundles, frozenSlots);
        return new(
            ProjectionSchema,
            Hash(emitted),
            package.Identity.ProducerSurface,
            GeneratedIsaCatalog.CatalogSchemaVersion,
            GeneratedIsaCatalog.CatalogVersion,
            GeneratedIsaCatalog.CatalogSha256,
            frozenBundles,
            frozenSlots,
            fingerprint);
    }

    internal static void AssertEquivalent(
        Rf066CompilerParityProjection expected,
        Rf066CompilerParityProjection candidate)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!string.Equals(expected.ProjectionSchema, candidate.ProjectionSchema, StringComparison.Ordinal) ||
            !string.Equals(expected.CarrierSha256, candidate.CarrierSha256, StringComparison.Ordinal) ||
            !string.Equals(expected.CompilerLoweringProviderIdentity, candidate.CompilerLoweringProviderIdentity, StringComparison.Ordinal) ||
            expected.CatalogSchemaVersion != candidate.CatalogSchemaVersion ||
            !string.Equals(expected.CatalogVersion, candidate.CatalogVersion, StringComparison.Ordinal) ||
            !string.Equals(expected.CatalogSha256, candidate.CatalogSha256, StringComparison.Ordinal) ||
            !string.Equals(expected.CanonicalFingerprint, candidate.CanonicalFingerprint, StringComparison.Ordinal) ||
            !expected.Bundles.SequenceEqual(candidate.Bundles) ||
            !expected.Slots.SequenceEqual(candidate.Slots))
        {
            throw Fail("parity-drift", "Compiler-to-ISE immutable parity projection changed.");
        }
    }

    internal static void AssertEquivalentIgnoringRegistryOrder(
        CompilerEmissionPackage package,
        Rf066CompilerParityProjection expected)
    {
        Rf066CompilerParityProjection reordered = Capture(
            package,
            entries => entries.Reverse().ToImmutableArray());
        AssertEquivalent(expected, reordered);
    }

    private static GeneratedIsaDescriptor ResolveDescriptorByExactBinding(GeneratedStaticBinding binding)
    {
        GeneratedIsaDescriptor[] matches = GeneratedIsaCatalog.Descriptors
            .Where(descriptor => GeneratedStaticBinding.FromDescriptor(in descriptor) == binding)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw Fail(
                "descriptor-binding-identity",
                $"Decoded binding must identify exactly one generated descriptor; found {matches.Length}.");
    }

    private static void ValidateBindingEvidence(
        ImmutableArray<Rf066CompilerParitySlot> slots,
        ImmutableArray<Rf066CompilerBindingEvidence> evidence)
    {
        GeneratedStaticBinding[] expected = slots.Select(slot => slot.StaticBinding).Distinct().ToArray();
        foreach (GeneratedStaticBinding binding in expected)
        {
            Rf066CompilerBindingEvidence[] matches = evidence.Where(entry => entry.Binding == binding).ToArray();
            if (matches.Length == 0)
            {
                throw Fail("missing-binding", $"Compiler corpus binding {binding} is missing.");
            }

            if (matches.Length != 1)
            {
                throw Fail("duplicate-binding", $"Compiler corpus binding {binding} is duplicated.");
            }

            if (!matches[0].IsActive)
            {
                throw Fail("inactive-binding", $"Compiler corpus binding {binding} is inactive.");
            }

            if (!string.Equals(matches[0].Schema, Rf066CompilerBindingEvidence.ExpectedSchema, StringComparison.Ordinal))
            {
                throw Fail("binding-schema-mismatch", $"Compiler corpus binding {binding} has a stale schema.");
            }
        }

        if (evidence.Any(entry => !expected.Contains(entry.Binding)))
        {
            throw Fail("unreferenced-binding", "Compiler parity binding evidence contains an unreferenced binding.");
        }
    }

    private static VLIW_Instruction[] ToRawSlots(VLIW_Bundle bundle) =>
        Enumerable.Range(0, BundleMetadata.BundleSlotCount)
            .Select(bundle.GetInstruction)
            .ToArray();

    private static string BuildOperandFingerprint(CanonicalDecodedInstruction slot) =>
        $"rd={slot.Rd};rs1={slot.Rs1};rs2={slot.Rs2};imm={slot.Immediate};csr={slot.CsrAddress?.ToString() ?? "none"};aq={slot.AcquireOrdering};rl={slot.ReleaseOrdering}";

    private static string Fingerprint(
        ImmutableArray<Rf066CompilerParityBundle> bundles,
        ImmutableArray<Rf066CompilerParitySlot> slots)
    {
        string canonical = string.Join(
            "|",
            bundles.Select(bundle => $"b:{bundle.BundleIndex}:{bundle.BundleSidebandSha256}:{bundle.SemanticAnnotationsSha256}")
                .Concat(slots.Select(slot =>
                    $"s:{slot.BundleIndex}:{slot.SlotIndex}:{slot.Opcode}:{slot.OperandSchema}:{slot.OperandFingerprint}:{slot.SlotSidebandSha256}:" +
                    $"{slot.DescriptorFingerprint}:{slot.StaticBinding.MaterializerId.Value}:{slot.StaticBinding.RuntimeExecutionProviderId.Value}:" +
                    $"{slot.LatencyModelId}:{slot.ExecutionClass}:{slot.Serialization}:{slot.PlacementClass}:{slot.PlacementPinning}:" +
                    $"{slot.DescriptorSlotConstraints}:{slot.MemoryStaticCapability}:{slot.StaticEffectContract}")));
        return Hash(System.Text.Encoding.UTF8.GetBytes(canonical));
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static InvalidOperationException Fail(string code, string detail) =>
        new($"RF06.6 CompilerParity [{code}] {detail} No admission or execution is permitted.");
}
