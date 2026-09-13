using System;
using System.Text.Json;
using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core.ControlFlow;

namespace HybridCPU_ISE.Tests;

public sealed class PhaseAuditEntryPointRelocationTests
{
    [Fact]
    public void EntryPointAddress_WhenRedefinedWithDifferentAddress_ThrowsTypedDefinitionFault()
    {
        Processor.EntryPoint entryPoint = default;
        entryPoint.EntryPoint_Address = 0x100;

        EntryPointDefinitionException exception =
            Assert.Throws<EntryPointDefinitionException>(
                () => entryPoint.EntryPoint_Address = 0x200);

        Assert.Equal(nameof(Processor.EntryPoint.EntryPoint_Address), exception.Operation);
        Assert.Equal(0x100UL, exception.ExistingAddress);
        Assert.Equal(0x200UL, exception.RequestedAddress);
        Assert.Equal(0x100UL, entryPoint.EntryPoint_Address);
    }

    [Theory]
    [InlineData(Processor.EntryPoint.EntryPointType.Return, "Return")]
    [InlineData(Processor.EntryPoint.EntryPointType.InterruptReturn, "InterruptReturn")]
    public void AddAddress_WhenEntryPointKindHasNoPatchSite_ThrowsTypedUnsupportedFault(
        Processor.EntryPoint.EntryPointType entryPointType,
        string expectedKind)
    {
        Processor.EntryPoint entryPoint = default;
        entryPoint.Type = entryPointType;

        UnsupportedEntryPointOperationException exception =
            Assert.Throws<UnsupportedEntryPointOperationException>(
                () => entryPoint.AddAddress(0x180));

        Assert.Equal(expectedKind, exception.EntryPointKind);
        Assert.Equal(nameof(Processor.EntryPoint.AddAddress), exception.Operation);
    }

    [Fact]
    public void EntryPointCompilerFixup_WhenTargetIsDeclared_RecordsRelocationAndDoesNotPatchMainMemory()
    {
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Compiler);

        long originalLength = Processor.MainMemory.Length;
        long originalPosition = Processor.MainMemory.Position;

