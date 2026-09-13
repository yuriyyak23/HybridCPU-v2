namespace YAKSys_Hybrid_CPU.Core;

public sealed partial class DomainRuntimeContext
{
    public DomainRuntimeContext()
        : this(
            execution: null,
            memory: null,
            io: null,
            capabilities: CapabilityDescriptorSet.Empty,
            secureCompute: null,
            domainTag: 0,
            addressSpaceTag: 0)
    {
    }

    public DomainRuntimeContext(
        ExecutionDomainDescriptor? execution,
        MemoryDomainDescriptor? memory,
        IoDomainDescriptor? io,
        CapabilityDescriptorSet capabilities)
        : this(
            execution,
            memory,
            io,
            capabilities,
            secureCompute: null,
            domainTag: 0,
            addressSpaceTag: 0)
    {
    }

    public DomainRuntimeContext(
        ExecutionDomainDescriptor? execution,
        MemoryDomainDescriptor? memory,
        IoDomainDescriptor? io,
        CapabilityDescriptorSet capabilities,
        SecureComputeDomainDescriptor? secureCompute)
        : this(
            execution,
            memory,
            io,
            capabilities,
            secureCompute,
            domainTag: 0,
            addressSpaceTag: 0)
    {
    }

    public DomainRuntimeContext(
        ExecutionDomainDescriptor? execution,
        MemoryDomainDescriptor? memory,
        IoDomainDescriptor? io,
        CapabilityDescriptorSet capabilities,
        SecureComputeDomainDescriptor? secureCompute,
        ulong domainTag,
        ulong addressSpaceTag)
    {
        Execution = execution;
        Memory = memory;
        Io = io;
        Capabilities = capabilities;
        SecureCompute = secureCompute;
        DomainTag = domainTag;
        AddressSpaceTag = addressSpaceTag;
    }

    public ExecutionDomainDescriptor? Execution { get; }

    public MemoryDomainDescriptor? Memory { get; }

    public IoDomainDescriptor? Io { get; }

    public CapabilityDescriptorSet Capabilities { get; }

    public SecureComputeDomainDescriptor? SecureCompute { get; }

    public ulong DomainTag { get; }

    public ulong AddressSpaceTag { get; }

    public bool HasExecutionDomain => Execution is not null;

    public bool HasMemoryDomain => Memory is not null;

    public bool HasIoDomain => Io is not null;

    public bool HasRequiredDomains =>
        HasExecutionDomain &&
        HasMemoryDomain &&
        HasIoDomain;

    public DomainRuntimeContext WithCapabilities(CapabilityDescriptorSet capabilities) =>
        new(Execution, Memory, Io, capabilities, SecureCompute, DomainTag, AddressSpaceTag);

    public DomainRuntimeContext WithSecureCompute(SecureComputeDomainDescriptor? secureCompute) =>
        new(Execution, Memory, Io, Capabilities, secureCompute, DomainTag, AddressSpaceTag);

    public bool IsBoundToDomain(ulong domainTag) =>
        DomainTag != 0 && DomainTag == domainTag;

    public bool IsBoundToAddressSpace(ulong addressSpaceTag) =>
        AddressSpaceTag != 0 && AddressSpaceTag == addressSpaceTag;
}
