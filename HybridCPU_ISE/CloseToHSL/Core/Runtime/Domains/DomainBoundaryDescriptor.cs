// Description: Neutral domain-boundary view for runtime-owned execution, memory, and I/O descriptor admission.
namespace YAKSys_Hybrid_CPU.Core;

public readonly record struct DomainBoundaryDescriptor(
    bool RequiresExecutionDomain,
    bool RequiresMemoryDomain,
    bool RequiresIoDomain)
{
    public static DomainBoundaryDescriptor FullDomainRuntime { get; } =
        new(
            RequiresExecutionDomain: true,
            RequiresMemoryDomain: true,
            RequiresIoDomain: true);

    public bool IsSatisfiedBy(DomainRuntimeContext? context)
    {
        if (context is null)
        {
            return false;
        }

        if (RequiresExecutionDomain &&
            context.Execution?.IsAuthoritativeExecutionStateOwner != true)
        {
            return false;
        }

        if (RequiresMemoryDomain &&
            context.Memory?.IsAuthoritativeMemoryStateOwner != true)
        {
            return false;
        }

        if (RequiresIoDomain &&
            context.Io?.IsAuthoritativeIoStateOwner != true)
        {
            return false;
        }

        return true;
    }
}
