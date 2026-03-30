using System;
using Steamworks;

namespace YSHSteamNet
{
    public enum TransportMode { Stub, Steam }

    // Wraps the Steam lifecycle: Init / RunCallbacks / Shutdown.
    //
    // V0 (Stub): random LocalId, no Steam dependency.
    // V1 (Steam): real SteamAPI — Steam client must be running and user logged in.
    //
    // Typical call pattern:
    //   Startup  : SteamManager.Init(TransportMode.Steam)
    //   Per frame: networkManager.Update()   ← calls SteamTransport.Poll() → RunCallbacks() internally
    //   Shutdown : SteamManager.Shutdown()
    public static class SteamManager
    {
        public static ulong         LocalId     { get; private set; }
        public static TransportMode Mode        { get; private set; }
        public static bool          Initialized { get; private set; }

        // fixedId: override the random stub ID (multi-instance testing on one machine).
        // Ignored in Steam mode — ID always comes from the logged-in Steam account.
        public static void Init(TransportMode mode = TransportMode.Stub, ulong? fixedId = null)
        {
            Mode = mode;

            if (mode == TransportMode.Steam)
            {
                if (!SteamAPI.Init())
                    throw new Exception("SteamAPI.Init() failed — is the Steam client running and are you logged in?");

                LocalId     = SteamUser.GetSteamID().m_SteamID;
                Initialized = true;
            }
            else
            {
                LocalId     = fixedId ?? (ulong)new Random().NextInt64(1, long.MaxValue);
                Initialized = true;
            }

            Console.WriteLine($"[SteamManager] Init({mode}) LocalId={LocalId}");
        }

        // Fires all queued Steam callbacks. Not needed if you use NetworkManager.Update(),
        // which calls SteamTransport.Poll() → SteamAPI.RunCallbacks() automatically.
        public static void RunCallbacks()
        {
            if (Initialized && Mode == TransportMode.Steam)
                SteamAPI.RunCallbacks();
        }

        public static void Shutdown()
        {
            if (Initialized && Mode == TransportMode.Steam)
                SteamAPI.Shutdown();
            Initialized = false;
        }
    }
}
