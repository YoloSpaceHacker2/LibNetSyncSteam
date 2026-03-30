using System;
using YSHSteamNet;

// ---------------------------------------------------------------------------
// YSHSteamNetApp — cible : 3 instances indépendantes, une par compte Steam.
//
// V0 (Stub) : le transport ne transmet rien entre les processus.
//             Chaque instance tourne en isolation — utile pour valider
//             l'initialisation, la structure et la CLI avant V1.
//
// V1 (Steam): remplacer SteamTransport.Send() par l'appel Steamworks.NET.
//             Rien d'autre ne change côté app.
//
// Lancer 3 instances (V0, IDs fixes pour repro) :
//   dotnet run --project YSHSteamNetApp -- --id 1001
//   dotnet run --project YSHSteamNetApp -- --id 1002
//   dotnet run --project YSHSteamNetApp -- --id 1003
// ---------------------------------------------------------------------------

// --- Parse args ---
// --steam      use real Steam transport (Steam client must be running)
// --id <val>   fix the local ID in Stub mode (simulate multiple accounts on one machine)
ulong? fixedId = null;
var mode = TransportMode.Stub;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--steam")
        mode = TransportMode.Steam;
    if (args[i] == "--id" && i + 1 < args.Length && ulong.TryParse(args[i + 1], out var parsed))
        fixedId = parsed;
}

// --- Init ---
SteamManager.Init(mode, fixedId);
var transport = new SteamTransport();
var net = new NetworkManager(SteamManager.LocalId, transport);

net.OnRemoteSpawn = (typeName, id, owner, payload) => typeName switch
{
    nameof(AppPlayer) => new AppPlayer(),
    _                 => null
};

// --- Affichage ---
Console.WriteLine();
string modeLabel = SteamManager.Mode == TransportMode.Steam ? "V1 Steam" : "V0 Stub ";
Console.WriteLine("╔══════════════════════════════╗");
Console.WriteLine($"║   YSHSteamNetApp  {modeLabel,-10}║");
Console.WriteLine("╚══════════════════════════════╝");
Console.WriteLine($"  ID local : {net.LocalId}");
Console.WriteLine($"  Mode     : {SteamManager.Mode}");
Console.WriteLine();
Console.WriteLine("  Commandes :");
Console.WriteLine("    connect <id>   — déclarer un peer");
Console.WriteLine("    spawn          — spawner un AppPlayer local");
Console.WriteLine("    move <valeur>  — déplacer le player local");
Console.WriteLine("    update         — déclencher la sync");
Console.WriteLine("    status         — afficher les objets connus");
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
            {
                Console.WriteLine("Usage: connect <id>");
                break;
            }
            net.AddPeer(peerId);
            Console.WriteLine($"Peer {peerId} enregistré.");
            break;

        case "spawn":
            if (localPlayer != null)
            {
                Console.WriteLine("AppPlayer déjà spawné.");
                break;
            }
            localPlayer = net.Spawn(new AppPlayer { SyncInterval = 0.1f });
            Console.WriteLine($"AppPlayer spawné — netId={localPlayer.NetId}");
            break;

        case "move":
            if (localPlayer == null) { Console.WriteLine("Pas de player. Faites 'spawn' d'abord."); break; }
            if (parts.Length < 2 || !float.TryParse(parts[1], out var dx)) { Console.WriteLine("Usage: move <valeur>"); break; }
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
SteamManager.Shutdown();
Console.WriteLine("Au revoir.");
