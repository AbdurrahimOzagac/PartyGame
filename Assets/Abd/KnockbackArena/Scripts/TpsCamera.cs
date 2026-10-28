using UnityEngine;

namespace Abd.KnockbackArena
{
    // Karakterin arkasında duran TPS kamera. Mouse yatay: karakterle birlikte döner, dikey: sadece kamerayı eğer.
    public class TpsCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] KnockbackArenaSettings settings;

        PlayerInputReader input;
        PlayerMotor motor;
        EliminationOnFall elimination;
        float pitch;

        void Start()
        {
            pitch = settings.cameraStartPitch;
            if (target == null) return;
            input = target.GetComponent<PlayerInputReader>();
            motor = target.GetComponent<PlayerMotor>();
            elimination = target.GetComponent<EliminationOnFall>();
        }

        void LateUpdate()
        {
            // Hedef elenince ya da yok olunca kamera olduğu yerde kalır.
            if (target == null || (elimination != null && elimination.IsEliminated)) return;

            if (input != null)
                pitch = Mathf.Clamp(pitch - input.LookDelta.y * settings.mouseSensitivity,
                    settings.cameraMinPitch, settings.cameraMaxPitch);

            float yaw = motor != null ? motor.Yaw : target.eulerAngles.y;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * settings.cameraPivotHeight;
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * settings.cameraDistance, rotation);
        }
    }
}
