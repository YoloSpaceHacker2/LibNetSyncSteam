using System.Linq;
using Xunit;
using YSHSteamNet;

/// <summary>
/// Test03 — Spawn depuis un client non-host (client1), réplication vers tous les participants.
///
/// Topologie : full mesh (chaque node connecté à tous les autres).
///   host ↔ client1 ↔ client2 ↔ host
///
/// En topologie étoile (Test01/02), client1 n'a que le host comme peer :
/// ses broadcasts n'atteindraient pas client2 directement.
/// Le full mesh reflète le modèle P2P Steam où chaque participant
/// est connecté à tous les autres via SteamNetworkingMessages.
/// </summary>
public class Test03
{
    private const int MobCount = 5;

    private readonly NetworkManager _host, _client1, _client2;
    private readonly MobPos[] _mobs;

    public Test03()
    {
        SteamManager.Init(TransportMode.Stub);

        var hub = new StubNetworkHub();
        _host    = new NetworkManager(1001, hub.CreateTransport(1001));
        _client1 = new NetworkManager(1002, hub.CreateTransport(1002));
        _client2 = new NetworkManager(1003, hub.CreateTransport(1003));

        ObjSync? Factory(string typeName, uint id, ulong owner, byte[] payload) => typeName switch
        {
            nameof(MobPos) => new MobPos(),
            _              => null
        };
        _host.OnRemoteSpawn    = Factory;
        _client1.OnRemoteSpawn = Factory;
        _client2.OnRemoteSpawn = Factory;

        // Full mesh — chaque node déclare les deux autres comme peers
        _host.AddPeer(1002);    _host.AddPeer(1003);
        _client1.AddPeer(1001); _client1.AddPeer(1003);
        _client2.AddPeer(1001); _client2.AddPeer(1002);

        // C'est client1 qui spawne les mobs (pas le host)
        _mobs = new MobPos[MobCount];
        for (int i = 0; i < MobCount; i++)
            _mobs[i] = _client1.Spawn(new MobPos { SyncInterval = 0 });
    }

    [Fact]
    public void Mobs_SpawnedByClient1_ReceivedByHost()
    {
        Assert.Equal(MobCount, _host.Objects.Values.OfType<MobPos>().Count());
    }

    [Fact]
    public void Mobs_SpawnedByClient1_ReceivedByClient2()
    {
        Assert.Equal(MobCount, _client2.Objects.Values.OfType<MobPos>().Count());
    }

    [Fact]
    public void Mobs_Owner_IsClient1OnAllNodes()
    {
        const ulong client1Id = 1002;

        Assert.All(_host.Objects.Values.OfType<MobPos>(),
            mob => Assert.Equal(client1Id, mob.Owner));

        Assert.All(_client2.Objects.Values.OfType<MobPos>(),
            mob => Assert.Equal(client1Id, mob.Owner));
    }

    [Fact]
    public void MobNetIds_AreConsistentAcrossAllNodes()
    {
        var hostIds    = _host.Objects.Values.OfType<MobPos>()
                             .Select(m => m.NetId).OrderBy(x => x).ToArray();
        var client2Ids = _client2.Objects.Values.OfType<MobPos>()
                             .Select(m => m.NetId).OrderBy(x => x).ToArray();
        var client1Ids = _mobs.Select(m => m.NetId).OrderBy(x => x).ToArray();

        Assert.Equal(client1Ids, hostIds);
        Assert.Equal(client1Ids, client2Ids);
    }

    [Fact]
    public void MobUpdate_ByClient1_SyncsToAllPeers()
    {
        MobPos? GetMob(NetworkManager nm, uint netId) =>
            nm.Objects.Values.OfType<MobPos>().FirstOrDefault(m => m.NetId == netId);

        for (int i = 0; i < MobCount; i++)
        {
            _mobs[i].X = (i + 1) * 10f;
            _client1.Update();

            Assert.Equal(_mobs[i].X, GetMob(_host,    _mobs[i].NetId)?.X);
            Assert.Equal(_mobs[i].X, GetMob(_client2, _mobs[i].NetId)?.X);
        }
    }

    [Fact]
    public void Client1_HasNoRemoteDuplicates()
    {
        // client1 owns the objects — OnRemoteSpawn is never called on its own broadcasts
        Assert.Equal(MobCount, _client1.Objects.Count);
    }

    [Fact]
    public void Host_DoesNotOwnClientMobs()
    {
        // Update() on host must not broadcast mobs it doesn't own
        Assert.DoesNotContain(_host.Objects.Values, obj => obj.Owner == 1001);
    }
}
