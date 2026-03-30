using System.Linq;
using Xunit;
using YSHSteamNet;

/// <summary>
/// Test01 — Basic spawn + update sync for a single object type (PlayerPos).
/// Verifies the core sync pipeline: host spawns, clients receive; host updates, clients converge.
/// </summary>
public class Test01
{
    private readonly NetworkManager _host, _client1, _client2;
    private readonly PlayerPos _player;

    public Test01()
    {
        SteamManager.Init(TransportMode.Stub);

        var hub = new StubNetworkHub();
        _host    = new NetworkManager(1001, hub.CreateTransport(1001));
        _client1 = new NetworkManager(1002, hub.CreateTransport(1002));
        _client2 = new NetworkManager(1003, hub.CreateTransport(1003));

        ObjSync? Factory(string _, uint id, ulong owner, byte[] payload) => new PlayerPos();
        _host.OnRemoteSpawn    = Factory;
        _client1.OnRemoteSpawn = Factory;
        _client2.OnRemoteSpawn = Factory;

        _host.AddPeer(1002);
        _host.AddPeer(1003);
        _client1.AddPeer(1001);
        _client2.AddPeer(1001);

        // SyncInterval=0 so Update() syncs immediately without Thread.Sleep
        _player = _host.Spawn(new PlayerPos { SyncInterval = 0 });
    }

    [Fact]
    public void SpawnedPlayer_IsRegisteredOnAllNodes()
    {
        Assert.Single(_host.Objects);
        Assert.Single(_client1.Objects);
        Assert.Single(_client2.Objects);
    }

    [Fact]
    public void SpawnedPlayer_HasCorrectMetadataOnClients()
    {
        var c1 = _client1.Objects.Values.OfType<PlayerPos>().First();
        var c2 = _client2.Objects.Values.OfType<PlayerPos>().First();

        Assert.Equal(_player.NetId, c1.NetId);
        Assert.Equal(_player.NetId, c2.NetId);
        Assert.Equal(_player.Owner, c1.Owner);
        Assert.Equal(_player.Owner, c2.Owner);
    }

    [Fact]
    public void SpawnedPlayer_InitialValueSyncedToClients()
    {
        var c1 = _client1.Objects.Values.OfType<PlayerPos>().First();
        var c2 = _client2.Objects.Values.OfType<PlayerPos>().First();

        Assert.Equal(_player.X, c1.X);
        Assert.Equal(_player.X, c2.X);
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
}
