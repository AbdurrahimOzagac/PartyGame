using UnityEngine;

namespace ShooterParty
{
    // Mario-style contact shadow straight under the critter, whatever the sun does, so height reads in jumps and
    // knockback arcs. Shrinks and fades with height; hidden when there is no floor below ("no shadow" = off).
    public class BlobShadow : MonoBehaviour
    {
        public Renderer shadow;                  // flat quad, M_FX_BlobShadow
        public float fullHeight = 1.6f, maxHeight = 6f;
        public float groundSize = 0.9f, apexSize = 0.7f, groundAlpha = 0.45f, apexAlpha = 0.2f;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static readonly Quaternion FaceUp = Quaternion.Euler(90f, 0f, 0f);   // Unity's quad faces -Z
        readonly RaycastHit[] hits = new RaycastHit[8];
        MaterialPropertyBlock mpb;
        Color colour;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            colour = shadow && shadow.sharedMaterial ? shadow.sharedMaterial.GetColor(ColorId) : Color.black;
        }

        void LateUpdate()
        {
            if (!shadow) return;
            var origin = transform.position + Vector3.up * 0.3f;   // inside our own collider, so the ray ignores it
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxHeight + 0.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i < n; i++)
                if (!hits[i].rigidbody && hits[i].distance < best) { best = hits[i].distance; bi = i; }   // static floor only
            shadow.enabled = bi >= 0;
            if (bi < 0) return;
            float k = Mathf.Clamp01((best - 0.3f) / fullHeight);
            shadow.transform.SetPositionAndRotation(hits[bi].point + hits[bi].normal * 0.01f,
                                                    Quaternion.FromToRotation(Vector3.up, hits[bi].normal) * FaceUp);
            shadow.transform.localScale = Vector3.one * Mathf.Lerp(groundSize, apexSize, k);
            var c = colour; c.a = Mathf.Lerp(groundAlpha, apexAlpha, k);
            shadow.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, c);
            shadow.SetPropertyBlock(mpb);
        }
    }
}
