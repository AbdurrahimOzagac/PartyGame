using UnityEngine;

namespace KnockbackArena
{
    // Fire-and-forget sound calls; a no-op when the scene has no SfxPlayer (tests, other mini-games).
    public static class Sfx
    {
        public static void Play(string id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (SfxPlayer.Instance) SfxPlayer.Instance.Play(id, pos, volume, pitch);
        }

        public static void Ui(string id) => Play(id, Vector3.zero);
        public static void Music(string id) { if (SfxPlayer.Instance) SfxPlayer.Instance.PlayMusic(id); }
    }
}
