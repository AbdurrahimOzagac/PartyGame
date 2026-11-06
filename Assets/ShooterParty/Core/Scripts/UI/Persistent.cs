using UnityEngine;

namespace ShooterParty.UI
{
    // Keeps SP_Boot's UI, audio and event system alive across scene loads (one of each, ever).
    public class Persistent : MonoBehaviour
    {
        public string key = "ShooterParty.Persistent";
        static readonly System.Collections.Generic.HashSet<string> alive = new System.Collections.Generic.HashSet<string>();

        void Awake()
        {
            if (!alive.Add(key)) { Destroy(gameObject); return; }
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy() => alive.Remove(key);
    }
}
