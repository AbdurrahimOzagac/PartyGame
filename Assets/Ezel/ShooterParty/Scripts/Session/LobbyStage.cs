using UnityEngine;

namespace ShooterParty
{
    // SP_Lobby's four pedestals. Each seat's critter stands on its pedestal in the seat's colour and cheers when
    // the player gets ready. The critters are plain local props (no networking): they mirror SessionManager.Players.
    public class LobbyStage : MonoBehaviour
    {
        public static LobbyStage Instance { get; private set; }

        public Transform[] pedestals = new Transform[4];
        public CritterAnimator[] critters = new CritterAnimator[4];   // one per pedestal, built into the scene
        public Vector3 tagOffset = new Vector3(0f, -0.15f, -0.6f);     // where a seat's name card hangs, from the pedestal

        readonly bool[] shownReady = new bool[4];
        float nextIdle;

        public CritterAnimator CritterAt(int slot) => slot >= 0 && slot < critters.Length ? critters[slot] : null;
        public bool ShownReady(int slot) => slot >= 0 && slot < shownReady.Length && shownReady[slot];
        public Vector3 TagPoint(int slot) => pedestals[slot].position + tagOffset;

        void Awake()
        {
            Instance = this;
            bool headless = PartyNet.Instance && PartyNet.Instance.Mode == PartyMode.DedicatedServer;
            foreach (var c in critters) if (c) LookOf(c).gameObject.SetActive(false);
            if (headless) enabled = false;   // nobody watches on the Pi
        }

        static CritterLook LookOf(CritterAnimator c) => c.GetComponentInParent<CritterLook>(true);   // the prefab root

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var s = SessionManager.Instance;
            bool lobby = s && s.IsSpawned && s.Phase.Value == SessionPhase.Lobby;
            for (int slot = 0; slot < critters.Length; slot++)
            {
                var c = critters[slot];
                if (!c) continue;
                var look = LookOf(c);
                var go = look.gameObject;
                if (!lobby || !s.TryGetPlayer(slot, out var p))
                {
                    if (go.activeSelf) go.SetActive(false);
                    shownReady[slot] = false;
                    continue;
                }
                if (!go.activeSelf) go.SetActive(true);
                look.Apply(p.colour);
                if (p.ready && !shownReady[slot]) c.PlayWin();
                shownReady[slot] = p.ready;
            }
            if (!lobby || Time.time < nextIdle) return;
            nextIdle = Time.time + Random.Range(2.5f, 4.5f);
            var pick = critters[Random.Range(0, critters.Length)];
            if (pick && pick.gameObject.activeInHierarchy) { if (Random.value < 0.5f) pick.PlayTaunt(); else pick.PlayJump(); }
        }
    }
}
