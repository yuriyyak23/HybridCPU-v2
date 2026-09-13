using HybridCPU_ISE;
using HybridCPU_ISE.Machine;
using HybridCPU_ISE.NonRTL.Runtime;
using HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed;
using YAKSys_Hybrid_CPU;
using CpuInterfaceBridge.Diagnostics;

internal static class OwnedCoreObservationRegression
{
    private sealed class FailingConsumer : IOwnedCoreObservationConsumer
    {
        public int Calls;
        public int Closes;
        public void OnBoundary() { Calls++; throw new InvalidOperationException("test capture fault"); }
        public void Dispose() { Closes++; throw new InvalidOperationException("test close fault"); }
    }
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        void Reject<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { checks++; return; }
            throw new InvalidOperationException($"Expected {typeof(T).Name}");
        }
        Reject<ArgumentNullException>(() => new OwnedCoreObservationSource(null!));
        var memory = new HybridCpuIseSparseMainMemoryAreaV1();
        var core = new Processor.CPU_Core(0, CpuCorePlatformContext.CreateFixed(memory, ProcessorMode.Compiler));
        core.InitializePipeline();
        core.PrepareExecutionStart(0x10000, 0);
        core.WriteCommittedArch(0, 18, 0x12345678);
        var failing = new FailingConsumer();
        OwnedCoreObservationBinding? binding = null;
        using (var session = new OwnedCoreObservationSession(core, 0x10000, value => { binding = value; return failing; }))
        {
            session.OnBoundary();
            session.OnBoundary();
            Check(failing.Calls == 1 && failing.Closes == 1, "Faulted consumer was called again");
            Check(session.Failure == "test capture fault | close: test close fault", "Diagnostic failure lost");
            Reject<ObjectDisposedException>(() => binding!.Observer.GetCoreState(0));
        }
        using (var session = new OwnedCoreObservationSession(core, 0x10000,
            value => { binding = value; throw new InvalidOperationException("test attach fault"); }))
        {
            Check(session.Failure == "test attach fault", "Attach fault escaped or disappeared");
            Reject<ObjectDisposedException>(() => binding!.Observer.GetCoreState(0));
            Check(session.Report.Closed && session.Report.BoundariesObserved == 0, "Attach failure left source open.");
        }
        using var source = new OwnedCoreObservationSource(core);
        var observer = new IseObservationService(source, new object());
        var first = observer.GetCoreState(0);
        Check(source.SourceProvenance == MachineStateSourceProvenance.LiveCore, "Wrong provenance");
        Check(first.ActiveVirtualThreadRegisters[18] == 0x12345678, "Wrong bound core register");
        Check(first.LiveInstructionPointer == 0x10000, "Wrong startup live PC");
        core.WriteCommittedArch(0, 18, 0xabcdef);
        var second = observer.GetCoreState(0);
        Check(second.ActiveVirtualThreadRegisters[18] == 0xabcdef, "Stale capture");
        Check(first.ActiveVirtualThreadRegisters[18] == 0x12345678, "Mutable old snapshot");
        Check(second.CycleCount == first.CycleCount, "Observation executed CPU cycles");
        Reject<ArgumentOutOfRangeException>(() => source.GetCoreSnapshot(1));
        Reject<MachineStateSourceUnavailableException>(() => source.ReadMemory(0, 8));
        Exception? crossThread = null;
        var thread = new Thread(() =>
        {
            try { observer.GetCoreState(0); } catch (Exception ex) { crossThread = ex; }
        });
        thread.Start();
        Check(thread.Join(TimeSpan.FromSeconds(5)), "Cross-thread read blocked");
        Check(crossThread is InvalidOperationException, "Cross-thread capture was not rejected");
        using var endpoint = IseHostObservationAdapter.Create("owned-core regression", observer);
        Reject<InvalidOperationException>(() => endpoint.Connect(endpoint.Descriptor.Identity with { InstanceId = Guid.NewGuid() }));
        using var connection = endpoint.Connect(endpoint.Descriptor.Identity);
        var captured = connection.CaptureOnce(0);
        Check(captured.Status == HostCaptureStatus.Observed, "Bridge capture failed: " + captured.Reason);
        Check(captured.Frame!.Snapshot.ActiveVirtualThreadRegisters[18] == 0xabcdef, "Bridge read wrong core");
        Check(captured.Frame.SourceKind == HostObservationSourceKind.LiveCore, "Bridge lost provenance");
        Check(!endpoint.Descriptor.SupportsRemoteAttach && !endpoint.Descriptor.SupportsExecutionControl,
            "In-process endpoint advertises unsupported capabilities");
        connection.Dispose();
        Check(connection.State == HostConnectionState.Detached, "Bridge detach failed");
        core.WriteCommittedArch(0, 18, 9);
        using var reconnected = endpoint.Connect(endpoint.Descriptor.Identity);
        Check(reconnected.ConnectionId != connection.ConnectionId, "Reconnect reused connection identity");
        Check(reconnected.CaptureOnce(0).Frame!.Snapshot.ActiveVirtualThreadRegisters[18] == 9, "Detach affected core");
        source.Dispose();
        Check(reconnected.CaptureOnce(0).Status == HostCaptureStatus.ProviderFault, "Closed source returned stale frame");
        Reject<ObjectDisposedException>(() => observer.GetCoreState(0));
        core.WriteCommittedArch(0, 18, 7);
        Check(core.ReadArch(0, 18) == 7, "Detach affected CPU ownership");
        Console.WriteLine($"PASS owned-core observation: {checks} checks; not guest qualification.");
        return 0;
    }
}
