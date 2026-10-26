using System.Collections.Generic;
using UnityEngine;

namespace ShooterParty.UI
{
    // On every client: the crown rides the critter whose seat is SessionManager.CrownSlot (the current game's top
    // scorer), so everyone can see whom to chase. An eliminated leader drops it; the HUD card keeps the icon.
    public class CrownView : MonoBehaviour
    {
        public LeaderCrown crownPrefab;

        LeaderCrown crown;
        NetworkCritter wearer;

        public static NetworkCritter Wearer(int crownSlot, IReadOnlyList<NetworkCritter> critters)
        {
            if (crownSlot < 0) return null;
            foreach (var c in critters) if (c && c.IsSpawned && c.Slot.Value == crownSlot && !c.Eliminated.Value) return c;
            return null;
        }

        void LateUpdate()
        {
            var s = SessionManager.Instance;
            var want = s && s.IsSpawned ? Wearer(s.CrownSlot.Value, NetworkCritter.All) : null;
            if (want == wearer && (crown || !want)) return;
            wearer = want;
            if (!want) { if (crown) crown.Detach(); return; }
            if (!crown) crown = Instantiate(crownPrefab);   // lost with the last critter or scene; make a new one
            crown.Attach(want.gameObject);
        }
    }
}
