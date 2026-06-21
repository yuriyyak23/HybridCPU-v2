using System;
using System.Buffers.Binary;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute;

namespace HybridCPU_ISE.Tests.TestHelpers;

internal static class DmaStreamComputeTestDescriptorFactory
{
    internal const ulong IdentityHash = 0xA11CE5EEDUL;
    private const int HeaderSize = 128;
    private const int RangeEntrySize = 16;
    private const int RangeAddressFieldOffset = 0;
    private const int RangeLengthFieldOffset = 8;
    private const int HeaderAbiVersionOffset = 4;
    private const int HeaderOperationOffset = 40;
    private const int HeaderElementTypeOffset = 42;
    private const int HeaderShapeOffset = 44;

    internal static byte[] BuildDescriptor(
        DmaStreamComputeOperationKind operation = DmaStreamComputeOperationKind.Add,
        DmaStreamComputeElementType elementType = DmaStreamComputeElementType.UInt32,
        DmaStreamComputeShapeKind shape = DmaStreamComputeShapeKind.Contiguous1D)
    {
        ushort sourceRangeCount = operation switch
        {
            DmaStreamComputeOperationKind.Copy => 1,
            DmaStreamComputeOperationKind.Fma => 3,
            _ => 2
        };

        const ushort destinationRangeCount = 1;
        int sourceRangeTableOffset = HeaderSize;
        int destinationRangeTableOffset = HeaderSize + (sourceRangeCount * RangeEntrySize);
        uint totalSize = (uint)(HeaderSize + ((sourceRangeCount + destinationRangeCount) * RangeEntrySize));
        byte[] bytes = new byte[totalSize];

        WriteUInt32(bytes, 0, DmaStreamComputeDescriptorParser.Magic);
        WriteUInt16(bytes, HeaderAbiVersionOffset, DmaStreamComputeDescriptorParser.CurrentAbiVersion);
        WriteUInt16(bytes, 6, HeaderSize);
        WriteUInt32(bytes, 8, totalSize);
        WriteUInt64(bytes, 24, IdentityHash);
        WriteUInt64(bytes, 32, 0xC011EC7EUL);
        WriteUInt16(bytes, HeaderOperationOffset, (ushort)operation);
        WriteUInt16(bytes, HeaderElementTypeOffset, (ushort)elementType);
        WriteUInt16(bytes, HeaderShapeOffset, (ushort)shape);
        WriteUInt16(bytes, 46, (ushort)DmaStreamComputeRangeEncoding.InlineContiguous);
        WriteUInt16(bytes, 48, sourceRangeCount);
        WriteUInt16(bytes, 50, destinationRangeCount);
        WriteUInt16(bytes, 56, (ushort)DmaStreamComputePartialCompletionPolicy.AllOrNone);
        WriteUInt32(bytes, 64, 77);
        WriteUInt32(bytes, 68, 1);
        WriteUInt32(bytes, 72, 2);
        WriteUInt32(bytes, 76, DmaStreamComputeDescriptor.CanonicalLane6DeviceId);
        WriteUInt64(bytes, 80, 0xD0A11);
        WriteUInt32(bytes, 96, (uint)sourceRangeTableOffset);
        WriteUInt32(bytes, 100, (uint)destinationRangeTableOffset);

        for (int i = 0; i < sourceRangeCount; i++)
        {
            WriteRange(bytes, sourceRangeTableOffset + (i * RangeEntrySize), 0x1000UL + ((ulong)i * 0x1000UL), 16);
        }

        WriteRange(bytes, destinationRangeTableOffset, 0x9000, 16);
        return bytes;
    }

    internal static DmaStreamComputeDescriptorReference CreateReference(
        byte[] descriptorBytes,
        ulong descriptorAddress = 0x8000,
        ulong descriptorIdentityHash = IdentityHash)
    {
        ArgumentNullException.ThrowIfNull(descriptorBytes);
        return new DmaStreamComputeDescriptorReference(
            descriptorAddress,
            (uint)descriptorBytes.Length,
            descriptorIdentityHash);
    }

    internal static DmaStreamComputeOwnerGuardDecision CreateGuardDecision(
        byte[] descriptorBytes,
        DmaStreamComputeDescriptorReference? descriptorReference = null)
    {
        DmaStreamComputeStructuralReadResult structuralRead =
            DmaStreamComputeDescriptorParser.ReadStructuralOwnerBinding(
                descriptorBytes,
                descriptorReference);
        if (!structuralRead.IsValid)
        {
            throw new InvalidOperationException(structuralRead.Message);
        }

        DmaStreamComputeOwnerBinding ownerBinding =
            structuralRead.RequireOwnerBindingForGuard();
        var context = new DmaStreamComputeOwnerGuardContext(
            ownerBinding.OwnerVirtualThreadId,
            ownerBinding.OwnerContextId,
            ownerBinding.OwnerCoreId,
            ownerBinding.OwnerPodId,
            ownerBinding.OwnerDomainTag,
            activeDomainCertificate: ownerBinding.OwnerDomainTag,
            deviceId: ownerBinding.DeviceId);

        return new SafetyVerifier().EvaluateDmaStreamComputeOwnerGuard(
            ownerBinding,
            context);
    }

    internal static DmaStreamComputeDescriptor CreateDescriptor()
    {
        byte[] descriptorBytes = BuildDescriptor();
        DmaStreamComputeDescriptorReference reference = CreateReference(descriptorBytes);
        DmaStreamComputeValidationResult validation =
            DmaStreamComputeDescriptorParser.Parse(
                descriptorBytes,
                CreateGuardDecision(descriptorBytes, reference),
                reference);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return validation.RequireDescriptorForAdmission();
    }

    private static void WriteRange(byte[] bytes, int offset, ulong address, ulong length)
    {
        WriteUInt64(bytes, offset + RangeAddressFieldOffset, address);
        WriteUInt64(bytes, offset + RangeLengthFieldOffset, length);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
}
