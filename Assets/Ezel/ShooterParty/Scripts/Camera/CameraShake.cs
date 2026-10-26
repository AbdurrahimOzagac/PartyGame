using UnityEngine;

namespace ShooterParty
{
    // Trauma-based camera shake (Eiserloh, GDC 2016): offset = trauma^2 * smooth noise. Local feel only: it
    // never touches networked state, and it runs after the follow camera so aiming (which uses the camera
    // rig's yaw, not this transform) is unaffected.
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Main { get; private set; }

        public Transform focus;                // the local player's critter; only its events shake the camera
        [Range(0f, 1f)] public float strength = 1f;   // settings slider
        public float maxOffset = 0.12f, maxRoll = 2.5f, maxYawPitch = 1.5f, frequency = 20f, decay = 1.5f;

        float trauma, seed;

        void Awake() { Main = this; seed = Random.value * 100f; }
        void OnDestroy() { if (Main == this) Main = null; }

        // trauma presets from the VFX brief: hit received 0.45, own fall-out 0.6, hit dealt 0.08, heavy landing 0.12
        public static void Report(Component who, float amount)
        {
            if (!Main || !who || !Main.focus || who.transform.root != Main.focus.root) return;
            Main.trauma = Mathf.Clamp01(Main.trauma + amount);
        }

        void LateUpdate()
        {
            if (trauma <= 0f) return;
            float s = trauma * trauma * strength, t = Time.time * frequency;
            float n(float o) => Mathf.PerlinNoise(seed + o, t) * 2f - 1f;
            transform.position += transform.rotation * new Vector3(n(0f), n(10f), 0f) * (maxOffset * s);
            transform.rotation *= Quaternion.Euler(n(20f) * maxYawPitch * s, n(30f) * maxYawPitch * s, n(40f) * maxRoll * s);
            trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
        }
    }
}
