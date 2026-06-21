using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using YAKSys_Hybrid_CPU.Core.ControlFlow;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            private const ulong VliwBundleBytes = 256;
            private const ulong VliwSlotBytes = 32;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void RefreshControlFlowRedirectWindow(ulong memoryAddress)
            {
                ulong bundleBase = memoryAddress - (memoryAddress % VliwBundleBytes);
                ulong bundleEnd = bundleBase + VliwBundleBytes;

                CoreFlagsRegister.Jump_Flag = true;
                CoreFlagsRegister.JumpOutsideVLIW_Flag =
                    memoryAddress < CoreFlagsRegister.VLIW_Begin_MemPtr ||
                    memoryAddress >= CoreFlagsRegister.VLIW_End_MemPtr;
                CoreFlagsRegister.VLIW_Begin_MemPtr = bundleBase;
                CoreFlagsRegister.VLIW_End_MemPtr = bundleEnd;
                CoreFlagsRegister.Offset_VLIWOpCode = (byte)((memoryAddress - bundleBase) / VliwSlotBytes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void RedirectActiveExecutionForControlFlow(ulong memoryAddress, bool prefetchVliwBundle = false)
            {
                WriteActiveLivePc(memoryAddress);
                RefreshControlFlowRedirectWindow(memoryAddress);

                if (prefetchVliwBundle)
                    PrefetchVLIWBundle(CoreFlagsRegister.VLIW_Begin_MemPtr);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void EnterExternalInterruptHandler(ulong handlerAddress)
            {
                Push_Interrupt_EntryPoint_Address();
                FlushPipeline(Core.AssistInvalidationReason.Trap);
                ApplyInterruptTransitionToVirtualThread(ReadActiveVirtualThreadId());
                RedirectActiveExecutionForControlFlow(handlerAddress, prefetchVliwBundle: !pipeCtrl.Enabled);
            }

            public void Push_Flag_Register()
            {
                Core_FlagsRegisters_Stack.Add(CoreFlagsRegister);
            }
            public void Pop_Flag_Register()
            {
                if (Core_FlagsRegisters_Stack.Count > 0)
                {
                    CoreFlagsRegister = Core_FlagsRegisters_Stack[Core_FlagsRegisters_Stack.Count - 1];

                    Core_FlagsRegisters_Stack.RemoveAt(Core_FlagsRegisters_Stack.Count - 1);
                }
            }

            public void Push_Call_EntryPoint_Address()
            {
                Call_Callback_Addresses.Add(ReadActiveLivePc() + 32);
            }
            public ulong Pop_Call_EntryPoint_Address()
            {
                if (Call_Callback_Addresses != null && Call_Callback_Addresses.Count > 0)
                {
                    ulong ulong_Call_EntryPoint_Address = Call_Callback_Addresses[Call_Callback_Addresses.Count - 1];

                    Call_Callback_Addresses.RemoveAt(Call_Callback_Addresses.Count - 1);

                    return ulong_Call_EntryPoint_Address;
                }

                throw new ControlFlowStackUnderflowException("Call");
            }

            public void Push_Interrupt_EntryPoint_Address()
            {
                Interrupt_Callback_Addresses.Add(ReadActiveLivePc() + 32);
            }
            public ulong Pop_Interrupt_EntryPoint_Address()
            {
                if (Interrupt_Callback_Addresses != null && Interrupt_Callback_Addresses.Count > 0)
                {
                    ulong ulong_Interrupt_EntryPoint_Address = Interrupt_Callback_Addresses[Interrupt_Callback_Addresses.Count - 1];

                    Interrupt_Callback_Addresses.RemoveAt(Interrupt_Callback_Addresses.Count - 1);

                    return ulong_Interrupt_EntryPoint_Address;
                }

                throw new ControlFlowStackUnderflowException("Interrupt");
            }

            public void ENTRY_POINT(ref Processor.EntryPoint EntryPoint)
            {
                if (EntryPoint.EntryPointAddressAlreadyDefined)
                {
                    throw new EntryPointDefinitionException(
                        nameof(ENTRY_POINT),
                        EntryPoint.EntryPoint_Address,
                        null,
                        "EntryPoint is already declared. Please use another EntryPoint descriptor.");
                }

                EntryPoint.Type = Processor.EntryPoint.EntryPointType.EntryPoint;

                Jump_Call_EntryPoint(ref EntryPoint, Processor.EntryPoint.EntryPointType.EntryPoint);
            }

            void Jump_Call_EntryPoint(ref Processor.EntryPoint EntryPoint, Processor.EntryPoint.EntryPointType EntryPoint_CurrentType)
            {
                if (IsCompilerExecutionMode())
                {
                    EntryPoint.Type = EntryPoint_CurrentType;

                    EntryPoint.AddAddress((ulong)MainMemory.Position);

                    PublishPendingEntryPointRelocations(ref EntryPoint);
                }
            }

            static void PublishPendingEntryPointRelocations(ref Processor.EntryPoint entryPoint)
            {
                if (!entryPoint.EntryPointAddressAlreadyDefined)
                {
                    return;
                }

                PublishPendingEntryPointRelocations(
                    ref entryPoint,
                    entryPoint.List_Jump_Addresses,
                    RelocationKind.AbsoluteJump);

                PublishPendingEntryPointRelocations(
                    ref entryPoint,
                    entryPoint.List_Call_Addresses,
                    RelocationKind.AbsoluteCall);

                PublishPendingEntryPointRelocations(
                    ref entryPoint,
                    entryPoint.List_Interrupt_Addresses,
                    RelocationKind.AbsoluteInterrupt);
            }

            static void PublishPendingEntryPointRelocations(
                ref Processor.EntryPoint entryPoint,
                List<ulong> pendingAddresses,
                RelocationKind kind)
            {
                if (pendingAddresses == null)
                {
                    return;
                }

                for (; pendingAddresses.Count != 0;)
                {
                    ulong emissionCursor = pendingAddresses[pendingAddresses.Count - 1];
                    entryPoint.AddRelocation(kind, emissionCursor);
                    pendingAddresses.RemoveAt(pendingAddresses.Count - 1);
                }
            }
        }
    }
}
