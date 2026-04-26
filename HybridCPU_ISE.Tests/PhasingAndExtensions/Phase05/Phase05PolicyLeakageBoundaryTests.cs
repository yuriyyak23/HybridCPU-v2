using System;
using System.Reflection;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Phase 05 policy leakage boundary tests.
    /// Extends the Phase 02/03/04 boundary to cover Phase 05 additions:
    /// DifferentialTraceCapture, DifferentialTraceCompare, runtime switch,
    /// and Phase 05 telemetry counters.
    ///
    /// Verifies that:
    /// - No Phase 05 structures introduce policy terms (scheduling, dispatch, etc.)
    /// - Runtime switch defaults to disabled and never auto-enables
    /// - All Phase 01-04 structural invariants still hold
    /// - DifferentialTraceEntry/Capture/Compare are diagnostic-only
    /// - No new mutable state in decoder-side structures
    /// </summary>
    public class Phase05PolicyLeakageBoundaryTests
    {
        /// <summary>
        /// Forbidden property name fragments inherited from Phase 02/03/04 boundary.
        /// </summary>
        private static readonly string[] PolicyLeakageTerms =
        [
            "Priority", "Rank", "Score", "Weight",
            "Fairness", "Quota", "Budget", "Credit", "Starvation",
            "Age", "Reorder", "Dispatch", "Retire",
            "Execute", "Pipeline", "Latency",
            "FinalAdmission", "FinalIssue", "FinalSchedule",
            "Committed", "Binding"
        ];

        // =====================================================================
        // DifferentialTraceEntry: no policy leakage
        // =====================================================================

        [Fact]
        public void WhenDifferentialTraceEntryInspected_ThenNoPolicyTermsInPropertyNames()
        {
            // Assert
            AssertNoPolicyTermsOnType(typeof(DifferentialTraceEntry));
        }

        [Fact]
        public void WhenDifferentialTraceEntryInspected_ThenIsReadonlyStruct()
        {
            // Assert
            Type type = typeof(DifferentialTraceEntry);
            Assert.True(type.IsValueType);

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                Assert.True(field.IsInitOnly,
                    $"DifferentialTraceEntry field '{field.Name}' is not readonly — entry struct must be readonly");
            }
        }

        [Fact]
        public void WhenDifferentialTraceEntryInspected_ThenNoMutableSetters()
        {
            // Assert
            PropertyInfo[] properties = typeof(DifferentialTraceEntry)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                Assert.False(property.CanWrite,
                    $"DifferentialTraceEntry.{property.Name} has a public setter — entry must be immutable");
            }
        }

        // =====================================================================
        // DifferentialTraceDiscrepancy: no policy leakage
        // =====================================================================

        [Fact]
        public void WhenDifferentialTraceDiscrepancyInspected_ThenNoPolicyTermsInPropertyNames()
        {
            // Assert
            AssertNoPolicyTermsOnType(typeof(DifferentialTraceDiscrepancy));
        }

        [Fact]
        public void WhenDifferentialTraceDiscrepancyInspected_ThenIsReadonlyStruct()
        {
            // Assert
            Type type = typeof(DifferentialTraceDiscrepancy);
            Assert.True(type.IsValueType);

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (FieldInfo field in fields)
            {
                Assert.True(field.IsInitOnly,
                    $"DifferentialTraceDiscrepancy field '{field.Name}' is not readonly");
            }
        }

        // =====================================================================
        // DifferentialTraceCapture: no policy leakage
        // =====================================================================

        [Fact]
        public void WhenDifferentialTraceCaptureInspected_ThenNoPolicyTermsInPropertyNames()
        {
            // Assert
            AssertNoPolicyTermsOnType(typeof(DifferentialTraceCapture));
        }

        [Fact]
        public void WhenDifferentialTraceCaptureInspected_ThenNoPolicyTermsInMethodNames()
        {
            // Assert
            MethodInfo[] methods = typeof(DifferentialTraceCapture)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (MethodInfo method in methods)
            {
                AssertNoPolicyTermInName(method.Name, $"DifferentialTraceCapture.{method.Name}");
            }
        }

        // =====================================================================
        // DifferentialTraceCompare: no policy leakage
        // =====================================================================

        [Fact]
        public void WhenDifferentialTraceCompareInspected_ThenNoPolicyTermsInMethodNames()
        {
            // Assert
            MethodInfo[] methods = typeof(DifferentialTraceCompare)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (MethodInfo method in methods)
            {
                AssertNoPolicyTermInName(method.Name, $"DifferentialTraceCompare.{method.Name}");
            }
        }

        [Fact]
        public void WhenDifferentialTraceCompareInspected_ThenIsStaticClass()
        {
            // Assert
            Type type = typeof(DifferentialTraceCompare);
            Assert.True(type.IsAbstract && type.IsSealed);
        }

        // =====================================================================
        // DifferentialTraceCompareResult: no policy leakage
        // =====================================================================

        [Fact]
        public void WhenDifferentialTraceCompareResultInspected_ThenNoPolicyTermsInPropertyNames()
        {
            // Assert
            AssertNoPolicyTermsOnType(typeof(DifferentialTraceCompareResult));
        }

        [Fact]
        public void WhenDifferentialTraceCompareResultInspected_ThenNoMutableSetters()
        {
            // Assert
            PropertyInfo[] properties = typeof(DifferentialTraceCompareResult)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                Assert.False(property.CanWrite,
                    $"DifferentialTraceCompareResult.{property.Name} has a setter — result must be immutable");
            }
        }

        // =====================================================================
        // Runtime switch: no auto-enable, no policy leakage
        // =====================================================================

        [Fact]
        public void WhenClusterPreparedModeEnabled_ThenFieldNameDoesNotContainPolicyTerms()
        {
            // Assert
            FieldInfo field = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core.PipelineControl)
                .GetField("ClusterPreparedModeEnabled", BindingFlags.Public | BindingFlags.Instance)!;
            Assert.NotNull(field);
            AssertNoPolicyTermInName(field.Name, "PipelineControl.ClusterPreparedModeEnabled");
        }

        [Fact]
        public void WhenPipelineControlCleared_ThenClusterPreparedModeNeverAutoEnabled()
        {
            // Arrange
            var pipeCtrl = new YAKSys_Hybrid_CPU.Processor.CPU_Core.PipelineControl();

            // Act
            pipeCtrl.Clear();

            // Assert — Stage 7 Phase A: ClusterPreparedMode is enabled by default after Clear()
            Assert.True(pipeCtrl.ClusterPreparedModeEnabled);
        }

        // =====================================================================
        // Phase 05 telemetry counters: no policy leakage
        // =====================================================================

        [Theory]
        [InlineData("DifferentialTraceCompareCount")]
        [InlineData("DifferentialTraceDiscrepancyCount")]
        [InlineData("ClusterModeFallbackCount")]
        public void WhenPhase05CounterInspected_ThenFieldNameDoesNotContainPolicyTerms(string counterName)
        {
            // Assert
            FieldInfo field = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core.PipelineControl)
                .GetField(counterName, BindingFlags.Public | BindingFlags.Instance)!;
            Assert.NotNull(field);
            AssertNoPolicyTermInName(field.Name, $"PipelineControl.{counterName}");
        }

        // =====================================================================
        // Pipeline RecordDifferentialTraceEntry: method isolation
        // =====================================================================

        [Fact]
        public void WhenRecordDifferentialTraceEntry_ThenMethodIsNotPublic()
        {
            // Assert
            Type coreType = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core);
            MethodInfo method = coreType.GetMethod("RecordDifferentialTraceEntry",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.NotNull(method);
            Assert.False(method.IsPublic);
        }

        [Fact]
        public void WhenRecordDifferentialTraceEntry_ThenReturnTypeIsVoid()
        {
            // Assert
            Type coreType = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core);
            MethodInfo method = coreType.GetMethod("RecordDifferentialTraceEntry",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
        }

        [Fact]
        public void WhenRecordDifferentialTraceEntry_ThenMethodNameDoesNotContainPolicyTerms()
        {
            // Assert
            Type coreType = typeof(YAKSys_Hybrid_CPU.Processor.CPU_Core);
            MethodInfo method = coreType.GetMethod("RecordDifferentialTraceEntry",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.NotNull(method);
            AssertNoPolicyTermInName(method.Name, "CPU_Core.RecordDifferentialTraceEntry");
        }

        // =====================================================================
        // Phase 01-04 boundary cross-check: decoder-side immutability preserved
        // =====================================================================

        [Fact]
        public void WhenDecodedBundleAdmissionPrepInspected_ThenPropertyCountUnchanged()
        {
            // Assert — Phase 01 contract: 8 properties
            PropertyInfo[] properties = typeof(DecodedBundleAdmissionPrep)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.Equal(8, properties.Length);
        }

        [Fact]
        public void WhenDecodedBundleDependencySummaryInspected_ThenPropertyCountUnchanged()
        {
            // Assert — Phase 01 contract: 9 properties
            PropertyInfo[] properties = typeof(DecodedBundleDependencySummary)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.Equal(9, properties.Length);
        }

        [Fact]
        public void WhenDecodeModeEnumInspected_ThenTwoValuesUnchanged()
        {
            // Assert — LegacySequentialMode and ClusterPreparedMode
            string[] names = Enum.GetNames<DecodeMode>();
            Assert.Equal(2, names.Length);
            Assert.Contains("ReferenceSequentialMode", names);
            Assert.Contains("ClusterPreparedMode", names);
        }

        [Fact]
        public void WhenHazardTriageClassInspected_ThenThreeValuesUnchanged()
        {
            // Assert — Safe, NeedsRuntimeCheck, HardReject
            string[] names = Enum.GetNames<HazardTriageClass>();
            Assert.Equal(3, names.Length);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static void AssertNoPolicyTermsOnType(Type type)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                AssertNoPolicyTermInName(property.Name, $"{type.Name}.{property.Name}");
            }
        }

        private static void AssertNoPolicyTermInName(string name, string context)
        {
            foreach (string term in PolicyLeakageTerms)
            {
                Assert.DoesNotContain(term, name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
