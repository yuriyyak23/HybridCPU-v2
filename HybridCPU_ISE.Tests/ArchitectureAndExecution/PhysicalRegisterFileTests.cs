using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Registers;

namespace HybridCPU_ISE.Tests
{
    /// <summary>
    /// Blueprint §9 test matrix — Physical Register File, RenameMap, CommitMap, FreeList.
    /// Invariants:
    ///  - PRF p0 is hardwired zero (reads 0, writes are no-ops).
    ///  - Allocation/commit do not produce overlapping physical register assignments.
    ///  - CommitMap.RestoreInto correctly rolls back RenameMap after a branch/exception.
    ///  - FreeList never returns p0; exhausted list returns -1; released registers are reusable.
    /// </summary>
    public class PhysicalRegisterFileTests
    {
        #region PhysicalRegisterFile

        [Fact]
        public void PhysReg_ReadP0_AlwaysReturnsZero()
        {
            var prf = new PhysicalRegisterFile();
            Assert.Equal(0UL, prf.Read(0));
        }

        [Fact]
        public void PhysReg_WriteP0_IsNoOp()
        {
            var prf = new PhysicalRegisterFile();
            prf.Write(0, 0xDEAD_BEEF_CAFE_BABEu);
            Assert.Equal(0UL, prf.Read(0));
        }

        [Fact]
        public void PhysReg_WriteAndReadNonZeroRegister_RoundTrips()
        {
            var prf = new PhysicalRegisterFile();
            prf.Write(5, 0x1234_5678_9ABC_DEF0u);
            Assert.Equal(0x1234_5678_9ABC_DEF0u, prf.Read(5));
        }

        [Fact]
        public void PhysReg_WriteLastRegister_RoundTrips()
        {
            var prf = new PhysicalRegisterFile();
            int last = PhysicalRegisterFile.TotalPhysRegs - 1;
            prf.Write(last, ulong.MaxValue);
            Assert.Equal(ulong.MaxValue, prf.Read(last));
        }

        [Fact]
        public void PhysReg_OutOfRangeRead_Throws()
        {
            var prf = new PhysicalRegisterFile();
            Assert.Throws<ArgumentOutOfRangeException>(() => prf.Read(PhysicalRegisterFile.TotalPhysRegs));
        }

        [Fact]
        public void PhysReg_OutOfRangeWrite_Throws()
        {
            var prf = new PhysicalRegisterFile();
            Assert.Throws<ArgumentOutOfRangeException>(() => prf.Write(-1, 42));
        }

        #endregion

        #region RenameMap

        [Fact]
        public void RenameMap_DefaultLookup_ReturnsIdentityMapping()
        {
            var rm = new RenameMap(4);
            for (int vt = 0; vt < 4; vt++)
                for (int r = 0; r < RenameMap.ArchRegs; r++)
                    Assert.Equal(r, rm.Lookup(vt, r));
        }

        [Fact]
        public void RenameMap_Remap_UpdatesCorrectVtAndReg()
        {
            var rm = new RenameMap(4);
            rm.Remap(2, 5, 99);
            Assert.Equal(99, rm.Lookup(2, 5));
            // Other VTs unchanged
            Assert.Equal(5, rm.Lookup(0, 5));
            Assert.Equal(5, rm.Lookup(1, 5));
            Assert.Equal(5, rm.Lookup(3, 5));
        }

        [Fact]
        public void RenameMap_Reset_RestoresIdentityMapping()
        {
            var rm = new RenameMap(4);
            rm.Remap(0, 0, 100);
            rm.Remap(1, 31, 127);
            rm.Reset();
            Assert.Equal(0, rm.Lookup(0, 0));
            Assert.Equal(31, rm.Lookup(1, 31));
        }

        [Fact]
        public void RenameMap_InvalidVtId_Throws()
        {
            var rm = new RenameMap(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => rm.Lookup(4, 0));
        }

        [Fact]
        public void RenameMap_InvalidArchReg_Throws()
        {
            var rm = new RenameMap(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => rm.Lookup(0, RenameMap.ArchRegs));
        }

        [Fact]
        public void RenameMap_DifferentVtsAreIsolated()
        {
            var rm = new RenameMap(4);
            // Remap same arch reg in all VTs to different physical regs
            rm.Remap(0, 10, 50);
            rm.Remap(1, 10, 60);
            rm.Remap(2, 10, 70);
            rm.Remap(3, 10, 80);

            Assert.Equal(50, rm.Lookup(0, 10));
            Assert.Equal(60, rm.Lookup(1, 10));
            Assert.Equal(70, rm.Lookup(2, 10));
            Assert.Equal(80, rm.Lookup(3, 10));
        }

