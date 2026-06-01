using System.Runtime.CompilerServices;

namespace YAKSys_Hybrid_CPU
{
    public partial struct Processor
    {
        public partial struct CPU_Core
        {
            public enum PipelineStallKind : byte
            {
                None = 0,
                DataHazard = 1,
                MemoryWait = 2,
                ControlHazard = 3,
                InvariantViolation = 4
            }

            public enum PipelineStallTextStyle : byte
            {
                Trace = 0,
                Snapshot = 1,
                Compact = 2,
                Banner = 3
            }

            public static class PipelineStallText
            {
                private readonly struct Entry
                {
                    public Entry(
                        string trace,
                        string snapshot,
                        string compact,
                        string banner)
                    {
                        Trace = trace;
                        Snapshot = snapshot;
                        Compact = compact;
                        Banner = banner;
                    }

                    public string Trace { get; }
                    public string Snapshot { get; }
                    public string Compact { get; }
                    public string Banner { get; }
                }

                private static readonly Entry[] Entries =
                {
                    new("none", "Pipeline Stall", "Stalled", "STALLED"),
                    new("data-hazard", "Data Hazard", "DataHazard", "STALLED (Data Hazard)"),
                    new("memory", "Memory Wait", "MemoryStall", "STALLED (Memory)"),
                    new("control", "Control Hazard", "ControlHazard", "STALLED (Control)"),
                    new("invariant-violation", "Invariant Violation", "InvariantViolation", "STALLED (Invariant Violation)")
                };

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static PipelineStallKind Normalize(PipelineStallKind kind)
                {
                    return kind switch
                    {
                        PipelineStallKind.None => PipelineStallKind.None,
                        PipelineStallKind.DataHazard => PipelineStallKind.DataHazard,
                        PipelineStallKind.MemoryWait => PipelineStallKind.MemoryWait,
                        PipelineStallKind.ControlHazard => PipelineStallKind.ControlHazard,
                        PipelineStallKind.InvariantViolation => PipelineStallKind.InvariantViolation,
                        _ => PipelineStallKind.None
                    };
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static string Render(PipelineStallKind kind, PipelineStallTextStyle style)
                {
                    Entry entry = Entries[(int)Normalize(kind)];
                    return style switch
                    {
                        PipelineStallTextStyle.Trace => entry.Trace,
                        PipelineStallTextStyle.Snapshot => entry.Snapshot,
                        PipelineStallTextStyle.Compact => entry.Compact,
                        PipelineStallTextStyle.Banner => entry.Banner,
                        _ => entry.Snapshot
                    };
                }
            }
        }
    }
}
