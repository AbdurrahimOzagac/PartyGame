using Unity.Netcode;
using UnityEngine;

namespace ShooterParty
{
    // Everything the core needs to list, introduce and run one mini-game. Create one per mini-game
    // (Assets > Create > ShooterParty > Mini-game); the catalog picks every definition under Assets/ShooterParty up.
    [CreateAssetMenu(menuName = "ShooterParty/Mini-game", fileName = "MiniGame")]
    public class MiniGameDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id = "my_minigame";              // stable, lowercase: playlists store it
        public string displayName = "My Mini-game";
        public string tagline = "";
        [TextArea] public string[] howToPlay = new string[0];   // 2-3 short lines on the intro card
        public string controlsHint = "WASD move · Mouse aim · Hold click to spit · Shift dash";
        public Sprite icon;

        [Header("Running it")]
        public string sceneName;                        // must be in the build settings
        public NetworkObject controllerPrefab;          // a NetworkObject with your MiniGameController subclass
        public NetworkObject critterPrefab;             // CubeCritter_Net or a variant of it
        public int minPlayers = 3, maxPlayers = 4;
        public float durationSeconds = 120f;
        public int[] placementPoints = { 4, 3, 2, 1 };
        public bool supportsBots = true;

        [Header("The shared critter and shot")]
        public CritterConfig critter = CritterConfig.Default;
        public ShotEffect shotEffect;
        public GameObject hudWidget;                    // optional, shown under the player cards
    }
}
