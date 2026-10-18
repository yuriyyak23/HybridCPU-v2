using HybridCPU.ExternalRuntime.Contracts;

namespace HybridCPU.ExternalRuntime;

/// <summary>
/// Provider-local bounded admission work slots. These are not semantic grants,
/// backend entitlements, machine certificates or issue permits.
/// </summary>
internal sealed class SecureRegisterLocalAdmissionReservations
{
    // Construction/copy is not authority: the issuing owner's live instance map
    // is checked on every transition, with the exact immutable original request.
    internal sealed class Reservation { }
    private sealed class Entry(ExternalSecureRegisterLocalRequest request)
    {
        internal ExternalSecureRegisterLocalRequest Request { get; } = request;
        internal bool Consumed;
        internal bool Invalidated;
    }

    private readonly object sync = new();
    private readonly int capacity;
    private readonly Dictionary<Reservation, Entry> live = [];

    internal SecureRegisterLocalAdmissionReservations(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    internal bool TryReserve(ExternalSecureRegisterLocalRequest request, out Reservation? reservation)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (sync)
        {
            reservation = null;
            if (live.Count >= capacity || live.Values.Any(entry => entry.Request.Operation == request.Operation))
                return false;
            reservation = new Reservation();
            live.Add(reservation, new Entry(request));
            return true;
        }
    }

    internal bool TryConsume(Reservation reservation, ExternalSecureRegisterLocalRequest request)
    {
        lock (sync)
        {
            if (!live.TryGetValue(reservation, out Entry? entry) || entry.Consumed || entry.Invalidated ||
                !ReferenceEquals(entry.Request, request)) return false;
            entry.Consumed = true;
            return true;
        }
    }

    internal bool Release(Reservation reservation, ExternalSecureRegisterLocalRequest request)
    {
        lock (sync)
        {
            if (!live.TryGetValue(reservation, out Entry? entry) || !ReferenceEquals(entry.Request, request))
                return false;
            return live.Remove(reservation);
        }
    }

    internal void Invalidate(ExternalSecureDomainLease domain)
    {
        lock (sync)
        {
            foreach (var entry in live.Values.Where(entry => entry.Request.Binding.SecureDomain.Equals(domain)))
                entry.Invalidated = true;
        }
    }
}
