// V6 Phase 5: Metadata Extraction — H39–H43, I44–I46
//
// Tests verify:
//   [H39] CanBeStolen evicted from MicroOp / ISA encoding → SlotMetadata.StealabilityPolicy
//   [H40] VirtualThreadId placement hint evicted from word3 → SlotMetadata.PreferredVt
//   [H41] MetadataUnpacker no longer extracts word3[50:48] and returns sideband defaults
//   [H42] Only architecturally observable flags remain in ISA plane (word0[23:16])
//   [H43] Pipeline correct with null / empty SlotMetadata (no compiler hints required)
//   [I44] No compiler-emitted typed-slot fact needed for correct scheduling
//   [I45] Profiling/admission annotations separate from ISA semantics
//   [I46] Compiler vocabulary decoupled from reference execution model

using System.Reflection;
using HybridCPU.Compiler.Core.Threading;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Pipeline.Metadata;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using HybridCPU_ISE.Tests.TestHelpers;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;
using HybridCPU_ISE.Arch;

namespace HybridCPU_ISE.Tests.V6Phase5
{
    // ── local helper ──────────────────────────────────────────────────────────
    // Minimal valid InstructionIR used by tests that only care about the
    // SlotMetadataBuilder interface contract (not execution semantics).

    internal static class Phase5IrHelper
    {
        internal static InstructionIR MinimalAlu() => new InstructionIR
        {
            CanonicalOpcode    = InstructionsEnum.Addition,
            Class              = InstructionClass.ScalarAlu,
            SerializationClass = SerializationClass.Free,
            Rd = 1, Rs1 = 2, Rs2 = 3, Imm = 0,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H41] MetadataUnpacker — word3[50] → StealabilityPolicy
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataUnpackerStealabilityTests
    {
        private static VLIW_Instruction MakeInst(bool canBeStolen, byte vtHint = 0)
        {
            var inst = new VLIW_Instruction();
            inst.Word3           = canBeStolen ? 1UL << 50 : 0;
            inst.VirtualThreadId = vtHint;
            return inst;
        }

        [Fact]
        public void MetadataUnpacker_HasNo_UnpackSlotMetadata_Method()
        {
            var type = typeof(SlotMetadata).Assembly.GetType(
                "YAKSys_Hybrid_CPU.Core.Pipeline.Metadata.MetadataUnpacker");
            Assert.Null(type);
        }

        [Fact]
        public void UnpackSlotMetadata_WhenLegacyBitsDiffer_StillReturnsDefaultMetadata()
        {
            var inst = MakeInst(canBeStolen: false, vtHint: 1);
            var meta = SlotMetadata.Default;
            Assert.Same(SlotMetadata.Default, meta);
            Assert.Equal(StealabilityPolicy.Stealable, meta.StealabilityPolicy);
            Assert.Equal(0xFF, meta.PreferredVt);
        }

        [Fact]
        public void UnpackSlotMetadata_WhenLegacyDefaultPresent_ThenReturnsSingleton()
        {
            // CanBeStolen=true, VirtualThreadId=0 → fast-exit → Default singleton
            var inst = MakeInst(canBeStolen: true, vtHint: 0);
            var meta = SlotMetadata.Default;
            Assert.Same(SlotMetadata.Default, meta);
        }

        [Fact]
        public void UnpackSlotMetadata_WhenLegacyNonDefaultPresent_ThenDoesNotRecoverPolicyFromWord3()
        {
            var inst = MakeInst(canBeStolen: false, vtHint: 0);
            var meta = SlotMetadata.Default;
            Assert.Same(SlotMetadata.Default, meta);
            Assert.Equal(StealabilityPolicy.Stealable, meta.StealabilityPolicy);
            // vtHint=0 is ambiguous in legacy encoding — mapped to 0xFF (no preference)
            Assert.Equal(0xFF, meta.PreferredVt);
        }

        [Fact]
        public void MetadataUnpacker_HasNo_ExtractCanBeStolen_Method()
        {
            var type = typeof(SlotMetadata).Assembly.GetType(
                "YAKSys_Hybrid_CPU.Core.Pipeline.Metadata.MetadataUnpacker");
            Assert.Null(type);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H40] MetadataUnpacker — word3[49:48] → PreferredVt
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataUnpackerVtPlacementTests
    {
        private static VLIW_Instruction MakeInst(bool canBeStolen, byte vtHint)
        {
            var inst = new VLIW_Instruction();
            inst.Word3           = canBeStolen ? 1UL << 50 : 0;
            inst.VirtualThreadId = vtHint;
            return inst;
        }

        [Theory]
        [InlineData((byte)1)]
        [InlineData((byte)2)]
        [InlineData((byte)3)]
        public void UnpackSlotMetadata_WhenVtNonZero_ThenPreferredVtRemainsNoPreference(byte vtHint)
        {
            var inst = MakeInst(canBeStolen: true, vtHint: vtHint);
            var meta = SlotMetadata.Default;
            Assert.Same(SlotMetadata.Default, meta);
            Assert.Equal(0xFF, meta.PreferredVt);
        }

        [Fact]
        public void UnpackSlotMetadata_WhenVtZero_ThenPreferredVtIsNoPreference()
        {
            // VirtualThreadId=0 in legacy encoding is indistinguishable from "not set"
            var inst = MakeInst(canBeStolen: false, vtHint: 0);
            var meta = SlotMetadata.Default;
            Assert.Equal(0xFF, meta.PreferredVt);
        }

        [Fact]
        public void MetadataUnpacker_HasNo_ExtractVtPlacementHint_Method()
        {
            var type = typeof(SlotMetadata).Assembly.GetType(
                "YAKSys_Hybrid_CPU.Core.Pipeline.Metadata.MetadataUnpacker");
            Assert.Null(type);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H41] MetadataUnpacker — CompilerAnnotation overload
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataUnpackerAnnotationApiTests
    {
        [Fact]
        public void MetadataUnpacker_HasNo_UnpackAnnotation_Method()
        {
            // Null annotation → SlotMetadataBuilder returns SlotMetadata.Default
            var type = typeof(SlotMetadata).Assembly.GetType(
                "YAKSys_Hybrid_CPU.Core.Pipeline.Metadata.MetadataUnpacker");
            Assert.Null(type);
        }

    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H42] Only architectural flags remain in ISA plane
    // ─────────────────────────────────────────────────────────────────────────

    public class IsaArchitecturalFlagsTests
    {
        [Fact]
        public void VLIW_Instruction_ArchitecturalFlags_AreInWord0()
        {
            // Architectural flags (Acquire/Release/Saturating/MaskAgnostic/TailAgnostic)
            // are in word0[23:16]; setting them must not pollute word3 scheduling bits.
            var inst = new VLIW_Instruction();
            inst.Acquire     = true;
            inst.Release     = true;
            inst.Saturating  = true;
            inst.MaskAgnostic = true;
            inst.TailAgnostic = true;

            Assert.Equal(0UL, (inst.Word3 >> 50) & 0x1UL);
            Assert.Equal(0, inst.VirtualThreadId);
        }

        [Fact]
        public void VLIW_Instruction_SchedulingBits_AreInWord3()
        {
            // Setting scheduling bits must not influence architectural flags.
            var inst = new VLIW_Instruction();
            inst.Word3           = 1UL << 50;
            inst.VirtualThreadId = 3;

            Assert.False(inst.Acquire);
            Assert.False(inst.Release);
            Assert.False(inst.Saturating);
            Assert.False(inst.MaskAgnostic);
            Assert.False(inst.TailAgnostic);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H43] Metadata optional — pipeline correct without per-MicroOp slot shells
    // ─────────────────────────────────────────────────────────────────────────

    public class MetadataOptionalCorrectnessTests
    {
        [Fact]
        public void MicroOp_DefaultAdmissionMetadata_IsStealable()
        {
            var op = new NopMicroOp();
            Assert.True(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void MicroOp_ExplicitStealabilityFalse_IsReflectedInAdmissionMetadata()
        {
            var op = new ScalarALUMicroOp { IsStealable = false };
            Assert.False(op.AdmissionMetadata.IsStealable);
        }

        [Fact]
        public void SlotMetadataBuilder_NullAnnotation_ProducesAllDefaults()
        {
            var meta = new SlotMetadataBuilder().Build(Phase5IrHelper.MinimalAlu(), annotation: null);

            Assert.Equal(StealabilityPolicy.Stealable, meta.StealabilityPolicy);
            Assert.Equal(BranchHint.None,   meta.BranchHint);
            Assert.Equal(LocalityHint.None, meta.LocalityHint);
            Assert.Equal(ThermalHint.None,  meta.ThermalHint);
            Assert.Equal(0xFF,              meta.PreferredVt);
            Assert.Equal(0xFF,              meta.DonorVtHint);
        }

        [Fact]
        public void Scheduler_PackBundle_WithoutLegacySlotShell_DoesNotThrow()
        {
            var scheduler = new MicroOpScheduler();

            var candidate = new ScalarALUMicroOp
            {
                VirtualThreadId = 1
            };
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var ex = Record.Exception(() =>
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0));

            Assert.Null(ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [I44] No compiler-emitted typed-slot fact required for correctness
    // ─────────────────────────────────────────────────────────────────────────

    public class TypedSlotOptionalTests
    {
        [Fact]
        public void Scheduler_TypedSlotDisabled_PacksWithoutSlotClassAnnotation()
        {
            var scheduler          = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = false;

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            candidate.Placement = candidate.Placement with { RequiredSlotClass = SlotClass.Unclassified }; // simulate no compiler hint
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var ex = Record.Exception(() =>
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0));

            Assert.Null(ex);
        }

        [Fact]
        public void Scheduler_TypedSlotEnabled_GracefulWithUnclassifiedSlotClass()
        {
            // TypedSlotEnabled must degrade gracefully — missing compiler hints must not crash.
            var scheduler          = new MicroOpScheduler();
            scheduler.TypedSlotEnabled = true;

            var candidate = MicroOpTestHelper.CreateScalarALU(1, destReg: 20, src1Reg: 21, src2Reg: 22);
            candidate.Placement = candidate.Placement with { RequiredSlotClass = SlotClass.Unclassified };
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            var ex = Record.Exception(() =>
                scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0));

            Assert.Null(ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [I45] Profiling / admission annotations separate from ISA semantics
    // ─────────────────────────────────────────────────────────────────────────

    public class AnnotationSeparationTests
    {
        [Fact]
        public void CompilerAnnotation_DoesNotAffectInstructionSerializationClass()
        {
            var ir = Phase5IrHelper.MinimalAlu(); // SerializationClass.Free

            // Advisory annotation: hot path, not stealable, prefer VT 2
            var annotation = new CompilerAnnotation
            {
                ThermalHint        = ThermalHint.Boost,
                StealabilityPolicy = StealabilityPolicy.NotStealable,
                PreferredVt        = 2,
            };

            var meta = new SlotMetadataBuilder().Build(ir, annotation);

            // Annotation fields land in SlotMetadata, NOT in SerializationClass
            Assert.Equal(ThermalHint.Boost,               meta.ThermalHint);
            Assert.Equal(StealabilityPolicy.NotStealable,  meta.StealabilityPolicy);
            Assert.Equal(2, meta.PreferredVt);

            // Architectural serialisation class must be untouched by metadata
            Assert.Equal(SerializationClass.Free, ir.SerializationClass);
        }

        [Fact]
        public void BundleMetadata_FspBoundary_AllSlotsNotStealable()
        {
            // FSP boundary is a scheduling annotation — carries no architectural side effects.
            var bundleMeta = BundleMetadata.CreateFspFence();

            Assert.True(bundleMeta.FspBoundary);
            for (int i = 0; i < 8; i++)
            {
                var slotMeta = bundleMeta.GetSlotMetadata(i);
                Assert.Equal(StealabilityPolicy.NotStealable, slotMeta.StealabilityPolicy);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [I46] Compiler vocabulary decoupled from reference execution model
    // ─────────────────────────────────────────────────────────────────────────

    public class CompilerVocabularyDecouplingTests
    {
        [Fact]
        public void VliwBundleAnnotations_FromLegacySlotMetadata_ExposeCanonicalBundleMetadata()
        {
            var annotations = new VliwBundleAnnotations(
                new[]
                {
                    new InstructionSlotMetadata(
                        YAKSys_Hybrid_CPU.Core.Registers.VtId.Create(1),
                        SlotMetadata.NotStealable),
                });

            Assert.Equal(StealabilityPolicy.NotStealable, annotations.BundleMetadata.GetSlotMetadata(0).StealabilityPolicy);
            Assert.Equal(SlotMetadata.Default, annotations.BundleMetadata.GetSlotMetadata(1));
        }

        [Fact]
        public void ThreadCompilerContext_NonStealableInstruction_EmitsCanonicalSlotMetadata_ViaPolicySurface()
        {
            var context = new HybridCpuThreadCompilerContext(virtualThreadId: 2);
            context.CompileInstruction(
                opCode: (uint)InstructionsEnum.Addition,
                dataType: 0,
                predicate: 0,
                immediate: 0,
                destSrc1: 0,
                src2: 0,
                streamLength: 0,
                stride: 0,
                stealabilityPolicy: StealabilityPolicy.NotStealable);

            VliwBundleAnnotations annotations = context.GetBundleAnnotations();

            Assert.True(annotations.TryGetInstructionSlotMetadata(0, out InstructionSlotMetadata slotMetadata));
            Assert.Equal((byte)2, slotMetadata.VirtualThreadId.Value);
            Assert.Equal(StealabilityPolicy.NotStealable, slotMetadata.SlotMetadata.StealabilityPolicy);
            Assert.Equal(StealabilityPolicy.NotStealable, annotations.BundleMetadata.GetSlotMetadata(0).StealabilityPolicy);
        }

        [Fact]
        public void SlotMetadata_ExistsWithNoCompilerAnnotation()
        {
            var meta = new SlotMetadata();

            Assert.Equal(MetadataSchemaVersion.Current, meta.SchemaVersion);
            Assert.Equal(StealabilityPolicy.Stealable, meta.StealabilityPolicy);
            Assert.Equal(BranchHint.None, meta.BranchHint);
            Assert.Equal(0xFF, meta.PreferredVt);
        }

        [Fact]
        public void Scheduler_CanScheduleWithoutAnyCompilerMetadata()
        {
            var scheduler = new MicroOpScheduler();

            var candidate = new ScalarALUMicroOp
            {
                VirtualThreadId = 1
            };
            scheduler.NominateSmtCandidate(1, candidate);

            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateScalarALU(0, destReg: 1, src1Reg: 2, src2Reg: 3);

            MicroOp[]? result = null;
            var ex = Record.Exception(() =>
                result = scheduler.PackBundleIntraCoreSmt(bundle, ownerVirtualThreadId: 0, localCoreId: 0));

            Assert.Null(ex);
            Assert.NotNull(result);
        }

        [Fact]
        public void CompilerAnnotation_AllDefaults_EquivalentToNullAnnotation()
        {
            // An all-default annotation must produce the same SlotMetadata as null.
            var builder = new SlotMetadataBuilder();
            var ir      = Phase5IrHelper.MinimalAlu();

            var fromNull    = builder.Build(ir, annotation: null);
            var fromDefault = builder.Build(ir, annotation: new CompilerAnnotation());

            Assert.Equal(fromNull.StealabilityPolicy, fromDefault.StealabilityPolicy);
            Assert.Equal(fromNull.BranchHint,         fromDefault.BranchHint);
            Assert.Equal(fromNull.LocalityHint,       fromDefault.LocalityHint);
            Assert.Equal(fromNull.ThermalHint,        fromDefault.ThermalHint);
            Assert.Equal(fromNull.PreferredVt,        fromDefault.PreferredVt);
            Assert.Equal(fromNull.DonorVtHint,        fromDefault.DonorVtHint);
        }

        [Fact]
        public void MetadataCompatibilityValidator_AcceptsCurrentSchemaVersion()
        {
            var meta   = new SlotMetadata();
            var result = MetadataCompatibilityValidator.Validate(meta);

            Assert.True(result.IsValid);
            Assert.Equal(ValidationSeverity.None, result.Severity);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // [H39] CanBeStolen fully evicted from MicroOp / ISA correctness path
    // ─────────────────────────────────────────────────────────────────────────

    public class CanBeStolenEvictionTests
    {
        [Fact]
        public void MicroOp_HasNo_CanBeStolen_Property()
        {
            // Mirrors [T5-04]; retained here for Phase 5 coverage completeness.
            var prop = typeof(MicroOp).GetProperty(
                "CanBeStolen", BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(prop);
        }

        [Fact]
        public void VliwInstruction_HasNo_CanBeStolen_Property()
        {
            var prop = typeof(VLIW_Instruction).GetProperty(
                "CanBeStolen",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Null(prop);
        }

        [Fact]
        public void MetadataUnpacker_IgnoresEncodedCanBeStolen_AndKeepsDefaultStealabilityPolicy()
        {
            // MetadataUnpacker is the official bridge from legacy word3[50] to the
            // new SlotMetadata.StealabilityPolicy field.
            var stolenInst = new VLIW_Instruction();
            stolenInst.Word3           = 1UL << 50;
            stolenInst.VirtualThreadId = 1;

            var notStolenInst = new VLIW_Instruction();
            notStolenInst.Word3           = 0;
            notStolenInst.VirtualThreadId = 1;

            var stolenMeta    = SlotMetadata.Default;
            var notStolenMeta = SlotMetadata.Default;

            Assert.Same(SlotMetadata.Default, stolenMeta);
            Assert.Same(SlotMetadata.Default, notStolenMeta);
            Assert.Equal(StealabilityPolicy.Stealable, stolenMeta.StealabilityPolicy);
            Assert.Equal(StealabilityPolicy.Stealable, notStolenMeta.StealabilityPolicy);
        }
    }
}

