using System;
using Steamworks;

namespace YSHSteamNet
{
    public enum TransportMode { Stub, Steam, Custom }

    // Wraps the network stack lifecycle: Init / RunCallbacks / Shutdown.
    //
    // Stub  : random LocalId, no inter-process communication. In-process only.
    // Steam : real SteamAPI — Steam client must be running and user logged in.
    // Custom: TCP sockets + JSON config — multi-process local testing without Steam.
    //
    // Typical call pattern:
    //   Startup  : SteamManager.Init(mode, ...)
    //   Per frame: networkManager.Update()  ← Poll() + object sync (covers RunCallbacks in Steam mode)
    //   Shutdown : SteamManager.Shutdown()
    public static class SteamManager
    {
        public static ulong         LocalId     { get; private set; }
        public static TransportMode Mode        { get; private set; }
        public static bool          Initialized { get; private set; }
        public static CustomConfig? Config      { get; private set; }

        // Stub  : fixedId overrides random ID (multi-instance on one machine without TCP).
        // Steam : fixedId ignored — ID comes from the Steam account.
        // Custom: configPath is required — loads localId, listenPort and peer addresses.
        public static void Init(TransportMode mode = TransportMode.Stub,
                                ulong?        fixedId    = null,
                                string?       configPath = null)
        {
            Mode = mode;

            switch (mode)
            {
                case TransportMode.Steam:
                    if (!SteamAPI.Init())
                        throw new Exception("SteamAPI.Init() failed — is the Steam client running and are you logged in?");
                    LocalId     = SteamUser.GetSteamID().m_SteamID;
                    Initialized = true;
                    break;

                case TransportMode.Custom:
                    if (configPath == null)
                        throw new ArgumentNullException(nameof(configPath), "Custom mode requires --config <path>");
                    Config      = CustomConfig.Load(configPath);
                    LocalId     = Config.LocalId;
                    Initialized = true;
                    break;

                default: // Stub
                    LocalId     = fixedId ?? (ulong)new Random().NextInt64(1, long.MaxValue);
                    Initialized = true;
                    break;
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
