using System.Collections.Generic;
using UnityEngine;

namespace KnockbackArena
{
    // Scene player: a small round-robin pool of AudioSources plus one music source. Sfx.Play is a no-op without
    // one in the scene (tests, other mini-games).
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }
        public SfxBank bank;
        [Range(0f, 1f)] public float sfxVolume = 1f, musicVolume = 0.55f;
        public int voices = 16;

        readonly Dictionary<string, SfxBank.Entry> map = new Dictionary<string, SfxBank.Entry>();
        AudioSource[] pool;
        AudioSource music;
        int next;

        void Awake()
        {
            Instance = this;
            if (bank) foreach (var e in bank.entries) map[e.id] = e;
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var a = pool[i] = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 4f; a.maxDistance = 45f;
                a.dopplerLevel = 0f;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true; music.playOnAwake = false; music.spatialBlend = 0f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Play(string id, Vector3 pos, float volume, float pitch)
        {
            if (!map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) return;
            var a = pool[next = (next + 1) % pool.Length];
            a.transform.position = pos;
            a.spatialBlend = e.spatial;
            a.pitch = pitch * (1f + Random.Range(-e.pitchJitter, e.pitchJitter));
            a.PlayOneShot(e.clips[Random.Range(0, e.clips.Length)], e.volume * volume * sfxVolume);
        }

        public void PlayMusic(string id)
        {
            if (!map.TryGetValue(id, out var e) || e.clips == null || e.clips.Length == 0) { music.Stop(); return; }
            if (music.clip == e.clips[0] && music.isPlaying) return;
            music.clip = e.clips[0];
            music.volume = e.volume * musicVolume;
            music.Play();
        }
    }
}
