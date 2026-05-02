using System;
using YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Capabilities;

namespace YAKSys_Hybrid_CPU.Core.Execution.ExternalAccelerators.Tokens;

public enum AcceleratorRegisterAbiWriteKind : byte
{
    NoWriteRejected = 0,
    WriteRegister = 1,
    NoWritePreciseFault = 2
}

public sealed record AcceleratorRegisterAbiResult
{
    private AcceleratorRegisterAbiResult(
        AcceleratorRegisterAbiWriteKind writeKind,
        ulong registerValue,
        AcceleratorTokenFaultCode faultCode,
        string message)
    {
        WriteKind = writeKind;
        RegisterValue = registerValue;
        FaultCode = faultCode;
        Message = message;
    }

    public AcceleratorRegisterAbiWriteKind WriteKind { get; }

    public bool WritesRegister => WriteKind == AcceleratorRegisterAbiWriteKind.WriteRegister;

    public bool RequiresPreciseFault => WriteKind == AcceleratorRegisterAbiWriteKind.NoWritePreciseFault;

    public bool IsRejectedNoWrite => WriteKind == AcceleratorRegisterAbiWriteKind.NoWriteRejected;

    public ulong RegisterValue { get; }

    public AcceleratorTokenFaultCode FaultCode { get; }

    public string Message { get; }

    public static AcceleratorRegisterAbiResult Write(
        ulong registerValue,
        string message,
        AcceleratorTokenFaultCode faultCode = AcceleratorTokenFaultCode.None) =>
        new(
            AcceleratorRegisterAbiWriteKind.WriteRegister,
            registerValue,
            faultCode,
            message);

    public static AcceleratorRegisterAbiResult NoWriteRejected(
        AcceleratorTokenFaultCode faultCode,
        string message) =>
        new(
            AcceleratorRegisterAbiWriteKind.NoWriteRejected,
            0,
            faultCode,
            message);

    public static AcceleratorRegisterAbiResult PreciseFaultNoWrite(
        AcceleratorTokenFaultCode faultCode,
        string message) =>
        new(
            AcceleratorRegisterAbiWriteKind.NoWritePreciseFault,
            0,
            faultCode,
            message);
}

public static class AcceleratorRegisterAbi
{
    // Model-side result packing only. Current SystemDeviceCommandMicroOp
    // carriers do not execute and do not perform architectural rd writeback.
    public static AcceleratorRegisterAbiResult FromSubmitAdmission(
        AcceleratorTokenAdmissionResult admissionResult)
    {
        ArgumentNullException.ThrowIfNull(admissionResult);

        if (admissionResult.IsAccepted)
        {
            return AcceleratorRegisterAbiResult.Write(
                admissionResult.Handle.Value,
                "ACCEL_SUBMIT rd receives a nonzero opaque token handle after guarded admission.");
        }

        if (admissionResult.RequiresPreciseFault)
        {
            return AcceleratorRegisterAbiResult.PreciseFaultNoWrite(
                admissionResult.FaultCode,
                "ACCEL_SUBMIT precise fault performs no architectural rd write.");
        }

        return AcceleratorRegisterAbiResult.Write(
            0,
            "ACCEL_SUBMIT non-trapping rejection writes zero to rd.",
            admissionResult.FaultCode);
    }

    public static AcceleratorRegisterAbiResult FromStatusLookup(
        AcceleratorTokenLookupResult lookupResult)
    {
        ArgumentNullException.ThrowIfNull(lookupResult);

        if (!lookupResult.IsAllowed)
        {
            return AcceleratorRegisterAbiResult.NoWriteRejected(
                lookupResult.FaultCode,
                "ACCEL token status rd is written only after guarded token lookup succeeds.");
        }

        return AcceleratorRegisterAbiResult.Write(
            lookupResult.PackedStatusWord,
            "ACCEL_POLL/WAIT/CANCEL/FENCE rd receives packed status after guarded lookup.");
    }

    public static AcceleratorRegisterAbiResult FromCapabilityQuery(
        AcceleratorCapabilityAcceptanceResult capabilityAcceptance)
    {
        ArgumentNullException.ThrowIfNull(capabilityAcceptance);

        if (!capabilityAcceptance.IsAccepted || capabilityAcceptance.Descriptor is null)
        {
            return AcceleratorRegisterAbiResult.NoWriteRejected(
                AcceleratorTokenFaultCode.CapabilityNotAccepted,
                "ACCEL_QUERY_CAPS rd is written only after guard-backed capability acceptance.");
        }

        AcceleratorCapabilityDescriptor descriptor = capabilityAcceptance.Descriptor;
        ulong packedSummary = 0;
        packedSummary |= 1UL;
        packedSummary |= ((ulong)Math.Min(descriptor.CapabilityVersion, 0xFFFFu)) << 16;
        packedSummary |= ((ulong)Math.Min(descriptor.Operations.Count, 0xFF)) << 32;
        packedSummary |= ((ulong)Math.Min(descriptor.ResourceModel.MaxQueueOccupancy, 0xFFu)) << 40;
        return AcceleratorRegisterAbiResult.Write(
            packedSummary,
            "ACCEL_QUERY_CAPS rd receives a bounded metadata summary after guard-backed capability acceptance.");
    }
}
