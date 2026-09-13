using System;
using Xunit;
using YAKSys_Hybrid_CPU.Core.Registers;
using YAKSys_Hybrid_CPU.Core.Registers.Retire;

namespace HybridCPU_ISE.Tests
{
    public class RegisterIdentityTests
    {
        [Fact]
        public void ArchRegId_CreateAcceptsFlatArchitecturalRange()
        {
            Assert.Equal(0, ArchRegId.Zero.Value);
            Assert.Equal(31, ArchRegId.Create(31).Value);
            Assert.Equal("x7", ArchRegId.Create(7).ToString());
        }

        [Fact]
        public void ArchRegId_CreateRejectsValuesOutsideArchitecturalRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ArchRegId.Create(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArchRegId.Create(32));
        }

        [Fact]
        public void VtId_CreateRejectsValuesOutsideFourWaySmtRange()
        {
            Assert.Equal(3, VtId.Create(3).Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => VtId.Create(4));
        }

        [Fact]
        public void PhysRegId_CreateRejectsValuesOutsidePrfRange()
        {
            Assert.Equal(PhysicalRegisterFile.TotalPhysRegs - 1, PhysRegId.Create(PhysicalRegisterFile.TotalPhysRegs - 1).Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => PhysRegId.Create(PhysicalRegisterFile.TotalPhysRegs));
        }

        [Fact]
        public void RenameAndCommitMapsSupportTypedRegisterIdentities()
        {
            var renameMap = new RenameMap(4);
            var commitMap = new CommitMap(4);
            VtId vt1 = VtId.Create(1);
            ArchRegId x5 = ArchRegId.Create(5);
            PhysRegId p42 = PhysRegId.Create(42);
            PhysRegId p99 = PhysRegId.Create(99);

            renameMap.Remap(vt1, x5, p42);
            Assert.Equal(p42, renameMap.Lookup(vt1, x5));

            commitMap.Commit(vt1, x5, p42);
            renameMap.Remap(vt1, x5, p99);
            commitMap.RestoreInto(renameMap);

            Assert.Equal(p42, commitMap.Lookup(vt1, x5));
            Assert.Equal(p42, renameMap.Lookup(vt1, x5));
        }

        [Fact]
        public void FreeListAndPrfSupportTypedPhysicalRegisterIdentities()
        {
            var freeList = new FreeList();
            var prf = new PhysicalRegisterFile();

            Assert.True(freeList.TryAllocate(out PhysRegId physReg));

            prf.Write(physReg, 0x1234_5678_9ABC_DEF0u);
            Assert.Equal(0x1234_5678_9ABC_DEF0u, prf.Read(physReg));

            freeList.Release(physReg);
        }

        [Fact]
        public void RetireCoordinatorCommitsTypedArchitecturalRegisterAccess()
        {
            var contexts = new[]
            {
                new ArchContextState(RenameMap.ArchRegs, 0),
                new ArchContextState(RenameMap.ArchRegs, 1),
                new ArchContextState(RenameMap.ArchRegs, 2),
                new ArchContextState(RenameMap.ArchRegs, 3),
            };

            var prf = new PhysicalRegisterFile();
            var renameMap = new RenameMap(4);
            var commitMap = new CommitMap(4);
            var retireCoordinator = new RetireCoordinator(prf, renameMap, commitMap, contexts);

            VtId vt2 = VtId.Create(2);
            ArchRegId x9 = ArchRegId.Create(9);
            PhysRegId p77 = PhysRegId.Create(77);

            renameMap.Remap(vt2, x9, p77);
            commitMap.Commit(vt2, x9, p77);
            retireCoordinator.Retire(RetireRecord.RegisterWrite(vt2.Value, x9.Value, 222UL));

            Assert.Equal(222UL, contexts[vt2.Value].CommittedRegs[x9.Value]);
            Assert.Equal(222UL, prf.Read(p77));
            Assert.Equal(p77, commitMap.Lookup(vt2, x9));
        }
    }
}
