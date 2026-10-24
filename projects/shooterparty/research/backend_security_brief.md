# ShooterParty backend on a home Raspberry Pi 5: exposure, lobby, security, stack, observability

Research brief, 2026-10-24. Scope: 3-4 players per room, at most 1-2 rooms at once, Unity 6.3 (project is on 6000.3.9f1, no netcode package installed yet), Windows + macOS clients, a Pi 5 on a ~30 Mbps line, domain ezellm.com already served from the Pi.

Evidence tags used throughout:
- **(V)** checked against a primary doc (vendor docs, RFC, OWASP, man page) during this research.
- **(S)** secondary source: forum, blog, community post, or a vendor page that is not the spec.
- **(U)** not verified. This is my estimate or inference, so test it before relying on it.

---

## 0. Recommendations at a glance

**Exposure path**
- **Primary (default everywhere, including the school demo):** all public traffic goes through Cloudflare Tunnel on 443/TCP, on a dedicated subdomain such as `party.ezellm.com` with its own tunnel. The lobby runs as HTTPS REST plus one WebSocket per client. Game traffic is Unity Transport (UTP) in WebSocket mode (WSS at the Cloudflare edge). The Pi opens no inbound ports and the home IP stays hidden. It works on networks that block UDP. This is one code path, which makes it the most robust option for a graded demo.
- **Fallback / upgrade (only when all conditions hold):** direct UDP with DTLS to `play.ezellm.com` (a grey-cloud, DNS-only record), through one forwarded UDP port range. Conditions: (a) the line is not behind CGNAT; (b) the owner accepts that every player and anyone who resolves the name learns the home IP; (c) the players' networks allow outbound UDP; (d) WSS latency or hitching actually measured bad. Open the port only during play sessions.
- **If CGNAT or IP exposure rules out the fallback:** forward UDP through a cheap VPS over WireGuard/Tailscale, or use Unity Relay (free tier) with player hosting. Not Spectrum, not Funnel (sections 1.3 and 1.8).
- **Insurance for the demo:** a LAN direct-connect mode on a phone hotspot or travel router. School Wi-Fi often isolates clients from each other (U).

**Lobby:** one small .NET 10 service holds all state in memory. REST handles commands and one WebSocket per client pushes full room snapshots. Rooms use 5-letter, vowel-free codes. Playlists live in client-local JSON. The lobby spawns one headless Unity server per room and issues single-use HMAC join tickets, which the server checks in connection approval.

**Security, in priority order:** (1) no inbound ports and services bound to 127.0.0.1; (2) admin only over Tailscale, keys only; (3) server-authoritative gameplay with validation and per-client RPC rate caps; (4) signed, single-use, short-lived join tickets checked in connection approval; (5) strict input validation and size caps at every boundary; (6) layered rate limits (Cloudflare free rule plus the app limiter), sized for 4+ players behind one school NAT; (7) systemd sandbox under an unprivileged user; (8) secrets out of git, automatic security updates, and a one-command kill switch.

**Stack:** ASP.NET Core minimal API on .NET 10 LTS, because it shares C# DTOs and ticket code with Unity.

---

## 1. Getting real-time traffic to a home Pi

### 1.1 First, find out how ezellm.com is served today (10 minutes)

These checks are read-only. Run them before choosing anything.

