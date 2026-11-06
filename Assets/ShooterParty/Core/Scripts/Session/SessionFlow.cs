using System;
using System.Collections.Generic;
using System.Linq;

namespace ShooterParty
{
    // The session's rules as a plain state machine (no Unity networking), so they are unit-tested on a fake clock.
    // SessionManager owns one on the server and mirrors it to every client.
    //
    //   Lobby -> Loading -> Intro(4 s) -> Countdown(3 s) -> Playing -> RoundResults(6 s) -> Standings(6 s)
    //         -> (more games ? Loading : Final(12 s, in the lobby scene)) -> Lobby
    public sealed class SessionFlow
    {
        public struct Options
        {
            public int minPlayers, maxPlayers;
            public bool replaceLeavers, fillWithBots;
        }

        public const float IntroSeconds = 4f, CountdownSeconds = 3f, ResultsSeconds = 6f, StandingsSeconds = 6f, FinalSeconds = 12f;
        public const float RejoinGraceSeconds = 60f;
        public static float TimeScaleForTests = 1f;
        public static readonly string[] BotNames = { "Mochi", "Zesty", "Grape", "Tofu" };

        public string LobbyScene = "SP_Lobby";
        public SessionPhase Phase { get; private set; } = SessionPhase.Lobby;
        public double PhaseEndsAt { get; private set; } = double.PositiveInfinity;
        public int RoundIndex { get; private set; } = -1;
        public string PlaylistName { get; private set; } = "";
        public IReadOnlyList<string> Playlist => playlist;
        public IReadOnlyList<PlayerState> Players => players;
        public IReadOnlyList<IReadOnlyList<RankEntry>> Rankings => rankings;
        public bool ReplaceLeavers => options.replaceLeavers;
        public int MinPlayers => options.minPlayers;
        public MiniGameDefinition CurrentGame => RoundIndex >= 0 && RoundIndex < playlist.Count ? find(playlist[RoundIndex]) : null;

        public event Action<SessionPhase> PhaseChanged;
        public event Action<int, string> Notice;   // (seat, "Bea joined"): shown to everyone else as a toast
        public event Action<string> LoadScene;         // manager: NetworkSceneManager.LoadScene(name, Single)
        public event Action SetupRound;                // manager: spawn the mini-game controller, ServerSetupRound
        public event Action StartRound;                // manager: controller.ServerStartRound
        public event Action<int> PlayerOutMidRound;    // manager: controller.ServerPlayerLeft(slot)
        public event Action Changed;                   // anything replicated changed

        Options options;   // replaceLeavers is a lobby setting the leader can change
        readonly Func<string, MiniGameDefinition> find;
        readonly List<PlayerState> players = new List<PlayerState>();
        readonly List<string> playlist = new List<string>();
        readonly List<IReadOnlyList<RankEntry>> rankings = new List<IReadOnlyList<RankEntry>>();

        public SessionFlow(Options options, Func<string, MiniGameDefinition> find)
        {
            this.options = options;
            this.find = find;
            if (this.options.maxPlayers <= 0) this.options.maxPlayers = 4;
        }

        static float Scaled(float s) => s * TimeScaleForTests;
        int Index(string pid) => players.FindIndex(p => p.pid.ToString() == pid);
        int Humans => players.Count(p => !p.isBot && p.eligible);
        public double LastDropAt { get; private set; } = double.NegativeInfinity;   // when a connection last dropped (not a clean leave)
        int ConnectedHumans => players.Count(p => !p.isBot && p.connected);

