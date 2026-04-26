namespace YAKSys_Hybrid_CPU.Core.Diagnostics;

/// <summary>
/// Formal compiler-facing contract for DomainTag-based isolation.
/// Exposes structural queries only and does not participate in runtime policy decisions.
/// </summary>
public readonly struct DomainIsolationContract
{
    /// <summary>
    /// Returns <see langword="true"/> when both domains are non-zero and bitwise disjoint.
    /// DomainTag <c>0</c> is treated as kernel/unrestricted and is never considered disjoint.
    /// </summary>
    /// <param name="domainA">First domain tag.</param>
    /// <param name="domainB">Second domain tag.</param>
    /// <returns>
    /// <see langword="true"/> only when <paramref name="domainA"/> and <paramref name="domainB"/>
    /// are both non-zero and <c>(domainA &amp; domainB) == 0</c>.
    /// </returns>
    public static bool AreDomainsDisjoint(ulong domainA, ulong domainB)
    {
        return (domainA & domainB) == 0 && domainA != 0 && domainB != 0;
    }
}
