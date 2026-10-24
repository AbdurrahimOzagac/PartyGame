using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ShooterParty
{
    // Client-side bridge for online play. Unity Transport's WebSocket client writes "Host: <resolved IP>" in its
    // handshake, which Cloudflare rejects (403), and its resolver prefers IPv6. UTP connects to this local plain-ws
    // listener instead; the bridge resolves IPv4 first, does TLS with the real hostname (SNI + system CAs), fixes
    // the Host line of the first request and then pipes bytes untouched. Either side closing closes both.
    public sealed class WssBridge : IDisposable
    {
        readonly TcpListener listener;
        readonly string host, hostHeader;
        readonly int port, delayMs;
        readonly bool tls;
        readonly ConcurrentDictionary<int, (TcpClient a, TcpClient b)> live = new ConcurrentDictionary<int, (TcpClient, TcpClient)>();
        int nextId;
        long up, down;
        volatile bool disposed;

        public int LocalPort => ((IPEndPoint)listener.LocalEndpoint).Port;
        public long BytesUp => Interlocked.Read(ref up);
        public long BytesDown => Interlocked.Read(ref down);

        WssBridge(string host, int port, bool tls, int delayMs)
        {
            this.host = host; this.port = port; this.tls = tls; this.delayMs = delayMs;
            hostHeader = port == 443 || port == 80 ? host : host + ":" + port;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            _ = Task.Run(AcceptLoop);   // thread pool: awaits must not resume on Unity's main-thread context
        }

        // delayMs: dev-only latency added in each direction (keeps order, does not serialise the stream).
        public static WssBridge Start(string host, int port, bool tls, int delayMs = 0) => new WssBridge(host, port, tls, delayMs);

        public static byte[] RewriteHost(byte[] head, int length, string host)
        {
            var lines = Encoding.ASCII.GetString(head, 0, length).Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Select(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase) ? "Host: " + host : l);
            return Encoding.ASCII.GetBytes(string.Join("\r\n", lines));
        }

        // Test hook and "connection lost" simulation: closes every live pair.
        public void DropAll()
        {
            foreach (var kv in live) Close(kv.Value);
            live.Clear();
        }

        public void Dispose()
        {
            disposed = true;
            try { listener.Stop(); } catch (SocketException) { }
            DropAll();
        }

        async Task AcceptLoop()
        {
            while (!disposed)
            {
                TcpClient local;
                try { local = await listener.AcceptTcpClientAsync(); }
                catch (Exception) { return; }   // listener stopped
                _ = Handle(local);
            }
        }

        async Task Handle(TcpClient local)
        {
            var remote = new TcpClient(AddressFamily.InterNetwork);
            int id = Interlocked.Increment(ref nextId);
            live[id] = (local, remote);
            try
            {
                local.NoDelay = true;
                await ConnectIPv4First(remote);
                remote.NoDelay = true;
                Stream rs = remote.GetStream();
                if (tls)
                {
                    var ssl = new SslStream(rs, false);
                    await ssl.AuthenticateAsClientAsync(host);
                    rs = ssl;
                }
                var ls = local.GetStream();
                var buf = new byte[8192]; int n = 0, end = -1;
                while (end < 0)
                {
                    if (n == buf.Length) throw new IOException("handshake too large");
                    int r = await ls.ReadAsync(buf, n, buf.Length - n);
                    if (r <= 0) return;
                    n += r;
                    end = IndexOf(buf, n, "\r\n\r\n");
                }
                var head = RewriteHost(buf, end + 4, hostHeader);
                var first = new byte[head.Length + n - end - 4];
                Buffer.BlockCopy(head, 0, first, 0, head.Length);
                Buffer.BlockCopy(buf, end + 4, first, head.Length, n - end - 4);
                var upPipe = new Pipe(rs, delayMs, c => Interlocked.Add(ref up, c));
                upPipe.Push(first, first.Length);
                var downPipe = new Pipe(ls, delayMs, c => Interlocked.Add(ref down, c));
                await Task.WhenAny(Read(ls, upPipe), Read(rs, downPipe), upPipe.Writer, downPipe.Writer);
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException || e is System.Security.Authentication.AuthenticationException)
            {
                if (!disposed) Debug.LogWarning("[WssBridge] " + e.Message);
            }
            catch (Exception e) { if (!disposed) Debug.LogWarning("[WssBridge] " + e); }
            finally
            {
                live.TryRemove(id, out _);
                Close((local, remote));
            }
        }

        async Task ConnectIPv4First(TcpClient remote)
        {
            var addrs = (await Dns.GetHostAddressesAsync(host)).Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
            if (addrs.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            Exception last = null;
            foreach (var a in addrs)
            {
                try { await remote.ConnectAsync(a, port); return; }
                catch (SocketException e) { last = e; }
            }
            throw last;
        }

        static async Task Read(Stream from, Pipe to)
        {
            var buf = new byte[16384]; int r;
            while ((r = await from.ReadAsync(buf, 0, buf.Length)) > 0) to.Push(buf, r);
        }

        static void Close((TcpClient a, TcpClient b) p)
        {
            try { p.a?.Close(); } catch (Exception) { }
            try { p.b?.Close(); } catch (Exception) { }
        }

        static int IndexOf(byte[] b, int n, string s)
        {
            for (int i = 0; i + s.Length <= n; i++)
            {
                int k = 0;
                while (k < s.Length && b[i + k] == s[k]) k++;
                if (k == s.Length) return i;
            }
            return -1;
        }

        // Ordered writer with an optional fixed delay per chunk: the reader never waits on the delay.
        sealed class Pipe
        {
            readonly Stream to;
            readonly int delayMs;
            readonly Action<int> counted;
            readonly ConcurrentQueue<(long due, byte[] data)> queue = new ConcurrentQueue<(long, byte[])>();
            readonly SemaphoreSlim ready = new SemaphoreSlim(0);
            public readonly Task Writer;

            public Pipe(Stream to, int delayMs, Action<int> counted)
            {
                this.to = to; this.delayMs = delayMs; this.counted = counted;
                Writer = Run();
            }

            public void Push(byte[] buf, int n)
            {
                var copy = new byte[n];
                Buffer.BlockCopy(buf, 0, copy, 0, n);
                queue.Enqueue((Environment.TickCount + delayMs, copy));
                ready.Release();
            }

            async Task Run()
            {
                while (true)
                {
                    await ready.WaitAsync();
                    if (!queue.TryDequeue(out var item)) continue;
                    int wait = (int)(item.due - Environment.TickCount);
                    if (wait > 0) await Task.Delay(wait);
                    await to.WriteAsync(item.data, 0, item.data.Length);
                    await to.FlushAsync();
                    counted(item.data.Length);
                }
            }
        }
    }
}
