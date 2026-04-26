using System.Collections.Generic;
using System.Linq;
using System.Text;
using YAKSys_Hybrid_CPU.Memory;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Generate register isolation verification condition (Phase 5)
        /// </summary>
        private VerificationCondition GenerateRegisterIsolationCondition(
            IReadOnlyList<MicroOp?> bundle, MicroOp candidate,
            int bundleOwner, int candidateOwner)
        {
            var vc = new VerificationCondition
            {
                Type = VerificationConditionType.RegisterIsolation,
                Description = $"Thread {candidateOwner} register writes don't affect thread {bundleOwner}"
            };

            var candidateWrites = new HashSet<int>(candidate.WriteRegisters);
            foreach (var op in bundle)
            {
                if (op == null || op.OwnerThreadId != bundleOwner)
                    continue;

                var bundleReads = new HashSet<int>(op.ReadRegisters);
                var bundleWrites = new HashSet<int>(op.WriteRegisters);

                if (candidateWrites.Overlaps(bundleReads) ||
                    candidateWrites.Overlaps(bundleWrites))
                {
                    vc.IsValid = false;
                    vc.CounterExample = $"Register conflict: candidate writes {string.Join(",", candidateWrites)} " +
                                       $"overlaps bundle R{string.Join(",", bundleReads)} W{string.Join(",", bundleWrites)}";
                    return vc;
                }
            }

            vc.IsValid = true;
            vc.FormalSpec = GenerateSMTRegisterIsolationSpec(candidateWrites, bundle);
            return vc;
        }

        /// <summary>
        /// Generate memory domain separation verification condition (Phase 5)
        /// </summary>
        private VerificationCondition GenerateMemoryDomainCondition(
            MicroOp candidate, int bundleOwner, int candidateOwner)
        {
            var vc = new VerificationCondition
            {
                Type = VerificationConditionType.MemoryDomainSeparation,
                Description = $"Thread {candidateOwner} memory accesses respect SIP boundaries"
            };

            if (!FormalContext.ThreadMemoryDomains.TryGetValue(candidateOwner, out var candidateDomain) ||
                !FormalContext.ThreadMemoryDomains.TryGetValue(bundleOwner, out var bundleDomain))
            {
                vc.IsValid = true;
                vc.Description += " (domains not configured, basic check only)";
                return vc;
            }

            var allRanges = new List<(ulong Address, ulong Length)>();
            allRanges.AddRange(GetSafetyVisibleReadRanges(candidate));
            if (candidate.WriteMemoryRanges != null)
                allRanges.AddRange(candidate.WriteMemoryRanges);

            foreach (var range in allRanges)
            {
                if (!candidateDomain.Contains(range.Address, range.Length))
                {
                    vc.IsValid = false;
                    vc.CounterExample = $"Access to 0x{range.Address:X}+{range.Length} outside thread {candidateOwner} domain";
                    return vc;
                }

                if (bundleDomain.Overlaps(range.Address, range.Length))
                {
                    vc.IsValid = false;
                    vc.CounterExample = $"Memory range 0x{range.Address:X}+{range.Length} overlaps protected domain";
                    return vc;
                }
            }

            vc.IsValid = true;
            vc.FormalSpec = GenerateSMTMemoryDomainSpec(candidateDomain, bundleDomain, allRanges);
            return vc;
        }

        /// <summary>
        /// Generate FSP non-interference verification condition (Phase 5)
        /// </summary>
        private VerificationCondition GenerateFSPNonInterferenceCondition(
            IReadOnlyList<MicroOp?> bundle, int targetSlot, MicroOp candidate)
        {
            var vc = new VerificationCondition
            {
                Type = VerificationConditionType.FSPNonInterference,
                Description = "FSP injection maintains non-interference"
            };

            if (!candidate.AdmissionMetadata.IsStealable)
            {
                vc.IsValid = false;
                vc.CounterExample = "Candidate is not stealable";
                return vc;
            }

            if (candidate.IsControlFlow)
            {
                vc.IsValid = false;
                vc.CounterExample = "Control flow operations cannot be stolen";
                return vc;
            }

            if (targetSlot < 0 || targetSlot >= 8)
            {
                vc.IsValid = false;
                vc.CounterExample = $"Invalid target slot: {targetSlot}";
                return vc;
            }

            vc.IsValid = true;
            return vc;
        }

        /// <summary>
        /// Generate SMT-LIB2 specification for register isolation (Phase 5)
        /// </summary>
        private string GenerateSMTRegisterIsolationSpec(
            HashSet<int> candidateWrites,
            IReadOnlyList<MicroOp?> bundle)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Register isolation constraint");
            sb.AppendLine("(assert (forall ((r Int))");
            sb.AppendLine("  (=> (member r candidate_writes)");
            sb.AppendLine("      (and (not (member r bundle_reads))");
            sb.AppendLine("           (not (member r bundle_writes))))))");
            return sb.ToString();
        }

        /// <summary>
        /// Generate SMT-LIB2 specification for memory domain separation (Phase 5)
        /// </summary>
        private string GenerateSMTMemoryDomainSpec(
            MemoryDomain candidateDomain,
            MemoryDomain bundleDomain,
            List<(ulong Address, ulong Length)> accessRanges)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; Memory domain separation constraint");
            sb.AppendLine("(declare-const candidate_domain_base (_ BitVec 64))");
            sb.AppendLine("(declare-const candidate_domain_size (_ BitVec 64))");
            sb.AppendLine("(declare-const bundle_domain_base (_ BitVec 64))");
            sb.AppendLine("(declare-const bundle_domain_size (_ BitVec 64))");
            sb.AppendLine($"(assert (= candidate_domain_base #x{candidateDomain.BaseAddress:X16}))");
            sb.AppendLine($"(assert (= candidate_domain_size #x{candidateDomain.Size:X16}))");
            sb.AppendLine($"(assert (= bundle_domain_base #x{bundleDomain.BaseAddress:X16}))");
            sb.AppendLine($"(assert (= bundle_domain_size #x{bundleDomain.Size:X16}))");
            sb.AppendLine("; Domains do not overlap");
            sb.AppendLine("(assert (or");
            sb.AppendLine("  (bvuge candidate_domain_base (bvadd bundle_domain_base bundle_domain_size))");
            sb.AppendLine("  (bvuge bundle_domain_base (bvadd candidate_domain_base candidate_domain_size))))");
            return sb.ToString();
        }

        /// <summary>
        /// Log verification conditions (Phase 5)
        /// </summary>
        private void LogVerificationConditions(VerificationCondition[] conditions)
        {
            if (FormalContext.GeneratedConditions == null)
            {
                FormalContext = new VerificationContext
                {
                    ThreadMemoryDomains = FormalContext.ThreadMemoryDomains,
                    GeneratedConditions = new List<VerificationCondition>(),
                    EnableFormalChecks = FormalContext.EnableFormalChecks
                };
            }

            FormalContext.GeneratedConditions.AddRange(conditions);
        }
    }
}
