using System.Collections.Generic;
using System.Text;

namespace YAKSys_Hybrid_CPU
{
    public partial class PerformanceReport
    {
        public List<BurstTrace>? BurstTraces { get; set; }

        public class BurstTrace
        {
            public long Timestamp { get; set; }
            public ulong Address { get; set; }
            public int Length { get; set; }
            public bool IsRead { get; set; }
            public int BankId { get; set; }
            public long Duration { get; set; }
        }

        private bool HasBurstTraces => BurstTraces is { Count: > 0 };

        private void AppendBurstTraceSummary(StringBuilder sb)
        {
            if (!HasBurstTraces)
            {
                return;
            }

            sb.AppendLine($"Burst Traces: {BurstTraces!.Count} traces collected");
            sb.AppendLine();
        }

        public string ExportBurstTracesToCSV()
        {
            if (!HasBurstTraces)
            {
                return "No burst traces available";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Address,Length,IsRead,BankId,Duration");

            foreach (BurstTrace trace in BurstTraces!)
            {
                sb.AppendLine($"{trace.Timestamp},{trace.Address},{trace.Length},{trace.IsRead},{trace.BankId},{trace.Duration}");
            }

            return sb.ToString();
        }
    }
}
