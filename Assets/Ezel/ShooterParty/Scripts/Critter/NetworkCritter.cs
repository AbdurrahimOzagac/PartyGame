using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace ShooterParty
{
    public enum CritterAction : byte { Spit, Dash, Jump, Land }

    // The networked half of a critter. The owner simulates it (a human's own client, or the server for bots and the
    // host's own critter); every other copy is a kinematic proxy that animates from observed motion. The server
    // decides mass, knockback, elimination and scores; knockback reaches the owner as a velocity change via RPC.
    // Offline (never spawned) this component does nothing, so the playground and single-player tests still work.
    [RequireComponent(typeof(CritterMotor))]
    public class NetworkCritter : NetworkBehaviour
    {
        static readonly List<NetworkCritter> all = new List<NetworkCritter>();
        public static IReadOnlyList<NetworkCritter> All => all;
        public static event Action<NetworkCritter, int> KnockbackApplied;   // server: owner applied the hit from blob id

        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<byte> ColourIndex = new NetworkVariable<byte>();
        public readonly NetworkVariable<bool> IsBot = new NetworkVariable<bool>();
        public readonly NetworkVariable<ulong> HumanClientId = new NetworkVariable<ulong>();   // whose keyboard drives it (humans)
        public readonly NetworkVariable<float> Mass = new NetworkVariable<float>(1f);
        public readonly NetworkVariable<bool> Eliminated = new NetworkVariable<bool>();
        public readonly NetworkVariable<int> Epoch = new NetworkVariable<int>();
        public readonly NetworkVariable<CritterConfig> Config = new NetworkVariable<CritterConfig>(CritterConfig.Default);
        public readonly NetworkVariable<PlayerModifiers> Modifiers = new NetworkVariable<PlayerModifiers>(PlayerModifiers.One);

        public CritterMotor Motor { get; private set; }
        public bool Simulated => IsSpawned && IsOwner;
        public float StartMass => baseMass * Modifiers.Value.mass;   // same on every copy, also for late joiners
        public float MassFraction => StartMass > 0f ? Mass.Value / StartMass : 1f;

        CritterAnimator anim;
        CritterFx fx;
        CritterSpit spit;
        CritterLook look;
        NetworkTransform netTransform;
        SpeedCheck speedCheck;
        readonly TokenBucket actions = new TokenBucket(20f, 40f);
        float baseMoveSpeed, baseMass, lastKnockbackSent = -99f;
        Vector3 lastPos, remoteVelocity;

        void Awake()
        {
            Motor = GetComponent<CritterMotor>();
            anim = GetComponentInChildren<CritterAnimator>();
            fx = GetComponent<CritterFx>();
            spit = GetComponent<CritterSpit>();
            look = GetComponent<CritterLook>();
            netTransform = GetComponent<NetworkTransform>();
            baseMoveSpeed = Motor.moveSpeed;
            baseMass = GetComponent<Rigidbody>().mass;
        }

        // Server, before Spawn(): the values travel with the spawn message.
        public void ServerSetup(int slot, int colourIndex, bool isBot, CritterConfig cfg, PlayerModifiers mods, int epoch,
                                ulong humanClientId = NetworkManager.ServerClientId)
        {
            HumanClientId.Value = humanClientId;
            Slot.Value = slot;
            ColourIndex.Value = (byte)colourIndex;
            IsBot.Value = isBot;
            Config.Value = cfg;
            Modifiers.Value = mods;
            Epoch.Value = epoch;
            Mass.Value = baseMass * mods.mass;
        }

        public override void OnNetworkSpawn()
        {
            foreach (var other in all) IgnoreCollisions(other);
            all.Add(this);
            if (look) look.Apply(ColourIndex.Value);
            ColourIndex.OnValueChanged += OnColour;
            Mass.OnValueChanged += OnMass;
            Config.OnValueChanged += OnConfig;
            Modifiers.OnValueChanged += OnModifiers;
            Eliminated.OnValueChanged += OnEliminated;
            Motor.SetMass(Mass.Value);
            ApplyConfig();
            Motor.respawnBelowY = float.NegativeInfinity;   // falling off is final online; the mini-game decides
            Motor.Dashed += OnDashed;
            Motor.Jumped += OnJumped;
            Motor.Landed += OnLanded;
            ApplyAuthority();
            if (Eliminated.Value) OnEliminated(false, true);
            lastPos = transform.position;
        }

        // Owner simulates; everyone else animates a kinematic proxy. Re-run when ownership moves
        // (a leaver's critter handed to the server, a rejoiner taking it back).
        void ApplyAuthority()
        {
            Motor.Remote = !Simulated;
            Motor.enabled = Simulated && !Eliminated.Value;
            var input = GetComponent<LocalCritterInput>();
            bool wantsInput = Simulated && !IsBot.Value && !Eliminated.Value && HumanClientId.Value == NetworkManager.LocalClientId;
            if (wantsInput && !input) AttachLocalInput();
            else if (!wantsInput && input) Destroy(input);
            speedCheck = IsServer && !IsOwner ? speedCheck ?? new SpeedCheck(Motor.dashSpeed * 1.3f + 2f) : null;
        }

        public override void OnGainedOwnership() => ApplyAuthority();
        public override void OnLostOwnership() => ApplyAuthority();

        // Out of the round: vanish (the mini-game plays its own fall FX) and, for its player, spectate someone else.
        void OnEliminated(bool _, bool now)
        {
            if (!now) return;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Motor.Move(Vector3.zero);
            Motor.enabled = false;
            var input = GetComponent<LocalCritterInput>();
            if (input) Destroy(input);
            var cam = FindAnyObjectByType<OverShoulderCamera>();
            if (cam && cam.target == transform)
            {
                NetworkCritter next = null;
                foreach (var c in all) if (c != this && c && !c.Eliminated.Value) { next = c; break; }
                if (next) cam.SetTarget(next.transform, false);
            }
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            ColourIndex.OnValueChanged -= OnColour;
            Mass.OnValueChanged -= OnMass;
            Config.OnValueChanged -= OnConfig;
            Modifiers.OnValueChanged -= OnModifiers;
            Eliminated.OnValueChanged -= OnEliminated;
            Motor.Dashed -= OnDashed;
            Motor.Jumped -= OnJumped;
            Motor.Landed -= OnLanded;
            var input = GetComponent<LocalCritterInput>();
            if (input) Destroy(input);
        }

        void OnColour(byte _, byte v) { if (look) look.Apply(v); }
        void OnMass(float _, float v) => Motor.SetMass(v);
        void OnConfig(CritterConfig _, CritterConfig __) => ApplyConfig();
        void OnModifiers(PlayerModifiers _, PlayerModifiers __) => ApplyConfig();

        void ApplyConfig()
        {
            var cfg = Config.Value;
            var mods = Modifiers.Value;
            Motor.jumpCharges = cfg.jump == CritterConfig.JumpMode.On ? -1 : 0;
            Motor.dashCharges = Motor.maxDashCharges = cfg.dashCharges;
            Motor.moveSpeed = baseMoveSpeed * mods.moveSpeed;
            if (spit) { spit.speed = cfg.shotSpeed; spit.gravity = cfg.shotGravity; spit.cooldown = cfg.shotCooldown * mods.cooldown; }
        }

        void AttachLocalInput()
        {
            var cam = FindAnyObjectByType<OverShoulderCamera>();
            var input = gameObject.AddComponent<LocalCritterInput>();
            input.cam = cam;
            if (cam) cam.SetTarget(transform);
            var shake = cam ? cam.GetComponent<CameraShake>() : null;
            if (shake) shake.focus = transform;
        }

        void IgnoreCollisions(NetworkCritter other)
        {
            foreach (var a in GetComponentsInChildren<Collider>())
                foreach (var b in other.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(a, b, true);
        }

        void Update()
        {
            if (!IsSpawned || Simulated) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var pos = transform.position;
            remoteVelocity = Vector3.Lerp(remoteVelocity, (pos - lastPos) / dt, 1f - Mathf.Exp(-12f * dt));
            lastPos = pos;
            bool grounded = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            Motor.SetRemoteState(remoteVelocity, grounded);
            var h = remoteVelocity; h.y = 0f;
            if (anim) anim.SetLocomotion(h.magnitude, grounded);
        }

        // ------------------------------------------------------------------ hits (server decides)
        // Server: apply a hit computed with the server's mass. Owned-by-server critters (bots, the host's own) get it
        // directly; a remote human gets it through its owner, who then confirms with KnockbackAppliedRpc.
        public void ServerKnock(int blobId, Vector3 dv, float newMass)
        {
            Mass.Value = newMass;
            lastKnockbackSent = Time.time;
            if (IsOwner) { Motor.ApplyVelocityChange(dv); KnockbackApplied?.Invoke(this, blobId); }
            else KnockbackRpc(blobId, dv, newMass, Epoch.Value);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void KnockbackRpc(int blobId, Vector3 dv, float newMass, int epoch)
        {
            if (epoch != Epoch.Value) return;   // a hit from an earlier round arriving late
            Motor.SetMass(newMass);
            Motor.ApplyVelocityChange(dv);
            if (ShotSystem.Instance) ShotSystem.Instance.LocalVictimImpact(blobId, this, dv);
            KnockbackAppliedRpc(blobId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void KnockbackAppliedRpc(int blobId) => KnockbackApplied?.Invoke(this, blobId);

        // Owner -> server: "I spat". The critter is the RPC's object, so nobody can fire as someone else.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void FireRpc(Vector3 origin, Vector3 dir, double clientTime)
        {
            if (ShotSystem.Instance) ShotSystem.Instance.ValidateAndFire(this, origin, dir, clientTime);
        }

        // Server: a pickup granted jumps. Owner-side state, like dash charges.
        public void ServerAddJumpCharge(int n)
        {
            if (IsOwner) Motor.AddJumpCharge(n);
            else AddJumpChargeRpc(n);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void AddJumpChargeRpc(int n) => Motor.AddJumpCharge(n);

        public void ServerTeleport(Vector3 pos, Quaternion rot)
        {
            if (IsOwner) Teleport(pos, rot);
            else TeleportRpc(pos, rot);
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        void TeleportRpc(Vector3 pos, Quaternion rot) => Teleport(pos, rot);

        void Teleport(Vector3 pos, Quaternion rot)
        {
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            if (netTransform && netTransform.CanCommitToTransform) netTransform.Teleport(pos, rot, transform.localScale);
            else { rb.position = pos; rb.rotation = rot; }
        }

        // ------------------------------------------------------------------ one-shot actions for other copies
        void OnDashed() { if (IsSpawned) ActionRpc(CritterAction.Dash, 0f); }
        void OnJumped() { if (IsSpawned) ActionRpc(CritterAction.Jump, 0f); }
        void OnLanded(float speed) { if (IsSpawned && speed >= 2f) ActionRpc(CritterAction.Land, speed); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ActionRpc(CritterAction a, float value)
        {
            if (!actions.Take(Time.unscaledTimeAsDouble)) return;
            ActionEchoRpc(a, value);
        }

        [Rpc(SendTo.NotOwner, InvokePermission = RpcInvokePermission.Server)]
        void ActionEchoRpc(CritterAction a, float value)
        {
            if (!NetworkManager.IsClient) return;   // a dedicated server draws nothing
            switch (a)
            {
                case CritterAction.Dash: if (fx) fx.PlayDash(transform.forward); break;
                case CritterAction.Jump: if (anim) anim.PlayJump(); if (fx) fx.PlayJump(); break;
                case CritterAction.Land: if (fx) fx.PlayLand(value); break;
                case CritterAction.Spit: if (anim) anim.PlaySpit(); break;
            }
        }

        // ------------------------------------------------------------------ server speed check
        internal void OnOwnerState(int tick, Vector3 pos)
        {
            if (speedCheck == null || IsBot.Value) return;
            double t = tick / (double)NetworkManager.NetworkConfig.TickRate;
            var verdict = speedCheck.Feed(t, pos, Time.time - lastKnockbackSent < 1.5f);
            if (verdict == SpeedVerdict.Log)
                Debug.LogWarning($"[NetworkCritter] speed violation slot={Slot.Value} pos={pos} cap={Motor.dashSpeed * 1.3f + 2f:F1}");
            else if (verdict == SpeedVerdict.Kick)
            {
                Debug.LogWarning($"[NetworkCritter] kicking slot={Slot.Value}: impossible movement for {SpeedCheck.KickAfterSeconds} s");
                NetworkManager.DisconnectClient(OwnerClientId, "Kicked: impossible movement");
            }
        }
    }
}
