using HybridCPU_ISE.Arch;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public static partial class DmaStreamComputeDescriptorParser
    {
        public const uint Magic = 0x31435344; // "DSC1" as a little-endian scalar.
        public const ushort CurrentAbiVersion = 1;
        public const int CurrentHeaderSize = 128;

        public static bool ExecutionEnabled => false;

        internal const string ExecutionDisabledProjectionMessage =
            "DmaStreamComputeMicroOp execution is disabled and must fail closed. " +
            "The lane6 typed-slot surface preserves descriptor and footprint evidence only; " +
            "DmaStreamComputeRuntime is an explicit runtime helper, not MicroOp.Execute, and no StreamEngine or DMAController fallback is implied.";

        private const int HeaderSize = CurrentHeaderSize;
        private const int RangeEntrySize = 16;

        private const int MagicOffset = 0;
        private const int AbiVersionOffset = 4;
        private const int HeaderSizeOffset = 6;
        private const int TotalSizeOffset = 8;
        private const int FlagsOffset = 12;
        private const int Reserved0Offset = 16;
        private const int DescriptorIdentityHashOffset = 24;
        private const int CertificateInputHashOffset = 32;
        private const int OperationOffset = 40;
        private const int ElementTypeOffset = 42;
        private const int ShapeOffset = 44;
        private const int RangeEncodingOffset = 46;
        private const int SourceRangeCountOffset = 48;
        private const int DestinationRangeCountOffset = 50;
        private const int MaskRangeCountOffset = 52;
        private const int AccumulatorRangeCountOffset = 54;
        private const int PartialCompletionPolicyOffset = 56;
        private const int RoundingModeOffset = 58;
        private const int OwnerVirtualThreadIdOffset = 60;
        private const int Reserved1Offset = 62;
        private const int OwnerContextIdOffset = 64;
        private const int OwnerCoreIdOffset = 68;
        private const int OwnerPodIdOffset = 72;
        private const int DeviceIdOffset = 76;
        private const int OwnerDomainTagOffset = 80;
        private const int Reserved2Offset = 88;
        private const int SourceRangeTableOffset = 96;
        private const int DestinationRangeTableOffset = 100;
        private const int MaskRangeTableOffset = 104;
        private const int AccumulatorRangeTableOffset = 108;
        private const int Reserved3Offset = 112;
        private const int Reserved4Offset = 120;

        public static DmaStreamComputeStructuralReadResult ReadStructuralOwnerBinding(
            ReadOnlySpan<byte> descriptorBytes,
            DmaStreamComputeDescriptorReference? descriptorReference = null)
        {
            if (descriptorBytes.Length < HeaderSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "Descriptor is shorter than the v1 fixed header.");
            }

            if (ReadUInt32(descriptorBytes, MagicOffset) != Magic)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "Descriptor magic does not match DmaStreamCompute v1.");
            }

            ushort abiVersion = ReadUInt16(descriptorBytes, AbiVersionOffset);
            if (abiVersion != CurrentAbiVersion)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.UnsupportedAbiVersion,
                    $"Unsupported DmaStreamCompute descriptor ABI version {abiVersion}.");
            }

            ushort headerSize = ReadUInt16(descriptorBytes, HeaderSizeOffset);
            if (headerSize != HeaderSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    $"Descriptor header size {headerSize} does not match v1 header size {HeaderSize}.");
            }

            uint totalSize = ReadUInt32(descriptorBytes, TotalSizeOffset);
            if (totalSize < HeaderSize || totalSize > descriptorBytes.Length)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "Descriptor total size is outside the provided descriptor buffer.");
            }

            ulong descriptorIdentityHash = ReadUInt64(descriptorBytes, DescriptorIdentityHashOffset);
            DmaStreamComputeDescriptorReference effectiveReference =
                descriptorReference ??
                new DmaStreamComputeDescriptorReference(
                    descriptorAddress: 0,
                    descriptorSize: totalSize,
                    descriptorIdentityHash: descriptorIdentityHash);

            if (effectiveReference.DescriptorSize != 0 &&
                effectiveReference.DescriptorSize < totalSize)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorReferenceLost,
                    "Descriptor sideband reference does not cover the validated descriptor payload.");
            }

            if (effectiveReference.DescriptorIdentityHash != 0 &&
                effectiveReference.DescriptorIdentityHash != descriptorIdentityHash)
            {
                return DmaStreamComputeStructuralReadResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorReferenceLost,
                    "Descriptor sideband identity hash does not match the validated payload.");
            }

            var ownerBinding = new DmaStreamComputeOwnerBinding
            {
                OwnerVirtualThreadId = ReadUInt16(descriptorBytes, OwnerVirtualThreadIdOffset),
                OwnerContextId = ReadUInt32(descriptorBytes, OwnerContextIdOffset),
                OwnerCoreId = ReadUInt32(descriptorBytes, OwnerCoreIdOffset),
                OwnerPodId = ReadUInt32(descriptorBytes, OwnerPodIdOffset),
                DeviceId = ReadUInt32(descriptorBytes, DeviceIdOffset),
                OwnerDomainTag = ReadUInt64(descriptorBytes, OwnerDomainTagOffset)
            };

            return DmaStreamComputeStructuralReadResult.Valid(
                effectiveReference,
                descriptorIdentityHash,
                totalSize,
                ownerBinding);
        }

        public static DmaStreamComputeValidationResult Parse(
            ReadOnlySpan<byte> descriptorBytes,
            DmaStreamComputeDescriptorReference? descriptorReference = null,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            telemetry?.RecordDescriptorParseAttempt();
            DmaStreamComputeStructuralReadResult structuralRead =
                ReadStructuralOwnerBinding(descriptorBytes, descriptorReference);
            if (!structuralRead.IsValid)
            {
                return Fail(
                    structuralRead.Fault,
                    structuralRead.Message,
                    telemetry);
            }

            return Fail(
                DmaStreamComputeValidationFault.OwnerDomainFault,
                "DmaStreamCompute descriptor cannot be accepted without an explicit owner/domain guard decision.",
                telemetry);
        }

        public static DmaStreamComputeValidationResult Parse(
            ReadOnlySpan<byte> descriptorBytes,
            DmaStreamComputeOwnerGuardDecision ownerGuardDecision,
            DmaStreamComputeDescriptorReference? descriptorReference = null,
            DmaStreamComputeTelemetryCounters? telemetry = null)
        {
            telemetry?.RecordDescriptorParseAttempt();
            DmaStreamComputeStructuralReadResult structuralRead =
                ReadStructuralOwnerBinding(descriptorBytes, descriptorReference);
            if (!structuralRead.IsValid)
            {
                return Fail(
                    structuralRead.Fault,
                    structuralRead.Message,
                    telemetry);
            }

            DmaStreamComputeOwnerBinding structuralOwnerBinding =
                structuralRead.RequireOwnerBindingForGuard();
            if (!ownerGuardDecision.IsAllowed)
            {
                telemetry?.RecordOwnerGuardRejected(structuralOwnerBinding, ownerGuardDecision);
                return DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    ownerGuardDecision.Message);
            }

            if (ownerGuardDecision.LegalityDecision.AuthoritySource != LegalityAuthoritySource.GuardPlane ||
                ownerGuardDecision.LegalityDecision.AttemptedReplayCertificateReuse)
            {
                return Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    "DmaStreamCompute owner guard decision must come from the guard plane before replay or certificate reuse.",
                    telemetry);
            }

            if (ownerGuardDecision.DescriptorOwnerBinding is null ||
                !ownerGuardDecision.DescriptorOwnerBinding.Equals(structuralOwnerBinding))
            {
                return Fail(
                    DmaStreamComputeValidationFault.OwnerDomainFault,
                    "DmaStreamCompute owner guard decision does not match structurally-read descriptor owner fields.",
                    telemetry);
            }

            ushort abiVersion = ReadUInt16(descriptorBytes, AbiVersionOffset);
            ushort headerSize = ReadUInt16(descriptorBytes, HeaderSizeOffset);
            uint totalSize = structuralRead.TotalSize;

            if (HasAnyReservedBitSet(descriptorBytes))
            {
                return Fail(
                    DmaStreamComputeValidationFault.ReservedFieldFault,
                    "DmaStreamCompute v1 descriptor reserved fields and flags must be zero.",
                    telemetry);
            }

            if (!TryDecodeOperation(
                ReadUInt16(descriptorBytes, OperationOffset),
                out DmaStreamComputeOperationKind operation))
            {
                return Fail(
                    DmaStreamComputeValidationFault.UnsupportedOperation,
                    "DmaStreamCompute descriptor operation is unsupported in v1.",
                    telemetry);
            }

            if (!TryDecodeElementType(
                ReadUInt16(descriptorBytes, ElementTypeOffset),
                out DmaStreamComputeElementType elementType,
                out int elementSize))
            {
                return Fail(
                    DmaStreamComputeValidationFault.UnsupportedElementType,
                    "DmaStreamCompute descriptor element type is unsupported in v1.",
                    telemetry);
            }

            if (!TryDecodeShape(
                ReadUInt16(descriptorBytes, ShapeOffset),
                operation,
                out DmaStreamComputeShapeKind shape))
            {
                return Fail(
                    DmaStreamComputeValidationFault.UnsupportedShape,
                    "DmaStreamCompute descriptor shape is unsupported or not legal for the operation.",
                    telemetry);
            }

            if (ReadUInt16(descriptorBytes, RangeEncodingOffset) !=
                (ushort)DmaStreamComputeRangeEncoding.InlineContiguous)
            {
                return Fail(
                    DmaStreamComputeValidationFault.UnsupportedShape,
                    "Only inline contiguous range tables are accepted by the v1 parser.",
                    telemetry);
            }

            if (ReadUInt16(descriptorBytes, PartialCompletionPolicyOffset) !=
                (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone)
            {
                return Fail(
                    DmaStreamComputeValidationFault.ReservedFieldFault,
                    "DmaStreamCompute v1 requires all-or-none partial completion policy.",
                    telemetry);
            }

            ushort sourceRangeCount = ReadUInt16(descriptorBytes, SourceRangeCountOffset);
            ushort destinationRangeCount = ReadUInt16(descriptorBytes, DestinationRangeCountOffset);
            ushort expectedSourceRangeCount = GetExpectedSourceRangeCount(operation);
            if (sourceRangeCount != expectedSourceRangeCount || destinationRangeCount == 0)
            {
                return Fail(
                    DmaStreamComputeValidationFault.DescriptorDecodeFault,
                    "Descriptor range counts do not match the v1 operation contract.",
                    telemetry);
            }

            uint sourceRangeOffset = ReadUInt32(descriptorBytes, SourceRangeTableOffset);
            uint destinationRangeOffset = ReadUInt32(descriptorBytes, DestinationRangeTableOffset);
            if (!TryReadRanges(
                descriptorBytes,
                totalSize,
                sourceRangeOffset,
                sourceRangeCount,
                elementSize,
                out IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
                out DmaStreamComputeValidationResult? readFailure))
            {
                telemetry?.RecordDescriptorRejected(readFailure!.Fault);
                return readFailure!;
            }

            if (!TryReadRanges(
                descriptorBytes,
                totalSize,
                destinationRangeOffset,
                destinationRangeCount,
                elementSize,
                out IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges,
                out DmaStreamComputeValidationResult? writeFailure))
            {
                telemetry?.RecordDescriptorRejected(writeFailure!.Fault);
                return writeFailure!;
            }

            IReadOnlyList<DmaStreamComputeMemoryRange> normalizedReadRanges =
                NormalizeMemoryRanges(readRanges);
            IReadOnlyList<DmaStreamComputeMemoryRange> normalizedWriteRanges =
                NormalizeMemoryRanges(writeRanges);
            if (!TryValidateAliasPolicy(
                readRanges,
                writeRanges,
                out DmaStreamComputeAliasPolicy aliasPolicy,
                out DmaStreamComputeValidationResult? aliasFailure))
            {
                telemetry?.RecordDescriptorRejected(aliasFailure!.Fault);
                return aliasFailure!;
            }

            var descriptor = new DmaStreamComputeDescriptor
            {
                DescriptorReference = structuralRead.DescriptorReference,
                AbiVersion = abiVersion,
                HeaderSize = headerSize,
                TotalSize = totalSize,
                DescriptorIdentityHash = structuralRead.DescriptorIdentityHash,
                CertificateInputHash = ReadUInt64(descriptorBytes, CertificateInputHashOffset),
                Operation = operation,
                ElementType = elementType,
                Shape = shape,
                RangeEncoding = DmaStreamComputeRangeEncoding.InlineContiguous,
                PartialCompletionPolicy = DmaStreamComputePartialCompletionPolicy.AllOrNone,
                OwnerBinding = structuralOwnerBinding,
                OwnerGuardDecision = ownerGuardDecision,
                ReadMemoryRanges = readRanges,
                NormalizedReadMemoryRanges = normalizedReadRanges,
                WriteMemoryRanges = writeRanges,
                NormalizedWriteMemoryRanges = normalizedWriteRanges,
                AliasPolicy = aliasPolicy,
                NormalizedFootprintHash = ComputeNormalizedFootprintHash(
                    operation,
                    elementType,
                    shape,
                    aliasPolicy,
                    normalizedReadRanges,
                    normalizedWriteRanges)
            };

            return Valid(descriptor, telemetry);
        }

        public static DmaStreamComputeValidationResult TryDecodeRawVliwCarrier(
            in VLIW_Instruction instruction,
            int slotIndex)
        {
            if (TryValidateNativeVliwCarrier(
                    in instruction,
                    slotIndex,
                    hasDescriptorSideband: false,
                    out DmaStreamComputeValidationResult? nativeFailure))
            {
                return DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault,
                    "DmaStreamCompute raw VLIW carrier unexpectedly validated without a typed descriptor sideband.");
            }

            return nativeFailure!;
        }

        public static bool TryValidateNativeVliwCarrier(
            in VLIW_Instruction instruction,
            int slotIndex,
            bool hasDescriptorSideband,
            out DmaStreamComputeValidationResult? failure)
        {
            if ((instruction.Word3 & VLIW_Instruction.RetiredPolicyGapMask) != 0)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault,
                    $"Slot {slotIndex}: word3[50] is a retired policy gap and cannot carry a descriptor ABI bit.");
                return false;
            }

            if (instruction.Reserved != 0)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault,
                    $"Slot {slotIndex}: word0[47:40] is reserved and cannot carry an unversioned descriptor ABI.");
                return false;
            }

            if (instruction.VirtualThreadId != 0)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault,
                    $"Slot {slotIndex}: word3[49:48] VirtualThreadId is a transport hint, not descriptor owner authority.");
                return false;
            }

            if (!hasDescriptorSideband)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.DescriptorCarrierDecodeFault,
                    "DmaStreamCompute descriptor references are accepted only through typed decoded sideband.");
                return false;
            }

            failure = null;
            return true;
        }

        private static DmaStreamComputeValidationResult Fail(
            DmaStreamComputeValidationFault fault,
            string message,
            DmaStreamComputeTelemetryCounters? telemetry)
        {
            telemetry?.RecordDescriptorRejected(fault);
            return DmaStreamComputeValidationResult.Fail(fault, message);
        }

        private static DmaStreamComputeValidationResult Valid(
            DmaStreamComputeDescriptor descriptor,
            DmaStreamComputeTelemetryCounters? telemetry)
        {
            telemetry?.RecordDescriptorAccepted(descriptor);
            return DmaStreamComputeValidationResult.Valid(descriptor);
        }

        private static bool HasAnyReservedBitSet(ReadOnlySpan<byte> bytes)
        {
            return ReadUInt32(bytes, FlagsOffset) != 0 ||
                   ReadUInt64(bytes, Reserved0Offset) != 0 ||
                   ReadUInt16(bytes, MaskRangeCountOffset) != 0 ||
                   ReadUInt16(bytes, AccumulatorRangeCountOffset) != 0 ||
                   ReadUInt16(bytes, RoundingModeOffset) != 0 ||
                   ReadUInt16(bytes, Reserved1Offset) != 0 ||
                   ReadUInt64(bytes, Reserved2Offset) != 0 ||
                   ReadUInt32(bytes, MaskRangeTableOffset) != 0 ||
                   ReadUInt32(bytes, AccumulatorRangeTableOffset) != 0 ||
                   ReadUInt64(bytes, Reserved3Offset) != 0 ||
                   ReadUInt64(bytes, Reserved4Offset) != 0;
        }

        private static bool TryDecodeOperation(
            ushort rawOperation,
            out DmaStreamComputeOperationKind operation)
        {
            operation = (DmaStreamComputeOperationKind)rawOperation;
            return operation is
                DmaStreamComputeOperationKind.Copy or
                DmaStreamComputeOperationKind.Add or
                DmaStreamComputeOperationKind.Mul or
                DmaStreamComputeOperationKind.Fma or
                DmaStreamComputeOperationKind.Reduce;
        }

        private static bool TryDecodeElementType(
            ushort rawElementType,
            out DmaStreamComputeElementType elementType,
            out int elementSize)
        {
            elementType = (DmaStreamComputeElementType)rawElementType;
            elementSize = elementType switch
            {
                DmaStreamComputeElementType.UInt8 => 1,
                DmaStreamComputeElementType.UInt16 => 2,
                DmaStreamComputeElementType.UInt32 => 4,
                DmaStreamComputeElementType.UInt64 => 8,
                DmaStreamComputeElementType.Float32 => 4,
                DmaStreamComputeElementType.Float64 => 8,
                _ => 0
            };

            return elementSize != 0;
        }

        private static bool TryDecodeShape(
            ushort rawShape,
            DmaStreamComputeOperationKind operation,
            out DmaStreamComputeShapeKind shape)
        {
            shape = (DmaStreamComputeShapeKind)rawShape;
            return shape == DmaStreamComputeShapeKind.Contiguous1D ||
                   (operation == DmaStreamComputeOperationKind.Reduce &&
                    shape == DmaStreamComputeShapeKind.FixedReduce);
        }

        private static ushort GetExpectedSourceRangeCount(DmaStreamComputeOperationKind operation) =>
            operation switch
            {
                DmaStreamComputeOperationKind.Copy => 1,
                DmaStreamComputeOperationKind.Add => 2,
                DmaStreamComputeOperationKind.Mul => 2,
                DmaStreamComputeOperationKind.Fma => 3,
                DmaStreamComputeOperationKind.Reduce => 1,
                _ => 0
            };

        private static bool TryReadRanges(
            ReadOnlySpan<byte> bytes,
            uint totalSize,
            uint tableOffset,
            ushort rangeCount,
            int elementSize,
            out IReadOnlyList<DmaStreamComputeMemoryRange> ranges,
            out DmaStreamComputeValidationResult? failure)
        {
            ranges = Array.Empty<DmaStreamComputeMemoryRange>();
            failure = null;

            if (rangeCount == 0)
            {
                return true;
            }

            if ((tableOffset & 0x7) != 0)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.AlignmentFault,
                    "Descriptor range table offset must be 8-byte aligned.");
                return false;
            }

            ulong tableBytes = (ulong)rangeCount * RangeEntrySize;
            ulong tableEnd = (ulong)tableOffset + tableBytes;
            if (tableEnd < tableOffset || tableEnd > totalSize)
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.RangeOverflow,
                    "Descriptor range table overflows the validated descriptor size.");
                return false;
            }

            var normalized = new DmaStreamComputeMemoryRange[rangeCount];
            for (int i = 0; i < rangeCount; i++)
            {
                int entryOffset = checked((int)tableOffset + (i * RangeEntrySize));
                ulong address = ReadUInt64(bytes, entryOffset);
                ulong length = ReadUInt64(bytes, entryOffset + sizeof(ulong));

                if (length == 0)
                {
                    failure = DmaStreamComputeValidationResult.Fail(
                        DmaStreamComputeValidationFault.ZeroLengthFault,
                        "DmaStreamCompute memory ranges must have non-zero length.");
                    return false;
                }

                if (address > ulong.MaxValue - length)
                {
                    failure = DmaStreamComputeValidationResult.Fail(
                        DmaStreamComputeValidationFault.RangeOverflow,
                        "DmaStreamCompute memory range address + length overflows UInt64.");
                    return false;
                }

                ulong alignment = (ulong)elementSize;
                if ((address % alignment) != 0 || (length % alignment) != 0)
                {
                    failure = DmaStreamComputeValidationResult.Fail(
                        DmaStreamComputeValidationFault.AlignmentFault,
                        "DmaStreamCompute memory range is not aligned to the element size.");
                    return false;
                }

                normalized[i] = new DmaStreamComputeMemoryRange(address, length);
            }

            ranges = normalized;
            return true;
        }

        private static IReadOnlyList<DmaStreamComputeMemoryRange> NormalizeMemoryRanges(
            IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            if (ranges is null || ranges.Count == 0)
            {
                return Array.Empty<DmaStreamComputeMemoryRange>();
            }

            if (ranges.Count == 1)
            {
                return ranges;
            }

            var sorted = new DmaStreamComputeMemoryRange[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                sorted[i] = ranges[i];
            }

            Array.Sort(
                sorted,
                static (left, right) =>
                {
                    int addressCompare = left.Address.CompareTo(right.Address);
                    return addressCompare != 0
                        ? addressCompare
                        : left.Length.CompareTo(right.Length);
                });

            var merged = new List<DmaStreamComputeMemoryRange>(sorted.Length);
            ulong currentStart = sorted[0].Address;
            ulong currentEnd = sorted[0].Address + sorted[0].Length;
            for (int i = 1; i < sorted.Length; i++)
            {
                DmaStreamComputeMemoryRange next = sorted[i];
                ulong nextEnd = next.Address + next.Length;
                if (next.Address <= currentEnd)
                {
                    if (nextEnd > currentEnd)
                    {
                        currentEnd = nextEnd;
                    }

                    continue;
                }

                merged.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
                currentStart = next.Address;
                currentEnd = nextEnd;
            }

            merged.Add(new DmaStreamComputeMemoryRange(currentStart, currentEnd - currentStart));
            return merged.ToArray();
        }

        private static bool TryValidateAliasPolicy(
            IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
            IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges,
            out DmaStreamComputeAliasPolicy aliasPolicy,
            out DmaStreamComputeValidationResult? failure)
        {
            aliasPolicy = DmaStreamComputeAliasPolicy.Disjoint;
            failure = null;

            if (HasAnyInternalOverlap(writeRanges))
            {
                failure = DmaStreamComputeValidationResult.Fail(
                    DmaStreamComputeValidationFault.AliasOverlapFault,
                    "DmaStreamCompute destination ranges overlap; v1 requires explicit non-overlapping writes.");
                return false;
            }

            for (int writeIndex = 0; writeIndex < writeRanges.Count; writeIndex++)
            {
                DmaStreamComputeMemoryRange writeRange = writeRanges[writeIndex];
                for (int readIndex = 0; readIndex < readRanges.Count; readIndex++)
                {
                    DmaStreamComputeMemoryRange readRange = readRanges[readIndex];
                    if (!RangesOverlap(readRange, writeRange))
                    {
                        continue;
                    }

                    if (RangesEqual(readRange, writeRange))
                    {
                        aliasPolicy = DmaStreamComputeAliasPolicy.ExactInPlaceSnapshot;
                        continue;
                    }

                    failure = DmaStreamComputeValidationResult.Fail(
                        DmaStreamComputeValidationFault.AliasOverlapFault,
                        "DmaStreamCompute source/destination overlap is only legal for exact in-place snapshot ranges.");
                    return false;
                }
            }

            return true;
        }

        private static bool HasAnyInternalOverlap(IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
        {
            if (ranges is null || ranges.Count <= 1)
            {
                return false;
            }

            var sorted = new DmaStreamComputeMemoryRange[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                sorted[i] = ranges[i];
            }

            Array.Sort(
                sorted,
                static (left, right) =>
                {
                    int addressCompare = left.Address.CompareTo(right.Address);
                    return addressCompare != 0
                        ? addressCompare
                        : left.Length.CompareTo(right.Length);
                });

            ulong previousEnd = sorted[0].Address + sorted[0].Length;
            for (int i = 1; i < sorted.Length; i++)
            {
                if (sorted[i].Address < previousEnd)
                {
                    return true;
                }

                ulong currentEnd = sorted[i].Address + sorted[i].Length;
                if (currentEnd > previousEnd)
                {
                    previousEnd = currentEnd;
                }
            }

            return false;
        }

        private static bool RangesOverlap(
            DmaStreamComputeMemoryRange left,
            DmaStreamComputeMemoryRange right)
        {
            ulong leftEnd = left.Address + left.Length;
            ulong rightEnd = right.Address + right.Length;
            return left.Address < rightEnd && right.Address < leftEnd;
        }

        private static bool RangesEqual(
            DmaStreamComputeMemoryRange left,
            DmaStreamComputeMemoryRange right) =>
            left.Address == right.Address && left.Length == right.Length;

        private static ulong ComputeNormalizedFootprintHash(
            DmaStreamComputeOperationKind operation,
            DmaStreamComputeElementType elementType,
            DmaStreamComputeShapeKind shape,
            DmaStreamComputeAliasPolicy aliasPolicy,
            IReadOnlyList<DmaStreamComputeMemoryRange> readRanges,
            IReadOnlyList<DmaStreamComputeMemoryRange> writeRanges)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offsetBasis;
            Add((ushort)operation);
            Add((ushort)elementType);
            Add((ushort)shape);
            Add((ushort)aliasPolicy);
            AddRanges(readRanges);
            AddRanges(writeRanges);
            return hash == 0 ? offsetBasis : hash;

            void Add(ulong value)
            {
                unchecked
                {
                    hash ^= value;
                    hash *= prime;
                }
            }

            void AddRanges(IReadOnlyList<DmaStreamComputeMemoryRange> ranges)
            {
                Add((ulong)ranges.Count);
                for (int i = 0; i < ranges.Count; i++)
                {
                    Add(ranges[i].Address);
                    Add(ranges[i].Length);
                }
            }
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, sizeof(ushort)));

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, sizeof(uint)));

        private static ulong ReadUInt64(ReadOnlySpan<byte> bytes, int offset) =>
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(offset, sizeof(ulong)));
    }
}
