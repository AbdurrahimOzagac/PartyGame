using UnityEngine;

namespace ShooterParty
{
    // Sound bank: id -> clip variants with volume and pitch jitter. Built by Knockback Arena/Rebuild.
    [CreateAssetMenu(menuName = "Knockback Arena/Sfx Bank")]
    public class SfxBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            public float pitchJitter = 0.04f;    // +- fraction, so repeats never sound machine-gunned
            [Range(0f, 1f)] public float spatial = 0.6f;   // 0 = UI / music, partly 3D for gameplay
        }

        public Entry[] entries;
    }
}
