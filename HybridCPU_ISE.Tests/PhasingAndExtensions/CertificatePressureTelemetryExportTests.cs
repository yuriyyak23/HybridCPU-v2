using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Diagnostics;

namespace HybridCPU_ISE.Tests;

/// <summary>
/// Tests certificate-pressure telemetry export and backward-compatible profile serialization.
/// </summary>
public class CertificatePressureTelemetryExportTests
{
    [Fact]
    public void BuildProfile_WhenCertificateCountersExist_ThenExportsCertificatePressurePayload()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics
        {
            CertificateRejectByAluClass = 3,
            CertificateRejectByLsuClass = 1,
            RejectionsVT0 = 4,
            RejectionsVT2 = 3,
            RegGroupConflictsVT2 = 2,
            RegGroupConflictsVT3 = 1,
            CertificateRegGroupConflictVT2 = 2,
            CertificateRegGroupConflictVT3 = 1
        });

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        Assert.NotNull(profile.CertificatePressure);
        Assert.Equal(3, profile.TotalRejectsPerClass[SlotClass.AluClass]);
        Assert.Equal(1, profile.TotalRejectsPerClass[SlotClass.LsuClass]);
        Assert.Equal(3, profile.CertificatePressure!.RejectsPerClass![SlotClass.AluClass]);
        Assert.Equal(1, profile.CertificatePressure.RejectsPerClass[SlotClass.LsuClass]);
        Assert.Equal(4, profile.PerVtRejectionCounts![0]);
        Assert.Equal(3, profile.PerVtRejectionCounts[2]);
        Assert.Equal(2, profile.PerVtRegGroupConflicts![2]);
        Assert.Equal(1, profile.PerVtRegGroupConflicts[3]);
        Assert.Equal(2, profile.CertificatePressure.RegisterGroupConflictsPerVt![2]);
        Assert.Equal(1, profile.CertificatePressure.RegisterGroupConflictsPerVt[3]);
    }

    [Fact]
    public void BuildProfile_WhenCertificateCountersAbsent_ThenLeavesCertificatePressureNull()
    {
        var scheduler = new MicroOpScheduler();

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        Assert.Null(profile.CertificatePressure);
        Assert.Null(profile.PerVtRejectionCounts);
        Assert.Null(profile.PerVtRegGroupConflicts);
        Assert.Equal(0, profile.TotalRejectsPerClass[SlotClass.AluClass]);
        Assert.Equal(0, profile.TotalRejectsPerClass[SlotClass.SystemSingleton]);
    }

    [Fact]
    public void BuildProfile_WhenOnlyGuardPlaneLegalityCountersExist_ThenKeepsCertificatePressureNull()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics
        {
            SmtLegalityRejectByAluClass = 2,
            SmtOwnerContextGuardRejects = 2
        });

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        Assert.Null(profile.CertificatePressure);
        Assert.NotNull(profile.SmtLegalityRejectsPerClass);
        Assert.Equal(2, profile.TotalRejectsPerClass[SlotClass.AluClass]);
        Assert.Equal(2, profile.SmtLegalityRejectsPerClass![SlotClass.AluClass]);
    }

    [Fact]
    public void BuildProfile_WhenBankTelemetryExists_ThenExportsBankPayload()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics
        {
            BankPendingRejectBank2 = 3,
            BankPendingRejectBank9 = 1,
            MemoryClusteringEvents = 4,
            DomainIsolationCrossDomainBlocks = 6
        });

        var pipelineControl = new YAKSys_Hybrid_CPU.Processor.CPU_Core.PipelineControl
        {
            BankConflictStallCycles = 5,
            HazardRegisterDataCount = 7,
            HazardMemoryBankCount = 4,
            HazardControlFlowCount = 3,
            HazardSystemBarrierCount = 2,
            HazardPinnedLaneCount = 1
        };

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash", pipelineControl);

        Assert.NotNull(profile.BankPendingRejectsPerBank);
        Assert.Equal(3, profile.BankPendingRejectsPerBank![2]);
        Assert.Equal(1, profile.BankPendingRejectsPerBank[9]);
        Assert.Equal(4, profile.MemoryClusteringEventCount);
        Assert.Equal(5, profile.BankConflictStallCycles);
        Assert.Equal(7, profile.HazardRegisterDataCount);
        Assert.Equal(4, profile.HazardMemoryBankCount);
        Assert.Equal(3, profile.HazardControlFlowCount);
        Assert.Equal(2, profile.HazardSystemBarrierCount);
        Assert.Equal(1, profile.HazardPinnedLaneCount);
        Assert.Equal(6, profile.CrossDomainRejectCount);
    }

    [Fact]
    public void BuildProfile_WhenSiblingBankTelemetryExistsButCrossDomainCountIsZero_ThenLeavesCrossDomainRejectCountNull()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics
        {
            BankPendingRejectBank2 = 3,
            MemoryClusteringEvents = 4
        });

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");
        string json = TelemetryExporter.SerializeToJson(profile);

        Assert.NotNull(profile.BankPendingRejectsPerBank);
        Assert.Equal(3, profile.BankPendingRejectsPerBank![2]);
        Assert.Equal(4, profile.MemoryClusteringEventCount);
        Assert.Null(profile.CrossDomainRejectCount);
        Assert.DoesNotContain("\"crossDomainRejectCount\"", json);
    }

    [Fact]
    public void BuildProfile_WhenEligibilityTelemetryExists_ThenExportsEligibilityPayload()
    {
        var scheduler = new MicroOpScheduler();
        scheduler.TestSetPhaseMetrics(new SchedulerPhaseMetrics
        {
            EligibilityMaskedCycles = 2,
            EligibilityMaskedReadyCandidates = 3,
            LastEligibilityRequestedMask = 0x0F,
            LastEligibilityNormalizedMask = 0x05,
            LastEligibilityReadyPortMask = 0x07,
            LastEligibilityVisibleReadyMask = 0x05,
            LastEligibilityMaskedReadyMask = 0x02
        });

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        Assert.Equal(2, profile.EligibilityMaskedCycles);
        Assert.Equal(3, profile.EligibilityMaskedReadyCandidates);
        Assert.Equal((byte)0x0F, profile.LastEligibilityRequestedMask);
        Assert.Equal((byte)0x05, profile.LastEligibilityNormalizedMask);
        Assert.Equal((byte)0x07, profile.LastEligibilityReadyPortMask);
        Assert.Equal((byte)0x05, profile.LastEligibilityVisibleReadyMask);
        Assert.Equal((byte)0x02, profile.LastEligibilityMaskedReadyMask);
    }

    [Fact]
    public void BuildProfile_WhenAssistRejectCountersExist_ThenExportsAssistRejectReasons()
    {
        var scheduler = new MicroOpScheduler();

        scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistQuotaReject);
        scheduler.TestRecordTypedSlotReject(TypedSlotRejectReason.AssistBackpressureReject);

        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        Assert.Equal(1, profile.RejectsByReason[TypedSlotRejectReason.AssistQuotaReject]);
        Assert.Equal(1, profile.RejectsByReason[TypedSlotRejectReason.AssistBackpressureReject]);
    }

    [Fact]
    public void SerializeToJson_WhenCertificatePressureAbsent_ThenOmitsCertificatePressureProperty()
    {
        var scheduler = new MicroOpScheduler();
        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        string json = TelemetryExporter.SerializeToJson(profile);

        Assert.DoesNotContain("\"certificatePressure\"", json);
        Assert.DoesNotContain("\"perVtRejectionCounts\"", json);
        Assert.DoesNotContain("\"perVtRegGroupConflicts\"", json);
    }

    [Fact]
    public void SerializeToJson_WhenBankTelemetryAbsent_ThenOmitsBankTelemetryProperties()
    {
        var scheduler = new MicroOpScheduler();
        TypedSlotTelemetryProfile profile = TelemetryExporter.BuildProfile(scheduler, "program-hash");

        string json = TelemetryExporter.SerializeToJson(profile);

        Assert.DoesNotContain("\"bankPendingRejectsPerBank\"", json);
        Assert.DoesNotContain("\"memoryClusteringEventCount\"", json);
        Assert.DoesNotContain("\"bankConflictStallCycles\"", json);
        Assert.DoesNotContain("\"hazardRegisterDataCount\"", json);
        Assert.DoesNotContain("\"hazardMemoryBankCount\"", json);
        Assert.DoesNotContain("\"hazardControlFlowCount\"", json);
        Assert.DoesNotContain("\"hazardSystemBarrierCount\"", json);
        Assert.DoesNotContain("\"hazardPinnedLaneCount\"", json);
        Assert.DoesNotContain("\"crossDomainRejectCount\"", json);
        Assert.DoesNotContain("\"eligibilityMaskedCycles\"", json);
        Assert.DoesNotContain("\"eligibilityMaskedReadyCandidates\"", json);
        Assert.DoesNotContain("\"lastEligibilityRequestedMask\"", json);
        Assert.DoesNotContain("\"lastEligibilityNormalizedMask\"", json);
        Assert.DoesNotContain("\"lastEligibilityReadyPortMask\"", json);
        Assert.DoesNotContain("\"lastEligibilityVisibleReadyMask\"", json);
        Assert.DoesNotContain("\"lastEligibilityMaskedReadyMask\"", json);
    }

    [Fact]
    public void DeserializeFromJson_WhenLegacyProfileJson_ThenLeavesCertificatePressureNull()
    {
        const string legacyJson = """
            {
              "programHash": "legacy-hash",
              "totalInjectionsPerClass": {},
              "totalRejectsPerClass": {},
              "rejectsByReason": {},
              "averageNopDensity": 0,
              "averageBundleUtilization": 1,
              "totalBundlesExecuted": 0,
              "totalNopsExecuted": 0,
              "replayTemplateHits": 0,
              "replayTemplateMisses": 0,
              "replayHitRate": 0,
              "fairnessStarvationEvents": 0,
              "perVtInjectionCounts": {},
              "workerMetrics": null
            }
            """;

        TypedSlotTelemetryProfile? profile = TelemetryExporter.DeserializeFromJson(legacyJson);

        Assert.NotNull(profile);
        Assert.Null(profile!.CertificatePressure);
        Assert.Null(profile.PerVtRejectionCounts);
        Assert.Null(profile.PerVtRegGroupConflicts);
        Assert.Null(profile.BankPendingRejectsPerBank);
        Assert.Null(profile.MemoryClusteringEventCount);
        Assert.Null(profile.BankConflictStallCycles);
        Assert.Null(profile.HazardRegisterDataCount);
        Assert.Null(profile.HazardMemoryBankCount);
        Assert.Null(profile.HazardControlFlowCount);
        Assert.Null(profile.HazardSystemBarrierCount);
        Assert.Null(profile.HazardPinnedLaneCount);
        Assert.Null(profile.CrossDomainRejectCount);
        Assert.Null(profile.EligibilityMaskedCycles);
        Assert.Null(profile.EligibilityMaskedReadyCandidates);
        Assert.Null(profile.LastEligibilityRequestedMask);
        Assert.Null(profile.LastEligibilityNormalizedMask);
        Assert.Null(profile.LastEligibilityReadyPortMask);
        Assert.Null(profile.LastEligibilityVisibleReadyMask);
        Assert.Null(profile.LastEligibilityMaskedReadyMask);
    }

    [Fact]
    public void SerializeToJson_WhenCertificatePressurePresent_ThenRoundTripsCertificatePayload()
    {
        var profile = new TypedSlotTelemetryProfile(
            ProgramHash: "program-hash",
            TotalInjectionsPerClass: new Dictionary<SlotClass, long>(),
            TotalRejectsPerClass: new Dictionary<SlotClass, long>(),
            RejectsByReason: new Dictionary<TypedSlotRejectReason, long>(),
            AverageNopDensity: 0,
            AverageBundleUtilization: 1,
            TotalBundlesExecuted: 0,
            TotalNopsExecuted: 0,
            ReplayTemplateHits: 0,
            ReplayTemplateMisses: 0,
            ReplayHitRate: 0,
            FairnessStarvationEvents: 0,
            PerVtInjectionCounts: new Dictionary<int, long>(),
            WorkerMetrics: null)
        {
            CertificatePressure = new CertificatePressureMetrics(
                new Dictionary<SlotClass, long>
                {
                    [SlotClass.BranchControl] = 5
                },
                new Dictionary<int, long>
                {
                    [1] = 7
                }),
            PerVtRejectionCounts = new Dictionary<int, long>
            {
                [1] = 2,
                [3] = 4
            },
            PerVtRegGroupConflicts = new Dictionary<int, long>
            {
                [1] = 7
            }
        };

        string json = TelemetryExporter.SerializeToJson(profile);
        TypedSlotTelemetryProfile? roundTripped = TelemetryExporter.DeserializeFromJson(json);

        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.CertificatePressure);
        Assert.Equal(5, roundTripped.CertificatePressure!.RejectsPerClass![SlotClass.BranchControl]);
        Assert.Equal(7, roundTripped.CertificatePressure.RegisterGroupConflictsPerVt![1]);
        Assert.Equal(2, roundTripped.PerVtRejectionCounts![1]);
        Assert.Equal(4, roundTripped.PerVtRejectionCounts[3]);
        Assert.Equal(7, roundTripped.PerVtRegGroupConflicts![1]);
    }

    [Fact]
    public void SerializeToJson_WhenBankTelemetryPresent_ThenRoundTripsBankPayload()
    {
        var profile = new TypedSlotTelemetryProfile(
            ProgramHash: "program-hash",
            TotalInjectionsPerClass: new Dictionary<SlotClass, long>(),
            TotalRejectsPerClass: new Dictionary<SlotClass, long>(),
            RejectsByReason: new Dictionary<TypedSlotRejectReason, long>(),
            AverageNopDensity: 0,
            AverageBundleUtilization: 1,
            TotalBundlesExecuted: 12,
            TotalNopsExecuted: 0,
            ReplayTemplateHits: 0,
            ReplayTemplateMisses: 0,
            ReplayHitRate: 0,
            FairnessStarvationEvents: 0,
            PerVtInjectionCounts: new Dictionary<int, long>(),
            WorkerMetrics: null)
        {
            BankPendingRejectsPerBank = new Dictionary<int, long>
            {
                [4] = 6,
                [7] = 2
            },
            MemoryClusteringEventCount = 3,
            BankConflictStallCycles = 9,
            HazardRegisterDataCount = 8,
            HazardMemoryBankCount = 5,
            HazardControlFlowCount = 4,
            HazardSystemBarrierCount = 2,
            HazardPinnedLaneCount = 1,
            CrossDomainRejectCount = 11
        };

        string json = TelemetryExporter.SerializeToJson(profile);
        TypedSlotTelemetryProfile? roundTripped = TelemetryExporter.DeserializeFromJson(json);

        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.BankPendingRejectsPerBank);
        Assert.Equal(6, roundTripped.BankPendingRejectsPerBank![4]);
        Assert.Equal(3, roundTripped.MemoryClusteringEventCount);
        Assert.Equal(9, roundTripped.BankConflictStallCycles);
        Assert.Equal(8, roundTripped.HazardRegisterDataCount);
        Assert.Equal(5, roundTripped.HazardMemoryBankCount);
        Assert.Equal(4, roundTripped.HazardControlFlowCount);
        Assert.Equal(2, roundTripped.HazardSystemBarrierCount);
        Assert.Equal(1, roundTripped.HazardPinnedLaneCount);
        Assert.Equal(11, roundTripped.CrossDomainRejectCount);
    }

    [Fact]
    public void SerializeToJson_WhenEligibilityTelemetryPresent_ThenRoundTripsEligibilityPayload()
    {
        var profile = new TypedSlotTelemetryProfile(
            ProgramHash: "program-hash",
            TotalInjectionsPerClass: new Dictionary<SlotClass, long>(),
            TotalRejectsPerClass: new Dictionary<SlotClass, long>(),
            RejectsByReason: new Dictionary<TypedSlotRejectReason, long>(),
            AverageNopDensity: 0,
            AverageBundleUtilization: 1,
            TotalBundlesExecuted: 12,
            TotalNopsExecuted: 0,
            ReplayTemplateHits: 0,
            ReplayTemplateMisses: 0,
            ReplayHitRate: 0,
            FairnessStarvationEvents: 0,
            PerVtInjectionCounts: new Dictionary<int, long>(),
            WorkerMetrics: null)
        {
            EligibilityMaskedCycles = 4,
            EligibilityMaskedReadyCandidates = 6,
            LastEligibilityRequestedMask = 0x0F,
            LastEligibilityNormalizedMask = 0x05,
            LastEligibilityReadyPortMask = 0x07,
            LastEligibilityVisibleReadyMask = 0x05,
            LastEligibilityMaskedReadyMask = 0x02
        };

        string json = TelemetryExporter.SerializeToJson(profile);
        TypedSlotTelemetryProfile? roundTripped = TelemetryExporter.DeserializeFromJson(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(4, roundTripped!.EligibilityMaskedCycles);
        Assert.Equal(6, roundTripped.EligibilityMaskedReadyCandidates);
        Assert.Equal((byte)0x0F, roundTripped.LastEligibilityRequestedMask);
        Assert.Equal((byte)0x05, roundTripped.LastEligibilityNormalizedMask);
        Assert.Equal((byte)0x07, roundTripped.LastEligibilityReadyPortMask);
        Assert.Equal((byte)0x05, roundTripped.LastEligibilityVisibleReadyMask);
        Assert.Equal((byte)0x02, roundTripped.LastEligibilityMaskedReadyMask);
    }
}
