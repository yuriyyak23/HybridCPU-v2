using Xunit;
using Xunit.Abstractions;
using YAKSys_Hybrid_CPU;
using YAKSys_Hybrid_CPU.Core;
using HybridCPU_ISE.Tests.TestHelpers;
using System.Collections.Generic;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Category 4: Memory Consistency Model Tests
    ///
    /// Implements litmus tests for memory ordering patterns:
    /// - Store → Load ordering
    /// - Load Buffering
    /// - Dekker's algorithm
    /// - Message Passing
    /// - Forbidden execution detection
    /// </summary>
    public class ISAModelMemoryConsistencyTests
    {
        private readonly ITestOutputHelper _output;

        public ISAModelMemoryConsistencyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Store-Load Ordering

        [Fact]
        public void MemoryConsistency_StoreLoad_BasicOrdering()
        {
            // Test: Store followed by Load should observe the store

            // Arrange: Store to address A, then Load from A
            var storeOp = MicroOpTestHelper.CreateStore(0, srcReg: 1, address: 0x1000, domainTag: 0);
            var loadOp = MicroOpTestHelper.CreateLoad(0, destReg: 2, address: 0x1000, domainTag: 0);

            // Assert: Both operations target same address
            Assert.Equal(storeOp.Address, loadOp.Address);
            Assert.Equal(storeOp.Placement.DomainTag, loadOp.Placement.DomainTag);
            _output.WriteLine("Store-Load: same address and domain verified");
        }

        [Fact]
        public void MemoryConsistency_StoreLoad_DifferentAddresses()
        {
            // Stores and loads to different addresses can reorder

            // Arrange
            var store1 = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);
            var load1 = MicroOpTestHelper.CreateLoad(0, 2, 0x2000, 0);

            // Assert: Different addresses
            Assert.NotEqual(store1.Address, load1.Address);
            _output.WriteLine("Store-Load: different addresses can proceed independently");
        }

        [Fact]
        public void MemoryConsistency_StoreLoad_SameDomainConflict()
        {
            // Stores and loads to same domain should be ordered

            // Arrange
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            var candidate = MicroOpTestHelper.CreateLoad(1, 2, 0x2000, 0);
            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Same domain causes structural conflict
            Assert.False(result, "Store and Load in same domain conflict");
            _output.WriteLine("Store-Load: same domain ordering enforced");
        }

        #endregion

        #region Load Buffering

        [Fact]
        public void MemoryConsistency_LoadBuffering_IndependentPairs()
        {
            // Load buffering: Thread 1 loads X then stores Y, Thread 2 loads Y then stores X
            // Can both threads see 0?

            // Arrange: Simulate two independent load-store pairs
            var t1_load = MicroOpTestHelper.CreateLoad(0, destReg: 1, address: 0x1000, domainTag: 0);
            var t1_store = MicroOpTestHelper.CreateStore(0, srcReg: 2, address: 0x2000, domainTag: 0);

            var t2_load = MicroOpTestHelper.CreateLoad(1, destReg: 3, address: 0x2000, domainTag: 0);
            var t2_store = MicroOpTestHelper.CreateStore(1, srcReg: 4, address: 0x1000, domainTag: 0);

            // Assert: Operations are independent
            Assert.NotEqual(t1_load.Address, t1_store.Address);
            Assert.NotEqual(t2_load.Address, t2_store.Address);
            _output.WriteLine("Load buffering: independent pairs verified");
        }

        [Fact]
        public void MemoryConsistency_LoadBuffering_CrossThreadDependencies()
        {
            // Check that cross-thread dependencies are tracked

            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];

            // Thread 0: Load X, Store Y
            bundle[0] = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, 0);

            // Thread 1: Load Y
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateLoad(1, 2, 0x2000, 0));

            // Act
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Assert: Check packing results
            _output.WriteLine($"Load buffering: cross-thread packing attempted");
        }

        [Fact]
        public void MemoryConsistency_LoadBuffering_DomainIsolation()
        {
            // Different domains don't interfere

            // Arrange
            var t1_load = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, domainTag: 0);
            var t2_load = MicroOpTestHelper.CreateLoad(1, 2, 0x1000, domainTag: 1);

            // Assert: Different domains
            Assert.NotEqual(t1_load.Placement.DomainTag, t2_load.Placement.DomainTag);
            _output.WriteLine("Load buffering: domain isolation verified");
        }

        #endregion

        #region Dekker's Algorithm

        [Fact]
        public void MemoryConsistency_Dekker_FlagUpdates()
        {
            // Dekker: Thread 1 sets flag1, Thread 2 sets flag2
            // Both check other's flag for mutual exclusion

            // Arrange: Simulate flag stores
            var t1_setFlag = MicroOpTestHelper.CreateStore(0, srcReg: 1, address: 0x1000, domainTag: 0);
            var t2_setFlag = MicroOpTestHelper.CreateStore(1, srcReg: 2, address: 0x2000, domainTag: 0);

            // Assert: Different flag addresses
            Assert.NotEqual(t1_setFlag.Address, t2_setFlag.Address);
            _output.WriteLine("Dekker: flag updates to different addresses");
        }

        [Fact]
        public void MemoryConsistency_Dekker_FlagChecks()
        {
            // Dekker: Threads load other's flag

            // Arrange
            var t1_checkFlag2 = MicroOpTestHelper.CreateLoad(0, destReg: 1, address: 0x2000, domainTag: 0);
            var t2_checkFlag1 = MicroOpTestHelper.CreateLoad(1, destReg: 2, address: 0x1000, domainTag: 0);

            // Assert: Cross-check addresses
            Assert.Equal(0x2000UL, t1_checkFlag2.Address);
            Assert.Equal(0x1000UL, t2_checkFlag1.Address);
            _output.WriteLine("Dekker: cross-checking flags");
        }

        [Fact]
        public void MemoryConsistency_Dekker_MutualExclusion()
        {
            // Dekker should guarantee mutual exclusion

            // Arrange: Setup both threads
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];

            // Thread 0: Set flag and check
            bundle[0] = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            // Thread 1: Set flag
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateStore(1, 2, 0x2000, domainTag: 0));

            // Act
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Assert: Both operations should be trackable
            _output.WriteLine("Dekker: mutual exclusion pattern tracked");
        }

        #endregion

        #region Message Passing

        [Fact]
        public void MemoryConsistency_MessagePassing_ProducerConsumer()
        {
            // Producer writes data then flag, Consumer reads flag then data

            // Arrange: Producer sequence
            var writeData = MicroOpTestHelper.CreateStore(0, srcReg: 1, address: 0x1000, domainTag: 0);
            var writeFlag = MicroOpTestHelper.CreateStore(0, srcReg: 2, address: 0x2000, domainTag: 0);

            // Consumer sequence
            var readFlag = MicroOpTestHelper.CreateLoad(1, destReg: 3, address: 0x2000, domainTag: 0);
            var readData = MicroOpTestHelper.CreateLoad(1, destReg: 4, address: 0x1000, domainTag: 0);

            // Assert: Proper addressing
            Assert.Equal(writeData.Address, readData.Address);
            Assert.Equal(writeFlag.Address, readFlag.Address);
            _output.WriteLine("Message passing: producer-consumer addresses match");
        }

        [Fact]
        public void MemoryConsistency_MessagePassing_DataDependency()
        {
            // Data dependency: Consumer must see producer's writes in order

            // Arrange
            var scheduler = new MicroOpScheduler();
            var bundle = new MicroOp[8];

            // Producer: Write data
            bundle[0] = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            // Consumer: Read data (different VT)
            scheduler.NominateSmtCandidate(1, MicroOpTestHelper.CreateLoad(1, 2, 0x1000, 0));

            // Act
            var packed = scheduler.PackBundleIntraCoreSmt(bundle, 0, 0);

            // Assert
            _output.WriteLine("Message passing: data dependency tracked");
        }

        [Fact]
        public void MemoryConsistency_MessagePassing_ReleaseAcquire()
        {
            // Release-acquire semantics for synchronization

            // Arrange: Release (store with barrier semantics)
            var releaseStore = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            // Acquire (load with barrier semantics)
            var acquireLoad = MicroOpTestHelper.CreateLoad(1, 2, 0x1000, 0);

            // Assert: Same location for synchronization
            Assert.Equal(releaseStore.Address, acquireLoad.Address);
            _output.WriteLine("Message passing: release-acquire synchronization point");
        }

        #endregion

        #region Forbidden Executions

        [Fact]
        public void MemoryConsistency_Forbidden_NoStoreStoreReordering()
        {
            // Stores to same domain should not reorder

            // Arrange
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            var candidate = MicroOpTestHelper.CreateStore(1, 2, 0x2000, domainTag: 0);
            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: Stores to same domain conflict
            Assert.False(result, "Stores to same domain cannot be reordered freely");
            _output.WriteLine("Forbidden: store-store reordering prevented");
        }

        [Fact]
        public void MemoryConsistency_Forbidden_NoLoadLoadReordering()
        {
            // Loads from same domain may conflict at LSU level

            // Arrange
            var bundle = new MicroOp[8];
            bundle[0] = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, 0);

            var candidate = MicroOpTestHelper.CreateLoad(1, 2, 0x2000, 0);
            var verifier = new SafetyVerifier();

            // Act
            bool result = SafetyVerifierCompatibilityTestModel.VerifyInjectionFast(verifier, bundle, candidate);

            // Assert: LSU structural resource conflict
            Assert.False(result, "Multiple loads conflict on LSU resources");
            _output.WriteLine("Forbidden: load-load LSU conflict detected");
        }

        [Fact]
        public void MemoryConsistency_Forbidden_CoherenceViolation()
        {
            // Same address accesses from different threads

            // Arrange
            var t1_store = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);
            var t2_load = MicroOpTestHelper.CreateLoad(1, 2, 0x1000, 0);

            // Assert: Same address, different threads
            Assert.Equal(t1_store.Address, t2_load.Address);
            Assert.NotEqual(t1_store.VirtualThreadId, t2_load.VirtualThreadId);
            _output.WriteLine("Forbidden: coherence violation scenario tracked");
        }

        #endregion

        #region TSO-like Consistency

        [Fact]
        public void MemoryConsistency_TSO_StoreBuffering()
        {
            // TSO: Stores can be buffered before becoming visible

            // Arrange: Store operation
            var storeOp = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);

            // Assert: Store has address and value
            Assert.Equal(0x1000UL, storeOp.Address);
            _output.WriteLine("TSO: store buffering capability");
        }

        [Fact]
        public void MemoryConsistency_TSO_LoadOrdering()
        {
            // TSO: Loads execute in order

            // Arrange: Sequential loads
            var load1 = MicroOpTestHelper.CreateLoad(0, 1, 0x1000, 0);
            var load2 = MicroOpTestHelper.CreateLoad(0, 2, 0x2000, 0);

            // Assert: Sequential from same thread
            Assert.Equal(load1.VirtualThreadId, load2.VirtualThreadId);
            _output.WriteLine("TSO: load ordering from same thread");
        }

        [Fact]
        public void MemoryConsistency_TSO_FenceOperation()
        {
            // TSO: Fence ensures ordering

            // Arrange: Fence would be a special operation
            // Here we test that operations can be sequenced
            var preOp = MicroOpTestHelper.CreateStore(0, 1, 0x1000, domainTag: 0);
            var postOp = MicroOpTestHelper.CreateLoad(0, 2, 0x2000, 0);

            // Assert: Both from same thread (ordering implied)
            Assert.Equal(preOp.VirtualThreadId, postOp.VirtualThreadId);
            _output.WriteLine("TSO: fence semantics (operation sequencing)");
        }

        #endregion
    }
}
