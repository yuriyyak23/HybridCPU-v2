using System;
using HybridCPU_ISE.Core;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        /// <summary>
        /// TraceSink for recording execution events
        /// </summary>
        public static TraceSink? TraceSink;

        /// <summary>
        /// Instruction coverage tracker
        /// </summary>
        public static InstructionCoverage? Coverage;

        /// <summary>
        /// Random seed for deterministic execution
        /// </summary>
        public static ulong RandomSeed = 0;

        /// <summary>
        /// Current replay token
        /// </summary>
        private static ReplayToken? currentReplayToken;

        private static ulong GetCurrentReplayMemorySize() => (ulong)MainMemory.Length;

        private static byte DecodeReplayTokenSew(ulong vtype)
        {
            return ((vtype >> 3) & 0x7) switch
            {
                0 => 8,
                1 => 16,
                2 => 32,
                3 => 64,
                ulong invalidEncoding => throw new InvalidOperationException(
                    $"GetReplayToken() reached replay-token capture with unsupported VTYPE SEW encoding {invalidEncoding}. " +
                    "Replay token publication must fail closed instead of serializing a lossy or phantom vector-width snapshot.")
            };
        }

        private static byte DecodeReplayTokenLmul(ulong vtype)
        {
            return (vtype & 0x7) switch
            {
                0 => 1,
                1 => 2,
                2 => 4,
                3 => 8,
                5 => throw new InvalidOperationException(
                    "GetReplayToken() reached replay-token capture with fractional LMUL = 1/8. " +
                    "This verification token only serializes representable LMUL values {1, 2, 4, 8} and must fail closed instead of publishing a widened or lossy replay snapshot."),
                6 => throw new InvalidOperationException(
                    "GetReplayToken() reached replay-token capture with fractional LMUL = 1/4. " +
                    "This verification token only serializes representable LMUL values {1, 2, 4, 8} and must fail closed instead of publishing a widened or lossy replay snapshot."),
                7 => throw new InvalidOperationException(
                    "GetReplayToken() reached replay-token capture with fractional LMUL = 1/2. " +
                    "This verification token only serializes representable LMUL values {1, 2, 4, 8} and must fail closed instead of publishing a widened or lossy replay snapshot."),
                ulong invalidEncoding => throw new InvalidOperationException(
                    $"GetReplayToken() reached replay-token capture with unsupported LMUL encoding {invalidEncoding}. " +
                    "Replay token publication must fail closed instead of serializing phantom vector-grouping truth.")
            };
        }

        private static byte EncodeReplayTokenSew(byte vsew)
        {
            return vsew switch
            {
                8 => 0,
                16 => 1,
                32 => 2,
                64 => 3,
                _ => throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with unsupported VSEW = {vsew}. " +
                    "Replay restore must fail closed instead of synthesizing a non-representable vector-width encoding.")
            };
        }

        private static byte EncodeReplayTokenLmul(byte lmul)
        {
            return lmul switch
            {
                1 => 0,
                2 => 1,
                4 => 2,
                8 => 3,
                _ => throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with unsupported LMUL = {lmul}. " +
                    "Replay restore must fail closed instead of synthesizing a widened or non-representable vector-grouping encoding.")
            };
        }

        private static ulong ComposeReplayTokenVType(ReplayToken token)
        {
            ulong vtype = EncodeReplayTokenLmul(token.LMUL);
            vtype |= (ulong)EncodeReplayTokenSew(token.VSEW) << 3;

            if (token.TailAgnostic)
            {
                vtype |= 1UL << 6;
            }

            if (token.MaskAgnostic)
            {
                vtype |= 1UL << 7;
            }

            return vtype;
        }

        private static bool HasReplayVerificationCore() =>
            Ready_Flag && CPU_Cores != null && CPU_Cores.Length > 0;

        private static ReplayToken CreateReplayTokenFromLiveState()
        {
            if (!HasReplayVerificationCore())
            {
                return ReplayToken.CreateFromConfig(
                    RandomSeed,
                    (uint)CPU_Core.RVV_Config.VLMAX,
                    (uint)CPU_Core.RVV_Config.VLMAX,
                    32,
                    1,
                    false,
                    false,
                    0,
                    0,
                    GetCurrentReplayMemorySize());
            }

            ref CPU_Core core = ref CPU_Cores[0];
            return ReplayToken.CreateFromConfig(
                RandomSeed,
                checked((uint)core.VectorConfig.VL),
                (uint)CPU_Core.RVV_Config.VLMAX,
                DecodeReplayTokenSew(core.VectorConfig.VTYPE),
                DecodeReplayTokenLmul(core.VectorConfig.VTYPE),
                core.VectorConfig.TailAgnostic != 0,
                core.VectorConfig.MaskAgnostic != 0,
                core.ExceptionStatus.RoundingMode,
                core.ExceptionStatus.ExceptionMode,
                GetCurrentReplayMemorySize());
        }

        private static void ApplyReplayTokenToLiveVerificationCore(ReplayToken token)
        {
            if (!HasReplayVerificationCore())
            {
                return;
            }

            if (token.VLMAX != CPU_Core.RVV_Config.VLMAX)
            {
                throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with token VLMAX = {token.VLMAX}, but the current runtime exposes VLMAX = {CPU_Core.RVV_Config.VLMAX}. " +
                    "Replay restore must fail closed instead of silently widening or truncating the vector-hardware boundary.");
            }

            if (token.VL > token.VLMAX)
            {
                throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with VL = {token.VL} above VLMAX = {token.VLMAX}. " +
                    "Replay restore must fail closed instead of publishing an impossible vector-length state.");
            }

            ulong currentMemorySize = GetCurrentReplayMemorySize();
            if (token.MemorySize != currentMemorySize)
            {
                throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with memorySize = {token.MemorySize}, but the current runtime MainMemory.Length is {currentMemorySize}. " +
                    "Replay restore must fail closed instead of silently crossing an incompatible memory boundary.");
            }

            ref CPU_Core core = ref CPU_Cores[0];
            core.VectorConfig.VL = token.VL;
            core.VectorConfig.VTYPE = ComposeReplayTokenVType(token);
            core.VectorConfig.TailAgnostic = token.TailAgnostic ? (byte)1 : (byte)0;
            core.VectorConfig.MaskAgnostic = token.MaskAgnostic ? (byte)1 : (byte)0;

            if (!core.ExceptionStatus.SetRoundingMode(token.RoundingMode))
            {
                throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with unsupported rounding mode {token.RoundingMode}. " +
                    "Replay restore must fail closed instead of silently keeping stale exception-mode state.");
            }

            if (!core.ExceptionStatus.SetExceptionMode(token.ExceptionMode))
            {
                throw new InvalidOperationException(
                    $"InitializeFromReplayToken() reached replay-token restore with unsupported exception mode {token.ExceptionMode}. " +
                    "Replay restore must fail closed instead of silently keeping stale vector-exception handling truth.");
            }
        }

        /// <summary>
        /// Configure tracing with specified format and path
        /// </summary>
        public static void ConfigureTracing(bool enable, TraceFormat format = TraceFormat.CSV,
            string path = "trace.log", TraceLevel level = TraceLevel.Summary)
        {
            if (TraceSink == null)
            {
                TraceSink = new TraceSink(format, path);
            }

            TraceSink.SetEnabled(enable);
            TraceSink.SetLevel(level);
        }

        /// <summary>
        /// Enable or disable instruction coverage tracking
        /// </summary>
        public static void ConfigureCoverage(bool enable)
        {
            if (enable && Coverage == null)
            {
                Coverage = new InstructionCoverage();
            }
        }

        /// <summary>
        /// Set random seed for deterministic execution
        /// </summary>
        public static void SetRandomSeed(ulong seed)
        {
            RandomSeed = seed;
        }

        /// <summary>
        /// Get replay token for current execution
        /// </summary>
        public static string GetReplayToken()
        {
            currentReplayToken = CreateReplayTokenFromLiveState().BindMainMemory(MainMemory);

            // Compute trace hash if TraceSink has events
            if (TraceSink != null && TraceSink.EventCount > 0)
            {
                currentReplayToken.TraceHash = ReplayToken.ComputeTraceHash(TraceSink.GetEvents());
            }

            return currentReplayToken.ToJson();
        }

        /// <summary>
        /// Initialize from replay token
        /// </summary>
        public static void InitializeFromReplayToken(string tokenJson)
        {
            var token = ReplayToken.FromJson(tokenJson, mainMemory: MainMemory);
            if (token == null)
                return;

            RandomSeed = token.RandomSeed;
            currentReplayToken = token;
            ApplyReplayTokenToLiveVerificationCore(token);
        }

        /// <summary>
        /// Flush trace to file
        /// </summary>
        public static void FlushTrace()
        {
            TraceSink?.Flush();
        }

        /// <summary>
        /// Dump coverage report to file
        /// </summary>
        public static void DumpCoverageReport(string path = "coverage_report.txt")
        {
            Coverage?.DumpCoverageReport(path);
        }

        /// <summary>
        /// Clear all tracing and coverage data
        /// </summary>
        public static void ClearVerificationData()
        {
            TraceSink?.Clear();
            Coverage?.Clear();
            currentReplayToken = null;
        }

        /// <summary>
        /// Get verification statistics
        /// </summary>
        public static Dictionary<string, object>? GetVerificationStatistics()
        {
            if (Coverage == null)
                return null;

            var stats = Coverage.GetStatistics();

            if (TraceSink != null)
            {
                stats["TraceEventCount"] = TraceSink.EventCount;
            }

            return stats;
        }
    }
}