        // ------------------------------------------------------------------ membership
        // Returns the seat, or -1 with a player-facing error. A known pid reconnects to its seat.
        public int AddPlayer(string pid, string name, bool isBot, ulong clientId, double now, out string error)
        {
            error = null;
            int known = Index(pid);
            if (known >= 0)
            {
                var p = players[known];
                bool back = !p.connected;
                // a rejoin can beat the server to noticing the old connection's end: that critter falls all the same
                if (!back && p.clientId != clientId && Phase == SessionPhase.Playing) PlayerOutMidRound?.Invoke(p.slot);
                p.connected = true; p.clientId = clientId; p.eligible = true;
                p.isBot = false;   // a bot kept the seat warm; the human drives it again from the next round
                players[known] = p;
                if (back) Notice?.Invoke(p.slot, p.name + " is back");
                Changed?.Invoke();
                return p.slot;
            }
            if (Phase != SessionPhase.Lobby) { error = "Session in progress"; return -1; }
            if (!isBot && options.fillWithBots) RemoveOneBot();
            if (players.Count >= options.maxPlayers) { error = "Room is full"; return -1; }
            int slot = Enumerable.Range(0, options.maxPlayers).First(s => players.All(p => p.slot != s));
            var state = new PlayerState
            {
                slot = (byte)slot, colour = (byte)FreeColour(slot), pid = pid, clientId = clientId,
                name = NameRules.Unique(name, players.Select(p => p.name.ToString())),
                isBot = isBot, connected = true, ready = isBot, eligible = true, joinedAt = now,
                leader = !isBot && players.All(p => p.isBot || !p.leader),
            };
            players.Add(state);
            players.Sort((a, b) => a.slot.CompareTo(b.slot));
            if (!isBot && options.fillWithBots) FillBots(now);
            if (!isBot) Notice?.Invoke(slot, state.name + " joined");
            Changed?.Invoke();
            return slot;
        }

        void FillBots(double now)
        {
            int i = 0;
            while (players.Count < options.maxPlayers)
            {
                var name = BotNames.First(n => players.All(p => p.name.ToString() != n));
                AddPlayer("bot-" + (++i) + "-" + name, name, true, 0, now, out _);
            }
        }

        void RemoveOneBot()
        {
            int bot = players.FindLastIndex(p => p.isBot);
            if (bot >= 0 && players.Count >= options.maxPlayers) players.RemoveAt(bot);
        }

        int FreeColour(int preferred)
        {
            if (players.All(p => p.colour != preferred)) return preferred;
            return Enumerable.Range(0, 4).First(c => players.All(p => p.colour != c));
        }

        // The same rules as AddPlayer, asked before a connection is approved.
        public string CanJoin(string pid)
        {
            if (Index(pid) >= 0) return null;
            if (Phase != SessionPhase.Lobby) return "Session in progress";
            bool botCanMakeRoom = options.fillWithBots && players.Any(p => p.isBot);
            if (players.Count >= options.maxPlayers && !botCanMakeRoom) return "Room is full";
            return null;
        }

        public void SetRtt(string pid, int ms) => Edit(pid, (ref PlayerState p) => p.rttMs = (ushort)Math.Max(0, Math.Min(ms, ushort.MaxValue)));

        // A clean leave (menu -> Leave). In the lobby the seat empties; mid-session it stays, out of the running.
        public void Remove(string pid, bool clean, double now) => Remove(pid, now, " left");

        void Remove(string pid, double now, string what)
        {
            int i = Index(pid);
            if (i < 0) return;
            var p = players[i];
            Notice?.Invoke(p.slot, p.name + what);
            bool wasLeader = p.leader;
            if (Phase == SessionPhase.Lobby) players.RemoveAt(i);
            else
            {
                if (Phase == SessionPhase.Playing && p.connected) PlayerOutMidRound?.Invoke(p.slot);
                p.connected = false; p.eligible = false; p.leader = false; p.ready = false; p.disconnectedAt = now;
                players[i] = p;
            }
            if (wasLeader) PassLeadership();
            CheckTooFewHumans(now);
            Changed?.Invoke();
        }

        // The connection dropped: the seat and points are held for RejoinGraceSeconds.
        // clientId: the connection that ended. A seat already on a newer connection (rejoined first) is left alone.
        public void Disconnected(string pid, double now, ulong? clientId = null)
        {
            int i = Index(pid);
            if (i < 0) return;
            if (clientId.HasValue && players[i].clientId != clientId.Value) return;
            LastDropAt = now;
            if (Phase == SessionPhase.Lobby) { Remove(pid, now, " disconnected"); return; }
            var p = players[i];
            if (p.connected) Notice?.Invoke(p.slot, p.name + " disconnected");
            if (Phase == SessionPhase.Playing && p.connected) PlayerOutMidRound?.Invoke(p.slot);
            bool wasLeader = p.leader;
            p.connected = false; p.leader = false; p.disconnectedAt = now;
            players[i] = p;
            if (wasLeader) PassLeadership();
            Changed?.Invoke();
        }

        void PassLeadership()
        {
            for (int i = 0; i < players.Count; i++) if (players[i].leader) { var q = players[i]; q.leader = false; players[i] = q; }
            int next = -1;
            for (int i = 0; i < players.Count; i++)
                if (!players[i].isBot && players[i].connected && (next < 0 || players[i].joinedAt < players[next].joinedAt)) next = i;
            if (next < 0) return;
            var n = players[next]; n.leader = true; players[next] = n;
        }

