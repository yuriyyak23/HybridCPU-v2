using System.Reflection;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.VmxRefactoring;

public sealed class VmxPrGCompletionOwnerE5Tests
{
    [Fact]
    public void CanonicalE3_AtomicallyPublishesOneRecordAndOpaqueE5_WhileRetireStaysFaulted()
    {
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture =
            VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true, completion: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));

        DomainHypercallCompletionPublicationResult publication =
            Assert.IsType<DomainHypercallCompletionPublicationResult>(
                fixture.Carrier.ExactHypercallCompletionPublication);
        Assert.True(publication.IsPublished);
        Assert.Equal(CompletionRecordClass.Event, publication.Completion.RecordClass);
        Assert.NotNull(publication.E5);
        Assert.False(publication.RetirePublicationAuthorized);
        Assert.False(publication.E5!.RetirePublicationAuthorized);
        Assert.Equal(fixture.Carrier.ExactHypercallExecutionReceipt!.AttemptId, publication.E5.AttemptId);
        Assert.Equal(fixture.Carrier.ExactHypercallExecutionReceipt.VirtualThreadId, publication.E5.VirtualThreadId);
        Assert.Equal(fixture.Carrier.ExactHypercallExecutionReceipt.DomainTag, publication.E5.DomainTag);
        Assert.True(fixture.CompletionOwner!.ValidateLive(publication.E5, publication.Completion, fixture.RestoreOwner));
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void MissingCompletionOwner_PreservesPrFFaultOnlyRollbackAndProducesNoRecordOrE5()
    {
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture =
            VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true, completion: false);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));

        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        Assert.True(fixture.Carrier.ExactHypercallExecutionResult!.Value.IsExecuted);
        Assert.Null(fixture.Carrier.ExactHypercallCompletionPublication);
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void DuplicateE3Publication_IsDeniedAndCannotProduceASecondRecordOrE5()
    {
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture =
            VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true, completion: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));

        DomainHypercallCompletionPublicationResult duplicate = fixture.CompletionOwner!.PublishExactProbe(
            fixture.Executor!, fixture.Carrier.ExactHypercallExecutionReceipt,
            fixture.RestoreOwner, fixture.Context!, fixture.Root!, fixture.LifecycleGate);
        Assert.Equal(DomainHypercallCompletionDecision.InvalidExecution, duplicate.Decision);
        Assert.True(duplicate.Completion.IsEmpty);
        Assert.Null(duplicate.E5);
    }

    [Fact]
    public void ForeignCompletionOwner_CannotConsumeConfiguredOwnersE3()
    {
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture =
            VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true, completion: false);
        var configuredOwner = new DomainHypercallCompletionOwner();
        fixture.Executor!.BindCompletionOwner(configuredOwner);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));

        var foreignOwner = new DomainHypercallCompletionOwner();
        DomainHypercallCompletionPublicationResult denied = foreignOwner.PublishExactProbe(
            fixture.Executor, fixture.Carrier.ExactHypercallExecutionReceipt,
            fixture.RestoreOwner, fixture.Context!, fixture.Root!, fixture.LifecycleGate);
        Assert.Equal(DomainHypercallCompletionDecision.InvalidExecution, denied.Decision);
        Assert.True(denied.Completion.IsEmpty);
        Assert.Null(denied.E5);
    }

    [Fact]
    public void RestoreInvalidatesLiveE5WithoutGrantingRetire()
    {
        VmxPrFCanonicalHypercallCompositionTests.Fixture fixture =
            VmxPrFCanonicalHypercallCompositionTests.CreateFixture(configure: true, completion: true);
        Assert.True(VmxPrFCanonicalHypercallCompositionTests.Materialize(fixture, 1));
        YAKSys_Hybrid_CPU.Processor.CPU_Core core = fixture.Core;
        Assert.True(fixture.Carrier.Execute(ref core));
        DomainHypercallCompletionPublicationResult publication =
            fixture.Carrier.ExactHypercallCompletionPublication!.Value;

        fixture.RestoreOwner.AdvanceAfterRestore();
        Assert.False(fixture.CompletionOwner!.ValidateLive(
            publication.E5, publication.Completion, fixture.RestoreOwner));
        Assert.True(fixture.Carrier.CreateRetireEffect().IsFaulted);
    }

    [Fact]
    public void E5_IsOpaqueAndExistingCallerBooleanFenceRemainsSeparateScaffolding()
    {
        ConstructorInfo constructor = Assert.Single(
            typeof(DomainHypercallCompletionOwner.CompletionPublicationToken)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);
        Assert.Empty(typeof(DomainHypercallCompletionOwner.CompletionPublicationToken).GetConstructors());
        Assert.DoesNotContain(
            typeof(DomainHypercallCompletionOwner).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(bool)));
    }
}
