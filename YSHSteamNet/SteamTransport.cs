using System;
using System.Runtime.InteropServices;
using Steamworks;

namespace YSHSteamNet
{
    // Steam P2P transport via ISteamNetworkingMessages.
    //
    // Session model: SendMessageToUser() opens a session implicitly on first send.
    // The remote peer receives SteamNetworkingMessagesSessionRequest_t and we auto-accept it.
    //
    // All Steam calls are guarded by SteamManager.Mode — safe to instantiate in Stub mode.
    public class SteamTransport : ITransport
    {
        private const int Channel       = 0;
        private const int MaxMsgPerPoll = 64;

        public Action<ulong, byte[]>? OnReceive { get; set; }

        // Kept as fields — GC would silence callbacks if declared as locals.
        private Callback<SteamNetworkingMessagesSessionRequest_t>? _sessionRequest;
        private Callback<SteamNetworkingMessagesSessionFailed_t>?  _sessionFailed;

        public SteamTransport()
        {
            if (SteamManager.Mode != TransportMode.Steam) return;

            _sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>
                .Create(OnSessionRequest);
            _sessionFailed  = Callback<SteamNetworkingMessagesSessionFailed_t>
                .Create(OnSessionFailed);
        }

        // reliable=true  → k_nSteamNetworkingSend_Reliable   (ordered, retried, ~TCP)
        // reliable=false → k_nSteamNetworkingSend_Unreliable (fire-and-forget, ~UDP)
        public void Send(ulong target, byte[] data, bool reliable = true)
        {
            if (SteamManager.Mode != TransportMode.Steam) return;

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(target);

            int flags = reliable
                ? Constants.k_nSteamNetworkingSend_Reliable
                : Constants.k_nSteamNetworkingSend_Unreliable;

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                EResult result = SteamNetworkingMessages.SendMessageToUser(
                    ref identity, handle.AddrOfPinnedObject(), (uint)data.Length, flags, Channel);

                if (result != EResult.k_EResultOK)
                    Console.WriteLine($"[SteamTransport] SendMessageToUser → {result}");
            }
            finally
            {
                handle.Free();
            }
        }

        // Drain all pending messages on Channel and dispatch via OnReceive.
        // NetworkManager.Update() calls this automatically every frame.
        public void Poll()
        {
            if (SteamManager.Mode != TransportMode.Steam) return;

            SteamAPI.RunCallbacks();

            // ReceiveMessagesOnChannel returns native pointers (nint) to
            // SteamNetworkingMessage_t structs allocated by Steam.
            // We use unsafe ref to the original unmanaged memory so that
            // Release() passes the correct this* to the native destructor.
            var pMessages = new nint[MaxMsgPerPoll];
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(
                Channel, pMessages, MaxMsgPerPoll);

            unsafe
            {
                for (int i = 0; i < count; i++)
                {
                    ref var msg = ref *(SteamNetworkingMessage_t*)pMessages[i];

                    ulong  senderId = msg.m_identityPeer.GetSteamID64();
                    byte[] data     = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                    OnReceive?.Invoke(senderId, data);

                    msg.Release(); // frees the Steam-side buffer via original pointer
                }
            }
        }

        // Called by NetworkManager.RemovePeer — frees the Steam session.
        public void CloseSession(ulong peerId)
        {
            if (SteamManager.Mode != TransportMode.Steam) return;

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(peerId);
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
        }

        // Auto-accept all incoming session requests.
        // For a lobby-gated game, verify param.m_identityRemote is a known lobby member first.
        private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t param)
        {
            SteamNetworkingMessages.AcceptSessionWithUser(ref param.m_identityRemote);
        }

        private void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t param)
        {
            ulong peerId = param.m_info.m_identityRemote.GetSteamID64();
            Console.WriteLine($"[SteamTransport] Session failed with {peerId}: {param.m_info.m_eState}");
        }
    }
}
