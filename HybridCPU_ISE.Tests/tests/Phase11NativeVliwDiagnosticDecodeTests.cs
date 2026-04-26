using System;
using System.Collections.Generic;
using System.Text;
using HybridCPU.Compiler.Core;
using HybridCPU.Compiler.Core.IR;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Decoder;
using YAKSys_Hybrid_CPU.Core.Registers;
using Xunit;

namespace HybridCPU_ISE.Tests.tests;

public sealed class Phase11NativeVliwDiagnosticDecodeTests
{
    private const int MaxInstructions = 256;
    private const int MultiThreadRounds = 8;
    private const int MultiThreadSchedulingWindowRounds = 2;
    private const ulong LkProbeBase = 0x120000;

    [Fact]
    public void NativeVliwPackedScalarDiagnosticImage_RemainsCanonicalDecodeClean()
    {
        VLIW_Instruction[] instructions = new VLIW_Instruction[MaxInstructions];
        InstructionSlotMetadata[] metadata = new InstructionSlotMetadata[MaxInstructions];
        int count = EmitNativeVliwPackedScalarDiagnosticProgram(instructions, metadata);

        HybridCpuCompiledProgram compiledProgram = HybridCpuCanonicalCompiler.CompileProgram(
            0,
            new ReadOnlySpan<VLIW_Instruction>(instructions, 0, count),
            bundleAnnotations: BuildBundleAnnotations(metadata, count),
            frontendMode: FrontendMode.NativeVLIW);

        var decoder = new VliwDecoderV4();
        for (int bundleIndex = 0; bundleIndex < compiledProgram.BundleCount; bundleIndex++)
        {
            VLIW_Instruction[] bundle = ReadBundle(compiledProgram.ProgramImage, bundleIndex);
            try
            {
                _ = decoder.DecodeInstructionBundle(
                    bundle,
                    bundleAddress: (ulong)bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes,
                    bundleSerial: (ulong)bundleIndex);
            }
            catch (InvalidOpcodeException ex)
            {
                Assert.Fail(BuildDecodeFailureMessage(bundleIndex, bundle, ex));
            }
        }
    }

    private static int EmitNativeVliwPackedScalarDiagnosticProgram(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata)
    {
        int instructionCount = 0;
        for (int round = 0; round < MultiThreadRounds; round++)
        {
            EmitCryptoLikeRound(instructions, metadata, ref instructionCount, 0, 4, 12 + (((round + 0) & 0x3) * 4), round);
            EmitPolynomialLikeRound(instructions, metadata, ref instructionCount, 1, 4, 12 + (((round + 1) & 0x3) * 4), round);
            EmitAddressCalculationRound(instructions, metadata, ref instructionCount, 2, 4, 12 + (((round + 2) & 0x3) * 4), round);
            EmitBaselineScalarRound(instructions, metadata, ref instructionCount, 3, 4, 12 + (((round + 3) & 0x3) * 4), round);
            EmitPackedSpecLikeMemoryTrafficRound(instructions, metadata, ref instructionCount, LkProbeBase, round, bankStride: 3);

            if (ShouldEmitSchedulingWindowFence(round, MultiThreadRounds, MultiThreadSchedulingWindowRounds))
            {
                EmitFence(instructions, metadata, ref instructionCount, 0);
            }
        }

        EmitFence(instructions, metadata, ref instructionCount, 0);
        return instructionCount;
    }

