using UnityEngine;

namespace KnockbackArena
{
    // Goo splats left on the ice by missed shots: one flat particle system for the whole arena (no URP Decal
    // feature needed). Each splat holds 3 s, then fades and shrinks over 1 s (curves live on the prefab).
    [RequireComponent(typeof(ParticleSystem))]
    public class SplatDecals : MonoBehaviour
    {
        public static SplatDecals Instance { get; private set; }
        ParticleSystem ps;

        void Awake() { Instance = this; ps = GetComponent<ParticleSystem>(); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public static void Add(Vector3 point, Vector3 normal, Color colour, float size = 0.75f)
        {
            if (!Instance || Vector3.Dot(normal, Vector3.up) < 0.7f) return;   // floors only
            Instance.ps.Emit(new ParticleSystem.EmitParams
            {
                position = point + normal * 0.012f,
                startColor = colour,
                startSize = size * Random.Range(0.85f, 1.15f),
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false,
            }, 1);
        }
    }
}
