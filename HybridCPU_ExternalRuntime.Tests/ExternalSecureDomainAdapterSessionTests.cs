using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalSecureDomainAdapterSessionTests
{
    [Fact]
    public void CreationQuery_RecoversLostReplyOnceWithoutReissuingOrResurrectingAfterClose()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain))
        {
            OnCreate = _ => throw new InvalidOperationException("lost reply")
        };
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(request with { }));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(request));
        provider.OnClose = (current, operation) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, operation, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(1, provider.CreateQueries);
    }

    [Theory]
    [InlineData(ExternalSecureDomainProviderStatus.Unavailable)]
    [InlineData(ExternalSecureDomainProviderStatus.Stale)]
    [InlineData(ExternalSecureDomainProviderStatus.Faulted)]
    [InlineData(ExternalSecureDomainProviderStatus.Rejected)]
    public void CreationQuery_UncertainObservationRetainsOriginalReservation(ExternalSecureDomainProviderStatus status)
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain)) { CreateResult = new(status) };
        var session = new ExternalSecureDomainAdapterSession(provider);
        session.Create(request);
        provider.OnQueryCreate = _ => new(status);
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        provider.OnQueryCreate = _ => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(request, domain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileCreation(request));
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(2, provider.CreateQueries);
    }

    [Fact]
    public async Task CreationQuery_SerializesCallbacksOutsideLockAndQuarantinesForeignReceipt()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        provider.OnCreate = _ =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return new(ExternalSecureDomainProviderStatus.Unavailable);
        };
        var pending = Task.Run(() => session.Create(request));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
                await Task.Run(() => session.ReconcileCreation(request)).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { release.Set(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        provider.OnQueryCreate = current =>
        {
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
                Task.Run(() => session.ReconcileCreation(current)).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult());
            return new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(Request(), domain));
        };
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        Assert.Equal(1, provider.CreateQueries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreationQuery_ThrowOrNullNeverAdopts(bool nullReply)
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain))
        {
            CreateResult = new(ExternalSecureDomainProviderStatus.Unavailable),
            OnQueryCreate = _ => nullReply ? null! : throw new InvalidOperationException("query lost")
        };
        var session = new ExternalSecureDomainAdapterSession(provider);
        session.Create(request);
        Assert.Equal(nullReply ? ExternalSecureDomainAdapterStatus.Quarantined : ExternalSecureDomainAdapterStatus.TransportUnavailable,
            session.ReconcileCreation(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        provider.OnQueryCreate = _ => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(request, domain));
        Assert.Equal(nullReply ? ExternalSecureDomainAdapterStatus.Quarantined : ExternalSecureDomainAdapterStatus.Accepted,
            session.ReconcileCreation(request));
        Assert.Equal(1, provider.CreateCalls);
    }

    [Fact]
    public void CreationQuery_ConflictingGenerationCannotBeExchangedForAnotherDomain()
    {
        var request = Request();
        var uncertain = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        provider.CreateResult = new(ExternalSecureDomainProviderStatus.Unavailable);
        session.Create(uncertain);
        provider.OnQueryCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(uncertain));
        provider.OnQueryCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, Domain()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileCreation(uncertain));
        Assert.Equal(1, provider.CreateQueries);
        var legacy = new ExternalSecureDomainAdapterSession(new QueryProvider(CreateReceipt(request, domain)));
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, legacy.ReconcileCreation(request));
    }

    [Fact]
    public void ExactProviderCreationAndTerminalClose_AreAcceptedOnce()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        ExternalOperationIdentity operation = Operation();
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(
            ExternalSecureDomainProviderStatus.Receipt,
            new ExternalSecureDomainCloseReceipt(domain, operation, true));

        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, operation));
    }

    [Fact]
    public void MalformedCreationOrCloseReceipt_IsQuarantinedAndNeverBecomesSuccess()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var wrongRequest = Request();
        var malformedCreate = new FakeProvider(CreateReceipt(wrongRequest, domain));
        var createSession = new ExternalSecureDomainAdapterSession(malformedCreate);

        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, createSession.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, createSession.Create(request));

        var provider = new FakeProvider(CreateReceipt(request, domain));
        var closeSession = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, closeSession.Create(request));
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(
            ExternalSecureDomainProviderStatus.Receipt,
            new ExternalSecureDomainCloseReceipt(domain, Operation(), true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, closeSession.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, closeSession.Close(domain, Operation()));
    }

    [Fact]
    public void TransportStaleAndFault_DoNotCreateOrClose()
    {
        ExternalSecureDomainCreateRequest request = Request();
        ExternalSecureDomainLease domain = Domain();
        var unavailable = new FakeProvider(CreateReceipt(request, domain))
        {
            CreateResult = new ExternalSecureDomainCreateProviderResult(ExternalSecureDomainProviderStatus.Unavailable)
        };
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable,
            new ExternalSecureDomainAdapterSession(unavailable).Create(request));

        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        provider.CloseResult = new ExternalSecureDomainCloseProviderResult(ExternalSecureDomainProviderStatus.Stale);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Stale, session.Close(domain, Operation()));
    }

    [Fact]
    public void CreationReservation_PrecedesReentrantProviderCallAndRejectsCopiedRequest()
    {
        var request = Request();
        var provider = new FakeProvider(CreateReceipt(request, Domain()));
        var session = new ExternalSecureDomainAdapterSession(provider);
        provider.OnCreate = current =>
        {
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(current with { }));
            return new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, Domain()));
        };

        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        Assert.Equal(1, provider.CreateCalls);
    }

    [Theory]
    [InlineData(ExternalSecureDomainProviderStatus.Unavailable, ExternalSecureDomainAdapterStatus.TransportUnavailable)]
    [InlineData(ExternalSecureDomainProviderStatus.Faulted, ExternalSecureDomainAdapterStatus.ProviderFault)]
    [InlineData(ExternalSecureDomainProviderStatus.Stale, ExternalSecureDomainAdapterStatus.Stale)]
    [InlineData(ExternalSecureDomainProviderStatus.Rejected, ExternalSecureDomainAdapterStatus.Rejected)]
    public void UncertainCreationAndClose_DoNotPermitCallbackRetry(
        ExternalSecureDomainProviderStatus status, ExternalSecureDomainAdapterStatus expected)
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain))
        {
            CreateResult = new(status),
        };
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(expected, session.Create(request));
        provider.CreateResult = null;
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request with { }));
        Assert.Equal(1, provider.CreateCalls);

        var otherRequest = Request();
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(otherRequest));
        provider.CloseResult = new(status);
        Assert.Equal(expected, session.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        Assert.Equal(1, provider.CloseCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderThrowOrNull_RetainsReservationWithoutResubmission(bool nullReply)
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        provider.OnCreate = _ => nullReply ? null! : throw new InvalidOperationException("lost reply");
        Assert.Equal(nullReply ? ExternalSecureDomainAdapterStatus.Quarantined : ExternalSecureDomainAdapterStatus.TransportUnavailable,
            session.Create(request));
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(1, provider.CreateCalls);

        var other = Request();
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(other));
        provider.OnClose = (_, _) => nullReply ? null! : throw new InvalidOperationException("lost close");
        Assert.Equal(nullReply ? ExternalSecureDomainAdapterStatus.Quarantined : ExternalSecureDomainAdapterStatus.TransportUnavailable,
            session.Close(domain, Operation()));
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(1, provider.CloseCalls);
    }

    [Fact]
    public async Task ConcurrentCreateAndClose_ReserveBeforeProviderCallbacksOutsideLock()
    {
        var request = Request();
        var domain = Domain();
        var operation = Operation();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        provider.OnCreate = current =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        };
        Task<ExternalSecureDomainAdapterStatus> create = Task.Run(() => session.Create(request));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
                await Task.Run(() => session.Create(request with { })).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { release.Set(); }
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, await create.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, provider.CreateCalls);

        entered.Reset();
        release.Reset();
        provider.OnClose = (current, correlation) =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
            return new(ExternalSecureDomainProviderStatus.Receipt, new(current, correlation, true));
        };
        Task<ExternalSecureDomainAdapterStatus> close = Task.Run(() => session.Close(domain, operation));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
                await Task.Run(() => session.Close(domain, Operation())).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { release.Set(); }
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, await close.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, provider.CloseCalls);
    }

    [Fact]
    public void TerminalClose_LeavesOldGenerationDeadAndRequiresFreshCreateRequest()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        provider.OnClose = (current, correlation) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, correlation, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, default));
        Assert.Equal(0, provider.CloseCalls);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(Request()));

        domain = new(domain.Handle, new ExternalSecureDomainGeneration(2));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(Request()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected,
            session.Close(new(domain.Handle, new ExternalSecureDomainGeneration(1)), Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(2, provider.CloseCalls);
    }

    [Fact]
    public void CloseCorrelation_CannotBeReusedForForeignDomainOrNewGeneration()
    {
        var request = Request();
        var first = Domain();
        var second = Domain();
        var operation = Operation();
        var provider = new FakeProvider(CreateReceipt(request, first));
        var session = new ExternalSecureDomainAdapterSession(provider);
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt,
            CreateReceipt(current, current == request ? first : second));
        provider.OnClose = (current, correlation) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, correlation, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(Request()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(first, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(second, operation));
        Assert.Equal(1, provider.CloseCalls);
        // The rejected foreign correlation does not consume the second domain's close.
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(second, Operation()));
        Assert.Equal(2, provider.CloseCalls);
    }

    [Fact]
    public void RejectedOverlappingGeneration_CannotResurrectAfterOldDomainCloses()
    {
        var domain = Domain();
        var request = Request();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, domain));
        provider.OnClose = (current, operation) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, operation, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var original = domain;
        domain = new(domain.Handle, new ExternalSecureDomainGeneration(2));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(Request()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(original, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(Request()));
        domain = new(domain.Handle, new ExternalSecureDomainGeneration(3));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(Request()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
    }

    [Fact]
    public void LostClose_ReconcilesOnlyExactOriginalWithoutRepeatingEffect()
    {
        var request = Request();
        var domain = Domain();
        var operation = Operation();
        var provider = new QueryProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileClose(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, session.Close(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileClose(domain, Operation()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileClose(Domain(), operation));
        provider.Query = (current, original) =>
        {
            Assert.Equal(domain, current);
            Assert.Equal(operation, original);
            Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileClose(domain, operation));
            return new(ExternalSecureDomainProviderStatus.Receipt, new(current, original, true));
        };
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileClose(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.ReconcileClose(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, operation));
        Assert.Equal(1, provider.CloseCalls);
        Assert.Equal(1, provider.QueryCalls);
    }

    [Theory]
    [InlineData(ExternalSecureDomainProviderStatus.Unavailable)]
    [InlineData(ExternalSecureDomainProviderStatus.Faulted)]
    [InlineData(ExternalSecureDomainProviderStatus.Stale)]
    [InlineData(ExternalSecureDomainProviderStatus.Rejected)]
    public void QueryWithoutClosure_KeepsOriginalPinnedAndAllowsReadOnlyReconciliation(
        ExternalSecureDomainProviderStatus status)
    {
        var request = Request();
        var domain = Domain();
        var operation = Operation();
        var provider = new QueryProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, session.Close(domain, operation));
        provider.Query = (_, _) => new(status);
        Assert.NotEqual(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileClose(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        provider.Query = (_, _) => throw new InvalidOperationException("query lost");
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, session.ReconcileClose(domain, operation));
        provider.Query = (current, original) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, original, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.ReconcileClose(domain, operation));
        Assert.Equal(1, provider.CloseCalls);
        Assert.Equal(3, provider.QueryCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedQuery_QuarantinesAndCannotBecomeClosure(bool nullResult)
    {
        var request = Request();
        var domain = Domain();
        var operation = Operation();
        var provider = new QueryProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        Assert.Equal(ExternalSecureDomainAdapterStatus.TransportUnavailable, session.Close(domain, operation));
        provider.Query = (_, _) => nullResult ? null! :
            new(ExternalSecureDomainProviderStatus.Receipt, new(domain, Operation(), true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, session.ReconcileClose(domain, operation));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Quarantined, session.ReconcileClose(domain, operation));
        Assert.Equal(1, provider.QueryCalls);
        Assert.Equal(1, provider.CloseCalls);
    }

    private sealed class QueryProvider(ExternalSecureDomainCreateReceipt receipt) : IExternalSecureDomainProvider,
        IExternalSecureDomainCloseQueryProvider
    {
        public int CloseCalls;
        public int QueryCalls;
        public Func<ExternalSecureDomainLease, ExternalOperationIdentity, ExternalSecureDomainCloseProviderResult>? Query;
        public ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request) =>
            new(ExternalSecureDomainProviderStatus.Receipt, receipt);
        public ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
        {
            CloseCalls++;
            throw new InvalidOperationException("original close reply lost");
        }
        public ExternalSecureDomainCloseProviderResult QueryClose(ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
        {
            QueryCalls++;
            return Query!(domain, operation);
        }
    }

    [Fact]
    public void NeutralRegisterLocalIngress_ReachesCanonicalVerifierDenyWithoutProviderExecution()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        var child = new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()),
            new ExternalChildDomainEpoch(1), request.Parent);
        var binding = new ExternalSecureExecutionBinding(child, domain, request.Parent, 1,
            ExternalSecureComputeContract.Version);
        var intent = new ExternalSecureRegisterLocalRequest(binding, Operation(), 19, 23);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding,
            session.AdmitRegisterLocal(intent).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        {
            var denied = session.AdmitRegisterLocal(intent);
            Assert.False(denied.IsAllowed);
            Assert.Same(intent, denied.Request);
            Assert.Equal(intent.Operation, denied.Request.Operation);
            Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable,
                denied.Decision);
            Assert.NotNull(denied.Canonical);
            Assert.False(denied.Canonical.IsReplayEligible);
            var primitive = denied.Canonical.GetSlot(0);
            Assert.Equal((uint)YAKSys_Hybrid_CPU.Processor.CPU_Core.IsaOpcodeValues.ADD, primitive.Opcode);
            Assert.Equal((byte)3, primitive.Rd);
            Assert.Equal((byte)1, primitive.Rs1);
            Assert.Equal((byte)2, primitive.Rs2);
            Assert.Equal(YAKSys_Hybrid_CPU.Core.Decoder.CanonicalSecurityOperationKind.Ordinary,
                primitive.SecurityOperationKind);
        }
        int queriesBeforeDuplicate = provider.BindingQueries;
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedOperationCompleted,
            session.AdmitRegisterLocal(intent).Decision);
        Assert.Equal(queriesBeforeDuplicate, provider.BindingQueries);
        Assert.Equal(1, provider.CreateCalls);
        Assert.Equal(0, provider.CloseCalls);
        var foreignParent = Request().Parent;
        var foreignBinding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), foreignParent),
            domain, foreignParent, 1, ExternalSecureComputeContract.Version);
        var staleBinding = new ExternalSecureExecutionBinding(child,
            new ExternalSecureDomainLease(domain.Handle, new ExternalSecureDomainGeneration(2)),
            request.Parent, 1, ExternalSecureComputeContract.Version);
        foreach (var invalid in new[] { foreignBinding, staleBinding })
        {
            var denied = session.AdmitRegisterLocal(new(invalid, Operation(), 1, 2));
            Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding, denied.Decision);
            Assert.Null(denied.Canonical);
        }
        // Starting Close kills local ingress even if its outcome is unresolved.
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, Operation()));
        var afterClose = session.AdmitRegisterLocal(intent);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding,
            afterClose.Decision);
        Assert.Null(afterClose.Canonical);
    }

    [Fact]
    public void NeutralRegisterLocalRequest_RejectsUnknownSchemaAndMissingOperation()
    {
        var request = Request();
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            Domain(), request.Parent, 1, ExternalSecureComputeContract.Version);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2, 2));
        Assert.Throws<ArgumentException>(() => new ExternalSecureRegisterLocalRequest(binding, default, 1, 2));
        Assert.All(typeof(ExternalSecureRegisterLocalRequest).GetProperties(), property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void LocalRevoke_PermanentlyClosesExactIngressButPreservesOriginalCleanup()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        var intent = new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable,
            session.AdmitRegisterLocal(intent).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.RevokeLocalBinding(Domain()));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.RevokeLocalBinding(
            new(domain.Handle, new ExternalSecureDomainGeneration(2))));
        intent = new(binding, Operation(), 1, 2);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable,
            session.AdmitRegisterLocal(intent).Decision);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(domain));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.RevokeLocalBinding(domain));
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding,
            session.AdmitRegisterLocal(intent).Decision);
        var operation = Operation();
        provider.OnClose = (current, original) =>
        {
            Assert.Equal(operation, original);
            Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding,
                session.AdmitRegisterLocal(intent).Decision);
            return new(ExternalSecureDomainProviderStatus.Receipt, new(current, original, true));
        };
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, operation));
        Assert.Equal(1, provider.CloseCalls);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding,
            session.AdmitRegisterLocal(intent).Decision);
    }

    [Fact]
    public void BindingRevalidation_RequiresOriginalObservationAndRechecksRevokeAfterCallback()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        var intent = new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2);
        foreach (var status in new[] { ExternalSecureDomainProviderStatus.Unavailable,
            ExternalSecureDomainProviderStatus.Faulted,
            ExternalSecureDomainProviderStatus.Rejected })
        {
            provider.OnRevalidate = original => new(original, status);
            Assert.Null(session.AdmitRegisterLocal(intent).Canonical);
        }
        provider.OnRevalidate = _ => throw new InvalidOperationException("query lost");
        Assert.Null(session.AdmitRegisterLocal(intent).Canonical);
        provider.OnRevalidate = original =>
        {
            Assert.Null(session.AdmitRegisterLocal(intent).Canonical);
            Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(domain));
            return new(original, ExternalSecureDomainProviderStatus.Receipt);
        };
        var revoked = session.AdmitRegisterLocal(intent);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding, revoked.Decision);
        Assert.Null(revoked.Canonical);
        int calls = provider.BindingQueries;
        Assert.Null(session.AdmitRegisterLocal(intent).Canonical);
        Assert.Equal(calls, provider.BindingQueries);
        Assert.Equal(0, provider.CloseCalls);
    }

    [Fact]
    public void MissingBindingProvider_DoesNotReachCanonicalMachineBoundary()
    {
        var request = Request();
        var domain = Domain();
        var session = new ExternalSecureDomainAdapterSession(new QueryProvider(CreateReceipt(request, domain)));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        Assert.Null(session.AdmitRegisterLocal(new(binding, Operation(), 1, 2)).Canonical);
    }

    [Fact]
    public async Task BindingQuery_CloseMayFinishOutsideCallbackAndLateObservationIsDenied()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        var intent = new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        provider.OnRevalidate = original =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return new(original, ExternalSecureDomainProviderStatus.Receipt);
        };
        provider.OnClose = (current, original) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, original, true));
        var query = Task.Run(() => session.AdmitRegisterLocal(intent));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null((await Task.Run(() => session.AdmitRegisterLocal(intent)).WaitAsync(TimeSpan.FromSeconds(5))).Canonical);
            Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted,
                await Task.Run(() => session.Close(domain, Operation())).WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { release.Set(); }
        var late = await query.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedLocalBinding, late.Decision);
        Assert.Null(late.Canonical);
        Assert.Equal(1, provider.BindingQueries);
        Assert.Equal(1, provider.CloseCalls);
    }

    [Fact]
    public void RegisterLocalCorrelation_IsOriginalOnlyAcrossMutationFailureAndForeignBinding()
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var child = new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent);
        var binding = new ExternalSecureExecutionBinding(child, domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        var operation = Operation();
        var original = new ExternalSecureRegisterLocalRequest(binding, operation, 1, 2);
        provider.OnRevalidate = _ => throw new InvalidOperationException("lost observation");
        Assert.Null(session.AdmitRegisterLocal(original).Canonical);
        int queries = provider.BindingQueries;
        var changedChild = new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(2), request.Parent);
        var changedBindings = new[] { binding,
            new ExternalSecureExecutionBinding(changedChild, domain, request.Parent, 1, ExternalSecureComputeContract.Version),
            new ExternalSecureExecutionBinding(child, domain, request.Parent, 2, ExternalSecureComputeContract.Version) };
        foreach (var changed in changedBindings)
        {
            Assert.Null(session.AdmitRegisterLocal(new(changed, operation, 1, 2)).Canonical);
            Assert.Null(session.AdmitRegisterLocal(new(changed, operation, 3, 2)).Canonical);
            Assert.Null(session.AdmitRegisterLocal(new(changed, operation, 1, 3)).Canonical);
        }
        Assert.Equal(queries, provider.BindingQueries);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, operation));
        Assert.Equal(0, provider.CloseCalls);
        provider.OnRevalidate = current => new(current, ExternalSecureDomainProviderStatus.Receipt);
        Assert.NotNull(session.AdmitRegisterLocal(original).Canonical);
        Assert.Equal(queries + 1, provider.BindingQueries);
        // New work needs a fresh external operation, even for equal operand values.
        Assert.NotNull(session.AdmitRegisterLocal(new(binding, Operation(), 1, 2)).Canonical);
    }

    [Fact]
    public void CloseOperation_CannotBecomeRegisterLocalWorkOnAnotherLiveDomain()
    {
        var firstRequest = Request();
        var secondRequest = Request();
        var first = Domain();
        var second = Domain();
        var provider = new FakeProvider(CreateReceipt(firstRequest, first));
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt,
            CreateReceipt(current, ReferenceEquals(current, firstRequest) ? first : second));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(firstRequest));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(secondRequest));
        var operation = Operation();
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(first, operation));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), secondRequest.Parent),
            second, secondRequest.Parent, 1, ExternalSecureComputeContract.Version);
        Assert.Null(session.AdmitRegisterLocal(new(binding, operation, 1, 2)).Canonical);
        Assert.Equal(0, provider.BindingQueries);
        Assert.NotNull(session.AdmitRegisterLocal(new(binding, Operation(), 1, 2)).Canonical);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void StaleOrMalformedBindingObservation_PermanentlyKillsIngressButAllowsCleanup(int variant)
    {
        var request = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(request, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(request));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), request.Parent),
            domain, request.Parent, 1, ExternalSecureComputeContract.Version);
        var original = new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2);
        provider.OnRevalidate = current => variant switch
        {
            0 => new(current, ExternalSecureDomainProviderStatus.Stale),
            1 => null!,
            _ => new(new(current.Binding, current.Operation, current.Left, current.Right), ExternalSecureDomainProviderStatus.Receipt),
        };
        Assert.Null(session.AdmitRegisterLocal(original).Canonical);
        int queries = provider.BindingQueries;
        provider.OnRevalidate = current => new(current, ExternalSecureDomainProviderStatus.Receipt);
        // Neither original retry nor fresh correlation can resurrect this generation.
        Assert.Null(session.AdmitRegisterLocal(original).Canonical);
        Assert.Null(session.AdmitRegisterLocal(new(binding, Operation(), 1, 2)).Canonical);
        Assert.Equal(queries, provider.BindingQueries);
        provider.OnClose = (current, operation) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, operation, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(1, provider.CloseCalls);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Create(request));
        // A fresh provider generation needs a fresh request; old intent remains dead.
        var fresh = new ExternalSecureDomainLease(domain.Handle, new ExternalSecureDomainGeneration(2));
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt, CreateReceipt(current, fresh));
        var freshRequest = Request();
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(freshRequest));
        Assert.Null(session.AdmitRegisterLocal(original).Canonical);
        var freshBinding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), freshRequest.Parent),
            fresh, freshRequest.Parent, 2, ExternalSecureComputeContract.Version);
        var newWork = session.AdmitRegisterLocal(new(freshBinding, Operation(), 1, 2));
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable, newWork.Decision);
        Assert.NotNull(newWork.Canonical);
        Assert.False(newWork.IsAllowed);
    }

    [Fact]
    public void AdmissionReservation_ConsumesOriginalOnceAndRetainsCapacityUntilCallbackRelease()
    {
        var parent = Request().Parent;
        var domain = Domain();
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), parent),
            domain, parent, 1, ExternalSecureComputeContract.Version);
        var request = new ExternalSecureRegisterLocalRequest(binding, Operation(), 1, 2);
        var owner = new SecureRegisterLocalAdmissionReservations(1);
        var foreign = new SecureRegisterLocalAdmissionReservations(1);
        Assert.True(owner.TryReserve(request, out var token));
        Assert.NotNull(token);
        Assert.False(foreign.TryConsume(token, request));
        Assert.False(owner.TryConsume(new(), request));
        Assert.False(owner.TryConsume(token, new(binding, request.Operation, 1, 2)));
        Assert.True(owner.TryConsume(token, request));
        Assert.False(owner.TryConsume(token, request));
        Assert.False(owner.TryReserve(new(binding, Operation(), 1, 2), out _));
        Assert.True(owner.Release(token, request));
        Assert.False(owner.Release(token, request));
        Assert.False(owner.TryConsume(token, request));
        Assert.True(owner.TryReserve(request, out var next));
        owner.Invalidate(Domain());
        owner.Invalidate(domain);
        Assert.False(owner.TryConsume(next!, request));
        Assert.False(owner.TryReserve(new(binding, Operation(), 1, 2), out _));
        Assert.True(owner.Release(next!, request));
        Assert.True(owner.TryReserve(new(binding, Operation(), 1, 2), out _));
    }

    [Fact]
    public async Task AdmissionCapacity_BoundsProviderQueriesAcrossDomainsAndReleasesOnDeny()
    {
        var firstRequest = Request();
        var secondRequest = Request();
        var firstDomain = Domain();
        var secondDomain = Domain();
        var provider = new FakeProvider(CreateReceipt(firstRequest, firstDomain));
        provider.OnCreate = current => new(ExternalSecureDomainProviderStatus.Receipt,
            CreateReceipt(current, ReferenceEquals(current, firstRequest) ? firstDomain : secondDomain));
        var session = new ExternalSecureDomainAdapterSession(provider, maximumConcurrentAdmissions: 1);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(firstRequest));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(secondRequest));
        ExternalSecureRegisterLocalRequest Intent(ExternalSecureDomainCreateRequest create, ExternalSecureDomainLease domain) => new(
            new ExternalSecureExecutionBinding(
                new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), create.Parent),
                domain, create.Parent, 1, ExternalSecureComputeContract.Version), Operation(), 1, 2);
        var first = Intent(firstRequest, firstDomain);
        var second = Intent(secondRequest, secondDomain);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        provider.OnRevalidate = original =>
        {
            if (ReferenceEquals(original, first))
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            return new(original, ExternalSecureDomainProviderStatus.Receipt);
        };
        var pending = Task.Run(() => session.AdmitRegisterLocal(first));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Null(session.AdmitRegisterLocal(second).Canonical);
            Assert.Equal(1, provider.BindingQueries);
            Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.RevokeLocalBinding(firstDomain));
            // Revoked callback is still running and must retain its local slot.
            Assert.Null(session.AdmitRegisterLocal(second).Canonical);
            Assert.Equal(1, provider.BindingQueries);
        }
        finally { release.Set(); }
        Assert.Null((await pending.WaitAsync(TimeSpan.FromSeconds(10))).Canonical);
        Assert.NotNull(session.AdmitRegisterLocal(second).Canonical);
        Assert.Null(session.AdmitRegisterLocal(second).Canonical);
        Assert.Equal(2, provider.BindingQueries);
        Assert.Equal(0, provider.CloseCalls);
    }

    [Fact]
    public void TerminalCanonicalDeny_DropsOperandCustodyButRetainsCorrelationTombstone()
    {
        var create = Request();
        var domain = Domain();
        var provider = new FakeProvider(CreateReceipt(create, domain));
        var session = new ExternalSecureDomainAdapterSession(provider);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Create(create));
        var binding = new ExternalSecureExecutionBinding(
            new ExternalChildDomainLease(new ExternalChildDomainHandle(Guid.NewGuid()), new ExternalChildDomainEpoch(1), create.Parent),
            domain, create.Parent, 1, ExternalSecureComputeContract.Version);
        var operation = Operation();
        WeakReference original = CompleteOne(session, binding, operation);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(original.IsAlive);
        int queries = provider.BindingQueries;
        var copied = new ExternalSecureRegisterLocalRequest(binding, operation, 99, 101);
        var denied = session.AdmitRegisterLocal(copied);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedOperationCompleted, denied.Decision);
        Assert.Null(denied.Canonical);
        Assert.Equal(queries, provider.BindingQueries);
        Assert.Equal(ExternalSecureDomainAdapterStatus.Rejected, session.Close(domain, operation));
        Assert.NotNull(session.AdmitRegisterLocal(new(binding, Operation(), 99, 101)).Canonical);
        provider.OnClose = (current, close) => new(ExternalSecureDomainProviderStatus.Receipt, new(current, close, true));
        Assert.Equal(ExternalSecureDomainAdapterStatus.Accepted, session.Close(domain, Operation()));
        Assert.Equal(1, provider.CloseCalls);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CompleteOne(ExternalSecureDomainAdapterSession session,
        ExternalSecureExecutionBinding binding, ExternalOperationIdentity operation)
    {
        var original = new ExternalSecureRegisterLocalRequest(binding, operation, 99, 101);
        Assert.Equal(YAKSys_Hybrid_CPU.Core.SecureComputeCanonicalAdmissionDecision.DeniedMachineChainUnavailable,
            session.AdmitRegisterLocal(original).Decision);
        return new WeakReference(original);
    }

    private static ExternalSecureDomainCreateRequest Request() => new(
        new ExternalDomainLease(new ExternalDomainLeaseHandle(Guid.NewGuid()), new ExternalDomainLeaseEpoch(1)),
        new ExternalSecureEvidenceContextHandle(Guid.NewGuid()),
        new ExternalSecureDomainProperties(ExternalSecureAssuranceClass.High, true, true),
        ExternalSecureComputeContract.Version);
    private static ExternalSecureDomainLease Domain() =>
        new(new ExternalSecureDomainHandle(Guid.NewGuid()), new ExternalSecureDomainGeneration(1));
    private static ExternalOperationIdentity Operation() =>
        new(new ExternalOperationHandle(Guid.NewGuid()), new ExternalOperationGeneration(1));
    private static ExternalSecureDomainCreateReceipt CreateReceipt(
        ExternalSecureDomainCreateRequest request, ExternalSecureDomainLease domain) => new(
            request, domain, request.RequiredProperties, new ExternalSecureDomainCreationProof(Guid.NewGuid()));

    private sealed class FakeProvider(ExternalSecureDomainCreateReceipt createReceipt) : IExternalSecureDomainProvider,
        IExternalSecureExecutionBindingProvider, IExternalSecureDomainCreateQueryProvider,
        IExternalSecureExecutionReservationProvider
    {
        public ExternalSecureExecutionReservationResult Reserve(ExternalSecureRegisterLocalRequest request) =>
            new(ExternalSecureDomainProviderStatus.Receipt, new(request, Guid.NewGuid()));
        public ExternalSecureExecutionReservationResult QueryReservation(ExternalSecureRegisterLocalRequest request) =>
            new(ExternalSecureDomainProviderStatus.Unavailable);
        public ExternalSecureExecutionReservationResult Release(ExternalSecureExecutionReservation reservation) =>
            new(ExternalSecureDomainProviderStatus.Receipt, reservation);
        public ExternalSecureExecutionReservationResult QueryRelease(ExternalSecureExecutionReservation reservation) =>
            new(ExternalSecureDomainProviderStatus.Unavailable);
        public int CreateQueries;
        public Func<ExternalSecureDomainCreateRequest, ExternalSecureDomainCreateProviderResult>? OnQueryCreate { get; set; }
        public ExternalSecureDomainCreateProviderResult QueryCreate(ExternalSecureDomainCreateRequest request)
        {
            Interlocked.Increment(ref CreateQueries);
            return OnQueryCreate is not null ? OnQueryCreate(request) : new(ExternalSecureDomainProviderStatus.Receipt, createReceipt);
        }
        public int CreateCalls;
        public int CloseCalls;
        public int BindingQueries;
        public Func<ExternalSecureRegisterLocalRequest, ExternalSecureExecutionBindingObservation>? OnRevalidate { get; set; }
        public ExternalSecureExecutionBindingObservation Revalidate(ExternalSecureRegisterLocalRequest request)
        {
            Interlocked.Increment(ref BindingQueries);
            return OnRevalidate is not null ? OnRevalidate(request) : new(request, ExternalSecureDomainProviderStatus.Receipt);
        }
        public Func<ExternalSecureDomainCreateRequest, ExternalSecureDomainCreateProviderResult>? OnCreate { get; set; }
        public Func<ExternalSecureDomainLease, ExternalOperationIdentity, ExternalSecureDomainCloseProviderResult>? OnClose { get; set; }
        public ExternalSecureDomainCreateProviderResult? CreateResult { get; set; }
        public ExternalSecureDomainCloseProviderResult? CloseResult { get; set; }
        public ExternalSecureDomainCreateProviderResult Create(ExternalSecureDomainCreateRequest request)
        {
            Interlocked.Increment(ref CreateCalls);
            return OnCreate is not null ? OnCreate(request) :
                CreateResult ?? new ExternalSecureDomainCreateProviderResult(ExternalSecureDomainProviderStatus.Receipt, createReceipt);
        }
        public ExternalSecureDomainCloseProviderResult Close(ExternalSecureDomainLease domain, ExternalOperationIdentity operation)
        {
            Interlocked.Increment(ref CloseCalls);
            return OnClose is not null ? OnClose(domain, operation) :
                CloseResult ?? new ExternalSecureDomainCloseProviderResult(ExternalSecureDomainProviderStatus.Rejected);
        }
    }
}
