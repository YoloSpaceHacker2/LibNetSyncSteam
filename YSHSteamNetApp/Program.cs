using YSHSteamNet;

// ---------------------------------------------------------------------------
// YSHSteamNetApp — une instance par participant.
//
// Stub   : isolation totale, pas de communication inter-process.
//   dotnet run --project YSHSteamNetApp -- --id 1001
//
// Custom : TCP local, 3 process sur la même machine (ou réseau local).
//   dotnet run --project YSHSteamNetApp -- --config configs/node_1001.json
//   dotnet run --project YSHSteamNetApp -- --config configs/node_1002.json
//   dotnet run --project YSHSteamNetApp -- --config configs/node_1003.json
//
// Steam  : Steam P2P réel, client Steam requis.
//   dotnet run --project YSHSteamNetApp -- --steam
// ---------------------------------------------------------------------------

// --- Parse args ---
ulong?  fixedId    = null;
string? configPath = null;
var     mode       = TransportMode.Stub;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--steam")
        mode = TransportMode.Steam;
    if (args[i] == "--config" && i + 1 < args.Length)
    {   mode = TransportMode.Custom;
        configPath = args[++i]; }
    if (args[i] == "--id" && i + 1 < args.Length && ulong.TryParse(args[i + 1], out var parsed))
        fixedId = parsed;
}

// --- Init ---
SteamManager.Init(mode, fixedId, configPath);

ITransport transport = SteamManager.Mode switch
{
    TransportMode.Custom => new TcpTransport(SteamManager.LocalId, SteamManager.Config!),
    _                    => new SteamTransport()
};

var net = new NetworkManager(SteamManager.LocalId, transport);

net.OnRemoteSpawn = (typeName, id, owner, payload) => typeName switch
{
    nameof(AppPlayer) => new AppPlayer(),
    _                 => null
};

// --- Boucle Update automatique (Custom / Steam) ---
// En mode Custom/Steam les messages arrivent en arrière-plan (TCP / Steam callbacks).
// Poll() + sync d'objets doivent être appelés périodiquement sans attendre une commande.
var cts = new CancellationTokenSource();
if (SteamManager.Mode != TransportMode.Stub)
{
    _ = Task.Run(async () =>
    {
        while (!cts.Token.IsCancellationRequested)
        {
            net.Update();
            await Task.Delay(100, cts.Token).ContinueWith(_ => { }); // absorbe TaskCanceledException
        }
    });
}

// --- Affichage ---
Console.WriteLine();
string modeLabel = SteamManager.Mode switch
{
    TransportMode.Steam  => "V1 Steam ",
    TransportMode.Custom => "Custom   ",
    _                    => "V0 Stub  "
};
Console.WriteLine("╔══════════════════════════════╗");
Console.WriteLine($"║   YSHSteamNetApp  {modeLabel,-10}║");
Console.WriteLine("╚══════════════════════════════╝");
Console.WriteLine($"  ID local : {net.LocalId}");
Console.WriteLine($"  Mode     : {SteamManager.Mode}");
if (SteamManager.Config != null)
    Console.WriteLine($"  Nom      : {SteamManager.Config.Name}  port:{SteamManager.Config.ListenPort}");
Console.WriteLine();
Console.WriteLine("  Commandes :");
Console.WriteLine("    connect <id>   — déclarer un peer (Custom: déclenche connexion TCP)");
Console.WriteLine("    spawn          — spawner un AppPlayer local");
Console.WriteLine("    move <valeur>  — déplacer le player local");
Console.WriteLine("    update         — déclencher la sync manuellement (Stub uniquement)");
Console.WriteLine("    status         — afficher peers et objets connus");
Console.WriteLine("    quit           — quitter");
Console.WriteLine();

// --- Boucle principale ---
AppPlayer? localPlayer = null;

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (line == null) break;

    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0) continue;

    switch (parts[0])
    {
        case "connect":
            if (parts.Length < 2 || !ulong.TryParse(parts[1], out var peerId))
            { Console.WriteLine("Usage: connect <id>"); break; }
            net.AddPeer(peerId);
            Console.WriteLine($"Peer {peerId} enregistré.");
            break;

        case "spawn":
            if (localPlayer != null)
            { Console.WriteLine("AppPlayer déjà spawné."); break; }
            localPlayer = net.Spawn(new AppPlayer { SyncInterval = 0.1f });
            Console.WriteLine($"AppPlayer spawné — netId={localPlayer.NetId}");
            break;

        case "move":
            if (localPlayer == null) { Console.WriteLine("Pas de player. Faites 'spawn' d'abord."); break; }
            if (parts.Length < 2 || !float.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var dx))
            { Console.WriteLine("Usage: move <valeur>"); break; }
            localPlayer.X += dx;
            Console.WriteLine($"X = {localPlayer.X}");
            break;

        case "update":
            net.Update();
            Console.WriteLine("Update envoyé.");
            break;

        case "status":
            Console.WriteLine($"Peers  : {net.Peers.Count}");
            Console.WriteLine($"Objets : {net.Objects.Count}");
            foreach (var obj in net.Objects.Values)
                Console.WriteLine($"  [{obj.NetId}] {obj.GetType().Name} owner={obj.Owner}  {obj}");
            break;

        case "quit":
            goto exit;

        default:
            Console.WriteLine($"Commande inconnue : {parts[0]}");
            break;
    }
}

exit:
cts.Cancel();
if (transport is TcpTransport tcp) tcp.Shutdown();
SteamManager.Shutdown();
Console.WriteLine("Au revoir.");
