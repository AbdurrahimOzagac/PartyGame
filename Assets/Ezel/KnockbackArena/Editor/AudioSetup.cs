using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KnockbackArena.EditorTools
{
    // Audio import settings (UI/audio brief, Unity import table) + the SfxBank that maps ids to clip variants.
    // SFX come from blender/v2/sfx.py (procedural), music from Higgsfield Seed Audio cut to loops by music_loop.py.
    public class KnockbackAudioImport : AssetPostprocessor
    {
        static readonly string[] Pcm = { "SFX_Spit", "SFX_Beep", "SFX_Go", "SFX_Ui", "SFX_BlobPop", "SFX_DashReady", "SFX_Pickup" };
        static readonly string[] Vorbis = { "SFX_Wheee", "SFX_KO", "SFX_Fanfare" };

        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/KnockbackArena/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var file = Path.GetFileNameWithoutExtension(assetPath);
            var s = ai.defaultSampleSettings;
            if (assetPath.Contains("/Audio/Music/"))
            {
                ai.forceToMono = false;
                ai.loadInBackground = true;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = false;
            }
            else
            {
                ai.forceToMono = true;
                ai.loadInBackground = false;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = Pcm.Any(file.StartsWith) ? AudioCompressionFormat.PCM
                                    : Vorbis.Any(file.StartsWith) ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.7f;
                s.preloadAudioData = true;
            }
            ai.defaultSampleSettings = s;
        }
    }

    public static class AudioSetup
    {
        public const string BankPath = CubeCritterSetup.Root + "/Audio/SFX_Bank.asset";

        // id -> (file prefix, volume, spatial blend). Spatial 0 = UI / jingles / music.
        static readonly (string id, string prefix, float volume, float spatial)[] Entries =
        {
            ("Spit", "SFX_Spit_", 0.9f, 0.6f), ("Hit", "SFX_Hit_", 1f, 0.6f), ("Splat", "SFX_Splat_", 0.8f, 0.7f),
            ("BlobPop", "SFX_BlobPop_", 0.8f, 0.7f), ("Whoosh", "SFX_Whoosh_", 0.8f, 0.6f), ("Wheee", "SFX_Wheee_", 1f, 0.5f),
            ("Splash", "SFX_Splash_", 1f, 0.4f), ("Land", "SFX_Land_", 0.9f, 0.6f), ("Step", "SFX_Step_", 0.8f, 0.7f),
            ("Jump", "SFX_Jump_", 0.9f, 0.5f), ("Pickup", "SFX_Pickup_", 1f, 0.4f),
            ("Beep", "SFX_Beep_", 1f, 0f), ("Go", "SFX_Go_", 1f, 0f), ("KO", "SFX_KO_", 0.9f, 0f), ("Fanfare", "SFX_Fanfare_", 1f, 0f),
            ("UiClick", "SFX_UiClick_", 0.8f, 0f), ("UiHover", "SFX_UiHover_", 0.8f, 0f), ("UiBack", "SFX_UiBack_", 0.8f, 0f),
            ("DashReady", "SFX_DashReady_", 0.8f, 0f),
            ("MusicGameplay", "MUS_Gameplay", 1f, 0f), ("MusicLobby", "MUS_Lobby", 1f, 0f),
        };

        public static SfxBank BuildBank()
        {
            var root = CubeCritterSetup.Root + "/Audio";
            AssetDatabase.ImportAsset(root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { root })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c).ToList();
            var bank = AssetDatabase.LoadAssetAtPath<SfxBank>(BankPath);
            if (!bank)
            {
                AssetDatabase.DeleteAsset(BankPath);   // a stale asset whose script reference no longer resolves
                bank = ScriptableObject.CreateInstance<SfxBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            var list = new List<SfxBank.Entry>();
            foreach (var (id, prefix, volume, spatial) in Entries)
            {
                var found = clips.Where(c => c.name.StartsWith(prefix)).OrderBy(c => c.name).ToArray();
                if (found.Length == 0) Debug.LogWarning($"[Audio] no clips for '{id}' ({prefix}*)");
                list.Add(new SfxBank.Entry { id = id, clips = found, volume = volume, spatial = spatial, pitchJitter = spatial > 0f ? 0.04f : 0f });
            }
            bank.entries = list.ToArray();
            EditorUtility.SetDirty(bank);
            return bank;
        }
    }
}
