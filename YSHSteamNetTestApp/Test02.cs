using System.Linq;
using Xunit;
using YSHSteamNet;

/// <summary>
/// Test02 — Multi-type spawn (PlayerPos + MobPos) with factory dispatch.
/// Verifies that the SpawnFactory correctly distinguishes types by name,
/// that all objects are received by clients, and that updates sync correctly.
/// </summary>
public class Test02
{
    private const int MobCount = 10;

    private readonly NetworkManager _host, _client1, _client2;
    private readonly PlayerPos _player;
    private readonly MobPos[] _mobs;

    public Test02()
    {
        SteamManager.Init(TransportMode.Stub);

        var hub = new StubNetworkHub();
        _host    = new NetworkManager(1001, hub.CreateTransport(1001));
        _client1 = new NetworkManager(1002, hub.CreateTransport(1002));
        _client2 = new NetworkManager(1003, hub.CreateTransport(1003));

        ObjSync? Factory(string typeName, uint id, ulong owner, byte[] payload) => typeName switch
        {
            nameof(PlayerPos) => new PlayerPos(),
            nameof(MobPos)    => new MobPos(),
            _                 => null
        };
        _host.OnRemoteSpawn    = Factory;
        _client1.OnRemoteSpawn = Factory;
        _client2.OnRemoteSpawn = Factory;

        _host.AddPeer(1002);
        _host.AddPeer(1003);
        _client1.AddPeer(1001);
        _client2.AddPeer(1001);

        // SyncInterval=0 so Update() syncs immediately without Thread.Sleep
        _mobs = new MobPos[MobCount];
        for (int i = 0; i < MobCount; i++)
            _mobs[i] = _host.Spawn(new MobPos { SyncInterval = 0 });

        _player = _host.Spawn(new PlayerPos { SyncInterval = 0 });
    }

    [Fact]
    public void AllMobs_ReceivedByAllClients()
    {
        Assert.Equal(MobCount, _client1.Objects.Values.OfType<MobPos>().Count());
        Assert.Equal(MobCount, _client2.Objects.Values.OfType<MobPos>().Count());
    }

    [Fact]
    public void Player_ReceivedByAllClients()
    {
        Assert.Single(_client1.Objects.Values.OfType<PlayerPos>());
        Assert.Single(_client2.Objects.Values.OfType<PlayerPos>());
    }

    [Fact]
    public void FactoryDispatch_MobsAreNotPlayerPos()
    {
        Assert.All(
            _client1.Objects.Values.OfType<MobPos>(),
            mob => Assert.IsNotType<PlayerPos>(mob)
        );
    }

    [Fact]
    public void MobNetIds_MatchBetweenHostAndClients()
    {
        var hostIds    = _mobs.Select(m => m.NetId).OrderBy(x => x).ToArray();
        var client1Ids = _client1.Objects.Values.OfType<MobPos>().Select(m => m.NetId).OrderBy(x => x).ToArray();

        Assert.Equal(hostIds, client1Ids);
    }

    [Fact]
    public void PlayerUpdate_SyncsToAllClients()
    {
        PlayerPos GetClientPlayer(NetworkManager nm) =>
            nm.Objects.Values.OfType<PlayerPos>().First();

        for (int i = 1; i <= 10; i++)
        {
            _player.X = i;
            _host.Update();

            Assert.Equal(_player.X, GetClientPlayer(_client1).X);
            Assert.Equal(_player.X, GetClientPlayer(_client2).X);
        }
    }

    [Fact]
    public void EachMobUpdate_SyncsToClients()
    {
        MobPos? GetClientMob(NetworkManager nm, uint netId) =>
            nm.Objects.Values.OfType<MobPos>().FirstOrDefault(m => m.NetId == netId);

        for (int i = 0; i < MobCount; i++)
        {
            _mobs[i].X = i + 1;
            _host.Update();

            Assert.Equal(_mobs[i].X, GetClientMob(_client1, _mobs[i].NetId)?.X);
            Assert.Equal(_mobs[i].X, GetClientMob(_client2, _mobs[i].NetId)?.X);
        }
    }
}
