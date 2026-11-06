using ShooterParty;
using UnityEngine;

namespace IceLoop
{
    // The bumper car under a critter: paints the body in the seat's colour (the car is part of the critter prefab, so
    // the colour follows NetworkCritter.ColourIndex) and spins the little light on the pole.
    public class IceCar : MonoBehaviour
    {
        public Renderer[] tinted = new Renderer[0];
        public Transform beacon;

        NetworkCritter critter;
        int shown = -1;
        MaterialPropertyBlock block;

        void Awake() { critter = GetComponentInParent<NetworkCritter>(); block = new MaterialPropertyBlock(); }

        void Update()
        {
            if (beacon) beacon.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
            if (!critter || !critter.IsSpawned || shown == critter.ColourIndex.Value) return;
            shown = critter.ColourIndex.Value;
            var colour = CritterLook.Colours[Mathf.Clamp(shown, 0, 3)];
            foreach (var r in tinted)
            {
                if (!r) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", colour);
                r.SetPropertyBlock(block);
            }
        }
    }
}
