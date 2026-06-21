namespace YAKSys_Hybrid_CPU.Core
{
    public sealed partial class VirtualInterruptFabric
    {
        public void ConfigureRemap(InterruptRemapEntry entry)
        {
            _remap.Configure(entry);
            AdvanceRoutingEpoch();
        }

        public bool RemoveRemap(InterruptRemapKey key)
        {
            bool removed = _remap.Remove(key);
            if (removed)
            {
                AdvanceRoutingEpoch();
            }

            return removed;
        }

        public void ClearRemaps()
        {
            ulong before = _remap.PolicyEpoch;
            _remap.Clear();
            if (_remap.PolicyEpoch != before)
            {
                AdvanceRoutingEpoch();
            }
        }
    }
}
