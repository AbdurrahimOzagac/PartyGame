using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ShooterParty
{
    public struct RoomJoin { public string code, wsUrl, ticket, rejoinToken; }
    public struct JoinError { public int status; public string message; }

    // HTTP client for the room service (projects/shooterparty/server/rooms/room_service.py).
    // Every failure comes back as a sentence the join screen can show as is.
    public class RoomServiceClient
    {
        public const string Unreachable = "Server unreachable — try Local Play";
        public const string UpdateYourGame = "Update your game";
        public const string BadCode = "Party codes are 5 letters";
        public const float TimeoutSeconds = 8f;

        public string BaseUrl { get; }
        public int Version = PartyProtocol.Version;

        [Serializable] class CreateBody { public string name; public bool replaceLeaversWithBots; }
        [Serializable] class JoinBody { public string name; }
        [Serializable] class RejoinBody { public string rejoinToken; }
        [Serializable] class Reply { public string code, wsUrl, ticket, rejoinToken, error; }

        public RoomServiceClient(string baseUrl = null) { BaseUrl = (baseUrl ?? DefaultUrl()).TrimEnd('/'); }

        // -roomService <url> overrides the public server (local tests, a VPS later).
        public static string DefaultUrl()
        {
            var argv = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(argv, "-roomService");
            return i >= 0 && i + 1 < argv.Length ? argv[i + 1] : PartyProtocol.RoomServiceUrl;
        }

        // "bc-df g" -> "BCDFG"; null when it cannot be a party code.
        public static string NormaliseCode(string typed)
        {
            var c = new string((typed ?? "").ToUpperInvariant().Where(ch => ch != ' ' && ch != '-').ToArray());
            return c.Length == 5 && c.All(ch => ServerArgs.Alphabet.IndexOf(ch) >= 0) ? c : null;
        }

        public IEnumerator Create(string name, bool replaceLeavers, Action<RoomJoin> ok, Action<JoinError> fail)
        {
            if (!NameRules.TryClean(name, out var clean, out var err)) { fail(new JoinError { status = 400, message = err }); yield break; }
            yield return Post("/rooms", JsonUtility.ToJson(new CreateBody { name = clean, replaceLeaversWithBots = replaceLeavers }), ok, fail);
        }

        public IEnumerator Join(string code, string name, Action<RoomJoin> ok, Action<JoinError> fail)
        {
            if (!NameRules.TryClean(name, out var clean, out var err)) { fail(new JoinError { status = 400, message = err }); yield break; }
            var c = NormaliseCode(code);
            if (c == null) { fail(new JoinError { status = 400, message = BadCode }); yield break; }
            yield return Post($"/rooms/{c}/join", JsonUtility.ToJson(new JoinBody { name = clean }), r => { r.code = c; ok(r); }, fail);
        }

        public IEnumerator Rejoin(string code, string token, Action<RoomJoin> ok, Action<JoinError> fail)
        {
            var c = NormaliseCode(code);
            if (c == null) { fail(new JoinError { status = 400, message = BadCode }); yield break; }
            yield return Post($"/rooms/{c}/rejoin", JsonUtility.ToJson(new RejoinBody { rejoinToken = token }), r => { r.code = c; ok(r); }, fail);
        }

        IEnumerator Post(string path, string json, Action<RoomJoin> ok, Action<JoinError> fail)
        {
            using var req = new UnityWebRequest(BaseUrl + path, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(TimeoutSeconds),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-ShooterParty-Version", Version.ToString());
            yield return req.SendWebRequest();
            int status = (int)req.responseCode;
            Reply reply = null;
            try { reply = JsonUtility.FromJson<Reply>(req.downloadHandler?.text ?? ""); } catch (ArgumentException) { }
            if (req.result == UnityWebRequest.Result.Success && reply != null && !string.IsNullOrEmpty(reply.ticket))
            {
                ok(new RoomJoin { code = reply.code, wsUrl = reply.wsUrl, ticket = reply.ticket, rejoinToken = reply.rejoinToken });
                yield break;
            }
            string message =
                status == 426 ? UpdateYourGame :
                req.result == UnityWebRequest.Result.ConnectionError || status == 0 ? Unreachable :
                status >= 500 && string.IsNullOrEmpty(reply?.error) ? Unreachable :   // Cloudflare's 52x/530 pages: tunnel or Pi down
                !string.IsNullOrEmpty(reply?.error) ? reply.error :
                $"Something went wrong ({status}) — try again";
            fail(new JoinError { status = status, message = message });
        }
    }
}