        try
        {
            Processor.MainMemory.SetLength(0x400);
            byte[] marker = [0xA5, 0x5A, 0xC3, 0x3C, 0x11, 0x22, 0x33, 0x44];
            Processor.MainMemory.Position = 0x110;
            Processor.MainMemory.Write(marker, 0, marker.Length);

            Processor.EntryPoint entryPoint = default;
            entryPoint.SymbolName = "target.entry";
            entryPoint.Type = Processor.EntryPoint.EntryPointType.Jump;
            entryPoint.AddAddress(0x120);

            Processor.MainMemory.Position = 0x200;
            Processor.CPU_Cores[0].ENTRY_POINT(ref entryPoint);

            RelocationEntry relocation = Assert.Single(entryPoint.RelocationEntries);
            Assert.Equal(RelocationKind.AbsoluteJump, relocation.Kind);
            Assert.Equal(0x110UL, relocation.PatchAddress);
            Assert.Equal(0x100UL, relocation.BundlePc);
            Assert.Equal(8, relocation.PatchWidth);
            Assert.Equal("target.entry", relocation.TargetSymbol);
            Assert.Equal(0x200UL, relocation.ResolvedTargetAddress);

            Processor.MainMemory.Position = 0x110;
            byte[] observed = new byte[marker.Length];
            Assert.Equal(marker.Length, Processor.MainMemory.Read(observed, 0, observed.Length));
            Assert.Equal(marker, observed);
        }
        finally
        {
            Processor.MainMemory.SetLength(originalLength);
            Processor.MainMemory.Position = Math.Min(originalPosition, Processor.MainMemory.Length);
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void RelocationEntry_WhenSerialized_RoundTripsWithoutRuntimePipelineState()
    {
        RelocationEntry relocation = RelocationEntry.CreateLegacyAbsolute64(
            RelocationKind.AbsoluteCall,
            emissionCursor: 0x220,
            targetSymbol: "callee",
            resolvedTargetAddress: 0x400);

        string json = JsonSerializer.Serialize(relocation);
        RelocationEntry roundTripped = JsonSerializer.Deserialize<RelocationEntry>(json);

        Assert.Equal(relocation, roundTripped);
    }

    [Fact]
    public void RelocationEncodingRule_WhenCursorCannotReachPatchField_ThrowsTypedFault()
    {
        RelocationEncodingException exception =
            Assert.Throws<RelocationEncodingException>(
                () => EntryPointRelocationEncodingRules.ResolveLegacyAbsoluteTargetPatchAddress(8));

        Assert.Equal(8UL, exception.EmissionCursor);
        Assert.Equal(
            EntryPointRelocationEncodingRules.LegacyAbsoluteTargetPatchBackOffsetBytes,
            exception.RequiredBackOffsetBytes);
    }

    [Fact]
    public void EntryPointRelocationLinker_WhenApplyingPublishedRelocation_PatchesClonedProgramImageDeterministically()
    {
        ProcessorMode originalMode = Processor.CurrentProcessorMode;
        _ = new Processor(ProcessorMode.Compiler);
        long originalPosition = Processor.MainMemory.Position;

        try
        {
            Processor.EntryPoint entryPoint = default;
            entryPoint.SymbolName = "target.entry";
            entryPoint.Type = Processor.EntryPoint.EntryPointType.Jump;
            entryPoint.AddAddress(0x120);

            Processor.MainMemory.Position = 0x200;
            Processor.CPU_Cores[0].ENTRY_POINT(ref entryPoint);

            byte[] programImage = new byte[0x80];
            for (int index = 0; index < programImage.Length; index++)
            {
                programImage[index] = (byte)(0x40 + index);
            }

            byte[] originalImage = (byte[])programImage.Clone();
            RelocationEntry relocation = Assert.Single(entryPoint.RelocationEntries);

            byte[] linkedImage =
                EntryPointRelocationLinker.ApplyRelocations(
                    programImage,
                    imageBaseAddress: 0x100,
                    entryPoint.RelocationEntries);

            Assert.Equal(originalImage, programImage);
            Assert.Equal(0x200UL, BitConverter.ToUInt64(linkedImage, 0x10));
            Assert.Equal(originalImage[0x0F], linkedImage[0x0F]);
            Assert.Equal(originalImage[0x18], linkedImage[0x18]);
            Assert.Equal("target.entry", relocation.TargetSymbol);
            Assert.Equal(0x110UL, relocation.PatchAddress);
        }
        finally
        {
            Processor.MainMemory.Position = originalPosition;
            Processor.CurrentProcessorMode = originalMode;
        }
    }

    [Fact]
    public void EntryPointRelocationLinker_WhenPatchFallsOutsideImage_ThrowsTypedFault()
    {
        RelocationEntry relocation = RelocationEntry.CreateLegacyAbsolute64(
            RelocationKind.AbsoluteJump,
            emissionCursor: 0x120,
            targetSymbol: "target.entry",
            resolvedTargetAddress: 0x200);

        RelocationPatchOutOfRangeException exception =
            Assert.Throws<RelocationPatchOutOfRangeException>(
                () => EntryPointRelocationLinker.ApplyRelocations(
                    new byte[0x10],
                    imageBaseAddress: 0x100,
                    new[] { relocation }));

        Assert.Equal(0x100UL, exception.ImageBaseAddress);
        Assert.Equal(0x110UL, exception.PatchAddress);
        Assert.Equal(RelocationEntry.Absolute64PatchWidth, exception.PatchWidth);
        Assert.Equal("target.entry", exception.TargetSymbol);
    }

    [Fact]
    public void EntryPointRelocationLinker_WhenPatchWidthDoesNotMatchEncoding_ThrowsTypedFault()
    {
        RelocationEntry relocation = RelocationEntry.CreateLegacyAbsolute64(
            RelocationKind.AbsoluteCall,
            emissionCursor: 0x120,
            targetSymbol: "callee",
            resolvedTargetAddress: 0x280) with
        {
            PatchWidth = 4
        };

        UnsupportedRelocationApplicationException exception =
            Assert.Throws<UnsupportedRelocationApplicationException>(
                () => EntryPointRelocationLinker.ApplyRelocations(
                    new byte[0x40],
                    imageBaseAddress: 0x100,
                    new[] { relocation }));

        Assert.Equal(RelocationEncodingKind.LegacyAbsolute64, exception.EncodingKind);
        Assert.Equal((byte)4, exception.PatchWidth);
    }

    [Theory]
    [InlineData(true, "Call")]
    [InlineData(false, "Interrupt")]
    public void PopControlFlowStack_WhenEmpty_ThrowsTypedUnderflowInsteadOfReturningZero(
        bool callStack,
        string expectedStackName)
    {
        _ = new Processor(ProcessorMode.Emulation);

        ControlFlowStackUnderflowException exception =
            Assert.Throws<ControlFlowStackUnderflowException>(
                () =>
                {
                    if (callStack)
                    {
                        Processor.CPU_Cores[0].Pop_Call_EntryPoint_Address();
                    }
                    else
                    {
                        Processor.CPU_Cores[0].Pop_Interrupt_EntryPoint_Address();
                    }
                });

        Assert.Equal(expectedStackName, exception.StackName);
    }
}
