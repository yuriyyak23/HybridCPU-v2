using System;
using HybridCPU_ISE.Arch;
using YAKSys_Hybrid_CPU.Arch;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU.Compiler.Core.IR;

internal static class CompilerVectorTransferEmissionLowerer
{
    public static CompilerVectorTransferEmissionPlan Lower(CompilerVectorTransferEmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        VLIW_Instruction instruction = BuildInstruction(request);
        return CreatePlan(request, instruction);
    }

    public static bool TryRecoverFromInstruction(
        InstructionsEnum opcode,
        in VLIW_Instruction instruction,
        out CompilerVectorTransferEmissionPlan? plan)
    {
        plan = null;
        if (!CompilerVectorTransferPositiveEmissionAbiContract.IsVectorTransferPositiveOpcode(opcode))
        {
            return false;
        }

        CompilerVectorTransferEmissionRequest request = RecoverRequest(opcode, in instruction);
        plan = CreatePlan(request, instruction);
        return true;
    }

    private static CompilerVectorTransferEmissionPlan CreatePlan(
        CompilerVectorTransferEmissionRequest request,
        in VLIW_Instruction instruction)
    {
        CompilerVectorTransferPositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(request.Mnemonic);

        OpcodeInfo? opcodeInfo = OpcodeRegistry.GetInfo((uint)request.Opcode);
        if (!opcodeInfo.HasValue || opcodeInfo.Value.InstructionClass != InstructionClass.Memory)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission requires runtime memory opcode authority.");
        }

