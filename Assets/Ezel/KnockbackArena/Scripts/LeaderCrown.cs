using UnityEngine;

namespace KnockbackArena
{
    // The session leader's crown (pitch comeback rule: "everyone can see who to catch"). Rides the "Top" bone so
    // it jiggles with the head; sits around the goo curl with a jaunty tilt. Offsets are relative to the bone and
    // are computed by the editor setup from the rest pose.
    public class LeaderCrown : MonoBehaviour
    {
        public Vector3 boneLocalPosition;
        public Quaternion boneLocalRotation = Quaternion.identity;
        public Vector3 boneLocalScale = Vector3.one;

        float popT = -1f;

        public void Attach(GameObject critter)
        {
            var top = Find(critter.transform, "Top");
            if (!top) { Debug.LogWarning("LeaderCrown: no Top bone on " + critter.name); return; }
            gameObject.SetActive(true);
            transform.SetParent(top, false);
            transform.localPosition = boneLocalPosition;
            transform.localRotation = boneLocalRotation;
            popT = 0f;
            Update();
        }

        public void Detach() { transform.SetParent(null, false); gameObject.SetActive(false); }

        void Update()
        {
            if (popT < 0f) return;
            // drops on: 0 -> 1.2 -> 1 over 0.3 s
            popT += Time.deltaTime;
            float k = Mathf.Clamp01(popT / 0.3f);
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.2f, Mathf.Sin(k / 0.6f * Mathf.PI * 0.5f)) : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f);
            transform.localScale = boneLocalScale * s;
            if (k >= 1f) popT = -1f;
        }

        public static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r) return r; }
            return null;
        }
    }
}
