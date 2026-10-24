using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace ShooterParty
{
    // Networked spit. Only the server's physical blob decides anything; everyone else sees cosmetic blobs that
    // ignore critters. Flow (spec §3):
    //   owner fires -> local cosmetic blob + FireRpc -> server validates (cooldown on client timestamps, rate,
    //   origin) and launches the physical blob -> BlobSpawned to clients -> server contact:
    //     critter: ShotEffect.ApplyServer (e.g. knockback) -> shooter gets BlobHit at once, observers once the
    //              victim's owner has applied the knockback (so the splat and the flight arrive together)
    //     world:   BlobSplat to everyone
    // The host / server shows its physical blobs directly, so it never spawns cosmetic copies.
    public class ShotSystem : NetworkBehaviour
    {
        public static ShotSystem Instance { get; private set; }

        public ShotEffect Effect { get; set; }          // set by the running mini-game on every machine
        public int Epoch { get; set; } = 1;
        public int ServerShots { get; private set; }    // physical blobs launched (tests, stats)
        public event Action<ShotHit> ServerHit;         // after the effect ran (mini-games credit KOs here)

        class ServerShot
        {
            public int id;
            public NetworkCritter shooter, victim;
            public SpitBlob blob;
            public Vector3 point, normal;
            public bool knocked, notified;
            public float hitAt;
        }

        readonly Dictionary<int, ServerShot> pendingObservers = new Dictionary<int, ServerShot>();
        readonly List<SpitBlob> serverBlobs = new List<SpitBlob>();
        readonly Dictionary<NetworkCritter, (double lastClientTime, TokenBucket bucket)> fireState =
            new Dictionary<NetworkCritter, (double, TokenBucket)>();
        readonly Dictionary<int, SpitBlob> cosmetic = new Dictionary<int, SpitBlob>();
        readonly Dictionary<int, Queue<(SpitBlob blob, float time)>> pendingLocal = new Dictionary<int, Queue<(SpitBlob, float)>>();
        int nextId;

        public static NetworkCritter CritterBySlot(int slot)
        {
            foreach (var c in NetworkCritter.All) if (c && c.Slot.Value == slot) return c;
            return null;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) NetworkCritter.KnockbackApplied += OnKnockbackApplied;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            NetworkCritter.KnockbackApplied -= OnKnockbackApplied;
            foreach (var b in serverBlobs) if (b) Destroy(b.gameObject);   // their contacts would outlive the session
            serverBlobs.Clear();
        }

        // ------------------------------------------------------------------ firing
        // Owner-side entry, called by CritterSpit at the release frame.
        public void Fire(NetworkCritter shooter, Vector3 origin, Vector3 dir)
        {
            if (Effect) Effect.OnFireLocal(shooter);
            if (IsServer) { ServerFire(shooter, origin, dir, NetworkManager.ServerTime.Time); return; }
            var blob = Launch(shooter, origin, Velocity(shooter, dir), cosmeticBlob: true);
            int slot = shooter.Slot.Value;
            if (!pendingLocal.TryGetValue(slot, out var q)) pendingLocal[slot] = q = new Queue<(SpitBlob, float)>();
            q.Enqueue((blob, Time.time));
            shooter.FireRpc(origin, dir, NetworkManager.LocalTime.Time);
        }

        // Server: FireRpc lands here. Timestamps are the client's, so a TCP stall does not trip the cooldown.
        internal void ValidateAndFire(NetworkCritter shooter, Vector3 origin, Vector3 dir, double clientTime)
        {
            if (!IsServer || !shooter || shooter.Eliminated.Value) return;
            if (!fireState.TryGetValue(shooter, out var st)) st = (double.NegativeInfinity, new TokenBucket(6f, 12f));
            double now = NetworkManager.ServerTime.Time;
            float cooldown = shooter.GetComponent<CritterSpit>().cooldown;
            bool ok = st.bucket.Take(Time.unscaledTimeAsDouble) && clientTime - st.lastClientTime >= cooldown * 0.9f && clientTime <= now + 1.0;
            if (ok) st.lastClientTime = clientTime;
            fireState[shooter] = st;
            if (!ok || !Finite(dir) || dir.sqrMagnitude < 1e-4f) return;
            // origin must be near where the server sees the shooter; otherwise use the server's own muzzle point
            var serverMuzzle = shooter.transform.position + Vector3.up * 0.45f;
            float lag = NetworkManager.NetworkConfig.NetworkTransport.GetCurrentRtt(shooter.OwnerClientId) / 1000f + 0.15f;
            float tolerance = shooter.Motor.HorizontalVelocity.magnitude * lag + 1.2f;
            if (!Finite(origin) || (origin - serverMuzzle).magnitude > tolerance) origin = serverMuzzle + shooter.transform.forward * 0.6f;
            ServerFire(shooter, origin, dir, now);
        }

        // Server: launch the deciding blob. Server-simulated shooters (bots, the host's critter) call this directly.
        public void ServerFire(NetworkCritter shooter, Vector3 origin, Vector3 dir, double time)
        {
            if (!IsServer || !shooter) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            var v0 = Velocity(shooter, dir.normalized);
            var shot = new ServerShot { id = ++nextId, shooter = shooter };
            shot.blob = Launch(shooter, origin, v0, cosmeticBlob: false);
            shot.blob.GetComponent<Rigidbody>().mass = 0.02f;   // no physical shove: the ShotEffect decides the push
            shot.blob.Impacted += (b, c) => OnServerImpact(shot, c);
            serverBlobs.RemoveAll(b => !b);
            serverBlobs.Add(shot.blob);
            ServerShots++;
            if (!shooter.IsOwner && NetworkManager.IsClient) PlaySpitPose(shooter);   // a host watching a remote shooter
            BlobSpawnedRpc(shot.id, shooter.Slot.Value, origin, v0, time);
        }

        static Vector3 Velocity(NetworkCritter c, Vector3 dir)
        {
            dir.y = 0f;
            return dir.normalized * c.GetComponent<CritterSpit>().speed + Vector3.up * 0.8f;
        }

        static SpitBlob Launch(NetworkCritter shooter, Vector3 origin, Vector3 v0, bool cosmeticBlob)
        {
            var spit = shooter.GetComponent<CritterSpit>();
            var blob = Instantiate(spit.blobPrefab, origin, Quaternion.LookRotation(v0));
            blob.gravity = spit.gravity;
            blob.Cosmetic = cosmeticBlob;
            blob.Launch(shooter.gameObject, v0, 0f, spit.blobMaterial, shooter.GetComponentsInChildren<Collider>());
            return blob;
        }

        static void PlaySpitPose(NetworkCritter shooter)
        {
            var anim = shooter.GetComponentInChildren<CritterAnimator>();
            if (anim) anim.PlaySpit();
        }

        // ------------------------------------------------------------------ server contacts
        void OnServerImpact(ServerShot shot, Collision c)
        {
            if (!IsSpawned) return;
            var contact = c.GetContact(0);
            var victim = c.collider.GetComponentInParent<NetworkCritter>();
            if (victim && victim != shot.shooter && victim.IsSpawned)
            {
                shot.victim = victim; shot.point = contact.point; shot.normal = contact.normal; shot.hitAt = Time.time;
                shot.blob.ForceImpact(contact.point, contact.normal, victim.transform);
                var hit = new ShotHit
                {
                    shooterSlot = shot.shooter.Slot.Value, victimSlot = victim.Slot.Value,
                    point = contact.point, normal = contact.normal, velocity = shot.blob.LastVelocity,
                };
                pendingObservers[shot.id] = shot;
                if (!victim.Eliminated.Value && Effect) Effect.ApplyServer(hit, new ShotContext(this, shot.id));
                ServerHit?.Invoke(hit);
                if (!shot.shooter.IsOwner)
                    BlobHitRpc(shot.id, hit.victimSlot, contact.point, contact.normal, RpcTarget.Single(shot.shooter.OwnerClientId, RpcTargetUse.Temp));
                // a remote victim confirms first (OnKnockbackApplied); everything else can be shown now
                if (!shot.notified && !(shot.knocked && !victim.IsOwner)) NotifyObservers(shot);
            }
            else
            {
                shot.blob.ForceImpact(contact.point, contact.normal, null);
                BlobSplatRpc(shot.id, contact.point, contact.normal);
            }
        }

        internal void MarkKnocked(int blobId)
        {
            if (pendingObservers.TryGetValue(blobId, out var s)) s.knocked = true;
        }

        void OnKnockbackApplied(NetworkCritter victim, int blobId)
        {
            if (pendingObservers.TryGetValue(blobId, out var shot) && !shot.notified) NotifyObservers(shot);
        }

        void NotifyObservers(ServerShot shot)
        {
            shot.notified = true;
            pendingObservers.Remove(shot.id);
            var skip = new HashSet<ulong> { NetworkManager.ServerClientId };
            if (!shot.shooter.IsOwner) skip.Add(shot.shooter.OwnerClientId);
            if (shot.victim && !shot.victim.IsOwner) skip.Add(shot.victim.OwnerClientId);
            var ids = NetworkManager.ConnectedClientsIds.Where(id => !skip.Contains(id)).ToList();
            if (ids.Count > 0)
                BlobHitRpc(shot.id, shot.victim ? shot.victim.Slot.Value : -1, shot.point, shot.normal, RpcTarget.Group(ids, RpcTargetUse.Temp));
        }

        void Update()
        {
            if (!IsServer || pendingObservers.Count == 0) return;
            // a victim that never confirms (disconnected) must not hide the hit from everyone else
            foreach (var shot in pendingObservers.Values.Where(s => Time.time - s.hitAt > 1f).ToList()) NotifyObservers(shot);
        }

        // ------------------------------------------------------------------ client presentation
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void BlobSpawnedRpc(int id, int shooterSlot, Vector3 p0, Vector3 v0, double serverTime)
        {
            var shooter = CritterBySlot(shooterSlot);
            if (!shooter) return;
            if (shooter.IsOwner && pendingLocal.TryGetValue(shooterSlot, out var q))
                while (q.Count > 0)
                {
                    var (b, t) = q.Dequeue();
                    if (b && Time.time - t < 1f) { cosmetic[id] = b; return; }   // older ones were refused by the server
                }
            PlaySpitPose(shooter);
            var spit = shooter.GetComponent<CritterSpit>();
            float dt = Mathf.Clamp((float)(NetworkManager.ServerTime.Time - serverTime), 0f, 0.3f);
            var g = Vector3.down * spit.gravity;
            var blob = Launch(shooter, p0 + v0 * dt + 0.5f * dt * dt * g, v0 + g * dt, cosmeticBlob: true);
            Fx.Play(spit.popFx, p0, Quaternion.LookRotation(v0), Fx.ColourOf(spit.blobMaterial));
            Sfx.Play("Spit", p0);
            cosmetic[id] = blob;
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void BlobHitRpc(int id, int victimSlot, Vector3 point, Vector3 normal, RpcParams rpcParams = default)
        {
            var victim = CritterBySlot(victimSlot);
            if (cosmetic.TryGetValue(id, out var blob) && blob) blob.ForceImpact(point, normal, victim ? victim.transform : null);
            cosmetic.Remove(id);
            if (victim && !victim.IsOwner)
            {
                var anim = victim.GetComponentInChildren<CritterAnimator>();
                if (anim) anim.PlayHit(0.7f);
            }
        }

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void BlobSplatRpc(int id, Vector3 point, Vector3 normal)
        {
            if (cosmetic.TryGetValue(id, out var blob) && blob) blob.ForceImpact(point, normal, null);
            cosmetic.Remove(id);
        }

        // Victim's own client: KnockbackRpc arrived, so its copy of the blob lands on it now.
        internal void LocalVictimImpact(int blobId, NetworkCritter victim, Vector3 dv)
        {
            if (!cosmetic.TryGetValue(blobId, out var blob) || !blob) return;
            var dir = dv; dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : -victim.transform.forward;
            blob.ForceImpact(victim.transform.position + Vector3.up * 0.55f - dir * 0.45f, -dir, victim.transform);
            cosmetic.Remove(blobId);
        }

        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                                           float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