    private static void EmitCryptoLikeRound(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int registerBase,
        int destinationRegisterBase,
        int round)
    {
        int rotateRegister = registerBase + 1 + (round & 0x3);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Addition, destinationRegisterBase, registerBase + 1, registerBase + 2);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.XOR, destinationRegisterBase + 1, registerBase + 3, registerBase + 4);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.ShiftLeft, destinationRegisterBase + 2, registerBase + 5, rotateRegister);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.AND, destinationRegisterBase + 3, registerBase + 6, registerBase + 7);
    }

    private static void EmitPolynomialLikeRound(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int registerBase,
        int destinationRegisterBase,
        int round)
    {
        int rotateRegister = registerBase + 1 + ((round + 1) & 0x3);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Multiplication, destinationRegisterBase, registerBase + 1, registerBase + 2);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Addition, destinationRegisterBase + 1, registerBase + 3, registerBase + 4);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Multiplication, destinationRegisterBase + 2, registerBase + 5, registerBase + 6);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Addition, destinationRegisterBase + 3, registerBase + 7, rotateRegister);
    }

    private static void EmitAddressCalculationRound(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int registerBase,
        int destinationRegisterBase,
        int round)
    {
        int rotateRegister = registerBase + 1 + (round & 0x3);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.ShiftLeft, destinationRegisterBase, rotateRegister, registerBase + 2);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Addition, destinationRegisterBase + 1, registerBase, registerBase + 3);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.XOR, destinationRegisterBase + 2, registerBase + 4, registerBase + 5);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.OR, destinationRegisterBase + 3, registerBase + 6, registerBase + 7);
    }

    private static void EmitBaselineScalarRound(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int registerBase,
        int destinationRegisterBase,
        int round)
    {
        int rotateRegister = registerBase + 1 + ((round + 2) & 0x3);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Addition, destinationRegisterBase, registerBase + 1, registerBase + 2);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.XOR, destinationRegisterBase + 1, registerBase + 3, rotateRegister);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.OR, destinationRegisterBase + 2, registerBase + 5, registerBase + 6);
        EmitBinaryScalar(instructions, metadata, ref instructionCount, virtualThreadId, Processor.CPU_Core.InstructionsEnum.Multiplication, destinationRegisterBase + 3, registerBase + 2, registerBase + 7);
    }

    private static void EmitPackedSpecLikeMemoryTrafficRound(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        ulong probeBase,
        int round,
        int bankStride)
    {
        int lineIndex = round & 0x1F;
        byte loadVirtualThreadId = (byte)(round & 0x3);
        byte storeVirtualThreadId = (byte)((round + 2) & 0x3);
        byte secondLoadVirtualThreadId = (byte)((round + 1) & 0x3);
        byte secondStoreVirtualThreadId = (byte)((round + 3) & 0x3);
        ulong loadThreadBase = probeBase + ((ulong)loadVirtualThreadId * 0x8000);
        ulong storeThreadBase = probeBase + ((ulong)storeVirtualThreadId * 0x8000) + 0x2000;
        ulong secondLoadThreadBase = probeBase + ((ulong)secondLoadVirtualThreadId * 0x8000);
        ulong secondStoreThreadBase = probeBase + ((ulong)secondStoreVirtualThreadId * 0x8000) + 0x2000;
        int loadBankId = ((round * bankStride) + loadVirtualThreadId + 1) & 0x7;
        int storeBankId = ((round * bankStride) + storeVirtualThreadId + 4) & 0x7;
        int secondLoadBankId = ((round * bankStride) + secondLoadVirtualThreadId + 2) & 0x7;
        int secondStoreBankId = ((round * bankStride) + secondStoreVirtualThreadId + 6) & 0x7;
        int loadRegister = 28 + (round & 0x3);
        int secondLoadRegister = 28 + ((round + 1) & 0x3);
        int storeSourceRegister = 5 + ((round + storeVirtualThreadId) & 0x3);
        int secondStoreSourceRegister = 5 + ((round + secondStoreVirtualThreadId + 1) & 0x3);
        int firstWordOffset = round & 0x1;
        int secondWordOffset = (round + 1) & 0x1;

        EmitTypedLoad(instructions, metadata, ref instructionCount, loadVirtualThreadId, loadRegister, 4, GetBankedAddress(loadThreadBase, loadBankId, lineIndex, firstWordOffset));
        EmitTypedStore(instructions, metadata, ref instructionCount, storeVirtualThreadId, 4, storeSourceRegister, GetBankedAddress(storeThreadBase, storeBankId, lineIndex, secondWordOffset));
        EmitTypedLoad(instructions, metadata, ref instructionCount, secondLoadVirtualThreadId, secondLoadRegister, 4, GetBankedAddress(secondLoadThreadBase, secondLoadBankId, lineIndex, secondWordOffset));
        EmitTypedStore(instructions, metadata, ref instructionCount, secondStoreVirtualThreadId, 4, secondStoreSourceRegister, GetBankedAddress(secondStoreThreadBase, secondStoreBankId, lineIndex, firstWordOffset));
    }

    private static void EmitTypedLoad(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int destinationRegisterId,
        int baseRegisterId,
        ulong address)
    {
        EmitInstruction(
            instructions,
            metadata,
            ref instructionCount,
            virtualThreadId,
            Processor.CPU_Core.InstructionsEnum.LD,
            0,
            0,
            VLIW_Instruction.PackArchRegs(checked((byte)destinationRegisterId), checked((byte)baseRegisterId), VLIW_Instruction.NoArchReg),
            address,
            0,
            0,
            canBeStolen: true);
    }

    private static void EmitTypedStore(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        int baseRegisterId,
        int sourceRegisterId,
        ulong address)
    {
        EmitInstruction(
            instructions,
            metadata,
            ref instructionCount,
            virtualThreadId,
            Processor.CPU_Core.InstructionsEnum.SD,
            0,
            0,
            VLIW_Instruction.PackArchRegs(VLIW_Instruction.NoArchReg, checked((byte)baseRegisterId), checked((byte)sourceRegisterId)),
            address,
            0,
            0,
            canBeStolen: true);
    }

    private static void EmitBinaryScalar(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        Processor.CPU_Core.InstructionsEnum instruction,
        int destinationRegisterId,
        int firstRegisterId,
        int secondRegisterId)
    {
        EmitInstruction(
            instructions,
            metadata,
            ref instructionCount,
            virtualThreadId,
            instruction,
            0,
            0,
            VLIW_Instruction.PackArchRegs(checked((byte)destinationRegisterId), checked((byte)firstRegisterId), checked((byte)secondRegisterId)),
            0,
            0,
            0,
            canBeStolen: true);
    }

    private static void EmitFence(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId)
    {
        EmitInstruction(
            instructions,
            metadata,
            ref instructionCount,
            virtualThreadId,
            Processor.CPU_Core.InstructionsEnum.FENCE,
            0,
            0,
            0,
            0,
            0,
            0,
            canBeStolen: false);
    }

    private static void EmitInstruction(
        VLIW_Instruction[] instructions,
        InstructionSlotMetadata[] metadata,
        ref int instructionCount,
        byte virtualThreadId,
        Processor.CPU_Core.InstructionsEnum instruction,
        byte dataType,
        ushort immediate,
        ulong destSrc1,
        ulong src2,
        ulong streamLength,
        ushort stride,
        bool canBeStolen)
    {
        instructions[instructionCount] = new VLIW_Instruction
        {
            OpCode = (uint)instruction,
            DataType = dataType,
            PredicateMask = 0,
            Immediate = immediate,
            DestSrc1Pointer = destSrc1,
            Src2Pointer = src2,
            StreamLength = (uint)streamLength,
            Stride = stride
        };
        metadata[instructionCount] = new InstructionSlotMetadata(
            VtId.Create(virtualThreadId),
            canBeStolen ? SlotMetadata.Default : SlotMetadata.NotStealable);
        instructionCount++;
    }

    private static VliwBundleAnnotations BuildBundleAnnotations(
        InstructionSlotMetadata[] metadata,
        int instructionCount)
    {
        var slotMetadata = new InstructionSlotMetadata[instructionCount];
        Array.Copy(metadata, slotMetadata, instructionCount);
        return new VliwBundleAnnotations(slotMetadata);
    }

    private static VLIW_Instruction[] ReadBundle(byte[] programImage, int bundleIndex)
    {
        var bundle = new VLIW_Instruction[BundleMetadata.BundleSlotCount];
        int bundleOffset = bundleIndex * HybridCpuBundleSerializer.BundleSizeBytes;
        for (int slotIndex = 0; slotIndex < bundle.Length; slotIndex++)
        {
            int offset = bundleOffset + (slotIndex * 32);
            if (!bundle[slotIndex].TryReadBytes(programImage, offset))
            {
                throw new InvalidOperationException($"Unable to decode bundle {bundleIndex}, slot {slotIndex}.");
            }
        }

        return bundle;
    }

    private static ulong GetBankedAddress(ulong baseAddress, int bankId, int lineIndex, int wordOffset)
    {
        const ulong cacheLineStride = 4096UL;
        const ulong wordStride = 8UL;
        return baseAddress + ((ulong)bankId * cacheLineStride) + ((ulong)lineIndex * 64UL) + ((ulong)wordOffset * wordStride);
    }

    private static bool ShouldEmitSchedulingWindowFence(int round, int totalRounds, int windowRounds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowRounds, 1);
        return round + 1 < totalRounds &&
               ((round + 1) % windowRounds) == 0;
    }

    private static string BuildDecodeFailureMessage(
        int bundleIndex,
        VLIW_Instruction[] bundle,
        InvalidOpcodeException exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Bundle {bundleIndex} failed canonical decode: {exception.Message}");
        for (int slotIndex = 0; slotIndex < bundle.Length; slotIndex++)
        {
            VLIW_Instruction slot = bundle[slotIndex];
            builder.AppendLine(
                $"slot {slotIndex}: opcode=0x{slot.OpCode:X} mnemonic={OpcodeRegistry.GetMnemonicOrHex(slot.OpCode)} " +
                $"dt={slot.DataType} imm=0x{slot.Immediate:X4} word1=0x{slot.Word1:X16} word2=0x{slot.Word2:X16} word3=0x{slot.Word3:X16}");
        }

        return builder.ToString();
    }
}
