using System;

namespace YSHSteamNet
{
    public interface ITransport
    {
        Action<ulong, byte[]>? OnReceive { get; set; }
        void Send(ulong target, byte[] data, bool reliable = true);
        // Called every frame — drains incoming messages and dispatches them via OnReceive.
        // No-op for StubTransport (synchronous delivery). In SteamTransport, calls
        // SteamNetworkingMessages.ReceiveMessagesOnChannel() then SteamAPI.RunCallbacks().
        void Poll();
        void CloseSession(ulong peerId); // no-op for StubTransport, CloseSessionWithUser for Steam
    }
}
