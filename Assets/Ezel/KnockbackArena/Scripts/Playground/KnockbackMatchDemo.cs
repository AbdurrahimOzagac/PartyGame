using System.Collections;
using System.Collections.Generic;
using KnockbackArena.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using ShooterParty;
using ShooterParty.UI;

namespace KnockbackArena.Playground
{
    // Local, one-machine round of Knockback Arena so every asset can be seen working together:
    // lobby -> 3-2-1-GO -> spit each other off the ice -> OUT banners -> the podium rises -> crown, confetti,
    // fanfare -> again. Placement points 4/3/2/1 and the +1 comeback point follow the pitch.
    // NOT the networked game flow: the real one (NGO) replaces this and keeps the HUD / lobby views.
    public class KnockbackMatchDemo : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public GameObject prefab;
            public string name;
            public Color colour;
            public Sprite[] faces;          // Neutral, Happy, Hurt, Sad
            public Transform spawn;
            public bool human;
        }

        public Slot[] slots;
        public OverShoulderCamera cam;
        public PartyHud hud;
        public LobbyPanel lobby;
        public MassRing ringPrefab;
        public Transform podium;
        public Vector3[] podiumStands;      // podium-local top centres for 1st..4th
        // +Z side reads 2nd-1st-3rd-4th; shifted so the podium sits in the left 60 % beside the results panel
        public Vector3 resultsCamPos = new Vector3(-1.7f, 1.8f, 5.8f), resultsCamLook = new Vector3(-1.7f, 1.05f, 0f);
        public ParticleSystem fallPoof, confetti;
        public LeaderCrown crown;
        public float outY = -2.5f, massLossPerHit = 0.1f;
        public int[] placementPoints = { 4, 3, 2, 1 };

        enum Phase { Lobby, Countdown, Playing, Results }

        Phase phase;
        GameObject[] live;
        CritterMotor[] motors;
        bool[] isOut, falling;
        int[] totals;
        readonly List<int> outOrder = new List<int>();
        int human = -1;

        void Start()
        {
            totals = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++) if (slots[i].human) human = i;
            if (podium) podium.gameObject.SetActive(false);
            if (crown) crown.gameObject.SetActive(false);
            lobby.onStart.AddListener(StartRound);
            lobby.onHost.AddListener(() => lobby.SetHosting("192.168.1.24:7777 (demo)"));
            Spawn(false);
            ShowLobby();
        }

        void ShowLobby()
        {
            phase = Phase.Lobby;
            lobby.gameObject.SetActive(true);
            hud.gameObject.SetActive(false);
            cam.allowCursorLock = false;
            for (int i = 0; i < slots.Length; i++)
                lobby.SetSlot(i, LobbyPanel.SlotState.Ready, slots[i].name, slots[i].colour, slots[i].faces != null && slots[i].faces.Length > 0 ? slots[i].faces[0] : null);
            lobby.SetCanStart(true, true);
            lobby.SetReady(true);
            Sfx.Music("MusicLobby");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (phase == Phase.Lobby && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) StartRound();
            if (phase == Phase.Results && kb != null && kb.rKey.wasPressedThisFrame) StartRound();
            if (phase == Phase.Results && kb != null && kb.escapeKey.wasPressedThisFrame) { Cleanup(); Spawn(false); ShowLobby(); }
            if (phase != Phase.Playing) return;
            int alive = 0;
            for (int i = 0; i < live.Length; i++)
            {
                if (isOut[i]) continue;
                var p = live[i].transform.position;
                if (!falling[i] && p.y < -0.4f && !motors[i].Grounded) { falling[i] = true; Sfx.Play("Wheee", p); }
                if (p.y < outY) Out(i); else alive++;
            }
            if (alive <= 1) StartCoroutine(EndRound());
        }

        // ------------------------------------------------------------------ round
        void Spawn(bool active)
        {
            Cleanup();
            int n = slots.Length;
            live = new GameObject[n]; motors = new CritterMotor[n]; isOut = new bool[n]; falling = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var s = slots[i];
                var face = Quaternion.LookRotation(-new Vector3(s.spawn.position.x, 0f, s.spawn.position.z));
                live[i] = Instantiate(s.prefab, s.spawn.position, face);
                live[i].name = s.name;
                var m = motors[i] = live[i].GetComponent<CritterMotor>();
                m.massLossPerHit = massLossPerHit;
                m.jumpCharges = 0;                         // jumping comes from the pickups
                m.respawnBelowY = float.NegativeInfinity;  // falling off is final here
                if (s.human)
                {
                    var input = live[i].AddComponent<LocalCritterInput>();
                    input.cam = cam; input.showHelp = false; input.enabled = active;
                }
                else live[i].AddComponent<ArenaBotAI>().enabled = active;
                if (ringPrefab && !s.human) Instantiate(ringPrefab).Bind(m, s.colour);
            }
            if (human >= 0) { cam.SetTarget(live[human].transform); var shake = cam.GetComponent<CameraShake>(); if (shake) shake.focus = live[human].transform; }
        }

        void Cleanup()
        {
            if (crown) crown.Detach();
            if (live == null) return;
            foreach (var g in live) if (g) Destroy(g);
            foreach (var b in FindObjectsByType<SpitBlob>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        }

        void StartRound()
        {
            StopAllCoroutines();
            hud.HideResults();
            if (podium) podium.gameObject.SetActive(false);
            cam.enabled = true;
            Spawn(false);
            outOrder.Clear();
            lobby.gameObject.SetActive(false);
            hud.gameObject.SetActive(true);
            var list = new List<PartyHud.Player>();
            for (int i = 0; i < slots.Length; i++)
                list.Add(new PartyHud.Player { motor = motors[i], name = slots[i].name, colour = slots[i].colour, faces = slots[i].faces });
            hud.Bind(list, human);
            int leader = Leader();
            hud.SetScores(totals, leader);
            if (crown && leader >= 0) crown.Attach(live[leader]);
            cam.allowCursorLock = true;
            phase = Phase.Countdown;
            Sfx.Music("");
            StartCoroutine(hud.Countdown(() =>
            {
                phase = Phase.Playing;
                foreach (var g in live)
                {
                    var input = g.GetComponent<LocalCritterInput>(); if (input) input.enabled = true;
                    var ai = g.GetComponent<ArenaBotAI>(); if (ai) ai.enabled = true;
                }
                Sfx.Music("MusicGameplay");
            }));
        }

        int Leader()
        {
            int best = -1, score = 0;
            for (int i = 0; i < totals.Length; i++) if (totals[i] > score) { score = totals[i]; best = i; }
            return best;
        }

        void Out(int i)
        {
            isOut[i] = true;
            int remaining = 0;
            foreach (var o in isOut) if (!o) remaining++;
            outOrder.Add(i);
            var p = live[i].transform.position;
            Fx.Play(fallPoof, new Vector3(p.x, outY, p.z), Quaternion.identity, slots[i].colour);
            StartCoroutine(Delayed(0.6f, () => Sfx.Play("Splash", new Vector3(p.x, outY, p.z))));
            if (i == human) CameraShake.Report(live[i].transform, 0.6f);
            if (crown && crown.transform.IsChildOf(live[i].transform)) crown.Detach();
            live[i].SetActive(false);
            hud.ShowOut(i, remaining + 1, remaining, i == human);
            if (i == human)   // spectate whoever is still in
                for (int k = 0; k < live.Length; k++) if (!isOut[k]) { cam.SetTarget(live[k].transform, false); break; }
        }

        IEnumerator Delayed(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        IEnumerator EndRound()
        {
            phase = Phase.Results;
            yield return new WaitForSeconds(1.2f);
            Sfx.Music("");
            var order = new List<int>();                                    // 1st first
            for (int i = 0; i < live.Length; i++) if (!isOut[i]) order.Add(i);
            for (int k = outOrder.Count - 1; k >= 0; k--) order.Add(outOrder[k]);
            var plus = new int[slots.Length];
            for (int r = 0; r < order.Count; r++) plus[order[r]] = placementPoints[Mathf.Min(r, placementPoints.Length - 1)];
            for (int i = 0; i < totals.Length; i++) totals[i] += plus[i];
            int last = 0;                                                    // comeback: last place overall gets +1
            for (int i = 1; i < totals.Length; i++) if (totals[i] < totals[last]) last = i;
            totals[last] += 1;
            var rows = new List<PartyHud.Result>();
            for (int r = 0; r < order.Count; r++)
                rows.Add(new PartyHud.Result { player = order[r], place = r + 1, plus = plus[order[r]], comeback = order[r] == last, total = totals[order[r]] });
            hud.SetScores(totals, Leader());

            // the podium rises out of the ice; everyone stands on it facing the results camera
            if (crown) crown.Detach();
            podium.gameObject.SetActive(true);
            for (int r = 0; r < order.Count; r++)
            {
                var g = live[order[r]];
                g.SetActive(true);
                foreach (var ring in g.GetComponentsInChildren<MassRing>(true)) ring.gameObject.SetActive(false);
                var input = g.GetComponent<LocalCritterInput>(); if (input) input.enabled = false;
                var ai = g.GetComponent<ArenaBotAI>(); if (ai) ai.enabled = false;
                var rb = g.GetComponent<Rigidbody>(); rb.isKinematic = true;
                g.GetComponent<CritterMotor>().enabled = false;
                g.transform.SetPositionAndRotation(podium.TransformPoint(podiumStands[Mathf.Min(r, podiumStands.Length - 1)]), podium.rotation);   // face the results camera (+Z)
                var anim = g.GetComponentInChildren<CritterAnimator>();
                anim.SetLocomotion(0f, true);
                if (r == 0) anim.PlayWin(); else if (r == order.Count - 1) anim.PlayLose(); else anim.PlayTaunt();
            }
            cam.enabled = false;
            cam.allowCursorLock = false;
            StartCoroutine(MoveCamera());
            StartCoroutine(RisePodium(order));
            hud.ShowResults(rows);
            yield return new WaitForSeconds(0.9f + 0.15f * order.Count);
            if (crown) crown.Attach(live[order[0]]);
            Fx.Play(confetti, podium.position + Vector3.up * 0.2f, Quaternion.identity, Color.white);
        }

        IEnumerator RisePodium(List<int> order)
        {
            var basePos = podium.position; basePos.y = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float y = Mathf.Lerp(-1.4f, 0f, Ease.OutBack(t / 0.6f));
                podium.position = basePos + Vector3.up * y;
                for (int r = 0; r < order.Count; r++) live[order[r]].transform.position = podium.TransformPoint(podiumStands[Mathf.Min(r, podiumStands.Length - 1)]);
                yield return null;
            }
            podium.position = basePos;
            for (int r = 0; r < order.Count; r++) live[order[r]].transform.position = podium.TransformPoint(podiumStands[Mathf.Min(r, podiumStands.Length - 1)]);
        }

        IEnumerator MoveCamera()
        {
            var t0 = cam.transform; var p0 = t0.position; var r0 = t0.rotation;
            var p1 = podium.TransformPoint(resultsCamPos); var r1 = Quaternion.LookRotation(podium.TransformPoint(resultsCamLook) - p1);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                float k = Ease.OutCubic(t);
                t0.SetPositionAndRotation(Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
                yield return null;
            }
            t0.SetPositionAndRotation(p1, r1);
        }
    }
}
