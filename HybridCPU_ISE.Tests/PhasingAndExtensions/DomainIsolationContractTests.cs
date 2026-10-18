using System;
using System.Linq;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Diagnostics;

namespace HybridCPU_ISE.Tests;

/// <summary>
/// Focused V4-E tests for the compiler-facing domain isolation contract.
/// </summary>
public class DomainIsolationContractTests
{
    [Fact]
    public void AreDomainsDisjoint_WhenBothDomainsZero_ThenReturnsFalse()
    {
        Assert.False(DomainIsolationContract.AreDomainsDisjoint(0, 0));
    }

    [Theory]
    [InlineData(0UL, 1UL)]
    [InlineData(0UL, 0x10UL)]
    [InlineData(0UL, ulong.MaxValue)]
    public void AreDomainsDisjoint_WhenFirstDomainIsZero_ThenReturnsFalse(ulong domainA, ulong domainB)
    {
        Assert.False(DomainIsolationContract.AreDomainsDisjoint(domainA, domainB));
    }

    [Theory]
    [InlineData(1UL, 0UL)]
    [InlineData(0x10UL, 0UL)]
    [InlineData(ulong.MaxValue, 0UL)]
    public void AreDomainsDisjoint_WhenSecondDomainIsZero_ThenReturnsFalse(ulong domainA, ulong domainB)
    {
        Assert.False(DomainIsolationContract.AreDomainsDisjoint(domainA, domainB));
    }

    [Theory]
    [InlineData(0x1UL, 0x1UL)]
    [InlineData(0x3UL, 0x1UL)]
    [InlineData(0x18UL, 0x08UL)]
    public void AreDomainsDisjoint_WhenDomainsOverlap_ThenReturnsFalse(ulong domainA, ulong domainB)
    {
        Assert.False(DomainIsolationContract.AreDomainsDisjoint(domainA, domainB));
    }

    [Theory]
    [InlineData(0x1UL, 0x2UL)]
    [InlineData(0x4UL, 0x8UL)]
    [InlineData(0x30UL, 0x0CUL)]
    public void AreDomainsDisjoint_WhenNonZeroDomainsAreBitwiseDisjoint_ThenReturnsTrue(ulong domainA, ulong domainB)
    {
        Assert.True(DomainIsolationContract.AreDomainsDisjoint(domainA, domainB));
    }

    [Theory]
    [InlineData(0x2UL, 0x4UL)]
    [InlineData(0x7UL, 0x1UL)]
    [InlineData(0UL, 0x8UL)]
    public void AreDomainsDisjoint_WhenArgumentsAreSwapped_ThenResultIsSymmetric(ulong domainA, ulong domainB)
    {
        bool forward = DomainIsolationContract.AreDomainsDisjoint(domainA, domainB);
        bool reverse = DomainIsolationContract.AreDomainsDisjoint(domainB, domainA);

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void AreDomainsDisjoint_WhenComparedPairwise_ThenDisjointnessIsNotTransitive()
    {
        const ulong domainA = 0x4;
        const ulong domainB = 0x1;
        const ulong domainC = 0x3;

        Assert.True(DomainIsolationContract.AreDomainsDisjoint(domainA, domainB));
        Assert.True(DomainIsolationContract.AreDomainsDisjoint(domainA, domainC));
        Assert.False(DomainIsolationContract.AreDomainsDisjoint(domainB, domainC));
    }

    [Fact]
    public void DomainIsolationContract_WhenInspected_ThenExposesStructuralStaticQueryOnly()
    {
        Type contractType = typeof(DomainIsolationContract);
        MethodInfo[] publicMethods = contractType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        FieldInfo[] publicFields = contractType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        PropertyInfo[] publicProperties = contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);

        MethodInfo areDomainsDisjoint = Assert.Single(publicMethods);
        Assert.Equal(nameof(DomainIsolationContract.AreDomainsDisjoint), areDomainsDisjoint.Name);
        Assert.True(areDomainsDisjoint.IsStatic);
        Assert.Equal(typeof(bool), areDomainsDisjoint.ReturnType);
        Assert.All(areDomainsDisjoint.GetParameters(), parameter => Assert.Equal(typeof(ulong), parameter.ParameterType));
        Assert.Empty(publicFields);
        Assert.Empty(publicProperties);
    }

    [Fact]
    public void DomainIsolationContract_WhenInspected_ThenDoesNotExposeRuntimePolicyTerminology()
    {
        Type contractType = typeof(DomainIsolationContract);
        string[] forbiddenTerms = ["Schedule", "Dispatch", "Issue", "Retire", "Fairness", "Budget", "Credit", "Priority"];
        string[] exposedNames = contractType
            .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .ToArray();

        foreach (string exposedName in exposedNames)
        {
            foreach (string forbiddenTerm in forbiddenTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, exposedName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
