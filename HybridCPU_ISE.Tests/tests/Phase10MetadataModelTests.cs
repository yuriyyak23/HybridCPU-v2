// Phase 10: Metadata Model — SlotMetadata / BundleMetadata Formalization
// Covers:
//   - SchemaVersion on SlotMetadata and BundleMetadata
//   - SlotMetadata.ThermalHint field
//   - SlotMetadata.DonorVtHint (canonical name)
//   - BundleMetadata.BundleThermalHint (renamed from ThermalHint)
//   - BundleMetadata.IsReplayAnchor flag
//   - BundleMetadata.DiagnosticsTag field
//   - BundleMetadata.Default static instance
//   - BundleMetadata nullable SlotMetadata (fallback to Default)
//   - MetadataSchemaVersion constant
//   - MetadataCompatibilityValidator: Ok / Warning (newer) / Warning (older)

using System.Collections.Generic;
using Xunit;
using YAKSys_Hybrid_CPU.Core;

namespace HybridCPU_ISE.Tests.Phase10
{
    // ─────────────────────────────────────────────────────────────────────────
    // MetadataSchemaVersion
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataSchemaVersionTests
    {
        [Fact]
        public void MetadataSchemaVersion_Current_IsFour()
            => Assert.Equal(4, MetadataSchemaVersion.Current);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SlotMetadata v4 schema fields
    // ─────────────────────────────────────────────────────────────────────────

    public class SlotMetadataV4Tests
    {
        [Fact]
        public void SlotMetadata_Default_SchemaVersionIsFour()
            => Assert.Equal(4, new SlotMetadata().SchemaVersion);

        [Fact]
        public void SlotMetadata_DefaultSingleton_SchemaVersionIsFour()
            => Assert.Equal(4, SlotMetadata.Default.SchemaVersion);

        [Fact]
        public void SlotMetadata_Default_ThermalHintIsNone()
            => Assert.Equal(ThermalHint.None, new SlotMetadata().ThermalHint);

        [Fact]
        public void SlotMetadata_Default_DonorVtHintIs0xFF()
            => Assert.Equal(0xFF, new SlotMetadata().DonorVtHint);

        [Fact]
        public void SlotMetadata_DonorVtHint_CanBeSetViaInit()
        {
            var meta = new SlotMetadata { DonorVtHint = 2 };
            Assert.Equal(2, meta.DonorVtHint);
        }

        [Fact]
        public void SlotMetadata_ThermalHint_CanBeSetViaInit()
        {
            var meta = new SlotMetadata { ThermalHint = ThermalHint.Boost };
            Assert.Equal(ThermalHint.Boost, meta.ThermalHint);
        }

        [Fact]
        public void SlotMetadata_AllV4Fields_CanBeSetTogether()
        {
            var meta = new SlotMetadata
            {
                SchemaVersion = 4,
                BranchHint = BranchHint.Likely,
                StealabilityPolicy = StealabilityPolicy.NotStealable,
                LocalityHint = LocalityHint.Hot,
                PreferredVt = 3,
                DonorVtHint = 1,
                ThermalHint = ThermalHint.Throttle,
            };
            Assert.Equal(4, meta.SchemaVersion);
            Assert.Equal(BranchHint.Likely, meta.BranchHint);
            Assert.Equal(StealabilityPolicy.NotStealable, meta.StealabilityPolicy);
            Assert.Equal(LocalityHint.Hot, meta.LocalityHint);
            Assert.Equal(3, meta.PreferredVt);
            Assert.Equal(1, meta.DonorVtHint);
            Assert.Equal(ThermalHint.Throttle, meta.ThermalHint);
        }

        [Fact]
        public void SlotMetadata_IsRecord_SupportValueEquality_WithNewFields()
        {
            var a = new SlotMetadata { DonorVtHint = 5, ThermalHint = ThermalHint.Boost };
            var b = new SlotMetadata { DonorVtHint = 5, ThermalHint = ThermalHint.Boost };
            Assert.Equal(a, b);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BundleMetadata v4 schema fields
    // ─────────────────────────────────────────────────────────────────────────

    public class BundleMetadataV4Tests
    {
        [Fact]
        public void BundleMetadata_Default_SchemaVersionIsFour()
            => Assert.Equal(4, BundleMetadata.Default.SchemaVersion);

        [Fact]
        public void BundleMetadata_Default_FspBoundaryIsFalse()
            => Assert.False(BundleMetadata.Default.FspBoundary);

        [Fact]
        public void BundleMetadata_Default_BundleThermalHintIsNone()
            => Assert.Equal(ThermalHint.None, BundleMetadata.Default.BundleThermalHint);

        [Fact]
        public void BundleMetadata_Default_IsReplayAnchorIsFalse()
            => Assert.False(BundleMetadata.Default.IsReplayAnchor);

        [Fact]
        public void BundleMetadata_Default_DiagnosticsTagIsNull()
            => Assert.Null(BundleMetadata.Default.DiagnosticsTag);

        [Fact]
        public void BundleMetadata_Default_SlotMetadataIsNull()
            => Assert.Null(BundleMetadata.Default.SlotMetadata);

        [Fact]
        public void BundleMetadata_Default_GetSlotMetadata_ReturnsSlotDefault()
            => Assert.Equal(SlotMetadata.Default, BundleMetadata.Default.GetSlotMetadata(0));

        [Fact]
        public void BundleMetadata_NullSlotList_GetSlotMetadata_AlwaysReturnsDefault()
        {
            var bm = new BundleMetadata { SlotMetadata = null };
            for (int i = 0; i < BundleMetadata.BundleSlotCount; i++)
                Assert.Equal(SlotMetadata.Default, bm.GetSlotMetadata(i));
        }

        [Fact]
        public void BundleMetadata_IsReplayAnchor_CanBeSetViaInit()
        {
            var bm = new BundleMetadata { IsReplayAnchor = true };
            Assert.True(bm.IsReplayAnchor);
        }

        [Fact]
        public void BundleMetadata_DiagnosticsTag_CanBeSetViaInit()
        {
            var bm = new BundleMetadata { DiagnosticsTag = "loop_header" };
            Assert.Equal("loop_header", bm.DiagnosticsTag);
        }

        [Fact]
        public void BundleMetadata_BundleThermalHint_CanBeSetViaInit()
        {
            var bm = new BundleMetadata { BundleThermalHint = ThermalHint.Boost };
            Assert.Equal(ThermalHint.Boost, bm.BundleThermalHint);
        }

        [Fact]
        public void BundleMetadata_SchemaVersion_CanBeSetViaInit()
        {
            var bm = new BundleMetadata { SchemaVersion = 3 };
            Assert.Equal(3, bm.SchemaVersion);
        }

        [Fact]
        public void BundleMetadata_AllV4Fields_CanBeSetTogether()
        {
            var slots = new SlotMetadata[BundleMetadata.BundleSlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = SlotMetadata.Default;

            var bm = new BundleMetadata
            {
                SchemaVersion = 4,
                SlotMetadata = slots,
                FspBoundary = true,
                BundleThermalHint = ThermalHint.Throttle,
                IsReplayAnchor = true,
                DiagnosticsTag = "test_bundle",
            };

            Assert.Equal(4, bm.SchemaVersion);
            Assert.NotNull(bm.SlotMetadata);
            Assert.True(bm.FspBoundary);
            Assert.Equal(ThermalHint.Throttle, bm.BundleThermalHint);
            Assert.True(bm.IsReplayAnchor);
            Assert.Equal("test_bundle", bm.DiagnosticsTag);
        }

        [Fact]
        public void BundleMetadata_IsRecord_SupportValueEquality()
        {
            var a = new BundleMetadata { IsReplayAnchor = true, DiagnosticsTag = "tag" };
            var b = new BundleMetadata { IsReplayAnchor = true, DiagnosticsTag = "tag" };
            Assert.Equal(a, b);
        }

        [Fact]
        public void BundleMetadata_GetSlotMetadata_WithPartialList_OutOfRangeReturnsDefault()
        {
            var shortList = new List<SlotMetadata> { SlotMetadata.Default, SlotMetadata.Default };
            var bm = new BundleMetadata { SlotMetadata = shortList };
            Assert.Equal(SlotMetadata.Default, bm.GetSlotMetadata(5));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MetadataCompatibilityValidator
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataCompatibilityValidatorTests
    {
        [Fact]
        public void Validate_BundleMetadata_CurrentVersion_ReturnsOk()
        {
            var bm = new BundleMetadata { SchemaVersion = MetadataSchemaVersion.Current };
            var result = MetadataCompatibilityValidator.Validate(bm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.None, result.Severity);
            Assert.Null(result.Message);
        }

        [Fact]
        public void Validate_BundleMetadata_OlderVersion_ReturnsWarning()
        {
            var bm = new BundleMetadata { SchemaVersion = 3 };
            var result = MetadataCompatibilityValidator.Validate(bm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.Warning, result.Severity);
            Assert.NotNull(result.Message);
            Assert.Contains("older", result.Message);
        }

        [Fact]
        public void Validate_BundleMetadata_NewerVersion_ReturnsWarning()
        {
            var bm = new BundleMetadata { SchemaVersion = 5 };
            var result = MetadataCompatibilityValidator.Validate(bm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.Warning, result.Severity);
            Assert.NotNull(result.Message);
            Assert.Contains("newer", result.Message);
        }

        [Fact]
        public void Validate_SlotMetadata_CurrentVersion_ReturnsOk()
        {
            var sm = new SlotMetadata { SchemaVersion = MetadataSchemaVersion.Current };
            var result = MetadataCompatibilityValidator.Validate(sm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.None, result.Severity);
        }

        [Fact]
        public void Validate_SlotMetadata_OlderVersion_ReturnsWarning()
        {
            var sm = new SlotMetadata { SchemaVersion = 2 };
            var result = MetadataCompatibilityValidator.Validate(sm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.Warning, result.Severity);
            Assert.Contains("older", result.Message!);
        }

        [Fact]
        public void Validate_SlotMetadata_NewerVersion_ReturnsWarning()
        {
            var sm = new SlotMetadata { SchemaVersion = 10 };
            var result = MetadataCompatibilityValidator.Validate(sm);
            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.Warning, result.Severity);
            Assert.Contains("newer", result.Message!);
        }

        [Fact]
        public void ValidationResult_Ok_IsValidAndNoSeverity()
        {
            var r = ValidationResult.Ok();
            Assert.True(r.IsValid);
            Assert.Equal(ValidationSeverity.None, r.Severity);
            Assert.Null(r.Message);
        }

        [Fact]
        public void ValidationResult_Warning_IsValidWithMessage()
        {
            var r = ValidationResult.Warning("test warning");
            Assert.True(r.IsValid);
            Assert.Equal(ValidationSeverity.Warning, r.Severity);
            Assert.Equal("test warning", r.Message);
        }

        [Fact]
        public void ValidationResult_Error_IsNotValidWithMessage()
        {
            var r = ValidationResult.Error("test error");
            Assert.False(r.IsValid);
            Assert.Equal(ValidationSeverity.Error, r.Severity);
            Assert.Equal("test error", r.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FSP boundary with BundleMetadata.Default
    // ─────────────────────────────────────────────────────────────────────────

    public class FspBoundaryWithDefaultMetadataTests
    {
        private readonly FspProcessor _fsp = new();

        [Fact]
        public void FspPilfer_WithDefaultBundleMetadata_NullSlotList_ReturnsNoPilfers()
        {
            // BundleMetadata.Default has null SlotMetadata — all slots fall back to Default (Stealable)
            // but all bundle slots are null (empty), so no donors can be found.
            var bundle = new MicroOp?[8];
            var result = _fsp.TryPilfer(bundle, BundleMetadata.Default);
            Assert.Equal(0, result.PilferCount);
        }

        [Fact]
        public void FspPilfer_WithReplayAnchorBundle_BehavesLikeNormalBundle()
        {
            // IsReplayAnchor is for the diagnostics/replay subsystem only;
            // it does not affect FSP scheduling behavior.
            var bm = new BundleMetadata { IsReplayAnchor = true };
            var bundle = new MicroOp?[8];
            var result = _fsp.TryPilfer(bundle, bm);
            Assert.Equal(0, result.PilferCount);
        }
    }
}
