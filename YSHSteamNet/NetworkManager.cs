using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace YSHSteamNet
{
    public class NetworkManager
    {
        private readonly ConcurrentDictionary<uint, ObjSync> _objects = new();
        public IReadOnlyDictionary<uint, ObjSync> Objects => _objects;
        public IReadOnlyDictionary<ulong, Peer> Peers => _peers;
        private readonly ConcurrentDictionary<ulong, Peer> _peers = new();
        private int _nextId = 0;

        public ulong LocalId { get; }
        public ITransport Transport { get; }

        // Provide this factory so the NetworkManager can instantiate received remote objects.
        // Called with (typeName, netId, ownerId, initialPayload) — return the new ObjSync, or null to ignore.
        public Func<string, uint, ulong, byte[], ObjSync?>? OnRemoteSpawn;

        public NetworkManager(ulong localId, ITransport transport)
        {
            LocalId = localId;
            Transport = transport;
            Transport.OnReceive = OnReceive;
        }

        // Register a peer. Immediately sends the current full object state as a handshake.
        public void AddPeer(ulong peerId)
        {
            _peers[peerId] = new Peer { SteamId = peerId };
            SendFullStateTo(peerId);
        }

        public void RemovePeer(ulong peerId)
        {
            if (_peers.TryGetValue(peerId, out var p))
            {
                p.Connected = false;
                Transport.CloseSession(peerId);
            }
        }

        // Spawn a local object and broadcast it to all peers.
        public T Spawn<T>(T obj) where T : ObjSync
        {
            obj.NetId = (uint)Interlocked.Increment(ref _nextId);
            obj.Owner = LocalId;
            _objects[obj.NetId] = obj;
            Broadcast(BuildSpawnMsg(obj));
            return obj;
        }

        public void Despawn(uint id)
        {
            if (_objects.TryRemove(id, out _))
                Broadcast(BuildMsg(MsgType.Despawn, id, LocalId, null));
        }

        // Call this from your game/app loop.
        // 1. Polls incoming messages (SteamTransport: drains ISteamNetworkingMessages + RunCallbacks).
        // 2. Syncs locally owned objects that have changed.
        public void Update()
        {
            Transport.Poll();

            foreach (var obj in _objects.Values)
            {
                if (obj.Owner == LocalId && obj.ShouldSync())
                    Broadcast(BuildMsg(MsgType.Update, obj.NetId, LocalId, obj.Build()));
            }
        }

        private void SendFullStateTo(ulong peerId)
        {
            foreach (var obj in _objects.Values)
                Transport.Send(peerId, BuildSpawnMsg(obj));
        }

        // Spawn payload: typeNameLen(2) + typeName(UTF8) + userData(N)
        private static byte[] BuildSpawnMsg(ObjSync obj)
        {
            var typeBytes = Encoding.UTF8.GetBytes(obj.GetType().Name);
            var userData  = obj.Serialize();
            var payload   = new byte[2 + typeBytes.Length + userData.Length];
            BitConverter.GetBytes((ushort)typeBytes.Length).CopyTo(payload, 0);
            typeBytes.CopyTo(payload, 2);
            userData.CopyTo(payload, 2 + typeBytes.Length);
            return BuildMsg(MsgType.Spawn, obj.NetId, obj.Owner, payload);
        }

        private void Broadcast(byte[] data)
        {
            foreach (var p in _peers.Values)
                if (p.Connected)
                    Transport.Send(p.SteamId, data);
        }

        private void OnReceive(ulong from, byte[] data)
        {
            // Header: type(1) + netId(4) + owner(8) = 13 bytes minimum
            if (data.Length < 13) return;

            var type = (MsgType)data[0];
            var id = BitConverter.ToUInt32(data, 1);
            var owner = BitConverter.ToUInt64(data, 5);
            var payload = data.Length > 13 ? data[13..] : Array.Empty<byte>();

            switch (type)
            {
                case MsgType.Spawn:
                    if (!_objects.ContainsKey(id) && payload.Length >= 2)
                    {
                        var typeNameLen = BitConverter.ToUInt16(payload, 0);
                        var typeName    = Encoding.UTF8.GetString(payload, 2, typeNameLen);
                        var userData    = payload[(2 + typeNameLen)..];
                        var obj = OnRemoteSpawn?.Invoke(typeName, id, owner, userData);
                        if (obj != null)
                        {
                            obj.NetId = id;
                            obj.Owner = owner;
                            if (userData.Length > 0)
                                obj.Deserialize(userData);
                            _objects[id] = obj;
                        }
                    }
                    break;

                case MsgType.Update:
                    if (_objects.TryGetValue(id, out var existing))
                        existing.Deserialize(payload);
                    break;

                case MsgType.Despawn:
                    _objects.TryRemove(id, out _);
                    break;
            }
        }

        // Wire format: type(1) + netId(4) + owner(8) + payload(N)
        private static byte[] BuildMsg(MsgType type, uint id, ulong owner, byte[]? payload)
        {
            var msg = new byte[13 + (payload?.Length ?? 0)];
            msg[0] = (byte)type;
            BitConverter.GetBytes(id).CopyTo(msg, 1);
            BitConverter.GetBytes(owner).CopyTo(msg, 5);
            payload?.CopyTo(msg, 13);
            return msg;
        }
    }
}
