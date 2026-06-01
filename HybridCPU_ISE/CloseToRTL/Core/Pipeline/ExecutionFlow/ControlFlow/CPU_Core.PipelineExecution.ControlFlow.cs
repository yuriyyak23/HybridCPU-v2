using HybridCPU_ISE.Arch;
using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyControlFlowDominanceBeforeRedirect()
            {
                Span<byte> controlFlowOrder = stackalloc byte[4];
                int controlFlowOrderCount = ResolveStableControlFlowOrder(pipeEX, controlFlowOrder);
                if (controlFlowOrderCount == 0)
                    return;

                pipeEX.ActiveLaneIndex = controlFlowOrder[0];

                byte dominatedMask = ResolveDominatedScalarLanesForControlFlow(pipeEX);
                if (dominatedMask == 0)
                    return;

                SuppressDominatedExecuteScalarLanes(dominatedMask);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ApplyPipelineControlFlowRedirect(
                ulong targetPc,
                Core.AssistInvalidationReason assistInvalidationReason = Core.AssistInvalidationReason.PipelineFlush)
            {
                ApplyControlFlowDominanceBeforeRedirect();
                FlushPipeline(assistInvalidationReason);
                RedirectActiveExecutionForControlFlow(targetPc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SuppressDominatedExecuteScalarLanes(byte dominatedMask)
            {
                if ((dominatedMask & (1 << 0)) != 0)
                {
                    ScalarExecuteLaneState clearedLane = new();
                    clearedLane.Clear(0);
                    pipeEX.SetLane(0, clearedLane);
                }

                if ((dominatedMask & (1 << 1)) != 0)
                {
                    ScalarExecuteLaneState clearedLane = new();
                    clearedLane.Clear(1);
                    pipeEX.SetLane(1, clearedLane);
                }

                if ((dominatedMask & (1 << 2)) != 0)
                {
                    ScalarExecuteLaneState clearedLane = new();
                    clearedLane.Clear(2);
                    pipeEX.SetLane(2, clearedLane);
                }

                if ((dominatedMask & (1 << 3)) != 0)
                {
                    ScalarExecuteLaneState clearedLane = new();
                    clearedLane.Clear(3);
                    pipeEX.SetLane(3, clearedLane);
                }

                pipeEX.MaterializedScalarLaneCount = CountOccupiedScalarExecuteLanes(pipeEX);
                pipeEX.MaterializedPhysicalLaneCount = CountOccupiedPhysicalExecuteLanes(pipeEX);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsControlFlowOpcode(uint opCode)
            {
                return Arch.OpcodeRegistry.IsControlFlowOp(opCode);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.BranchExecutionPayload ResolveBranchExecutionPayload(
                in ScalarExecuteLaneState branchLane,
                Core.BranchMicroOp branchOp)
            {
                int consumerThreadId = NormalizePipelineStateVtId(branchLane.OwnerThreadId);
                ulong operand1 = 0;
                ulong operand2 = 0;
                ulong baseValue = 0;

                if (branchOp.IsConditional)
                {
                    operand1 = GetRegisterValueWithForwarding(consumerThreadId, branchOp.Reg1ID);
                    operand2 = GetRegisterValueWithForwarding(consumerThreadId, branchOp.Reg2ID);
                }
                else if (branchLane.OpCode == IsaOpcodeValues.JALR)
                {
                    baseValue = GetRegisterValueWithForwarding(consumerThreadId, branchOp.Reg1ID);
                }

                return branchOp.ResolveExecutionPayload(
                    unchecked((ushort)branchLane.OpCode),
                    branchLane.PC,
                    operand1,
                    operand2,
                    baseValue);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void MaterializeBranchExecuteCarrier(
                ref ScalarExecuteLaneState branchLane,
                in Core.BranchExecutionPayload branchPayload)
            {
                branchLane.IsMemoryOp = false;
                branchLane.IsLoad = false;
                branchLane.MemoryAccessSize = 0;
                branchLane.MemoryAddress = 0;
                branchLane.MemoryData = 0;
                branchLane.GeneratedEvent = null;
                branchLane.GeneratedCsrEffect = null;
                branchLane.GeneratedAtomicEffect = null;
                branchLane.GeneratedVmxEffect = null;
                branchLane.GeneratedRetireRecordCount = 0;
                branchLane.GeneratedRetireRecord0 = default;
                branchLane.GeneratedRetireRecord1 = default;
                branchLane.VectorComplete = true;
                branchLane.ResultReady = true;
                branchLane.ResultValue = branchPayload.MaterializedResultValue;
            }

            /// <summary>
            /// Execute a scalar <see cref="Core.BranchMicroOp"/> on the single-lane path
            /// without falling through the placeholder <see cref="Core.MicroOp.Execute"/>.
            /// Branch execute now only resolves condition/target/link metadata; authoritative
            /// control-flow redirect is published later by the WB retire window through the
            /// branch MicroOp retire records.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteScalarBranchMicroOp()
            {
                byte laneIndex = pipeEX.ActiveLaneIndex;
                ScalarExecuteLaneState branchLane = pipeEX.GetLane(laneIndex);
                if (!branchLane.IsOccupied ||
                    branchLane.MicroOp is not Core.BranchMicroOp branchOp)
                    return false;

                Core.BranchExecutionPayload branchPayload =
                    ResolveBranchExecutionPayload(branchLane, branchOp);
                if (!branchPayload.IsExecutable)
                    return false;

                MaterializeBranchExecuteCarrier(ref branchLane, branchPayload);
                pipeEX.SetLane(laneIndex, branchLane);
                pipeEX.Valid = true;
                PublishExecuteCompletionContourCertificate(
                    Core.PipelineContourOwner.SingleLaneMicroOpExecution,
                    Core.PipelineContourVisibilityStage.Execute,
                    branchLane.PC,
                    (byte)(1 << laneIndex));
                ConsumeDecodeStateAfterExecuteDispatch();
                PublishSingleLaneExecuteForwarding(includeTimingMetadata: true);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryExecuteExplicitPacketLane7Branch(
                byte laneIndex,
                ref ScalarExecuteLaneState branchLane,
                ref int executedPhysicalLaneCount)
            {
                if (laneIndex != 7 ||
                    !branchLane.IsOccupied ||
                    branchLane.MicroOp is not Core.BranchMicroOp branchOp)
                {
                    return false;
                }

                pipeEX.ActiveLaneIndex = 7;

                Core.BranchExecutionPayload branchPayload =
                    ResolveBranchExecutionPayload(branchLane, branchOp);
                if (!branchPayload.IsExecutable)
                {
                    return false;
                }

                MaterializeBranchExecuteCarrier(ref branchLane, branchPayload);
                pipeEX.SetLane(laneIndex, branchLane);
                executedPhysicalLaneCount++;

                RecordExecuteLaneTraceEvent(laneIndex, branchLane);
                if (branchOp.IsConditional)
                {
                    RecordLane7ConditionalBranchCompletion(
                        redirected: branchPayload.RedirectsControlFlow);
                }

                return true;
            }
        }
    }
}
