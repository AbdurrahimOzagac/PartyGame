using UnityEngine;
using UnityEngine.EventSystems;

namespace KnockbackArena.UI
{
    // Hover tick on buttons (SFX9). Clicks are played by the views that own the buttons.
    public class UiHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData e) => Sfx.Ui("UiHover");
    }
}