        if (opcodeInfo.Value.OpCode != (uint)request.Opcode)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission request does not match the runtime opcode identity.");
        }

        ValidateProjectionMatchesTypedRequest(request, instruction, opcodeInfo.Value);

        return new CompilerVectorTransferEmissionPlan(
            request,
            CompilerVectorTransferPositiveEmissionAbiContract.RuntimeHandoffReference,
            opcodeInfo.Value,
            instruction,
            UsesFallbackPath: false,
            UsesAliasPromotion: false,
            UsesScalarVectorDotOrBackendFallback: false,
            UsesBaseMemoryFallback: false,
            UsesBaseVectorFallback: false,
            UsesScalarHelperFallback: false,
            UsesWideningFmaFallback: false,
            UsesVectorTransposeOrSegmentFallback: false);
    }

    private static void ValidateRequest(CompilerVectorTransferEmissionRequest request)
    {
        request.Shape.Validate(nameof(request.Shape));
        CompilerVectorTransferPositiveEmissionAbiContract.RequireRuntimeHandoffAuthority(request.Mnemonic);
    }

    private static VLIW_Instruction BuildInstruction(CompilerVectorTransferEmissionRequest request)
    {
        CompilerVectorTransferShapeAbi shape = request.Shape;
        ulong destinationPointer = request.Destination.BaseAddress;
        ulong sourcePointer = request.Source.BaseAddress;

        return new VLIW_Instruction
        {
            OpCode = (uint)request.Opcode,
            DataTypeValue = shape.ElementType,
            PredicateMask = shape.PredicateMask,
            Immediate = 0,
            DestSrc1Pointer = request.Kind == CompilerVectorTransferPositiveEmissionKind.Vload
                ? destinationPointer
                : sourcePointer,
            Src2Pointer = request.Kind == CompilerVectorTransferPositiveEmissionKind.Vload
                ? sourcePointer
                : destinationPointer,
            StreamLength = shape.ElementCount,
            Stride = shape.StrideBytes,
            RowStride = 0,
            Indexed = false,
            Is2D = false,
            Reduction = false,
            TailAgnostic = false,
            MaskAgnostic = false,
            VirtualThreadId = 0
        };
    }

    private static CompilerVectorTransferEmissionRequest RecoverRequest(
        InstructionsEnum opcode,
        in VLIW_Instruction instruction)
    {
        ValidateRecoveredInstructionShape(opcode, instruction);

        CompilerVectorTransferShapeAbi shape = CompilerVectorTransferShapeAbi.Create(
            instruction.DataTypeValue,
            instruction.StreamLength,
            instruction.Stride,
            instruction.PredicateMask);

        CompilerVectorTransferMemoryAddressAbi destination =
            CompilerVectorTransferMemoryAddressAbi.Create(
                opcode == InstructionsEnum.VLOAD
                    ? instruction.DestSrc1Pointer
                    : instruction.Src2Pointer);
        CompilerVectorTransferMemoryAddressAbi source =
            CompilerVectorTransferMemoryAddressAbi.Create(
                opcode == InstructionsEnum.VLOAD
                    ? instruction.Src2Pointer
                    : instruction.DestSrc1Pointer);

        return opcode == InstructionsEnum.VLOAD
            ? CompilerVectorTransferEmissionRequest.Vload(destination, source, shape)
            : CompilerVectorTransferEmissionRequest.Vstore(source, destination, shape);
    }

    private static void ValidateProjectionMatchesTypedRequest(
        CompilerVectorTransferEmissionRequest request,
        in VLIW_Instruction instruction,
        OpcodeInfo opcodeInfo)
    {
        if (instruction.OpCode != (uint)request.Opcode ||
            instruction.DataTypeValue != request.Shape.ElementType ||
            instruction.PredicateMask != request.Shape.PredicateMask ||
            instruction.StreamLength != request.Shape.ElementCount ||
            instruction.Stride != request.Shape.StrideBytes ||
            instruction.RowStride != 0 ||
            instruction.Indexed ||
            instruction.Is2D ||
            instruction.Reduction ||
            instruction.TailAgnostic ||
            instruction.MaskAgnostic)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission did not round-trip through the direct vector-transfer helper ABI.");
        }

        switch (request.Kind)
        {
            case CompilerVectorTransferPositiveEmissionKind.Vload:
                if (instruction.DestSrc1Pointer != request.Destination.BaseAddress ||
                    instruction.Src2Pointer != request.Source.BaseAddress)
                {
                    throw new InvalidOperationException(
                        "VLOAD compiler emission lost destination/source address identity.");
                }
                break;
            case CompilerVectorTransferPositiveEmissionKind.Vstore:
                if (instruction.DestSrc1Pointer != request.Source.BaseAddress ||
                    instruction.Src2Pointer != request.Destination.BaseAddress)
                {
                    throw new InvalidOperationException(
                        "VSTORE compiler emission lost source/destination address identity.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "Unknown vector transfer compiler helper kind.");
        }

        if (opcodeInfo.InstructionClass != InstructionClass.Memory)
        {
            throw new InvalidOperationException(
                $"{request.Mnemonic} compiler emission requires memory-class runtime opcode authority.");
        }
    }

    private static void ValidateRecoveredInstructionShape(
        InstructionsEnum opcode,
        in VLIW_Instruction instruction)
    {
        if (opcode is not (InstructionsEnum.VLOAD or InstructionsEnum.VSTORE))
        {
            throw new InvalidOperationException(
                $"Opcode {(ushort)opcode} is not a direct vector transfer helper opcode.");
        }

        if (instruction.StreamLength == 0)
        {
            throw new InvalidOperationException(
                $"Opcode {(ushort)opcode} reached the vector-transfer compiler lowerer with StreamLength == 0. " +
                "The typed helper requires a non-empty transfer shape and must fail closed instead of materializing a hidden no-op contour.");
        }

        if (instruction.Stride == 0)
        {
            throw new InvalidOperationException(
                $"Opcode {(ushort)opcode} reached the vector-transfer compiler lowerer with Stride == 0. " +
                "The typed helper requires an explicit stride and must fail closed instead of reopening the legacy contiguous fallback.");
        }

        if (instruction.Indexed || instruction.Is2D)
        {
            string addressingContour = instruction.Indexed && instruction.Is2D
                ? "indexed+2D"
                : instruction.Indexed
                    ? "indexed"
                    : "2D";

            throw new InvalidOperationException(
                $"Opcode {(ushort)opcode} reached the vector-transfer compiler lowerer with unsupported {addressingContour} addressing. " +
                "The typed helper only publishes the 1D transfer contour and must fail closed instead of reopening a compat surface.");
        }
    }
}
