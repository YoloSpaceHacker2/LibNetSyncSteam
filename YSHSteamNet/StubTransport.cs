using System;

namespace YSHSteamNet
{
    public class StubTransport : ITransport
    {
        private readonly ulong _localId;
        private readonly StubNetworkHub _hub;

        public Action<ulong, byte[]>? OnReceive { get; set; }

        internal StubTransport(ulong localId, StubNetworkHub hub)
        {
            _localId = localId;
            _hub = hub;
        }

        public void Send(ulong target, byte[] data, bool reliable = true)
        {
            _hub.Deliver(_localId, target, data);
        }

        // Stub delivery is synchronous — no polling needed.
        public void Poll() { }
        public void CloseSession(ulong peerId) { }
    }
}
