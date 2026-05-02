using System;
using System.Buffers.Binary;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public sealed record DmaStreamComputeExecutionResult
    {
        public DmaStreamComputeExecutionResult(
            DmaStreamComputeToken token,
            DmaStreamComputeCommitResult completion,
            DmaStreamComputeBackendTelemetry telemetry)
        {
            ArgumentNullException.ThrowIfNull(token);
            ArgumentNullException.ThrowIfNull(completion);
            ArgumentNullException.ThrowIfNull(telemetry);

            Token = token;
            Completion = completion;
            Telemetry = telemetry;
        }

        public DmaStreamComputeToken Token { get; }

        public DmaStreamComputeCommitResult Completion { get; }

        public DmaStreamComputeBackendTelemetry Telemetry { get; }

        public bool IsCommitPending =>
            Token.State == DmaStreamComputeTokenState.CommitPending &&
            !Completion.RequiresRetireExceptionPublication;

        public bool RequiresRetireExceptionPublication =>
            Completion.RequiresRetireExceptionPublication;
    }

    public static class DmaStreamComputeRuntime
    {
        // Explicit runtime/model helper. This method is intentionally not wired
        // into DmaStreamComputeMicroOp.Execute or any hidden StreamEngine/DMA path.
        public static DmaStreamComputeExecutionResult ExecuteToCommitPending(
            DmaStreamComputeDescriptor descriptor,
            ulong tokenId = 0,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            return ExecuteToCommitPending(
                descriptor,
                new DmaStreamAcceleratorBackend(Processor.MainMemory, telemetry),
                tokenId,
                telemetry);
        }

        public static DmaStreamComputeExecutionResult ExecuteToCommitPending(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamAcceleratorBackend backend,
            ulong tokenId = 0,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(backend);
            backend.AttachTelemetry(telemetry);

            var token = new DmaStreamComputeToken(
                descriptor,
                tokenId == 0 ? descriptor.DescriptorIdentityHash : tokenId,
                telemetry);
            token.MarkIssued();

            if (!TryValidateRuntimeDescriptor(descriptor, token, out DmaStreamComputeCommitResult validationFault))
            {
                return CreateResult(token, validationFault, backend);
            }

            if (!TryReadOperands(
                    descriptor,
                    token,
                    backend,
                    out byte[][] operands,
                    out DmaStreamComputeCommitResult readFault))
            {
                return CreateResult(token, readFault, backend);
            }

            token.MarkReadsComplete();

            if (!TryCompute(
                    descriptor,
                    token,
                    operands,
                    backend,
                    out byte[] output,
                    out DmaStreamComputeCommitResult computeFault))
            {
                return CreateResult(token, computeFault, backend);
            }

            DmaStreamComputeCommitResult stageResult =
                StageOutput(descriptor, token, backend, output);
            if (stageResult.RequiresRetireExceptionPublication)
            {
                return CreateResult(token, stageResult, backend);
            }

            DmaStreamComputeCommitResult completion = token.MarkComputeComplete();
            return CreateResult(token, completion, backend);
        }

        private static DmaStreamComputeExecutionResult CreateResult(
            DmaStreamComputeToken token,
            DmaStreamComputeCommitResult completion,
            DmaStreamAcceleratorBackend backend) =>
            new(token, completion, backend.SnapshotTelemetry());

        private static bool TryValidateRuntimeDescriptor(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            out DmaStreamComputeCommitResult fault)
        {
            fault = null!;

            if (descriptor.RangeEncoding != DmaStreamComputeRangeEncoding.InlineContiguous)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,
                    "DmaStreamCompute runtime only supports inline contiguous range encoding in Phase 07.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            if (ResolveElementSize(descriptor.ElementType) == 0)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,
                    "DmaStreamCompute runtime reached an unsupported element type.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            if (GetExpectedOperandCount(descriptor.Operation) == 0)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,
                    "DmaStreamCompute runtime reached an unsupported operation.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            if (descriptor.ReadMemoryRanges is null ||
                descriptor.ReadMemoryRanges.Count != GetExpectedOperandCount(descriptor.Operation) ||
                descriptor.WriteMemoryRanges is null ||
                descriptor.WriteMemoryRanges.Count == 0)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.DescriptorDecodeFault,
                    "DmaStreamCompute runtime descriptor range counts do not match the Phase 07 execution contract.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            if (descriptor.Operation == DmaStreamComputeOperationKind.Reduce &&
                descriptor.Shape is not (DmaStreamComputeShapeKind.FixedReduce or DmaStreamComputeShapeKind.Contiguous1D))
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,
                    "DmaStreamCompute runtime reached an unsupported reduction shape.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            return true;
        }

        private static bool TryReadOperands(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            DmaStreamAcceleratorBackend backend,
            out byte[][] operands,
            out DmaStreamComputeCommitResult fault)
        {
            operands = Array.Empty<byte[]>();
            fault = null!;

            int operandCount = descriptor.ReadMemoryRanges.Count;
            operands = new byte[operandCount][];
            for (int i = 0; i < operandCount; i++)
            {
                DmaStreamComputeMemoryRange range = descriptor.ReadMemoryRanges[i];
                if (range.Length > int.MaxValue)
                {
                    fault = token.PublishFault(
                        DmaStreamComputeTokenFaultKind.MemoryFault,
                        $"DmaStreamCompute runtime source range length {range.Length} exceeds the materializable Phase 07 buffer limit.",
                        range.Address,
                        isWrite: false);
                    return false;
                }

                byte[] buffer = new byte[(int)range.Length];
                if (!backend.TryReadRange(range, buffer, out string message))
                {
                    fault = token.PublishFault(
                        DmaStreamComputeTokenFaultKind.MemoryFault,
                        message,
                        range.Address,
                        isWrite: false);
                    return false;
                }

                operands[i] = buffer;
            }

            return true;
        }

        private static bool TryCompute(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            DmaStreamAcceleratorBackend backend,
            out byte[] output,
            out DmaStreamComputeCommitResult fault)
        {
            output = Array.Empty<byte>();
            fault = null!;

            int elementSize = ResolveElementSize(descriptor.ElementType);
            if (!TryGetTotalWriteBytes(descriptor, out ulong totalWriteBytes) ||
                totalWriteBytes > int.MaxValue ||
                totalWriteBytes % (ulong)elementSize != 0)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute runtime write footprint is not exactly materializable as complete elements.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: true);
                return false;
            }

            switch (descriptor.Operation)
            {
                case DmaStreamComputeOperationKind.Copy:
                    return TryComputeCopy(descriptor, token, operands, totalWriteBytes, backend, out output, out fault);

                case DmaStreamComputeOperationKind.Add:
                case DmaStreamComputeOperationKind.Mul:
                    return TryComputeBinary(descriptor, token, operands, totalWriteBytes, backend, out output, out fault);

                case DmaStreamComputeOperationKind.Fma:
                    return TryComputeFma(descriptor, token, operands, totalWriteBytes, backend, out output, out fault);

                case DmaStreamComputeOperationKind.Reduce:
                    return TryComputeReduce(descriptor, token, operands, totalWriteBytes, backend, out output, out fault);

                default:
                    fault = token.PublishFault(
                        DmaStreamComputeTokenFaultKind.UnsupportedAbiOrOperation,
                        "DmaStreamCompute runtime reached an unsupported operation.",
                        descriptor.DescriptorReference.DescriptorAddress,
                        isWrite: false);
                    return false;
            }
        }

        private static bool TryComputeCopy(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            ulong totalWriteBytes,
            DmaStreamAcceleratorBackend backend,
            out byte[] output,
            out DmaStreamComputeCommitResult fault)
        {
            output = Array.Empty<byte>();
            fault = null!;

            if (operands.Length != 1 || (ulong)operands[0].Length != totalWriteBytes)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute copy requires source and destination byte footprints to match exactly.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: true);
                return false;
            }

            output = new byte[(int)totalWriteBytes];
            operands[0].CopyTo(output, 0);
            int elementSize = ResolveElementSize(descriptor.ElementType);
            backend.RecordComputeElements(totalWriteBytes / (ulong)elementSize, descriptor.Operation);
            return true;
        }

        private static bool TryComputeBinary(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            ulong totalWriteBytes,
            DmaStreamAcceleratorBackend backend,
            out byte[] output,
            out DmaStreamComputeCommitResult fault)
        {
            output = Array.Empty<byte>();
            fault = null!;

            if (!ValidateOperandByteLengths(descriptor, token, operands, totalWriteBytes, out fault))
            {
                return false;
            }

            int elementSize = ResolveElementSize(descriptor.ElementType);
            ulong elementCount = totalWriteBytes / (ulong)elementSize;
            output = new byte[(int)totalWriteBytes];
            for (ulong i = 0; i < elementCount; i++)
            {
                int offset = checked((int)(i * (ulong)elementSize));
                if (IsFloatingElement(descriptor.ElementType))
                {
                    double left = LoadFloat(operands[0], offset, descriptor.ElementType);
                    double right = LoadFloat(operands[1], offset, descriptor.ElementType);
                    double value = descriptor.Operation == DmaStreamComputeOperationKind.Add
                        ? left + right
                        : left * right;
                    StoreFloat(output, offset, descriptor.ElementType, value);
                }
                else
                {
                    ulong left = LoadUnsigned(operands[0], offset, descriptor.ElementType);
                    ulong right = LoadUnsigned(operands[1], offset, descriptor.ElementType);
                    ulong value = descriptor.Operation == DmaStreamComputeOperationKind.Add
                        ? unchecked(left + right)
                        : unchecked(left * right);
                    StoreUnsigned(output, offset, descriptor.ElementType, value);
                }
            }

            backend.RecordComputeElements(elementCount, descriptor.Operation);
            return true;
        }

        private static bool TryComputeFma(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            ulong totalWriteBytes,
            DmaStreamAcceleratorBackend backend,
            out byte[] output,
            out DmaStreamComputeCommitResult fault)
        {
            output = Array.Empty<byte>();
            fault = null!;

            if (!ValidateOperandByteLengths(descriptor, token, operands, totalWriteBytes, out fault))
            {
                return false;
            }

            int elementSize = ResolveElementSize(descriptor.ElementType);
            ulong elementCount = totalWriteBytes / (ulong)elementSize;
            output = new byte[(int)totalWriteBytes];
            for (ulong i = 0; i < elementCount; i++)
            {
                int offset = checked((int)(i * (ulong)elementSize));
                if (IsFloatingElement(descriptor.ElementType))
                {
                    double a = LoadFloat(operands[0], offset, descriptor.ElementType);
                    double b = LoadFloat(operands[1], offset, descriptor.ElementType);
                    double c = LoadFloat(operands[2], offset, descriptor.ElementType);
                    StoreFloat(output, offset, descriptor.ElementType, (a * b) + c);
                }
                else
                {
                    ulong a = LoadUnsigned(operands[0], offset, descriptor.ElementType);
                    ulong b = LoadUnsigned(operands[1], offset, descriptor.ElementType);
                    ulong c = LoadUnsigned(operands[2], offset, descriptor.ElementType);
                    StoreUnsigned(output, offset, descriptor.ElementType, unchecked((a * b) + c));
                }
            }

            backend.RecordComputeElements(elementCount, descriptor.Operation);
            return true;
        }

        private static bool TryComputeReduce(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            ulong totalWriteBytes,
            DmaStreamAcceleratorBackend backend,
            out byte[] output,
            out DmaStreamComputeCommitResult fault)
        {
            output = Array.Empty<byte>();
            fault = null!;

            int elementSize = ResolveElementSize(descriptor.ElementType);
            if (operands.Length != 1 ||
                operands[0].Length == 0 ||
                operands[0].Length % elementSize != 0 ||
                totalWriteBytes != (ulong)elementSize)
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute reduce requires one complete source vector and one scalar destination element.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: true);
                return false;
            }

            ulong elementCount = (ulong)(operands[0].Length / elementSize);
            output = new byte[elementSize];
            if (IsFloatingElement(descriptor.ElementType))
            {
                double accumulator = 0;
                for (ulong i = 0; i < elementCount; i++)
                {
                    accumulator += LoadFloat(
                        operands[0],
                        checked((int)(i * (ulong)elementSize)),
                        descriptor.ElementType);
                }

                StoreFloat(output, 0, descriptor.ElementType, accumulator);
            }
            else
            {
                ulong accumulator = 0;
                for (ulong i = 0; i < elementCount; i++)
                {
                    accumulator = unchecked(accumulator + LoadUnsigned(
                        operands[0],
                        checked((int)(i * (ulong)elementSize)),
                        descriptor.ElementType));
                }

                StoreUnsigned(output, 0, descriptor.ElementType, accumulator);
            }

            backend.RecordComputeElements(elementCount, descriptor.Operation);
            return true;
        }

        private static bool ValidateOperandByteLengths(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            byte[][] operands,
            ulong totalWriteBytes,
            out DmaStreamComputeCommitResult fault)
        {
            fault = null!;
            if (operands.Length != GetExpectedOperandCount(descriptor.Operation))
            {
                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.DescriptorDecodeFault,
                    "DmaStreamCompute runtime operand count changed after descriptor acceptance.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: false);
                return false;
            }

            for (int i = 0; i < operands.Length; i++)
            {
                if ((ulong)operands[i].Length == totalWriteBytes)
                {
                    continue;
                }

                fault = token.PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute element-wise operation requires every source footprint to match the destination footprint exactly.",
                    descriptor.ReadMemoryRanges[i].Address,
                    isWrite: false);
                return false;
            }

            return true;
        }

        private static DmaStreamComputeCommitResult StageOutput(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeToken token,
            DmaStreamAcceleratorBackend backend,
            byte[] output)
        {
            int offset = 0;
            try
            {
                for (int i = 0; i < descriptor.WriteMemoryRanges.Count; i++)
                {
                    DmaStreamComputeMemoryRange range = descriptor.WriteMemoryRanges[i];
                    if (range.Length > int.MaxValue ||
                        offset > output.Length - (int)range.Length)
                    {
                        return token.PublishFault(
                            DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                            "DmaStreamCompute runtime output buffer does not exactly cover the descriptor destination footprint.",
                            range.Address,
                            isWrite: true);
                    }

                    token.StageDestinationWrite(
                        range.Address,
                        output.AsSpan(offset, (int)range.Length));
                    backend.RecordStagedWrite(range.Length);
                    offset += (int)range.Length;
                }
            }
            catch (InvalidOperationException)
            {
                return DmaStreamComputeCommitResult.Faulted(token.LastFault!);
            }

            if (offset != output.Length)
            {
                return token.PublishFault(
                    DmaStreamComputeTokenFaultKind.PartialCompletionFault,
                    "DmaStreamCompute runtime produced extra bytes outside the accepted destination footprint.",
                    descriptor.DescriptorReference.DescriptorAddress,
                    isWrite: true);
            }

            return DmaStreamComputeCommitResult.Pending(token.State);
        }

        private static bool TryGetTotalWriteBytes(
            DmaStreamComputeDescriptor descriptor,
            out ulong totalWriteBytes)
        {
            totalWriteBytes = 0;
            if (descriptor.WriteMemoryRanges is null || descriptor.WriteMemoryRanges.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < descriptor.WriteMemoryRanges.Count; i++)
            {
                ulong length = descriptor.WriteMemoryRanges[i].Length;
                if (length == 0 || totalWriteBytes > ulong.MaxValue - length)
                {
                    return false;
                }

                totalWriteBytes += length;
            }

            return true;
        }

        private static int GetExpectedOperandCount(DmaStreamComputeOperationKind operation) =>
            operation switch
            {
                DmaStreamComputeOperationKind.Copy => 1,
                DmaStreamComputeOperationKind.Add => 2,
                DmaStreamComputeOperationKind.Mul => 2,
                DmaStreamComputeOperationKind.Fma => 3,
                DmaStreamComputeOperationKind.Reduce => 1,
                _ => 0
            };

        private static int ResolveElementSize(DmaStreamComputeElementType elementType) =>
            elementType switch
            {
                DmaStreamComputeElementType.UInt8 => 1,
                DmaStreamComputeElementType.UInt16 => 2,
                DmaStreamComputeElementType.UInt32 => 4,
                DmaStreamComputeElementType.UInt64 => 8,
                DmaStreamComputeElementType.Float32 => 4,
                DmaStreamComputeElementType.Float64 => 8,
                _ => 0
            };

        private static bool IsFloatingElement(DmaStreamComputeElementType elementType) =>
            elementType is DmaStreamComputeElementType.Float32 or DmaStreamComputeElementType.Float64;

        private static ulong LoadUnsigned(
            ReadOnlySpan<byte> buffer,
            int offset,
            DmaStreamComputeElementType elementType) =>
            elementType switch
            {
                DmaStreamComputeElementType.UInt8 => buffer[offset],
                DmaStreamComputeElementType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(offset, 2)),
                DmaStreamComputeElementType.UInt32 => BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4)),
                DmaStreamComputeElementType.UInt64 => BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset, 8)),
                _ => throw new InvalidOperationException("Unsigned load reached a non-unsigned DmaStreamCompute element type.")
            };

        private static void StoreUnsigned(
            Span<byte> buffer,
            int offset,
            DmaStreamComputeElementType elementType,
            ulong value)
        {
            switch (elementType)
            {
                case DmaStreamComputeElementType.UInt8:
                    buffer[offset] = (byte)value;
                    break;
                case DmaStreamComputeElementType.UInt16:
                    BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(offset, 2), (ushort)value);
                    break;
                case DmaStreamComputeElementType.UInt32:
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), (uint)value);
                    break;
                case DmaStreamComputeElementType.UInt64:
                    BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(offset, 8), value);
                    break;
                default:
                    throw new InvalidOperationException("Unsigned store reached a non-unsigned DmaStreamCompute element type.");
            }
        }

        private static double LoadFloat(
            ReadOnlySpan<byte> buffer,
            int offset,
            DmaStreamComputeElementType elementType) =>
            elementType switch
            {
                DmaStreamComputeElementType.Float32 => BitConverter.Int32BitsToSingle(
                    BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset, 4))),
                DmaStreamComputeElementType.Float64 => BitConverter.Int64BitsToDouble(
                    BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(offset, 8))),
                _ => throw new InvalidOperationException("Floating-point load reached a non-floating DmaStreamCompute element type.")
            };

        private static void StoreFloat(
            Span<byte> buffer,
            int offset,
            DmaStreamComputeElementType elementType,
            double value)
        {
            switch (elementType)
            {
                case DmaStreamComputeElementType.Float32:
                    BinaryPrimitives.WriteInt32LittleEndian(
                        buffer.Slice(offset, 4),
                        BitConverter.SingleToInt32Bits((float)value));
                    break;
                case DmaStreamComputeElementType.Float64:
                    BinaryPrimitives.WriteInt64LittleEndian(
                        buffer.Slice(offset, 8),
                        BitConverter.DoubleToInt64Bits(value));
                    break;
                default:
                    throw new InvalidOperationException("Floating-point store reached a non-floating DmaStreamCompute element type.");
            }
        }
    }
}
