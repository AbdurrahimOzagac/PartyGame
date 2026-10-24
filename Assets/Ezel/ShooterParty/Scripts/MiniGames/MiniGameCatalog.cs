using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty
{
    // The list of playable mini-games. Rebuilt by "ShooterParty/Rebuild core assets" from every
    // MiniGameDefinition under Assets/Ezel, so adding a mini-game never edits core code.
    [CreateAssetMenu(menuName = "ShooterParty/Mini-game catalog", fileName = "MiniGameCatalog")]
    public class MiniGameCatalog : ScriptableObject
    {
        public List<MiniGameDefinition> games = new List<MiniGameDefinition>();

        public MiniGameDefinition Find(string id)
        {
            foreach (var g in games) if (g && g.id == id) return g;
            return null;
        }

        public bool Has(string id) => Find(id) != null;
    }
}
