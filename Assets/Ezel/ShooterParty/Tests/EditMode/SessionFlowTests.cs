using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShooterParty.Tests
{
    public class SessionFlowTests
    {
        MiniGameDefinition game;
        SessionFlow flow;
        double now;
        readonly List<string> loads = new List<string>();
        readonly List<int> droppedMidRound = new List<int>();
        int setups, starts;

        [SetUp]
        public void SetUp()
        {
            game = ScriptableObject.CreateInstance<MiniGameDefinition>();
            game.id = "knockback_arena"; game.sceneName = "KnockbackArena_Ice"; game.placementPoints = new[] { 4, 3, 2, 1 };
            New(new SessionFlow.Options { minPlayers = 3, maxPlayers = 4 });
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(game);

        void New(SessionFlow.Options o)
        {
            flow = new SessionFlow(o, id => id == game.id ? game : null);
            now = 0;
            loads.Clear(); droppedMidRound.Clear(); setups = starts = 0;
            flow.LoadScene += loads.Add;
            flow.SetupRound += () => setups++;
            flow.StartRound += () => starts++;
            flow.PlayerOutMidRound += droppedMidRound.Add;
        }

        int Join(string pid, string name = null, bool bot = false)
        {
            int slot = flow.AddPlayer(pid, name ?? pid, bot, (ulong)(flow.Players.Count + 1), now, out var error);
            Assert.GreaterOrEqual(slot, 0, error);
            now += 1;
            return slot;
        }

        void ReadyAll() { foreach (var p in flow.Players.ToList()) flow.SetReady(p.pid.ToString(), true); }
        void Playlist(int n) => Assert.IsTrue(flow.SetPlaylist("a", "Test", Enumerable.Repeat(game.id, n).ToList(), out var e), e);
        PlayerState P(string pid) => flow.Players.First(p => p.pid.ToString() == pid);

        void Advance(double seconds) { now += seconds; flow.Tick(now); }

        static List<RankEntry> Ranking(params int[] slotsBestFirst) => slotsBestFirst.Select((s, i) => new RankEntry { slot = s, rank = i + 1 }).ToList();

        void StartGame(int games = 2)
        {
            Join("a"); Join("b"); Join("c");
            Playlist(games);
            ReadyAll();
            Assert.IsNull(flow.TryStart("a", now));
        }

        void PlayRound(params int[] order)
        {
            Assert.AreEqual(SessionPhase.Loading, flow.Phase);
            flow.SceneLoaded(game.sceneName, now);
            Assert.AreEqual(SessionPhase.Intro, flow.Phase);
            Advance(SessionFlow.IntroSeconds);
            Assert.AreEqual(SessionPhase.Countdown, flow.Phase);
            Advance(SessionFlow.CountdownSeconds);
            Assert.AreEqual(SessionPhase.Playing, flow.Phase);
            flow.RoundDone(Ranking(order), now);
            Assert.AreEqual(SessionPhase.RoundResults, flow.Phase);
            Advance(SessionFlow.ResultsSeconds);
            Assert.AreEqual(SessionPhase.Standings, flow.Phase);
            Advance(SessionFlow.StandingsSeconds);
        }

        [Test]
        public void StartNeedsMinHumansAllReadyAndLeader()
        {
            Join("a"); Join("b");
            Playlist(1);
            ReadyAll();
            Assert.AreEqual("Need at least 3 players", flow.TryStart("a", now));
            Join("c");
            Assert.AreEqual("Everyone must be ready", flow.TryStart("a", now));
            ReadyAll();
            Assert.AreEqual("Only the leader can start", flow.TryStart("b", now));
            Assert.IsNull(flow.TryStart("a", now));
            Assert.AreEqual(SessionPhase.Loading, flow.Phase);
            CollectionAssert.AreEqual(new[] { "KnockbackArena_Ice" }, loads);
        }

        [Test]
        public void StartNeedsAPlaylist()
        {
            Join("a"); Join("b"); Join("c"); ReadyAll();
            Assert.AreEqual("Pick a playlist first", flow.TryStart("a", now));
        }

        [Test]
        public void PhasesFollowSpecTimings()
        {
            Assert.AreEqual(4f, SessionFlow.IntroSeconds); Assert.AreEqual(3f, SessionFlow.CountdownSeconds);
            Assert.AreEqual(6f, SessionFlow.ResultsSeconds); Assert.AreEqual(6f, SessionFlow.StandingsSeconds);
            Assert.AreEqual(12f, SessionFlow.FinalSeconds);
            StartGame(1);
            flow.SceneLoaded(game.sceneName, now);
            Assert.AreEqual(1, setups);
            Assert.AreEqual(now + 4, flow.PhaseEndsAt, 1e-9);
            Advance(3.9); Assert.AreEqual(SessionPhase.Intro, flow.Phase);
            Advance(0.11); Assert.AreEqual(SessionPhase.Countdown, flow.Phase);
            Advance(3); Assert.AreEqual(SessionPhase.Playing, flow.Phase); Assert.AreEqual(1, starts);
        }

        [Test]
        public void TwoRoundPlaylistReachesFinalThenLobby()
        {
            StartGame(2);
            PlayRound(0, 1, 2);
            Assert.AreEqual(SessionPhase.Loading, flow.Phase, "second game loads");
            PlayRound(1, 0, 2);
            Assert.AreEqual(SessionPhase.Final, flow.Phase);
            Assert.AreEqual(flow.LobbyScene, loads.Last(), "the final plays in the lobby scene");
            Assert.AreEqual(4 + 3, P("a").total); Assert.AreEqual(3 + 4, P("b").total); Assert.AreEqual(2 + 2, P("c").total);
            Assert.AreEqual(1, P("a").wins); Assert.AreEqual(1, P("b").wins);
            Advance(SessionFlow.FinalSeconds);
            Assert.AreEqual(SessionPhase.Lobby, flow.Phase);
            Assert.IsFalse(P("b").ready, "ready resets for the next session");
        }

        [Test]
        public void ChampionUsesWinsThenLastRound()
        {
            StartGame(2);
            PlayRound(0, 1, 2);
            PlayRound(1, 0, 2);
            CollectionAssert.AreEqual(new[] { 1 }, flow.Champions(), "tie on 7 points and 1 win: b was better in the last round");
        }

        [Test]
        public void LeaderLeavesPassesLeadership()
        {
            Join("a"); Join("b"); Join("c");
            Assert.IsTrue(P("a").leader);
            flow.Remove("a", clean: true, now);
            Assert.IsTrue(P("b").leader, "the longest-present player leads");
            Assert.IsFalse(P("c").leader);
        }

        [Test]
        public void FewerThanTwoHumansJumpsToFinal()
        {
            StartGame(3);
            flow.SceneLoaded(game.sceneName, now);
            flow.Remove("b", clean: true, now);
            flow.Remove("c", clean: true, now);
            flow.Tick(now);
            Assert.AreEqual(SessionPhase.Final, flow.Phase);
        }

        [Test]
        public void BackToLobbyNeedsThreeAgain()
        {
            StartGame(1);
            PlayRound(0, 1, 2);
            Advance(SessionFlow.FinalSeconds);
            Assert.AreEqual(SessionPhase.Lobby, flow.Phase);
            flow.Remove("c", clean: true, now);
            ReadyAll();
            Assert.AreEqual("Need at least 3 players", flow.TryStart("a", now));
        }

        [Test]
        public void DuplicateNamesAreSuffixedOnJoin()
        {
            Join("a", "Mochi"); Join("b", "Mochi"); Join("c", "mochi");
            CollectionAssert.AreEqual(new[] { "Mochi", "Mochi 2", "mochi 3" }, flow.Players.Select(p => p.name.ToString()).ToArray());
        }

        [Test]
        public void FullRoomAndInProgressAreRefused()
        {
            Join("a"); Join("b"); Join("c"); Join("d");
            Assert.Less(flow.AddPlayer("e", "e", false, 9, now, out var full), 0);
            Assert.AreEqual("Room is full", full);
            flow.Remove("d", true, now);
            Playlist(1); ReadyAll(); flow.TryStart("a", now);
            Assert.Less(flow.AddPlayer("e", "e", false, 9, now, out var busy), 0);
            Assert.AreEqual("Session in progress", busy);
        }

        [Test]
        public void DisconnectMidRoundCountsAsFallAndRejoinKeepsPoints()
        {
            StartGame(2);
            PlayRound(2, 1, 0);
            int cTotal = P("c").total;
            flow.SceneLoaded(game.sceneName, now);
            Advance(SessionFlow.IntroSeconds + SessionFlow.CountdownSeconds);
            flow.Disconnected("c", now);
            CollectionAssert.AreEqual(new[] { 2 }, droppedMidRound);
            Assert.IsFalse(P("c").connected);
            Assert.AreEqual(2, flow.AddPlayer("c", "c", false, 42, now, out _), "rejoining reclaims the slot");
            Assert.IsTrue(P("c").connected); Assert.AreEqual(cTotal, P("c").total); Assert.AreEqual(42ul, P("c").clientId);
        }

        [Test]
        public void ReplaceLeaversBotsFromNextRound()
        {
            New(new SessionFlow.Options { minPlayers = 3, maxPlayers = 4, replaceLeavers = true });
            StartGame(2);
            PlayRound(0, 1, 2);
            flow.Disconnected("c", now);
            flow.SceneLoaded(game.sceneName, now);
            var slots = flow.RoundSlots();
            Assert.IsTrue(slots.First(s => s.slot == 2).isBot, "a bot drives the leaver's critter");
            Assert.AreEqual(3, slots.Count);
        }

        [Test]
        public void RejoinerTakesTheSeatBackFromTheBot()
        {
            New(new SessionFlow.Options { minPlayers = 3, maxPlayers = 4, replaceLeavers = true });
            StartGame(3);
            PlayRound(0, 1, 2);
            flow.Disconnected("c", now);
            flow.SceneLoaded(game.sceneName, now);
            Assert.IsTrue(P("c").isBot, "a bot plays the round the human missed");
            flow.AddPlayer("c", "c", false, 42, now, out _);
            Assert.IsFalse(P("c").isBot, "back in human hands");
            Advance(SessionFlow.IntroSeconds + SessionFlow.CountdownSeconds);
            flow.RoundDone(Ranking(0, 1, 2), now);
            Advance(SessionFlow.ResultsSeconds + SessionFlow.StandingsSeconds);
            flow.SceneLoaded(game.sceneName, now);
            var c = flow.RoundSlots().First(x => x.slot == 2);
            Assert.IsFalse(c.isBot);
            Assert.AreEqual(42ul, c.ownerClientId, "the human drives it from the next round");
        }

        [Test]
        public void NoticesSayWhoJoinedLeftWasKickedDroppedCameBackOrWasReplaced()
        {
            New(new SessionFlow.Options { minPlayers = 3, maxPlayers = 4, replaceLeavers = true });
            var notes = new List<string>();
            flow.Notice += (slot, text) => notes.Add(text);
            Join("a", "Ann"); Join("b", "Bea"); Join("c", "Cem"); Join("d", "Dan");
            CollectionAssert.AreEqual(new[] { "Ann joined", "Bea joined", "Cem joined", "Dan joined" }, notes);
            notes.Clear();
            flow.Kick("a", P("d").slot);
            Assert.AreEqual("Dan was kicked", notes.Single());
            notes.Clear();
            Join("e", "Eve");
            flow.Remove("e", true, now);
            CollectionAssert.AreEqual(new[] { "Eve joined", "Eve left" }, notes);
            notes.Clear();
            Playlist(2); ReadyAll();
            Assert.IsNull(flow.TryStart("a", now));
            PlayRound(0, 1, 2);
            flow.Disconnected("c", now);
            flow.SceneLoaded(game.sceneName, now);
            flow.AddPlayer("c", "Cem", false, 9, now, out _);
            CollectionAssert.AreEqual(new[] { "Cem disconnected", "Cem replaced by a bot", "Cem is back" }, notes);
        }

        // A dropped WebSocket can outlive the rejoin: the server notices the old connection's end after the new one
        // already took the seat. That late disconnect must not knock the rejoined player out again.
        [Test]
        public void StaleDisconnectAfterRejoinIsIgnored()
        {
            StartGame(2);
            ulong first = P("c").clientId;
            flow.AddPlayer("c", "c", false, 77, now, out _);   // rejoined on a new connection
            flow.Disconnected("c", now, first);
            Assert.IsTrue(P("c").connected, "the old connection's end is ignored");
            flow.Disconnected("c", now, 77);
            Assert.IsFalse(P("c").connected, "the current connection's end counts");
        }

        [Test]
        public void RejoinBeforeTheDropIsSeenStillCountsAsAFall()
        {
            StartGame(2);
            flow.SceneLoaded(game.sceneName, now);
            Advance(SessionFlow.IntroSeconds + SessionFlow.CountdownSeconds);
            Assert.AreEqual(SessionPhase.Playing, flow.Phase);
            flow.AddPlayer("c", "c", false, 77, now, out _);   // the server has not noticed the old connection end yet
            CollectionAssert.AreEqual(new[] { 2 }, droppedMidRound, "the old critter is out for this round");
        }

        [Test]
        public void BotsArriveQuietly()
        {
            New(new SessionFlow.Options { minPlayers = 1, maxPlayers = 4, fillWithBots = true });
            var notes = new List<string>();
            flow.Notice += (slot, text) => notes.Add(text);
            Join("a", "Ann");
            Assert.AreEqual(4, flow.Players.Count);
            CollectionAssert.AreEqual(new[] { "Ann joined" }, notes, "no toast per practice bot");
        }

        [Test]
        public void WithoutReplacementALeaverSitsOut()
        {
            StartGame(2);
            PlayRound(0, 1, 2);
            flow.Disconnected("c", now);
            flow.SceneLoaded(game.sceneName, now);
            Assert.AreEqual(2, flow.RoundSlots().Count);
        }

        [Test]
        public void LeftHumanNotEligibleForChampion()
        {
            StartGame(2);
            PlayRound(2, 1, 0);   // c leads
            flow.Remove("c", clean: true, now);
            Assert.IsFalse(P("c").eligible);
            PlayRound(1, 0);
            Assert.IsFalse(flow.Champions().Contains(2));
        }

        [Test]
        public void DisconnectedTooLongCountsAsLeft()
        {
            StartGame(3);
            PlayRound(0, 1, 2);
            flow.Disconnected("c", now);
            Advance(SessionFlow.RejoinGraceSeconds + 1);
            Assert.IsFalse(P("c").eligible);
        }

        [Test]
        public void KickOnlyInLobby()
        {
            Join("a"); Join("b"); Join("c"); Join("d");
            Assert.IsNull(flow.Kick("b", 3), "only the leader kicks");
            Assert.AreEqual("d", flow.Kick("a", 3));
            Assert.AreEqual(3, flow.Players.Count);
            Playlist(1); ReadyAll(); flow.TryStart("a", now);
            Assert.IsNull(flow.Kick("a", 2), "no kicking once the session runs");
        }

        [Test]
        public void PracticeFillsWithBots()
        {
            New(new SessionFlow.Options { minPlayers = 1, maxPlayers = 4, fillWithBots = true });
            Join("me", "Ezel");
            CollectionAssert.AreEqual(new[] { "Ezel", "Mochi", "Zesty", "Grape" }, flow.Players.Select(p => p.name.ToString()).ToArray());
            Assert.AreEqual(3, flow.Players.Count(p => p.isBot));
            Assert.IsTrue(flow.SetPlaylist("me", "Solo", new List<string> { game.id }, out var e), e);
            Assert.IsNull(flow.TryStart("me", now), "bots are always ready");
        }

        [Test]
        public void CanJoinMirrorsAddPlayerRules()
        {
            Join("a"); Join("b"); Join("c");
            Assert.IsNull(flow.CanJoin("d"));
            Join("d");
            Assert.AreEqual("Room is full", flow.CanJoin("e"));
            Assert.IsNull(flow.CanJoin("b"), "a known player may always come back");
            flow.Remove("d", true, now);
            Playlist(1); ReadyAll(); flow.TryStart("a", now);
            Assert.AreEqual("Session in progress", flow.CanJoin("e"));
            Assert.IsNull(flow.CanJoin("c"));
        }

        [Test]
        public void PracticeAlwaysHasRoomForItsHuman()
        {
            New(new SessionFlow.Options { minPlayers = 1, maxPlayers = 4, fillWithBots = true });
            Join("me");
            Assert.IsNull(flow.CanJoin("friend"), "a bot makes room");
        }

        [Test]
        public void RttIsStoredPerPlayer()
        {
            Join("a");
            flow.SetRtt("a", 148);
            Assert.AreEqual(148, P("a").rttMs);
        }

        [Test]
        public void ColourCyclesToAFreeOne()
        {
            Join("a"); Join("b");
            Assert.AreEqual(0, P("a").colour); Assert.AreEqual(1, P("b").colour);
            flow.CycleColour("a");
            Assert.AreEqual(2, P("a").colour, "skips colours other players use");
        }
    }
}
