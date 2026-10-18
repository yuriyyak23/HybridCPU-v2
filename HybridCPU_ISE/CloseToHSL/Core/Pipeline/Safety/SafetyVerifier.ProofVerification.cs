using System.Collections.Generic;
using System.Linq;

namespace YAKSys_Hybrid_CPU.Core
{
    public partial class SafetyVerifier
    {
        /// <summary>
        /// Verify injection with formal proof generation (Phase 5)
        /// </summary>
        public bool VerifyInjectionWithProof(
            IReadOnlyList<MicroOp?> bundle,
            int targetSlot,
            MicroOp candidate,
            int bundleOwnerThreadId,
            int candidateOwnerThreadId,
            out VerificationCondition[] conditions)
        {
            var conditionList = new List<VerificationCondition>();

            // Generate verification condition for register isolation
            conditionList.Add(GenerateRegisterIsolationCondition(
                bundle, candidate, bundleOwnerThreadId, candidateOwnerThreadId));

            // Generate verification condition for memory domain separation
            conditionList.Add(GenerateMemoryDomainCondition(
                candidate, bundleOwnerThreadId, candidateOwnerThreadId));

            // Generate verification condition for FSP non-interference
            conditionList.Add(GenerateFSPNonInterferenceCondition(
                bundle, targetSlot, candidate));

            conditions = conditionList.ToArray();

            // Check all conditions
            bool allValid = conditions.All(c => c.IsValid);

            // Log to verification database
            if (FormalContext.EnableFormalChecks)
            {
                LogVerificationConditions(conditions);
            }

            return allValid;
        }
    }
}
