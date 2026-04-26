using System;
using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CanRouteDecodedSlotToConfiguredExecutionSurface(
                in Core.DecodedBundleSlotDescriptor slotDescriptor)
            {
                if (!slotDescriptor.IsValid || slotDescriptor.GetRuntimeExecutionIsEmptyOrNop())
                {
                    return true;
                }

                uint opCode = slotDescriptor.GetRuntimeExecutionOpCode();
                if (IsUnsupportedMainlineStreamControlSurface(opCode))
                {
                    return false;
                }

                return slotDescriptor.MicroOp is not Core.VmxMicroOp || HasWiredVmxExecutionPlane;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void EnforceDecodedSlotExecutionSurfaceContract(
                in Core.DecodedBundleSlotDescriptor slotDescriptor,
                ulong bundlePc)
            {
                if (CanRouteDecodedSlotToConfiguredExecutionSurface(slotDescriptor))
                {
                    return;
                }

                uint opCode = slotDescriptor.GetRuntimeExecutionOpCode();
                if (IsUnsupportedMainlineStreamControlSurface(opCode))
                {
                    FlushPipeline(Core.AssistInvalidationReason.Trap);
                    throw Core.UnsupportedExecutionSurfaceException.CreateForStreamControl(
                        slotDescriptor.SlotIndex,
                        opCode,
                        bundlePc);
                }

                throw CreateUnwiredVmxDecodeSurfaceException(
                    slotDescriptor.SlotIndex,
                    opCode,
                    bundlePc);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Core.IssuePacketLane ApplyIssueLaneExecutionSurfaceContract(
                Core.IssuePacketLane issueLane,
                ulong bundlePc)
            {
                if (!issueLane.IsOccupied)
                {
                    return issueLane;
                }

                uint opCode = ResolveIssueLaneExecutionSurfaceOpCode(issueLane);
                if (IsUnsupportedMainlineStreamControlSurface(opCode))
                {
                    FlushPipeline(Core.AssistInvalidationReason.Trap);
                    throw Core.UnsupportedExecutionSurfaceException.CreateForStreamControl(
                        issueLane.SlotIndex,
                        opCode,
                        bundlePc);
                }

                if (issueLane.MicroOp is Core.VmxMicroOp && !HasWiredVmxExecutionPlane)
                {
                    throw CreateUnwiredVmxIssuePacketSurfaceException(
                        issueLane.SlotIndex,
                        ResolveIssueLaneExecutionSurfaceOpCode(issueLane),
                        bundlePc);
                }

                return issueLane;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveIssueLaneExecutionSurfaceOpCode(
                in Core.IssuePacketLane issueLane)
            {
                return issueLane.OpCode;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsUnsupportedMainlineStreamControlSurface(uint opCode)
            {
                return opCode is
                    IsaOpcodeValues.STREAM_SETUP or
                    IsaOpcodeValues.STREAM_START;
            }

            private static InvalidOperationException CreateUnwiredVmxDecodeSurfaceException(
                byte slotIndex,
                uint opCode,
                ulong bundlePc)
            {
                return new InvalidOperationException(
                    $"VMX slot {slotIndex} (opcode 0x{opCode:X}) at PC 0x{bundlePc:X} reached decode/admission without a wired VMX execution plane. " +
                    "Admission/routing must reject or defer VMX before execute.");
            }

            private static InvalidOperationException CreateUnwiredVmxIssuePacketSurfaceException(
                byte slotIndex,
                uint opCode,
                ulong bundlePc)
            {
                return new InvalidOperationException(
                    $"VMX slot {slotIndex} (opcode 0x{opCode:X}) at PC 0x{bundlePc:X} reached issue-packet execute materialization without a wired VMX execution plane. " +
                    "Issue-packet routing must reject live VMX lanes explicitly instead of dropping them before execute.");
            }

            internal bool CanRouteDecodedSlotToExecutionSurfaceForTesting(
                in Core.DecodedBundleSlotDescriptor slotDescriptor)
            {
                return CanRouteDecodedSlotToConfiguredExecutionSurface(slotDescriptor);
            }

            internal void EnforceDecodedSlotExecutionSurfaceContractForTesting(
                in Core.DecodedBundleSlotDescriptor slotDescriptor,
                ulong bundlePc)
            {
                EnforceDecodedSlotExecutionSurfaceContract(slotDescriptor, bundlePc);
            }

            internal void EnforceIssueLaneExecutionSurfaceContractForTesting(
                in Core.IssuePacketLane issueLane,
                ulong bundlePc)
            {
                _ = ApplyIssueLaneExecutionSurfaceContract(issueLane, bundlePc);
            }
        }
    }
}
