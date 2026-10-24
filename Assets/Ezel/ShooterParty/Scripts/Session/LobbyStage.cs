using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty
{
    // SP_Lobby's stage. In the lobby each seat's critter stands on its pedestal in the seat's colour and cheers when
    // the player gets ready. In the Final the pedestals give way to the podium: critters stand by session total,
    // the champion wears the crown under confetti. The critters are local props mirroring SessionManager.Players.
    public class LobbyStage : MonoBehaviour
    {
        public static LobbyStage Instance { get; private set; }

        public Transform[] pedestals = new Transform[4];
        public CritterAnimator[] critters = new CritterAnimator[4];   // one per seat, built into the scene
        public Vector3 tagOffset = new Vector3(0f, -0.15f, -0.6f);     // where a seat's name card hangs, from the pedestal
        [Header("Final")]
        public GameObject podium;
        public Vector3[] stands = new Vector3[4];                      // podium tops, 1st (tallest) first
        public LeaderCrown crownPrefab;
        public GameObject confettiPrefab;
        public Vector3 faceTowards;                                    // the camera's position, so the podium faces it

        readonly bool[] shownReady = new bool[4];
        Vector3[] homePos;
        Quaternion[] homeRot;
        readonly List<LeaderCrown> crowns = new List<LeaderCrown>();   // one per champion (ties share)
        float nextIdle;
        bool final;

        public int CrownedSlot { get; private set; } = -1;
        public CritterAnimator CritterAt(int slot) => slot >= 0 && slot < critters.Length ? critters[slot] : null;
        public bool ShownReady(int slot) => slot >= 0 && slot < shownReady.Length && shownReady[slot];
        public Vector3 TagPoint(int slot) => pedestals[slot].position + tagOffset;

        static CritterLook LookOf(CritterAnimator c) => c.GetComponentInParent<CritterLook>(true);   // the prefab root

        void Awake()
        {
            Instance = this;
            homePos = new Vector3[critters.Length];
            homeRot = new Quaternion[critters.Length];
            for (int i = 0; i < critters.Length; i++)
            {
                if (!critters[i]) continue;
                var t = LookOf(critters[i]).transform;
                homePos[i] = t.position; homeRot[i] = t.rotation;
                t.gameObject.SetActive(false);
            }
            if (podium) podium.SetActive(false);
            if (PartyNet.Instance && PartyNet.Instance.Mode == PartyMode.DedicatedServer) enabled = false;   // nobody watches on the Pi
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var s = SessionManager.Instance;
            var phase = s && s.IsSpawned ? s.Phase.Value : (SessionPhase?)null;
            bool lobby = phase == SessionPhase.Lobby, wantFinal = phase == SessionPhase.Final;
            if (wantFinal != final) SetFinal(wantFinal, s);
            for (int slot = 0; slot < critters.Length; slot++)
            {
                var c = critters[slot];
                if (!c) continue;
                var look = LookOf(c);
                PlayerState p = default;
                if (!(lobby || final) || !s.TryGetPlayer(slot, out p))
                {
                    if (look.gameObject.activeSelf) look.gameObject.SetActive(false);
                    shownReady[slot] = false;
                    continue;
                }
                if (!look.gameObject.activeSelf) look.gameObject.SetActive(true);
                look.Apply(p.colour);
                if (lobby && p.ready && !shownReady[slot]) c.PlayWin();
                shownReady[slot] = lobby && p.ready;
            }
            if ((!lobby && !final) || Time.time < nextIdle) return;
            nextIdle = Time.time + Random.Range(2.5f, 4.5f);
            if (final && CritterAt(CrownedSlot)) { CritterAt(CrownedSlot).PlayWin(); return; }   // the champion keeps celebrating
            var pick = critters[Random.Range(0, critters.Length)];
            if (pick && pick.gameObject.activeInHierarchy) { if (Random.value < 0.5f) pick.PlayTaunt(); else pick.PlayJump(); }
        }

        // Pedestals <-> podium. On the podium the critters stand by session total and the champion gets the crown.
        void SetFinal(bool on, SessionManager s)
        {
            final = on;
            foreach (var p in pedestals) if (p) p.gameObject.SetActive(!on);
            if (podium) podium.SetActive(on);
            CrownedSlot = -1;
            foreach (var c in crowns) if (c) c.Detach();
            for (int i = 0; i < critters.Length; i++)
                if (critters[i]) LookOf(critters[i]).transform.SetPositionAndRotation(homePos[i], homeRot[i]);
            if (!on || !s) return;
            var order = s.SeatsByTotal();
            var champs = s.ChampionSlots();
            for (int place = 0; place < order.Count && place < stands.Length; place++)
            {
                var c = CritterAt(order[place].slot);
                if (!c) continue;
                var t = LookOf(c).transform;
                t.gameObject.SetActive(true);   // a freshly loaded stage starts hidden: its animators wake on activation
                var face = faceTowards - stands[place]; face.y = 0f;
                t.SetPositionAndRotation(stands[place], face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : t.rotation);
                if (place == order.Count - 1 && place > 0) c.PlayLose();
            }
            if (champs.Count == 0 || !CritterAt(champs[0])) return;
            CrownedSlot = champs[0];
            for (int i = 0; i < champs.Count; i++)
            {
                var c = CritterAt(champs[i]);
                if (!c) continue;
                var champion = LookOf(c);
                champion.gameObject.SetActive(true);
                if (crownPrefab)
                {
                    while (crowns.Count <= i) crowns.Add(null);
                    if (!crowns[i]) crowns[i] = Instantiate(crownPrefab);
                    crowns[i].Attach(champion.gameObject);
                }
                c.PlayWin();
                if (confettiPrefab) Instantiate(confettiPrefab, champion.transform.position + Vector3.up * 2.2f, Quaternion.identity);
            }
        }
    }
}
