#if UNITY_EDITOR
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    // Talks to the real room service (Python, with its fake game server) on a free local port.
    public class RoomServiceClientTests
    {
        Process service;
        string baseUrl, dir;

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0); l.Start();
            int p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            return p;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var rooms = Path.GetFullPath(Path.Combine(Application.dataPath, "../projects/shooterparty/server/rooms"));
            dir = Path.Combine(Path.GetTempPath(), "sp-rooms-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "secret"), new string('a', 64));
            int port = FreePort(), status = FreePort();
            var psi = new ProcessStartInfo("python3",
                $"\"{rooms}/room_service.py\" --server-cmd \"python3 '{rooms}/fake_game_server.py'\" --secret-file \"{dir}/secret\" " +
                $"--listen 127.0.0.1:{port} --status-listen 127.0.0.1:{status} --ws-url-template \"ws://127.0.0.1:{{port}}/g/{{slot}}\" --maintenance-file \"{dir}/m\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            try { service = Process.Start(psi); }
            catch (System.ComponentModel.Win32Exception) { Assert.Ignore("python3 is not installed"); }
            baseUrl = $"http://127.0.0.1:{port}/api/v1";
            for (int i = 0; i < 50; i++)   // wait until it listens
            {
                using (var c = new TcpClient()) { try { c.Connect(IPAddress.Loopback, port); yield break; } catch (SocketException) { } }
                yield return new WaitForSecondsRealtime(0.1f);
            }
            Assert.Fail("room service did not start: " + service.StandardError.ReadToEnd());
        }

        [TearDown]
        public void TearDown()
        {
            if (service != null && !service.HasExited) service.Kill();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        [UnityTest]
        public IEnumerator CreateThenJoinByCode()
        {
            var client = new RoomServiceClient(baseUrl);
            RoomJoin created = default, joined = default; JoinError error = default;
            yield return client.Create("Ezel", false, r => created = r, e => error = e);
            Assert.IsNull(error.message, error.message);
            StringAssert.IsMatch("^[BCDFGHJKLMNPQRSTVWXZ]{5}$", created.code);
            Assert.AreEqual("ws://127.0.0.1:7701/g/1", created.wsUrl);
            Assert.IsNotEmpty(created.ticket); Assert.IsNotEmpty(created.rejoinToken);
            yield return client.Join(created.code.ToLowerInvariant(), "Zesty", r => joined = r, e => error = e);
            Assert.IsNull(error.message, error.message);
            Assert.AreEqual(created.wsUrl, joined.wsUrl);
            yield return client.Rejoin(created.code, created.rejoinToken, r => joined = r, e => error = e);
            Assert.IsNull(error.message, error.message);
            Assert.IsNotEmpty(joined.ticket);
        }

        [UnityTest]
        public IEnumerator MapsErrors()
        {
            var client = new RoomServiceClient(baseUrl);
            JoinError error = default;
            yield return client.Join("BCDFG", "Zesty", _ => { }, e => error = e);
            Assert.AreEqual(404, error.status);
            Assert.AreEqual("No room with code BCDFG", error.message);

            yield return client.Join("BCDFG", "<b>x</b>", _ => { }, e => error = e);
            Assert.AreEqual(NameRules.CharsetError, error.message, "names are checked before anything is sent");

            var old = new RoomServiceClient(baseUrl) { Version = 0 };
            yield return old.Create("Ezel", false, _ => { }, e => error = e);
            Assert.AreEqual(RoomServiceClient.UpdateYourGame, error.message);

            var nobody = new RoomServiceClient($"http://127.0.0.1:{FreePort()}/api/v1");
            yield return nobody.Create("Ezel", false, _ => { }, e => error = e);
            Assert.AreEqual(RoomServiceClient.Unreachable, error.message);

            yield return client.Join("AE", "Zesty", _ => { }, e => error = e);
            Assert.AreEqual(RoomServiceClient.BadCode, error.message);
        }

        // Tunnel stopped or the Pi off: Cloudflare answers 530 (or another 5xx) with an HTML page, not our JSON.
        [UnityTest]
        public IEnumerator CloudflareOutagePagesReadAsUnreachable()
        {
            int port = FreePort();
            var stub = new HttpListener();
            stub.Prefixes.Add($"http://127.0.0.1:{port}/");
            stub.Start();
            stub.BeginGetContext(ar =>
            {
                var ctx = stub.EndGetContext(ar);
                var page = System.Text.Encoding.UTF8.GetBytes("<html><body>Error 1033 Argo Tunnel error</body></html>");
                ctx.Response.StatusCode = 530;
                ctx.Response.ContentType = "text/html";
                ctx.Response.OutputStream.Write(page, 0, page.Length);
                ctx.Response.Close();
            }, null);
            JoinError error = default;
            yield return new RoomServiceClient($"http://127.0.0.1:{port}/api/v1").Join("BCDFG", "Zesty", _ => { }, e => error = e);
            stub.Stop();
            Assert.AreEqual(530, error.status);
            Assert.AreEqual(RoomServiceClient.Unreachable, error.message);
        }
    }
}
#endif
