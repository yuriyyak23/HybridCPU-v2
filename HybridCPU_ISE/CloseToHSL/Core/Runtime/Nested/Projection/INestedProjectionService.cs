namespace YAKSys_Hybrid_CPU.Core.Nested;

public interface INestedProjectionService
{
    bool TryEnable(
        NestedDomainDescriptor domain,
        NestedEnablementRequest request,
        out NestedValidationResult validation);

    void Disable(NestedDomainDescriptor domain);
}
