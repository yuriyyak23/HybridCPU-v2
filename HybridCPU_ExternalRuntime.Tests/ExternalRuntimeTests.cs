using System.Reflection;
using System.Xml.Linq;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class ExternalRuntimeTests
{
    [Fact]
    public void Manifest_IsImmutablePerFamilyAndFailClosed()
    {
        var source = new List<HybridCpuExternalFeatureDescriptor>
        {
            new(HybridCpuExternalFeatureFamily.DomainLifecycle, HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1)
        };
        var manifest = new HybridCpuExternalFeatureManifest(HybridCpuExternalContractVersion.V1, 4, source);
        source.Clear();

        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            manifest.GetFeature(HybridCpuExternalFeatureFamily.DomainLifecycle).Availability);
        Assert.Equal(HybridCpuExternalFeatureDescriptor.Unavailable(HybridCpuExternalFeatureFamily.DeviceBinding),
            manifest.GetFeature(HybridCpuExternalFeatureFamily.DeviceBinding));
        Assert.Equal(HybridCpuExternalFeatureDescriptor.Unavailable(HybridCpuExternalFeatureFamily.SecureDomains),
            manifest.GetFeature(HybridCpuExternalFeatureFamily.SecureDomains));
        Assert.Throws<ArgumentException>(() => new HybridCpuExternalFeatureManifest(
            HybridCpuExternalContractVersion.V1, 1,
            [new(HybridCpuExternalFeatureFamily.DomainLifecycle, HybridCpuExternalFeatureAvailability.RuntimeAdmission, 1),
             new(HybridCpuExternalFeatureFamily.DomainLifecycle, HybridCpuExternalFeatureAvailability.Executable, 2)]));
        Assert.Throws<ArgumentException>(() => new HybridCpuExternalFeatureManifest(
            HybridCpuExternalContractVersion.V1, 1,
            [new(HybridCpuExternalFeatureFamily.DomainLifecycle, HybridCpuExternalFeatureAvailability.Unavailable, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HybridCpuExternalFeatureManifest(
            default, 1, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HybridCpuExternalFeatureManifest(
            HybridCpuExternalContractVersion.V1, 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => HybridCpuExternalFeatureManifest.CreateNext(manifest, 3, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.GetFeature((HybridCpuExternalFeatureFamily)255));
    }

    [Fact]
    public void ProductionFacade_ClaimsExecutableOnlyForCpuBackedImageFamilyAndOwnsExactClose()
    {
        var runtime = new HybridCpuExternalRuntime();
        HybridCpuExternalFeatureManifest features = runtime.QueryFeatures();
        Assert.Equal(HybridCpuExternalFeatureAvailability.RuntimeAdmission,
            features.GetFeature(HybridCpuExternalFeatureFamily.DomainLifecycle).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.Executable,
            features.GetFeature(HybridCpuExternalFeatureFamily.ChildExecutableImage).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.Unavailable,
            features.GetFeature(HybridCpuExternalFeatureFamily.DmaAdmission).Availability);
        Assert.Equal(HybridCpuExternalFeatureAvailability.Unavailable,
            features.GetFeature(HybridCpuExternalFeatureFamily.SecureDomains).Availability);

        ExternalDomainBindResult bind = runtime.BindDomain(Request(Op(1)));
        Assert.Equal(ExternalRuntimeOutcome.Bound, bind.Outcome);
        ExternalDomainLease lease = bind.Receipt!.Lease;
        ExternalDomainTransitionResult transition = runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(2));
        Assert.Equal(ExternalRuntimeOutcome.Unsupported, transition.Outcome);
        Assert.Null(transition.Receipt);

        ExternalOperationIdentity closeOperation = Op(3);
        ExternalDomainCloseResult close = runtime.CloseDomain(lease, closeOperation);
        Assert.Equal(ExternalRuntimeOutcome.Closed, close.Outcome);
        Assert.True(close.Receipt!.IsTerminal);
        Assert.Equal(lease, close.Receipt.Lease);
        Assert.Equal(closeOperation, close.Receipt.Operation);
        Assert.Equal(features.Generation, close.Receipt.ManifestGeneration);
        Assert.Equal(ExternalRuntimeOutcome.Stale, runtime.CloseDomain(lease, Op(4)).Outcome);
    }

    [Fact]
    public void LeaseValidation_RejectsForgedStaleAndWrongDomain()
    {
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.NotFound,
            runtime.TransitionDomain(new(new(Guid.NewGuid()), new(lease.Epoch.Value)), ExternalDomainTransition.Start, Op(2)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.TransitionDomain(lease with { Epoch = new(lease.Epoch.Value + 1) }, ExternalDomainTransition.Start, Op(3)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.NotFound,
            runtime.CloseDomain(default, Op(4)).Outcome);
    }

    [Fact]
    public void OperationIdentity_CannotCrossDomains()
    {
        var runtime = new HybridCpuExternalRuntime(() => new ExactOwner());
        ExternalDomainLease first = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
        ExternalDomainLease second = runtime.BindDomain(Request(Op(2))).Receipt!.Lease;
        ExternalOperationIdentity shared = Op(3);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionDomain(first, ExternalDomainTransition.Start, shared).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionDomain(second, ExternalDomainTransition.Start, shared).Outcome);
    }

    [Fact]
    public void ExactReceipts_DriveOnlyLegalLifecycleAndRejectReplay()
    {
        var owner = new ExactOwner();
        var runtime = new HybridCpuExternalRuntime(() => owner);
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;

        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Park, Op(2)).Outcome);
        ExternalOperationIdentity startOperation = Op(3);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Start, startOperation).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Park, startOperation).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Park, Op(4)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Resume, Op(5)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(lease, Op(6)).Outcome);
    }

    [Theory]
    [InlineData(FaultMode.LostResult)]
    [InlineData(FaultMode.WrongGeneration)]
    [InlineData(FaultMode.WrongLease)]
    [InlineData(FaultMode.WrongEpoch)]
    [InlineData(FaultMode.WrongTransition)]
    [InlineData(FaultMode.WrongContractVersion)]
    [InlineData(FaultMode.WrongResultingState)]
    [InlineData(FaultMode.DuplicateLate)]
    public void MalformedOrLostTransition_QuarantinesUntilExactTeardown(FaultMode mode)
    {
        var owner = new ExactOwner { Mode = mode };
        var runtime = new HybridCpuExternalRuntime(() => owner);
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.Unknown,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(2)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(3)).Outcome);
        owner.Mode = FaultMode.None;
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(lease, Op(4)).Outcome);
    }

    [Fact]
    public void DenialRevocationFaultAndThrowRemainDistinct()
    {
        foreach ((FaultMode mode, ExternalRuntimeOutcome expected) in new[]
        {
            (FaultMode.Denied, ExternalRuntimeOutcome.Denied),
            (FaultMode.Revoked, ExternalRuntimeOutcome.Revoked),
            (FaultMode.Faulted, ExternalRuntimeOutcome.Faulted),
            (FaultMode.Throw, ExternalRuntimeOutcome.Unknown),
        })
        {
            var owner = new ExactOwner();
            var runtime = new HybridCpuExternalRuntime(() => owner);
            ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
            owner.Mode = mode;
            Assert.Equal(expected, runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(2)).Outcome);
        }
    }

    [Theory]
    [InlineData(FaultMode.Denied, ExternalRuntimeOutcome.Denied)]
    [InlineData(FaultMode.Faulted, ExternalRuntimeOutcome.Faulted)]
    [InlineData(FaultMode.Throw, ExternalRuntimeOutcome.Unknown)]
    [InlineData(FaultMode.LostResult, ExternalRuntimeOutcome.Unknown)]
    [InlineData(FaultMode.WrongGeneration, ExternalRuntimeOutcome.Unknown)]
    public void BindFaults_DistinguishPrecisePreAcceptanceFromAmbiguity(
        FaultMode mode, ExternalRuntimeOutcome expected)
    {
        var owner = new ExactOwner { OpenMode = mode };
        var runtime = new HybridCpuExternalRuntime(() => owner);
        Assert.Equal(expected, runtime.BindDomain(Request(Op(1))).Outcome);
    }

    [Fact]
    public async Task CloseWithInFlightTransition_DrainsBeforeReleasingOwner()
    {
        var owner = new BlockingOwner();
        var runtime = new HybridCpuExternalRuntime(() => owner);
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
        Task<ExternalDomainTransitionResult> transition = Task.Run(() =>
            runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(2)));
        Assert.True(owner.Entered.Wait(TimeSpan.FromSeconds(5)));
        Task<ExternalDomainCloseResult> close = Task.Run(() => runtime.CloseDomain(lease, Op(3)));
        await Task.Delay(50);
        Assert.False(close.IsCompleted);
        owner.Release.Set();
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, (await transition).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, (await close).Outcome);
    }

    [Fact]
    public async Task TransitionDeadline_QuarantinesPinsAndSuppressesLateSuccessUntilExactClose()
    {
        var owner = new BlockingOwner();
        var runtime = new HybridCpuExternalRuntime(() => owner,
            backendCallDeadline: TimeSpan.FromMilliseconds(50));
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;

        ExternalDomainTransitionResult transition = runtime.TransitionDomain(
            lease, ExternalDomainTransition.Start, Op(2));
        Assert.Equal(ExternalRuntimeOutcome.Unknown, transition.Outcome);
        Assert.Null(transition.Receipt);
        Assert.Equal(ExternalRuntimeOutcome.Denied,
            runtime.TransitionDomain(lease, ExternalDomainTransition.Start, Op(3)).Outcome);

        Task<ExternalDomainCloseResult> close = Task.Run(() => runtime.CloseDomain(lease, Op(4)));
        await Task.Delay(50);
        Assert.False(close.IsCompleted);
        owner.Release.Set();
        Assert.Equal(ExternalRuntimeOutcome.Closed, (await close).Outcome);
    }

    [Fact]
    public void BindDeadline_QuarantinesWhenBackendOpenDoesNotReturn()
    {
        var owner = new BlockingOwner { BlockOpen = true };
        var runtime = new HybridCpuExternalRuntime(() => owner,
            backendCallDeadline: TimeSpan.FromMilliseconds(50));

        ExternalDomainBindResult bind = runtime.BindDomain(Request(Op(1)));

        Assert.Equal(ExternalRuntimeOutcome.Unknown, bind.Outcome);
        Assert.Null(bind.Receipt);
        owner.Release.Set();
    }

    [Fact]
    public void WrongTerminalBit_QuarantinesAndCanBeReconciledByLaterExactClose()
    {
        var owner = new ExactOwner { CloseMode = FaultMode.WrongTerminal };
        var runtime = new HybridCpuExternalRuntime(() => owner);
        ExternalDomainLease lease = runtime.BindDomain(Request(Op(1))).Receipt!.Lease;
        Assert.Equal(ExternalRuntimeOutcome.Unknown, runtime.CloseDomain(lease, Op(2)).Outcome);
        owner.CloseMode = FaultMode.None;
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(lease, Op(3)).Outcome);
    }

    [Fact]
    public void ObservationFailuresNeverChangeAuthority()
    {
        var diagnostics = new ThrowingDiagnostics();
        var runtime = new HybridCpuExternalRuntime(() => new ExactOwner(), diagnostics);
        ExternalDomainBindResult bind = runtime.BindDomain(Request(Op(1)));
        Assert.Equal(ExternalRuntimeOutcome.Bound, bind.Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(bind.Receipt!.Lease, Op(2)).Outcome);
        Assert.Equal(2, diagnostics.Calls);
    }

    [Fact]
    public void ContractsAbi_HasNoIseGuiSingNextOrRawHardwareSurface()
    {
        Assembly contracts = typeof(IHybridCpuExternalRuntime).Assembly;
        Assert.DoesNotContain(contracts.GetReferencedAssemblies(), static name =>
            name.Name!.Contains("ISE", StringComparison.OrdinalIgnoreCase) ||
            name.Name.Contains("SingNext", StringComparison.OrdinalIgnoreCase) ||
            name.Name.Contains("GUI", StringComparison.OrdinalIgnoreCase));
        string[] forbidden = ["DomainTag", "AddressSpaceTag", "VMCS", "VMX", "APIC", "MSI", "GSI", "IOMMU",
            "PhysicalAddress", "BusAddress", "Opcode", "Lane"];
        foreach (Type type in contracts.GetExportedTypes())
        {
            Assert.DoesNotContain(forbidden, word => type.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                Assert.False(method.CallingConvention.HasFlag(CallingConventions.VarArgs));
                Assert.DoesNotContain(method.GetParameters(), static parameter => parameter.ParameterType == typeof(object[]));
            }
        }
    }

    [Fact]
    public void ProjectDependencyBoundary_IsExactAndIseIsPackagePrivate()
    {
        string root = FindRepositoryRoot();
        XDocument contracts = XDocument.Load(Path.Combine(root,
            "HybridCPU_ExternalRuntime.Contracts", "HybridCPU_ExternalRuntime.Contracts.csproj"));
        Assert.Empty(contracts.Descendants("ProjectReference"));

        XDocument runtime = XDocument.Load(Path.Combine(root,
            "HybridCPU_ExternalRuntime", "HybridCPU_ExternalRuntime.csproj"));
        XElement[] references = runtime.Descendants("ProjectReference").ToArray();
        Assert.Equal(3, references.Length);
        Assert.Contains(references, item => ((string?)item.Attribute("Include"))!.EndsWith(
            "HybridCPU_ExternalRuntime.Contracts\\HybridCPU_ExternalRuntime.Contracts.csproj", StringComparison.Ordinal));
        XElement ise = Assert.Single(references, item => ((string?)item.Attribute("Include"))!.EndsWith(
            "HybridCPU_ISE\\HybridCPU_ISE.csproj", StringComparison.Ordinal));
        Assert.Equal("all", (string?)ise.Attribute("PrivateAssets"));
        XElement compiler = Assert.Single(references, item => ((string?)item.Attribute("Include"))!.EndsWith(
            "HybridCPU_Compiler\\Core\\HybridCPU.Compiler.Core.csproj", StringComparison.Ordinal));
        Assert.Equal("all", (string?)compiler.Attribute("PrivateAssets"));
        Assert.DoesNotContain(runtime.Descendants(), item =>
            item.Value.Contains("SingNextOS", StringComparison.OrdinalIgnoreCase));
    }

    private static ExternalOperationIdentity Op(ulong generation) => new(new(Guid.NewGuid()), new(generation));
    private static ExternalDomainBindRequest Request(ExternalOperationIdentity operation) =>
        new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, operation);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HybridCPU v2.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    public enum FaultMode
    {
        None, Denied, Revoked, Faulted, Throw, LostResult, WrongGeneration, WrongLease, WrongEpoch,
        WrongTransition, WrongContractVersion, WrongResultingState, WrongTerminal, DuplicateLate
    }

    private sealed class ExactOwner : IExternalDomainExecutionOwner
    {
        public FaultMode OpenMode { get; set; }
        public FaultMode Mode { get; set; }
        public FaultMode CloseMode { get; set; }

        public BackendBindResult Open(ExternalDomainLease lease, ExternalOperationIdentity operation,
            HybridCpuExternalContractVersion version, ulong generation)
        {
            if (OpenMode == FaultMode.Throw) throw new InvalidOperationException("ambiguous open");
            if (OpenMode == FaultMode.Denied) return new(BackendOutcome.Denied, null, "denied before accept");
            if (OpenMode == FaultMode.Faulted) return new(BackendOutcome.Faulted, null, "fault before accept");
            if (OpenMode == FaultMode.LostResult) return new(BackendOutcome.Unknown, null, "lost after accept");
            ulong returnedGeneration = OpenMode == FaultMode.WrongGeneration ? generation + 1 : generation;
            return new(BackendOutcome.Accepted,
                new(lease, operation, version, returnedGeneration, ExternalDomainState.Ready), string.Empty);
        }

        public BackendTransitionResult Transition(ExternalDomainLease lease, ExternalDomainTransition transition,
            ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong generation)
        {
            if (Mode == FaultMode.Throw) throw new InvalidOperationException("accept-then-lost");
            if (Mode == FaultMode.Denied) return new(BackendOutcome.Denied, null, "pre-accept denial");
            if (Mode == FaultMode.Revoked) return new(BackendOutcome.Revoked, null, "revoked");
            if (Mode == FaultMode.Faulted) return new(BackendOutcome.Faulted, null, "faulted");
            if (Mode == FaultMode.LostResult) return new(BackendOutcome.Unknown, null, "lost");
            ExternalDomainLease returnedLease = Mode switch
            {
                FaultMode.WrongLease => lease with { Handle = new(Guid.NewGuid()) },
                FaultMode.WrongEpoch => lease with { Epoch = new(lease.Epoch.Value + 1) },
                _ => lease,
            };
            var returnedOperation = Mode is FaultMode.WrongGeneration or FaultMode.DuplicateLate
                ? operation with { Generation = new(operation.Generation.Value + 1) } : operation;
            ExternalDomainTransition returnedTransition = Mode == FaultMode.WrongTransition
                ? ExternalDomainTransition.Park : transition;
            ExternalDomainState state = Mode == FaultMode.WrongResultingState
                ? ExternalDomainState.Closed
                : returnedTransition == ExternalDomainTransition.Park
                ? ExternalDomainState.Parked : ExternalDomainState.Running;
            HybridCpuExternalContractVersion returnedVersion = Mode == FaultMode.WrongContractVersion
                ? new(version.Major, checked((ushort)(version.Minor + 1)), version.Patch)
                : version;
            return new(BackendOutcome.Accepted,
                new(returnedLease, returnedOperation, returnedVersion, generation, returnedTransition, state), string.Empty);
        }

        public BackendCloseResult Close(ExternalDomainLease lease, ExternalOperationIdentity operation,
            HybridCpuExternalContractVersion version, ulong generation) =>
            new(BackendOutcome.Accepted,
                new(lease, operation, version, generation, ExternalDomainState.Closed,
                    IsTerminal: CloseMode != FaultMode.WrongTerminal), string.Empty);
    }

    private sealed class BlockingOwner : IExternalDomainExecutionOwner
    {
        public bool BlockOpen { get; set; }
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public BackendBindResult Open(ExternalDomainLease lease, ExternalOperationIdentity operation,
            HybridCpuExternalContractVersion version, ulong generation)
        {
            if (BlockOpen)
            {
                Entered.Set();
                Release.Wait(TimeSpan.FromSeconds(5));
            }
            return new(BackendOutcome.Accepted, new(lease, operation, version, generation, ExternalDomainState.Ready), string.Empty);
        }
        public BackendTransitionResult Transition(ExternalDomainLease lease, ExternalDomainTransition transition,
            ExternalOperationIdentity operation, HybridCpuExternalContractVersion version, ulong generation)
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
            return new(BackendOutcome.Accepted,
                new(lease, operation, version, generation, transition, ExternalDomainState.Running), string.Empty);
        }
        public BackendCloseResult Close(ExternalDomainLease lease, ExternalOperationIdentity operation,
            HybridCpuExternalContractVersion version, ulong generation) =>
            new(BackendOutcome.Accepted,
                new(lease, operation, version, generation, ExternalDomainState.Closed, true), string.Empty);
    }

    private sealed class ThrowingDiagnostics : IExternalDomainDiagnostics
    {
        public int Calls { get; private set; }
        public void Attach(ExternalDomainLease lease, ulong manifestGeneration) { Calls++; throw new IOException("attach"); }
        public void Detach(ExternalDomainLease lease, ulong manifestGeneration) { Calls++; throw new IOException("disconnect"); }
    }
}
