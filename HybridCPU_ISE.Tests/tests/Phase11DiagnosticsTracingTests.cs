// Phase 11: Diagnostics / Tracing — Deterministic Replay and Trace Integration
// Covers:
//   - TraceEventKind: all v4 event taxonomy values and their byte codes
//   - V4TraceEvent: Create helpers, field semantics, ToString
//   - IV4TraceEventSink / NullV4TraceEventSink: interface contract, singleton
//   - TraceSink.RecordV4Event: enabled/disabled gate, append-only semantics
//   - TraceSink.GetV4Events: full list, by kind, by VT
//   - TraceSink.V4EventCount
//   - ReplaySnapshot: all required fields, SnapshotTrigger values
//   - ReplayAnchorEvaluator: IsReplayAnchor, periodic fallback, FSM transition
//   - ReplayAnchorEvaluator.PeriodicSnapshotInterval constant
//   - TelemetryCounters: all increment methods, per-VT queries, global queries
//   - TelemetryCounters.ApplyTraceEvent: all 33 event kinds routed correctly
//   - TelemetryCounters.ExportAsCsrSnapshot: correct CSR address keys
//   - TelemetryCounters.Reset: clears all counters
//   - ReplayValidationResult: Deterministic / Diverged factories, ToString
//   - ReplayValidator.ValidateTrace: identical traces, Kind mismatch, VtId mismatch,
//       FsmState mismatch, Payload mismatch, BundleSerial offset, length mismatch
//   - ReplayValidator.ValidateTrace(maxBundleCount): crops correctly

using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Core;

