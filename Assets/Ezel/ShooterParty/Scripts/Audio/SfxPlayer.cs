using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty
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
        float musicBase = 1f;
        Coroutine fade;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
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

        // Crossfades (0.6 s out, 0.6 s in) so lobby -> game -> results never cuts hard.
        public void PlayMusic(string id)
        {
            AudioClip clip = null; float baseVol = 1f;
            if (map.TryGetValue(id ?? "", out var e) && e.clips != null && e.clips.Length > 0) { clip = e.clips[0]; baseVol = e.volume; }
            if (clip && music.clip == clip && music.isPlaying) return;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(clip, baseVol));
        }

        System.Collections.IEnumerator Crossfade(AudioClip clip, float baseVol)
        {
            float from = music.volume;
            for (float t = 0f; music.isPlaying && t < 0.6f; t += Time.unscaledDeltaTime) { music.volume = Mathf.Lerp(from, 0f, t / 0.6f); yield return null; }
            music.Stop();
            musicBase = baseVol;
            if (!clip) yield break;
            music.clip = clip;
            music.volume = 0f;
            music.Play();
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { music.volume = Mathf.Lerp(0f, musicBase * musicVolume, t / 0.6f); yield return null; }
            music.volume = musicBase * musicVolume;
            fade = null;
        }

        // Settings sliders call this; it applies at once, also to the track that is playing.
        public void SetVolumes(float sfx, float musicVol)
        {
            sfxVolume = sfx;
            musicVolume = musicVol;
            if (fade == null) music.volume = musicBase * musicVolume;
        }
    }
}