        #endregion

        #region CommitMap

        [Fact]
        public void CommitMap_DefaultLookup_ReturnsIdentity()
        {
            var cm = new CommitMap(4);
            for (int vt = 0; vt < 4; vt++)
                for (int r = 0; r < RenameMap.ArchRegs; r++)
                    Assert.Equal(r, cm.Lookup(vt, r));
        }

        [Fact]
        public void CommitMap_Commit_UpdatesEntry()
        {
            var cm = new CommitMap(4);
            cm.Commit(1, 7, 88);
            Assert.Equal(88, cm.Lookup(1, 7));
        }

        [Fact]
        public void CommitMap_RestoreInto_RollsBackRenameMap()
        {
            var rm = new RenameMap(4);
            var cm = new CommitMap(4);

            // Simulate rename: arch 5 of VT0 → phys 42
            rm.Remap(0, 5, 42);
            // Commit the change
            cm.Commit(0, 5, 42);

            // Later: speculate further rename arch 5 → phys 99
            rm.Remap(0, 5, 99);
            Assert.Equal(99, rm.Lookup(0, 5));

            // Exception: restore from commit map
            cm.RestoreInto(rm);
            Assert.Equal(42, rm.Lookup(0, 5));
        }

        [Fact]
        public void CommitMap_Reset_RestoresIdentity()
        {
            var cm = new CommitMap(4);
            cm.Commit(0, 0, 100);
            cm.Commit(3, 31, 127);
            cm.Reset();
            Assert.Equal(0, cm.Lookup(0, 0));
            Assert.Equal(31, cm.Lookup(3, 31));
        }

        #endregion

        #region FreeList

        [Fact]
        public void FreeList_InitialAvailable_IsSpeculativeRegCount()
        {
            var fl = new FreeList();
            int expected = PhysicalRegisterFile.TotalPhysRegs - RenameMap.ArchRegs;
            Assert.Equal(expected, fl.Available);
        }

        [Fact]
        public void FreeList_Allocate_DecreasesAvailable()
        {
            var fl = new FreeList();
            int before = fl.Available;
            int physReg = fl.Allocate();
            Assert.NotEqual(-1, physReg);
            Assert.Equal(before - 1, fl.Available);
        }

        [Fact]
        public void FreeList_Allocate_NeverReturnsP0()
        {
            var fl = new FreeList();
            while (fl.Available > 0)
            {
                int r = fl.Allocate();
                Assert.NotEqual(0, r);
            }
        }

        [Fact]
        public void FreeList_Allocate_WhenExhausted_ReturnsMinusOne()
        {
            var fl = new FreeList();
            while (fl.Available > 0)
                fl.Allocate();
            Assert.Equal(-1, fl.Allocate());
        }

        [Fact]
        public void FreeList_Release_IncreasesAvailableAndAllowsReuse()
        {
            var fl = new FreeList();
            int physReg = fl.Allocate();
            int after = fl.Available;

            fl.Release(physReg);

            Assert.Equal(after + 1, fl.Available);

            // The released register can be allocated again
            int reallocated = fl.Allocate();
            Assert.NotEqual(-1, reallocated);
        }

        [Fact]
        public void FreeList_ReleaseP0_IsIgnored()
        {
            var fl = new FreeList();
            int before = fl.Available;
            fl.Release(0); // p0 must never be in the free list
            Assert.Equal(before, fl.Available);
        }

        [Fact]
        public void FreeList_AllocateReleaseCycle_NoRegistersOverlap()
        {
            var fl = new FreeList();
            // Drain the free list
            var allocated = new System.Collections.Generic.HashSet<int>();
            while (fl.Available > 0)
            {
                int r = fl.Allocate();
                Assert.DoesNotContain(r, allocated); // no duplicate allocation
                allocated.Add(r);
            }
            // All allocated registers are non-zero and in valid range
            foreach (int r in allocated)
            {
                Assert.NotEqual(0, r);
                Assert.InRange(r, RenameMap.ArchRegs, PhysicalRegisterFile.TotalPhysRegs - 1);
            }
        }

        #endregion
    }
}
