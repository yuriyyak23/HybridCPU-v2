using System;

namespace YAKSys_Hybrid_CPU.Core.Execution.DmaStreamCompute
{
    public enum DmaStreamComputeValidationFault
    {
        None = 0,
        DescriptorDecodeFault = 1,
        UnsupportedAbiVersion = 2,
        UnsupportedOperation = 3,
        UnsupportedElementType = 4,
        UnsupportedShape = 5,
        DescriptorCarrierDecodeFault = 6,
        DescriptorReferenceLost = 7,
        ReservedFieldFault = 8,
        RangeOverflow = 9,
        AlignmentFault = 10,
        ZeroLengthFault = 11,
        AliasOverlapFault = 12,
        OwnerDomainFault = 13,
        QuotaAdmissionReject = 14,
        ExecutionDisabled = 15,
        BackpressureAdmissionReject = 16,
        TokenCapAdmissionReject = 17,
        UnsupportedCapability = 18,
        MalformedExtension = 19,
        FootprintNormalizationFault = 20,
        AddressSpaceFault = 21,
        UnderApproximatedFootprintFault = 22
    }

    public sealed record DmaStreamComputeValidationResult
    {
        private DmaStreamComputeValidationResult(
            DmaStreamComputeValidationFault fault,
            DmaStreamComputeDescriptor? descriptor,
            string message)
        {
            Fault = fault;
            Descriptor = descriptor;
            Message = message;
        }

        public bool IsValid => Fault == DmaStreamComputeValidationFault.None && Descriptor is not null;

        public DmaStreamComputeValidationFault Fault { get; }

        public DmaStreamComputeDescriptor? Descriptor { get; }

        public string Message { get; }

        public static DmaStreamComputeValidationResult Valid(DmaStreamComputeDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return new DmaStreamComputeValidationResult(
                DmaStreamComputeValidationFault.None,
                descriptor,
                "Descriptor accepted after owner/domain guard; execution remains disabled.");
        }

        public static DmaStreamComputeValidationResult Fail(
            DmaStreamComputeValidationFault fault,
            string message)
        {
            if (fault == DmaStreamComputeValidationFault.None)
            {
                throw new ArgumentException(
                    "Use Valid for successful descriptor validation.",
                    nameof(fault));
            }

            return new DmaStreamComputeValidationResult(fault, null, message);
        }

        public DmaStreamComputeDescriptor RequireDescriptorForAdmission()
        {
            if (IsValid && Descriptor is not null)
            {
                return Descriptor;
            }

            throw new InvalidOperationException(
                $"DmaStreamCompute descriptor is not admissible: {Fault}. {Message}");
        }
    }
}