        public string Kick(string byPid, int slot)
        {
            if (Phase != SessionPhase.Lobby || !IsLeader(byPid)) return null;
            int i = players.FindIndex(p => p.slot == slot);
            if (i < 0 || players[i].pid.ToString() == byPid) return null;
            var pid = players[i].pid.ToString();
            Notice?.Invoke(players[i].slot, players[i].name + " was kicked");
            players.RemoveAt(i);
            Changed?.Invoke();
            return pid;
        }

        public bool IsLeader(string pid) { int i = Index(pid); return i >= 0 && players[i].leader; }

        // ------------------------------------------------------------------ lobby settings
        public void SetReady(string pid, bool ready) => Edit(pid, (ref PlayerState p) => p.ready = ready || p.isBot);

        public void CycleColour(string pid) => Edit(pid, (ref PlayerState p) =>
        {
            for (int k = 1; k <= 4; k++)
            {
                int c = (p.colour + k) % 4;
                var me = p.pid;
                if (players.All(o => o.pid == me || o.colour != c)) { p.colour = (byte)c; return; }
            }
        });

        public bool SetPlaylist(string pid, string name, IReadOnlyList<string> ids, out string error)
        {
            error = null;
            if (Phase != SessionPhase.Lobby) { error = "The session already started"; return false; }
            if (!IsLeader(pid)) { error = "Only the leader picks the playlist"; return false; }
            return UsePlaylist(name, ids, out error);
        }

        // The server's own pick (the room's default playlist), validated like the leader's.
        public bool UsePlaylist(string name, IReadOnlyList<string> ids, out string error)
        {
            if (!PlaylistRules.TryValidate(name, ids, id => find(id) != null, out error)) return false;
            NameRules.TryClean(name, 1, PlaylistRules.MaxNameLength, "Playlist name", out var clean, out _);
            PlaylistName = clean;
            playlist.Clear();
            playlist.AddRange(ids);
            Changed?.Invoke();
            return true;
        }

        public void SetReplaceLeavers(string pid, bool on)
        {
            if (Phase != SessionPhase.Lobby || !IsLeader(pid)) return;
            options.replaceLeavers = on;
            Changed?.Invoke();
        }

        delegate void Mutation(ref PlayerState p);

        void Edit(string pid, Mutation change)
        {
            int i = Index(pid);
            if (i < 0) return;
            var p = players[i];
            change(ref p);
            players[i] = p;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ flow
        public string TryStart(string pid, double now)
        {
            if (Phase != SessionPhase.Lobby) return "The session already started";
            if (!IsLeader(pid)) return "Only the leader can start";
            var blocker = StartBlocker(players, options.minPlayers, playlist.Count);
            if (blocker != null) return blocker;
            players.RemoveAll(p => !p.isBot && !p.connected);
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.total = 0; p.wins = 0; p.lastRank = 0; p.lastPoints = 0; p.roundScore = 0; p.eligible = true;
                players[i] = p;
            }
            rankings.Clear();
            RoundIndex = -1;
            NextGame(now);
            return null;
        }

        // Why the leader can't start yet, or null. Clients run it on the replicated seats to explain a disabled Start.
        public static string StartBlocker(IEnumerable<PlayerState> seats, int minPlayers, int playlistCount)
        {
            var humans = seats.Where(p => !p.isBot && p.connected).ToList();
            if (humans.Count < minPlayers) return $"Need at least {minPlayers} players";
            if (humans.Any(p => !p.leader && !p.ready)) return "Everyone must be ready";
            if (playlistCount == 0) return "Pick a playlist first";
            return null;
        }

        void NextGame(double now)
        {
            RoundIndex++;
            var game = CurrentGame;
            if (game == null) { EnterFinal(now); return; }
            Enter(SessionPhase.Loading, double.PositiveInfinity);
            LoadScene?.Invoke(game.sceneName);
        }

        void EnterFinal(double now)
        {
            Enter(SessionPhase.Final, now + Scaled(FinalSeconds));
            LoadScene?.Invoke(LobbyScene);
        }

        public void SceneLoaded(string scene, double now)
        {
            if (Phase != SessionPhase.Loading || CurrentGame == null || scene != CurrentGame.sceneName) return;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.roundScore = 0;
                if (!p.isBot && !p.connected && options.replaceLeavers)   // a bot drives the leaver's seat
                {
                    p.isBot = true;
                    Notice?.Invoke(p.slot, p.name + " replaced by a bot");
                }
                players[i] = p;
            }
            Enter(SessionPhase.Intro, now + Scaled(IntroSeconds));
            SetupRound?.Invoke();
        }

