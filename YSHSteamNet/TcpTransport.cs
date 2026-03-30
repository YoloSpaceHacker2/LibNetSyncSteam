using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace YSHSteamNet
{
    // Multi-process P2P transport for local testing — mimics the Steam session model.
    //
    // Connection model (same as ISteamNetworkingMessages):
    //   - Each node listens on its configured port (AcceptLoop).
    //   - Outgoing connections are established lazily on first Send() to a peer.
    //   - On connect, the initiator sends a handshake (localId, 8 bytes) so the
    //     acceptor knows who connected — mirrors SteamNetworkingMessagesSessionRequest_t.
    //
    // Wire framing: length(4) + data(N)   — same for all messages.
    //
    // Thread model:
    //   - AcceptLoop  : background Task, accepts incoming TCP connections.
    //   - ReadLoop    : one background Task per accepted connection, enqueues to _inbox.
    //   - Poll()      : called by NetworkManager.Update() on the app thread, drains _inbox.
    //   - Send()      : may be called from any thread; stream writes are lock-protected.
    public class TcpTransport : ITransport
    {
        private readonly ulong _localId;
        private readonly CustomConfig _config;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();

        // Outgoing connections: peerId → stream used for writing
        private readonly ConcurrentDictionary<ulong, NetworkStream> _outStreams = new();
        private readonly object _connectLock = new();

        // Received messages queued by background read tasks, drained on Poll()
        private readonly ConcurrentQueue<(ulong from, byte[] data)> _inbox = new();

        public Action<ulong, byte[]>? OnReceive { get; set; }

        public TcpTransport(ulong localId, CustomConfig config)
        {
            _localId = localId;
            _config  = config;

            _listener = new TcpListener(IPAddress.Any, config.ListenPort);
            _listener.Start();
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));

            Console.WriteLine($"[TcpTransport] {config.Name} listening on :{config.ListenPort}");
        }

        // reliable is always guaranteed by TCP — parameter kept for ITransport compatibility.
        public void Send(ulong target, byte[] data, bool reliable = true)
        {
            var stream = GetOrConnect(target);
            if (stream == null) return;

            try
            {
                lock (stream)
                    WriteFrame(stream, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpTransport] Send to {target} failed: {ex.Message}");
                _outStreams.TryRemove(target, out _);
            }
        }

        // Drain inbox and dispatch via OnReceive — call every frame from NetworkManager.Update().
        public void Poll()
        {
            while (_inbox.TryDequeue(out var msg))
                OnReceive?.Invoke(msg.from, msg.data);
        }

        public void CloseSession(ulong peerId)
        {
            if (_outStreams.TryRemove(peerId, out var stream))
                stream.Close();
        }

        public void Shutdown()
        {
            _cts.Cancel();
            _listener.Stop();
        }

        // Connect to peer on first Send() — mirrors implicit session creation in Steam P2P.
        private NetworkStream? GetOrConnect(ulong peerId)
        {
            if (_outStreams.TryGetValue(peerId, out var existing)) return existing;

            lock (_connectLock)
            {
                if (_outStreams.TryGetValue(peerId, out existing)) return existing;

                var peer = _config.Peers.Find(p => p.Id == peerId);
                if (peer == null)
                {
                    Console.WriteLine($"[TcpTransport] No config entry for peer {peerId}");
                    return null;
                }

                try
                {
                    var client = new TcpClient(peer.Address, peer.Port);
                    var stream = client.GetStream();

                    // Handshake: send our localId so the acceptor knows who we are.
                    // Mirrors SteamNetworkingMessagesSessionRequest_t on the remote side.
                    stream.Write(BitConverter.GetBytes(_localId));
                    stream.Flush();

                    _outStreams[peerId] = stream;
                    Console.WriteLine($"[TcpTransport] Connected to {peer.Name} ({peer.Address}:{peer.Port})");
                    return stream;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TcpTransport] Connect to {peer.Name} ({peer.Address}:{peer.Port}) failed: {ex.Message}");
                    return null;
                }
            }
        }

        // Accept incoming connections and start a read loop for each.
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try   { client = await _listener.AcceptTcpClientAsync(ct); }
                catch { break; }

                _ = Task.Run(async () =>
                {
                    var stream = client.GetStream();
                    try
                    {
                        // Read handshake: remote's localId
                        var idBuf = new byte[8];
                        await stream.ReadExactlyAsync(idBuf.AsMemory(), ct);
                        var peerId = BitConverter.ToUInt64(idBuf);

                        var peer = _config.Peers.Find(p => p.Id == peerId);
                        Console.WriteLine($"[TcpTransport] Accepted connection from {peer?.Name ?? peerId.ToString()}");

                        await ReadLoopAsync(peerId, stream, ct);
                    }
                    catch { client.Close(); }
                }, ct);
            }
        }

        // Read framed messages from an accepted connection and enqueue to _inbox.
        private async Task ReadLoopAsync(ulong peerId, NetworkStream stream, CancellationToken ct)
        {
            var lenBuf = new byte[4];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await stream.ReadExactlyAsync(lenBuf.AsMemory(), ct);
                    int len  = BitConverter.ToInt32(lenBuf);
                    var data = new byte[len];
                    await stream.ReadExactlyAsync(data.AsMemory(), ct);
                    _inbox.Enqueue((peerId, data));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"[TcpTransport] Connection with {peerId} closed: {ex.Message}");
            }
        }

        // Frame format: length(4 little-endian) + data(N)
        private static void WriteFrame(NetworkStream stream, byte[] data)
        {
            stream.Write(BitConverter.GetBytes(data.Length));
            stream.Write(data);
            stream.Flush();
        }
    }
}
