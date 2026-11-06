using UnityEngine;

namespace ShooterParty.UI
{
    // SP_Menu's living background: the camera drifts around the ice while the four critters idle, hop and cheer.
    public class MenuBackdrop : MonoBehaviour
    {
        public Transform cameraRig;
        public Vector3 centre = new Vector3(0f, 0.6f, 0f);
        public float radius = 9.5f, height = 3.2f, degreesPerSecond = 4f;
        public CritterAnimator[] critters = new CritterAnimator[0];
        float angle = 200f, nextAct;

        void Start()
        {
            for (int i = 0; i < critters.Length; i++)
            {
                var look = critters[i] ? critters[i].GetComponentInParent<CritterLook>() : null;
                if (look) look.Apply(i);
            }
            nextAct = Time.time + 1.5f;
        }

        void Update()
        {
            angle += degreesPerSecond * Time.deltaTime;
            if (cameraRig)
            {
                var a = angle * Mathf.Deg2Rad;
                var pos = centre + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
                cameraRig.SetPositionAndRotation(pos, Quaternion.LookRotation(centre - pos));
            }
            if (Time.time < nextAct || critters.Length == 0) return;
            nextAct = Time.time + Random.Range(1.2f, 2.6f);
            var c = critters[Random.Range(0, critters.Length)];
            if (!c) return;
            switch (Random.Range(0, 4))
            {
                case 0: c.PlayTaunt(); break;
                case 1: c.PlayWin(); break;
                case 2: c.PlayJump(); break;
                default: c.PlaySpit(); break;
            }
        }
    }
}
