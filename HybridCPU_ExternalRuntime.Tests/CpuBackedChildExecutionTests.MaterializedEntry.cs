using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime.Tests;

public sealed partial class CpuBackedChildExecutionTests
{
    [Theory]
    [InlineData(nameof(LongRun), ExternalExecutionTerminalKind.BudgetExhausted)]
    [InlineData(nameof(FiniteBranchReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(CallTreeReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedArrayReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedArrayBoundsReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedCatchFinallyReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedFinallyReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedRethrowReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedCalleeCatchFinallyReturn), ExternalExecutionTerminalKind.Completed)]
    [InlineData(nameof(ManagedCalleeFinallyCatchReturn), ExternalExecutionTerminalKind.Completed)]
    public void CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
        string method, ExternalExecutionTerminalKind expectedTerminal)
    {
        var runtime = new HybridCpuExternalRuntime();
        var parent = runtime.BindDomain(new(Guid.NewGuid(), ExternalDomainProfile.IsolatedDomain, Op(910))).Receipt!.Lease;
        var child = runtime.CreateChildDomain(parent, new(Guid.NewGuid(), ExternalChildAuthority.Execute | ExternalChildAuthority.GuestMemory,
            16 * 1024 * 1024, Op(911))).Receipt!.Lease;
        var mapping = runtime.MapChildGuestMemory(child, new(0, 16 * 1024 * 1024, Op(912))).Receipt!.Mapping;
        var bind = runtime.BindChildExecutableArtifact(child, new(mapping, Compile(method),
            method is nameof(ManagedMultiStaticReturn) or nameof(ManagedMultiFailedStaticReturn) or nameof(ManagedFailedStaticReturn) or nameof(ManagedForeignStaticMethodReturn) or nameof(ManagedForeignStaticDirectReturn) or nameof(ManagedNominalReferenceReturn) or nameof(ManagedNominalNullReturn) or nameof(ManagedNominalSlotReturn) or nameof(ManagedNominalOverloadReturn) or nameof(ManagedNestedCatchRethrowReturn) or nameof(ManagedNestedForeignCatchRethrowReturn) or nameof(ManagedFinallyReplacementReturn) or nameof(ManagedObjectFieldReturn) or nameof(ManagedInstanceCallReturn) or nameof(ManagedVirtualDispatchReturn) or nameof(ManagedInheritedSlotReturn) or nameof(ManagedHiddenSlotReturn) or nameof(ManagedNullVirtualReturn) or nameof(ManagedDeepHiddenSlotReturn) or nameof(ManagedDistinctSlotsReturn) ? 80000 :
            method == nameof(ManagedAncestorRethrowReturn) ? 60000 :
            method is nameof(ManagedCalleeCatchFinallyReturn) or nameof(ManagedCalleeFinallyCatchReturn) ? 40000 :
            method is nameof(ManagedArrayBoundsReturn) or nameof(ManagedCatchFinallyReturn) or nameof(ManagedFinallyReturn) or nameof(ManagedRethrowReturn) ? 20000 : 1000, Op(913)));
        Assert.True(bind.Outcome == ExternalRuntimeOutcome.Succeeded, bind.Reason);
        var artifact = bind.Receipt!;
        var request = new ExternalOriginalChildExecutionStartRequest(child, artifact,
            new(artifact.ArtifactHandle, artifact.ArtifactEpoch, Op(914)));
        var raw = runtime.StartOriginalChildExecution(request)!;
        object? failedCpu = null;
        if (raw.Raw.Outcome != ExternalRuntimeOutcome.Succeeded)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var records = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!;
            var owner = records[child.Handle]!;
            var execution = owner.GetType().GetProperty("Execution")!.GetValue(owner)!;
            var run = execution.GetType().GetField("run", flags)!.GetValue(execution)!;
            failedCpu = run.GetType().GetProperty("Result")!.GetValue(run);
        }
        Assert.True(raw.Raw.Outcome == ExternalRuntimeOutcome.Succeeded, $"{raw.Raw.Reason}; CPU={failedCpu}");
        var terminal = runtime.ResolveOriginalExecutionTerminal(request)!;
        Assert.NotNull(terminal);
        Assert.Same(raw.Raw.Receipt, terminal.Receipt);
        Assert.Equal(expectedTerminal, terminal.Kind);
        Assert.Equal(expectedTerminal == ExternalExecutionTerminalKind.Completed
            ? method is nameof(ManagedMultiFailedStaticReturn) or nameof(ManagedFailedStaticReturn) or nameof(ManagedArrayBoundsReturn) or nameof(ManagedRethrowReturn) ? 255 : 42 : (int?)null, terminal.ProcessExitCode);
        Assert.False(runtime.ValidateCurrentOriginalExecutionTerminal(request, terminal with { ProcessExitCode = 0 }));
        Assert.True(terminal.Receipt.LastRetireSequence > 5);
        Assert.Same(terminal, runtime.ResolveOriginalExecutionTerminal(request));
        Assert.True(runtime.ValidateCurrentOriginalExecutionTerminal(request, terminal));
        if (expectedTerminal == ExternalExecutionTerminalKind.Completed || method is nameof(ManagedFailedStaticReturn) or nameof(ManagedMultiFailedStaticReturn))
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var records = (System.Collections.IDictionary)typeof(HybridCpuExternalRuntime).GetField("childRegistry", flags)!.GetValue(runtime)!;
            var owner = records[child.Handle]!;
            var execution = owner.GetType().GetProperty("Execution")!.GetValue(owner)!;
            var run = execution.GetType().GetField("run", flags)!.GetValue(execution)!;
            var result = run.GetType().GetProperty("Result")!.GetValue(run)!;
            Assert.Equal(method is nameof(ManagedMultiFailedStaticReturn) or nameof(ManagedFailedStaticReturn) or nameof(ManagedArrayBoundsReturn) or nameof(ManagedRethrowReturn) ? 255 : 42,
                result.GetType().GetProperty("ProcessExitCode")!.GetValue(result));
            if (method is nameof(ManagedFailedStaticReturn) or nameof(ManagedMultiFailedStaticReturn))
            {
                var diagnostics = (HybridCPU_ISE.NonRTL.Runtime.OwnedCoreObservationDiagnostics)
                    result.GetType().GetProperty("ObservationDiagnostics")!.GetValue(result)!;
                Assert.Equal(0, diagnostics.FailedSegments);
                Assert.Equal(method == nameof(ManagedMultiFailedStaticReturn) ? 2 : 1, diagnostics.Segments.Count);
                Assert.All(diagnostics.Segments, segment =>
                {
                    Assert.Equal("Initializer", segment.SegmentKind);
                    Assert.True(segment.Closed);
                });
            }
            if (method == nameof(ManagedMultiStaticReturn))
            {
                var diagnostics = (HybridCPU_ISE.NonRTL.Runtime.OwnedCoreObservationDiagnostics)
                    result.GetType().GetProperty("ObservationDiagnostics")!.GetValue(result)!;
                Assert.Equal(0, diagnostics.FailedSegments);
                Assert.Equal(["Initializer", "Initializer", "Main"], diagnostics.Segments.Select(segment => segment.SegmentKind));
                Assert.All(diagnostics.Segments, segment => Assert.True(segment.Closed));
            }
            if (method is nameof(ManagedForeignStaticMethodReturn) or nameof(ManagedForeignStaticDirectReturn) or nameof(ManagedNominalReferenceReturn) or nameof(ManagedNominalNullReturn))
            {
                int safepoints = (int)result.GetType().GetProperty("GcSafepointsObserved")!.GetValue(result)!;
                var digests = (IReadOnlyList<string>)result.GetType().GetProperty("GcResultDigests")!.GetValue(result)!;
                Assert.True(safepoints > 0);
                Assert.Equal(safepoints, digests.Count);
                Assert.All(digests, digest => Assert.Matches("^[0-9a-f]{64}$", digest));
            }
        }
        var release = new ExternalArtifactReleaseRequest(child, artifact, Op(915));
        Assert.True(runtime.ReleaseChildExecutableArtifact(release).Receipt!.NoFurtherUse);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.UnmapChildGuestMemory(mapping, Op(916)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseChildDomain(child, Op(917)).Outcome);
        Assert.Equal(ExternalRuntimeOutcome.Closed, runtime.CloseDomain(parent, Op(918)).Outcome);
    }

    public static int FiniteBranchReturn()
    {
        int value = 1;
        if (value == 1) return 42;
        return 0;
    }

    public sealed class GuestNode
    {
        public int Value;
        public GuestNode? Next;
        public int ReadWithAllocation()
        {
            var garbage = new GuestNode();
            garbage.Value = 99;
            return Value + Next!.Value + 2;
        }
    }

    public static int ManagedObjectFieldReturn()
    {
        var root = new GuestNode();
        root.Value = 20;
        var child = new GuestNode();
        child.Value = 20;
        root.Next = child;
        return ReadGuestNode(root);
    }

    private static int ReadGuestNode(GuestNode root)
    {
        var garbage = new GuestNode();
        garbage.Value = 99;
        return root.Value + root.Next!.Value + 2;
    }

    public static int ManagedInstanceCallReturn()
    {
        var root = new GuestNode();
        root.Value = 20;
        root.Next = new GuestNode();
        root.Next.Value = 20;
        return root.ReadWithAllocation();
    }

    [Fact]
    public void CompiledInstanceReceiverSurvivesAllocationAndReadsOriginalFields()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedInstanceCallReturn), ExternalExecutionTerminalKind.Completed);

    public class GuestBase
    {
        public int Value;
        public virtual int ReadValue() => 99;
    }

    public class SlotBase
    {
        public int Value;
        public virtual int ReadValue() => 99;
    }
    public class SlotOverride : SlotBase
    {
        public override int ReadValue() => Value + 2;
    }
    public sealed class SlotInherited : SlotOverride { }
    public class SlotHidden : SlotOverride
    {
        public new virtual int ReadValue() => 99;
    }
    public sealed class SlotHiddenChild : SlotHidden
    {
        public override int ReadValue() => 99;
    }
    public static int ManagedDeepHiddenSlotReturn()
    {
        SlotBase receiver = new SlotHiddenChild();
        receiver.Value = 40;
        return receiver.ReadValue();
    }
    public static int ManagedDistinctSlotsReturn()
    {
        var receiver = new SlotHiddenChild();
        receiver.Value = 40;
        SlotBase original = receiver;
        SlotHidden hidden = receiver;
        int first = original.ReadValue();
        int second = hidden.ReadValue();
        return first == 42 && second == 99 ? 42 : 0;
    }
    [Fact]
    public void CompiledBothVirtualSlotOriginsRemainDistinctOnSameReceiver()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedDistinctSlotsReturn), ExternalExecutionTerminalKind.Completed);
    [Fact]
    public void CompiledDescendantOverrideCannotReplaceHiddenAncestorSlot()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedDeepHiddenSlotReturn), ExternalExecutionTerminalKind.Completed);
    public interface IUnsupportedSlotMarker { int Marker(); }
    public class UnsupportedSlotBase { public virtual int ReadValue() => 42; }
    public sealed class UnsupportedSlotImpl : UnsupportedSlotBase, IUnsupportedSlotMarker
    {
        int IUnsupportedSlotMarker.Marker() => 1;
    }
    public static int UnsupportedMethodImplSlotReturn()
    {
        UnsupportedSlotBase receiver = new UnsupportedSlotImpl();
        return receiver.ReadValue();
    }
    public class UnsupportedGenericSlotBase { public virtual int ReadValue() => 42; }
    public class UnsupportedGenericSlot<T> : UnsupportedGenericSlotBase { }
    public static int UnsupportedGenericSlotReturn()
    {
        UnsupportedGenericSlotBase receiver = new UnsupportedGenericSlot<int>();
        return receiver.ReadValue();
    }
    [Theory]
    [InlineData(nameof(UnsupportedMethodImplSlotReturn))]
    [InlineData(nameof(UnsupportedGenericSlotReturn))]
    public void AutomaticVirtualPlannerRejectsUnsupportedMetadataBeforeArtifact(string method)
        => Assert.Empty(Compile(method, expectSuccess: false, expectedDiagnostic: "HCCIL1501"));
    public static int ManagedInheritedSlotReturn()
    {
        SlotBase receiver = new SlotInherited();
        receiver.Value = 40;
        return receiver.ReadValue();
    }
    public static int ManagedHiddenSlotReturn()
    {
        SlotBase receiver = new SlotHidden();
        receiver.Value = 40;
        return receiver.ReadValue();
    }
    public static int ManagedNullVirtualReturn()
    {
        SlotBase receiver = null!;
        try { return receiver.ReadValue(); }
        catch (NullReferenceException) { return 42; }
    }
    [Theory]
    [InlineData(nameof(ManagedInheritedSlotReturn))]
    [InlineData(nameof(ManagedHiddenSlotReturn))]
    [InlineData(nameof(ManagedNullVirtualReturn))]
    public void CompiledAutomaticSlotBoundaryExecutesOriginalSemantics(string method)
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            method, ExternalExecutionTerminalKind.Completed);

    public sealed class GuestDerived : GuestBase
    {
        public override int ReadValue()
        {
            var garbage = new GuestNode();
            garbage.Value = 99;
            return Value + 2;
        }
    }

    public static int ManagedVirtualDispatchReturn()
    {
        GuestBase receiver = new GuestDerived();
        receiver.Value = 40;
        return receiver.ReadValue();
    }

    [Fact]
    public void CompiledVirtualSlotSelectsOriginalDerivedReceiver()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedVirtualDispatchReturn), ExternalExecutionTerminalKind.Completed);

    [Fact]
    public void StandaloneVirtualDispatchRejectsTamperedMetadataBeforeArtifact()
        => Assert.Empty(Compile(nameof(ManagedVirtualDispatchReturn), expectSuccess: false, tamperMetadata: true));

    [Fact]
    public void CompiledObjectReferenceFieldSurvivesCalleeAllocationSafepoints()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedObjectFieldReturn), ExternalExecutionTerminalKind.Completed);

    public static int CallTreeReturn() => AddBranch(20) + AddBranch(21);
    private static int AddBranch(int value) => value < 21 ? value + 1 : value;

    [Fact]
    public void StandaloneLinkedImageRejectsUnresolvedExternalCalleeBeforeArtifact()
        => Assert.Empty(Compile(nameof(UnresolvedExternalReturn), expectSuccess: false));

    public static int UnresolvedExternalReturn() => Environment.TickCount;

    public static int ManagedCalleeCatchFinallyReturn()
    {
        int result = 0;
        try { result = AllocateArrayBounds(); }
        catch (IndexOutOfRangeException) { result = 40; }
        finally { result = result + 2; }
        return result;
    }

    public static int ManagedCalleeFinallyCatchReturn()
    {
        var values = new int[2];
        int result = 0;
        try { WriteBoundsAndFinally(values); }
        catch (IndexOutOfRangeException) { result = values[0]; }
        finally { result = result + 2; }
        return result;
    }

    private static void WriteBoundsAndFinally(int[] values)
    {
        try { values[2] = 7; }
        finally { values[0] = 40; }
    }

    public static int ManagedAncestorRethrowReturn()
    {
        var values = new int[2];
        int result = 0;
        try { IntermediateRethrowFinally(values); }
        catch (IndexOutOfRangeException) { result = values[0] + values[1]; }
        finally { result = result + 2; }
        return result;
    }

    private static void IntermediateRethrowFinally(int[] values)
    {
        try { LeafBoundsRethrow(values); }
        finally { values[1] = 20; }
    }

    private static void LeafBoundsRethrow(int[] values)
    {
        try { values[2] = 7; }
        catch (IndexOutOfRangeException) { values[0] = 20; throw; }
    }

    [Fact]
    public void CompiledRethrowRunsIntermediateFinallyBeforeAncestorCatch()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedAncestorRethrowReturn), ExternalExecutionTerminalKind.Completed);

    public static int ManagedFinallyReturn()
    {
        int result = 0;
        try { result = 40; }
        finally { result = result + 2; }
        return result;
    }

    public static int ManagedNestedCatchRethrowReturn()
    {
        var values = new int[2];
        try { NestedCatchLeaf(values); }
        catch (IndexOutOfRangeException) { return values[0] + values[1] + 2; }
        return 0;
    }

    private static void NestedCatchLeaf(int[] values)
    {
        try { values[2] = 7; }
        catch (IndexOutOfRangeException)
        {
            try { values[2] = 9; }
            catch (IndexOutOfRangeException) { values[0] = 20; }
            values[1] = 20;
            throw;
        }
    }

    [Fact]
    public void CompiledNestedCatchPreservesOuterRethrowIntoAncestor()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedNestedCatchRethrowReturn), ExternalExecutionTerminalKind.Completed);

    public static int ManagedNestedForeignCatchRethrowReturn()
    {
        var values = new int[2];
        try { NestedForeignCatchLeaf(values); }
        catch (IndexOutOfRangeException) { return values[0] + values[1] + 2; }
        catch (NullReferenceException) { return 99; }
        return 0;
    }

    private static void NestedForeignCatchLeaf(int[] values)
    {
        try { values[2] = 7; }
        catch (IndexOutOfRangeException)
        {
            int[] missing = null!;
            try { missing[0] = 9; }
            catch (NullReferenceException) { values[0] = 20; }
            values[1] = 20;
            throw;
        }
    }

    [Fact]
    public void CompiledNestedForeignCatchRethrowsOriginalBoundsIdentity()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedNestedForeignCatchRethrowReturn), ExternalExecutionTerminalKind.Completed);

    public static int ManagedRethrowReturn()
    {
        try
        {
            var values = new int[2];
            values[2] = 7;
            return 42;
        }
        catch (IndexOutOfRangeException) { throw; }
    }

    public static int ManagedFinallyReplacementReturn()
    {
        var values = new int[2];
        try { FinallyReplacementLeaf(values); }
        catch (NullReferenceException) { return values[0] + values[1] + 2; }
        catch (IndexOutOfRangeException) { return 99; }
        return 0;
    }

    private static void FinallyReplacementLeaf(int[] values)
    {
        try
        {
            try { values[2] = 7; }
            catch (IndexOutOfRangeException) { values[0] = 20; throw; }
        }
        finally
        {
            values[1] = 20;
            int[] missing = null!;
            missing[0] = 9;
        }
    }

    [Fact]
    public void CompiledFinallyReplacementReachesAncestorWithNewException()
        => CompiledConstantsExecutePastEntryDecodeAndRetainOriginalTerminalAndBackingRelease(
            nameof(ManagedFinallyReplacementReturn), ExternalExecutionTerminalKind.Completed);
    public static int ManagedCatchFinallyReturn()
    {
        int result = 0;
        try
        {
            var values = new int[2];
            values[2] = 7;
            result = 100;
        }
        catch (IndexOutOfRangeException)
        {
            result = 40;
        }
        finally
        {
            result = result + 2;
        }
        return result;
    }

    [Fact]
    public void StandaloneEhImageRejectsTamperedMetadataBeforeArtifact()
        => Assert.Empty(Compile(nameof(ManagedCatchFinallyReturn), expectSuccess: false, tamperMetadata: true));
    public static int ManagedArrayReturn() => AllocateArraySum();
    private static int AllocateArraySum()
    {
        var values = new int[2];
        values[0] = 40;
        values[1] = 2;
        return values[0] + values[1];
    }

    public static int ManagedArrayBoundsReturn() => AllocateArrayBounds();
    private static int AllocateArrayBounds()
    {
        var values = new int[2];
        values[2] = 42;
        return values[0];
    }

    [Fact]
    public void StandaloneArrayImageRejectsTamperedMetadataBeforeArtifact()
        => Assert.Empty(Compile(nameof(ManagedArrayReturn), expectSuccess: false, tamperMetadata: true));

    [Theory]
    [InlineData("missing")]
    [InlineData("throw")]
    [InlineData("foreign")]
    public void ActualTerminalSourceLossDeniesReceiptWithoutPreventingBackingClosure(string fault)
        => CheckTerminalSourceLoss(fault, false);

    [Theory]
    [InlineData("missing")]
    [InlineData("throw")]
    [InlineData("foreign")]
    [InlineData("copy")]
    [InlineData("reentry")]
    public void ActualInitializerSourceLossDeniesReceiptWithoutRepeatingExecution(string fault)
        => CheckTerminalSourceLoss(fault, true);

    [Fact]
    public void ActualMultiInitializerOwnerRejectsOutOfOrderSourceWithoutRepeatingExecution()
    {
        Assert.True(CpuBackedChildExecution.TryCreate(Compile(nameof(ManagedMultiStaticReturn)), 80000,
            out var execution, out var reason), reason);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(CpuBackedChildExecution).GetField("request", flags)!;
        var request = (HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionRequestV1)field.GetValue(execution)!;
        var entries = request.LoadResult.PendingInitializers!.OrderBy(row => row.Order).ToArray();
        Assert.Equal(2, entries.Length);
        bool changed = false;
        field.SetValue(execution, request with { ObservationAttach = binding =>
        {
            if (binding.SegmentKind == "Initializer" && !changed)
            {
                changed = true;
                return request.ObservationAttach!(binding with { EntryAddress = entries[1].EntryAddress });
            }
            return request.ObservationAttach!(binding);
        }});
        Assert.False(execution!.ExecuteToTerminal(out var result, out reason));
        Assert.True(changed);
        Assert.Equal(42, result!.ProcessExitCode);
        Assert.Contains("retire source", reason);
        Assert.False(execution.ExecuteToTerminal(out _, out _));
        Assert.True(execution.Close(out reason), reason);
    }

    private static void CheckTerminalSourceLoss(string fault, bool initializer)
    {
        Assert.True(CpuBackedChildExecution.TryCreate(Compile(initializer ? nameof(ManagedForeignStaticDirectReturn) : nameof(ManagedArrayBoundsReturn)), initializer ? 80000 : 10000,
            out var execution, out var reason), reason);
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(CpuBackedChildExecution).GetField("request", flags)!;
        var request = (HybridCPU_ISE.CloseToHSL.Core.Runtime.Managed.HybridCpuIseManagedGuestExecutionRequestV1)field.GetValue(execution)!;
        var gate = typeof(CpuBackedChildExecution).GetField("sync", flags)!.GetValue(execution)!;
        bool closing = false;
        field.SetValue(execution, request with { ObservationAttach = binding =>
        {
            if (initializer && binding.SegmentKind != "Initializer") return request.ObservationAttach!(binding);
            var read = binding.ReadOriginalRetire!;
            var captured = binding with { ReadOriginalRetire = () =>
            {
                Assert.False(System.Threading.Monitor.IsEntered(gate));
                var raw = read();
                if (!closing) return raw;
                return fault switch
                {
                    "missing" => null,
                    "throw" => throw new IOException("Terminal source lost."),
                    "copy" => raw,
                    "reentry" => Reenter(),
                    _ => raw! with { SegmentId = Guid.NewGuid() },
                };
                HybridCPU_ISE.NonRTL.Runtime.OwnedCoreRetireObservation? Reenter()
                {
                    Assert.False(execution!.ExecuteToTerminal(out _, out _));
                    throw new IOException("Initializer source reentered then lost its reply.");
                }
            }};
            var consumer = request.ObservationAttach!(captured);
            return new ClosingRetireFault(consumer, () =>
            {
                closing = true;
                if (fault == "copy")
                    typeof(CpuBackedChildExecution).GetMethod("CaptureRetire", flags)!.Invoke(execution,
                        [captured with { }, true]);
            });
        }});
        Assert.False(execution!.ExecuteToTerminal(out var result, out reason));
        Assert.Equal(initializer ? 42 : 255, result!.ProcessExitCode);
        Assert.Contains("retire source", reason);
        Assert.False(execution.ExecuteToTerminal(out _, out _));
        Assert.True(execution.Close(out reason), reason);
    }

    private sealed class ClosingRetireFault(HybridCPU_ISE.NonRTL.Runtime.IOwnedCoreObservationConsumer inner,
        Action beforeClose) : HybridCPU_ISE.NonRTL.Runtime.IOwnedCoreObservationConsumer
    {
        public void OnBoundary() => inner.OnBoundary();
        public void Dispose() { beforeClose(); inner.Dispose(); }
    }
}
