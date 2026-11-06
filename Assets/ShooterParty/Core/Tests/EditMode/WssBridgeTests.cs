using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class WssBridgeTests
    {
        const string Head = "GET /g/1 HTTP/1.1\r\nUpgrade: websocket\r\nHost: 104.1.2.3:443\r\nSec-WebSocket-Version: 13\r\n\r\n";

        // Echo server: sends back every byte it receives until the peer closes (or until closeAfter bytes).
        sealed class Echo : IDisposable
        {
            readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            public int Port => ((IPEndPoint)l.LocalEndpoint).Port;
            public Echo(int closeAfter = -1)
            {
                l.Start();
                Task.Run(async () =>
                {
                    using var c = await l.AcceptTcpClientAsync();
                    var s = c.GetStream(); var buf = new byte[65536]; int r, total = 0;
                    while ((r = await s.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await s.WriteAsync(buf, 0, r);
                        total += r;
                        if (closeAfter >= 0 && total >= closeAfter) break;
                    }
                });
            }
            public void Dispose() => l.Stop();
        }

        static byte[] ReadExactly(Stream s, int n, int timeoutMs = 3000)
        {
            var buf = new byte[n]; int got = 0; var sw = Stopwatch.StartNew();
            while (got < n)
            {
                var t = s.ReadAsync(buf, got, n - got);
                if (!t.Wait(Math.Max(1, timeoutMs - (int)sw.ElapsedMilliseconds))) Assert.Fail($"timed out after {got}/{n} bytes");
                if (t.Result <= 0) Assert.Fail($"closed after {got}/{n} bytes");
                got += t.Result;
            }
            return buf;
        }

        [Test]
        public void RewriteHostReplacesOnlyHostLine()
        {
            var head = Encoding.ASCII.GetBytes(Head);
            var outText = Encoding.ASCII.GetString(WssBridge.RewriteHost(head, head.Length, "partyshooter.ezellm.com"));
            Assert.AreEqual(Head.Replace("Host: 104.1.2.3:443", "Host: partyshooter.ezellm.com"), outText);
        }

        [Test]
        public void PipesThroughPlainEcho()
        {
            using var echo = new Echo();
            using var bridge = WssBridge.Start("127.0.0.1", echo.Port, tls: false);
            using var c = new TcpClient(); c.Connect(IPAddress.Loopback, bridge.LocalPort);
            var s = c.GetStream();
            var payload = Enumerable.Range(0, 10240).Select(i => (byte)(i * 7)).ToArray();
            var head = Encoding.ASCII.GetBytes(Head);
            s.Write(head, 0, head.Length); s.Write(payload, 0, payload.Length);
            var expectedHead = Head.Replace("Host: 104.1.2.3:443", "Host: 127.0.0.1:" + echo.Port);
            Assert.AreEqual(expectedHead, Encoding.ASCII.GetString(ReadExactly(s, expectedHead.Length)));
            CollectionAssert.AreEqual(payload, ReadExactly(s, payload.Length));
            Thread.Sleep(50);
            Assert.AreEqual(expectedHead.Length + payload.Length, bridge.BytesUp);
            Assert.AreEqual(bridge.BytesUp, bridge.BytesDown);
        }

        [Test]
        public void ClosingRemoteClosesLocal()
        {
            using var echo = new Echo(closeAfter: Head.Length);
            using var bridge = WssBridge.Start("127.0.0.1", echo.Port, tls: false);
            using var c = new TcpClient(); c.Connect(IPAddress.Loopback, bridge.LocalPort);
            var s = c.GetStream();
            var head = Encoding.ASCII.GetBytes(Head);
            s.Write(head, 0, head.Length);
            var expected = Head.Replace("Host: 104.1.2.3:443", "Host: 127.0.0.1:" + echo.Port).Length;
            ReadExactly(s, expected);
            var t = s.ReadAsync(new byte[16], 0, 16);
            Assert.IsTrue(t.Wait(1000), "local side should close within 1 s of the remote closing");
            Assert.AreEqual(0, t.Result);
        }

        [Test]
        public void DropAllClosesLivePairs()
        {
            using var echo = new Echo();
            using var bridge = WssBridge.Start("127.0.0.1", echo.Port, tls: false);
            using var c = new TcpClient(); c.Connect(IPAddress.Loopback, bridge.LocalPort);
            var s = c.GetStream();
            var head = Encoding.ASCII.GetBytes(Head);
            s.Write(head, 0, head.Length);
            ReadExactly(s, Head.Replace("Host: 104.1.2.3:443", "Host: 127.0.0.1:" + echo.Port).Length);
            bridge.DropAll();
            var t = s.ReadAsync(new byte[16], 0, 16);
            Assert.IsTrue(t.Wait(1000)); Assert.AreEqual(0, t.Result);
        }

        [Test]
        public void DelayAddsLatency()
        {
            using var echo = new Echo();
            using var bridge = WssBridge.Start("127.0.0.1", echo.Port, tls: false, delayMs: 100);
            using var c = new TcpClient(); c.Connect(IPAddress.Loopback, bridge.LocalPort);
            var s = c.GetStream();
            var head = Encoding.ASCII.GetBytes(Head);
            s.Write(head, 0, head.Length);
            ReadExactly(s, Head.Replace("Host: 104.1.2.3:443", "Host: 127.0.0.1:" + echo.Port).Length);
            // 20 pings back to back: delay must apply per direction without serialising the stream
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++) s.Write(new byte[] { (byte)i }, 0, 1);
            var back = ReadExactly(s, 20, 3000);
            long ms = sw.ElapsedMilliseconds;
            CollectionAssert.AreEqual(Enumerable.Range(0, 20).Select(i => (byte)i).ToArray(), back);
            Assert.GreaterOrEqual(ms, 190, "round trip should include 100 ms each way");
            Assert.Less(ms, 600, "chunks must be pipelined, not delayed one after another");
        }

        [Test, Category("Online")]
        public void TlsToCloudflareAcceptsRewrittenHost()
        {
            try { Dns.GetHostAddresses("ezellm.com"); } catch (SocketException) { Assert.Ignore("offline"); }
            using var bridge = WssBridge.Start("ezellm.com", 443, tls: true);
            using var c = new TcpClient(); c.Connect(IPAddress.Loopback, bridge.LocalPort);
            var s = c.GetStream();
            var req = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 172.67.0.1:443\r\nConnection: close\r\n\r\n");
            s.Write(req, 0, req.Length);
            var status = Encoding.ASCII.GetString(ReadExactly(s, 12, 8000));
            StringAssert.StartsWith("HTTP/1.1 ", status);
            Assert.AreNotEqual("403", status.Substring(9, 3), "Cloudflare rejected the Host header");
        }
    }
}
