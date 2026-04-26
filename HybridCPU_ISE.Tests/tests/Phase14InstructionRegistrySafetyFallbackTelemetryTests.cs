using System;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase14;

[CollectionDefinition("Phase14 InstructionRegistry Safety Fallback Telemetry", DisableParallelization = true)]
public sealed class Phase14InstructionRegistrySafetyFallbackTelemetryCollection;

[Collection("Phase14 InstructionRegistry Safety Fallback Telemetry")]
public sealed class Phase14InstructionRegistrySafetyFallbackTelemetryTests
{
    private sealed class SyntheticMaskMicroOp : MicroOp
    {
        private readonly bool _allowsStructuralSafetyFallback;
        private readonly CanonicalDecodePublicationMode _canonicalDecodePublication;

        public SyntheticMaskMicroOp(
            IReadOnlyList<int>? readRegisters = null,
            ResourceBitset? resourceMask = null,
            SlotPlacementMetadata? placement = null,
            bool allowsStructuralSafetyFallback = false,
            CanonicalDecodePublicationMode canonicalDecodePublication = CanonicalDecodePublicationMode.Unspecified,
            bool isControlFlow = false,
            bool hasSideEffects = false)
        {
            _allowsStructuralSafetyFallback = allowsStructuralSafetyFallback;
            _canonicalDecodePublication = canonicalDecodePublication;
            ReadRegisters = readRegisters ?? Array.Empty<int>();
            WriteRegisters = Array.Empty<int>();
            ResourceMask = resourceMask ?? ResourceBitset.Zero;
            Placement = placement ?? SlotPlacementMetadata.Default;
            IsControlFlow = isControlFlow;
            HasSideEffects = hasSideEffects;
            SerializationClass = SerializationClass.Free;
            Class = MicroOpClass.Other;
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            _canonicalDecodePublication;

        protected internal override bool AllowsStructuralSafetyFallback =>
            _allowsStructuralSafetyFallback;

        public override bool Execute(ref Processor.CPU_Core core) => true;

        public override string GetDescription() => "Synthetic safety-mask test MicroOp";
    }

    [Fact]
    public void ComputeSafetyMask_WhenCanonicalBitsExist_DoesNotUseStructuralFallbackTelemetry()
    {
        InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();

        try
        {
            var microOp = new SyntheticMaskMicroOp(readRegisters: new[] { 1 });

            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(0, microOp, memoryDomainId: 0);

            Assert.True(mask.IsNonZero);
            Assert.Equal(0UL, InstructionRegistry.ResourceMaskStructuralFallbackCount);
            Assert.Equal(0UL, InstructionRegistry.PlacementStructuralFallbackCount);
        }
        finally
        {
            InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();
        }
    }

    [Fact]
    public void ComputeSafetyMask_WhenResourceMaskFallbackIsUsed_IncrementsResourceFallbackTelemetry()
    {
        InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();

        try
        {
            ResourceBitset resourceMask = ResourceMaskBuilder.ForMemoryBank(3);
            var microOp = new SyntheticMaskMicroOp(
                resourceMask: resourceMask,
                allowsStructuralSafetyFallback: true);

            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(0, microOp, memoryDomainId: 0);

            Assert.Equal(resourceMask.Low, mask.Low);
            Assert.Equal(resourceMask.High, mask.High);
            Assert.Equal(1UL, InstructionRegistry.ResourceMaskStructuralFallbackCount);
            Assert.Equal(0UL, InstructionRegistry.PlacementStructuralFallbackCount);
        }
        finally
        {
            InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();
        }
    }

    [Fact]
    public void ComputeSafetyMask_WhenPlacementFallbackIsUsed_IncrementsPlacementFallbackTelemetry()
    {
        InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();

        try
        {
            var microOp = new SyntheticMaskMicroOp(
                placement: new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.BranchControl,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = 0
                },
                allowsStructuralSafetyFallback: true);

            SafetyMask128 mask = InstructionRegistry.ComputeSafetyMask(0, microOp, memoryDomainId: 0);

            Assert.Equal(0UL, mask.Low);
            Assert.Equal(1UL << 63, mask.High);
            Assert.Equal(0UL, InstructionRegistry.ResourceMaskStructuralFallbackCount);
            Assert.Equal(1UL, InstructionRegistry.PlacementStructuralFallbackCount);
        }
        finally
        {
            InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();
        }
    }

    [Fact]
    public void ComputeSafetyMask_WhenCanonicalControlFlowDoesNotAllowFallback_ThrowsInsteadOfSynthesizingPlacementMask()
    {
        InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();

        try
        {
            var microOp = new SyntheticMaskMicroOp(
                placement: new SlotPlacementMetadata
                {
                    RequiredSlotClass = SlotClass.BranchControl,
                    PinningKind = SlotPinningKind.ClassFlexible,
                    PinnedLaneId = 0,
                    DomainTag = 0
                },
                canonicalDecodePublication: CanonicalDecodePublicationMode.SelfPublishes,
                isControlFlow: true);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => InstructionRegistry.ComputeSafetyMask(0, microOp, memoryDomainId: 0));

            Assert.Contains("explicit structural safety mask", exception.Message);
            Assert.Equal(0UL, InstructionRegistry.ResourceMaskStructuralFallbackCount);
            Assert.Equal(0UL, InstructionRegistry.PlacementStructuralFallbackCount);
        }
        finally
        {
            InstructionRegistry.ResetSafetyFallbackTelemetryForTesting();
        }
    }
}
