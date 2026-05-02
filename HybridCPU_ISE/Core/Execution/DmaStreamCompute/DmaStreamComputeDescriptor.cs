using System;
using System.Collections.Generic;
using YAKSys_Hybrid_CPU.Core;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeOperationKind : ushort
    {
        Copy = 1,
        Add = 2,
        Mul = 3,
        Fma = 4,
        Reduce = 5
    }

    public enum DmaStreamComputeElementType : ushort
    {
        UInt8 = 1,
        UInt16 = 2,
        UInt32 = 3,
        UInt64 = 4,
        Float32 = 5,
        Float64 = 6
    }

    public enum DmaStreamComputeShapeKind : ushort
    {
        Contiguous1D = 1,
        FixedReduce = 2
    }

    public enum DmaStreamComputeRangeEncoding : ushort
    {
        InlineContiguous = 1
    }

    public enum DmaStreamComputePartialCompletionPolicy : ushort
    {
        AllOrNone = 1
    }

    public enum DmaStreamComputeAliasPolicy : ushort
    {
        Disjoint = 0,
        ExactInPlaceSnapshot = 1
    }

    public readonly record struct DmaStreamComputeDescriptorReference
    {
        public DmaStreamComputeDescriptorReference(
            ulong descriptorAddress,
            uint descriptorSize,
            ulong descriptorIdentityHash)
        {
            DescriptorAddress = descriptorAddress;
            DescriptorSize = descriptorSize;
            DescriptorIdentityHash = descriptorIdentityHash;
        }

        public ulong DescriptorAddress { get; }

        public uint DescriptorSize { get; }

        public ulong DescriptorIdentityHash { get; }
    }

    public readonly record struct DmaStreamComputeMemoryRange(ulong Address, ulong Length);

    public sealed record DmaStreamComputeOwnerBinding
    {
        public required ushort OwnerVirtualThreadId { get; init; }

        public required uint OwnerContextId { get; init; }

        public required uint OwnerCoreId { get; init; }

        public required uint OwnerPodId { get; init; }

        public required ulong OwnerDomainTag { get; init; }

        public required uint DeviceId { get; init; }
    }

    public readonly record struct DmaStreamComputeOwnerGuardContext
    {
        public DmaStreamComputeOwnerGuardContext(
            ushort ownerVirtualThreadId,
            uint ownerContextId,
            uint ownerCoreId,
            uint ownerPodId,
            ulong ownerDomainTag,
            ulong activeDomainCertificate,
            uint deviceId = DmaStreamComputeDescriptor.CanonicalLane6DeviceId)
        {
            OwnerVirtualThreadId = ownerVirtualThreadId;
            OwnerContextId = ownerContextId;
            OwnerCoreId = ownerCoreId;
            OwnerPodId = ownerPodId;
            OwnerDomainTag = ownerDomainTag;
            ActiveDomainCertificate = activeDomainCertificate;
            DeviceId = deviceId;
        }

        public ushort OwnerVirtualThreadId { get; init; }

        public uint OwnerContextId { get; init; }

        public uint OwnerCoreId { get; init; }

        public uint OwnerPodId { get; init; }

        public ulong OwnerDomainTag { get; init; }

        public ulong ActiveDomainCertificate { get; init; }

        public uint DeviceId { get; init; }
    }

    public readonly record struct DmaStreamComputeOwnerGuardDecision
    {
        private DmaStreamComputeOwnerGuardDecision(
            DmaStreamComputeOwnerBinding descriptorOwnerBinding,
            DmaStreamComputeOwnerGuardContext runtimeOwnerContext,
            LegalityDecision legalityDecision,
            string message)
        {
            DescriptorOwnerBinding = descriptorOwnerBinding;
            RuntimeOwnerContext = runtimeOwnerContext;
            LegalityDecision = legalityDecision;
            Message = message;
        }

        public DmaStreamComputeOwnerBinding? DescriptorOwnerBinding { get; }

        public DmaStreamComputeOwnerGuardContext RuntimeOwnerContext { get; }

        public LegalityDecision LegalityDecision { get; }

        public string Message { get; }

        public bool IsAllowed => LegalityDecision.IsAllowed;

        internal static DmaStreamComputeOwnerGuardDecision Allow(
            DmaStreamComputeOwnerBinding descriptorOwnerBinding,
            DmaStreamComputeOwnerGuardContext runtimeOwnerContext,
            string message)
        {
            ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);
            return new DmaStreamComputeOwnerGuardDecision(
                descriptorOwnerBinding,
                runtimeOwnerContext,
                LegalityDecision.Allow(
                    LegalityAuthoritySource.GuardPlane,
                    attemptedReplayCertificateReuse: false),
                message);
        }

        internal static DmaStreamComputeOwnerGuardDecision Reject(
            DmaStreamComputeOwnerBinding descriptorOwnerBinding,
            DmaStreamComputeOwnerGuardContext runtimeOwnerContext,
            RejectKind rejectKind,
            string message)
        {
            ArgumentNullException.ThrowIfNull(descriptorOwnerBinding);
            return new DmaStreamComputeOwnerGuardDecision(
                descriptorOwnerBinding,
                runtimeOwnerContext,
                LegalityDecision.Reject(
                    rejectKind,
                    CertificateRejectDetail.None,
                    LegalityAuthoritySource.GuardPlane,
                    attemptedReplayCertificateReuse: false),
                message);
        }
    }

    public sealed record DmaStreamComputeStructuralReadResult
    {
        private DmaStreamComputeStructuralReadResult(
            DmaStreamComputeValidationFault fault,
            DmaStreamComputeDescriptorReference descriptorReference,
            ulong descriptorIdentityHash,
            uint totalSize,
            DmaStreamComputeOwnerBinding? ownerBinding,
            string message)
        {
            Fault = fault;
            DescriptorReference = descriptorReference;
            DescriptorIdentityHash = descriptorIdentityHash;
            TotalSize = totalSize;
            OwnerBinding = ownerBinding;
            Message = message;
        }

        public bool IsValid => Fault == DmaStreamComputeValidationFault.None && OwnerBinding is not null;

        public DmaStreamComputeValidationFault Fault { get; }

        public DmaStreamComputeDescriptorReference DescriptorReference { get; }

        public ulong DescriptorIdentityHash { get; }

        public uint TotalSize { get; }

        public DmaStreamComputeOwnerBinding? OwnerBinding { get; }

        public string Message { get; }

        public static DmaStreamComputeStructuralReadResult Valid(
            DmaStreamComputeDescriptorReference descriptorReference,
            ulong descriptorIdentityHash,
            uint totalSize,
            DmaStreamComputeOwnerBinding ownerBinding)
        {
            ArgumentNullException.ThrowIfNull(ownerBinding);
            return new DmaStreamComputeStructuralReadResult(
                DmaStreamComputeValidationFault.None,
                descriptorReference,
                descriptorIdentityHash,
                totalSize,
                ownerBinding,
                "Structural owner fields located; descriptor is not executable.");
        }

        public static DmaStreamComputeStructuralReadResult Fail(
            DmaStreamComputeValidationFault fault,
            string message)
        {
            if (fault == DmaStreamComputeValidationFault.None)
            {
                throw new ArgumentException(
                    "Use Valid for successful structural descriptor reads.",
                    nameof(fault));
            }

            return new DmaStreamComputeStructuralReadResult(
                fault,
                default,
                descriptorIdentityHash: 0,
                totalSize: 0,
                ownerBinding: null,
                message);
        }

        public DmaStreamComputeOwnerBinding RequireOwnerBindingForGuard()
        {
            if (IsValid && OwnerBinding is not null)
            {
                return OwnerBinding;
            }

            throw new InvalidOperationException(
                $"DmaStreamCompute structural owner read failed: {Fault}. {Message}");
        }
    }

    public sealed record DmaStreamComputeDescriptor
    {
        public const uint CanonicalLane6DeviceId = 6;

        public required DmaStreamComputeDescriptorReference DescriptorReference { get; init; }

        public required ushort AbiVersion { get; init; }

        public required ushort HeaderSize { get; init; }

        public required uint TotalSize { get; init; }

        public required ulong DescriptorIdentityHash { get; init; }

        public required ulong CertificateInputHash { get; init; }

        public required DmaStreamComputeOperationKind Operation { get; init; }

        public required DmaStreamComputeElementType ElementType { get; init; }

        public required DmaStreamComputeShapeKind Shape { get; init; }

        public required DmaStreamComputeRangeEncoding RangeEncoding { get; init; }

        public required DmaStreamComputePartialCompletionPolicy PartialCompletionPolicy { get; init; }

        public required DmaStreamComputeOwnerBinding OwnerBinding { get; init; }

        public required DmaStreamComputeOwnerGuardDecision OwnerGuardDecision { get; init; }

        public required IReadOnlyList<DmaStreamComputeMemoryRange> ReadMemoryRanges { get; init; }

        public required IReadOnlyList<DmaStreamComputeMemoryRange> NormalizedReadMemoryRanges { get; init; }

        public required IReadOnlyList<DmaStreamComputeMemoryRange> WriteMemoryRanges { get; init; }

        public required IReadOnlyList<DmaStreamComputeMemoryRange> NormalizedWriteMemoryRanges { get; init; }

        public required DmaStreamComputeAliasPolicy AliasPolicy { get; init; }

        public required ulong NormalizedFootprintHash { get; init; }
    }
}
