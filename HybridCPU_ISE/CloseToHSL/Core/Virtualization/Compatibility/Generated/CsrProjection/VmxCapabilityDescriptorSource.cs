namespace YAKSys_Hybrid_CPU.Core;

public interface IVmxCapabilityDescriptorSource
{
    CapabilityDescriptorSet GetCapabilityDescriptorSet();
}

public sealed class StaticVmxCapabilityDescriptorSource : IVmxCapabilityDescriptorSource
{
    private readonly CapabilityDescriptorSet _descriptorSet;

    public StaticVmxCapabilityDescriptorSource(CapabilityDescriptorSet descriptorSet)
    {
        _descriptorSet = descriptorSet ?? CapabilityDescriptorSet.Empty;
    }

    public CapabilityDescriptorSet GetCapabilityDescriptorSet() => _descriptorSet;
}
