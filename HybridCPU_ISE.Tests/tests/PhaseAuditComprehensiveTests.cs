// ─────────────────────────────────────────────────────────────────────────────
// HybridCPU ISA v4 — Comprehensive Refactoring Audit Tests
// Validates the completion of deferred items: C14-C17, E25-E28, G37-G38,
// J53, K54-K56.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Xunit;
using YAKSys_Hybrid_CPU.Arch;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Memory;
using YAKSys_Hybrid_CPU.Core.Pipeline;
using YAKSys_Hybrid_CPU.Core.Pipeline.MicroOps;
using static YAKSys_Hybrid_CPU.Processor.CPU_Core;

namespace HybridCPU_ISE.Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // C14: TRAP_ENTRY — TrapPending state + TrapEnter trigger
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class C14TrapEntryFsmTests
    {
        [Fact]
        public void PipelineState_HasTrapPending_Value9()
            => Assert.Equal(9, (int)PipelineState.TrapPending);

        [Fact]
        public void Trigger_HasTrapEnter()
            => Assert.True(Enum.IsDefined(typeof(PipelineTransitionTrigger),
                PipelineTransitionTrigger.TrapEnter));

        [Fact]
        public void Task_TrapEnter_Transitions_To_TrapPending()
            => Assert.Equal(PipelineState.TrapPending,
                PipelineFsmGuard.Transition(PipelineState.Task,
                    PipelineTransitionTrigger.TrapEnter));

        [Fact]
        public void TrapEntryEvent_MapsTo_TrapEnter_Trigger()
        {
            var evt = new TrapEntryEvent
            {
                VtId         = 0,
                BundleSerial = 1,
                CauseCode    = 0x8000_0000_0000_0005UL, // interrupt 5
                FaultAddress = 0,
            };
            var result = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.TrapPending, result);
        }

        [Fact]
        public void TrapEntryEvent_CarriesCauseCode()
        {
            var evt = new TrapEntryEvent
            {
                VtId = 0, BundleSerial = 0,
                CauseCode = 0x0000_0002UL,  // illegal instruction
                FaultAddress = 0xDEAD,
            };
            Assert.Equal(0x0000_0002UL, evt.CauseCode);
            Assert.Equal(0xDEADUL, evt.FaultAddress);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C15: TRAP_RETURN — MretEvent / SretEvent map to TrapReturn trigger
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class C15TrapReturnFsmTests
    {
        [Fact]
        public void Trigger_HasTrapReturn()
            => Assert.True(Enum.IsDefined(typeof(PipelineTransitionTrigger),
                PipelineTransitionTrigger.TrapReturn));

        [Fact]
        public void TrapPending_TrapReturn_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.TrapPending,
                    PipelineTransitionTrigger.TrapReturn));

        [Fact]
        public void MretEvent_MapsTo_TrapReturn_From_TrapPending()
        {
            var evt = new MretEvent { VtId = 0, BundleSerial = 0 };
            var result = PipelineFsmGuard.Advance(PipelineState.TrapPending, evt);
            Assert.Equal(PipelineState.Task, result);
        }

        [Fact]
        public void SretEvent_MapsTo_TrapReturn_From_TrapPending()
        {
            var evt = new SretEvent { VtId = 0, BundleSerial = 0 };
            var result = PipelineFsmGuard.Advance(PipelineState.TrapPending, evt);
            Assert.Equal(PipelineState.Task, result);
        }

        [Fact]
        public void TrapPending_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.TrapPending,
                    PipelineTransitionTrigger.Interrupt));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C16: WFE/SEV — WaitForEvent state + EnterWaitForEvent / ExitWaitForEvent
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class C16WfeSeVFsmTests
    {
        [Fact]
        public void PipelineState_HasWaitForEvent_Value10()
            => Assert.Equal(10, (int)PipelineState.WaitForEvent);

        [Fact]
        public void Trigger_HasEnterWaitForEvent()
            => Assert.True(Enum.IsDefined(typeof(PipelineTransitionTrigger),
                PipelineTransitionTrigger.EnterWaitForEvent));

        [Fact]
        public void Trigger_HasExitWaitForEvent()
            => Assert.True(Enum.IsDefined(typeof(PipelineTransitionTrigger),
                PipelineTransitionTrigger.ExitWaitForEvent));

        [Fact]
        public void Task_EnterWaitForEvent_Transitions_To_WaitForEvent()
            => Assert.Equal(PipelineState.WaitForEvent,
                PipelineFsmGuard.Transition(PipelineState.Task,
                    PipelineTransitionTrigger.EnterWaitForEvent));

        [Fact]
        public void WaitForEvent_ExitWaitForEvent_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.WaitForEvent,
                    PipelineTransitionTrigger.ExitWaitForEvent));

        [Fact]
        public void WaitForEvent_Interrupt_Transitions_To_Task()
            => Assert.Equal(PipelineState.Task,
                PipelineFsmGuard.Transition(PipelineState.WaitForEvent,
                    PipelineTransitionTrigger.Interrupt));

        [Fact]
        public void WfeEvent_MapsTo_EnterWaitForEvent_Trigger()
        {
            var wfe = new WfeEvent { VtId = 1, BundleSerial = 42 };
            var result = PipelineFsmGuard.Advance(PipelineState.Task, wfe);
            Assert.Equal(PipelineState.WaitForEvent, result);
        }

        [Fact]
        public void SevEvent_MapsTo_ExitWaitForEvent_Trigger()
        {
            var sev = new SevEvent { VtId = 0, BundleSerial = 43 };
            var result = PipelineFsmGuard.Advance(PipelineState.WaitForEvent, sev);
            Assert.Equal(PipelineState.Task, result);
        }

        [Fact]
        public void SevEvent_From_Task_ThrowsIllegalTransition()
        {
            // SEV from Task has no FSM transition (ExitWaitForEvent requires WaitForEvent source).
            // Architecturally, SEV is only valid when a VT is in WaitForEvent state.
            var sev = new SevEvent { VtId = 0, BundleSerial = 0 };
            Assert.Throws<IllegalFsmTransitionException>(() =>
                PipelineFsmGuard.Advance(PipelineState.Task, sev));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C17: SystemEventOrderGuarantee on SysEventMicroOp
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class C17OrderGuaranteeTests
    {
        [Fact]
        public void SystemEventOrderGuarantee_HasExpectedValues()
        {
            Assert.Equal(0, (int)SystemEventOrderGuarantee.None);
            Assert.Equal(1, (int)SystemEventOrderGuarantee.DrainStores);
            Assert.Equal(2, (int)SystemEventOrderGuarantee.DrainMemory);
            Assert.Equal(3, (int)SystemEventOrderGuarantee.FlushPipeline);
            Assert.Equal(4, (int)SystemEventOrderGuarantee.FullSerialTrapBoundary);
        }

        [Fact]
        public void ForFence_Returns_DrainMemory()
        {
            var op = SysEventMicroOp.ForFence();
            Assert.Equal(SystemEventKind.Fence, op.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.DrainMemory, op.OrderGuarantee);
        }

        [Fact]
        public void ForFenceI_Returns_FlushPipeline()
        {
            var op = SysEventMicroOp.ForFenceI();
            Assert.Equal(SystemEventKind.FenceI, op.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FlushPipeline, op.OrderGuarantee);
        }

        [Fact]
        public void ForEcall_Returns_FullSerialTrapBoundary()
        {
            var op = SysEventMicroOp.ForEcall();
            Assert.Equal(SystemEventKind.Ecall, op.EventKind);
            Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary, op.OrderGuarantee);
            Assert.Equal(new[] { 17 }, op.AdmissionMetadata.ReadRegisters);
            Assert.Empty(op.AdmissionMetadata.WriteRegisters);
        }

        [Fact]
        public void ForEbreak_Returns_FullSerialTrapBoundary()
            => Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary,
                SysEventMicroOp.ForEbreak().OrderGuarantee);

        [Fact]
        public void ForMret_Returns_FullSerialTrapBoundary()
            => Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary,
                SysEventMicroOp.ForMret().OrderGuarantee);

        [Fact]
        public void ForSret_Returns_FullSerialTrapBoundary()
            => Assert.Equal(SystemEventOrderGuarantee.FullSerialTrapBoundary,
                SysEventMicroOp.ForSret().OrderGuarantee);

        [Fact]
        public void ForWfi_Returns_DrainMemory()
            => Assert.Equal(SystemEventOrderGuarantee.DrainMemory,
                SysEventMicroOp.ForWfi().OrderGuarantee);

        [Fact]
        public void DefaultCtor_HasNoOrderGuarantee()
        {
            var op = new SysEventMicroOp();
            Assert.Equal(SystemEventOrderGuarantee.None, op.OrderGuarantee);
        }

        [Fact]
        public void TrapBoundary_OrderGuarantee_GreaterThan_FlushPipeline()
            => Assert.True(
                SystemEventOrderGuarantee.FullSerialTrapBoundary >
                SystemEventOrderGuarantee.FlushPipeline);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E25/E26: InternalOpCategory — computed from Kind, independent of ISA opcode
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class E25E26InternalOpCategoryTests
    {
        [Fact]
        public void Add_Category_IsComputation()
            => Assert.Equal(InternalOpCategory.Computation,
                new InternalOp { Kind = InternalOpKind.Add }.Category);

        [Fact]
        public void Mul_Category_IsComputation()
            => Assert.Equal(InternalOpCategory.Computation,
                new InternalOp { Kind = InternalOpKind.Mul }.Category);

        [Fact]
        public void Load_Category_IsMemoryAccess()
            => Assert.Equal(InternalOpCategory.MemoryAccess,
                new InternalOp { Kind = InternalOpKind.Load }.Category);

        [Fact]
        public void Store_Category_IsMemoryAccess()
            => Assert.Equal(InternalOpCategory.MemoryAccess,
                new InternalOp { Kind = InternalOpKind.Store }.Category);

        [Fact]
        public void Jal_Category_IsControlFlow()
            => Assert.Equal(InternalOpCategory.ControlFlow,
                new InternalOp { Kind = InternalOpKind.Jal }.Category);

        [Fact]
        public void Branch_Category_IsControlFlow()
            => Assert.Equal(InternalOpCategory.ControlFlow,
                new InternalOp { Kind = InternalOpKind.Branch }.Category);

        [Fact]
        public void AmoWord_Category_IsAtomic()
            => Assert.Equal(InternalOpCategory.Atomic,
                new InternalOp { Kind = InternalOpKind.AmoWord }.Category);

        [Fact]
        public void LrW_Category_IsAtomic()
            => Assert.Equal(InternalOpCategory.Atomic,
                new InternalOp { Kind = InternalOpKind.LrW }.Category);

        [Fact]
        public void CsrReadWrite_Category_IsCsr()
            => Assert.Equal(InternalOpCategory.Csr,
                new InternalOp { Kind = InternalOpKind.CsrReadWrite }.Category);

        [Fact]
        public void CsrClear_Category_IsCsr()
            => Assert.Equal(InternalOpCategory.Csr,
                new InternalOp { Kind = InternalOpKind.CsrClear }.Category);

        [Fact]
        public void Yield_Category_IsSysEvent()
            => Assert.Equal(InternalOpCategory.SysEvent,
                new InternalOp { Kind = InternalOpKind.Yield }.Category);

        [Fact]
        public void Fence_Category_IsSysEvent()
            => Assert.Equal(InternalOpCategory.SysEvent,
                new InternalOp { Kind = InternalOpKind.Fence }.Category);

        [Fact]
        public void Wfe_Category_IsSysEvent()
            => Assert.Equal(InternalOpCategory.SysEvent,
                new InternalOp { Kind = InternalOpKind.Wfe }.Category);

        [Fact]
        public void VmRead_Category_IsVmxEvent()
            => Assert.Equal(InternalOpCategory.VmxEvent,
                new InternalOp { Kind = InternalOpKind.VmRead }.Category);

        [Fact]
        public void VmWrite_Category_IsVmxEvent()
            => Assert.Equal(InternalOpCategory.VmxEvent,
                new InternalOp { Kind = InternalOpKind.VmWrite }.Category);

        [Fact]
        public void Category_DoesNotDependOnISAOpcode()
        {
            // Category must be the same for any two Add ops regardless of origin metadata
            var op1 = new InternalOp { Kind = InternalOpKind.Add };
            var op2 = new InternalOp { Kind = InternalOpKind.Add, Immediate = 999, Rs1 = 5 };
            Assert.Equal(op1.Category, op2.Category);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E27: Explicit effect descriptors
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class E27EffectDescriptorTests
    {
        [Fact]
        public void Load_IsMemoryRead()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Load }.IsMemoryRead);

        [Fact]
        public void Load_IsNot_MemoryWrite()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Load }.IsMemoryWrite);

        [Fact]
        public void Store_IsMemoryWrite()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Store }.IsMemoryWrite);

        [Fact]
        public void Store_IsNot_MemoryRead()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Store }.IsMemoryRead);

        [Fact]
        public void AmoWord_IsMemoryReadAndWrite()
        {
            var op = new InternalOp { Kind = InternalOpKind.AmoWord };
            Assert.True(op.IsMemoryRead);
            Assert.True(op.IsMemoryWrite);
        }

        [Fact]
        public void LrW_IsMemoryRead_Not_Write()
        {
            var op = new InternalOp { Kind = InternalOpKind.LrW };
            Assert.True(op.IsMemoryRead);
            Assert.False(op.IsMemoryWrite);
        }

        [Fact]
        public void ScW_IsMemoryWrite_Not_Read()
        {
            var op = new InternalOp { Kind = InternalOpKind.ScW };
            Assert.False(op.IsMemoryRead);
            Assert.True(op.IsMemoryWrite);
        }

        [Fact]
        public void Jal_IsControlTransfer()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Jal }.IsControlTransfer);

        [Fact]
        public void Add_IsNot_ControlTransfer()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Add }.IsControlTransfer);

        [Fact]
        public void Div_IsExceptionPotential()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Div }.IsExceptionPotential);

        [Fact]
        public void Load_IsExceptionPotential()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Load }.IsExceptionPotential);

        [Fact]
        public void Ecall_IsExceptionPotential()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Ecall }.IsExceptionPotential);

        [Fact]
        public void Add_IsNot_ExceptionPotential()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Add }.IsExceptionPotential);

        [Fact]
        public void AmoWord_WithAcquire_HasOrdering()
        {
            var op = new InternalOp
            {
                Kind  = InternalOpKind.AmoWord,
                Flags = InternalOpFlags.AcquireOrdering,
            };
            Assert.True(op.HasOrdering);
        }

        [Fact]
        public void AmoWord_NoFlags_NoOrdering()
        {
            var op = new InternalOp { Kind = InternalOpKind.AmoWord };
            Assert.False(op.HasOrdering);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E28: Serialisation properties on InternalOp
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class E28SerializationTests
    {
        [Fact]
        public void CsrReadWrite_IsSerializing()
            => Assert.True(new InternalOp { Kind = InternalOpKind.CsrReadWrite }.IsSerializing);

        [Fact]
        public void Fence_IsSerializing()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Fence }.IsSerializing);

        [Fact]
        public void Wfe_IsSerializing()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Wfe }.IsSerializing);

        [Fact]
        public void VmRead_IsSerializing()
            => Assert.True(new InternalOp { Kind = InternalOpKind.VmRead }.IsSerializing);

        [Fact]
        public void Add_IsNot_Serializing()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Add }.IsSerializing);

        [Fact]
        public void Load_IsNot_Serializing()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Load }.IsSerializing);

        [Fact]
        public void PodBarrier_RequiresPipelineFlush()
            => Assert.True(new InternalOp { Kind = InternalOpKind.PodBarrier }.RequiresPipelineFlush);

        [Fact]
        public void FenceI_RequiresPipelineFlush()
            => Assert.True(new InternalOp { Kind = InternalOpKind.FenceI }.RequiresPipelineFlush);

        [Fact]
        public void VmWrite_RequiresPipelineFlush()
            => Assert.True(new InternalOp { Kind = InternalOpKind.VmWrite }.RequiresPipelineFlush);

        [Fact]
        public void Add_DoesNot_RequirePipelineFlush()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Add }.RequiresPipelineFlush);

        [Fact]
        public void Wfe_ForbidsFsp()
            => Assert.True(new InternalOp { Kind = InternalOpKind.Wfe }.ForbidsFsp);

        [Fact]
        public void Add_DoesNot_ForbidFsp()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Add }.ForbidsFsp);

        [Fact]
        public void CsrReadWrite_ForbidsSmtInjection()
            => Assert.True(new InternalOp { Kind = InternalOpKind.CsrReadWrite }.ForbidsSmtInjection);

        [Fact]
        public void Store_DoesNot_ForbidSmtInjection()
            => Assert.False(new InternalOp { Kind = InternalOpKind.Store }.ForbidsSmtInjection);

        [Fact]
        public void ForbidsFsp_ConsistentWith_IsSerializing()
        {
            // ForbidsFsp must be true exactly when IsSerializing is true
            foreach (InternalOpKind kind in Enum.GetValues<InternalOpKind>())
            {
                var op = new InternalOp { Kind = kind };
                Assert.Equal(op.IsSerializing, op.ForbidsFsp);
            }
        }

        [Fact]
        public void ForbidsSmtInjection_ConsistentWith_IsSerializing()
        {
            foreach (InternalOpKind kind in Enum.GetValues<InternalOpKind>())
            {
                var op = new InternalOp { Kind = kind };
                Assert.Equal(op.IsSerializing, op.ForbidsSmtInjection);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G37: SafetyMaskBuilder.BuildFromOpcode — runtime path without hints
    // G38: SafetyMaskBuilder.BuildFromInternalOp — decoupled from opcode ranges
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class G37G38SafetyMaskBuilderTests
    {
        [Fact]
        public void BuildFromOpcode_UnknownOpcode_Returns_Zero()
        {
            var mask = SafetyMaskBuilder.BuildFromOpcode(0xFFFF_FFFF);
            Assert.Equal(SafetyMask128.Zero, mask);
        }

        [Fact]
        public void BuildFromOpcode_AdditionOpcode_Returns_NonZero()
        {
            uint addOpcode = (uint)InstructionsEnum.Addition;
            var mask = SafetyMaskBuilder.BuildFromOpcode(addOpcode);
            Assert.NotEqual(SafetyMask128.Zero, mask);
        }

        [Fact]
        public void BuildFromOpcode_SameAsClass_ForKnownOpcodes()
        {
            // For ADD (ScalarAlu / Free): BuildFromOpcode must agree with BuildFromClass
            uint addOpcode = (uint)InstructionsEnum.Addition;
            var fromOpcode = SafetyMaskBuilder.BuildFromOpcode(addOpcode);
            var fromClass  = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.ScalarAlu, SerializationClass.Free);
            Assert.Equal(fromClass, fromOpcode);
        }

        [Fact]
        public void BuildFromInternalOp_Add_MatchesScalarAluFreeClass()
        {
            var op  = new InternalOp { Kind = InternalOpKind.Add };
            var got = SafetyMaskBuilder.BuildFromInternalOp(op);
            var exp = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.ScalarAlu, SerializationClass.Free);
            Assert.Equal(exp, got);
        }

        [Fact]
        public void BuildFromInternalOp_Load_MatchesMemoryOrderedClass()
        {
            var op  = new InternalOp { Kind = InternalOpKind.Load };
            var got = SafetyMaskBuilder.BuildFromInternalOp(op);
            var exp = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.Memory, SerializationClass.MemoryOrdered);
            Assert.Equal(exp, got);
        }

        [Fact]
        public void BuildFromInternalOp_Wfe_MatchesSmtVtFullSerialClass()
        {
            var op  = new InternalOp { Kind = InternalOpKind.Wfe };
            var got = SafetyMaskBuilder.BuildFromInternalOp(op);
            var exp = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.SmtVt, SerializationClass.FullSerial);
            Assert.Equal(exp, got);
        }

        [Fact]
        public void BuildFromInternalOp_VmRead_MatchesVmxSerialClass()
        {
            var op  = new InternalOp { Kind = InternalOpKind.VmRead };
            var got = SafetyMaskBuilder.BuildFromInternalOp(op);
            var exp = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.Vmx, SerializationClass.VmxSerial);
            Assert.Equal(exp, got);
        }

        [Fact]
        public void BuildFromInternalOp_AmoWithAcquireRelease_MatchesAtomicSerial()
        {
            var op = new InternalOp
            {
                Kind  = InternalOpKind.AmoWord,
                Flags = InternalOpFlags.AcquireOrdering | InternalOpFlags.ReleaseOrdering,
            };
            var got = SafetyMaskBuilder.BuildFromInternalOp(op);
            var exp = SafetyMaskBuilder.BuildFromClass(
                InstructionClass.Atomic, SerializationClass.AtomicSerial);
            Assert.Equal(exp, got);
        }

        [Fact]
        public void BuildFromInternalOp_DoesNotDependOnOpcodeRange()
        {
            // Two ops with different metadata but same Kind must produce the same mask
            var op1 = new InternalOp { Kind = InternalOpKind.Store };
            var op2 = new InternalOp { Kind = InternalOpKind.Store, Rd = 7, Rs1 = 3, Immediate = 128 };
            Assert.Equal(
                SafetyMaskBuilder.BuildFromInternalOp(op1),
                SafetyMaskBuilder.BuildFromInternalOp(op2));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // J53: VmcsField nested translation fields
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class J53VmcsNestedTranslationFieldsTests
    {
        [Fact]
        public void VmcsField_HasEptPointer_Value80()
            => Assert.Equal(80, (ushort)VmcsField.EptPointer);

        [Fact]
        public void VmcsField_HasVpid_Value81()
            => Assert.Equal(81, (ushort)VmcsField.Vpid);

        [Fact]
        public void VmcsField_HasSecondaryProcControls_Value82()
            => Assert.Equal(82, (ushort)VmcsField.SecondaryProcControls);

        [Fact]
        public void VmcsField_HasCr3TargetCount_Value83()
            => Assert.Equal(83, (ushort)VmcsField.Cr3TargetCount);

        [Fact]
        public void VmcsField_HasGuestPhysicalAddress_Value112()
            => Assert.Equal(112, (ushort)VmcsField.GuestPhysicalAddress);

        [Fact]
        public void VmcsField_HasEptViolationQualification_Value113()
            => Assert.Equal(113, (ushort)VmcsField.EptViolationQualification);

        [Fact]
        public void VmcsManager_CanReadWrite_EptPointer()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000UL); // VMPTRLD with dummy physical address
            mgr.WriteField(VmcsField.EptPointer, 0x1234_5678_0000L);
            Assert.Equal(0x1234_5678_0000L, mgr.ReadField(VmcsField.EptPointer));
        }

        [Fact]
        public void VmcsManager_CanReadWrite_Vpid()
        {
            var mgr = new VmcsManager();
            mgr.Load(0x1000UL);
            mgr.WriteField(VmcsField.Vpid, 42L);
            Assert.Equal(42L, mgr.ReadField(VmcsField.Vpid));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // K54: MemoryOrderingEvent — first-class atomic ordering barrier
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class K54MemoryOrderingEventTests
    {
        [Fact]
        public void MemoryOrderingEvent_IsAcquire_True()
        {
            var evt = new MemoryOrderingEvent
            {
                VtId = 0, BundleSerial = 0,
                IsAcquire = true, IsRelease = false,
            };
            Assert.True(evt.IsAcquire);
            Assert.False(evt.IsRelease);
        }

        [Fact]
        public void MemoryOrderingEvent_IsRelease_True()
        {
            var evt = new MemoryOrderingEvent
            {
                VtId = 0, BundleSerial = 0,
                IsAcquire = false, IsRelease = true,
            };
            Assert.False(evt.IsAcquire);
            Assert.True(evt.IsRelease);
        }

        [Fact]
        public void MemoryOrderingEvent_AcqRel_Both_True()
        {
            var evt = new MemoryOrderingEvent
            {
                VtId = 0, BundleSerial = 0,
                IsAcquire = true, IsRelease = true,
            };
            Assert.True(evt.IsAcquire);
            Assert.True(evt.IsRelease);
        }

        [Fact]
        public void MemoryOrderingEvent_DerivedFrom_PipelineEvent()
        {
            var evt = new MemoryOrderingEvent
            {
                VtId = 2, BundleSerial = 100,
                IsAcquire = true, IsRelease = false,
            };
            Assert.IsAssignableFrom<PipelineEvent>(evt);
            Assert.Equal(2, evt.VtId);
            Assert.Equal(100UL, evt.BundleSerial);
        }

        [Fact]
        public void MemoryOrderingEvent_DoesNotTransition_FsmState()
        {
            // Ordering events do NOT change the pipeline FSM state
            var evt = new MemoryOrderingEvent
            {
                VtId = 0, BundleSerial = 0,
                IsAcquire = true, IsRelease = true,
            };
            var result = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Task, result);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // K55: IHardwareOccupancyInput interface contract
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class K55HardwareOccupancyInputTests
    {
        private sealed class StubOccupancyInput : IHardwareOccupancyInput
        {
            public SafetyMask128 NextMask { get; set; } = SafetyMask128.Zero;
            public int AdvanceCallCount { get; private set; }
            public HardwareOccupancySnapshot128 GetHardwareOccupancySnapshot128() => new HardwareOccupancySnapshot128();

            private SafetyMask128 _current = SafetyMask128.Zero;

            public SafetyMask128 GetHardwareOccupancyMask128() => _current;

            public void AdvanceSamplingEpoch()
            {
                _current = NextMask;
                AdvanceCallCount++;
            }
        }

        [Fact]
        public void GetHardwareOccupancyMask128_Returns_Zero_Initially()
        {
            var stub = new StubOccupancyInput();
            Assert.Equal(SafetyMask128.Zero, stub.GetHardwareOccupancyMask128());
        }

        [Fact]
        public void AdvanceSamplingEpoch_Updates_Mask()
        {
            var stub = new StubOccupancyInput
            {
                NextMask = new SafetyMask128(0xFFUL, 0UL),
            };
            stub.AdvanceSamplingEpoch();
            Assert.Equal(new SafetyMask128(0xFFUL, 0UL), stub.GetHardwareOccupancyMask128());
        }

        [Fact]
        public void GetHardwareOccupancyMask128_Idempotent_WithinEpoch()
        {
            var stub = new StubOccupancyInput
            {
                NextMask = new SafetyMask128(0xABUL, 0UL),
            };
            stub.AdvanceSamplingEpoch();
            var m1 = stub.GetHardwareOccupancyMask128();
            var m2 = stub.GetHardwareOccupancyMask128();
            Assert.Equal(m1, m2);
        }

        [Fact]
        public void AdvanceSamplingEpoch_CalledOnce_PerCycle()
        {
            var stub = new StubOccupancyInput();
            stub.AdvanceSamplingEpoch();
            stub.AdvanceSamplingEpoch();
            stub.AdvanceSamplingEpoch();
            Assert.Equal(3, stub.AdvanceCallCount);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // K56: BackpressureEvent — separated from memory and trap semantics
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class K56BackpressureEventTests
    {
        [Fact]
        public void BackpressureEvent_DerivedFrom_PipelineEvent()
        {
            var evt = new BackpressureEvent
            {
                VtId = 0, BundleSerial = 0,
                OverloadedResources = SafetyMask128.Zero,
            };
            Assert.IsAssignableFrom<PipelineEvent>(evt);
        }

        [Fact]
        public void BackpressureEvent_CarriesOverloadedResourceMask()
        {
            var mask = new SafetyMask128(0x0F00_0000_0000_0000UL, 0UL);
            var evt = new BackpressureEvent
            {
                VtId = 1, BundleSerial = 10,
                OverloadedResources = mask,
                SuppressedSlotCount = 3,
            };
            Assert.Equal(mask, evt.OverloadedResources);
            Assert.Equal(3, evt.SuppressedSlotCount);
        }

        [Fact]
        public void BackpressureEvent_DoesNotTransition_FsmState()
        {
            var evt = new BackpressureEvent
            {
                VtId = 0, BundleSerial = 0,
                OverloadedResources = new SafetyMask128(0xFFUL, 0UL),
            };
            var result = PipelineFsmGuard.Advance(PipelineState.Task, evt);
            Assert.Equal(PipelineState.Task, result);
        }

        [Fact]
        public void BackpressureEvent_IsNot_FenceEvent()
            => Assert.IsNotType<FenceEvent>(
                new BackpressureEvent { VtId = 0, BundleSerial = 0,
                    OverloadedResources = SafetyMask128.Zero } as PipelineEvent);

        [Fact]
        public void BackpressureEvent_IsNot_TrapEntryEvent()
        {
            PipelineEvent evt = new BackpressureEvent
            {
                VtId = 0, BundleSerial = 0,
                OverloadedResources = SafetyMask128.Zero,
            };
            Assert.False(evt is TrapEntryEvent);
        }
    }
}
