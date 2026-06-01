using System;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        /// <summary>
        /// Device identifiers for interrupt routing.
        /// </summary>
        public enum DeviceType : ushort
        {
            CPU = 0,
            VectorUnit = 1,
            StreamEngine = 2,
            DMAController = 3,
            IOPort = 4,
            Timer = 5,
            // Reserved for future devices
        }

        public struct InterruptData
        {
            public static IRQ_Address[] IRQ = new IRQ_Address[16384];

            /// <summary>
            /// IRQ address entry with handler address and priority.
            /// </summary>
            public struct IRQ_Address
            {
                ushort short_InterruptID;
                public ushort InterruptID
                {
                    get
                    {
                        return short_InterruptID;
                    }
                    set
                    {
                        short_InterruptID = value;
                    }
                }

                ulong ulong_MemoryAddress;
                public ulong MemoryAddress
                {
                    get
                    {
                        return ulong_MemoryAddress;
                    }
                    set
                    {
                        ulong_MemoryAddress = value;
                    }
                }

                byte byte_Priority;
                /// <summary>
                /// Handler priority level (0-7, where 7 is highest priority).
                /// </summary>
                public byte Priority
                {
                    get
                    {
                        return byte_Priority;
                    }
                    set
                    {
                        byte_Priority = value;
                    }
                }
            }

            /// <summary>
            /// Trigger an interrupt from a specific device.
            /// Looks up the interrupt handler address from the interrupt vector table
            /// and schedules the interrupt for processing by the CPU core.
            /// </summary>
            /// <param name="DeviceID">The device raising the interrupt</param>
            /// <param name="InterruptID">The specific interrupt ID (e.g., 0x80 for vector exceptions)</param>
            /// <param name="CoreID">The CPU core that should handle this interrupt</param>
            /// <returns>0 on success, error code otherwise</returns>
            public static byte CallInterrupt(DeviceType DeviceID, ushort InterruptID, ulong CoreID)
            {
                // Validate interrupt ID
                if (InterruptID >= IRQ.Length)
                {
                    return 0xFF; // Invalid interrupt ID
                }

                // Look up interrupt handler address from vector table
                ulong handlerAddress = IRQ[InterruptID].MemoryAddress;

                if (handlerAddress == 0)
                {
                    // No handler registered for this interrupt
                    return 0xFE; // No handler error
                }

                // Get the target CPU core
                if (CoreID >= (ulong)CPU_Cores.Length)
                {
                    return 0xFD; // Invalid core ID
                }

                ref CPU_Core targetCore = ref CPU_Cores[CoreID];

                // Route through the external-interrupt contour unless the core
                // already has a live interrupt-handler frame in flight.
                if (!targetCore.HasActiveInterruptHandlerFrame())
                {
                    targetCore.EnterExternalInterruptHandler(handlerAddress);

                    // In a real system, additional state would be saved here:
                    // - Processor status flags
                    // - Exception cause register
                    // - Exception return address
                }

                return 0; // Success
            }

            /// <summary>
            /// Register an interrupt handler in the interrupt vector table.
            /// </summary>
            /// <param name="interruptID">The interrupt ID (0-16383)</param>
            /// <param name="handlerAddress">The address of the handler function</param>
            /// <param name="priority">Handler priority (0-7, where 7 is highest)</param>
            /// <returns>True if registered successfully, false if invalid parameters</returns>
            public static bool RegisterHandler(ushort interruptID, ulong handlerAddress, byte priority = 5)
            {
                // Validate interrupt ID
                if (interruptID >= IRQ.Length)
                {
                    return false;
                }

                // Validate priority (0-7)
                if (priority > 7)
                {
                    return false;
                }

                // Register handler in IRQ table
                IRQ[interruptID].InterruptID = interruptID;
                IRQ[interruptID].MemoryAddress = handlerAddress;
                IRQ[interruptID].Priority = priority;

                return true;
            }

            /// <summary>
            /// Unregister an interrupt handler from the interrupt vector table.
            /// </summary>
            /// <param name="interruptID">The interrupt ID to unregister</param>
            /// <returns>True if unregistered successfully, false if invalid ID</returns>
            public static bool UnregisterHandler(ushort interruptID)
            {
                // Validate interrupt ID
                if (interruptID >= IRQ.Length)
                {
                    return false;
                }

                // Clear handler entry
                IRQ[interruptID].MemoryAddress = 0;
                IRQ[interruptID].Priority = 0;

                return true;
            }

            /// <summary>
            /// Get the handler address for a specific interrupt ID.
            /// </summary>
            /// <param name="interruptID">The interrupt ID</param>
            /// <param name="handlerAddress">Output: handler address (0 if not registered)</param>
            /// <param name="priority">Output: handler priority</param>
            /// <returns>True if handler exists, false otherwise</returns>
            public static bool GetHandler(ushort interruptID, out ulong handlerAddress, out byte priority)
            {
                handlerAddress = 0;
                priority = 0;

                if (interruptID >= IRQ.Length)
                {
                    return false;
                }

                handlerAddress = IRQ[interruptID].MemoryAddress;
                priority = IRQ[interruptID].Priority;

                return handlerAddress != 0;
            }

            /// <summary>
            /// Default handler for vector exceptions (IRQ 0x80).
            /// Logs exception status and clears exception counters.
            /// </summary>
            /// <param name="coreID">The CPU core that raised the exception</param>
            public static void DefaultVectorExceptionHandler(ulong coreID)
            {
                if (coreID >= (ulong)CPU_Cores.Length)
                {
                    return;
                }

                ref CPU_Core core = ref CPU_Cores[coreID];
                ref CPU_Core.VectorExceptionStatus status = ref core.ExceptionStatus;

                // Log exception information (in real hardware, this might write to a trace buffer)
                // For emulation, we track the exception occurred
                bool hasExceptions = false;

                if (status.OverflowCount > 0)
                {
                    hasExceptions = true;
                    // In a real implementation: Console.WriteLine($"Core {coreID}: Overflow exceptions: {status.OverflowCount}");
                }

                if (status.UnderflowCount > 0)
                {
                    hasExceptions = true;
                    // Console.WriteLine($"Core {coreID}: Underflow exceptions: {status.UnderflowCount}");
                }

                if (status.DivByZeroCount > 0)
                {
                    hasExceptions = true;
                    // Console.WriteLine($"Core {coreID}: Divide-by-zero exceptions: {status.DivByZeroCount}");
                }

                if (status.InvalidOpCount > 0)
                {
                    hasExceptions = true;
                    // Console.WriteLine($"Core {coreID}: Invalid operation exceptions: {status.InvalidOpCount}");
                }

                if (status.InexactCount > 0)
                {
                    hasExceptions = true;
                    // Console.WriteLine($"Core {coreID}: Inexact result exceptions: {status.InexactCount}");
                }

                // Clear exception counters after handling
                status.ClearCounters();

                // Return from the default handler contour by releasing one saved
                // interrupt frame and restoring the active VT's runnable state.
                if (core.HasActiveInterruptHandlerFrame())
                {
                    _ = core.Pop_Interrupt_EntryPoint_Address();
                }

                core.ApplyInterruptTransitionToVirtualThread(core.ReadActiveVirtualThreadId());
            }

            /// <summary>
            /// Initialize the interrupt controller with default handlers.
            /// Should be called during system initialization.
            /// </summary>
            public static void Initialize()
            {
                // Clear all IRQ entries
                for (int i = 0; i < IRQ.Length; i++)
                {
                    IRQ[i].InterruptID = (ushort)i;
                    IRQ[i].MemoryAddress = 0;
                    IRQ[i].Priority = 0;
                }

                // Register default vector exception handler at IRQ 0x80
                // In a real system, this would be the address of DefaultVectorExceptionHandler
                // For simulation, we use a symbolic address (0x1000)
                RegisterHandler(0x80, 0x1000, priority: 7); // Highest priority for exceptions
            }
        }
    }
}