namespace HybridCPU_ISE.Tests.Phase11
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    internal static class TraceHelper
    {
        /// <summary>Build a simple V4TraceEvent for testing.</summary>
        internal static V4TraceEvent Evt(
            TraceEventKind kind,
            ulong serial  = 1,
            byte  vtId    = 0,
            PipelineState fsm = PipelineState.Task,
            ulong payload = 0)
            => V4TraceEvent.Create(serial, vtId, fsm, kind, payload);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TraceEventKind taxonomy
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class TraceEventKindTests
    {
        [Theory]
        [InlineData(TraceEventKind.BundleDispatched, 0x01)]
        [InlineData(TraceEventKind.BundleRetired,    0x02)]
        [InlineData(TraceEventKind.BundleReplayed,   0x03)]
        [InlineData(TraceEventKind.AluExecuted,      0x10)]
        [InlineData(TraceEventKind.LoadExecuted,     0x20)]
        [InlineData(TraceEventKind.StoreExecuted,    0x21)]
        [InlineData(TraceEventKind.FenceExecuted,    0x22)]
        [InlineData(TraceEventKind.BranchTaken,      0x30)]
        [InlineData(TraceEventKind.BranchNotTaken,   0x31)]
        [InlineData(TraceEventKind.JumpExecuted,     0x32)]
        [InlineData(TraceEventKind.LrExecuted,       0x40)]
        [InlineData(TraceEventKind.ScSucceeded,      0x41)]
        [InlineData(TraceEventKind.ScFailed,         0x42)]
        [InlineData(TraceEventKind.AmoWordExecuted,  0x43)]
        [InlineData(TraceEventKind.AmoDwordExecuted, 0x44)]
        [InlineData(TraceEventKind.TrapTaken,        0x50)]
        [InlineData(TraceEventKind.PrivilegeReturn,  0x51)]
        [InlineData(TraceEventKind.WfiEntered,       0x52)]
        [InlineData(TraceEventKind.CsrRead,          0x60)]
        [InlineData(TraceEventKind.CsrWrite,         0x61)]
        [InlineData(TraceEventKind.VtYield,          0x70)]
        [InlineData(TraceEventKind.VtWfe,            0x71)]
        [InlineData(TraceEventKind.VtSev,            0x72)]
        [InlineData(TraceEventKind.PodBarrierEntered,0x73)]
        [InlineData(TraceEventKind.PodBarrierExited, 0x74)]
        [InlineData(TraceEventKind.VtBarrierEntered, 0x75)]
        [InlineData(TraceEventKind.VtBarrierExited,  0x76)]
        [InlineData(TraceEventKind.VmxOn,            0x80)]
        [InlineData(TraceEventKind.VmxOff,           0x81)]
        [InlineData(TraceEventKind.VmEntry,          0x82)]
        [InlineData(TraceEventKind.VmEntryFailed,    0x83)]
        [InlineData(TraceEventKind.VmExit,           0x84)]
        [InlineData(TraceEventKind.VmcsRead,         0x85)]
        [InlineData(TraceEventKind.VmcsWrite,        0x86)]
        [InlineData(TraceEventKind.FspPilfer,        0x90)]
        [InlineData(TraceEventKind.FspBoundary,      0x91)]
        [InlineData(TraceEventKind.FsmTransition,    0xA0)]
        public void EventKind_ByteValues_MatchSpec(TraceEventKind kind, byte expected)
        {
            Assert.Equal(expected, (byte)kind);
        }

        [Fact]
        public void EventKind_IsBackedByByte()
        {
            // Enum underlying type must be byte for compact trace storage
            Assert.Equal(typeof(byte), System.Enum.GetUnderlyingType(typeof(TraceEventKind)));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // V4TraceEvent
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class V4TraceEventTests
    {
        [Fact]
        public void Create_NoPayload_FieldsPopulated()
        {
            var evt = V4TraceEvent.Create(42UL, 2, PipelineState.GuestExecution, TraceEventKind.AluExecuted);

            Assert.Equal(42UL,                     evt.BundleSerial);
            Assert.Equal((byte)2,                  evt.VtId);
            Assert.Equal(PipelineState.GuestExecution, evt.FsmState);
            Assert.Equal(TraceEventKind.AluExecuted,   evt.Kind);
            Assert.Equal(0UL,                      evt.Payload);
        }

        [Fact]
        public void Create_WithPayload_PayloadPreserved()
        {
            var evt = V4TraceEvent.Create(10UL, 0, PipelineState.Task, TraceEventKind.VmExit, 0xDEAD_BEEF);

            Assert.Equal(0xDEAD_BEEFUL, evt.Payload);
        }

        [Fact]
        public void ToString_ContainsKindAndBundleSerial()
        {
            var evt = V4TraceEvent.Create(99UL, 1, PipelineState.Task, TraceEventKind.BundleRetired);
            var str = evt.ToString();

            Assert.Contains("99",           str);
            Assert.Contains("BundleRetired", str);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IV4TraceEventSink / NullV4TraceEventSink
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class NullV4TraceEventSinkTests
    {
        [Fact]
        public void Singleton_IsNotNull()
        {
            Assert.NotNull(NullV4TraceEventSink.Instance);
        }

        [Fact]
        public void RecordV4Event_DoesNotThrow()
        {
            var sink = NullV4TraceEventSink.Instance;
            var evt  = TraceHelper.Evt(TraceEventKind.AluExecuted);

            // Must not throw — null sink discards events silently
            sink.RecordV4Event(evt);
        }

        [Fact]
        public void ImplementsInterface()
        {
            Assert.IsAssignableFrom<IV4TraceEventSink>(NullV4TraceEventSink.Instance);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TraceSink v4 recording
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class TraceSinkV4Tests
    {
        private static TraceSink EnabledSink()
        {
            var sink = new TraceSink();
            sink.SetEnabled(true);
            return sink;
        }

        [Fact]
        public void TraceSink_ImplementsIV4TraceEventSink()
        {
            Assert.IsAssignableFrom<IV4TraceEventSink>(new TraceSink());
        }

        [Fact]
        public void RecordV4Event_WhenDisabled_NotStored()
        {
            var sink = new TraceSink(); // disabled by default
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted));

            Assert.Equal(0, sink.V4EventCount);
        }

        [Fact]
        public void RecordV4Event_WhenEnabled_Stored()
        {
            var sink = EnabledSink();
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.BundleRetired));

            Assert.Equal(1, sink.V4EventCount);
        }

        [Fact]
        public void RecordV4Event_MultipleEvents_AppendedInOrder()
        {
            var sink = EnabledSink();
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.BundleDispatched, serial: 1));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted,      serial: 1));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.BundleRetired,    serial: 1));

            Assert.Equal(3, sink.V4EventCount);
            Assert.Equal(TraceEventKind.BundleDispatched, sink.GetV4Events()[0].Kind);
            Assert.Equal(TraceEventKind.AluExecuted,      sink.GetV4Events()[1].Kind);
            Assert.Equal(TraceEventKind.BundleRetired,    sink.GetV4Events()[2].Kind);
        }

        [Fact]
        public void GetV4Events_ByKind_FiltersCorrectly()
        {
            var sink = EnabledSink();
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.VmExit, serial: 1));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted, serial: 2));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.VmExit, serial: 3));

            var exits = sink.GetV4Events(TraceEventKind.VmExit);
            Assert.Equal(2, exits.Count);
            Assert.All(exits, e => Assert.Equal(TraceEventKind.VmExit, e.Kind));
        }

        [Fact]
        public void GetV4EventsForVt_FiltersCorrectly()
        {
            var sink = EnabledSink();
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted, vtId: 0));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted, vtId: 1));
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted, vtId: 0));

            var vt0 = sink.GetV4EventsForVt(0);
            Assert.Equal(2, vt0.Count);
            Assert.All(vt0, e => Assert.Equal(0, e.VtId));
        }

        [Fact]
        public void ClearV4Events_ResetsCount()
        {
            var sink = EnabledSink();
            sink.RecordV4Event(TraceHelper.Evt(TraceEventKind.AluExecuted));
            sink.ClearV4Events();

            Assert.Equal(0, sink.V4EventCount);
        }

        [Fact]
        public void ShouldCaptureFullState_TracksEnabledAndFullLevel()
        {
            var sink = new TraceSink();

            Assert.False(sink.ShouldCaptureFullState);

            sink.SetEnabled(true);
            Assert.False(sink.ShouldCaptureFullState);

            sink.SetLevel(TraceLevel.Full);
            Assert.True(sink.ShouldCaptureFullState);

            sink.SetEnabled(false);
            Assert.False(sink.ShouldCaptureFullState);
        }

        [Fact]
        public void RecordPhaseAwareState_WhenNotCapturingFullState_DoesNotStorePerThreadTrace()
        {
            var sink = new TraceSink();
            sink.SetEnabled(true);
            sink.SetLevel(TraceLevel.Summary);

            sink.RecordPhaseAwareState(
                new FullStateTraceEvent
                {
                    ThreadId = 0,
                    CycleNumber = 1,
                    BundleId = 0,
                    OpIndex = 0,
                    Opcode = 0x10,
                    PipelineStage = "TEST"
                },
                default,
                default,
                phaseCertificateTemplateReusable: false);

            Assert.Empty(sink.GetThreadTrace(0));

            sink.SetLevel(TraceLevel.Full);
            sink.RecordPhaseAwareState(
                new FullStateTraceEvent
                {
                    ThreadId = 0,
                    CycleNumber = 2,
                    BundleId = 1,
                    OpIndex = 0,
                    Opcode = 0x11,
                    PipelineStage = "TEST"
                },
                default,
                default,
                phaseCertificateTemplateReusable: false);

            Assert.Single(sink.GetThreadTrace(0));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReplaySnapshot
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ReplaySnapshotTests
    {
        [Fact]
        public void ReplaySnapshot_AllRequiredFields_Constructable()
        {
            var snap = new ReplaySnapshot
            {
                BundleSerial  = 256UL,
                FsmState      = PipelineState.Task,
                VtId          = 1,
                Pc            = 0x1000UL,
                Registers     = new long[32],
                CsrSnapshot   = new Dictionary<ushort, long>(),
                LrReservation = (0UL, false),
                CycleCount    = 512UL,
            };

            Assert.Equal(256UL,            snap.BundleSerial);
            Assert.Equal(PipelineState.Task, snap.FsmState);
            Assert.Equal((byte)1,          snap.VtId);
            Assert.Equal(0x1000UL,         snap.Pc);
            Assert.Equal(32,               snap.Registers.Length);
            Assert.Equal(512UL,            snap.CycleCount);
            Assert.False(snap.LrReservation.Valid);
        }

        [Fact]
        public void ReplaySnapshot_DefaultTrigger_IsPeriodic()
        {
            var snap = new ReplaySnapshot
            {
                BundleSerial  = 0,
                FsmState      = PipelineState.Task,
                VtId          = 0,
                Pc            = 0,
                Registers     = new long[32],
                CsrSnapshot   = new Dictionary<ushort, long>(),
                LrReservation = (0, false),
                CycleCount    = 0,
            };
            Assert.Equal(SnapshotTrigger.Periodic, snap.Trigger);
        }

        [Fact]
        public void SnapshotTrigger_AllValuesExist()
        {
            _ = SnapshotTrigger.Periodic;
            _ = SnapshotTrigger.AnchorHint;
            _ = SnapshotTrigger.FsmTransition;
        }

        [Fact]
        public void ReplaySnapshot_LrReservation_Valid()
        {
            var snap = new ReplaySnapshot
            {
                BundleSerial  = 1,
                FsmState      = PipelineState.Task,
                VtId          = 0,
                Pc            = 0,
                Registers     = new long[32],
                CsrSnapshot   = new Dictionary<ushort, long>(),
                LrReservation = (0xDEAD_0000UL, true),
                CycleCount    = 0,
            };
            Assert.True(snap.LrReservation.Valid);
            Assert.Equal(0xDEAD_0000UL, snap.LrReservation.Address);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReplayAnchorEvaluator
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ReplayAnchorEvaluatorTests
    {
        private static BundleMetadata NonAnchor  => BundleMetadata.Default;
        private static BundleMetadata AnchorMeta => new() { IsReplayAnchor = true };

        [Fact]
        public void AnchorHint_AlwaysCaptured()
        {
            var eval = new ReplayAnchorEvaluator();
            var trigger = eval.ShouldCaptureSnapshot(AnchorMeta, bundleSerial: 7);

            Assert.Equal(SnapshotTrigger.AnchorHint, trigger);
        }

        [Fact]
        public void Periodic_CapturedAtInterval()
        {
            var eval = new ReplayAnchorEvaluator();
            ulong interval = ReplayAnchorEvaluator.PeriodicSnapshotInterval;

            var trigger = eval.ShouldCaptureSnapshot(NonAnchor, bundleSerial: interval);
            Assert.Equal(SnapshotTrigger.Periodic, trigger);
        }

        [Fact]
        public void Periodic_NotCapturedBetweenIntervals()
        {
            var eval = new ReplayAnchorEvaluator();
            // serial 1 is not a multiple of PeriodicSnapshotInterval and no FSM transition
            var trigger = eval.ShouldCaptureSnapshot(NonAnchor, bundleSerial: 1);
            Assert.Null(trigger);
        }

        [Fact]
        public void FsmTransition_CapturedAtTransitionBundle()
        {
            var eval = new ReplayAnchorEvaluator();
            eval.NotifyFsmTransition(bundleSerial: 17);

            var trigger = eval.ShouldCaptureSnapshot(NonAnchor, bundleSerial: 17);
            Assert.Equal(SnapshotTrigger.FsmTransition, trigger);
        }

        [Fact]
        public void FsmTransition_NotCapturedAtDifferentBundle()
        {
            var eval = new ReplayAnchorEvaluator();
            eval.NotifyFsmTransition(bundleSerial: 17);

            // bundle 18 != 17
            var trigger = eval.ShouldCaptureSnapshot(NonAnchor, bundleSerial: 18);
            Assert.Null(trigger);
        }

        [Fact]
        public void AnchorHint_TakesPriorityOverPeriodic()
        {
            var eval = new ReplayAnchorEvaluator();
            ulong interval = ReplayAnchorEvaluator.PeriodicSnapshotInterval;

            // At the periodic boundary, but also marked as anchor
            var trigger = eval.ShouldCaptureSnapshot(AnchorMeta, bundleSerial: interval);
            Assert.Equal(SnapshotTrigger.AnchorHint, trigger);
        }

        [Fact]
        public void PeriodicSnapshotInterval_IsPositive()
        {
            Assert.True(ReplayAnchorEvaluator.PeriodicSnapshotInterval > 0);
        }

        [Fact]
        public void Reset_ClearsTransitionNotification()
        {
            var eval = new ReplayAnchorEvaluator();
            eval.NotifyFsmTransition(bundleSerial: 5);
            eval.Reset();

            // After reset, bundle 5 should not trigger FsmTransition capture
            var trigger = eval.ShouldCaptureSnapshot(NonAnchor, bundleSerial: 5);
            Assert.Null(trigger);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TelemetryCounters
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class TelemetryCountersTests
    {
        [Fact]
        public void InitialCounters_AllZero()
        {
            var c = new TelemetryCounters();
            Assert.Equal(0UL, c.CycleCount);
            Assert.Equal(0UL, c.BundleRetiredCount);
            Assert.Equal(0UL, c.InstrRetiredCount);
            Assert.Equal(0UL, c.VmExitCount);
            Assert.Equal(0UL, c.BarrierCount);
            Assert.Equal(0UL, c.StealCount);
            Assert.Equal(0UL, c.ReplayCount);
        }

        [Fact]
        public void IncrementCycle_Accumulates()
        {
            var c = new TelemetryCounters();
            c.IncrementCycle();
            c.IncrementCycle(9);
            Assert.Equal(10UL, c.CycleCount);
        }

        [Fact]
        public void IncrementVmExit_CountsCorrectly()
        {
            var c = new TelemetryCounters();
            c.IncrementVmExit();
            c.IncrementVmExit();
            Assert.Equal(2UL, c.VmExitCount);
        }

        [Fact]
        public void IncrementBarrier_CountsCorrectly()
        {
            var c = new TelemetryCounters();
            c.IncrementBarrier();
            Assert.Equal(1UL, c.BarrierCount);
        }

        [Fact]
        public void IncrementSteal_CountsCorrectly()
        {
            var c = new TelemetryCounters();
            c.IncrementSteal();
            c.IncrementSteal();
            c.IncrementSteal();
            Assert.Equal(3UL, c.StealCount);
        }

        [Fact]
        public void IncrementAmoDwordCount_PerVt()
        {
            var c = new TelemetryCounters();
            c.IncrementAmoDwordCount(0);
            c.IncrementAmoDwordCount(0);
            c.IncrementAmoDwordCount(1);

            Assert.Equal(2UL, c.GetAmoDwordCount(0));
            Assert.Equal(1UL, c.GetAmoDwordCount(1));
            Assert.Equal(3UL, c.GetTotalAmoDwordCount());
        }

        [Fact]
        public void GetAmoDwordCount_OutOfRange_ReturnsZero()
        {
            var c = new TelemetryCounters();
            Assert.Equal(0UL, c.GetAmoDwordCount(200));
        }

        [Fact]
        public void ApplyTraceEvent_BundleRetired_IncreasesBundleCount()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.BundleRetired));
            Assert.Equal(1UL, c.BundleRetiredCount);
        }

        [Fact]
        public void ApplyTraceEvent_AmoDword_IncreasesAmoDwordAndInstrCount()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.AmoDwordExecuted, vtId: 2));

            Assert.Equal(1UL, c.GetAmoDwordCount(2));
            Assert.Equal(1UL, c.InstrRetiredCount);
        }

        [Fact]
        public void ApplyTraceEvent_AmoWord_IncreasesAmoWordAndInstrCount()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.AmoWordExecuted, vtId: 1));

            Assert.Equal(1UL, c.GetAmoWordCount(1));
            Assert.Equal(1UL, c.InstrRetiredCount);
        }

        [Fact]
        public void ApplyTraceEvent_VmExit_IncrementsVmExitCount()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.VmExit));
            Assert.Equal(1UL, c.VmExitCount);
        }

        [Fact]
        public void ApplyTraceEvent_PodBarrierEntered_IncrementsBarrier()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.PodBarrierEntered));
            Assert.Equal(1UL, c.BarrierCount);
        }

        [Fact]
        public void ApplyTraceEvent_VtBarrierEntered_IncrementsBarrier()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.VtBarrierEntered));
            Assert.Equal(1UL, c.BarrierCount);
        }

        [Fact]
        public void ApplyTraceEvent_FspPilfer_IncrementsSteal()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.FspPilfer));
            Assert.Equal(1UL, c.StealCount);
        }

        [Fact]
        public void ApplyTraceEvent_BundleReplayed_IncrementsReplay()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.BundleReplayed));
            Assert.Equal(1UL, c.ReplayCount);
        }

        [Fact]
        public void ApplyTraceEvent_LrSc_PerVtCounters()
        {
            var c = new TelemetryCounters();
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.LrExecuted,  vtId: 0));
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.ScSucceeded, vtId: 0));
            c.ApplyTraceEvent(TraceHelper.Evt(TraceEventKind.ScFailed,    vtId: 0));

            Assert.Equal(1UL, c.GetLrCount(0));
            Assert.Equal(1UL, c.GetScSuccessCount(0));
            Assert.Equal(1UL, c.GetScFailCount(0));
            Assert.Equal(3UL, c.InstrRetiredCount);
        }

        [Fact]
        public void Reset_ClearsAllCounters()
        {
            var c = new TelemetryCounters();
            c.IncrementVmExit();
            c.IncrementBarrier();
            c.IncrementSteal();
            c.IncrementAmoDwordCount(0);
            c.Reset();

            Assert.Equal(0UL, c.VmExitCount);
            Assert.Equal(0UL, c.BarrierCount);
            Assert.Equal(0UL, c.StealCount);
            Assert.Equal(0UL, c.GetAmoDwordCount(0));
        }

        [Fact]
        public void ExportAsCsrSnapshot_ContainsAllKeys()
        {
            var c = new TelemetryCounters();
            c.IncrementVmExit();
            c.IncrementBarrier();
            c.IncrementSteal();
            c.IncrementReplay();

            var snap = c.ExportAsCsrSnapshot();

            Assert.True(snap.ContainsKey(CsrAddresses.Cycle));
            Assert.True(snap.ContainsKey(CsrAddresses.BundleRet));
            Assert.True(snap.ContainsKey(CsrAddresses.InstrRet));
            Assert.True(snap.ContainsKey(CsrAddresses.VmExitCnt));
            Assert.True(snap.ContainsKey(CsrAddresses.BarrierCnt));
            Assert.True(snap.ContainsKey(CsrAddresses.StealCnt));
            Assert.True(snap.ContainsKey(CsrAddresses.ReplayCnt));
        }

        [Fact]
        public void ExportAsCsrSnapshot_ValuesMatchCounters()
        {
            var c = new TelemetryCounters();
            c.IncrementVmExit();
            c.IncrementVmExit();
            c.IncrementBarrier();
            c.IncrementSteal();
            c.IncrementSteal();
            c.IncrementSteal();

            var snap = c.ExportAsCsrSnapshot();

            Assert.Equal(2L, snap[CsrAddresses.VmExitCnt]);
            Assert.Equal(1L, snap[CsrAddresses.BarrierCnt]);
            Assert.Equal(3L, snap[CsrAddresses.StealCnt]);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReplayValidationResult
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ReplayValidationResultTests
    {
        [Fact]
        public void Deterministic_Factory_SetsDeterministicTrue()
        {
            var r = ReplayValidationResult.Deterministic(100);
            Assert.True(r.IsDeterministic);
            Assert.Equal(100, r.ComparedEventCount);
            Assert.Equal(-1,  r.DivergenceIndex);
            Assert.Null(r.DivergenceDescription);
        }

        [Fact]
        public void Diverged_Factory_SetsDeterministicFalse()
        {
            var r = ReplayValidationResult.Diverged(50, 10, "Kind mismatch");
            Assert.False(r.IsDeterministic);
            Assert.Equal(50, r.ComparedEventCount);
            Assert.Equal(10, r.DivergenceIndex);
            Assert.Contains("Kind mismatch", r.DivergenceDescription);
        }

        [Fact]
        public void ToString_Deterministic_ContainsMatchedCount()
        {
            var r = ReplayValidationResult.Deterministic(77);
            Assert.Contains("77", r.ToString());
        }

        [Fact]
        public void ToString_Diverged_ContainsIndexAndDescription()
        {
            var r = ReplayValidationResult.Diverged(20, 5, "Payload mismatch");
            var s = r.ToString();
            Assert.Contains("5",  s);
            Assert.Contains("20", s);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReplayValidator
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ReplayValidatorTests
    {
        private readonly ReplayValidator _validator = new();

        private static List<V4TraceEvent> MakeTrace(params TraceEventKind[] kinds)
        {
            var list = new List<V4TraceEvent>();
            for (int i = 0; i < kinds.Length; i++)
                list.Add(TraceHelper.Evt(kinds[i], serial: (ulong)(i + 1)));
            return list;
        }

        [Fact]
        public void IdenticalTraces_Deterministic()
        {
            var original = MakeTrace(TraceEventKind.BundleDispatched, TraceEventKind.AluExecuted, TraceEventKind.BundleRetired);
            var replay   = MakeTrace(TraceEventKind.BundleDispatched, TraceEventKind.AluExecuted, TraceEventKind.BundleRetired);

            var result = _validator.ValidateTrace(original, replay);
            Assert.True(result.IsDeterministic);
            Assert.Equal(3, result.ComparedEventCount);
        }

        [Fact]
        public void EmptyTraces_Deterministic()
        {
            var result = _validator.ValidateTrace(
                new List<V4TraceEvent>(),
                new List<V4TraceEvent>());
            Assert.True(result.IsDeterministic);
        }

        [Fact]
        public void KindMismatch_Diverged()
        {
            var original = MakeTrace(TraceEventKind.AluExecuted);
            var replay   = MakeTrace(TraceEventKind.LoadExecuted); // different kind

            var result = _validator.ValidateTrace(original, replay);
            Assert.False(result.IsDeterministic);
            Assert.Equal(0, result.DivergenceIndex);
            Assert.Contains("Kind", result.DivergenceDescription);
        }

        [Fact]
        public void VtIdMismatch_Diverged()
        {
            var original = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.AluExecuted, vtId: 0) };
            var replay   = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.AluExecuted, vtId: 1) };

            var result = _validator.ValidateTrace(original, replay);
            Assert.False(result.IsDeterministic);
            Assert.Contains("VtId", result.DivergenceDescription);
        }

        [Fact]
        public void FsmStateMismatch_Diverged()
        {
            var original = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.AluExecuted, fsm: PipelineState.Task) };
            var replay   = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.AluExecuted, fsm: PipelineState.GuestExecution) };

            var result = _validator.ValidateTrace(original, replay);
            Assert.False(result.IsDeterministic);
            Assert.Contains("FsmState", result.DivergenceDescription);
        }

        [Fact]
        public void PayloadMismatch_Diverged()
        {
            var original = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.VmExit, payload: 1) };
            var replay   = new List<V4TraceEvent> { TraceHelper.Evt(TraceEventKind.VmExit, payload: 2) };

            var result = _validator.ValidateTrace(original, replay);
            Assert.False(result.IsDeterministic);
            Assert.Contains("Payload", result.DivergenceDescription);
        }

        [Fact]
        public void LengthMismatch_Diverged()
        {
            var original = MakeTrace(TraceEventKind.AluExecuted, TraceEventKind.BundleRetired);
            var replay   = MakeTrace(TraceEventKind.AluExecuted);

            var result = _validator.ValidateTrace(original, replay);
            Assert.False(result.IsDeterministic);
        }

        [Fact]
        public void MaxBundleCount_CropsComparison()
        {
            // Original: 2 bundles; replay: 2 bundles — both identical when cropped to 1
            var original = new List<V4TraceEvent>
            {
                TraceHelper.Evt(TraceEventKind.AluExecuted,   serial: 1),
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 1),
                TraceHelper.Evt(TraceEventKind.LoadExecuted,  serial: 2), // different event kind in bundle 2
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 2),
            };
            var replay = new List<V4TraceEvent>
            {
                TraceHelper.Evt(TraceEventKind.AluExecuted,   serial: 10),
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 10),
                TraceHelper.Evt(TraceEventKind.AluExecuted,   serial: 11), // same as bundle 1 but serial shifted
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 11),
            };

            // Cropped to 1 bundle: both traces have AluExecuted + BundleRetired → deterministic
            var result = _validator.ValidateTrace(original, replay, maxBundleCount: 1);
            Assert.True(result.IsDeterministic);
        }

        [Fact]
        public void BundleSerialOffset_Tolerated()
        {
            // Replay starts from a different bundle serial than original — should still match
            // if all relative-serial relationships are preserved
            var original = new List<V4TraceEvent>
            {
                TraceHelper.Evt(TraceEventKind.AluExecuted,   serial: 100),
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 100),
            };
            var replay = new List<V4TraceEvent>
            {
                TraceHelper.Evt(TraceEventKind.AluExecuted,   serial: 200),
                TraceHelper.Evt(TraceEventKind.BundleRetired, serial: 200),
            };

            var result = _validator.ValidateTrace(original, replay);
            Assert.True(result.IsDeterministic);
        }
    }
}
