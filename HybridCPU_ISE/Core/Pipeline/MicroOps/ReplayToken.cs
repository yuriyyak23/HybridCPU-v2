using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Core
{
    /// <summary>
    /// Replay token for evidence-bounded execution reproduction and rollback support.
    /// </summary>
    public class ReplayToken
    {
        private YAKSys_Hybrid_CPU.Processor.MainMemoryArea? _boundMainMemory;

        // === Original replay fields ===
        public ulong RandomSeed { get; set; }
        public uint VL { get; set; }
        public uint VLMAX { get; set; }
        public byte VSEW { get; set; }
        public byte LMUL { get; set; }
        public bool TailAgnostic { get; set; }
        public bool MaskAgnostic { get; set; }
        public byte RoundingMode { get; set; }
        public byte ExceptionMode { get; set; }
        public ulong MemorySize { get; set; }
        public string TraceHash { get; set; }
        public long Timestamp { get; set; }

        // === Replay-envelope rollback support fields ===

        /// <summary>
        /// Pre-execution register state for rollback.
        /// Maps register ID to register value before operation execution.
        /// </summary>
        public Dictionary<int, ulong> PreExecutionRegisterState { get; set; }

        /// <summary>
        /// Pre-execution memory state for rollback.
        /// Stores (Address, Data) tuples of memory contents before writes.
        /// </summary>
        public List<(ulong Address, byte[] Data)> PreExecutionMemoryState { get; set; }

        /// <summary>
        /// Owner thread ID for this micro-operation.
        /// Used to restore correct thread context on rollback.
        /// </summary>
        public int OwnerThreadId { get; set; }

        /// <summary>
        /// Does this operation have side effects requiring rollback support?
        /// True for: memory writes, CSR writes, I/O operations
        /// False for: pure ALU operations, loads (non-destructive)
        /// </summary>
        public bool HasSideEffects { get; set; }

        /// <summary>
        /// Parameterless constructor for JSON deserialization and config-only usage.
        /// The token remains unbound to any memory surface until an explicit binding
        /// is supplied via <see cref="ReplayToken(YAKSys_Hybrid_CPU.Processor.MainMemoryArea?)"/>
        /// or <see cref="BindMainMemory(YAKSys_Hybrid_CPU.Processor.MainMemoryArea)"/>.
        /// </summary>
        [JsonConstructor]
        public ReplayToken()
        {
            RandomSeed = 0;
            VL = 0;
            VLMAX = 32;
            VSEW = 32;
            LMUL = 1;
            TailAgnostic = false;
            MaskAgnostic = false;
            RoundingMode = 0;
            ExceptionMode = 0;
            MemorySize = 0;
            TraceHash = "";
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Initialize rollback fields
            PreExecutionRegisterState = new Dictionary<int, ulong>();
            PreExecutionMemoryState = new List<(ulong, byte[])>();
            OwnerThreadId = 0;
            HasSideEffects = false;
        }

        /// <summary>
        /// Construct a replay token with an explicit main-memory binding.
        /// </summary>
        /// <param name="mainMemory">Optional main-memory instance. When null,
        /// the token remains unbound and any replay-side memory capture/restore must fail closed
        /// until an explicit binding is supplied.</param>
        public ReplayToken(YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory)
            : this()
        {
            if (mainMemory != null)
            {
                BindMainMemory(mainMemory);
            }
        }

        public ReplayToken BindMainMemory(YAKSys_Hybrid_CPU.Processor.MainMemoryArea mainMemory)
        {
            ArgumentNullException.ThrowIfNull(mainMemory);
            _boundMainMemory = mainMemory;
            return this;
        }

        private bool HasFullyBoundRollbackMemoryState()
        {
            if (PreExecutionMemoryState == null || PreExecutionMemoryState.Count == 0)
            {
                return false;
            }

            if (_boundMainMemory == null)
            {
                return false;
            }

            foreach ((ulong address, byte[] data) in PreExecutionMemoryState)
            {
                if (data == null || data.Length == 0)
                {
                    return false;
                }

                if (!HasExactMainMemoryRange(_boundMainMemory, address, data.Length))
                {
                    return false;
                }
            }

            return true;
        }

        private YAKSys_Hybrid_CPU.Processor.MainMemoryArea GetRequiredMainMemory(string operation)
        {
            if (_boundMainMemory != null)
            {
                return _boundMainMemory;
            }

            throw new MainMemoryBindingUnavailableException(nameof(ReplayToken), operation);
        }

        private static bool HasExactMainMemoryRange(
            YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory,
            ulong address,
            int size)
        {
            if (size <= 0 || mainMemory == null)
            {
                return false;
            }

            ulong memoryLength = (ulong)mainMemory.Length;
            ulong requestSize = (ulong)size;
            return requestSize <= memoryLength &&
                   address <= memoryLength - requestSize;
        }

        private static void ThrowIfMainMemoryRangeUnavailable(
            YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory,
            ulong address,
            int size,
            string executionSurface)
        {
            if (HasExactMainMemoryRange(mainMemory, address, size))
            {
                return;
            }

            throw new InvalidOperationException(
                $"{executionSurface} reached replay-side MainMemory capture/restore at 0x{address:X} for {size} byte(s) without a fully materializable in-range surface. " +
                "Replay/rollback must fail closed instead of snapshotting or restoring a partial memory image across the boundary contour.");
        }

        private static void ReadMainMemoryExact(
            YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory,
            ulong address,
            byte[] buffer,
            string executionSurface)
        {
            ThrowIfMainMemoryRangeUnavailable(mainMemory, address, buffer.Length, executionSurface);

            if (!mainMemory!.TryReadPhysicalRange(address, buffer))
            {
                throw new InvalidOperationException(
                    $"{executionSurface} reached replay-side MainMemory capture at 0x{address:X} for {buffer.Length} byte(s), but the bound memory surface did not materialize the full snapshot. " +
                    "Replay/rollback must fail closed instead of storing a zero-filled or partial snapshot.");
            }
        }

        private static void WriteMainMemoryExact(
            YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory,
            ulong address,
            byte[] buffer,
            string executionSurface)
        {
            ThrowIfMainMemoryRangeUnavailable(mainMemory, address, buffer.Length, executionSurface);

            if (!mainMemory!.TryWritePhysicalRange(address, buffer))
            {
                throw new InvalidOperationException(
                    $"{executionSurface} reached replay-side MainMemory restore at 0x{address:X} for {buffer.Length} byte(s), but the bound memory surface did not materialize the full restore. " +
                    "Replay/rollback must fail closed instead of reporting a partial memory image as a completed restore.");
            }
        }

        /// <summary>
        /// Capture register state for potential rollback.
        /// Stores current values of specified registers.
        /// </summary>
        public void CaptureRegisterState(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core, int[] registerIds)
        {
            if (registerIds == null || registerIds.Length == 0)
                return;

            PreExecutionRegisterState.Clear();
            int vtId = ResolveOwnerThreadId(ref core);

            foreach (int regId in registerIds)
            {
                if (regId >= 0 && regId < YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
                {
                    PreExecutionRegisterState[regId] = core.ReadArch(vtId, regId);
                }
            }
        }

        /// <summary>
        /// Capture memory state for potential rollback.
        /// Reads and stores current memory contents at specified address.
        /// </summary>
        public void CaptureMemoryState(ulong address, int size)
        {
            if (size <= 0)
                return;

            YAKSys_Hybrid_CPU.Processor.MainMemoryArea mainMemory = GetRequiredMainMemory("ReplayToken.CaptureMemoryState()");

            // Read current memory contents
            byte[] data = new byte[size];
            ReadMainMemoryExact(mainMemory, address, data, "ReplayToken.CaptureMemoryState()");
            PreExecutionMemoryState.Add((address, data));
        }

        /// <summary>
        /// Rollback the operation by restoring pre-execution state.
        /// Restores register values and memory contents.
        /// </summary>
        public void Rollback(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core)
        {
            int vtId = ResolveOwnerThreadId(ref core);
            YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory =
                PreExecutionMemoryState.Count > 0 ? GetRequiredMainMemory("ReplayToken.Rollback()") : null;

            // Restore registers
            foreach (var kvp in PreExecutionRegisterState)
            {
                int regId = kvp.Key;
                ulong value = kvp.Value;

                if (regId >= 0 && regId < YAKSys_Hybrid_CPU.Core.Registers.RenameMap.ArchRegs)
                {
                    core.RetireCoordinator.Retire(RetireRecord.RegisterWrite(vtId, regId, value));
                }
            }

            // Restore memory
            foreach (var (address, data) in PreExecutionMemoryState)
            {
                WriteMainMemoryExact(mainMemory, address, data, "ReplayToken.Rollback()");
            }
        }

        /// <summary>
        /// Check whether the token currently carries enough fully bound state for rollback.
        /// Returns false when replay-side memory restore would cross an unbound or partial contour.
        /// </summary>
        public bool CanSafelyRollback()
        {
            if (!HasSideEffects)
                return true;

            bool hasRegistersToRestore = PreExecutionRegisterState != null && PreExecutionRegisterState.Count > 0;
            bool hasCapturedMemoryState = PreExecutionMemoryState != null && PreExecutionMemoryState.Count > 0;
            bool hasMemoryToRestore = HasFullyBoundRollbackMemoryState();

            if (hasCapturedMemoryState && !hasMemoryToRestore)
            {
                return false;
            }

            return hasRegistersToRestore || hasMemoryToRestore;
        }

        private int ResolveOwnerThreadId(ref YAKSys_Hybrid_CPU.Processor.CPU_Core core)
        {
            if ((uint)OwnerThreadId < (uint)YAKSys_Hybrid_CPU.Processor.CPU_Core.SmtWays)
                return OwnerThreadId;

            return core.ReadActiveVirtualThreadId();
        }

        /// <summary>
        /// Serialize token to JSON string
        /// </summary>
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(this, options);
        }

        /// <summary>
        /// Deserialize token from JSON string.
        /// </summary>
        /// <param name="json">Serialized replay token.</param>
        /// <param name="mainMemory">Optional explicit main-memory instance. When null,
        /// the deserialized token remains unbound until a caller provides a binding.</param>
        public static ReplayToken FromJson(string json, YAKSys_Hybrid_CPU.Processor.MainMemoryArea? mainMemory = null)
        {
            ReplayToken? token = JsonSerializer.Deserialize<ReplayToken>(json);
            if (token == null)
            {
                return null;
            }

            if (mainMemory != null)
            {
                token.BindMainMemory(mainMemory);
            }

            return token;
        }

        /// <summary>
        /// Compute hash from trace events
        /// </summary>
        public static string ComputeTraceHash(System.Collections.Generic.IReadOnlyList<TraceEvent> events)
        {
            if (events == null || events.Count == 0)
                return "";

            using var sha256 = SHA256.Create();
            var builder = new StringBuilder();

            foreach (var evt in events)
            {
                builder.Append(evt.PC);
                builder.Append('|');
                builder.Append(evt.BundleId);
                builder.Append('|');
                builder.Append(evt.OpIndex);
                builder.Append('|');
                builder.Append((int)evt.Opcode);
                builder.Append('|');
                builder.Append(evt.ExceptionCount);
                builder.Append('\n');
            }

            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Create token from current processor configuration
        /// </summary>
        public static ReplayToken CreateFromConfig(ulong seed, uint vl, uint vlmax, byte vsew, byte lmul,
            bool tailAgnostic, bool maskAgnostic, byte roundingMode, byte exceptionMode, ulong memorySize)
        {
            return new ReplayToken
            {
                RandomSeed = seed,
                VL = vl,
                VLMAX = vlmax,
                VSEW = vsew,
                LMUL = lmul,
                TailAgnostic = tailAgnostic,
                MaskAgnostic = maskAgnostic,
                RoundingMode = roundingMode,
                ExceptionMode = exceptionMode,
                MemorySize = memorySize,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>
        /// Validate if two tokens match (excluding hash and timestamp)
        /// </summary>
        public bool MatchesConfig(ReplayToken other)
        {
            if (other == null)
                return false;

            return RandomSeed == other.RandomSeed &&
                   VL == other.VL &&
                   VLMAX == other.VLMAX &&
                   VSEW == other.VSEW &&
                   LMUL == other.LMUL &&
                   TailAgnostic == other.TailAgnostic &&
                   MaskAgnostic == other.MaskAgnostic &&
                   RoundingMode == other.RoundingMode &&
                   ExceptionMode == other.ExceptionMode &&
                   MemorySize == other.MemorySize;
        }
    }
}
