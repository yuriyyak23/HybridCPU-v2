using Xunit;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using YAKSys_Hybrid_CPU.Memory;
using System;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Comprehensive tests for Phase 2 refactoring:
    /// 1. Memory Arbitration & Bank Conflict
    /// 2. Formal Resource Proofs (Verification Readiness)
    /// 3. Back-pressure Logic (Microarchitecture Fidelity)
    /// </summary>
    public class Phase2RefactoringTests
    {
        #region Task 1: Memory Arbitration & Bank Conflict Tests

        [Fact]
        public void BankArbitrator_Constructor_ShouldInitializeClean()
        {
            // Arrange & Act
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);

            // Assert
            Assert.NotNull(arbitrator);
            Assert.Equal(0, arbitrator.GetBusyBankCount());
        }

        [Fact]
        public void BankArbitrator_TryReserveBank_ShouldSucceedForFirstAccess()
        {
            // Arrange
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);
            ulong address = 0x1000; // Address = 4096, Bank = (4096/1024) % 8 = 4

            // Act
            bool reserved = arbitrator.TryReserveBank(address, out int bankId);

            // Assert
            Assert.True(reserved);
            Assert.Equal(4, bankId);  // Corrected: Bank 4, not Bank 1
            Assert.Equal(1, arbitrator.GetBusyBankCount());
        }

        [Fact]
        public void BankArbitrator_TryReserveBank_ShouldFailForConflict()
        {
            // Arrange
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);
            ulong address1 = 0x1000; // Bank 4: (4096/1024) % 8 = 4
            ulong address2 = 0x1100; // Bank 4: (4352/1024) % 8 = 4 (same bank, different offset)

            // Act
            bool reserved1 = arbitrator.TryReserveBank(address1, out int bankId1);
            bool reserved2 = arbitrator.TryReserveBank(address2, out int bankId2);

            // Assert
            Assert.True(reserved1);
            Assert.False(reserved2); // Conflict!
            Assert.Equal(4, bankId1);
            Assert.Equal(4, bankId2);
            Assert.Equal(1, arbitrator.GetBusyBankCount());
        }

        [Fact]
        public void BankArbitrator_ResetCycle_ShouldClearReservations()
        {
            // Arrange
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);
            arbitrator.TryReserveBank(0x1000, out _);
            Assert.Equal(1, arbitrator.GetBusyBankCount());

            // Act
            arbitrator.ResetCycle();

            // Assert
            Assert.Equal(0, arbitrator.GetBusyBankCount());
        }

        [Fact]
        public void BankArbitrator_MultipleAccesses_DifferentBanks_ShouldSucceed()
        {
            // Arrange
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);

            // Act - Access different banks
            bool reserved0 = arbitrator.TryReserveBank(0x0000, out _); // Bank 0
            bool reserved1 = arbitrator.TryReserveBank(0x0400, out _); // Bank 1
            bool reserved2 = arbitrator.TryReserveBank(0x0800, out _); // Bank 2

            // Assert
            Assert.True(reserved0);
            Assert.True(reserved1);
            Assert.True(reserved2);
            Assert.Equal(3, arbitrator.GetBusyBankCount());
        }

        [Fact]
        public void MicroOpScheduler_ScheduleWithArbitration_NonMemoryOp_ShouldSucceed()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);
            var nopOp = new NopMicroOp { OwnerThreadId = 0 };

            // Act
            var result = scheduler.ScheduleWithArbitration(nopOp, arbitrator);

            // Assert
            Assert.Equal(MicroOpScheduler.ExecutionResult.Success, result);
            Assert.Equal(0, scheduler.BankConflictsCount);
        }

        [Fact]
        public void MicroOpScheduler_ScheduleWithArbitration_MemoryOp_NoConflict_ShouldSucceed()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);

            var loadOp = new LoadMicroOp
            {
                Address = 0x1000,
                Size = 8,
                OwnerThreadId = 0,
                WritesRegister = true,
                DestRegID = 1,
                BaseRegID = 0
            };
            loadOp.InitializeMetadata();

            // Act
            var result = scheduler.ScheduleWithArbitration(loadOp, arbitrator);

            // Assert
            Assert.Equal(MicroOpScheduler.ExecutionResult.Success, result);
            Assert.Equal(0, scheduler.BankConflictsCount);
        }

        [Fact]
        public void MicroOpScheduler_ScheduleWithArbitration_MemoryOp_WithConflict_ShouldStall()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);

            // Reserve bank 1 first
            arbitrator.TryReserveBank(0x1000, out _);

            var loadOp = new LoadMicroOp
            {
                Address = 0x1100, // Same bank as 0x1000
                Size = 8,
                OwnerThreadId = 1,
                WritesRegister = true,
                DestRegID = 2,
                BaseRegID = 0
            };
            loadOp.InitializeMetadata();

            // Act
            var result = scheduler.ScheduleWithArbitration(loadOp, arbitrator);

            // Assert
            Assert.Equal(MicroOpScheduler.ExecutionResult.Stall, result);
            Assert.Equal(1, scheduler.BankConflictsCount);
        }

        [Fact]
        public void MicroOpScheduler_ScheduleWithArbitration_NullOp_ShouldReturnFailed()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            var arbitrator = new BankArbitrator(bankCount: 8, bankSize: 1024);

            // Act
            var result = scheduler.ScheduleWithArbitration(null, arbitrator);

            // Assert
            Assert.Equal(MicroOpScheduler.ExecutionResult.Failed, result);
        }

        #endregion

        #region Task 2: Formal Resource Proofs Tests

        [Fact]
        public void BundleResourceProof_Constructor_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            var proof = new BundleResourceProof();

            // Assert
            Assert.Equal(0, proof.Cycle);
            Assert.Equal(0u, proof.BundleHash);
            Assert.False(proof.IsValid);
            Assert.Equal("Not verified", proof.VerificationStatus);
        }

        [Fact]
        public void BundleResourceProof_CalculateBundleHash_EmptyBundle_ShouldReturnZero()
        {
            // Arrange
            MicroOp[] emptyBundle = Array.Empty<MicroOp>();

            // Act
            uint hash = BundleResourceProof.CalculateBundleHash(emptyBundle);

            // Assert
            Assert.Equal(0u, hash);
        }

        [Fact]
        public void BundleResourceProof_CalculateBundleHash_ShouldBeDeterministic()
        {
            // Arrange
            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp { OpCode = (uint)i, OwnerThreadId = i };
            }

            // Act
            uint hash1 = BundleResourceProof.CalculateBundleHash(bundle);
            uint hash2 = BundleResourceProof.CalculateBundleHash(bundle);

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.NotEqual(0u, hash1);
        }

        [Fact]
        public void BundleResourceProof_VerifySignature_NoSignature_ShouldReturnFalse()
        {
            // Arrange
            var proof = new BundleResourceProof { Signature = Array.Empty<byte>() };

            // Act
            bool valid = proof.VerifySignature();

            // Assert
            Assert.False(valid);
        }

        [Fact]
        public void BundleResourceProof_VerifySignature_WithValidSignature_ShouldReturnTrue()
        {
            // Arrange
            var proof = new BundleResourceProof { Signature = new byte[32] }; // Minimum valid length

            // Act
            bool valid = proof.VerifySignature();

            // Assert
            Assert.True(valid);
        }

        [Fact]
        public void SafetyVerifier_GenerateProof_ValidBundle_ShouldReturnValidProof()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            verifier.SetCurrentCycle(1000);

            var bundle = new MicroOp[8];
            for (int i = 0; i < 8; i++)
            {
                bundle[i] = new NopMicroOp { OwnerThreadId = 0 };
            }

            var context = new SecurityContext
            {
                MinAddr = 0x0000,
                MaxAddr = 0x10000,
                ActiveThreads = 0x01,
                OwnerThreadId = 0
            };

            // Act
            var proof = verifier.GenerateProof(bundle, context);

            // Assert
            Assert.True(proof.IsValid);
            Assert.Equal(1000, proof.Cycle);
            Assert.NotNull(proof.Signature);
            Assert.True(proof.Signature.Length >= 32); // SHA-256 hash
            Assert.Equal("All invariants verified", proof.VerificationStatus);
        }

        [Fact]
        public void SafetyVerifier_GenerateProof_NullBundle_ShouldReturnInvalidProof()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var context = new SecurityContext();

            // Act
            var proof = verifier.GenerateProof(null, context);

            // Assert
            Assert.False(proof.IsValid);
            Assert.Contains("Invalid bundle", proof.VerificationStatus);
        }

        [Fact]
        public void SafetyVerifier_GenerateProof_WrongBundleSize_ShouldReturnInvalidProof()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[4]; // Wrong size (should be 8)
            var context = new SecurityContext();

            // Act
            var proof = verifier.GenerateProof(bundle, context);

            // Assert
            Assert.False(proof.IsValid);
        }

        [Fact]
        public void SafetyVerifier_GenerateProof_MemoryViolation_ShouldReturnInvalidProof()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            verifier.SetCurrentCycle(100);

            var bundle = new MicroOp[8];
            for (int i = 0; i < 7; i++)
            {
                bundle[i] = new NopMicroOp { OwnerThreadId = 0 };
            }

            // Add a load operation that violates memory bounds
            var loadOp = new LoadMicroOp
            {
                Address = 0x20000, // Outside allowed range
                Size = 8,
                OwnerThreadId = 0
            };
            loadOp.InitializeMetadata();
            bundle[7] = loadOp;

            var context = new SecurityContext
            {
                MinAddr = 0x0000,
                MaxAddr = 0x10000, // Load at 0x20000 is outside
                ActiveThreads = 0x01,
                OwnerThreadId = 0
            };

            // Act
            var proof = verifier.GenerateProof(bundle, context);

            // Assert
            Assert.False(proof.IsValid);
            Assert.Contains("Memory isolation violation", proof.VerificationStatus);
        }

        [Fact]
        public void SafetyVerifier_GenerateProof_ThreadMask_ShouldReflectActiveThreads()
        {
            // Arrange
            var verifier = new SafetyVerifier();
            var bundle = new MicroOp[8];
            bundle[0] = new NopMicroOp { OwnerThreadId = 0 };
            bundle[1] = new NopMicroOp { OwnerThreadId = 2 };
            bundle[2] = new NopMicroOp { OwnerThreadId = 5 };
            for (int i = 3; i < 8; i++)
            {
                bundle[i] = new NopMicroOp { OwnerThreadId = 0 };
            }

            var context = new SecurityContext
            {
                MinAddr = 0,
                MaxAddr = 0x10000,
                ActiveThreads = 0xFF,
                OwnerThreadId = 0
            };

            // Act
            var proof = verifier.GenerateProof(bundle, context);

            // Assert
            Assert.True(proof.IsValid);
            // ThreadMask should have bits 0, 2, and 5 set
            Assert.Equal(0b00100101u, proof.ThreadMask);
        }

        #endregion

        #region Task 3: Nomination & Scoreboard Tests

        [Fact]
        public void MicroOpScheduler_Nominate_StalledCore_ShouldAnnul()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetCoreStalled(0, true);

            // Act
            scheduler.Nominate(0, new NopMicroOp { OwnerThreadId = 0 });

            // Assert - stalled core's nomination is annulled
            var stolen = scheduler.TryStealSlot(1, 0);
            Assert.Null(stolen);
        }

        [Fact]
        public void MicroOpScheduler_SetScoreboardPending_ShouldTrackDMA()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Act
            int slot = scheduler.SetScoreboardPending(targetId: 5, ownerThreadId: 1, currentCycle: 100);

            // Assert
            Assert.True(slot >= 0);
            Assert.True(scheduler.IsScoreboardPending(5));
        }

        [Fact]
        public void MicroOpScheduler_ClearScoreboardEntry_ShouldUnblockTarget()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            int slot = scheduler.SetScoreboardPending(targetId: 5, ownerThreadId: 1, currentCycle: 100);

            // Act
            scheduler.ClearScoreboardEntry(slot);

            // Assert
            Assert.False(scheduler.IsScoreboardPending(5));
        }

        [Fact]
        public void MicroOpScheduler_SetScoreboardPending_AllSlotsFull_ShouldReturnNegative()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();

            // Fill all 8 scoreboard slots
            for (int i = 0; i < 8; i++)
            {
                int slot = scheduler.SetScoreboardPending(targetId: i, ownerThreadId: 0, currentCycle: 0);
                Assert.True(slot >= 0);
            }

            // Act - Try to add one more
            int overflow = scheduler.SetScoreboardPending(targetId: 99, ownerThreadId: 0, currentCycle: 0);

            // Assert
            Assert.Equal(-1, overflow);
        }

        [Fact]
        public void MicroOpScheduler_ClearScoreboard_ShouldResetAllEntries()
        {
            // Arrange
            var scheduler = new MicroOpScheduler();
            scheduler.SetScoreboardPending(targetId: 1, ownerThreadId: 0, currentCycle: 0);
            scheduler.SetScoreboardPending(targetId: 2, ownerThreadId: 0, currentCycle: 0);
            Assert.True(scheduler.IsScoreboardPending(1));
            Assert.True(scheduler.IsScoreboardPending(2));

            // Act
            scheduler.ClearScoreboard();

            // Assert
            Assert.False(scheduler.IsScoreboardPending(1));
            Assert.False(scheduler.IsScoreboardPending(2));
        }

        [Fact]
        public void PipelinePanicException_Constructor_ShouldSetMessage()
        {
            // Arrange & Act
            var exception = new PipelinePanicException("Test panic message");

            // Assert
            Assert.NotNull(exception);
            Assert.Equal("Test panic message", exception.Message);
        }

        #endregion
    }
}
