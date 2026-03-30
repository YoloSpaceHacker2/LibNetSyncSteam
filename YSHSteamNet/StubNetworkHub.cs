using System.Collections.Concurrent;

namespace YSHSteamNet
{
    // Routes messages between StubTransports — simulates a network without real Steam.
    // Each node registers with CreateTransport(localId); Send() delivers to the target's OnReceive.
    public class StubNetworkHub
    {
        private readonly ConcurrentDictionary<ulong, StubTransport> _transports = new();

        public StubTransport CreateTransport(ulong localId)
        {
            var t = new StubTransport(localId, this);
            _transports[localId] = t;
            return t;
        }

        internal void Deliver(ulong from, ulong to, byte[] data)
        {
            if (_transports.TryGetValue(to, out var t))
                t.OnReceive?.Invoke(from, data);
        }
    }
}
