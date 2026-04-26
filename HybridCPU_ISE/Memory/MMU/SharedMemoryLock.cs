using System;
using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Memory
{
    /// <summary>
    /// Hardware-agnostic mutex/lock for shared memory regions (Phase 5 extension)
    ///
    /// Design principles:
    /// - HLS-compatible: simple spin-lock implementation
    /// - Deterministic: no OS-level primitives
    /// - Thread-safe: uses atomic operations for lock state
    /// - Singularity-style: explicit locking required for shared regions
    ///
    /// Usage:
    /// 1. Create lock for shared memory region
    /// 2. Each thread calls AcquireLock(threadId) before accessing
    /// 3. Thread calls ReleaseLock(threadId) after access
    /// 4. Lock tracks current owner for verification
    /// </summary>
    public class SharedMemoryLock
    {
        private volatile int _lockOwner = -1; // -1 = unlocked, 0-15 = thread ID
        private readonly object _syncRoot = new object();
        private readonly int _sharedRegionId;
        private readonly ulong _baseAddress;
        private readonly ulong _size;

        public int SharedRegionId => _sharedRegionId;
        public ulong BaseAddress => _baseAddress;
        public ulong Size => _size;
        public int LockOwner => _lockOwner;
        public bool IsLocked => _lockOwner >= 0;

        public SharedMemoryLock(int sharedRegionId, ulong baseAddress, ulong size)
        {
            _sharedRegionId = sharedRegionId;
            _baseAddress = baseAddress;
            _size = size;
        }

        /// <summary>
        /// Acquire lock for thread (blocking)
        /// In hardware synthesis: would translate to test-and-set operation
        /// </summary>
        /// <param name="threadId">Thread ID requesting lock (0-15)</param>
        /// <returns>True if lock acquired</returns>
        public bool AcquireLock(int threadId)
        {
            if (threadId < 0 || threadId > 15)
                return false;

            lock (_syncRoot)
            {
                if (_lockOwner == -1)
                {
                    _lockOwner = threadId;
                    return true;
                }
                return false; // Already locked
            }
        }

        /// <summary>
        /// Try to acquire lock (non-blocking)
        /// Returns immediately if lock unavailable
        /// </summary>
        /// <param name="threadId">Thread ID requesting lock (0-15)</param>
        /// <returns>True if lock acquired, false if already held</returns>
        public bool TryAcquireLock(int threadId)
        {
            return AcquireLock(threadId);
        }

        /// <summary>
        /// Release lock held by thread
        /// Only the owning thread can release the lock
        /// </summary>
        /// <param name="threadId">Thread ID releasing lock (0-15)</param>
        /// <returns>True if lock released, false if not owner</returns>
        public bool ReleaseLock(int threadId)
        {
            lock (_syncRoot)
            {
                if (_lockOwner == threadId)
                {
                    _lockOwner = -1;
                    return true;
                }
                return false; // Not owner
            }
        }

        /// <summary>
        /// Check if thread owns this lock
        /// </summary>
        public bool IsOwnedBy(int threadId)
        {
            return _lockOwner == threadId;
        }

        /// <summary>
        /// Force unlock (for debugging/recovery)
        /// </summary>
        public void ForceUnlock()
        {
            lock (_syncRoot)
            {
                _lockOwner = -1;
            }
        }
    }

    /// <summary>
    /// Manager for shared memory locks (Phase 5 extension)
    /// Maintains locks for all shared memory regions
    /// </summary>
    public static class SharedMemoryLockManager
    {
        private static Dictionary<int, SharedMemoryLock>? _locks;

        public static void Initialize()
        {
            _locks = new Dictionary<int, SharedMemoryLock>();
        }

        /// <summary>
        /// Create lock for shared memory region
        /// </summary>
        public static bool CreateLock(int sharedRegionId, ulong baseAddress, ulong size)
        {
            if (_locks == null) Initialize();

            if (_locks!.ContainsKey(sharedRegionId))
                return false; // Lock already exists

            _locks[sharedRegionId] = new SharedMemoryLock(sharedRegionId, baseAddress, size);
            return true;
        }

        /// <summary>
        /// Get lock for shared region
        /// </summary>
        public static SharedMemoryLock? GetLock(int sharedRegionId)
        {
            if (_locks == null || !_locks.ContainsKey(sharedRegionId))
                return null;

            return _locks[sharedRegionId];
        }

        /// <summary>
        /// Acquire lock for thread (blocking)
        /// </summary>
        public static bool AcquireLock(int sharedRegionId, int threadId)
        {
            var lock_ = GetLock(sharedRegionId);
            if (lock_ == null) return false;

            return lock_.AcquireLock(threadId);
        }

        /// <summary>
        /// Release lock for thread
        /// </summary>
        public static bool ReleaseLock(int sharedRegionId, int threadId)
        {
            var lock_ = GetLock(sharedRegionId);
            if (lock_ == null) return false;

            return lock_.ReleaseLock(threadId);
        }

        /// <summary>
        /// Check if region is locked
        /// </summary>
        public static bool IsRegionLocked(int sharedRegionId)
        {
            var lock_ = GetLock(sharedRegionId);
            return lock_?.IsLocked ?? false;
        }

        /// <summary>
        /// Get current owner of lock
        /// </summary>
        public static int GetLockOwner(int sharedRegionId)
        {
            var lock_ = GetLock(sharedRegionId);
            return lock_?.LockOwner ?? -1;
        }

        /// <summary>
        /// Remove lock for shared region
        /// </summary>
        public static bool RemoveLock(int sharedRegionId)
        {
            if (_locks == null || !_locks.ContainsKey(sharedRegionId))
                return false;

            _locks.Remove(sharedRegionId);
            return true;
        }

        /// <summary>
        /// Get all active locks (for debugging)
        /// </summary>
        public static IEnumerable<SharedMemoryLock> GetAllLocks()
        {
            if (_locks == null) return Array.Empty<SharedMemoryLock>();
            return _locks.Values;
        }
    }
}
