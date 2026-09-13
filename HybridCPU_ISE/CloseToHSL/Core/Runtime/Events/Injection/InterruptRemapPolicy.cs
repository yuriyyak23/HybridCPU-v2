using System.Collections.Generic;

namespace YAKSys_Hybrid_CPU.Core
{
    public enum InterruptPostDisposition : byte
    {
        None = 0,
        Queued = 1,
        RemappedQueued = 2,
        Coalesced = 3,
        RemappedCoalesced = 4,
        DroppedInvalid = 5,
        DroppedQueueFull = 6,
        DroppedByRemap = 7,
    }

    public enum InterruptRemapAction : byte
    {
        Route = 0,
        Drop = 1,
    }

    public readonly record struct InterruptRemapKey(
        EventInjectionKind Kind,
        byte SourceVector,
        ushort SourceExecutionDomainTag)
    {
        public const ushort AnyExecutionDomainTag = ushort.MaxValue;

        public bool Matches(EventInjectionDescriptor descriptor) =>
            descriptor.IsValid &&
            Kind == descriptor.Kind &&
            SourceVector == descriptor.Vector &&
            (SourceExecutionDomainTag == AnyExecutionDomainTag ||
             SourceExecutionDomainTag == descriptor.ExecutionDomainTag);
    }

    public readonly record struct InterruptRemapEntry(
        InterruptRemapKey Key,
        InterruptRemapAction Action,
        byte TargetVector,
        byte TargetVtId,
        ushort TargetExecutionDomainTag,
        ushort TargetAddressSpaceTag,
        byte Priority,
        bool OverridePriority,
        bool CoalescePosted)
    {
        public bool IsDrop => Action == InterruptRemapAction.Drop;

        public static InterruptRemapEntry Route(
            EventInjectionKind kind,
            byte sourceVector,
            byte targetVector,
            byte targetVtId,
            ushort targetExecutionDomainTag,
            ushort targetAddressSpaceTag = 0,
            byte priority = 0,
            bool overridePriority = true,
            bool coalescePosted = true,
            ushort sourceExecutionDomainTag = InterruptRemapKey.AnyExecutionDomainTag) =>
            new(
                new InterruptRemapKey(kind, sourceVector, sourceExecutionDomainTag),
                InterruptRemapAction.Route,
                targetVector,
                targetVtId,
                targetExecutionDomainTag,
                targetAddressSpaceTag,
                priority,
                overridePriority,
                coalescePosted);

        public static InterruptRemapEntry Drop(
            EventInjectionKind kind,
            byte sourceVector,
            ushort sourceExecutionDomainTag = InterruptRemapKey.AnyExecutionDomainTag) =>
            new(
                new InterruptRemapKey(kind, sourceVector, sourceExecutionDomainTag),
                InterruptRemapAction.Drop,
                TargetVector: 0,
                TargetVtId: 0,
                TargetExecutionDomainTag: 0,
                TargetAddressSpaceTag: 0,
                Priority: 0,
                OverridePriority: false,
                CoalescePosted: false);

        public bool Matches(EventInjectionDescriptor descriptor) =>
            Key.Matches(descriptor);

        public EventInjectionDescriptor Apply(EventInjectionDescriptor descriptor)
        {
            if (IsDrop)
            {
                return default;
            }

            return descriptor with
            {
                Vector = TargetVector,
                TargetVtId = TargetVtId,
                ExecutionDomainTag = TargetExecutionDomainTag,
                AddressSpaceTag = TargetAddressSpaceTag,
                Priority = OverridePriority ? Priority : descriptor.Priority,
            };
        }
    }

    public sealed class InterruptRemapTable
    {
        private readonly List<InterruptRemapEntry> _entries = new();

        public int Count => _entries.Count;

        public ulong PolicyEpoch { get; private set; }

        public void Configure(InterruptRemapEntry entry)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Key == entry.Key)
                {
                    _entries[index] = entry;
                    AdvancePolicyEpoch();
                    return;
                }
            }

            _entries.Add(entry);
            AdvancePolicyEpoch();
        }

        public bool Remove(InterruptRemapKey key)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index].Key == key)
                {
                    _entries.RemoveAt(index);
                    AdvancePolicyEpoch();
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();
            AdvancePolicyEpoch();
        }

        public bool TryRoute(
            EventInjectionDescriptor descriptor,
            out EventInjectionDescriptor routed,
            out bool remapped,
            out bool coalescePosted,
            out InterruptPostDisposition disposition)
        {
            routed = descriptor;
            remapped = false;
            coalescePosted = true;
            disposition = InterruptPostDisposition.None;

            if (!descriptor.IsValid)
            {
                disposition = InterruptPostDisposition.DroppedInvalid;
                return false;
            }

            if (!TryFindEntry(descriptor, out InterruptRemapEntry entry))
            {
                return true;
            }

            remapped = true;
            if (entry.IsDrop)
            {
                routed = default;
                coalescePosted = false;
                disposition = InterruptPostDisposition.DroppedByRemap;
                return false;
            }

            routed = entry.Apply(descriptor);
            coalescePosted = entry.CoalescePosted;
            return true;
        }

        private bool TryFindEntry(
            EventInjectionDescriptor descriptor,
            out InterruptRemapEntry entry)
        {
            for (int index = 0; index < _entries.Count; index++)
            {
                InterruptRemapEntry candidate = _entries[index];
                if (candidate.Key.SourceExecutionDomainTag != InterruptRemapKey.AnyExecutionDomainTag &&
                    candidate.Matches(descriptor))
                {
                    entry = candidate;
                    return true;
                }
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                InterruptRemapEntry candidate = _entries[index];
                if (candidate.Key.SourceExecutionDomainTag == InterruptRemapKey.AnyExecutionDomainTag &&
                    candidate.Matches(descriptor))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        private void AdvancePolicyEpoch()
        {
            unchecked
            {
                PolicyEpoch++;
            }
        }
    }
}
