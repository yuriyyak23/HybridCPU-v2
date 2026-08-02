namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class SecureDomainAdmissionService
{
    public SecureDomainAdmissionService()
        : this(new SecureDomainAdmissionPolicy())
    {
    }

    public SecureDomainAdmissionService(SecureDomainAdmissionPolicy policy)
    {
        Policy = policy;
    }

    public SecureDomainAdmissionPolicy Policy { get; }

    public SecureDomainAdmissionResult Admit(
        SecureComputeDomainDescriptor? descriptor,
        SecureDomainOperationClass operationClass,
        DomainMeasurementDescriptor? measurement,
        SecureMemoryDomainDescriptor? memory) =>
        Policy.Admit(descriptor, operationClass, measurement, memory);
}