        // Who plays the round that is being set up: connected humans plus bots.
        public List<SlotInfo> RoundSlots() =>
            players.Where(p => p.isBot || p.connected).Select(p => new SlotInfo
            {
                slot = p.slot, colourIndex = p.colour, isBot = p.isBot,
                ownerClientId = p.isBot ? 0ul : p.clientId,
                mods = ScoreRules.ModifiersFor(p.slot, Standings()),
            }).ToList();

        public void RoundDone(IReadOnlyList<RankEntry> ranking, double now)
        {
            if (Phase != SessionPhase.Playing) return;
            var game = CurrentGame;
            int slots = players.Count == 0 ? 0 : players.Max(p => p.slot) + 1;
            var points = ScoreRules.Award(ranking, game ? game.placementPoints : new[] { 4, 3, 2, 1 }, Math.Max(slots, 4));
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                p.total += points[p.slot];
                p.lastPoints = points[p.slot];
                var e = ranking.FirstOrDefault(r => r.slot == p.slot);
                p.lastRank = e.rank > 0 ? e.rank : players.Count;
                if (e.rank == 1) p.wins++;
                players[i] = p;
            }
            rankings.Add(ranking.ToList());
            Enter(SessionPhase.RoundResults, now + Scaled(ResultsSeconds));
        }

        public void SetRoundScores(IReadOnlyList<(int slot, int score)> scores)
        {
            bool any = false;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                foreach (var (slot, score) in scores)
                    if (slot == p.slot && p.roundScore != score) { p.roundScore = score; players[i] = p; any = true; }
            }
            if (any) Changed?.Invoke();
        }

        public IReadOnlyList<SlotTotals> Standings() => TotalsOf(players);

        // The seats as ScoreRules sees them; clients use it on the replicated seats to name the champion.
        public static List<SlotTotals> TotalsOf(IEnumerable<PlayerState> seats) =>
            seats.Select(p => new SlotTotals { slot = p.slot, total = p.total, wins = p.wins, lastRoundRank = p.lastRank == 0 ? 99 : p.lastRank, eligible = p.eligible }).ToList();

        public IReadOnlyList<int> Champions() => ScoreRules.Champions(Standings());

        public void Tick(double now)
        {
            ExpireLeavers(now);
            CheckTooFewHumans(now);
            // phases chain on their scheduled end times, so a late tick still lands in the right phase
            for (int guard = 0; guard < 8 && now >= PhaseEndsAt; guard++)
            {
                double t = PhaseEndsAt;
                switch (Phase)
                {
                    case SessionPhase.Intro: Enter(SessionPhase.Countdown, t + Scaled(CountdownSeconds)); break;
                    case SessionPhase.Countdown: Enter(SessionPhase.Playing, double.PositiveInfinity); StartRound?.Invoke(); break;
                    case SessionPhase.RoundResults: Enter(SessionPhase.Standings, t + Scaled(StandingsSeconds)); break;
                    case SessionPhase.Standings: NextGame(t); break;
                    case SessionPhase.Final:
                        for (int i = 0; i < players.Count; i++) { var p = players[i]; p.ready = p.isBot; players[i] = p; }
                        players.RemoveAll(p => !p.connected);   // gone humans, and the bots that stood in for them
                        RoundIndex = -1;
                        Enter(SessionPhase.Lobby, double.PositiveInfinity);
                        break;
                    default: return;
                }
            }
        }

        void ExpireLeavers(double now)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.isBot && !p.connected && p.eligible && now - p.disconnectedAt > RejoinGraceSeconds)
                {
                    p.eligible = false;
                    players[i] = p;
                    Changed?.Invoke();
                }
            }
        }

        void CheckTooFewHumans(double now)
        {
            if (Phase == SessionPhase.Lobby || Phase == SessionPhase.Final) return;
            // seats still held for a rejoin count: a tunnel blip that drops everyone must not end the session
            if (Humans < Math.Min(2, options.minPlayers)) EnterFinal(now);
        }

        void Enter(SessionPhase phase, double endsAt)
        {
            Phase = phase;
            PhaseEndsAt = endsAt;
            PhaseChanged?.Invoke(phase);
            Changed?.Invoke();
        }
    }
}
