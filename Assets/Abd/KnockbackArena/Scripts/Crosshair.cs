using UnityEngine;

namespace Abd.KnockbackArena
{
    // Ekran ortasına küçük bir nişan noktası çizer (Canvas gerektirmez).
    public class Crosshair : MonoBehaviour
    {
        [SerializeField] float size = 6f;

        void OnGUI()
        {
            GUI.DrawTexture(new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size),
                Texture2D.whiteTexture);
        }
    }
}
