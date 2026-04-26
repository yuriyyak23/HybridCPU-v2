using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests.Phase14;

/// <summary>
/// REF-14: dependency summaries must fail closed when producer-side register facts
/// outgrow the 64-bit transport masks used by decoded bundle dependency transport.
/// </summary>
public sealed class Phase14DependencyMaskRegisterRangeTests
{
    [Fact]
    public void TransportFacts_Throws_WhenReadRegisterIdExceedsDependencyMaskCapacity()
    {
        IReadOnlyList<MicroOp?> carrierBundle =
            CreateCarrierBundle(readRegisters: [ArchRegId.DependencyMaskBitCount]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc: 0x1000,
                carrierBundle));

        Assert.Contains("slot 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("read register id 64", exception.Message, StringComparison.Ordinal);
        Assert.Contains("wider bitset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TransportFacts_Throws_WhenWriteRegisterIdExceedsDependencyMaskCapacity()
    {
        IReadOnlyList<MicroOp?> carrierBundle =
            CreateCarrierBundle(
                readRegisters: Array.Empty<int>(),
                writeRegisters: [ArchRegId.DependencyMaskBitCount],
                writesRegister: true);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc: 0x1000,
                carrierBundle));

        Assert.Contains("slot 0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("write register id 64", exception.Message, StringComparison.Ordinal);
        Assert.Contains("wider bitset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TransportFacts_AcceptsBoundaryRegisterId63()
    {
        IReadOnlyList<MicroOp?> carrierBundle =
            CreateCarrierBundle(
                readRegisters: [ArchRegId.DependencyMaskBitCount - 1],
                writeRegisters: [ArchRegId.DependencyMaskBitCount - 1],
                writesRegister: true);

        DecodedBundleTransportFacts facts =
            DecodedBundleSlotCarrierBuilder.BuildTransportFacts(
                pc: 0x1000,
                carrierBundle);

        Assert.True(facts.DependencySummary.HasValue);

        DecodedBundleDependencySummary dependencySummary = facts.DependencySummary.Value;
        ulong expectedBoundaryBit = 1UL << (ArchRegId.DependencyMaskBitCount - 1);

        Assert.Equal(expectedBoundaryBit, dependencySummary.ReadRegisterMask);
        Assert.Equal(expectedBoundaryBit, dependencySummary.WriteRegisterMask);
    }

    [Fact]
    public void ArchRegisterCount_FitsDependencyMaskCapacity()
    {
        Assert.True(
            ArchRegId.RegisterCount <= ArchRegId.DependencyMaskBitCount,
            $"Architectural register count ({ArchRegId.RegisterCount}) exceeds dependency-mask capacity " +
            $"({ArchRegId.DependencyMaskBitCount}). Migrate decoded dependency summaries to a wider bitset.");
    }

    private static IReadOnlyList<MicroOp?> CreateCarrierBundle(
        IReadOnlyList<int> readRegisters,
        IReadOnlyList<int>? writeRegisters = null,
        bool writesRegister = false)
    {
        var microOp = new DependencyMaskContractMicroOp(
            readRegisters,
            writeRegisters ?? Array.Empty<int>(),
            writesRegister);

        return [microOp];
    }

    private sealed class DependencyMaskContractMicroOp : MicroOp
    {
        public DependencyMaskContractMicroOp(
            IReadOnlyList<int> readRegisters,
            IReadOnlyList<int> writeRegisters,
            bool writesRegister)
        {
            OpCode = (uint)Processor.CPU_Core.InstructionsEnum.Move;
            VirtualThreadId = 0;
            OwnerThreadId = 0;
            WritesRegister = writesRegister;
            ReadRegisters = readRegisters;
            WriteRegisters = writeRegisters;
            ReadMemoryRanges = Array.Empty<(ulong Address, ulong Length)>();
            WriteMemoryRanges = Array.Empty<(ulong Address, ulong Length)>();
            ResourceMask = ResourceBitset.Zero;
            SetClassFlexiblePlacement(SlotClass.AluClass);
        }

        public override CanonicalDecodePublicationMode CanonicalDecodePublication =>
            CanonicalDecodePublicationMode.SelfPublishes;

        public override bool Execute(ref Processor.CPU_Core core)
        {
            return true;
        }

        public override string GetDescription()
        {
            return "DependencyMaskContractMicroOp";
        }
    }
}
