using System.Diagnostics;
using HybridCPU.Compiler.NativeAot;
using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed class CpuBackedChildExecutionTests
{
    [Fact]
    public void V3CorrelatesArtifactMappingExecutionAndRetiredWorkAndClosesBoundedIo()
    {
        byte[] package = Compile(nameof(LongRun));
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(20))).Receipt!.Lease;
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(),
            ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory | ExternalChildAuthority.VirtualIo,
            16 * 1024 * 1024, Op(21))).Receipt!.Lease;
        ExternalGuestMappingLease mapping = runtime.MapChildGuestMemory(child,
            new(0, 16 * 1024 * 1024, Op(22))).Receipt!.Mapping;

        var artifact = runtime.BindChildExecutableArtifact(child,
            new(mapping, package, 2_000_000, Op(23)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, artifact.Outcome);
        Assert.Equal(mapping.Handle, artifact.Receipt!.MappingHandle);
        Assert.Equal(mapping.Epoch, artifact.Receipt.MappingEpoch);

        var wrongMapping = artifact.Receipt with { MappingEpoch = new(mapping.Epoch.Value + 1) };
        Assert.NotEqual(wrongMapping.MappingEpoch, artifact.Receipt.MappingEpoch);
        var execution = runtime.StartChildExecution(child,
            new(artifact.Receipt.ArtifactHandle, artifact.Receipt.ArtifactEpoch, Op(24)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, execution.Outcome);
        Assert.Equal(artifact.Receipt.ArtifactHandle, execution.Receipt!.ArtifactHandle);
        Assert.Equal(mapping.Handle, execution.Receipt.MappingHandle);
        Assert.True(execution.Receipt.ExecutionGeneration.Value > 0);
        Assert.True(execution.Receipt.RetiredPipelineCycles > 0);
        Assert.True(execution.Receipt.LastRetireSequence > 0);
        Assert.True(execution.Receipt.IsTerminal);

        var io = runtime.BindChildVirtualIo(child, new(new(Guid.NewGuid()), new(1),
            ExternalChildVirtualIoRights.Read, 4096, Op(25)));
        Assert.Equal(ExternalRuntimeOutcome.Succeeded, io.Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Stale,
            runtime.CloseChildVirtualIo(child, io.Receipt!.IoHandle,
                new(io.Receipt.IoEpoch.Value + 1), Op(26)).Outcome);
        var ioClose = runtime.CloseChildVirtualIo(child, io.Receipt.IoHandle, io.Receipt.IoEpoch, Op(27));
        Assert.Equal(ExternalRuntimeOutcome.Closed, ioClose.Outcome);
        Assert.True(ioClose.Receipt!.IsTerminal);

        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(28)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(29)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(30)).Outcome);
    }

    [Fact]
    public void CpuBackedChildProducesIndependentStartParkResumeAndTerminalCloseReceipts()
    {
        byte[] package = Compile(nameof(LongRun));
        var runtime = new HybridCpuExternalRuntime();
        ExternalDomainLease parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(1))).Receipt!.Lease;
        ExternalChildDomainLease child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(),
            ExternalChildAuthority.Execute, 4096, Op(2))).Receipt!.Lease;
        var loaded = runtime.LoadChildExecutableImage(child, new(package, 2_000_000, Op(3)));

        Assert.True(loaded.Outcome == ExternalRuntimeOutcome.Succeeded, loaded.Reason);
        Assert.Matches("^[0-9a-f]{64}$", loaded.Receipt!.PackageSha256);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Start, Op(4)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Park, Op(5)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Resume, Op(6)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Succeeded,
            runtime.TransitionChildDomain(child, ExternalChildDomainTransition.Park, Op(7)).Outcome);
        ExternalChildResult<ExternalChildDomainCloseReceipt> close = runtime.CloseChildDomain(child, Op(8));
        Assert.True(close.Outcome == ExternalRuntimeOutcome.Closed, close.Reason);
        Assert.True(close.Receipt!.IsTerminal);
    }

    public static int LongRun()
    {
        int a = 1;
        return a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a + a;
    }

    private static byte[] Compile(string method)
    {
        string output = Path.Combine(Path.GetTempPath(), $"external-child-{Guid.NewGuid():N}.hce");
        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(typeof(NativeAotSeamArtifactV1).Assembly.Location);
            start.ArgumentList.Add("compile-image");
            Add(start, "--assembly", typeof(CpuBackedChildExecutionTests).Assembly.Location);
            Add(start, "--type", typeof(CpuBackedChildExecutionTests).FullName!);
            Add(start, "--method", method);
            Add(start, "--out", output);
            Add(start, "--source-commit", NativeAotSeamBaselineV1.SourceCommit);
            Add(start, "--patch-digest", NativeAotSeamBaselineV1.PatchDigest);
            using Process process = Process.Start(start)!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, error);
            return File.ReadAllBytes(output);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(output + ".json")) File.Delete(output + ".json");
        }
    }

    private static void Add(ProcessStartInfo start, string name, string value)
    {
        start.ArgumentList.Add(name);
        start.ArgumentList.Add(value);
    }

    private static ExternalOperationIdentity Op(ulong generation) => new(new(Guid.NewGuid()), new(generation));
}
