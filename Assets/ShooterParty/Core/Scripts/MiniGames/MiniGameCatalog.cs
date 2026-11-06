using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty
{
    // The list of playable mini-games. Rebuilt by "ShooterParty/Rebuild core assets" from every
    // MiniGameDefinition under Assets/ShooterParty, so adding a mini-game never edits core code.
    [CreateAssetMenu(menuName = "ShooterParty/Mini-game catalog", fileName = "MiniGameCatalog")]
    public class MiniGameCatalog : ScriptableObject
    {
        public List<MiniGameDefinition> games = new List<MiniGameDefinition>();
        public List<Preset> presets = new List<Preset>();   // built-in playlists; the first is a new room's default

        [Serializable]
        public class Preset
        {
            public string name;
            public List<string> games = new List<string>();
        }

        public MiniGameDefinition Find(string id)
        {
            foreach (var g in games) if (g && g.id == id) return g;
            return null;
        }

        public bool Has(string id) => Find(id) != null;
    }
}