| Check | What it tells you |
|---|---|
| `dig +short ezellm.com A` from any machine | A Cloudflare address (WHOIS shows "Cloudflare, Inc.") means the record is orange-cloud proxied. Your home IP means it is DNS-only or direct (S: [miloserdov](https://miloserdov.org/?p=1362)). |
| Cloudflare dashboard → DNS | A CNAME to `<UUID>.cfargotunnel.com` means Cloudflare Tunnel (V: [CF Tunnel DNS](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/dns/)). |
| On the Pi: `systemctl status cloudflared`, `sudo ss -tulpn` | Whether cloudflared runs, and what is listening on 0.0.0.0 compared with 127.0.0.1. |
| Router admin → port forwarding / UPnP | Whether 80/443 (or anything else) is forwarded to the Pi. |
| Router WAN IP vs. `curl -4 https://ifconfig.me` | If they differ, or the WAN IP is in 100.64.0.0/10 (RFC 6598 shared space), you are behind CGNAT and port forwarding cannot work (S: [ipcheck.ing](https://docs.ipcheck.ing/knowledge-base/diagnose/am-i-behind-cgnat), [oneuptime](https://oneuptime.com/blog/post/2026-03-20-detect-cgnat/view)). |

If the site is proxied with port forwarding instead of Tunnel, the origin IP can usually still be found by internet-wide scans. Firewall 80/443 to Cloudflare IP ranges only, or move the site to a Tunnel (S: [christophetd](https://blog.christophetd.fr/bypassing-cloudflare-using-internet-wide-scan-data/)).

### 1.2 Cloudflare Tunnel: what it can and cannot carry (status as of Oct 2026)

- **Public hostnames carry HTTP/HTTPS, which includes WebSocket upgrades.** TCP, SSH, RDP and SMB are also possible, but only streamed over WebSocket, and "Non-HTTP services require installing cloudflared on the client" (V: [Tunnel protocols](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/protocols/)). **UDP is not offered for public hostnames.** That docs page does not list UDP at all.
- **The July 2025 "UDP improvements" changelog** re-architected how cloudflared proxies UDP, and it applies automatically (V: [changelog](https://developers.cloudflare.com/changelog/2025-07-15-udp-improvements/)). It does not name the routing mode. Its example is private DNS, and it links to private-network guidance. Read it as a private-network (WARP) improvement, not public UDP (inference, U). I found no 2025-2026 change that adds public-hostname UDP.
- **"WARP-to-tunnel" (private network routing):** devices running the Cloudflare WARP client, enrolled in your Zero Trust org, can reach private IPs/CIDRs behind cloudflared over TCP, UDP and ICMP (V: [Connect an IP/CIDR](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/private-net/cloudflared/connect-cidr), [peer-to-peer](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/private-net/create-private-networks)). The free Zero Trust plan covers up to 50 users (S: [CF blog](https://blog.cloudflare.com/teams-plans), [Cloudflare One](https://www.cloudflare.com/teams-access/)). **Verdict:** this technically carries UDP, but every player would have to install WARP and log into your org, possibly on school machines. That is too much friction for a party game. Reject it.

### 1.3 Cloudflare Spectrum: not an option

Spectrum is not available on Free. On Pro and Business it is a paid add-on limited to one Minecraft or SSH app (plus RDP on Business). Generic TCP/UDP is an Enterprise paid add-on only (V: [Protocols per plan](https://developers.cloudflare.com/spectrum/protocols-per-plan/)). The often-quoted "5 GB included, then $1/GB" comes from a Minecraft-context forum answer (S: [CF community](https://community.cloudflare.com/t/minecraft-server-gaming-server/163826)). It does not apply to generic UDP.

### 1.4 Game traffic as WebSocket through Cloudflare's proxy

- **ToS:** WebSockets are supported on all plans (V: [CF WebSockets](https://developers.cloudflare.com/network/websockets/)). In 2023 the old "non-HTML content" clause (2.8) was replaced by a CDN-only restriction on video and disproportionate large files not hosted on Cloudflare (V: [CF blog](https://blog.cloudflare.com/updated-tos/), [service-specific terms](https://www.cloudflare.com/service-specific-terms-application-services/)). I found nothing that prohibits low-volume WebSocket game traffic. A 4-player game at roughly 0.3 Mbps is nowhere near "disproportionate" (U, estimate).
- **Idle timeout:** Cloudflare's docs say only that it closes a WebSocket "when no data is transmitted in either direction for a period of time", and that Enterprise can ask for a custom value (V). The widely reported figure is **100 seconds**, fixed below Enterprise (S: [CF community](https://community.cloudflare.com/t/cloudflare-websocket-timeout/5865), [buildbot issue](https://github.com/buildbot/buildbot/issues/4078)). The general Proxy Read Timeout is now listed as 125 s (V: [connection limits](https://developers.cloudflare.com/fundamentals/reference/connection-limits/)). Game traffic at 20-30 Hz never goes idle. **The lobby WebSocket does go idle, so send an application-level heartbeat every 20-25 s.** ASP.NET Core's default `KeepAliveInterval` is 2 minutes (V: [ASP.NET WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)), which is longer than Cloudflare's window. Set it to 20 s and also send app-level `ping` messages. It is not documented whether Cloudflare counts protocol ping frames as data (U).
- **Restarts:** "When Cloudflare releases new code to its global network, we may restart servers, which terminates WebSockets connections" (V: [CF WebSockets](https://developers.cloudflare.com/network/websockets/)). Restarting or replacing cloudflared drops WebSockets too (V: [config file docs](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/local-management/configuration-file/)). **Reconnect-with-rejoin is mandatory, not optional** (section 2.7).
- **Latency:** I found no published benchmark of Tunnel overhead compared with a direct connection. The path is client → nearest Cloudflare PoP → Cloudflare backbone → the PoP holding the tunnel connection → Pi. The cost depends on PoP distance (S: [dev.to](https://dev.to/merbayerp/cloudflare-tunnel-vs-reverse-proxy-ways-to-access-self-hosted-1p7b), [fly.io community](https://community.fly.io/t/cloudflare-fly-for-better-performance/1595)). If players and Pi are in the same metro area with a local PoP, expect single-digit to low tens of ms (U). **Measure it:** compare the in-game RTT overlay over WSS-via-Tunnel against direct UDP on LAN.
- **TCP head-of-line blocking is the real cost.** One lost packet stalls everything behind it until it is retransmitted, which shows as freezes and then bursts (V: [Gaffer On Games, UDP vs TCP](https://gafferongames.com/post/udp_vs_tcp/)). On clean wired or home links this is rare. On congested campus Wi-Fi it can show up as hitches. That is why the UDP path is kept as an upgrade.
- **Cloudflare gotcha:** classic **Bot Fight Mode cannot be skipped** by WAF custom rules and "may challenge API or mobile app traffic". A Unity client cannot solve a JS challenge. Keep Bot Fight Mode off on the ezellm.com zone, or the lobby and game will randomly fail (V: [CF learning path](https://developers.cloudflare.com/learning-paths/modules/get-started/security-free/bot-fight-mode/), [skip options](https://developers.cloudflare.com/waf/custom-rules/skip/options)). "Under Attack" mode will also break non-browser clients (U). Test with `curl` after any security-setting change.

### 1.5 Unity Transport over WebSocket: what to know

- Any non-WebGL build can use `WebSocketNetworkInterface`. NGO's `UnityTransport` component exposes a `UseWebSockets` toggle, and UDP is the default (V: [UTP WebSockets](https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/websockets.html), [UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.Transports.UTP.UnityTransport.html)).
- **TLS:** the client calls `WithSecureClientParameters(serverName)` (NGO: `SetClientSecrets(serverCommonName)`) with no CA, and the OS trust store validates. That fits Cloudflare's public edge certificate. Cloudflare terminates TLS. cloudflared talks to `ws://127.0.0.1:PORT` on the Pi over the already-encrypted tunnel, so the Unity server needs no certificate in this mode (V for the UTP behaviour; the topology is Unity's documented "reverse proxy performing TLS termination" option).
- **Path:** `WithWebSocketParameters(path: "/g/1")`. Clients connect to that path, and a server "will only allow connections made to the given path" (V). This lets one hostname route to per-room server slots with static tunnel ingress rules (section 2.7).
- **One socket type per driver.** A WebSocket client cannot reach a UDP server (V). UTP's `MultiNetworkDriver` can serve UDP and WS together (V: [UTP manual](https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/)). However, NGO's stock `UnityTransport` component appears to wrap a single driver, so serving both from one NGO server likely needs a custom `NetworkTransport` (U). With one server process per room this does not matter: **the lobby picks the mode per room**, WSS by default and UDP only when all members opted in.
- A known pitfall: a proxy that completes the 101 upgrade but forwards traffic to a UTP server *not* in WebSocket mode fails at framing (S: [PlayFab issue](https://github.com/PlayFab/MultiplayerServerSecureWebsocket/issues/9)). Do a smoke test through the real tunnel in week 1.

### 1.6 Direct UDP: router port-forward plus a grey-cloud subdomain

- **How:** forward e.g. UDP 7801-7802 → Pi. Add `play.ezellm.com` as a DNS-only A record. Run UTP with `UseEncryption` (DTLS). Use your own CA: put the CA cert in the client (`SetClientSecrets(commonName, caCertificate)`), and keep the server cert and key on the Pi only (V: [UTP secure](https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/client-server-secure.html), [UnityTransport API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.Transports.UTP.UnityTransport.html)). Your own CA avoids Let's Encrypt renewals and port-80 challenges. The client only ever talks to your server, so pinning your CA is stricter than public PKI.
- **What it exposes:** a DNS-only record "will resolve to the record's actual origin IP address … potentially exposing your server to bad actors and DDoS attacks" (V: [CF proxy status](https://developers.cloudflare.com/dns/proxy-status/)). It is the same IP as the website. If the website currently hides behind Tunnel or proxying, this undoes that protection for the whole house. Every player also sees the IP in their client.
- **Realistic risk sizing (U):** the threats are (1) internet scanners finding the UDP port within days, which is harmless if the server drops anything that fails the handshake; and (2) a classmate or stranger renting a "booter". Gigabit-class floods are cheap, and a 30 Mbps residential line is saturated trivially. That takes the *whole home internet* down, not just the game, and residential ISPs offer no upstream mitigation (S: [TCPShield](https://docs.tcpshield.com/miscellaneous/protect-a-home-hosted-server), [LowEndTalk](https://lowendtalk.com/discussion/comment/1086168/)). Mitigations: do not expose by default; open the port only during sessions; get a new IP after an attack (reconnect the router if the IP is dynamic); move UDP to a VPS or Relay if it happens twice.
- **IPv6:** if the Pi has a public IPv6, it avoids CGNAT. However, every player's network must also have IPv6, and many campus and home networks do not (U). Not a reliable fallback.

### 1.7 If behind CGNAT, or if the home IP must stay hidden for UDP

| Option | UDP? | Hides home IP | Cost | Notes |
|---|---|---|---|---|
| Ask ISP for a public IPv4 / CGNAT opt-out | yes | no | often free or small | Then section 1.6 applies (S). |
| VPS forwarder (VPS public UDP → WireGuard/Tailscale → Pi) | yes | **yes** | Hetzner CAX11 ≈ €4.5-6/mo after the 2026 repricing (S: [costgoat](https://costgoat.com/pricing/hetzner), [privatedevops](https://privatedevops.com/news/hetzner-june-2026-cloud-price-increase-what-to-do)) | Hetzner advertises free DDoS protection (S). Adds the VPS hop. You could also run the headless server on the VPS directly. |
| playit.gg | yes (TCP and UDP tunnels) | yes | free tier; ~$30/yr paid (S, tunnel counts conflict) | Third-party dependency; random ports on free tier (S: [teklynk](https://www.teklynk.com/blog/how-to-host-a-game-server-from-your-home-using-playItgg), [pinggy](https://pinggy.io/blog/best_playit_gg_alternatives/)). |
| Unity Relay + player-hosting | yes (UDP/DTLS, WSS mode too) | yes (Pi not involved in game traffic) | first 50 avg monthly CCU and up to 150 GiB/mo free (S: [UGS pricing](https://unity.com/products/gaming-services/pricing), [Relay pricing FAQ](https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced)) | Needs UGS sign-in. Host leaving ends the match unless you build host migration. The Pi keeps only the lobby. |
| Tailscale Funnel | **no** | yes | free | Funnel is TLS-over-TCP only, on ports 443/8443/10000 (V: [Funnel docs](https://tailscale.com/kb/1223/funnel)). Its UDP works only inside a tailnet, so every player would have to join your tailnet. Use Tailscale for admin access, not for players. |

### 1.8 School and university networks

- No good statistic exists. Anecdotally, campus firewalls often block or restrict non-standard ports and rarely grant exceptions (V as policy pages: [Gettysburg](https://www.gettysburg.edu/offices/information-technology/pdfs/2019/GameandPortPolicy.pdf), [IU housing](https://kb.iu.edu/d/apjc), [UW-Madison](https://kb.wisc.edu/ns/16575)). A Unity staff reply about a school network that blocked Relay UDP and DTLS recommended WebSockets because restricted networks "might have a more lenient policy" for them (S: [Unity forum](https://discussions.unity.com/t/cannot-connect-to-relay-server-919217/919217)).
- Even UDP/443 is blocked "near the user" for about 4.5% of Chrome QUIC users in Google's data (S: [arXiv 1612.07816](https://arxiv.org/pdf/1612.07816)). Blocking of arbitrary UDP ports is much more common than that (U). Using UDP 443 as the game port might pass more firewalls, but DPI that expects QUIC could still drop it (U).
- **What people use:** WSS on 443, which looks like ordinary HTTPS. That is the default WebGL path anyway (V: UTP WebSockets doc). Some networks also break WebSockets through proxies. If so, the lobby can fall back to polling, but there is no fallback for game traffic (U).
- **Demo insurance:** school Wi-Fi often enables client isolation, so LAN mode can fail too (U). Bring a phone hotspot or travel router, and keep a LAN direct-connect mode that needs neither the Pi nor the internet.

### 1.9 Where the game server runs

- **Headless Unity server on the Pi (recommended with the Tunnel path).** A community report says a Linux Server **ARM64** IL2CPP build failed in early 6000.3 betas and worked from 6000.3.0b10, running on a Raspberry Pi 3 and in Docker (S: [Unity forum](https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359)). Unity's official dedicated-server page lists only `Linux64` (V: [dedicated server build](https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html)). **Verify an ARM64 server build on 6000.3.9f1 in week 1. It is the biggest technical risk in this plan.** Other practicalities (U, common practice): set `Application.targetFrameRate` to the tick rate on the server so it does not spin a core; run with `-batchmode -nographics -logFile -` so logs go to journald.
- **Player-hosting** only works with a relay, because a player behind home or school NAT cannot accept connections and the Tunnel cannot reach a player's laptop. This leads to Unity Relay (section 1.7).

### 1.10 Recommendation

| Situation | Path |
|---|---|
| Default, school demo, unknown networks | **Lobby and game over Cloudflare Tunnel (HTTPS + WSS on 443).** No inbound ports, home IP hidden. |
| Everyone on good home networks, not CGNAT, owner accepts IP exposure, WSS hitches measured | Per-room **UDP + DTLS** on `play.ezellm.com:7801-7802`, port opened only for the session. The lobby chooses the mode. |
| CGNAT, or IP must stay hidden, and UDP is needed | VPS WireGuard forwarder, or Unity Relay with player-hosting. |
| Internet or Pi unavailable at the demo | LAN direct-connect over a hotspot or travel router. |

Use a **subdomain** (`party.ezellm.com`) rather than `ezellm.com/partyshooter`. It isolates rate-limit and WAF rules, avoids path rewriting, and can have its **own tunnel** (separate credentials and kill switch, independent of the website). `ezellm.com/partyshooter` can stay a landing or download page. Path routing does work if you insist, since ingress `path` is a Go regex (V: [config file docs](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/local-management/configuration-file/)).

```yaml
# /etc/cloudflared/partyshooter.yml : separate tunnel from the website
tunnel: <partyshooter-tunnel-uuid>
credentials-file: /etc/cloudflared/partyshooter.json
ingress:
  - hostname: party.ezellm.com
    path: ^/api/
    service: http://127.0.0.1:5080     # lobby (public API + WS)
  - hostname: party.ezellm.com
    path: ^/g/1$
    service: http://127.0.0.1:7801     # room slot 1 (UTP WebSocket, path "/g/1")
  - hostname: party.ezellm.com
    path: ^/g/2$
    service: http://127.0.0.1:7802     # room slot 2
  - service: http_status:404           # mandatory catch-all
# NOTE: the lobby's internal endpoint (127.0.0.1:5081) is deliberately NOT routed.
```

---

## 2. Lobby / room-code service design

### 2.1 Shape: the smallest robust version

- **One process and in-memory state.** At most 2 rooms and 8 players, so a `Dictionary<code, Room>` behind a single lock is enough. A restart loses rooms, and clients reconnect and recreate them. That is acceptable for this scope. Persist only match summaries (JSONL) and, optionally, shared playlists (SQLite) later.
- **REST for commands, one WebSocket per client for server→client push.** REST is easy to test with curl, easy to rate-limit per endpoint, and idempotent to retry. The WebSocket pushes the **full room snapshot** (tiny: 4 players) with a monotonically increasing `version` after every change. There is no diffing. A socket close is the presence signal. After a reconnect the client does `GET /api/rooms/{code}` and resubscribes.
- **Why not pure polling:** polling 8 clients every 1-2 s is trivial load. However, it makes ready, kick and start feel laggy and gives no presence signal. Unity's own Lobby service is polling-based with a host heartbeat and marks a lobby inactive after 30 s without a heartbeat or update (V: [UGS glossary](https://docs.unity.com/lobby/glossary), [heartbeat article](https://support.unity.com/hc/en-us/articles/4408402562580-Understanding-and-Implementing-Lobby-Heartbeats)). If the WebSocket cannot connect, falling back to polling the snapshot every 2 s is a cheap robustness add-on (optional).

### 2.2 API surface

| Call | Purpose / rules |
|---|---|
| `POST /api/session {deviceId, name}` | Returns `{token, playerId, exp}`. The token is an opaque random 256-bit string held in a server map, 12 h TTL (section 3). |
| `POST /api/rooms {public}` | Creates a room and returns its code. The caller becomes host. Rejected with 503 when `MaxRooms` (2) is reached. |
| `POST /api/rooms/join {code}` | Joins by code. Returns 404 (generic "room not found") for a bad code. Returns 409 when the room is full (4) or in game. A kicked device is refused for that room's lifetime. |
| `POST /api/quickplay` | Joins the oldest public open room that has space, otherwise creates a public room (section 2.8). |
| `POST /api/rooms/{code}/ready {ready}` | Toggles ready. Any membership change resets everyone's ready state (prevents a stale ready). |
| `PUT /api/rooms/{code}/playlist {name, games[]}` | Host only. Server-validated (section 2.6). |
| `POST /api/rooms/{code}/kick {playerId}` | Host only. Pushes `kicked` to the target and closes its socket. |
| `POST /api/rooms/{code}/start` | Host only. Allowed when 3 ≤ players ≤ 4, all non-hosts are ready, and a playlist is set. |
| `POST /api/rooms/{code}/leave` | Explicit leave, preferable to waiting for a timeout. |
| `POST /api/rooms/{code}/rejoin` | A member who dropped mid-game gets a fresh ticket for the same server. |
| `GET /api/rooms/{code}` | Snapshot, used for resync. |
| `GET /api/ws` (WebSocket, `Authorization: Bearer`) | Pushes `room`, `start {endpoint, ticket}` (each player's own ticket only), `kicked`, `closed`, `maintenance`. The client sends only `ping`. |
| `GET /api/status` | Public health and counts, with no personal data (section 5). |

Put the token in the `Authorization` header (desktop `ClientWebSocket` supports custom headers), never in the URL, because query strings end up in proxy logs (U). `ClientWebSocket` and `UnityWebRequest` both generally work in desktop builds. Confirm `ClientWebSocket` in an **IL2CPP** standalone build early, since reports are old and mixed (S: [RxWebSocket](https://github.com/nenoNaninu/RxWebSocket)).

### 2.3 Room lifecycle and timers

`Open (lobby)` → host Start → `Starting` (spawn server, wait for its "ready" callback, push tickets) → `InGame` (locked; members can rejoin, non-members cannot join) → server exits with results → `Results` → back to `Open` (same code, same members) or `Closed`.

A sweeper runs every 5-10 s and applies these defaults (all U, tune as needed):
- Lobby phase: a member whose WebSocket has been gone for more than **30 s** is removed. Within the grace window, a reconnect with the same token keeps the slot.
- A room that is empty for more than **60 s** is deleted. A room idle in the lobby for more than **30 min** is deleted. A hard cap of **2 h** room lifetime applies.
- Game server watchdog: if it has had zero connected clients for 60 s, it exits and the room returns to `Open`.
- Codes of deleted rooms go on a **10-min cooldown** before reuse, so a late joiner never lands in a stranger's new room.

### 2.4 Presence, ready, kick, host leaving

- **Host leaves in the lobby:** promote the longest-present remaining member. This is deterministic and easy to explain. Unity Lobby picks randomly when the host leaves without naming a successor, and also migrates when the host is disconnected for more than 5 s (V: [Unity Lobby host migration](https://docs.unity.com/lobby/host-migration)).
- **Host leaves mid-game:** because the server runs on the Pi, "host" is only a lobby role, so the match continues. This is a strong reason to prefer the dedicated server over player-hosting: no NGO host migration is needed. NGO has no built-in host migration, and Unity's newer session-level migration relies on Unity Cloud snapshots (V: [UGS host migration](https://docs.unity.com/ugs/en-us/manual/lobby/manual/host-migration)).
- **Below 3 players mid-game:** game-design decision. The suggestion is to continue the playlist with whoever remains and end early only if 1 player is left.
- **Kick:** host only. The kicked deviceId is banned from that room until it closes.

### 2.5 Party code format

- **Alphabet:** 20 consonants, letters only: `BCDFGHJKLMNPQRSTVWXZ`. Having no digits removes 0/O, 1/I/L, 2/Z, 5/S and 8/B confusion. Having no vowels (A E I O U, and Y) means codes cannot spell most words, offensive ones included. Crockford's base32 drops U for the same "accidental obscenity" reason (V: [IETF draft base32-for-humans](https://www.ietf.org/archive/id/draft-crockford-davis-base32-for-humans-01.html), S: [Wikipedia Base32](https://en.wikipedia.org/wiki/Base32)). Input is case-insensitive, with spaces and dashes stripped.
- **Length 5:** 20^5 = 3.2 M codes. Generate with a CSPRNG (`RandomNumberGenerator.GetInt32`). Regenerate on collision with an active or cooldown code.
- **Reference points:** Jackbox uses 4 letters (S). Among Us moved from 4 alphanumeric to 6 uppercase letters in Sept 2020 as player counts exploded (S: [Hindenburg docs](https://hindenburg.js.org/pages/plugins/api/game-codes.html), [devtrackers](https://devtrackers.gg/among-us/p/003032ff-pinned-servers-update-2020-9-9-beta)). Unity Lobby codes are documented only as "randomly generated alphanumeric" (V: glossary). I could not confirm their length.
- **Brute-force odds at our scale** (2 active rooms, chance an attacker finds *any* live room within 1 hour; computed):

| Code | Space | Attacker capped at 10 failed joins/min per IP | Botnet at a global cap of 120 failed joins/min |
|---|---|---|---|
| 4 letters | 160,000 | 0.75 % | 8.6 % |
| **5 letters** | 3.2 M | **0.04 %** | **0.45 %** |
| 6 letters | 64 M | 0.002 % | 0.02 % |

  The impact of a hit is low: a stranger appears in the lobby and the host kicks them. Only the host can start. **5 letters** gives a comfortable margin for one extra keystroke.
- **Rate limits for joins:** about 10 failed joins per minute per IP, then a 60 s cooldown. Add a global failed-join cap of about 120/min that also raises a log alert. Use the same generic 404 for every failure. **Size per-IP limits for one shared NAT IP:** on a school network all 4+ players can appear as the same public IP (U, but standard NAT behaviour). So cap *failures*, not successful joins, and allow at least 20 sessions per IP.

### 2.6 Playlists: client-local JSON first

- Typical party games keep custom setups local or in the room settings rather than in accounts (S/U: I did not find an authoritative survey. Pummel Party's minigame selection, for example, is a rule set chosen per session (S: [Steam forum](https://steamcommunity.com/app/880940/discussions/2/1733213724897019218))).
- **Recommendation:** ship built-in default playlists with the game. Save user playlists as JSON under `Application.persistentDataPath` (LocalLow on Windows, `~/Library/Application Support/...` on macOS) (V: [persistentDataPath](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/persistentdatapath)). The host sends the chosen playlist to the room. The server validates it: name 1-24 chars, 1-12 entries, each entry a mini-game ID from a **server-side allowlist**, and per-game settings clamped. The server then passes it to the game server at start.
- **Server SQLite is only worth it** if playlists must follow a player across machines or be shared by code ("playlist code" like a room code). That is a later add-on, and the lobby schema does not change.

### 2.7 Handing clients to the game server: one-time join tickets

1. On `start`, the lobby picks a free slot (1 or 2) and generates a random 32-byte **per-room key**. It spawns the headless server with the slot port, WebSocket path `/g/{slot}`, room ID, expected playerIds, playlist, and the key passed **via environment** (not argv, which `ps` shows). No long-lived shared secret exists, and nothing needs to go in git.
2. The server reports "ready" to the lobby's **internal** listener (`127.0.0.1:5081`, not routed by the tunnel), authenticated with the same key.
3. The lobby pushes each player its own ticket: `base64url(payload).base64url(HMAC-SHA256(roomKey, payload))`, where payload = `{v, room, slot, pid, exp: now+60s, nonce}`.
4. The client sets the ticket as NGO `NetworkConfig.ConnectionData` and connects to `wss://party.ezellm.com/g/{slot}`.
5. The server's `ConnectionApprovalCallback` verifies the HMAC with a constant-time compare, `exp`, `room`, that `pid` is expected and not already connected, and that the nonce is unused (in-memory set). It approves with `pid` mapped to `clientId`, otherwise it denies with a `Reason` the client can display (V: [NGO connection approval](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/connection-approval.html)). Keep `ClientConnectionBufferTimeout` short, e.g. 5-10 s (V: same page).
6. **Scores are keyed by `pid`, not `clientId`.** A dropped player calls `/rejoin`, gets a fresh ticket, and resumes with their score intact.

Why this design: NGO "doesn't encrypt or authenticate any of the raw information sent over connection approval" (V). The HMAC provides authenticity. WSS or DTLS provides confidentiality. Single use plus a 60 s expiry limits replay. This is a simplified version of netcode.io's connect-token design (AEAD-protected private part, expiry, and a "usable for exactly one connection" token history) (V: [netcode STANDARD.md](https://github.com/mas-bandwidth/netcode/blob/main/STANDARD.md)). The ticket verify code is plain C# and is **shared verbatim** between the lobby and the Unity server.

### 2.8 Quick play: yes, as a 20-line feature, not a queue

`POST /api/quickplay` joins the oldest *public*, open, non-full room, or creates a public room and waits. This matches Unity Lobby's QuickJoin semantics: a filtered join, with a 404 on no match, after which the client creates a lobby (V: [Unity quick join](https://docs.unity.com/lobby/quick-join)). With 1-2 rooms there is nothing to rank or match on. Skip skill, region and queue logic. Codes remain the main flow, because private rooms are never quick-joined.

---

## 3. Security checklist ("don't get hacked"), prioritized for a hobby server

### P0: do these before anything is public

1. **No inbound ports. Everything binds to 127.0.0.1.** Use a Tunnel only, remove router port forwards, and **turn UPnP off** (S: [tech-insider](https://tech-insider.org/?p=19643)). The lobby, room slots and internal API all listen on loopback. If the UDP fallback is used, forward only 7801-7802/udp and only during sessions.
2. **Admin only over Tailscale.** Use SSH with keys only (`PasswordAuthentication no`, no root login) and never forward port 22. Use a host firewall (ufw/nftables): default deny incoming, allow on `tailscale0`. **Do not use Docker port publishing with ufw:** Docker's published ports are "routed before the firewall rules can be applied" (V: [Docker docs](https://docs.docker.com/engine/network/packet-filtering-firewalls/)). Use plain systemd services instead, which is also less to learn. Put the `/admin` status page behind Cloudflare Access (free ≤ 50 users, S).
3. **Server-authoritative gameplay** (V for the principle: [Gambetta](https://gabrielgambetta.com/client-server-game-architecture.html)):
   - Clients send *inputs and intents* only. The server owns health, ammo, cooldowns, scores, hit detection and game-state transitions.
   - **Fire rate:** on a fire RPC, check `serverTime - lastShot ≥ 1/rate - tolerance`, plus ammo and alive state. The server spawns the projectile or raycast.
   - **Movement:** if client-authoritative movement is used for feel, the server checks each update: displacement ≤ `maxSpeed × dt × 1.25`. Reject teleports, out-of-bounds positions and NaN/Inf, then snap back. Prefer server-authoritative transforms where feel allows.
   - **Per-client RPC rate caps** (e.g. 60 msgs/s): drop excess, and **kick on sustained violation**. Log every violation with `pid`, which feeds the cheat report in section 5.
   - Never trust IDs in a payload. Take the sender from RPC params (`SenderClientId`). Validate every `NetworkObjectReference` the client sends.
4. **Join tickets checked in connection approval** (section 2.7). Cap at 4 connections per server. Deny everything else, including anything without a valid ticket.
5. **Input validation at every trust boundary** (V for the principle: [OWASP API4:2023](https://api-security.owasp.org/editions/2023/en/0xa4-unrestricted-resource-consumption)):
   - Player name: NFC-normalize, trim, 1-16 chars, allowlist `[\p{L}\p{N} _-]`. **Strip `<` and `>`**, because TextMeshPro rich text would otherwise let a name like `<size=400>` break every UI. A `<noparse>` wrapper is not enough on its own, since a name can contain `</noparse>` (V: [TMP noparse](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichTextNoParse.html)). Reject control, zero-width and bidi-override characters.
   - Playlist name 1-24 chars, same charset. Mini-game IDs from the allowlist. Numeric settings clamped.
   - Profanity filter: optional. Host kick plus a tiny blocklist is enough for a class project.
   - **Size caps:** Kestrel `MaxRequestBodySize` = 8 KB (the default is about 30 MB) (U on the exact default). WebSocket receive buffer 4 KB, closing with code 1009 if a message exceeds it. Reject unknown JSON fields.
6. **Rate limits in two layers:**
   - Cloudflare Free gives **1 rate-limit rule**, IP-based, with a 10 s period and a 10 s mitigation (V: [CF rate limiting](https://developers.cloudflare.com/waf/rate-limiting-rules/)). Put it on `party.ezellm.com/api/*` at about 100 req/10 s per IP. That is generous because of school NAT, and it stops floods before they reach the Pi.
   - App level: the built-in ASP.NET Core `RateLimiter` (V: [docs](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)). Use token buckets per session token for commands, the failed-join limiter from section 2.5, and concurrency caps: ≤ 20 sessions/IP, ≤ 8 WebSockets/IP, MaxRooms = 2. Partition by `CF-Connecting-IP`, which is safe because the lobby only accepts loopback connections from cloudflared. Microsoft warns that partitions keyed on user input are a memory DoS vector (V), so cap the partition count.

### P1: first week

7. **systemd sandbox under an unprivileged user** (V: [systemd.exec](https://www.freedesktop.org/software/systemd/man/latest/systemd.exec.html)). Spawned Unity servers inherit the sandbox, so set `MemoryMax`/`TasksMax` for the whole tree. Check the result with `systemd-analyze security partyshooter-lobby`.

   ```ini
   # /etc/systemd/system/partyshooter-lobby.service
   [Service]
   User=partyshooter
   Group=partyshooter
   WorkingDirectory=/opt/partyshooter
   ExecStart=/opt/partyshooter/lobby/Lobby
   Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
   StateDirectory=partyshooter            # /var/lib/partyshooter (match logs, optional SQLite)
   Restart=on-failure
   RestartSec=2
   NoNewPrivileges=yes
   ProtectSystem=strict
   ProtectHome=yes
   PrivateTmp=yes
   PrivateDevices=yes
   ProtectKernelTunables=yes
   ProtectKernelModules=yes
   ProtectControlGroups=yes
   LockPersonality=yes
   RestrictNamespaces=yes
   RestrictRealtime=yes
   RestrictSUIDSGID=yes
   RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
   CapabilityBoundingSet=
   SystemCallArchitectures=native
   SystemCallFilter=@system-service
   MemoryMax=1500M                        # lobby + up to 2 Unity servers (tune after measuring)
   TasksMax=512
   # MemoryDenyWriteExecute left OFF: "incompatible with programs ... that generate program code
   # dynamically at runtime" (the .NET JIT does this). Test it on the IL2CPP Unity server only if you split units.
   ```
8. **Secrets out of git.** There are only two: the tunnel credential JSON (`/etc/cloudflared/`, root 0600) and the optional DTLS server key (`LoadCredential=`, V). The per-room ticket key is ephemeral (section 2.7). Add `*.key`, `*.pem`, `*.env` and `cloudflared/*.json` to `.gitignore`, and enable GitHub secret scanning and push protection on the school repo. **Anything shipped in the Unity client is public.** Never put an "API key" or the DTLS private key in it (V: the UTP secure docs say to keep server certificates and keys out of client builds).
9. **Automatic security updates:** `unattended-upgrades` installs security updates by default, enabled with `dpkg-reconfigure unattended-upgrades` (V: [Debian wiki](https://wiki.debian.org/UnattendedUpgrades)). Raspberry Pi-repo packages may need their origin added (S: [Nextcloud forum](https://help.nextcloud.com/t/ncp-some-packages-are-not-updated/184084)). Install cloudflared from Cloudflare's apt repo so it updates too. Use **.NET 10 LTS** (supported until Nov 2028). .NET 8 and 9 reach end of support on **10 Nov 2026** (V: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)).
10. **Kill switch, three levels:**
    - `touch /etc/partyshooter/maintenance`: the lobby returns 503 with a message the game displays, and running games finish.
    - `sudo systemctl stop partyshooter-lobby` (also kills its child game servers).
    - `sudo systemctl stop cloudflared@partyshooter`: the game's own tunnel goes down and the website keeps running.
    - Plus: turn the router UDP forward off.

### P2: nice to have

11. **Encryption everywhere:** WSS comes free from Cloudflare. If the UDP path is used, enable DTLS with your own CA (section 1.6). Without encryption the join ticket is sniffable on the LAN, although single use plus expiry limits the damage.
12. **Structured logs with an alert on anomalies:** failed-join spikes, approval denials, violation kicks, 429 counts. Truncate or hash IPs in logs. Hashed or truncated IPs are personal data under GDPR/KVKK-style rules (U).
13. **Home-line DDoS:** with the Tunnel-only setup, floods hit Cloudflare and not the house. With the UDP fallback, apply the mitigations in section 1.6. Accept that a determined booter can take the house offline for the duration of the attack, and that the VPS forwarder is the real fix.

---

## 4. Backend tech for the lobby on a Pi 5

| | .NET 10 ASP.NET Core minimal API | Go (net/http + websocket lib) | Node (Fastify + ws) | Python (FastAPI + uvicorn) |
|---|---|---|---|---|
| Idle memory (ARM64) | ~17-18 MB working set with Native AOT on ARM (S: [Thinktecture](https://www.thinktecture.com/?p=21287)). Microsoft lab: simple API 96 MB default vs 41 MB AOT, arch unspecified (V: [ASP.NET in .NET 8](https://devblogs.microsoft.com/dotnet/announcing-asp-net-core-in-dotnet-8)) | ~10-20 MB (U) | ~40-70 MB (U) | ~40-80 MB (U) |
| CPU for 8 clients | trivial | trivial | trivial | trivial |
| Share schemas with Unity C# | **Same language.** Link one `Shared/` folder of DTO and ticket files | duplicate or codegen | duplicate or codegen | duplicate or codegen |
| Built in | rate limiter, WebSockets, JSON, HMAC, process spawn | most of it; WebSocket needs a library | needs plugins for WS and rate limiting | needs extras (slowapi etc.) |
| Deploy to Pi | `dotnet publish -r linux-arm64 --self-contained`, copy the folder | single static binary (best) | runtime + node_modules | venv |
| Team familiarity | the team already writes C# daily in Unity | new language | common among students | common among students |

On a Pi 5 with 4-16 GB of RAM, a footprint gap of 100 MB does not matter. **Schema and code sharing does matter.** The ticket HMAC, the code alphabet, the validation rules and the DTOs can be a single C# file set compiled into both the lobby and the Unity server and client. **Recommendation: ASP.NET Core minimal API on .NET 10 LTS**, published as a JIT self-contained build. Skip Native AOT, because it is not needed and it complicates cross-compiling from macOS/Windows (U).

Rules for shared C# (Unity 6.3 = **C# 9.0**, .NET Standard 2.1) (V: [Unity C# compiler](https://docs.unity3d.com/6000.3/Documentation/Manual/csharp-compiler.html), [ApiCompatibilityLevel](https://docs.unity3d.com/ScriptReference/ApiCompatibilityLevel.html)):
- Use plain classes with **public fields** and `[Serializable]`. **No `init` setters** (unsupported in Unity) and **no records in serialized types** (Unity serialization doesn't support them).
- JSON: use Newtonsoft in Unity (`com.unity.nuget.newtonsoft-json`) and System.Text.Json on the server with `IncludeFields = true` and `PropertyNamingPolicy = null`, so field names match exactly (U on the defaults; add a round-trip unit test). `JsonUtility` can't do dictionaries or top-level arrays, so avoid it for DTOs (U).
- Constant-time compare: write a 5-line loop in the shared file rather than relying on `CryptographicOperations` being available in Unity's profile (U).

Runner-up: Go, if a single static binary and the smallest RAM matter more than sharing code. That trade is not worth it here.

---

## 5. Observability: "reporting should be good"

### 5.1 What to record (small, fixed set)

| Source | Metrics / events |
|---|---|
| Game server, per room every 5 s | tick time p50/p95/max against budget (33 ms at 30 Hz), players connected, per-player **RTT** (`UnityTransport.GetCurrentRtt(clientId)`, V: [API](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.Transports.UTP.UnityTransport.html)), per-player **packet loss** (UTP **2.7.0+** `NetworkDriver.GetConnectionStatistics`, which covers "ping … packet loss, duplication", added 2026-02-25, V: [UTP changelog](https://docs.unity3d.com/Packages/com.unity.transport@6.5/changelog/CHANGELOG.html); reaching the driver from NGO's `UnityTransport` is U), bytes in/out per second, transport mode (WSS/UDP). |
| Game server events | approval accepted/denied (with reason), disconnect (with reason), validation violations and kicks, mini-game start/end, scores, champion. |
| Lobby | rooms created/started/closed, joins ok/failed (by reason), quick-play hits, active sessions/WebSockets, 429s, HTTP latency. ASP.NET Core's built-in metrics can be watched live with `dotnet-counters`, with no extra infrastructure (V: [rate limiting metrics](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)). |
| Pi host | CPU %, RAM, **SoC temperature and throttling** (`vcgencmd measure_temp`, `vcgencmd get_throttled`. Bits 0-3 mean under-voltage, ARM frequency capped, currently throttled, soft temp limit) (V: [Raspberry Pi docs](https://www.raspberrypi.com/documentation/computers/os.html)). A throttled Pi shows up as tick-time spikes. |

### 5.2 Minimal way to view it

- **Structured JSON lines to stdout → journald.** Use `builder.Logging.AddJsonConsole()` in the lobby. Unity server logs go to stdout via `-logFile -`, with a small helper that prints one JSON line per stat sample or event. View with `journalctl -u partyshooter-lobby -o cat | jq`. No Prometheus or Grafana. They are an upgrade path only.
- **`GET /api/status` (public):** `{ok, version, uptime, rooms, players}`, no personal data. Point a free external uptime monitor at it (U).
- **`/admin` (behind Cloudflare Access):** one server-rendered HTML page showing live rooms, per-player RTT/loss/mode, tick p95, Pi temperature and throttle flags, and the last 100 events from an in-memory ring buffer.
- **Match report:** at playlist end, the server posts a summary to the internal endpoint, and the lobby appends it to `/var/lib/partyshooter/matches.jsonl`: date, duration, playlist, scores, champion, median and p95 RTT, loss, max tick, violations. This one file produces the course-report numbers ("N sessions, median RTT X ms, p95 tick Y ms").

### 5.3 In-game network overlay (players expect it)

- Toggle on F3 or in settings: **ping (ms)**, **loss %**, server tick rate and server tick p95 (sent by the server in a NetworkVariable once per second), **transport mode** (WSS/UDP), FPS.
- An always-on small **connection-quality icon** that turns yellow/red past thresholds, e.g. RTT > 120 ms or loss > 3% (U, tune these).
- Show each player's ping in the lobby list. The WebSocket heartbeat RTT gives it for free.
- For dev builds, Unity's **Runtime Network Stats Monitor** (Multiplayer Tools) is a configurable on-screen stats overlay that runs in standalone builds (V: [RNSM](https://mp-docs.dl.it.unity3d.com/tools/1.1.0/RNSM)).
- **Test bad networks on purpose:** UTP's `DebugSimulator` is obsolete; use "Network Simulator from the Multiplayer Tools package" (V: UnityTransport API). Also test from a phone hotspot (mimics a foreign network) and with outbound UDP blocked by the OS firewall (mimics school).

---

## 6. Things to verify on the real Pi before committing (ordered by risk)

1. A Linux **ARM64** headless server build from 6000.3.9f1 runs on the Pi 5. Measure tick time with 4 bots, two rooms at once.
2. The UTP WebSocket path through the real tunnel works (`wss://party.ezellm.com/g/1`), and measured RTT through Cloudflare compares acceptably with LAN.
3. How ezellm.com is currently exposed (section 1.1). Whether the line is behind CGNAT.
4. Bot Fight Mode / security level on the zone do not challenge Unity clients (test with `curl` and the game).
5. `ClientWebSocket` in an IL2CPP standalone build on Windows and macOS.
6. Whether UTP 2.7 (for loss stats) is compatible with the NGO version Unity 6.3 resolves.

---

## 7. Sources

Cloudflare
- Tunnel protocols: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/protocols/
- Tunnel DNS records: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/dns/
- Tunnel config file / ingress: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/local-management/configuration-file/
- UDP improvements changelog (2025-07-15): https://developers.cloudflare.com/changelog/2025-07-15-udp-improvements/
- Private networks via WARP: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/private-net/cloudflared/connect-cidr and https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/private-net/create-private-networks
- Spectrum protocols per plan: https://developers.cloudflare.com/spectrum/protocols-per-plan/
- WebSockets: https://developers.cloudflare.com/network/websockets/
- Connection limits: https://developers.cloudflare.com/fundamentals/reference/connection-limits/
- Proxy status / DNS-only exposure: https://developers.cloudflare.com/dns/proxy-status/
- Rate limiting rules: https://developers.cloudflare.com/waf/rate-limiting-rules/
- Bot Fight Mode (cannot be skipped): https://developers.cloudflare.com/learning-paths/modules/get-started/security-free/bot-fight-mode/ and https://developers.cloudflare.com/waf/custom-rules/skip/options
- ToS change: https://blog.cloudflare.com/updated-tos/ and https://www.cloudflare.com/service-specific-terms-application-services/
- Zero Trust free plan: https://blog.cloudflare.com/teams-plans
- Community, WebSocket 100 s: https://community.cloudflare.com/t/cloudflare-websocket-timeout/5865 and https://github.com/buildbot/buildbot/issues/4078
- Community, Spectrum Minecraft pricing: https://community.cloudflare.com/t/minecraft-server-gaming-server/163826

Unity
- UTP WebSockets: https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/websockets.html
- UTP secure client/server: https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/client-server-secure.html
- UTP manual (MultiNetworkDriver): https://docs.unity3d.com/Packages/com.unity.transport@2.5/manual/
- UTP changelog (GetConnectionStatistics 2.7.0): https://docs.unity3d.com/Packages/com.unity.transport@6.5/changelog/CHANGELOG.html
- NGO UnityTransport API: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/api/Unity.Netcode.Transports.UTP.UnityTransport.html
- NGO connection approval: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.4/manual/basics/connection-approval.html
- Dedicated server build: https://docs.unity3d.com/6000.3/Documentation/Manual/dedicated-server-build.html
- ARM64 server build report: https://discussions.unity.com/t/in-118136-fail-to-build-linux-server-arm64-conversion-to-bee-architecture-not-implemented-for-linux-architecture-arm64/1685359
- C# compiler (C# 9.0): https://docs.unity3d.com/6000.3/Documentation/Manual/csharp-compiler.html
- persistentDataPath: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/persistentdatapath
- Lobby glossary, heartbeat, host migration, quick join: https://docs.unity.com/lobby/glossary, https://support.unity.com/hc/en-us/articles/4408402562580-Understanding-and-Implementing-Lobby-Heartbeats, https://docs.unity.com/lobby/host-migration, https://docs.unity.com/lobby/quick-join
- UGS session host migration: https://docs.unity.com/ugs/en-us/manual/lobby/manual/host-migration
- Relay pricing: https://unity.com/products/gaming-services/pricing and https://support.unity.com/hc/en-us/articles/4410136449812-How-is-the-Relay-Service-Priced
- School-network Relay thread: https://discussions.unity.com/t/cannot-connect-to-relay-server-919217/919217
- RNSM: https://mp-docs.dl.it.unity3d.com/tools/1.1.0/RNSM
- TMP noparse: https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichTextNoParse.html

Security, networking, platform
- OWASP API4:2023: https://api-security.owasp.org/editions/2023/en/0xa4-unrestricted-resource-consumption
- netcode.io standard (connect tokens): https://github.com/mas-bandwidth/netcode/blob/main/STANDARD.md
- Gaffer On Games, UDP vs TCP: https://gafferongames.com/post/udp_vs_tcp/
- Gambetta, authoritative server: https://gabrielgambetta.com/client-server-game-architecture.html
- systemd.exec: https://www.freedesktop.org/software/systemd/man/latest/systemd.exec.html
- Docker and ufw: https://docs.docker.com/engine/network/packet-filtering-firewalls/
- Debian unattended upgrades: https://wiki.debian.org/UnattendedUpgrades
- Raspberry Pi vcgencmd: https://www.raspberrypi.com/documentation/computers/os.html
- Tailscale Funnel: https://tailscale.com/kb/1223/funnel
- ASP.NET Core rate limiting: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
- ASP.NET Core WebSockets: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- .NET 8 ASP.NET AOT numbers: https://devblogs.microsoft.com/dotnet/announcing-asp-net-core-in-dotnet-8 ; Thinktecture ARM numbers: https://www.thinktecture.com/?p=21287
- Base32 for humans (IETF draft): https://www.ietf.org/archive/id/draft-crockford-davis-base32-for-humans-01.html
- Among Us codes: https://hindenburg.js.org/pages/plugins/api/game-codes.html, https://devtrackers.gg/among-us/p/003032ff-pinned-servers-update-2020-9-9-beta
- UDP blocking data: https://arxiv.org/pdf/1612.07816 ; campus policies: https://www.gettysburg.edu/offices/information-technology/pdfs/2019/GameandPortPolicy.pdf, https://kb.iu.edu/d/apjc, https://kb.wisc.edu/ns/16575
- CGNAT detection: https://docs.ipcheck.ing/knowledge-base/diagnose/am-i-behind-cgnat
- Home DDoS context: https://docs.tcpshield.com/miscellaneous/protect-a-home-hosted-server
- Origin discovery: https://blog.christophetd.fr/bypassing-cloudflare-using-internet-wide-scan-data/
- Hetzner pricing (secondary): https://costgoat.com/pricing/hetzner ; playit.gg (secondary): https://www.teklynk.com/blog/how-to-host-a-game-server-from-your-home-using-playItgg
